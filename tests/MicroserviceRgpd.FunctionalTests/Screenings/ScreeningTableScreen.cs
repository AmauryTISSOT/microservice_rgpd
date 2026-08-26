using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.FunctionalTests.Layout;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// L'écran d'<b>une table</b> du rapport : la grappe du regard de l'<c>Operator</c>, celle où le
/// voisinage porte du sens.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le test le plus important de ce fichier est
/// <see cref="ShowsEveryColumnOfTheTableIncludingThoseWhereNothingWasSeen"/>.</b> Les
/// <c>Unflagged</c> <b>sont</b> l'écran — 92,5 % du contenu réel d'un relevé — et un écran qui ne
/// rendrait que les signalées serait un écran où l'omission a cessé d'être relisible : une colonne
/// qu'on ne montre pas est une colonne que personne ne relit jamais.
/// </para>
/// <para>
/// ⚠️ <b>Le moteur n'est pas doublé</b>, comme sur tout le reste de cette surface : ce sont les vrais
/// lexiques gelés qui décident ici quelle colonne est signalée.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ScreeningTableScreen(CustomWebApplicationFactory<Program> factory)
{
  private readonly ScreeningSurface _surface = new(factory);

  /// <summary>
  /// ⚠️ <b>Toutes les colonnes de la table, et il n'existe aucun moyen de les filtrer.</b> Un
  /// <c>Screening</c> qui filtre les non signalées cesse d'être ce contexte : ce que le service n'a
  /// pas vu ne se rattrape nulle part ailleurs que sur cet écran.
  /// </summary>
  [Fact]
  public async Task ShowsEveryColumnOfTheTableIncludingThoseWhereNothingWasSeen()
  {
    var table = await DepositAndOpenAsync(
      ScreeningSurface.Column("id_adh", position: 1),
      ScreeningSurface.Column("email", position: 2),
      ScreeningSurface.Column("montant", position: 3),
      ScreeningSurface.Column("date_crea", table: "cotisations", position: 1));

    // Les trois de la table ouverte, signalées comme non signalées.
    ScreeningSurface.BlockOf(table, "id_adh").ShouldNotBeNull();
    ScreeningSurface.BlockOf(table, "email").ShouldNotBeNull();
    ScreeningSurface.BlockOf(table, "montant").ShouldNotBeNull();

    // Au moins une de ces trois est une colonne où le moteur n'a rien vu, et elle est là quand même.
    table.ShouldContain("Rien n'a été vu");

    // ⚠️ Et il y en a EXACTEMENT trois. Nommer trois blocs présents ne dit rien d'un quatrième
    // qu'on aurait laissé tomber — et la colonne qu'un écran laisse tomber est très exactement
    // celle que personne ne relira jamais.
    ScreeningSurface.ColumnCountOf(table).ShouldBe(3);

    // Et la table voisine n'est pas de la partie : l'unité de travail est UNE table.
    ScreeningSurface.BlockOf(table, "date_crea").ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Il n'existe aucune commande pour masquer les colonnes.</b> Le filtre n'est pas absent par
  /// oubli : un filtre, même refermable, rétablit l'<c>Omission silencieuse</c> — l'
  /// <c>Operator</c> le pose une fois, ne le rouvre jamais, et l'écran redevient celui des seules
  /// signalées sans qu'aucune ligne de doctrine n'ait bougé.
  /// </summary>
  [Fact]
  public async Task OffersNoWayAtAllToHideTheColumnsWhereNothingWasSeen()
  {
    var table = await DepositAndOpenAsync(
      ScreeningSurface.Column("id_adh", position: 1),
      ScreeningSurface.Column("email", position: 2));

    // Aucune case, aucun sélecteur : la surface ne propose rien à cocher ni à choisir.
    //
    // ⚠️ LE BALAYAGE PORTE SUR LE `main`, ET NON SUR LA PAGE ENTIÈRE. Le layout partagé pose depuis
    // le panneau latéral une case à cocher dans chaque page — celle qui porte l'état du repli, et
    // qui n'a rien à voir avec les colonnes. Balayer la page entière ferait échouer ce test sur une
    // case qui n'est pas une commande de cet écran ; ce qu'il garde est ce que l'ÉCRAN propose.
    var content = LayoutSurface.MainOf(table);

    content.ShouldNotContain("<select");
    content.ShouldNotContain("type=\"checkbox\"");

    // ⚠️ Les SEULS formulaires de l'écran sont les arbitrages — un par colonne, fiche ou ligne,
    // plus le geste de lot — et aucun ne masque quoi que ce soit. Interdire tout <form> était
    // tenable tant que l'écran était en lecture seule ; ce qui doit rester interdit est le
    // formulaire qui MASQUE, jamais celui qui tranche.
    // ⚠️ Le compte attendu se DÉDUIT de la présence du geste de lot, qui n'est rendu que si la table
    // a encore quelque chose à sa portée : figer « +1 » ferait tomber ce test le jour où un jeu de
    // colonnes n'en offrirait plus aucune — et il tomberait pour une raison étrangère à ce qu'il garde.
    var batchGestures = Regex.Matches(table, "handler=Batch").Count;

    Regex.Matches(table, "<form").Count.ShouldBe(
      ScreeningSurface.ColumnCountOf(table) + batchGestures,
      "Il y a un formulaire par colonne et au plus un geste de lot, et aucun autre : un formulaire "
      + "de plus serait un filtre.");

    // ⚠️ Et le lot n'en est un que par ce qu'il NE porte PAS : il ne nomme aucune colonne. Le compte
    // reste donc celui des colonnes — un name=\"Column\" de plus serait une liste de colonnes
    // postée, c'est-à-dire le chemin par lequel un formulaire forgé écarterait en masse des signalées.
    Regex.Matches(table, "name=\"Column\"").Count.ShouldBe(ScreeningSurface.ColumnCountOf(table));

    // ⚠️ LE REPLI D'UNE FICHE N'EST PAS UN FILTRE, et c'est le nom lisible qui l'atteste : une
    // colonne signalée déjà tranchée se replie, mais son nom et son issue restent à l'écran. Ce qui
    // se replie est ce qu'on a fini de lire, jamais ce qu'on n'a pas encore vu.
    table.ShouldContain("email");

    // Et le geste lui-même n'écoute aucun paramètre de filtre : le demander ne change rien.
    var asked = WebUtility.HtmlDecode(await _surface.ReadAsync(
      $"{ScreeningSurface.Table}?schema=public&table=adherents&flagged=true&filtre=signalees"));

    ScreeningSurface.ColumnCountOf(asked).ShouldBe(ScreeningSurface.ColumnCountOf(table));
    asked.ShouldContain("Rien n'a été vu");
  }

  /// <summary>
  /// <b>Une adresse sans table nommée ne rend pas un écran vide</b> : elle ramène au rapport, d'où
  /// les tables s'ouvrent par leur nom.
  /// </summary>
  [Fact]
  public async Task SendsBackToTheReportWhenNoTableIsNamedAtAll()
  {
    await _surface.DepositAsync(ScreeningSurface.Paste(ScreeningSurface.Column("email")));

    var response = await _surface.Client.GetAsync(ScreeningSurface.Table);

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.Headers.Location!.OriginalString.ShouldContain(ScreeningSurface.Report);
  }

  /// <summary>
  /// <b>L'ordre est celui du relevé</b>, jamais l'alphabétique : c'est le seul qui garde à
  /// <c>adr_l1</c> le voisinage de <c>adr_l2</c>, <c>cp</c> et <c>ville</c>, et ce voisinage est ce
  /// qui rend l'arbitrage possible.
  /// </summary>
  [Fact]
  public async Task KeepsTheColumnsInTheOrderOfTheListingRatherThanSortingThem()
  {
    // Collées dans l'ordre du schéma, qui n'est pas l'alphabétique : « ville » y précède « adr_l1 ».
    var table = await DepositAndOpenAsync(
      ScreeningSurface.Column("ville", position: 1),
      ScreeningSurface.Column("adr_l1", position: 2),
      ScreeningSurface.Column("cp", position: 3));

    // ⚠️ Les trois sont signalées : elles se lisent donc dans le MÊME temps de l'écran, et leur
    // ordre relatif y est celui du relevé. L'écran range les signalées avant les Unflagged, ce qui
    // est une mise en page ; à l'intérieur d'un temps, rien ne trie.
    var rendered = new[] { "ville", "adr_l1", "cp" }
      .Select(column => ScreeningSurface.BlockOf(table, column) is { } block
        ? table.IndexOf(block, StringComparison.Ordinal)
        : -1)
      .ToArray();

    rendered.ShouldAllBe(position => position >= 0, "Les trois colonnes doivent être rendues.");

    rendered.SequenceEqual(rendered.Order()).ShouldBeTrue(
      "Les colonnes se rendent dans l'ordre du relevé : trié, « adr_l1 » serait passé devant "
      + "« ville », et le voisinage qui rend la table lisible aurait disparu.");
  }

  /// <summary>
  /// <b>L'arbitrage se fait sur pièces</b> : une colonne signalée porte sa catégorie, le <b>nom de
  /// la règle</b> qui a déclenché, et le <b>motif en prose française</b>. « <c>ContactDetails</c>,
  /// 0,72 » ne s'arbitre pas.
  /// </summary>
  [Fact]
  public async Task CarriesTheCategoryTheRuleNameAndTheProseReasonOnEveryFlaggedColumn()
  {
    var table = await DepositAndOpenAsync(
      ScreeningSurface.Column("id_adh", position: 1),
      ScreeningSurface.Column("email", position: 2));

    var flagged = ScreeningSurface.BlockOf(table, "email");

    flagged.ShouldNotBeNull("« email » est au lexique gelé : le moteur réel doit la signaler.");

    // La catégorie, en français.
    flagged.ShouldContain("Coordonnées");

    // Le NOM DE LA RÈGLE qui a déclenché — jamais une échelle, jamais un score.
    flagged.ShouldContain("correspondance exacte");

    // Le MOTIF, en clair, en PLEINE LARGEUR et dans la fiche : ni tronqué, ni replié derrière une
    // infobulle, où il n'existerait plus.
    flagged.ShouldMatch("(?s)class=\"reason\">\\s*\\S");

    // ⚠️ Et c'est le motif de CETTE colonne-ci, non un texte de remplissage : le moteur cite le
    // jeton qui a déclenché, et un motif générique se lirait comme une pièce sans l'être.
    ReasonOf(flagged).ShouldContain("email");

    // ⚠️ Le degré se lit comme une RÈGLE, jamais comme un score : la carte a exclu le nombre, et
    // l'écran ne le réintroduit pas par la bande — ni pourcentage, ni barre de progression.
    flagged.ShouldNotMatch(@"\d\s*%");
    table.ShouldNotContain("<progress");
    table.ShouldNotContain("<meter");
  }

  /// <summary>
  /// ⚠️ <b>L'invariant du contexte, visible à l'écran : une colonne <c>Unflagged</c> ne porte aucun
  /// motif.</b> C'est ce qui sépare « rien vu » de « vu et écarté » — et un motif posé là aurait fait
  /// croire à un jugement sur une donnée que le service n'a jamais lue.
  /// </summary>
  [Fact]
  public async Task LeavesNoReasonAtAllOnAColumnWhereNothingWasSeen()
  {
    var table = await DepositAndOpenAsync(
      ScreeningSurface.Column("montant", position: 1),
      ScreeningSurface.Column("email", position: 2));

    var nothingSeen = ScreeningSurface.BlockOf(table, "montant");

    nothingSeen.ShouldNotBeNull("« montant » n'est à aucun lexique : elle doit être rendue non signalée.");
    nothingSeen.ShouldContain("Rien n'a été vu");

    // ⚠️ Aucun motif, et pas même la place d'en poser un : ni « aucun motif », ni un tiret qui se
    // lirait comme un motif. Le second temps de l'écran n'a pas de colonne de motif du tout.
    nothingSeen.ShouldNotContain("class=\"reason\"");
  }

  /// <summary>
  /// <b>La clause d'incomplétude est due sur toute réponse rendant une <c>ScreenedColumn</c></b>, et
  /// cet écran n'en rend que ça. Ses comptes portent sur le <b>relevé entier</b> : une clause bornée
  /// à la table ouverte aurait dit d'un écran de trois colonnes qu'il est le périmètre de la
  /// détection.
  /// </summary>
  [Fact]
  public async Task CarriesTheIncompletenessClauseOnTheAnswerThatRendersColumns()
  {
    var table = await DepositAndOpenAsync(
      ScreeningSurface.Column("id_adh", position: 1),
      ScreeningSurface.Column("email", position: 2),
      ScreeningSurface.Column("date_crea", table: "cotisations", position: 1));

    table.ShouldContain("Le périmètre lu");
    table.ShouldContain("Hors périmètre");
    table.ShouldContain("Hors de portée");
    table.ShouldContain("Ce rapport de détection et vos systèmes déclarés");

    // Les comptes sont ceux du relevé entier — trois colonnes dans deux tables — et non ceux des
    // deux colonnes de la table ouverte.
    table.ShouldContain("3 colonnes");
    table.ShouldContain("2 tables");
  }

  /// <summary>
  /// <b>Le verrou et les comptes portent sur le rapport entier, et se recalculent à ce rendu.</b>
  /// C'est ici qu'ils comptent le plus : une table relue jusqu'au bout est l'instant précis où l'on
  /// croit avoir fini, alors que trente-neuf tables sont intactes.
  /// </summary>
  [Fact]
  public async Task RendersTheLockAndTheCountsOfTheWholeReportRatherThanThoseOfTheOpenTable()
  {
    var table = await DepositAndOpenAsync(
      ScreeningSurface.Column("id_adh", position: 1),
      ScreeningSurface.Column("montant", table: "cotisations", position: 1));

    table.ShouldContain("Ce rapport de détection est inachevé");

    // Deux colonnes attendent dans le rapport, dont une seule dans la table ouverte.
    Counted(table, "En attente").ShouldBe(2);
    Counted(table, "Retenues").ShouldBe(0);
    Counted(table, "Retenues sur « rien signalé »").ShouldBe(0);
  }

  /// <summary>
  /// <b>Le rapport mène à ses tables</b> : sans le lien, l'unité de travail resterait une décision
  /// écrite nulle part dans la surface.
  /// </summary>
  [Fact]
  public async Task LetsTheOperatorOpenATableFromTheReportThatNamesIt()
  {
    var report = await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(ScreeningSurface.Column("email")));

    report.ShouldMatch(@"href=""[^""]*schema=public[^""]*table=adherents");
  }

  /// <summary>
  /// ⚠️ <b>Une table que le rapport courant ne porte pas ne se rend pas comme une table vide.</b>
  /// Une table vide portant la clause aurait fait passer une adresse mal recopiée pour une table
  /// réellement dépourvue de colonnes — et déclaré l'incomplétude de quelque chose qui n'existe pas.
  /// </summary>
  [Fact]
  public async Task SendsBackToTheReportRatherThanRenderingATableTheCurrentScreeningDoesNotHold()
  {
    await _surface.DepositAsync(ScreeningSurface.Paste(ScreeningSurface.Column("email")));

    var response = await _surface.Client.GetAsync(
      $"{ScreeningSurface.Table}?schema=public&table=une_table_qui_n_existe_pas");

    response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    response.Headers.Location!.OriginalString.ShouldContain(ScreeningSurface.Report);
  }

  /// <summary>
  /// <b>Le mot est <em>détection</em></b> — jamais <em>recensement</em>, <em>cartographie</em> ni
  /// <em>scan</em> — sur cet écran comme sur les autres. ⚠️ <b>Le témoin porte la doctrine, pas le
  /// mot</b> : il a gelé l'ancien mot du contexte tant que l'interface le disait, il gèle
  /// « détection » depuis, et il ne se supprime pas quand le mot change.
  /// </summary>
  [Fact]
  public async Task NamesTheGestureWithTheOnlyWordTheGlossaryGivesIt()
  {
    var table = await DepositAndOpenAsync(ScreeningSurface.Column("email"));

    table.ShouldContain("détection");
    table.ShouldNotContain("cartographie", Case.Insensitive);
    table.ShouldNotContain("scan", Case.Insensitive);
  }

  /// <summary>
  /// <b>Deux colonnes signalées par deux règles différentes portent deux noms de règle différents et
  /// deux motifs différents.</b> Éprouvée sur une seule règle, la colonne « Règle » aurait pu être
  /// une étiquette constante — et l'<c>Operator</c> aurait arbitré sur une pièce qui ne distingue
  /// rien.
  /// </summary>
  [Fact]
  public async Task TellsTwoDifferentRulesApartOnTheScreenRatherThanLabellingThemAlike()
  {
    var table = await DepositAndOpenAsync(
      ScreeningSurface.Column("email", position: 1),
      ScreeningSurface.Column(
        "ref_x", position: 2, columnComment: "L'adresse postale de l'adhérent."));

    var byName = ScreeningSurface.BlockOf(table, "email");
    var byComment = ScreeningSurface.BlockOf(table, "ref_x");

    byName.ShouldNotBeNull();
    byComment.ShouldNotBeNull("Le commentaire porte « adresse » : le moteur réel doit la signaler.");

    // Le motif de chacune cite ce qui a déclenché chez ELLE.
    ReasonOf(byComment).ShouldContain("adresse");
    ReasonOf(byName).ShouldNotBe(ReasonOf(byComment));
  }

  /// <summary>
  /// <b>L'écran se lit en deux temps</b> : les signalées d'abord, en fiches ; les <c>Unflagged</c>
  /// ensuite, avec le geste de lot.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le rythme de la table est une mise en page, jamais un geste de plus.</b> Rien n'est
  /// retiré de l'écran : les deux temps portent ensemble toutes les colonnes de la table, et
  /// l'ordre est celui de ce qui se lit une par une avant ce qui se tranche d'un geste.
  /// </remarks>
  [Fact]
  public async Task ReadsInTwoTimesTheFlaggedCardsFirstAndTheUnflaggedColumnsAfterThem()
  {
    var table = await DepositAndOpenAsync(
      ScreeningSurface.Column("email", position: 1),
      ScreeningSurface.Column("montant", position: 2));

    var flagged = ScreeningSurface.BlockOf(table, "email").ShouldNotBeNull();
    var unflagged = ScreeningSurface.BlockOf(table, "montant").ShouldNotBeNull();

    // Une signalée est une FICHE, motif compris ; une Unflagged reste une ligne du tableau.
    flagged.ShouldStartWith("<details");
    unflagged.ShouldStartWith("<tr");

    // Et le premier temps précède le second : ce qui se lit une par une vient avant ce qui se
    // tranche d'un geste.
    table.IndexOf(flagged, StringComparison.Ordinal)
      .ShouldBeLessThan(table.IndexOf(unflagged, StringComparison.Ordinal));

    // Le geste de lot appartient au second temps, et il est toujours là, inchangé.
    table.ShouldContain("handler=Batch");
  }

  /// <summary>
  /// ⚠️ <b>L'ouverture d'une fiche se DÉDUIT du domaine, et rien d'autre ne la décide.</b> Dépliée
  /// tant que la colonne attend, repliée dès qu'elle est arbitrée : il reste à l'écran exactement
  /// ce qu'il reste à lire, sans état client et sans une ligne de JavaScript.
  /// </summary>
  [Fact]
  public async Task LeavesAFlaggedCardOpenWhileItsColumnAwaitsAndFoldsItOnceArbitrated()
  {
    await _surface.DepositAsync(ScreeningSurface.Paste(
      ScreeningSurface.Column("email", position: 1),
      ScreeningSurface.Column("montant", position: 2)));

    var awaiting = ScreeningSurface
      .BlockOf(await ReadTheTableAsync(), "email")
      .ShouldNotBeNull();

    awaiting.ShouldMatch(@"^<details[^>]*\sopen[\s>]");

    await _surface.ArbitrateAsync("email", ScreenedColumnState.Retained.Name);

    var arbitrated = ScreeningSurface
      .BlockOf(await ReadTheTableAsync(), "email")
      .ShouldNotBeNull();

    arbitrated.ShouldNotMatch(@"^<details[^>]*\sopen[\s>]");

    // ⚠️ Repliée, la fiche dit toujours QUI elle est et CE QU'ON EN A DIT : un repli qui effacerait
    // l'issue rendrait l'écran illisible d'un coup d'œil, et l'Operator rouvrirait trente fiches
    // pour retrouver ce qu'il vient de trancher.
    arbitrated.ShouldContain("email");
    arbitrated.ShouldContain("retenue");
  }

  /// <summary>
  /// <b>Une fiche signalée réserve l'emplacement de son aperçu, sur la ligne de son motif.</b> Il
  /// est vide tant que le service ne lit aucune valeur — le chemin collé ne lui en donne aucune —
  /// et le ticket des aperçus le remplira sans rebâtir la fiche.
  /// </summary>
  [Fact]
  public async Task ReservesAnEmptyPreviewSlotOnTheReasonLineOfEveryFlaggedCard()
  {
    var table = await DepositAndOpenAsync(ScreeningSurface.Column("email", position: 1));

    var flagged = ScreeningSurface.BlockOf(table, "email").ShouldNotBeNull();

    flagged.ShouldContain("class=\"preview\"");

    // ⚠️ VIDE, et il le reste : un relevé collé ne porte aucune valeur, et un emplacement qui
    // dirait « aucun aperçu » ferait passer le chemin collé pour un scan qui n'aurait rien vu.
    flagged.ShouldMatch(@"class=""preview""[^>]*>\s*</");
  }

  /// <summary>
  /// ⚠️ <b>Zéro JavaScript, sans exception</b> — <c>ADR-0005</c> et <c>ADR-0009</c>. Le repli des
  /// fiches est celui du navigateur : le jour où il demanderait un script, c'est le repli qu'il
  /// faudrait retirer, pas la règle.
  /// </summary>
  [Fact]
  public async Task ServesNotOneLineOfJavaScriptToFoldItsCards()
  {
    var table = await DepositAndOpenAsync(
      ScreeningSurface.Column("email", position: 1),
      ScreeningSurface.Column("montant", position: 2));

    table.ShouldNotContain("<script", Case.Insensitive);
    table.ShouldNotContain(".js", Case.Insensitive);
    table.ShouldNotContain("onclick", Case.Insensitive);
    table.ShouldNotContain("ontoggle", Case.Insensitive);
  }

  /// <summary>L'écran d'une table, relu par une adresse neuve — jamais la page rendue par le POST.</summary>
  private async Task<string> ReadTheTableAsync()
  {
    return WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.TableOf()));
  }

  /// <summary>Dépose un relevé, puis ouvre la table <c>public.adherents</c> du rapport qu'il rend.</summary>
  private async Task<string> DepositAndOpenAsync(params string[] columns)
  {
    var deposited = await _surface.DepositAsync(ScreeningSurface.Paste(columns));

    deposited.StatusCode.ShouldBe(
      HttpStatusCode.Redirect,
      "Le dépôt d'un relevé sincère doit mener au rapport qu'il vient de produire.");

    return WebUtility.HtmlDecode(
      await _surface.ReadAsync($"{ScreeningSurface.Table}?schema=public&table=adherents"));
  }

  /// <summary>Le motif tel que la fiche le rend, débarrassé de son balisage.</summary>
  private static string ReasonOf(string card)
  {
    var reason = Regex.Match(card, @"class=""reason"">(.*?)</", RegexOptions.Singleline);

    reason.Success.ShouldBeTrue("La fiche ne porte aucun motif.");

    return reason.Groups[1].Value.Trim();
  }

  /// <summary>Ce qu'un compte du rapport vaut, lu là où l'écran le rend.</summary>
  private static int Counted(string table, string label)
  {
    var counted = Regex.Match(table, $@"<dt>{Regex.Escape(label)}</dt>\s*<dd>\s*(\d+)");

    counted.Success.ShouldBeTrue($"L'écran ne rend aucun compte sous « {label} ».");

    return int.Parse(counted.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
  }
}
