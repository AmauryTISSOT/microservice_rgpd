using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.UseCases.Qualifications.Qualify;

/// <summary>
/// Demande son avis au moteur, et en fait une qualification rendue à l'appelant.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un seul moteur existe : le service est donc en permanence diminué.</b> Le témoin lexical
/// tient lieu de verdict, l'entrecontrôle n'a pas lieu, et la réponse le dit — signal forcé à
/// « à relire », booléen de dégradation levé. Ce n'est pas un provisoire bricolé : c'est
/// exactement le repli que le contrat spécifie quand le moteur principal manque, livré avant lui.
/// </para>
/// <para>
/// Le jour où un second moteur arrive, ce n'est pas ce handler qui apprend à comparer deux avis :
/// la règle de corroboration vit dans le domaine, se teste sans réseau, et prend ici la place des
/// trois valeurs forcées ci-dessous.
/// </para>
/// </remarks>
/// <param name="witness">
/// Le seul moteur du service à ce stade. Il est déclaré par le port, jamais par son implémentation :
/// le use case ignore qu'un sidecar Python et du HTTP existent.
/// </param>
public sealed class QualifyHandler(IQualificationEngine witness)
  : ICommandHandler<QualifyCommand, Result<QualificationOutcome>>
{
  /// <inheritdoc />
  public async ValueTask<Result<QualificationOutcome>> Handle(
    QualifyCommand command,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var opinion = await witness.QualifyAsync(command.Text, cancellationToken);

    return new QualificationOutcome(
      // Ordonné dans le temps, donc sans fragmentation d'index le jour où la trace d'audit
      // l'utilisera comme clé primaire.
      QualificationId: Guid.CreateVersion7(),
      Qualification: opinion.Qualification,
      // Aucun contrôle indépendant n'a eu lieu : « à relire » le dit exactement, et reste le seul
      // signal atteignable — « corroboré » est interdit à tout mode dégradé.
      ReviewSignal: ReviewSignal.NeedsReview,
      Degraded: true,
      CallerReference: command.CallerReference);
  }
}
