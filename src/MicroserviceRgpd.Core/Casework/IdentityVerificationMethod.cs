namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// <b>Comment</b> l'humain qui a déposé la demande s'est assuré — ou ne s'est pas assuré — de
/// l'identité du demandeur. Vocabulaire fermé de quatre valeurs, et c'est la <b>moitié qui se
/// compte</b> d'une <see cref="IdentityMotivation"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle est fermée pour survivre à la clôture.</b> C'est cette moitié-là qui entre au
/// <c>EvidenceLog</c> : le contrôle juge une <i>pratique</i> en dénombrant des valeurs — « douze accès
/// ouverts sous <c>None</c> » est un fait qu'on lit d'un coup d'œil — là où l'autre moitié, le
/// détail en prose, est irréductiblement nominative et meurt avec le <see cref="Case"/>.
/// </para>
/// <para>
/// ⚠️ <b><see cref="None"/> est une réponse, jamais une absence de réponse.</b> Sans la valeur
/// laide, l'opérateur pressé coche la valeur propre — exactement comme
/// <see cref="IdentityDeclaration.Unverified"/> et <see cref="StepState.Untreated"/>. « Personne
/// n'a pesé » ne s'écrit donc pas ici : il s'écrit par l'<b>absence</b> de
/// <see cref="IdentityMotivation"/>, que l'écran réclame et n'obtient pas.
/// </para>
/// <para>
/// ⚠️ <b>Aucune de ces valeurs ne prétend qu'une identité a été vérifiée.</b> Le service ne vérifie
/// lui-même aucune identité — <c>Enregistré, jamais vérifié</c> : il enregistre ce qu'un humain
/// déclare avoir fait, et n'en juge jamais la valeur.
/// </para>
/// </remarks>
public sealed class IdentityVerificationMethod : SmartEnum<IdentityVerificationMethod>
{
  /// <summary>
  /// Un attribut a été recoupé <b>que le demandeur n'a pas reçu de nous</b> — une référence de
  /// contrat, un montant, une date. La restriction est le fond de la méthode : recouper ce que le
  /// service a lui-même envoyé ne prouve rien de plus que l'accès à une boîte aux lettres.
  /// </summary>
  public static readonly IdentityVerificationMethod AttributeCrosscheck =
    new(nameof(AttributeCrosscheck), 0, "recoupement d'un attribut que le demandeur n'a pas reçu de nous");

  /// <summary>L'<c>Operator</c> reconnaît personnellement la personne.</summary>
  public static readonly IdentityVerificationMethod PersonalRecognition =
    new(nameof(PersonalRecognition), 1, "reconnaissance personnelle");

  /// <summary>
  /// Un rappel a été passé sur un contact <b>déjà enregistré</b> chez le client, et non sur celui que
  /// la demande porte : rappeler le numéro écrit dans le courriel ne vérifie que le courriel.
  /// </summary>
  public static readonly IdentityVerificationMethod CallbackOnKnownContact =
    new(nameof(CallbackOnKnownContact), 2, "rappel sur un contact déjà enregistré");

  /// <summary>
  /// Aucune. <b>La valeur laide, déclarée comme telle</b> : quelqu'un a regardé le dossier, l'a pesé,
  /// et dit qu'il n'a rien fait. Elle vaut infiniment mieux qu'une méthode propre cochée par
  /// commodité, et le service n'a jamais le droit de la barrer.
  /// </summary>
  public static readonly IdentityVerificationMethod None = new(nameof(None), 3, "aucune");

  private IdentityVerificationMethod(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
