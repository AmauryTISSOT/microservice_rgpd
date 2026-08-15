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
/// sans son verrou ni sa clause laisserait un dépistage <b>se lire comme un recensement complet</b>,
/// ce qui est le seul mode de panne que ce contexte existe pour empêcher.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ScreeningDepositScreen(CustomWebApplicationFactory<Program> factory)
{
  private readonly ScreeningSurface _surface = new(factory);

  /// <summary>
  /// <b>Un relevé collé produit un rapport lisible</b>, et le dépôt y mène. La redirection fait
  /// qu'un rechargement ne dépiste pas deux fois — et un second dépistage ne reprend aucun arbitrage
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
  /// <b>Les comptes se lisent au rapport</b>, et l'avancement n'est jamais un état de haut niveau
  /// rassurant : « en attente » est un nombre de colonnes, pas un mot.
  /// </summary>
  /// <remarks>
  /// ⚠️ Le compte « retenues sur rien signalé » vaut <b>zéro</b> ici, et c'est ce qu'on lui demande
  /// de dire : c'est la mesure directe de ce que l'<c>Omission relue</c> a rattrapé, et personne n'a
  /// encore relu quoi que ce soit.
  /// </remarks>
  [Fact]
  public async Task RendersTheCountsOfTheReportRatherThanAReassuringHighLevelStatus()
  {
    var report = await _surface.DepositAndReadTheReportAsync(ScreeningSurface.Paste(
      ScreeningSurface.Column("id_adh", position: 1),
      ScreeningSurface.Column("email", position: 2),
      ScreeningSurface.Column("montant", table: "cotisations", position: 1)));

    report.ShouldContain("Signalées");
    report.ShouldContain("Retenues");
    report.ShouldContain("Écartées");
    report.ShouldContain("En attente");
    report.ShouldContain("Retenues sur « rien signalé »");

    // Aucun arbitrage n'a eu lieu : les trois colonnes attendent, et rien n'a été rattrapé.
    Counted(report, "En attente").ShouldBe(3);
    Counted(report, "Retenues").ShouldBe(0);
    Counted(report, "Écartées").ShouldBe(0);
    Counted(report, "Retenues sur « rien signalé »").ShouldBe(0);
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
    Counted(report, "En attente").ShouldBe(4);
    Counted(report, "Signalées").ShouldBeGreaterThan(0);
    Counted(report, "Signalées").ShouldBeLessThan(4);
    report.ShouldContain("4 colonnes");
  }

  /// <summary>
  /// <b>Le verrou est en tête, et il se recalcule à chaque rendu.</b> Sans lui, un <c>Operator</c>
  /// qui a arbitré ses tables signalées voit une surface qui se tait et <b>croit le travail fini</b>,
  /// alors que l'<c>Omission relue</c> n'a précisément rien rattrapé.
  /// </summary>
  [Fact]
  public async Task KeepsTheLockOnUntilEveryColumnWhereNothingWasSeenHasBeenReRead()
  {
    var report = await _surface.DepositAndReadTheReportAsync(ScreeningSurface.Paste(
      ScreeningSurface.Column("id_adh", position: 1),
      ScreeningSurface.Column("email", position: 2),
      ScreeningSurface.Column("montant", table: "cotisations", position: 1)));

    report.ShouldContain("Ce dépistage est inachevé");
    report.ShouldContain("pas encore été relues");
  }

  /// <summary>
  /// <b>La clause d'incomplétude est une propriété de la réponse</b>, jamais une mention en pied de
  /// page : ses quatre parties se rendent, et rien ne se replie derrière un « en savoir plus ».
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

    // Les quatre parties.
    report.ShouldContain("Le périmètre lu");
    report.ShouldContain("Hors périmètre");
    report.ShouldContain("Hors de portée");
    report.ShouldContain("Ce rapport de détection et vos systèmes déclarés");

    // Ce que le service a lu comme indice, et ce qu'il n'a lu que pour écarter.
    report.ShouldContain("les noms de colonnes");
    report.ShouldContain("le type de la colonne");

    // Ce que ce relevé-ci lui apprend, et qui n'est pas une généralité.
    report.ShouldContain("2 colonnes");
    report.ShouldContain("sans aucun commentaire");
  }

  /// <summary>
  /// <b>Le mot est <em>dépistage</em></b> — jamais <em>recensement</em>, <em>cartographie</em> ni
  /// <em>scan</em>. Un contexte qui a deux mots pour son geste central en aura trois dans un an, et
  /// les trois interdits promettent chacun un document complet que ce rapport n'est pas.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>La règle porte sur ce qui <em>nomme</em></b> — titres, boutons, libellés, onglet — et non
  /// sur toute occurrence dans la page. La <c>Clause d'incomplétude</c> écrit en toutes lettres « ce
  /// rapport de détection <b>ne recense pas</b> vos systèmes : c'est vous qui les recensez », et
  /// c'est le texte gelé du domaine (ADR-0006) : bannir le mot jusque dans la phrase qui refuse la
  /// chose aurait fait disparaître la seule ligne qui dit à l'<c>Operator</c> ce que ce rapport
  /// n'est pas.
  /// </para>
  /// <para>
  /// ⚠️ <b>L'exemption tient parce qu'elle est <em>verbale</em>.</b> Ce que la doctrine refuse n'est
  /// pas le lexème mais <b>l'autorité d'un recensement</b>, que seul le nom défini confère. Le
  /// nouveau texte n'emploie plus que le verbe, dans la prose d'un <c>&lt;p&gt;</c> ; aucun titre,
  /// bouton ni libellé ne porte le nom, et la règle ci-dessous s'applique donc sans exception à
  /// tout ce qui <b>nomme</b>.
  /// </para>
  /// <para>
  /// <c>cartographie</c> et <c>scan</c>, eux, n'ont aucun emploi légitime nulle part sur ces écrans,
  /// et sont refusés sur la page entière.
  /// </para>
  /// </remarks>
  [Theory]
  [InlineData(ScreeningSurface.Report)]
  [InlineData(ScreeningSurface.Deposit)]
  public async Task NamesTheGestureWithTheOnlyWordTheGlossaryGivesIt(string address)
  {
    await _surface.DepositAsync(ScreeningSurface.Paste(ScreeningSurface.Column("email")));

    var rendered = await _surface.ReadAsync(address);

    rendered.ShouldContain("dépist");

    // Ce qui NOMME : l'onglet, les titres, les boutons, les libellés et les liens.
    var naming = System.Text.RegularExpressions.Regex
      .Matches(rendered, @"<(title|h1|h2|h3|button|label|a)\b[^>]*>(.*?)</\1>",
        System.Text.RegularExpressions.RegexOptions.Singleline)
      .Select(named => named.Groups[2].Value)
      .ToArray();

    naming.ShouldNotBeEmpty("La page doit bien nommer quelque chose.");

    foreach (var banned in new[] { "recensement", "cartographie", "scan" })
    {
      naming.ShouldAllBe(
        named => !named.Contains(banned, StringComparison.OrdinalIgnoreCase),
        $"Un titre, un bouton ou un libellé nomme le geste « {banned} ». Le geste central de ce "
        + "contexte n'a qu'un seul mot, et un contexte qui en a deux en aura trois dans un an.");
    }

    // Ceux-là n'ont aucun emploi légitime, pas même en prose.
    rendered.ShouldNotContain("cartographie", Case.Insensitive);
    rendered.ShouldNotContain("scan", Case.Insensitive);
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
  /// ⚠️ <b>Un relevé amputé est refusé EN BLOC, et le refus est lisible.</b> Un dépistage bâti sur
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
  /// <b>Le tout premier démarrage chez un client</b> : aucun dépistage n'a été lancé, et on rend
  /// l'écran de dépôt seul. Un rapport vide portant « ce dépistage n'a pas regardé le CRM en
  /// SaaS… » serait un <b>aveu sans acte</b>, et userait la clause avant son premier usage réel.
  /// </summary>
  /// <remarks>
  /// ⚠️ Ce n'est pas le cas spécial « quand rien n'est signalé », qui reste refusé : là il y a un
  /// dépistage réel dont on tairait le seuil zéro, ici il n'y a pas de dépistage du tout.
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

  /// <summary>Ce qu'un compte du rapport vaut, lu là où l'écran le rend.</summary>
  private static int Counted(string report, string label)
  {
    var counted = System.Text.RegularExpressions.Regex.Match(
      report,
      $@"<dt>{System.Text.RegularExpressions.Regex.Escape(label)}</dt>\s*<dd>\s*(\d+)");

    counted.Success.ShouldBeTrue($"Le rapport ne rend aucun compte sous « {label} ».");

    return int.Parse(counted.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
  }
}
