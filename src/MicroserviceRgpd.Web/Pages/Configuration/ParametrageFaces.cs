namespace MicroserviceRgpd.Web.Pages.Configuration;

/// <summary>Un onglet du Paramétrage : ce qu'on lit, et la face où il mène.</summary>
/// <param name="Label">Le libellé que l'onglet porte, en français — un texte destiné à l'humain.</param>
/// <param name="Address">L'adresse de la face.</param>
internal sealed record ParametrageTab(string Label, string Address);

/// <summary>
/// <b>Les deux faces du Paramétrage</b> — ce qu'elles ont en commun, et l'ordre dans lequel les
/// onglets les posent : la configuration HTTP, puis la configuration RabbitMQ. Les deux portent le
/// <b>même titre</b> et se lisent comme un seul écran à deux faces.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce n'est pas une région de navigation du layout.</b> Le panneau latéral décrit les points
/// d'entrée du service et garde ses quatre entrées ; les onglets décrivent <b>l'intérieur d'un
/// écran</b>. Le Paramétrage ne gagne donc pas un cinquième point d'entrée, et « Paramétrage » reste
/// marqué courant sur les deux faces — on n'a pas quitté le Paramétrage.
/// </para>
/// <para>
/// ⚠️ <b>Ce sont des liens, et il n'y a pas une ligne de JavaScript derrière.</b> Chaque face est une
/// adresse que le serveur rend entièrement ; l'onglet courant se marque au rendu, jamais au clic.
/// </para>
/// </remarks>
internal static class ParametrageFaces
{
  /// <summary>La face HTTP, et la racine du Paramétrage — celle où le panneau latéral mène.</summary>
  internal const string Http = "/parametrage";

  /// <summary>La face RabbitMQ, qui pend sous la racine plutôt que d'être une adresse voisine.</summary>
  internal const string RabbitMq = "/parametrage/rabbitmq";

  /// <summary>
  /// Le titre que les <b>deux</b> faces portent en tête, et que chacune donne à son onglet de
  /// navigateur. Il est écrit une fois ici : recopié dans deux gabarits, il aurait divergé.
  /// </summary>
  internal const string ScreenName = "Paramétrage du microservice RGPD";

  /// <summary>Les deux onglets, dans l'ordre où la page les pose.</summary>
  internal static IReadOnlyList<ParametrageTab> Tabs { get; } =
  [
    new("Configuration HTTP", Http),
    new("Configuration RabbitMQ", RabbitMq),
  ];
}
