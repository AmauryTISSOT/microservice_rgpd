namespace MicroserviceRgpd.Web.Pages.Shared;

/// <summary>Un point d'entrée de la barre : ce qu'on lit, et où l'on arrive.</summary>
/// <param name="Label">Le libellé, en français — c'est un texte destiné à l'humain.</param>
/// <param name="Address">L'adresse de l'écran d'entrée, et la racine de tout ce qui pend sous lui.</param>
internal sealed record ChromeEntryPoint(string Label, string Address)
{
  /// <summary>
  /// Si l'écran rendu relève de ce point d'entrée. La comparaison est faite <b>par segments</b> :
  /// un dossier relève de la file, la reprise d'une déclaration du <c>Manifest</c>, et la table
  /// d'arbitrage du dépistage — ce que le préfixe de texte nu n'aurait pas su dire sans confondre
  /// aussi une adresse qui commence par les mêmes lettres.
  /// </summary>
  internal bool IsCurrent(PathString path)
  {
    return path.StartsWithSegments(Address, StringComparison.OrdinalIgnoreCase);
  }
}

/// <summary>
/// <b>La barre de navigation de la surface de l'<c>Operator</c></b> : le nom du service, puis les
/// trois points d'entrée du service, et le marquage de celui dont l'écran courant relève.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Trois entrées, et pas une quatrième.</b> L'historique des dépistages n'en est pas une : il
/// s'atteint depuis le rapport courant, et une barre à trois entrées se lit d'un coup d'œil quand
/// une barre à quatre se parcourt.
/// </para>
/// <para>
/// ⚠️ <b>Aucun compteur, aucun badge numérique</b>, et il ne doit jamais y en avoir. La règle des
/// chiffres que la file applique — un « 0 dossier en retard » se lit comme une mesure rassurante là
/// où la phrase dit ce qu'elle est — vaut d'autant plus pour une barre qui se répète sur tous les
/// écrans sans qu'on l'ouvre jamais.
/// </para>
/// </remarks>
internal static class ChromeNavigation
{
  /// <summary>Le nom du service, porté devant les trois liens.</summary>
  internal const string ServiceName = "Droits des personnes concernées";

  /// <summary>
  /// Les trois points d'entrée, dans l'ordre où la barre les pose : le travail à instruire, le
  /// paysage sur lequel on l'instruit, et le temps d'avant.
  /// </summary>
  internal static IReadOnlyList<ChromeEntryPoint> EntryPoints { get; } =
  [
    new("La file", "/dossiers"),
    new("Le paysage déclaré", "/manifest"),
    new("Le dépistage", "/depistage"),
  ];
}
