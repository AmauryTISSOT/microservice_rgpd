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
  /// <b>Une issue rendue porte le nom saisi et une date, et les trois entrent ensemble.</b> C'est la
  /// seule trace que ce contexte garde d'un acte humain.
  /// </summary>
  [Fact]
  public async Task RecordsTheRulingUnderTheTypedNameAndTheDateThatTheServicePoses()
  {
    await DepositAsync(
      ScreeningSurface.Column("email", position: 1),
      ScreeningSurface.Column("montant", position: 2));

    var arbitrated = await _surface.ArbitrateAsync(
      "email", ScreenedColumnState.Retained.Name, "Camille Roux");

    // ⚠️ Le succès REDIRIGE : un rechargement ne doit pas redemander le formulaire à l'Operator,
    // qui reprend cet écran pendant trois jours.
    arbitrated.StatusCode.ShouldBe(HttpStatusCode.Redirect);

    var row = RowOf(await ReadTheTableAsync(), "email");

    row.ShouldNotBeNull();
    row.ShouldContain("retenue");
    row.ShouldContain("Camille Roux");

    // La date est celle du service, à la journée : c'est lui qui tient l'horloge.
    row.ShouldContain(Today());
  }

  /// <summary>
  /// ⚠️ <b>Un arbitrage sans nom n'est pas un champ vide : c'est un arbitrage qui n'a pas eu
  /// lieu.</b> Le refus se lit, et la colonne attend toujours.
  /// </summary>
  [Fact]
  public async Task RefusesAnArbitrationThatCarriesNoNameAndLeavesTheColumnAwaiting()
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    var refused = await _surface.ArbitrateAsync("email", ScreenedColumnState.Retained.Name, null);

    // ⚠️ Le refus se relit SUR PLACE, il ne redirige pas : le seul geste raisonnable après un refus
    // est de corriger devant le texte qui l'explique.
    refused.StatusCode.ShouldBe(HttpStatusCode.OK);

    var refusal = WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync());

    // Le refus vient du domaine, il nomme sa colonne, et il dit ce que le service a fait — rien.
    refusal.ShouldContain("Le nom du signataire");
    refusal.ShouldContain("email");
    refusal.ShouldContain("Aucun arbitrage n'a été enregistré");

    // Et la colonne attend toujours, sur l'écran relu par une adresse neuve.
    RowOf(await ReadTheTableAsync(), "email").ShouldNotBeNull().ShouldContain("En attente");
  }

  /// <summary>
  /// ⚠️ <b>Aucun chemin d'écriture ne pose une issue sans signature</b> — le domaine le tient, et
  /// c'est ici qu'on le vérifie <b>à travers la surface</b>, formulaire forgé compris.
  /// </summary>
  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public async Task NeverSettlesAColumnOnASignatureThatSaysNothing(string signedBy)
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    var refused = await _surface.ArbitrateAsync(
      "email", ScreenedColumnState.SetAside.Name, signedBy);

    refused.StatusCode.ShouldBe(HttpStatusCode.OK);

    RowOf(await ReadTheTableAsync(), "email").ShouldNotBeNull().ShouldContain("En attente");
  }

  /// <summary>
  /// ⚠️ <b><c>Awaiting</c> n'est pas une issue, et un formulaire forgé ne le rend pas signable.</b>
  /// Personne ne signe une absence de décision.
  /// </summary>
  [Fact]
  public async Task RefusesToSignTheAbsenceOfADecision()
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    await _surface.ArbitrateAsync("email", ScreenedColumnState.Awaiting.Name, "Camille Roux");

    var row = RowOf(await ReadTheTableAsync(), "email");

    row.ShouldNotBeNull().ShouldContain("En attente");

    // Rien n'a été signé : le nom posté ne s'est accroché à aucun état.
    row.ShouldNotContain("Camille Roux");
  }

  /// <summary>
  /// <b>Se raviser est sans cérémonie : le nouvel état écrase l'ancien.</b> Le coût est déclaré — qui
  /// avait dit quoi est effacé — parce que la trace <b>est</b> l'état courant seul.
  /// </summary>
  [Fact]
  public async Task LetsASecondRulingOverwriteTheFirstWithoutCeremony()
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    await _surface.ArbitrateAsync("email", ScreenedColumnState.Retained.Name, "Camille Roux");
    await _surface.ArbitrateAsync("email", ScreenedColumnState.SetAside.Name, "Dominique Blanc");

    var table = await ReadTheTableAsync();
    var row = RowOf(table, "email");

    row.ShouldNotBeNull();
    row.ShouldContain("écartée");
    row.ShouldContain("Dominique Blanc");

    // ⚠️ Il n'y a PAS d'histoire : le premier signataire a disparu, et c'est le régime déclaré.
    row.ShouldNotContain("Camille Roux");
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
      "Camille Roux",
      alsoPosted:
      [
        new KeyValuePair<string, string>("SignedOn", "2001-09-11T00:00:00Z"),
        new KeyValuePair<string, string>("Arbitration.SignedOn", "2001-09-11T00:00:00Z"),
      ]);

    var table = await ReadTheTableAsync();

    RowOf(table, "email").ShouldNotBeNull().ShouldContain(Today());
    table.ShouldNotContain("2001");

    // ⚠️ Et l'écran ne PROPOSE aucune date : le champ absent est ce qui tient la règle, le refus
    // n'étant qu'un filet.
    table.ShouldNotContain("type=\"date\"");
    table.ShouldNotContain("name=\"SignedOn\"");
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

    await _surface.ArbitrateAsync("email", ScreenedColumnState.Retained.Name, "Camille Roux");

    // ⚠️ Une seule des deux tranchées ne lève rien : c'est très exactement l'instant où l'on croit
    // avoir fini.
    var halfway = await ReadTheTableAsync();

    halfway.ShouldContain("Ce rapport de détection est inachevé");
    Counted(halfway, "En attente").ShouldBe(1);

    await _surface.ArbitrateAsync("montant", ScreenedColumnState.SetAside.Name, "Camille Roux");

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

    await _surface.ArbitrateAsync("montant", ScreenedColumnState.Retained.Name, "Camille Roux");
    await _surface.ArbitrateAsync("email", ScreenedColumnState.Retained.Name, "Camille Roux");

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
      "une_colonne_qui_n_existe_pas", ScreenedColumnState.Retained.Name, "Camille Roux");

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

    await _surface.ArbitrateAsync("email", ScreenedColumnState.Retained.Name, "Camille Roux");

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

    // Et le nom est saisi, non authentifié — l'écran le dit plutôt que de le taire.
    table.ShouldContain("non authentifié");
  }

  /// <summary>
  /// <b>Le formulaire reste après l'arbitrage</b> : se raviser doit rester possible sur une surface
  /// qu'on reprend pendant trois jours.
  /// </summary>
  [Fact]
  public async Task KeepsTheFormOnAColumnThatHasAlreadyBeenArbitrated()
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    await _surface.ArbitrateAsync("email", ScreenedColumnState.Retained.Name, "Camille Roux");

    var row = RowOf(await ReadTheTableAsync(), "email");

    row.ShouldNotBeNull();
    row.ShouldContain("retenue");
    row.ShouldContain("<form");
    row.ShouldContain($"value=\"{ScreenedColumnState.SetAside.Name}\"");
  }

  /// <summary>
  /// ⚠️ <b>La touche Entrée ne retient rien.</b> Un formulaire à champ texte soumet, sur Entrée, son
  /// <b>premier</b> bouton d'envoi : taper son nom et valider aurait retenu la colonne, ce qui est
  /// très exactement le geste distrait contre lequel les deux boutons ont été écrits — et il aurait
  /// gonflé le compte sur lequel la déclaration se construit ensuite.
  /// </summary>
  [Fact]
  public async Task NeverRetainsAColumnBecauseSomeoneHitEnterInTheNameField()
  {
    await DepositAsync(ScreeningSurface.Column("email", position: 1));

    var table = await ReadTheTableAsync();

    // Le premier bouton d'envoi du formulaire — celui qu'Entrée déclenche — ne tranche pas.
    var firstSubmit = Regex.Match(table, @"name=""Ruling""\s+value=""([^""]+)""");

    firstSubmit.Success.ShouldBeTrue();
    firstSubmit.Groups[1].Value.ShouldBe(
      ScreenedColumnState.Awaiting.Name,
      "Le premier bouton d'envoi est celui que la touche Entrée déclenche : s'il tranche, taper "
      + "son nom et valider retient la colonne.");

    // Et ce que ce bouton poste ne pose rien : la colonne attend toujours.
    await _surface.ArbitrateAsync("email", firstSubmit.Groups[1].Value, "Camille Roux");

    RowOf(await ReadTheTableAsync(), "email").ShouldNotBeNull().ShouldContain("En attente");
  }

  /// <summary>
  /// ⚠️ <b>Un rapport déposé pendant la lecture ne reçoit pas la signature de celui qui lisait
  /// l'autre.</b> « Courant » se recalcule à l'écriture : sans ce refus, un collègue qui dépose un
  /// relevé fait atterrir l'arbitrage sur un rapport que le signataire n'a jamais vu — motifs
  /// compris —, et le seul indice en serait son propre nom.
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
      "email", ScreenedColumnState.Retained.Name, "Camille Roux", screening: read);

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

    var forged = await _surface.ArbitrateAsync("email", "PasUnArbitrage", "Camille Roux");

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
      "une_colonne_qui_n_existe_pas", ScreenedColumnState.Retained.Name, "Camille Roux");

    var report = WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.Report));

    report.ShouldContain("n'est plus dans le rapport de détection courant");
  }

  /// <summary>
  /// <b>Un refus ne fait pas retaper son nom.</b> C'est le seul chemin d'erreur de ce formulaire, et
  /// retrouver sa ligne parmi cinq mille pour resaisir est le genre de coût qui fait abandonner la
  /// relecture.
  /// </summary>
  [Fact]
  public async Task KeepsTheTypedNameOnTheRefusedRowRatherThanMakingTheOperatorTypeItAgain()
  {
    await DepositAsync(
      ScreeningSurface.Column("email", position: 1),
      ScreeningSurface.Column("montant", position: 2));

    // Un nom que le domaine refuse : une signature n'est pas une prose, et celle-ci est démesurée.
    var refused = await _surface.ArbitrateAsync(
      "email", ScreenedColumnState.Retained.Name, "Camille " + new string('R', 200));

    refused.StatusCode.ShouldBe(HttpStatusCode.OK);

    var table = WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync());

    // Le nom est reposé SUR SA LIGNE...
    RowOf(table, "email").ShouldNotBeNull().ShouldContain("Camille");

    // ...et sur elle seule : reposé partout, il aurait fait signer les autres lignes d'un nom que
    // personne n'a saisi pour elles.
    RowOf(table, "montant").ShouldNotBeNull().ShouldNotContain("Camille");
  }

  /// <summary>
  /// ⚠️ <b>L'identifiant HTML du champ ne se coud pas sur le nom de la colonne.</b> Un nom d'objet
  /// est recopié verbatim du SGBD et peut porter une espace ou un point : l'association du label à
  /// son champ casserait alors en silence, et l'étiquette cesserait de donner le focus.
  /// </summary>
  [Fact]
  public async Task BuildsTheFieldIdentifiersWithoutTheColumnNameSoTheyStayValid()
  {
    await DepositAsync(ScreeningSurface.Column("mon nom.bizarre", position: 1));

    var table = await ReadTheTableAsync();

    table.ShouldNotContain("id=\"sign-mon nom.bizarre\"");

    // Chaque champ garde une étiquette qui le vise vraiment.
    var field = Regex.Match(table, @"<label for=""([^""]+)""");

    field.Success.ShouldBeTrue();
    field.Groups[1].Value.ShouldNotContain(" ");
    table.ShouldContain($"id=\"{field.Groups[1].Value}\"");
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
