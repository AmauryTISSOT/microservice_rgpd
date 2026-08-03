namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// La nature d'une <see cref="Designation"/> — ce qu'on tient, jamais ce que ça vaut.
/// <para>
/// Quatre valeurs, et <b>aucune hiérarchie entre elles</b>. Le vocabulaire est celui du contrat
/// d'<c>Adapter</c>, et il est identique quel que soit le canal d'entrée : il n'existe qu'un seul
/// chemin de recherche à maintenir.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Reference"/> n'est pas une clé.</b> C'est par elle que l'identifiant natif de
/// l'application entre — comme <b>une</b> désignation parmi d'autres, sans autorité particulière :
/// <c>clients.id</c> n'ouvre que trois tables sur onze, et lui donner le rang d'un identifiant de
/// personne ferait passer pour un recensement ce qui n'est qu'une clé de plus.
/// </para>
/// <para>
/// ⚠️ <b>Le membre du nom de personne s'appelle <see cref="PersonName"/> et non <c>Name</c></b> :
/// <c>SmartEnum</c> porte déjà un <c>Name</c>, qui est celui du membre. C'est <see cref="Token"/>,
/// et lui seul, qui circule — sur le fil comme en base : sans lui, le C# imposerait au contrat
/// public un mot que le contrat n'a pas choisi.
/// </para>
/// </remarks>
public sealed class DesignationKind : SmartEnum<DesignationKind>
{
  /// <summary>Une adresse électronique. Ni unique, ni exacte : <c>clients.email</c> ne l'est pas non plus.</summary>
  public static readonly DesignationKind Email = new(nameof(Email), 0, "email", "courriel");

  /// <summary>Un nom de personne, tel qu'il a été déclaré. Deux « Jean Dupont » sont deux fois le même nom.</summary>
  public static readonly DesignationKind PersonName = new(nameof(PersonName), 1, "name", "nom");

  /// <summary>Un numéro de téléphone.</summary>
  public static readonly DesignationKind Phone = new(nameof(Phone), 2, "phone", "téléphone");

  /// <summary>
  /// Une référence interne — celle de l'application, celle d'un dossier client, celle d'un contrat.
  /// C'est ici qu'entre l'identifiant natif de l'application, sans rang particulier.
  /// </summary>
  public static readonly DesignationKind Reference = new(nameof(Reference), 3, "reference", "référence");

  private DesignationKind(string name, int value, string token, string frenchLabel)
    : base(name, value)
  {
    Token = token;
    FrenchLabel = frenchLabel;
  }

  /// <summary>
  /// Le mot canonique de cette nature, <b>sur le fil comme en base</b>. Il est fixé par le contrat
  /// d'<c>Adapter</c>, et c'est la seule forme que le service écrit ou lit hors du C#.
  /// </summary>
  public string Token { get; }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>
  /// La nature que désigne ce mot, ou <c>null</c> si aucune — le vocabulaire est fermé, et un mot
  /// qu'il ignore n'est pas une nature qu'on devine.
  /// </summary>
  public static DesignationKind? FromToken(string? token)
  {
    // Comparaison ordinale : le contrat fixe quatre mots en minuscules ASCII, et une comparaison
    // sensible à la culture les rapprocherait différemment d'une machine à l'autre.
    return List.SingleOrDefault(kind => string.Equals(kind.Token, token, StringComparison.Ordinal));
  }
}
