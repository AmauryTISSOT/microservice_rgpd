using System.Net;

using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// <b>Le témoin de la clause, niveau 2 — sur l'écran, écrit par la négative, et symétrique.</b> Un
/// répertoire des phrases de chaque chemin, dont aucune ne peut apparaître dans quoi que ce soit
/// d'affiché quand l'origine est l'autre.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le niveau 1 seul serait vert pendant que l'écran ment.</b> Le rappel sous le compteur
/// <c>Signalées</c> est écrit <b>en dur</b> dans <c>Report.cshtml</c> et <c>Archive.cshtml</c> :
/// aucun test sur l'objet ne le verra jamais, et c'est très exactement la phrase que l'
/// <c>Operator</c> lit le plus souvent — bien avant la clause elle-même.
/// </para>
/// <para>
/// ⚠️ <b>Symétrique, et ce n'est pas une élégance.</b> Sans le répertoire inverse, une
/// correspondance retournée passerait : le code qui rendrait la phrase du scan sur un relevé collé
/// serait vert, et la promesse la plus forte du produit tomberait chez l'<c>Operator</c> qui colle
/// sans que rien ne rougisse.
/// </para>
/// <para>
/// ⚠️ <b>Les cinq surfaces sont regardées, pas une.</b> Le rapport, la table, l'archive, la table
/// archivée et l'historique : la clause est une propriété de <b>toute</b> réponse, et l'écran qu'on
/// oublie de regarder est celui où elle s'écaille.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class IncompletenessClauseOnEveryScreen(CustomWebApplicationFactory<Program> factory)
{
  /// <summary>
  /// Les phrases du <b>chemin collé</b>. Aucune ne peut apparaître sur un écran dont l'origine est
  /// <c>Scanné</c> : chacune y affirmerait que le service n'a vu aucune valeur, ce qui serait faux.
  /// </summary>
  private static readonly string[] PastedOnly =
  [
    "celui que vous avez collé",
    "n'a jamais vu une seule valeur",
  ];

  /// <summary>
  /// Les phrases du <b>chemin scanné</b>. Aucune ne peut apparaître sur un écran dont l'origine est
  /// <c>Collé</c> : chacune y inventerait un prélèvement qui n'a pas eu lieu.
  /// </summary>
  private static readonly string[] ScannedOnly =
  [
    "celui que le compte de connexion a présenté au service",
    "Le service ne peut pas savoir si ce compte lui a présenté toute la base",
    "valeurs au plus de chaque colonne",
    "dans l'ordre où elle les a rendues",
    "valeurs ne disent pas ce qu'une colonne contient",
  ];

  private readonly ScreeningSurface _surface = new(factory);

  /// <summary>
  /// Sur un relevé <b>collé</b>, aucune des phrases du chemin scanné n'apparaît nulle part — et
  /// celles du chemin collé y sont, mot pour mot.
  /// </summary>
  [Fact]
  public async Task NeverLetsAScannedPhraseReachAnyScreenOfAPastedListing()
  {
    var screens = await EveryScreenOfAsync(scanned: false);

    foreach (var (address, rendered) in screens)
    {
      foreach (var phrase in ScannedOnly)
      {
        rendered.ShouldNotContain(
          phrase,
          Case.Sensitive,
          $"L'écran {address} rend « {phrase} » sur un relevé COLLÉ : il invente un prélèvement qui "
          + "n'a pas eu lieu.");
      }
    }

    // ⚠️ Le versant positif, sans quoi le répertoire par la négative resterait vert sur un écran qui
    // ne dirait plus rien du tout.
    var report = screens[ScreeningSurface.Report];

    foreach (var phrase in PastedOnly)
    {
      report.ShouldContain(phrase, Case.Sensitive);
    }
  }

  /// <summary>
  /// Sur un relevé <b>scanné</b>, aucune des phrases du chemin collé n'apparaît nulle part — et
  /// celles du chemin scanné y sont, mot pour mot.
  /// </summary>
  [Fact]
  public async Task NeverLetsAPastedPhraseReachAnyScreenOfAScannedListing()
  {
    var screens = await EveryScreenOfAsync(scanned: true);

    foreach (var (address, rendered) in screens)
    {
      foreach (var phrase in PastedOnly)
      {
        rendered.ShouldNotContain(
          phrase,
          Case.Sensitive,
          $"L'écran {address} rend « {phrase} » sur un relevé SCANNÉ : il promet que le service n'a "
          + "vu aucune valeur, alors qu'il en a lu.");
      }
    }

    var report = screens[ScreeningSurface.Report];

    foreach (var phrase in ScannedOnly)
    {
      report.ShouldContain(phrase, Case.Sensitive);
    }
  }

  /// <summary>
  /// ⚠️ <b>Le rappel sous le compteur <c>Signalées</c> est le point que le niveau 1 ne voit pas</b>,
  /// et il est dû sur l'archive autant que sur le rapport du jour : un écran d'archive plus
  /// rassurant que celui du jour même est ce que ce dépôt interdit partout.
  /// </summary>
  [Fact]
  public async Task ReplacesTheTailOfTheFlaggedHintOnBothReportAndArchive()
  {
    var screens = await EveryScreenOfAsync(scanned: true);

    foreach (var address in new[] { ScreeningSurface.Report, ScreeningSurface.Archive })
    {
      var rendered = screens[address];

      rendered.ShouldContain(
        $"le service n'a lu que {ColumnPreview.MaxValuesInWords} valeurs par colonne",
        Case.Sensitive,
        $"Le rappel sous « Signalées » a perdu sa queue sur {address} : ici la queue EST la preuve.");

      rendered.ShouldContain(
        $"{ColumnPreview.MaxValuesInWords} valeurs ne disent pas ce qu'une colonne contient",
        Case.Sensitive);
    }
  }

  /// <summary>
  /// Les quatre comptes se lisent sur le rapport, <b>zéros compris</b> — et rien de tout cela ne
  /// s'écrit sur un relevé collé.
  /// </summary>
  [Fact]
  public async Task ShowsTheFourFamiliesOfMissingPreviewsOnTheScannedPathAndNoneOfThemOnThePastedOne()
  {
    var scanned = (await EveryScreenOfAsync(scanned: true))[ScreeningSurface.Report];

    foreach (var reason in PreviewAbsenceReason.List)
    {
      scanned.ShouldContain(
        reason.FrenchLabel,
        Case.Sensitive,
        $"La famille « {reason.FrenchLabel} » ne se lit pas : agréger, c'est éteindre la seule des "
        + "quatre qui appelle un geste.");
    }

    var pasted = (await EveryScreenOfAsync(scanned: false))[ScreeningSurface.Report];

    foreach (var reason in PreviewAbsenceReason.List)
    {
      pasted.ShouldNotContain(
        reason.FrenchLabel,
        Case.Sensitive,
        "Un compte d'absence d'aperçu sur un relevé COLLÉ affirme qu'un prélèvement a eu lieu.");
    }
  }

  /// <summary>
  /// ⚠️ <b>Quand aucun aperçu n'a abouti, le rapport le dit à son échelle</b>, en plus de la raison
  /// que porte chaque ligne et jamais à sa place.
  /// </summary>
  [Fact]
  public async Task SaysAtTheScaleOfTheReportWhenNotOneSinglePreviewSucceeded()
  {
    await _surface.DepositAndReadTheReportAsync(ScreeningSurface.Paste(
      ScreeningSurface.Column("id_adh", position: 1),
      ScreeningSurface.Column("adr_l1", position: 2)));

    // ⚠️ Décodé : Razor encode l'apostrophe en &#x27;, et un ShouldNotContain sur le HTML brut
    // serait vert quoi que la page dise.
    (await ReadDecodedAsync(ScreeningSurface.Report))
      .ShouldNotContain("Aucune valeur n'a pu être prélevée", Case.Sensitive);

    var screening = await _surface.CurrentScreeningAsync();

    // Toutes les colonnes du relevé sans aperçu : c'est le rapport aveugle, celui qui devrait faire
    // rescanner.
    await _surface.MakeItScannedAsync(
      screening,
      ("id_adh", PreviewAbsenceReason.AccessDenied),
      ("adr_l1", PreviewAbsenceReason.AccessDenied));

    var blind = await ReadDecodedAsync(ScreeningSurface.Report);

    blind.ShouldContain("Aucune valeur n'a pu être prélevée", Case.Sensitive);

    // ⚠️ ET RIEN SUR CET ÉCRAN NE DIT LE CONTRAIRE. Le rappel chiffré et la mise en garde du
    // premier venu affirmeraient cinq valeurs par colonne à trois lignes de la clause qui dit
    // qu'aucune n'a pu être prélevée : une surestimation de ce que le service a lu, dans la partie
    // du produit qui existe pour ne rien surestimer.
    blind.ShouldNotContain(
      $"n'a lu que {ColumnPreview.MaxValuesInWords} valeurs par colonne", Case.Sensitive);
    blind.ShouldNotContain("dans l'ordre où elle les a rendues", Case.Sensitive);

    // La phrase du chemin collé reste interdite : le service a demandé des valeurs et s'est fait
    // refuser, il ne peut pas promettre qu'il n'en a jamais vu.
    blind.ShouldNotContain("n'a jamais vu une seule valeur", Case.Sensitive);
  }

  /// <summary>
  /// Un relevé déposé, rendu scanné ou laissé collé, puis <b>archivé par un second dépôt</b> — et
  /// les cinq écrans qui le rendent, lus tels qu'un <c>Operator</c> les lit.
  /// </summary>
  /// <remarks>
  /// Le rendu est décodé : Razor encode l'apostrophe en <c>&amp;#x27;</c>, et un répertoire de
  /// phrases françaises cherché dans le HTML brut serait vert sur des phrases pourtant présentes.
  /// </remarks>
  private async Task<Dictionary<string, string>> EveryScreenOfAsync(bool scanned)
  {
    await _surface.DepositAndReadTheReportAsync(ScreeningSurface.Paste(
      ScreeningSurface.Column("id_adh", position: 1),
      ScreeningSurface.Column("adr_l1", position: 2),
      ScreeningSurface.Column("photo", position: 3)));

    var screening = await _surface.CurrentScreeningAsync();

    if (scanned)
    {
      await _surface.MakeItScannedAsync(
        screening,
        ("adr_l1", PreviewAbsenceReason.AccessDenied),
        ("photo", PreviewAbsenceReason.UnsampleableType));
    }

    var screens = new Dictionary<string, string>(StringComparer.Ordinal)
    {
      [ScreeningSurface.Report] = await ReadDecodedAsync(ScreeningSurface.Report),
      [ScreeningSurface.Table] = await ReadDecodedAsync(ScreeningSurface.TableOf()),
      [ScreeningSurface.History] = await ReadDecodedAsync(ScreeningSurface.History),
    };

    // Le second dépôt archive le premier : c'est le seul chemin qui existe vers l'écran d'archive,
    // et il n'écrase pas ce que le premier a lu.
    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(ScreeningSurface.Column("id_adh", table: "cotisations", position: 1)));

    screens[ScreeningSurface.Archive] =
      await ReadDecodedAsync(ScreeningSurface.ArchiveOf(screening));
    screens[ScreeningSurface.ArchivedTable] =
      await ReadDecodedAsync(ScreeningSurface.ArchivedTableOf(screening));

    return screens;
  }

  private async Task<string> ReadDecodedAsync(string address)
  {
    return WebUtility.HtmlDecode(await _surface.ReadAsync(address));
  }
}
