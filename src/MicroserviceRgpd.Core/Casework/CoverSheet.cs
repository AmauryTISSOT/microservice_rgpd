using System.Globalization;
using System.Text;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// La page que le service écrit lui-même dans chaque <see cref="Delivery"/> — <b>le seul texte du
/// dossier dont il soit l'auteur</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle énumère et ne compte jamais.</b> Pas de total, pas de ratio, pas de dénominateur : nommer
/// « l'export commercial transmis chaque mois à notre agence » est actionnable pour la personne là
/// où « 4 sur 6 » ne lui apprend rien et lui ment sur l'exhaustivité du recensement. Le chiffre
/// s'arrête au <c>Ledger</c>, dont le lecteur est le contrôle.
/// </para>
/// <para>
/// <b>Trois listes, et elles ne fusionnent pas.</b> Une pièce jointe, un système interrogé sans
/// rattachement, un système que la réponse ne couvre pas sont trois faits différents, et les
/// rapprocher ferait écrire à la personne un constat que personne n'a fait. La deuxième ne dit
/// <b>jamais</b> « vous n'avez rien chez nous » — un appel ne distingue pas « cherché, aucun
/// rattachement » de « désignation insuffisante », et la seule phrase vraie porte ce doute avec
/// elle en invitant la personne à fournir d'autres <see cref="Designation"/>.
/// </para>
/// <para>
/// <b>Le service l'écrit sans ouvrir une seule pièce.</b> Le <see cref="Manifest"/> et les
/// <see cref="Step"/> du dossier lui suffisent : l'enveloppe du transport distingue à elle seule la
/// pièce absente, la pièce vide et la pièce pleine. L'incomplétude ne coûte donc rien à
/// l'<c>Adapter</c>.
/// </para>
/// <para>
/// <b>Elle se clôt sur la non-exhaustivité du recensement</b>, sans quoi une déclaration qui
/// vieillit exprès prendrait l'autorité d'un recensement.
/// </para>
/// </remarks>
public sealed class CoverSheet
{
  /// <summary>Le nom du fichier sous lequel la page entre dans le paquet. Il est lu par un humain.</summary>
  public const string FileName = "page-de-garde.txt";

  private CoverSheet(
    DataSubjectRight right,
    IReadOnlyList<NamedSystem> joined,
    IReadOnlyList<NamedSystem> queriedWithoutAttachment,
    IReadOnlyList<NamedSystem> notCovered)
  {
    Right = right;
    Joined = joined;
    QueriedWithoutAttachment = queriedWithoutAttachment;
    NotCovered = notCovered;
  }

  /// <summary>Le droit au titre duquel cette réponse est faite — nommé en français, jamais par son article.</summary>
  public DataSubjectRight Right { get; }

  /// <summary>Les systèmes dont une pièce est jointe, une par système et jamais fusionnées.</summary>
  public IReadOnlyList<NamedSystem> Joined { get; }

  /// <summary>
  /// Les systèmes interrogés <b>sans trouver de rattachement sous les éléments dont nous
  /// disposons</b>, ou qui n'avaient rien à rendre.
  /// </summary>
  public IReadOnlyList<NamedSystem> QueriedWithoutAttachment { get; }

  /// <summary>
  /// Les systèmes que cette réponse ne couvre pas, <b>nommés un par un</b> dans les mots du champ
  /// « contient ». C'est cette liste qui dit à la personne quoi réclamer.
  /// </summary>
  public IReadOnlyList<NamedSystem> NotCovered { get; }

