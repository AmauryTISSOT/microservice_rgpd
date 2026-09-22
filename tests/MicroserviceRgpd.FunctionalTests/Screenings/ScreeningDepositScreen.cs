using System.Net;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// La tranche de bout en bout : l'<c>Operator</c> colle un relevé, <b>d'un seul geste synchrone</b>,
/// et obtient son rapport sommaire.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le moteur n'est pas doublé.</b> Ce sont les vrais lexiques gelés qui répondent : une
/// doublure aurait rendu ces tests verts sur un service dont le moteur ne serait jamais branché, et
/// c'est très exactement le montage qu'on cherche à éprouver ici.
/// </para>
/// <para>
/// ⚠️ <b>Le test le plus important de ce fichier n'est pas celui du chemin heureux</b> : c'est
/// <see cref="KeepsTheLockOnUntilEveryColumnWhereNothingWasSeenHasBeenReRead"/> et
/// <see cref="CarriesTheIncompletenessClauseAsAPropertyOfTheAnswer"/>. Un rapport qui se rendrait
/// sans son verrou ni sa clause laisserait un rapport de détection <b>se lire comme un recensement
/// complet</b>,
/// ce qui est le seul mode de panne que ce contexte existe pour empêcher.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ScreeningDepositScreen(CustomWebApplicationFactory<Program> factory)
{
  private readonly ScreeningSurface _surface = new(factory);

  /// <summary>
  /// <b>Un relevé collé produit un rapport lisible</b>, et le dépôt y mène. La redirection fait
  /// qu'un rechargement ne détecte pas deux fois — et un second rapport de détection ne reprend
  /// aucun arbitrage
  /// du premier.
  /// </summary>
  [Fact]
  public async Task TurnsAPastedListingIntoAReportTheOperatorCanReadRightAway()
  {
    var report = await _surface.DepositAndReadTheReportAsync(ScreeningSurface.Paste(
      ScreeningSurface.Column("id_adh", position: 1),
      ScreeningSurface.Column("email", position: 2),
      ScreeningSurface.Column("montant", table: "cotisations", position: 1)));

    report.ShouldContain("galette_prod");
    report.ShouldContain("postgresql");
    report.ShouldContain("adherents");
    report.ShouldContain("cotisations");
  }

  /// <summary>
  /// <b>Les tables sont retriées par le service</b>, et non rendues dans l'ordre où la base les a
  /// données : le pivot arrive dans la collation du SGBD source, qui ne range pas <c>_</c> comme
  /// l'ordre des octets. Sans ce retri, deux <c>Operator</c> collant le même schéma depuis deux
  /// réplicas configurés différemment liraient deux écrans ordonnés différemment.
  /// </summary>
  [Fact]
  public async Task SortsTheTablesItselfRatherThanRenderingThemInTheOrderTheSourceGaveThem()
  {
    // Collées dans l'ordre d'une collation de SGBD, qui place « llx_bom_bomline » avant
    // « llx_bom_bom_extrafields ». L'ordre des octets fait l'inverse, « _ » précédant « l ».
    var report = await _surface.DepositAndReadTheReportAsync(ScreeningSurface.Paste(
      ScreeningSurface.Column("rowid", table: "llx_bom_bomline"),
      ScreeningSurface.Column("rowid", table: "llx_bom_bom_extrafields"),
      ScreeningSurface.Column("rowid", table: "adherents")));

    var rendered = new[] { "adherents", "llx_bom_bom_extrafields", "llx_bom_bomline" }
      .Select(table => report.IndexOf($"public.{table}", StringComparison.Ordinal))
      .ToArray();

    rendered.ShouldAllBe(position => position >= 0, "Les trois tables doivent être nommées au rapport.");

    rendered.SequenceEqual(rendered.Order()).ShouldBeTrue(
      "Les tables se rendent dans l'ordre des octets, jamais dans celui de la collation de la source.");
  }

  /// <summary>
  /// <b>L'avancement se lit au rapport</b>, et il n'est jamais un état de haut niveau rassurant :
  /// « 0 / 3 colonnes tranchées » est un nombre de colonnes, pas un mot.
  /// </summary>
  [Fact]
  public async Task RendersTheProgressOfTheReportRatherThanAReassuringHighLevelStatus()
  {
    var report = await _surface.DepositAndReadTheReportAsync(ScreeningSurface.Paste(
      ScreeningSurface.Column("id_adh", position: 1),
      ScreeningSurface.Column("email", position: 2),
      ScreeningSurface.Column("montant", table: "cotisations", position: 1)));

    // Aucun arbitrage n'a eu lieu : les trois colonnes attendent.
    report.ShouldMatch(@"<span class=""count"">0</span> / 3 colonnes");
    (Counted(report, FlaggedAwaiting) + Counted(report, UnflaggedAwaiting)).ShouldBe(3);
  }

  /// <summary>
  /// ⚠️ <b>Le rapport rend TOUTES les colonnes du relevé</b>, signalées ou non. Un rapport qui
  /// n'aurait compté que les signalées serait un rapport où l'omission a cessé d'être relisible :
  /// une colonne absente est une colonne que personne ne relit jamais.
  /// </summary>
  [Fact]
  public async Task CountsEveryColumnOfTheListingAndNotOnlyTheFlaggedOnes()
  {
    var report = await _surface.DepositAndReadTheReportAsync(ScreeningSurface.Paste(
      ScreeningSurface.Column("id_adh", position: 1),
      ScreeningSurface.Column("email", position: 2),
      ScreeningSurface.Column("dt_crea", position: 3),
      ScreeningSurface.Column("montant", table: "cotisations", position: 1)));

    // Une seule ligne signalée sur quatre, et les quatre sont au rapport.
    report.ShouldMatch(@"<span class=""count"">0</span> / 4 colonnes");
    Counted(report, FlaggedAwaiting).ShouldBeGreaterThan(0);
    Counted(report, FlaggedAwaiting).ShouldBeLessThan(4);
    (Counted(report, FlaggedAwaiting) + Counted(report, UnflaggedAwaiting)).ShouldBe(4);
  }

  /// <summary>
  /// <b>Le verrou est en tête, en comptes, et il se recalcule à chaque rendu.</b> Sans lui, un
  /// <c>Operator</c> qui a arbitré ses tables signalées voit une surface qui se tait et <b>croit le
  /// travail fini</b>, alors que l'<c>Omission relue</c> n'a précisément rien rattrapé.
  /// </summary>
  /// <remarks>
  /// ⚠️ Sur le rapport, il ne se dit plus en phrase : l'avancement porte le reste à relire, chiffré.
  /// La phrase demeure sur l'écran d'une table — voir <c>ScreeningTableScreen</c>.
  /// </remarks>
  [Fact]
  public async Task KeepsTheLockOnUntilEveryColumnWhereNothingWasSeenHasBeenReRead()
  {
    var report = System.Net.WebUtility.HtmlDecode(await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(
        ScreeningSurface.Column("id_adh", position: 1),
        ScreeningSurface.Column("email", position: 2),
        ScreeningSurface.Column("montant", table: "cotisations", position: 1))));

    report.ShouldMatch(@"<strong>\d+ colonnes? où rien n'a été détecté</strong>\s*(est|sont) à relire");
  }

  /// <summary>
  /// <b>La clause d'incomplétude est une propriété de la réponse</b>, jamais une mention en pied de
  /// page : ses trois parties se rendent, et rien ne se replie derrière un « en savoir plus ».
  /// </summary>
  /// <remarks>
  /// ⚠️ Elle dit aussi ce que <b>ce</b> relevé-ci lui apprend — combien de colonnes, combien de
  /// tables, combien sans le moindre commentaire — et non seulement des généralités.
  /// </remarks>
  [Fact]
  public async Task CarriesTheIncompletenessClauseAsAPropertyOfTheAnswer()
  {
    var report = await _surface.DepositAndReadTheReportAsync(ScreeningSurface.Paste(
      ScreeningSurface.Column("id_adh", position: 1),
      ScreeningSurface.Column("email", position: 2, tableComment: "les adhérents")));

    // Les trois parties.
    report.ShouldContain("Le périmètre lu");
    report.ShouldContain("Hors périmètre");
    report.ShouldContain("Hors de portée");

    // Ce que ce relevé-ci lui apprend, et qui n'est pas une généralité.
    report.ShouldContain("2 colonnes");
    report.ShouldContain("sans aucun commentaire");
  }

  /// <summary>
  /// <b>Le mot est <em>détection</em></b> — jamais <em>recensement</em>, <em>cartographie</em> ni
  /// <em>scan</em>. Un contexte qui a deux mots pour son geste central en aura trois dans un an, et
  /// les trois interdits promettent chacun un document complet que ce rapport de détection n'est
  /// pas. ⚠️ <b>Le témoin porte la doctrine, pas le mot</b> : il a gelé l'ancien mot du contexte
  /// tant que l'interface le disait, il gèle « détection » depuis, et il ne se supprime pas quand le
  /// mot change.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>La règle porte sur ce qui <em>nomme</em></b> — titres, boutons, libellés, onglet — et non
  /// sur toute occurrence dans la page. La <c>Clause d'incomplétude</c> écrit en toutes lettres « ce
  /// rapport de détection <b>ne recense pas</b> vos systèmes : c'est vous qui les recensez », et
  /// c'est le texte gelé du domaine (ADR-0006) : bannir le mot jusque dans la phrase qui refuse la
  /// chose aurait fait disparaître la seule ligne qui dit à l'<c>Operator</c> ce que ce rapport de
  /// détection n'est pas.
  /// </para>
  /// <para>
  /// ⚠️ <b>L'exemption tient parce qu'elle est <em>verbale</em>.</b> Ce que la doctrine refuse n'est
  /// pas le lexème mais <b>l'autorité d'un recensement</b>, que seul le nom défini confère. Le
  /// nouveau texte n'emploie plus que le verbe, dans la prose d'un <c>&lt;p&gt;</c> ; aucun titre,
  /// bouton ni libellé ne porte le nom, et la règle ci-dessous s'applique donc sans exception à
  /// tout ce qui <b>nomme</b>.
  /// </para>
  /// <para>
  /// ⚠️ <b><c>scan</c> a cessé d'être un synonyme et est devenu un terme du glossaire</b> (#308),
  /// exactement comme <c>cartographie</c> avant lui : il nomme <b>la lecture d'une base</b> par le
  /// service, c'est-à-dire la voie connectée — jamais le geste central du contexte, ni ce qu'il
  /// rend. Il est donc refusé <b>sans réserve</b> dans l'onglet et le titre, qui nomment l'écran et
  /// donc le rapport, et n'a ailleurs droit de cité qu'<b>attaché à la base qu'il joint</b> :
  /// « connecter une base et lancer un scan ». Un lien qui dirait « le scan » tout court rougirait.
  /// </para>
  /// <para>
  /// ⚠️ <b><c>cartographie</c> a cessé d'être un synonyme et est devenu un terme du glossaire</b> :
  /// il nomme <b>ce qui sort du service</b> — le fichier exporté —, pas le geste qui l'a produit.
  /// La doctrine tient donc <b>mot pour mot</b>, et c'est ce que le garde vérifie désormais : le
  /// rapport garde son seul mot dans l'onglet et le titre, où le mot est refusé sans réserve, et
  /// partout ailleurs « cartographie » n'a droit de cité qu'<b>attaché à son export</b> — « exporter
  /// la cartographie », « la cartographie en CSV ». Un lien qui dirait « la cartographie » tout court
  /// rougirait : ce serait le second nom du rapport que la règle refuse.
  /// </para>
  /// </remarks>
  [Theory]
  [InlineData(ScreeningSurface.Report)]
  [InlineData(ScreeningSurface.Deposit)]
  public async Task NamesTheGestureWithTheOnlyWordTheGlossaryGivesIt(string address)
  {
    await _surface.DepositAsync(ScreeningSurface.Paste(ScreeningSurface.Column("email")));

    var rendered = await _surface.ReadAsync(address);

    rendered.ShouldContain("détection");

    // Ce qui NOMME : l'onglet, les titres, les boutons, les libellés et les liens.
    var naming = System.Text.RegularExpressions.Regex
      .Matches(rendered, @"<(title|h1|h2|h3|button|label|a)\b[^>]*>(.*?)</\1>",
        System.Text.RegularExpressions.RegexOptions.Singleline)
      .Select(named => named.Groups[2].Value)
      .ToArray();

    naming.ShouldNotBeEmpty("La page doit bien nommer quelque chose.");

    // ⚠️ « SCAN » A CESSÉ D'ÊTRE UN SYNONYME ET EST DEVENU UN TERME DU GLOSSAIRE (#308), comme
    // « cartographie » avant lui : il nomme LA LECTURE D'UNE BASE par le service — le geste de la
    // voie connectée —, jamais le geste central du contexte ni ce qu'il rend. Un lien qui dit
    // « lancer un scan » nomme donc la bonne chose. Ce qui reste refusé sans réserve est plus bas :
    // l'onglet et le titre, qui nomment l'écran et donc le rapport.
    foreach (var banned in new[] { "recensement" })
    {
      naming.ShouldAllBe(
        named => !named.Contains(banned, StringComparison.OrdinalIgnoreCase),
        $"Un titre, un bouton ou un libellé nomme le geste « {banned} ». Le geste central de ce "
        + "contexte n'a qu'un seul mot, et un contexte qui en a deux en aura trois dans un an.");
    }

    // ⚠️ « Cartographie » nomme CE QUI SORT du service, jamais le rapport : l'onglet et le titre,
    // qui nomment l'écran et donc le rapport, le refusent sans réserve.
    System.Text.RegularExpressions.Regex
      .Matches(rendered, @"<(title|h1)\b[^>]*>(.*?)</\1>",
        System.Text.RegularExpressions.RegexOptions.Singleline)
      .Select(named => named.Groups[2].Value)
      .ShouldAllBe(
        named => !named.Contains("cartographie", StringComparison.OrdinalIgnoreCase)
          && !named.Contains("scan", StringComparison.OrdinalIgnoreCase),
        "L'onglet ou le titre nomme le rapport « cartographie » ou « scan ». Le geste central de ce "
        + "contexte n'a qu'un seul mot ; la cartographie est ce qui en sort, le scan est la lecture "
        + "d'une base — ni l'un ni l'autre n'est lui.");

    // Et partout ailleurs, le mot n'a droit de cité qu'attaché à son export : « la cartographie »
    // tout court serait le second nom du rapport que la règle refuse.
    naming
      .Where(named => named.Contains("cartographie", StringComparison.OrdinalIgnoreCase))
      .ShouldAllBe(
        named => named.Contains("export", StringComparison.OrdinalIgnoreCase)
          || named.Contains("CSV", StringComparison.OrdinalIgnoreCase)
          || named.Contains("JSON", StringComparison.OrdinalIgnoreCase),
        "Un libellé dit « cartographie » sans dire de quel export il parle : détaché de son "
        + "fichier, le mot redevient un second nom du rapport de détection.");

    // Et « scan », comme « cartographie », n'a droit de cité qu'attaché à CE QU'IL LIT : « lancer un
    // scan » sur une base nomme la voie connectée ; « le scan » tout court redeviendrait un second
    // nom du geste de détection, qui n'en a qu'un.
    naming
      .Where(named => named.Contains("scan", StringComparison.OrdinalIgnoreCase))
      .ShouldAllBe(
        named => named.Contains("base", StringComparison.OrdinalIgnoreCase)
          || named.Contains("connect", StringComparison.OrdinalIgnoreCase),
        "Un libellé dit « scan » sans dire ce qui est lu : détaché de la base qu'il joint, le mot "
        + "redevient un second nom du geste de détection.");
  }

  /// <summary>
  /// <b>L'écran de dépôt fournit la requête, et elle vient des fichiers du dépôt.</b> Sans elle,
  /// l'<c>Operator</c> ne peut pas produire un relevé qui déclare son SGBD et son compte de
  /// colonnes — et une troncature au collage cesserait d'être détectable.
  /// </summary>
  [Theory]
  [InlineData("postgresql")]
  [InlineData("mariadb")]
  [InlineData("sqlite")]
  public async Task HandsTheOperatorTheVersionedListingQueryOfTheDatabaseHeChose(string dialect)
  {
    var screen = await _surface.ReadAsync($"{ScreeningSurface.Deposit}?dialect={dialect}");

    screen.ShouldContain("screening-pivot/1");
    screen.ShouldContain("Relevé de colonnes", Case.Insensitive);
  }

  /// <summary>
  /// ⚠️ <b>Un relevé amputé est refusé EN BLOC, et le refus est lisible.</b> Un rapport de détection
  /// bâti sur
  /// une part du relevé <b>se lirait comme complet</b>, et les colonnes perdues seraient précisément
  /// celles que personne ne relirait jamais. Le refus nomme la ligne et dit ce qui était attendu :
  /// sans cela, l'<c>Operator</c> recommence au hasard sur un collage d'un mégaoctet.
  /// </summary>
  [Fact]
  public async Task RefusesATruncatedListingOutrightAndSaysWhatWasExpected()
  {
    // La ligne de fin annonce quatre colonnes ; deux seulement ont survécu au collage.
    var refused = await _surface.DepositAsync(ScreeningSurface.Paste(
      declaredColumnCount: 4,
      ScreeningSurface.Column("id_adh", position: 1),
      ScreeningSurface.Column("email", position: 2)));

    refused.StatusCode.ShouldBe(HttpStatusCode.OK, "Un refus se relit sur l'écran, il ne redirige nulle part.");

    var rendered = await refused.Content.ReadAsStringAsync();

    rendered.ShouldContain("refusé en bloc", Case.Insensitive);
    rendered.ShouldContain("Aucune colonne");
    rendered.ShouldContain("été ingérée");
  }

  /// <summary>
  /// <b>Le tout premier démarrage chez un client</b> : aucune détection n'a été lancée, et on rend
  /// l'écran de dépôt seul. Un rapport de détection vide portant « ce rapport de détection n'a pas
  /// regardé le CRM en
  /// SaaS… » serait un <b>aveu sans acte</b>, et userait la clause avant son premier usage réel.
  /// </summary>
  /// <remarks>
  /// ⚠️ Ce n'est pas le cas spécial « quand rien n'est signalé », qui reste refusé : là il y a un
  /// rapport de détection réel dont on tairait le seuil zéro, ici il n'y a pas de détection du tout.
  /// </remarks>
  [Fact]
  public async Task LeadsToTheDepositScreenAloneWhenTheDeploymentHasLaunchedNoScreeningYet()
  {
    // La collection est partagée : on ne peut pas garantir une base vierge, mais on peut exiger que
    // le rendu soit l'un des deux, et jamais un rapport vide portant la clause.
    var response = await new ScreeningSurface(factory).Client.GetAsync(ScreeningSurface.Report);

    response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);

    if (response.StatusCode == HttpStatusCode.Redirect)
    {
      response.Headers.Location!.OriginalString.ShouldContain(ScreeningSurface.Deposit);
    }
  }

  /// <summary>Le premier nombre que <paramref name="pattern"/> capture, lu là où l'écran le rend.</summary>
  private static int Counted(string report, string pattern)
  {
    // Décodé : Razor encode l'apostrophe, et le motif s'écrit comme la phrase se lit.
    var counted = System.Text.RegularExpressions.Regex.Match(System.Net.WebUtility.HtmlDecode(report), pattern);

    counted.Success.ShouldBeTrue($"Le rapport ne rend aucun compte qui réponde à « {pattern} ».");

    return int.Parse(counted.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
  }

  /// <summary>Les colonnes signalées qui attendent encore d'être tranchées.</summary>
  private const string FlaggedAwaiting = @"<strong>(\d+) colonnes? signalées?</strong>";

  /// <summary>Les colonnes où rien n'a été détecté qui attendent encore d'être relues.</summary>
  private const string UnflaggedAwaiting = @"<strong>(\d+) colonnes? où rien n'a été détecté</strong>";
}
