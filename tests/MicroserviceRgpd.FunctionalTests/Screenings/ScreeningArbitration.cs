using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// L'<b>arbitrage colonne par colonne</b>, éprouvé par la seule frontière qui existe : le formulaire
/// de l'écran d'une table.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Rien n'est écrit en base à la main, et aucun geste n'est appelé directement.</b> Ce que
/// cette tranche livre est un formulaire : l'éprouver par MediatR aurait laissé passer un écran qui
/// ne poste pas le triplet, qui pré-coche « retenue », ou qui porte un champ de date.
/// </para>
/// <para>
/// ⚠️ <b>L'horloge n'est pas doublée</b>, comme le moteur ne l'est pas. C'est le vrai
/// <c>TimeProvider</c> qui date ici, et les tests le vérifient à la journée : une horloge dictée
/// aurait rendu vert un écran qui date les arbitrages depuis le navigateur.
/// </para>
/// <para>
/// <b>La collection est partagée</b>, et « courant » est un calcul sur tout le déploiement : chaque
/// test dépose donc son propre relevé avant d'arbitrer.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ScreeningArbitration(CustomWebApplicationFactory<Program> factory)
{
  private readonly ScreeningSurface _surface = new(factory);

  /// <summary>
  /// <b>Une issue rendue porte une date, et les deux entrent ensemble.</b> C'est la seule trace que
  /// ce contexte garde d'un acte humain.
  /// </summary>
  [Fact]
  public async Task RecordsTheRulingAndTheDateThatTheServicePoses()
  {
    await DepositAsync(
      ScreeningSurface.Column("email", position: 1),
      ScreeningSurface.Column("montant", position: 2));

    var arbitrated = await _surface.ArbitrateAsync("email", ScreenedColumnState.Retained.Name);

    // ⚠️ Le succès REDIRIGE : un rechargement ne doit pas redemander le formulaire à l'Operator,
    // qui reprend cet écran pendant trois jours.
    arbitrated.StatusCode.ShouldBe(HttpStatusCode.Redirect);

    var row = RowOf(await ReadTheTableAsync(), "email");

    row.ShouldNotBeNull();
    row.ShouldContain("retenue");

    // La date est celle du service, à la journée : c'est lui qui tient l'horloge.
    row.ShouldContain(Today());
  }

  /// <summary>
  /// ⚠️ <b>Ce contexte n'enregistre pas qui a arbitré, et un formulaire forgé ne l'y fait pas
  /// entrer.</b> L'écran n'offre aucun champ de nom, et un nom posté à la main ne s'accroche à
  /// rien — voir l'<c>ADR-0014</c>.
  /// </summary>
  [Fact]
  public async Task RecordsNobodyEvenWhenAForgedFormPostsAName()
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    var table = await ReadTheTableAsync();

    // L'écran ne DEMANDE aucun nom : le champ absent est ce qui tient la règle.
    table.ShouldNotContain("name=\"SignedBy\"");
    table.ShouldNotContain("Votre nom");

    await _surface.ArbitrateAsync(
      "email",
      ScreenedColumnState.Retained.Name,
      alsoPosted:
      [
        new KeyValuePair<string, string>("SignedBy", "Camille Roux"),
        new KeyValuePair<string, string>("Arbitration.SignedBy", "Camille Roux"),
      ]);

    var arbitrated = await ReadTheTableAsync();

    RowOf(arbitrated, "email").ShouldNotBeNull().ShouldContain("retenue");
    arbitrated.ShouldNotContain("Camille Roux");
  }

  /// <summary>
  /// ⚠️ <b><c>Awaiting</c> n'est pas une issue, et un formulaire forgé ne la rend pas rendable.</b>
  /// Personne ne tranche une absence de décision.
  /// </summary>
  [Fact]
  public async Task RefusesToRuleTheAbsenceOfADecision()
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    await _surface.ArbitrateAsync("email", ScreenedColumnState.Awaiting.Name);

    RowOf(await ReadTheTableAsync(), "email").ShouldNotBeNull().ShouldContain("En attente");
  }

  /// <summary>
  /// <b>Se raviser est sans cérémonie : le nouvel état écrase l'ancien.</b> Le coût est déclaré —
  /// la date de l'arbitrage remplacé est effacée — parce que la trace <b>est</b> l'état courant
  /// seul.
  /// </summary>
  [Fact]
  public async Task LetsASecondRulingOverwriteTheFirstWithoutCeremony()
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    await _surface.ArbitrateAsync("email", ScreenedColumnState.Retained.Name);
    await _surface.ArbitrateAsync("email", ScreenedColumnState.SetAside.Name);

    var table = await ReadTheTableAsync();
    var row = RowOf(table, "email");

    row.ShouldNotBeNull();
    row.ShouldContain("écartée");

    // ⚠️ Il n'y a PAS d'histoire : le premier arbitrage a disparu, et c'est le régime déclaré.
    row.ShouldNotContain("retenue");

    // Et les comptes suivent : une écartée, aucune retenue.
    Counted(table, "Écartées").ShouldBe(1);
    Counted(table, "Retenues").ShouldBe(0);
  }

  /// <summary>
  /// ⚠️ <b>La date vient du service, jamais du formulaire.</b> C'est le point le plus fragile de
  /// cette tranche : une date choisie par l'<c>Operator</c> sur la seule trace d'un acte humain
  /// viderait cette trace de sa valeur.
  /// </summary>
  [Fact]
  public async Task TakesTheDateFromTheServiceEvenWhenAForgedFormPostsOne()
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    await _surface.ArbitrateAsync(
      "email",
      ScreenedColumnState.Retained.Name,
      alsoPosted:
      [
        new KeyValuePair<string, string>("RenderedOn", "2001-09-11T00:00:00Z"),
        new KeyValuePair<string, string>("Arbitration.RenderedOn", "2001-09-11T00:00:00Z"),
      ]);

    var table = await ReadTheTableAsync();

    RowOf(table, "email").ShouldNotBeNull().ShouldContain(Today());
    table.ShouldNotContain("2001");

    // ⚠️ Et l'écran ne PROPOSE aucune date : le champ absent est ce qui tient la règle, le refus
    // n'étant qu'un filet.
    table.ShouldNotContain("type=\"date\"");
    table.ShouldNotContain("name=\"RenderedOn\"");
  }

  /// <summary>
  /// <b>Le verrou se lève quand plus aucune colonne n'attend</b>, et il est recalculé à chaque rendu
  /// — jamais persisté.
  /// </summary>
  [Fact]
  public async Task LiftsTheLockOnlyOnceNoColumnIsAwaitingAnyMore()
  {
    await DepositAsync(
      ScreeningSurface.Column("email", position: 1),
      ScreeningSurface.Column("montant", position: 2));

    (await ReadTheTableAsync()).ShouldContain("Ce rapport de détection est inachevé");

    await _surface.ArbitrateAsync("email", ScreenedColumnState.Retained.Name);

    // ⚠️ Une seule des deux tranchées ne lève rien : c'est très exactement l'instant où l'on croit
    // avoir fini.
    var halfway = await ReadTheTableAsync();

    halfway.ShouldContain("Ce rapport de détection est inachevé");
    Counted(halfway, "En attente").ShouldBe(1);

    await _surface.ArbitrateAsync("montant", ScreenedColumnState.SetAside.Name);

    var finished = await ReadTheTableAsync();

    finished.ShouldNotContain("Ce rapport de détection est inachevé");
    finished.ShouldContain("Toutes les colonnes de ce rapport de détection ont été relues");
    Counted(finished, "En attente").ShouldBe(0);
  }

  /// <summary>
  /// ⚠️ <b>« Retenues sur "rien signalé" » est la mesure de l'<c>Omission relue</c></b> : une colonne
  /// que le service n'avait pas vue, et qu'un humain a retenue de sa propre main. Sans ce compte, la
  /// relecture des non signalées serait décorative.
  /// </summary>
  [Fact]
  public async Task CountsARetainedColumnThatTheScreeningHadNotFlaggedAsTheMeasureOfTheReadOmission()
  {
    await DepositAsync(
      ScreeningSurface.Column("email", position: 1),
      ScreeningSurface.Column("montant", position: 2));

    // « montant » n'est à aucun lexique : c'est une colonne où le service n'a rien vu.
    RowOf(await ReadTheTableAsync(), "montant").ShouldNotBeNull().ShouldContain("Rien n'a été vu");

    await _surface.ArbitrateAsync("montant", ScreenedColumnState.Retained.Name);
    await _surface.ArbitrateAsync("email", ScreenedColumnState.Retained.Name);

    var table = await ReadTheTableAsync();

    Counted(table, "Retenues").ShouldBe(2);

    // ⚠️ Une seule des deux vient d'une omission relue, et c'est celle-là que le compte isole.
    Counted(table, "Retenues sur « rien signalé »").ShouldBe(1);

    // Et la ligne dit toujours que le service, lui, n'avait rien vu : un Retained prouve qu'un
    // HUMAIN l'a déclaré retenu, jamais que la colonne porte des données personnelles.
    var retainedOnNothing = RowOf(table, "montant");

    retainedOnNothing.ShouldNotBeNull();
    retainedOnNothing.ShouldContain("Rien n'a été vu");
    retainedOnNothing.ShouldContain("retenue");
  }

  /// <summary>
  /// <b>Une colonne que le rapport courant ne porte pas ne casse rien</b> : elle ramène au rapport,
  /// qui dit ce que le déploiement a réellement. Un écran affiché il y a une minute peut nommer une
  /// colonne qu'un second rapport de détection vient d'emporter.
  /// </summary>
  [Fact]
  public async Task SendsBackToTheReportRatherThanFailingOnAColumnTheCurrentScreeningDoesNotHold()
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    var stale = await _surface.ArbitrateAsync(
      "une_colonne_qui_n_existe_pas", ScreenedColumnState.Retained.Name);

    stale.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    stale.Headers.Location!.OriginalString.ShouldContain(ScreeningSurface.Report);
  }

  /// <summary>
  /// ⚠️ <b>Un arbitrage ne franchit pas les tables d'un même schéma.</b> Le triplet est ce qui
  /// désigne une colonne, et deux tables portent couramment un <c>email</c> : arbitrer l'une aurait
  /// tranché l'autre.
  /// </summary>
  [Fact]
  public async Task ArbitratesTheColumnOfTheNamedTableRatherThanEveryColumnOfThatName()
  {
    await DepositAsync(
      ScreeningSurface.Column("email", position: 1),
      ScreeningSurface.Column("email", table: "cotisations", position: 1));

    await _surface.ArbitrateAsync("email", ScreenedColumnState.Retained.Name);

    RowOf(await ReadTheTableAsync(), "email").ShouldNotBeNull().ShouldContain("retenue");

    RowOf(await ReadTheTableAsync("cotisations"), "email")
      .ShouldNotBeNull()
      .ShouldContain("En attente");
  }

  /// <summary>
  /// <b>Le formulaire ne pré-coche rien</b> : les deux issues sont deux boutons, et le compte des
  /// retenues est ce sur quoi la déclaration se construit ensuite.
  /// </summary>
  [Fact]
  public async Task OffersTheTwoRulingsAsTwoButtonsWithNeitherPreselected()
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    var table = await ReadTheTableAsync();

    table.ShouldContain($"name=\"Ruling\" value=\"{ScreenedColumnState.Retained.Name}\"");
    table.ShouldContain($"name=\"Ruling\" value=\"{ScreenedColumnState.SetAside.Name}\"");

    // Rien de coché ni de sélectionné d'avance : un clic distrait ne retient pas.
    table.ShouldNotContain("checked");
    table.ShouldNotContain("selected");
  }

  /// <summary>
  /// <b>Le formulaire reste après l'arbitrage</b> : se raviser doit rester possible sur une surface
  /// qu'on reprend pendant trois jours.
  /// </summary>
  [Fact]
  public async Task KeepsTheFormOnAColumnThatHasAlreadyBeenArbitrated()
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    await _surface.ArbitrateAsync("email", ScreenedColumnState.Retained.Name);

    var row = RowOf(await ReadTheTableAsync(), "email");

    row.ShouldNotBeNull();
    row.ShouldContain("retenue");
    row.ShouldContain("<form");
    row.ShouldContain($"value=\"{ScreenedColumnState.SetAside.Name}\"");
  }

  /// <summary>
  /// ⚠️ <b>La touche Entrée ne retient rien, et c'est l'absence de champ de saisie qui le tient.</b>
  /// Un formulaire soumet sur Entrée son <b>premier</b> bouton d'envoi, mais seulement depuis une
  /// commande textuelle : il n'en reste aucune ici, le champ du nom étant parti. <b>Reposer un
  /// champ texte, quel qu'il soit, remettrait la trappe</b> — taper puis valider retiendrait la
  /// colonne, et gonflerait le compte sur lequel la déclaration se construit ensuite.
  /// </summary>
  [Fact]
  public async Task OffersNoTextFieldThroughWhichTheEnterKeyCouldSettleAColumn()
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    var table = await ReadTheTableAsync();

    table.ShouldNotContain("type=\"text\"");
    table.ShouldNotContain("<textarea");
  }

  /// <summary>
  /// ⚠️ <b>Un rapport déposé pendant la lecture ne reçoit pas l'arbitrage de celui qui lisait
  /// l'autre.</b> « Courant » se recalcule à l'écriture : sans ce refus, un collègue qui dépose un
  /// relevé fait atterrir l'arbitrage sur un rapport que personne n'a lu — motifs compris.
  /// </summary>
  [Fact]
  public async Task RefusesToArbitrateWhenANewerScreeningWasDepositedDuringTheReading()
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    // Le rapport que l'Operator avait sous les yeux, capturé avant que le suivant n'arrive.
    var read = ScreeningOf(await ReadTheTableAsync());

    // Un collègue dépose : le courant change sous l'écran resté ouvert.
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    var refused = await _surface.ArbitrateAsync(
      "email", ScreenedColumnState.Retained.Name, screening: read);

    refused.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    refused.Headers.Location!.OriginalString.ShouldContain(ScreeningSurface.Report);

    // ⚠️ Rien n'a été écrit sur le rapport neuf, et le renvoi le DIT : un renvoi muet se lirait
    // comme une navigation ordinaire.
    RowOf(await ReadTheTableAsync(), "email").ShouldNotBeNull().ShouldContain("En attente");

    var report = WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.Report));

    report.ShouldContain("rapport de détection plus récent");
    report.ShouldContain("n'a pas été enregistré");
  }

  /// <summary>
  /// ⚠️ <b>Aucun renvoi n'est muet.</b> La réponse d'un arbitrage réussi est une redirection : un
  /// geste refusé qui redirigerait sans rien dire serait indiscernable d'un succès, et
  /// l'<c>Operator</c> repartirait en croyant avoir tranché.
  /// </summary>
  [Fact]
  public async Task SaysThatNothingWasArbitratedWhenTheFormDesignatesNoColumnAtAll()
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    var forged = await _surface.ArbitrateAsync("email", "PasUnArbitrage");

    forged.StatusCode.ShouldBe(HttpStatusCode.Redirect);

    var report = WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.Report));

    report.ShouldContain("Aucun arbitrage n'a été enregistré");

    RowOf(await ReadTheTableAsync(), "email").ShouldNotBeNull().ShouldContain("En attente");
  }

  /// <summary>
  /// <b>Une colonne que le rapport courant ne porte pas le dit</b>, plutôt que de ramener au rapport
  /// sans un mot.
  /// </summary>
  [Fact]
  public async Task SaysThatNothingWasArbitratedWhenTheColumnHasLeftTheCurrentScreening()
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    await _surface.ArbitrateAsync(
      "une_colonne_qui_n_existe_pas", ScreenedColumnState.Retained.Name);

    var report = WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.Report));

    report.ShouldContain("n'est plus dans le rapport de détection courant");
  }


  /// <summary>Le rapport que l'écran rendait, lu sur son formulaire.</summary>
  private static string ScreeningOf(string table)
  {
    var screening = Regex.Match(table, @"name=""Screening"" value=""([^""]+)""");

    screening.Success.ShouldBeTrue("Le formulaire ne dit pas quel rapport il rendait.");

    return screening.Groups[1].Value;
  }

  /// <summary>Dépose un relevé et exige qu'il ait produit un rapport.</summary>
  private async Task DepositAsync(params string[] columns)
  {
    var deposited = await _surface.DepositAsync(ScreeningSurface.Paste(columns));

    deposited.StatusCode.ShouldBe(
      HttpStatusCode.Redirect,
      "Le dépôt d'un relevé sincère doit mener au rapport qu'il vient de produire.");
  }

  /// <summary>L'écran d'une table, relu par une adresse neuve — jamais la page rendue par le POST.</summary>
  private async Task<string> ReadTheTableAsync(string table = "adherents")
  {
    return WebUtility.HtmlDecode(
      await _surface.ReadAsync(ScreeningSurface.TableOf(table: table)));
  }

  /// <summary>La date du jour, telle que l'écran la rend.</summary>
  private static string Today()
  {
    return DateTimeOffset.UtcNow.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("fr-FR"));
  }

  /// <summary>La ligne d'une colonne, telle que l'écran la rend — ou <c>null</c> si elle n'y est pas.</summary>
  private static string? RowOf(string table, string column)
  {
    var row = Regex.Match(
      table,
      $@"<tr>\s*<th scope=""row"">\s*{Regex.Escape(column)}\b.*?</tr>",
      RegexOptions.Singleline);

    return row.Success ? row.Value : null;
  }

  /// <summary>Ce qu'un compte du rapport vaut, lu là où l'écran le rend.</summary>
  private static int Counted(string table, string label)
  {
    var counted = Regex.Match(table, $@"<dt>{Regex.Escape(label)}</dt>\s*<dd>\s*(\d+)");

    counted.Success.ShouldBeTrue($"L'écran ne rend aucun compte sous « {label} ».");

    return int.Parse(counted.Groups[1].Value, CultureInfo.InvariantCulture);
  }
}
