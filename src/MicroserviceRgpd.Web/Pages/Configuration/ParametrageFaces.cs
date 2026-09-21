using System.Globalization;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Web.Pages.Configuration;

/// <summary>Un onglet du Paramétrage : ce qu'on lit, et la face où il mène.</summary>
/// <param name="Label">Le libellé que l'onglet porte, en français — un texte destiné à l'humain.</param>
/// <param name="Address">L'adresse de la face.</param>
internal sealed record ParametrageTab(string Label, string Address);

/// <summary>
/// <b>Ce qu'une face du Paramétrage donne à rendre</b> : les six droits, celui qu'on règle, et la
/// face sur laquelle on le règle. La liste, l'en-tête du droit et les onglets se rendent tous trois
/// à partir de lui, sur les deux faces.
/// </summary>
/// <param name="Settings">Le Paramétrage tel qu'il se lit à cet instant.</param>
/// <param name="Selected">Le droit dont le détail est ouvert.</param>
/// <param name="Face">L'adresse de la face rendue.</param>
public sealed record ParametrageScene(Settings Settings, DataSubjectRight Selected, string Face)
{
  /// <summary>Le canal du droit ouvert, tel qu'il est enregistré.</summary>
  public ExerciseChannel Channel => Settings.ChannelFor(Selected);
}

/// <summary>
/// <b>Les deux faces du Paramétrage</b> — ce qu'elles ont en commun, et l'ordre dans lequel les
/// onglets les posent : l'adresse HTTP, puis le routage RabbitMQ. Les deux portent le <b>même
/// titre</b> et se lisent comme un seul écran à deux faces.
/// </summary>
/// <remarks>
/// <para>
/// L'écran se lit <b>en liste et détail</b> : les six droits d'un côté, chacun avec son réglage en
/// une ligne, et le droit ouvert de l'autre, seul, avec sa mini-form. Le droit ouvert est porté par
/// l'adresse — <c>?droit=Erasure</c> —, et c'est ce qui laisse passer d'une face à l'autre sans
/// perdre le droit en chemin.
/// </para>
/// <para>
/// ⚠️ <b>Ce n'est pas une région de navigation du layout.</b> Le panneau latéral décrit les points
/// d'entrée du service et garde ses quatre entrées ; la liste des droits et les onglets décrivent
/// <b>l'intérieur d'un écran</b>. Le Paramétrage ne gagne donc pas un cinquième point d'entrée, et
/// « Paramétrage » reste marqué courant sur les deux faces — on n'a pas quitté le Paramétrage.
/// </para>
/// <para>
/// ⚠️ <b>Ce sont des liens, et il n'y a pas une ligne de JavaScript derrière.</b> Chaque face, pour
/// chaque droit, est une adresse que le serveur rend entièrement ; le droit et l'onglet courants se
/// marquent au rendu, jamais au clic.
/// </para>
/// </remarks>
internal static class ParametrageFaces
{
  /// <summary>La face HTTP, et la racine du Paramétrage — celle où le panneau latéral mène.</summary>
  internal const string Http = "/parametrage";

  /// <summary>La face RabbitMQ, qui pend sous la racine plutôt que d'être une adresse voisine.</summary>
  internal const string RabbitMq = "/parametrage/rabbitmq";

  /// <summary>
  /// Le paramètre d'adresse qui porte le droit ouvert, par son <b>nom canonique</b> — celui que sa
  /// mini-form envoie déjà. Un nom qu'il ignore ouvre le premier droit plutôt qu'une erreur : ce
  /// n'est qu'une lecture.
  /// </summary>
  internal const string Choice = "droit";

  /// <summary>
  /// Le titre que les <b>deux</b> faces portent en tête, et que chacune donne à son onglet de
  /// navigateur. Il est écrit une fois ici : recopié dans deux gabarits, il aurait divergé.
  /// </summary>
  internal const string ScreenName = "Paramétrage du microservice RGPD";

  /// <summary>Les deux onglets, dans l'ordre où la page les pose.</summary>
  internal static IReadOnlyList<ParametrageTab> Tabs { get; } =
  [
    new("Adresse HTTP", Http),
    new("Routage RabbitMQ", RabbitMq),
  ];

  /// <summary>L'adresse d'une face, le droit donné ouvert.</summary>
  internal static string Address(string face, DataSubjectRight right) => $"{face}?{Choice}={right.Name}";

  /// <summary>
  /// La face où la liste mène un droit : <b>celle de son canal</b>, pour qu'on le relise là où il se
  /// règle. Un droit « non configuré » n'en a pas, et la liste reste alors sur la face courante.
  /// </summary>
  internal static string FaceOf(ExerciseChannel channel, string current) => channel switch
  {
    ExerciseChannel.HttpEndpoint => Http,
    ExerciseChannel.RabbitMq => RabbitMq,
    _ => current,
  };

  /// <summary>
  /// Le libellé d'un droit tel qu'il ouvre une ligne ou un titre — « Droit d'accès ». Le SmartEnum
  /// le porte en minuscule, parce qu'il se lit d'abord au milieu d'une phrase.
  /// </summary>
  internal static string Titled(DataSubjectRight right) =>
    string.Concat(right.FrenchLabel[..1].ToUpper(CultureInfo.GetCultureInfo("fr-FR")), right.FrenchLabel[1..]);
}
