using System.Runtime.ExceptionServices;
using MicroserviceRgpd.Core.Qualifications;
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
/// revanche, ne laissent rien à qualifier : la panne du moteur principal ressort alors en
/// <see cref="QualificationEngineFailure"/>, puisque c'est son mode de défaillance qui décidera du
/// code rendu à l'appelant.
/// </para>
/// </remarks>
/// <param name="verdictEngine">Le moteur dont l'avis fait verdict, déclaré par le port et par son rôle.</param>
/// <param name="witness">Le moteur qui contrôle le verdict, déclaré par le même port et l'autre rôle.</param>
/// <param name="logger">
/// Le seul endroit où le silence d'un moteur laisse une trace lisible : le booléen de dégradation
/// dit à l'appelant que le service n'était pas entier, il ne dit pas à l'exploitant pourquoi.
/// </param>
public sealed class QualifyHandler(
  [FromKeyedServices(QualificationEngineRole.Verdict)] IQualificationEngine verdictEngine,
  [FromKeyedServices(QualificationEngineRole.Witness)] IQualificationEngine witness,
  ILogger<QualifyHandler> logger)
  : ICommandHandler<QualifyCommand, Result<QualificationOutcome>>
{
  /// <inheritdoc />
  public async ValueTask<Result<QualificationOutcome>> Handle(
    QualifyCommand command,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var verdictAnswer = AskAsync(verdictEngine, QualificationEngineRole.Verdict, command.Text, cancellationToken);
    var witnessAnswer = AskAsync(witness, QualificationEngineRole.Witness, command.Text, cancellationToken);

    await Task.WhenAll(verdictAnswer, witnessAnswer);

    var corroboration = Corroborate(verdictAnswer.Result, witnessAnswer.Result);

    return new QualificationOutcome(
      // Ordonné dans le temps, donc sans fragmentation d'index le jour où la trace d'audit
      // l'utilisera comme clé primaire.
      QualificationId: Guid.CreateVersion7(),
      Qualification: corroboration.Qualification,
      ReviewSignal: corroboration.ReviewSignal,
      Degraded: corroboration.Degraded,
      Justification: corroboration.Justification,
      CallerReference: command.CallerReference);
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
      var failure = verdict.Failure ?? witness.Failure!;

      if (failure is QualificationEngineFailure named)
      {
        ExceptionDispatchInfo.Throw(named);
      }

      // Un moteur peut se taire pour une raison que personne n'a prévue. Le domaine n'a pas à la
      // connaître, mais la double panne doit rester une double panne : la nommer ici évite qu'une
      // cause inattendue ne ressorte en erreur interne, alors que le service sait exactement ce qui
      // lui manque.
      throw new QualificationEngineFailure(
        "Aucun des deux moteurs n'a rendu d'avis : il ne reste rien à qualifier.", failure);
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
    try
    {
      return new EngineAnswer(await engine.QualifyAsync(text, cancellationToken), null);
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

      return new EngineAnswer(null, silence);
    }
  }

  /// <summary>Ce qu'un moteur a rendu : un avis, ou la raison de son silence — jamais les deux.</summary>
  private sealed record EngineAnswer(QualificationOpinion? Opinion, Exception? Failure);
}
