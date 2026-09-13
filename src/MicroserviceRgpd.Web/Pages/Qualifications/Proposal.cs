using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Qualifications.Qualify;

namespace MicroserviceRgpd.Web.Pages.Qualifications;

/// <summary>
/// Ce que le handler <c>Propose</c> rend à <b>un autre écran</b> qui fait qualifier un texte : les
/// droits proposés, les deux axes par lesquels les lire, et la justification du moteur — <b>aucune
/// phrase de l'écran</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce n'est pas <c>QualifyResponse</c>, et ce type ne doit pas le devenir.</b> Le contrat
/// public appartient aux applications tierces (ADR-0011) : l'y rattacher ferait bouger l'un chaque
/// fois que l'écran change d'avis sur l'autre.
/// </para>
/// <para>
/// ⚠️ <b>Ni identifiant de qualification, ni référence appelante.</b> L'écran appelant ne relie pas
/// sa demande à la trace d'audit : il n'en a pas besoin, et une clé qu'on lui tendrait finirait
/// enregistrée sur la demande. Les phrases, elles, sont celles de l'écran appelant, dans son
/// vocabulaire.
/// </para>
/// </remarks>
/// <param name="Rights">
/// Les droits proposés, dans l'ordre de la taxonomie — <c>OutOfScope</c> y paraissant seul, comme un
/// verdict et jamais comme une liste vide.
/// </param>
/// <param name="ReviewSignal">L'urgence à relire, sous son nom canonique.</param>
/// <param name="Degraded">Vrai quand le service n'était pas entier.</param>
/// <param name="Justification">La phrase du moteur principal, ou <c>null</c> quand il n'en a rendu aucune.</param>
public sealed record Proposal(
  IReadOnlyList<ProposedRight> Rights,
  string ReviewSignal,
  bool Degraded,
  string? Justification)
{
  /// <summary>Ce qu'une qualification rendue propose à l'écran appelant.</summary>
  public static Proposal Of(QualificationOutcome outcome)
  {
    ArgumentNullException.ThrowIfNull(outcome);

    return new Proposal(
      [.. outcome.Qualification.Rights.OrderBy(right => right.Value).Select(ProposedRight.Of)],
      outcome.ReviewSignal.ToString(),
      outcome.Degraded,
      outcome.Justification);
  }
}

/// <summary>
/// Un droit proposé : son <b>nom canonique</b>, que l'écran appelant compare à ses propres valeurs,
/// et son <b>libellé français</b>, lu sur la taxonomie et jamais réécrit.
/// </summary>
public sealed record ProposedRight(string Name, string Label)
{
  /// <summary>Le droit tel que la taxonomie le nomme.</summary>
  public static ProposedRight Of(DataSubjectRight right)
  {
    ArgumentNullException.ThrowIfNull(right);

    return new ProposedRight(right.Name, right.FrenchLabel);
  }
}
