using System.Diagnostics;
using System.Runtime.ExceptionServices;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Core.Qualifications.Audit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MicroserviceRgpd.UseCases.Qualifications.Qualify;

/// <summary>
/// Demande leur avis aux deux moteurs, <b>en même temps</b>, et fait de leur confrontation une
/// qualification rendue à l'appelant.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ce handler ne décide de rien.</b> Il obtient deux avis, les confie à la règle du domaine, et
/// forge une identité. La table de corroboration, le repli et l'interdiction faite au mode dégradé
/// de se présenter comme corroboré vivent dans <see cref="Corroboration"/>, où ils se testent sans
/// réseau, sans base et sans container.
/// </para>
/// <para>
/// <b>En parallèle, sans négociation</b> : le temps de réponse du service est alors celui du seul
/// appel LLM, le lexique ne coûtant qu'une fraction de milliseconde. Les enchaîner ferait payer
/// l'entrecontrôle en latence, et donnerait la première bonne raison de l'éteindre.
/// </para>
/// <para>
/// <b>Un moteur muet n'est pas une panne du service.</b> Quelle que soit la raison de son silence,
/// son avis manque, et c'est tout ce que le domaine a besoin d'en savoir. Les deux muets, en
/// revanche, ne laissent rien à qualifier : la panne du moteur principal ressort alors telle quelle,
/// puisque c'est son mode de défaillance qui décidera du code rendu à l'appelant.
/// </para>
/// </remarks>
/// <param name="verdictEngine">Le moteur dont l'avis fait verdict, déclaré par le port et par son rôle.</param>
/// <param name="witness">Le moteur qui contrôle le verdict, déclaré par le même port et l'autre rôle.</param>
/// <param name="auditTrail">
/// L'écrit de l'acte. Il est demandé <b>avant</b> de répondre, jamais après : un verdict rendu sans
/// trace serait un verdict dont plus personne ne pourrait répondre.
/// </param>
/// <param name="clock">
/// L'horloge, injectée pour que l'instant de l'acte se dicte en test plutôt que d'être lu sur la
/// machine qui l'exécute.
/// </param>
/// <param name="logger">
/// Le seul endroit où le silence d'un moteur laisse une trace lisible : le booléen de dégradation
/// dit à l'appelant que le service n'était pas entier, il ne dit pas à l'exploitant pourquoi.
/// </param>
public sealed class QualifyHandler(
  [FromKeyedServices(QualificationEngineRole.Verdict)] IQualificationEngine verdictEngine,
  [FromKeyedServices(QualificationEngineRole.Witness)] IQualificationEngine witness,
  IQualificationAuditTrail auditTrail,
  TimeProvider clock,
  ILogger<QualifyHandler> logger)
  : ICommandHandler<QualifyCommand, Result<QualificationOutcome>>
{
  /// <inheritdoc />
  public async ValueTask<Result<QualificationOutcome>> Handle(
    QualifyCommand command,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    // L'instant de l'acte est celui où il commence, et non celui où on l'écrit : lu après coup, il
    // ne se raccorderait pas à la latence totale, qui court depuis ici.
    var occurredAt = clock.GetUtcNow();
    var started = clock.GetTimestamp();

    var verdictAnswer = AskAsync(verdictEngine, QualificationEngineRole.Verdict, command.Text, cancellationToken);
    var witnessAnswer = AskAsync(witness, QualificationEngineRole.Witness, command.Text, cancellationToken);

    await Task.WhenAll(verdictAnswer, witnessAnswer);

    var corroboration = Corroborate(verdictAnswer.Result, witnessAnswer.Result);

    var outcome = new QualificationOutcome(
      // Ordonné dans le temps, donc sans fragmentation d'index puisque la trace d'audit en fait sa
      // clé primaire.
      QualificationId: Guid.CreateVersion7(),
      Qualification: corroboration.Qualification,
      ReviewSignal: corroboration.ReviewSignal,
      Degraded: corroboration.Degraded,
      Justification: corroboration.Justification,
      CallerReference: command.CallerReference);

    // Qualifier, écrire, répondre — dans cet ordre, et sans rattrapage. Un échec d'écriture remonte
    // tel quel : il n'y a pas de `200` dégradé pour une trace qui ne s'est pas écrite, y compris
    // quand la qualification elle-même l'était. Une base indisponible est une panne du service, pas
    // un mode dégradé.
    await auditTrail.RecordAsync(
      EntryOf(command, outcome, occurredAt, verdictAnswer.Result, witnessAnswer.Result, clock.GetElapsedTime(started)),
      cancellationToken);

    return outcome;
  }

  /// <summary>
  /// Met par écrit ce qui vient d'avoir lieu : le verdict rendu, <b>et les deux avis dont il est
  /// tiré</b>, chacun avec le moteur qui l'a rendu.
  /// </summary>
  /// <remarks>
  /// Rien n'y nomme le mode dégradé : un avis manquant entre nul, et c'est cette nullité qui
  /// l'enregistre — en distinguant le repli lexical du lexique absent, que le booléen public
  /// recouvre.
  /// </remarks>
  private static QualificationAuditEntry EntryOf(
    QualifyCommand command,
    QualificationOutcome outcome,
    DateTimeOffset occurredAt,
    EngineAnswer verdict,
    EngineAnswer witness,
    TimeSpan totalLatency)
  {
    return new QualificationAuditEntry(
      outcome.QualificationId,
      occurredAt,
      command.Text,
      outcome.Qualification,
      outcome.ReviewSignal,
      verdict.Opinion,
      witness.Opinion,
      outcome.Justification,
      outcome.CallerReference,
      // La trace de télémétrie est prise telle qu'elle est, et jamais fabriquée : son absence dit
      // que la propagation est cassée en amont, ce qu'un identifiant de repli masquerait.
      Activity.Current?.TraceId.ToString(),
      totalLatency,
      // La latence d'un moteur muet reste nulle, comme son avis : mesurer le temps qu'il a mis à ne
      // rien rendre ferait passer une panne pour une lenteur.
      verdict.Opinion is null ? null : verdict.Latency,
      witness.Opinion is null ? null : witness.Latency);
  }

  /// <summary>
  /// Confronte les deux réponses, ou présente la panne du moteur principal quand aucune n'est un avis.
  /// </summary>
  private static Corroboration Corroborate(EngineAnswer verdict, EngineAnswer witness)
  {
    if (verdict.Opinion is null && witness.Opinion is null)
    {
      // La panne du moteur principal prime : le service suit le mode de défaillance de celui dont
      // l'avis aurait fait verdict, et non celui du témoin qui n'a fait que tomber en même temps.
      ExceptionDispatchInfo.Throw(verdict.Failure ?? witness.Failure!);
    }

    return Corroboration.Between(verdict.Opinion, witness.Opinion);
  }

  /// <summary>
  /// Demande son avis à un moteur, et fait de son silence une absence plutôt qu'une exception.
  /// </summary>
  /// <remarks>
  /// L'annulation de l'appelant est la seule chose qui traverse : ce n'est pas un moteur qui a
  /// échoué, c'est un appelant qui est parti, et lui rendre un verdict dégradé qu'il n'attend plus
  /// n'aurait aucun sens.
  /// </remarks>
  private async Task<EngineAnswer> AskAsync(
    IQualificationEngine engine,
    string role,
    RightsRequestText text,
    CancellationToken cancellationToken)
  {
    // La mesure est prise par moteur, et non autour des deux : appelés en parallèle, leurs durées se
    // recouvrent, et une mesure commune ne dirait plus lequel a coûté le temps de la réponse.
    var started = clock.GetTimestamp();

    try
    {
      var opinion = await engine.QualifyAsync(text, cancellationToken);

      return new EngineAnswer(opinion, null, clock.GetElapsedTime(started));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      throw;
    }
    catch (Exception silence)
    {
      // Rattrapage large assumé : le domaine n'a besoin de connaître aucune des raisons qu'un moteur
      // peut avoir de se taire, et en énumérer quelques-unes ferait de la suivante une panne du
      // service entier. Elle est journalisée ici pour ne pas disparaître avec l'exception.
      logger.LogWarning(silence, "Le moteur tenant le rôle {Role} n'a rendu aucun avis.", role);

      return new EngineAnswer(null, silence, clock.GetElapsedTime(started));
    }
  }

  /// <summary>
  /// Ce qu'un moteur a rendu : un avis, ou la raison de son silence — jamais les deux —, et le temps
  /// qu'il y a mis.
  /// </summary>
  private sealed record EngineAnswer(QualificationOpinion? Opinion, Exception? Failure, TimeSpan Latency);
}