  /// <summary>
  /// Range les systèmes dont ce droit portait le travail dû, à partir du <see cref="Manifest"/> et
  /// des <see cref="Step"/> — et des enveloppes des pièces détenues, jamais de leur corps.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>L'univers est celui des <see cref="Step"/> du droit</b>, c'est-à-dire le paysage tel qu'il
  /// était recensé quand le dossier s'est ouvert. Le catalogue d'aujourd'hui sert à <b>nommer</b> ces
  /// systèmes, jamais à en ajouter ni à en retirer : une ligne qui s'évaporerait parce que quelqu'un
  /// a révisé le recensement entre-temps serait l'<c>Omission silencieuse</c> écrite de la main du
  /// service.
  /// </para>
  /// <para>
  /// <b>Une réserve en attente n'est pas un zéro.</b> Un système dont un humain n'a pas encore
  /// tranché la réserve n'entre pas dans la deuxième liste : quelque chose a bel et bien été trouvé
  /// sous ce qu'on avait, et écrire « interrogé sans rattachement » juste au-dessus de ces lignes-là
  /// serait faux. Il est <b>non couvert</b>, ce qu'il est.
  /// </para>
  /// </remarks>
  /// <param name="opened">Le dossier, pour ses <see cref="Step"/> et ce que ses appels ont rapporté.</param>
  /// <param name="right">Le droit au titre duquel cette réponse est faite.</param>
  /// <param name="manifest">Le catalogue d'aujourd'hui, qui donne les mots pour nommer les systèmes.</param>
  /// <param name="held">
  /// Les pièces détenues pour ce droit. Seuls leur système et le fait qu'elles soient vides sont lus ;
  /// leur corps ne s'ouvre pas.
  /// </param>
  /// <exception cref="ArgumentNullException">Un argument obligatoire est absent.</exception>
  /// <exception cref="ArgumentException">Le dossier ne porte pas ce droit.</exception>
  public static CoverSheet Compose(
    Case opened,
    DataSubjectRight right,
    Manifest manifest,
    IReadOnlyList<RetrievedData> held)
  {
    ArgumentNullException.ThrowIfNull(opened);
    ArgumentNullException.ThrowIfNull(right);
    ArgumentNullException.ThrowIfNull(manifest);
    ArgumentNullException.ThrowIfNull(held);

    var claim = opened.Claims.SingleOrDefault(one => one.Right == right)
      ?? throw new ArgumentException("Le dossier ne porte pas ce droit.", nameof(right));

    var catalogue = manifest.Systems.ToDictionary(system => system.Id);
    var pieces = held.Where(piece => piece.Right == right).ToDictionary(piece => piece.DeclaredSystem);

    var joined = new List<NamedSystem>();
    var queried = new List<NamedSystem>();
    var uncovered = new List<NamedSystem>();

    // Les systèmes du travail dû ET ceux dont une pièce est détenue. L'union n'est pas une précaution
    // : un système déclaré APRÈS l'ouverture du dossier n'a aucun `Step`, et sa pièce partirait
    // pourtant dans l'archive — la page de garde annoncerait alors moins que ce que la personne
    // reçoit, ce qui est la façon la plus sûre de lui faire croire qu'elle a tout.
    // L'ordre est celui du catalogue — le libellé, ordinal — puis l'identifiant pour ce qu'il ne
    // nomme plus : deux remises du même dossier rangent les mêmes systèmes dans le même ordre.
    var systems = claim.Steps
      .Select(step => step.DeclaredSystem)
      .Union(pieces.Keys)
      .OrderBy(id => catalogue.GetValueOrDefault(id)?.Label.Value ?? string.Empty, StringComparer.Ordinal)
      .ThenBy(id => id.Value, StringComparer.Ordinal);

    foreach (var id in systems)
    {
      var declared = catalogue.GetValueOrDefault(id);
      var named = new NamedSystem(id, declared?.Label, declared?.Contents);
      var piece = pieces.GetValueOrDefault(id);

      // Une pièce pleine est la seule chose qui vaille « joint » : une pièce vide n'est pas une
      // réponse, et la fondre dans la première liste ferait promettre un contenu qui n'existe pas.
      if (piece is not null)
      {
        (piece.IsEmpty ? queried : joined).Add(named);

        continue;
      }

      // Interrogé, et rien rendu pour cette personne : le seul cas où le service peut dire quelque
      // chose de ce système sans mentir — et il le dit en portant son doute.
      (QueriedForNothing(opened.LocatingIn(id)) ? queried : uncovered).Add(named);
    }

    return new CoverSheet(right, joined, queried, uncovered);
  }

  /// <summary>
  /// Ce système a-t-il été <b>interrogé sans que rien ne soit rattaché</b> ? Vrai d'un appel servi
  /// qui n'a rien rendu et sur lequel aucune réserve n'attend un humain.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Une réserve en attente n'est pas un zéro</b>, et c'est la condition la moins évidente :
  /// quelque chose a été trouvé sous ce qu'on avait, et ce qui manque est un regard, pas une
  /// désignation de plus. C'est la règle qui vaut déjà pour l'<see cref="OpenQuestion"/>.
  /// </remarks>
  private static bool QueriedForNothing(Locating? locating)
  {
    return locating is not null
      && locating.LastOutcome == AdapterOutcome.Served
      && !locating.HoldsAnAttachment
      && !locating.AwaitsAnArbitration;
  }

