namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// D'où vient la reconnaissance d'un droit dans un <see cref="Case"/>. Vocabulaire fermé de trois
/// valeurs, figé à la naissance du <see cref="Claim"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un <see cref="Claim"/> garde la porte sous laquelle il est né.</b> L'origine ne se réécrit
/// pas : une déclaration relevée en fin de dossier ne doit pas rendre rétroactivement propre un
/// droit ouvert sur rien, et la preuve d'hier ne se corrige pas par la saisie d'aujourd'hui.
/// </para>
/// <para>
/// <b><see cref="Proposed"/> n'est pas un état d'attente.</b> Il n'existe aucun vestibule où une
/// demande patienterait avant d'entrer : le <see cref="Case"/> est ouvert, le délai de l'art. 12.3
/// court, et la confirmation a lieu <b>dans le dossier</b>, sous les yeux de l'<c>Operator</c>. Un
/// <c>Claim</c> <c>Proposed</c> non confirmé est une <c>OpenQuestion</c> — visible pendant que le
/// compteur tourne.
/// </para>
/// </remarks>
public sealed class ClaimOrigin : SmartEnum<ClaimOrigin>
{
  /// <summary>
  /// La personne l'a désigné elle-même — une case cochée sur un formulaire, un droit nommé dans son
  /// courriel. C'est le seul cas où la reconnaissance ne repose sur l'interprétation de personne.
  /// </summary>
  public static readonly ClaimOrigin Named = new(nameof(Named), 0, "désigné par la personne", confirmedAtBirth: true);

  /// <summary>
  /// L'<c>Operator</c> l'affirme : la personne n'a nommé aucun droit, et un humain a lu sa demande
  /// et dit lequel elle exerçait. Il porte déjà la signature de quelqu'un — rien ne reste à confirmer.
  /// </summary>
  public static readonly ClaimOrigin Attested = new(nameof(Attested), 1, "attesté par l'opérateur", confirmedAtBirth: true);

  /// <summary>
  /// Une <c>Qualification</c> l'a <b>proposé</b>, et aucun humain ne l'a encore repris à son compte.
  /// C'est la seule origine qui naisse <b>non confirmée</b> : une machine ne produit jamais une
  /// issue, et un droit qu'elle seule aurait reconnu se lirait dans dix ans comme s'il avait été
  /// reconnu par quelqu'un.
  /// </summary>
  public static readonly ClaimOrigin Proposed = new(nameof(Proposed), 2, "proposé en amont", confirmedAtBirth: false);

  private ClaimOrigin(string name, int value, string frenchLabel, bool confirmedAtBirth)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
    ConfirmedAtBirth = confirmedAtBirth;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>
  /// Un <see cref="Claim"/> né de cette origine porte-t-il <b>déjà</b> le fait d'un humain ? Vrai de
  /// tout sauf <see cref="Proposed"/>.
  /// </summary>
  /// <remarks>
  /// <b>Ce n'est pas un quatrième <see cref="ClaimState"/></b>, et il n'y en aura pas : la
  /// confirmation dit d'où le droit vient, pas où en est la réponse du service. Un état de plus
  /// aurait fait porter à la réponse due à la personne une question de provenance, et il aurait
  /// fallu le faire retomber quelque part à la clôture.
  /// </remarks>
  public bool ConfirmedAtBirth { get; }
}