  /// <summary>
  /// Écrit la page, telle que la personne la lira. <b>Aucun chiffre n'y entre</b> — ni compte, ni
  /// taux, ni « N sur M », ni même le numéro de l'article : le droit s'y nomme en français.
  /// </summary>
  public string Write()
  {
    var page = new StringBuilder();

    page.AppendLine("Page de garde");
    page.AppendLine();
    page.AppendLine(CultureInfo.InvariantCulture, $"Ce document accompagne la réponse apportée à votre demande, au titre du {Right.FrenchLabel}.");
    page.AppendLine();
    page.AppendLine("Les pièces sont jointes côte à côte, une par système, telles que chaque système les a");
    page.AppendLine("rendues. Rien n'a été fusionné, rien n'a été réécrit.");
    page.AppendLine();

    Section(
      page,
      "Les systèmes dont une pièce est jointe",
      Joined,
      "Aucune pièce n'est jointe à cette réponse.");

    Section(
      page,
      "Les systèmes interrogés sans trouver de rattachement sous les éléments dont nous disposons",
      QueriedWithoutAttachment,
      "Aucun système n'est dans ce cas.");

    if (QueriedWithoutAttachment.Count > 0)
    {
      // Jamais « vous n'avez rien chez nous » : ce que le service ignore, c'est si la désignation
      // suffisait. La seule phrase vraie porte ce doute et rend la main à la personne.
      page.AppendLine("Si l'un de ces systèmes vous connaît sous une autre désignation — un autre courriel, un");
      page.AppendLine("ancien nom, une référence de client —, communiquez-la-nous : nous les interrogerons de");
      page.AppendLine("nouveau.");
      page.AppendLine();
    }

    Section(
      page,
      "Les systèmes que cette réponse ne couvre pas",
      NotCovered,
      "Aucun système recensé n'est laissé de côté par cette réponse.");

    // La clause de non-exhaustivité, toujours écrite : sans elle, une déclaration qui vieillit exprès
    // prendrait l'autorité d'un recensement.
    page.AppendLine("Cette liste est celle des systèmes recensés par le responsable de traitement. Elle ne");
    page.AppendLine("garantit pas qu'il n'en existe pas d'autres.");

    return page.ToString();
  }

  private static void Section(
    StringBuilder page,
    string heading,
    IReadOnlyList<NamedSystem> systems,
    string whenEmpty)
  {
    page.AppendLine(heading);
    page.AppendLine(new string('-', heading.Length));

    if (systems.Count == 0)
    {
      // Une liste vide se dit en mots et jamais par un zéro, comme partout ailleurs dans ce service.
      page.AppendLine(whenEmpty);
    }

    foreach (var system in systems)
    {
      page.AppendLine(CultureInfo.InvariantCulture, $"- {system.Describe()}");
    }

    page.AppendLine();
  }
}

/// <summary>
/// Un système tel que la <see cref="CoverSheet"/> le nomme : <b>dans les mots de celui qui l'a
/// déclaré</b>, et non par un identifiant technique qui n'apprendrait rien à la personne.
/// </summary>
/// <remarks>
/// <b>Un système que le catalogue ne porte plus garde sa ligne.</b> Il se nomme alors par son
/// identifiant, en disant que le recensement ne le décrit plus : une ligne qui s'évaporerait serait
/// l'<c>Omission silencieuse</c> écrite de la main du service.
/// </remarks>
/// <param name="DeclaredSystem">L'identifiant du système, celui du recensement.</param>
/// <param name="Label">Le nom du système, ou <c>null</c> si le catalogue ne le porte plus.</param>
/// <param name="Contents">
/// Ce que le système contient, dans les mots de celui qui l'a déclaré, ou <c>null</c> si le
/// catalogue ne le porte plus.
/// </param>
public sealed record NamedSystem(
  DeclaredSystemId DeclaredSystem,
  SystemLabel? Label,
  SystemContents? Contents)
{
  /// <summary>La ligne que la personne lit, en une phrase.</summary>
  public string Describe()
  {
    if (Label is null || Contents is null)
    {
      return $"{DeclaredSystem.Value} (le recensement ne décrit plus ce système)";
    }

    return $"{Label.Value.Value} — {Contents.Value.Value}";
  }
}
