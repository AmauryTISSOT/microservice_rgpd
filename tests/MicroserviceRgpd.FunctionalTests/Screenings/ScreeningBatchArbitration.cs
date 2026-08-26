using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// Le <b>geste de lot</b>, éprouvé par la seule frontière qui existe : le formulaire de l'écran d'une
/// table.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il existe pour qu'un rapport de cinq mille colonnes reste tenable</b> : un écran intenable
/// rétablit l'<c>Omission silencieuse</c> par l'épuisement, et personne n'abandonne en déclarant
/// qu'il abandonne. Ce que ces tests gardent est donc autant ce qu'il fait que <b>ce qu'il ne touche
/// jamais</b>.
/// </para>
/// <para>
/// ⚠️ <b>Rien n'est écrit en base à la main, et aucun geste n'est appelé directement.</b> Ce que
/// cette tranche livre est un formulaire : l'éprouver par MediatR aurait laissé passer un écran qui
/// poste des noms de colonnes, ou qui offre le lot sur le rapport entier.
/// </para>
/// <para>
/// ⚠️ <b>L'horloge n'est pas doublée</b>, comme le moteur ne l'est pas : c'est le vrai
/// <c>TimeProvider</c> qui date ici, et les tests le vérifient à la journée.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ScreeningBatchArbitration(CustomWebApplicationFactory<Program> factory)
{
  private readonly ScreeningSurface _surface = new(factory);

  /// <summary>
  /// <b>Le lot tranche toutes les colonnes où rien n'a été vu de la table ouverte, et chacune porte
  /// sa propre date.</b> Ce ne sont pas des états de lot : ce sont n arbitrages, que l'on reprend
  /// ensuite ligne à ligne comme les autres.
  /// </summary>
  [Fact]
  public async Task SettlesEveryUnflaggedColumnOfTheOpenTableUnderItsOwnDate()
  {
    await DepositAsync(
      ScreeningSurface.Column("email", position: 1),
      ScreeningSurface.Column("montant", position: 2),
      ScreeningSurface.Column("quantite", position: 3));

    var before = await ReadTheTableAsync();

    ScreeningSurface.BlockOf(before, "montant").ShouldNotBeNull().ShouldContain("Rien n'a été vu");
    ScreeningSurface.BlockOf(before, "quantite").ShouldNotBeNull().ShouldContain("Rien n'a été vu");

    var batched = await _surface.ArbitrateInBatchAsync(ScreenedColumnState.SetAside.Name);

    // ⚠️ Le succès REDIRIGE : un rechargement ne doit pas reposer le lot.
    batched.StatusCode.ShouldBe(HttpStatusCode.Redirect);

    var table = await ReadTheTableAsync();

    foreach (var column in new[] { "montant", "quantite" })
    {
      var row = ScreeningSurface.BlockOf(table, column).ShouldNotBeNull();

      row.ShouldContain("écartée");

      // La date est celle du service, à la journée — le formulaire n'en porte aucun champ.
      row.ShouldContain(Today());
    }
  }

  /// <summary>
  /// ⚠️ <b>Aucun geste de lot ne porte sur une colonne signalée.</b> Une suspicion ne s'écarte jamais
  /// sans avoir été lue une par une : l'écarter en masse est très exactement ce que le rapport existe
  /// pour empêcher.
  /// </summary>
  [Fact]
  public async Task NeverReachesAColumnTheScreeningFlagged()
  {
    await DepositAsync(
      ScreeningSurface.Column("email", position: 1),
      ScreeningSurface.Column("montant", position: 2));

    ScreeningSurface.BlockOf(await ReadTheTableAsync(), "email").ShouldNotBeNull().ShouldNotContain("Rien n'a été vu");

    await _surface.ArbitrateInBatchAsync(ScreenedColumnState.SetAside.Name);

    var table = await ReadTheTableAsync();
    var flagged = ScreeningSurface.BlockOf(table, "email").ShouldNotBeNull();

    flagged.ShouldContain("En attente");

    // ⚠️ Et la phrase d'achèvement ne se referme PAS sur le lot : elle dit que les colonnes où rien
    // n'a été vu ont été relues, et que la signalée, elle, attend toujours. Un lot qui aurait rendu
    // « tout a été relu » aurait tranché la suspicion sans que personne ne l'ait lue.
    table.ShouldContain("Toutes les colonnes où rien n'a été vu ont été relues");
    table.ShouldContain("1 colonne signalée attend encore d'être tranchée");
    table.ShouldNotContain("Toutes les colonnes de ce rapport de détection ont été relues");
  }

  /// <summary>
  /// ⚠️ <b>Le formulaire du lot ne poste aucun nom de colonne, et il ne doit jamais pouvoir en
  /// poster.</b> C'est ce qui rend « aucun lot ne porte sur une colonne signalée » impossible à
  /// contourner par un formulaire forgé : il n'y a rien à forger — le geste nomme une table, et le
  /// domaine décide de ce qu'il atteint dedans.
  /// </summary>
  [Fact]
  public async Task OffersNoWayAtAllToNameTheColumnsAndBatchGestureShouldReach()
  {
    await DepositAsync(
      ScreeningSurface.Column("email", position: 1),
      ScreeningSurface.Column("montant", position: 2));

    var batchForm = Regex.Match(
      await ReadTheTableAsync(),
      @"<section class=""batch"">.*?</section>",
      RegexOptions.Singleline);

    batchForm.Success.ShouldBeTrue("L'écran d'une table n'offre aucun geste de lot.");

    batchForm.Value.ShouldNotContain("name=\"Column\"");
    batchForm.Value.ShouldNotContain("type=\"checkbox\"");

    // Il dit en revanche sa portée exacte AVANT le clic : on ne tranche pas sans savoir sur quoi.
    batchForm.Value.ShouldContain("1 colonne");
  }

  /// <summary>
  /// ⚠️ <b>Le bouton du lot poste sur la table qu'on lit, et la table voyage dans l'adresse.</b>
  /// Cette forme-ci est la seule de l'écran à porter un attribut de tag helper, donc la seule dont
  /// l'<c>action</c> est fabriquée plutôt qu'héritée de l'URL courante — et la fabrication ne reprend
  /// que les valeurs de route, dont cette page n'a aucune. Sans le schéma et la table réécrits, le
  /// clic arriverait sans table nommée et le geste se refuserait lui-même : le lot serait mort dans
  /// un vrai navigateur sans qu'aucun test postant l'adresse à la main ne s'en aperçoive.
  /// </summary>
  [Fact]
  public async Task PostsTheBatchBackOntoTheTableThatIsOpenRatherThanNowhere()
  {
    await DepositAsync(ScreeningSurface.Column("montant", position: 1));

    var batchForm = Regex.Match(
      WebUtility.HtmlDecode(await ReadTheTableAsync()),
      @"<section class=""batch"">.*?</section>",
      RegexOptions.Singleline);

    var action = Regex.Match(batchForm.Value, @"action=""([^""]+)""");

    action.Success.ShouldBeTrue("Le geste de lot ne fabrique aucune action.");

    action.Groups[1].Value.ShouldContain("schema=public");
    action.Groups[1].Value.ShouldContain("table=adherents");
    action.Groups[1].Value.ShouldContain("handler=Batch");
  }

  /// <summary>
  /// ⚠️ <b>Le lot ne sort jamais de la table ouverte.</b> Un lot qui porterait sur le rapport entier
  /// trancherait d'un clic ce que l'<c>Operator</c> n'a pas sous les yeux — et la table est l'unité
  /// de travail précisément parce que c'est l'unité qu'on lit.
  /// </summary>
  [Fact]
  public async Task NeverCrossesOutOfTheTableThatIsOpen()
  {
    await DepositAsync(
      ScreeningSurface.Column("montant", position: 1),
      ScreeningSurface.Column("montant", table: "cotisations", position: 1));

    await _surface.ArbitrateInBatchAsync(ScreenedColumnState.Retained.Name);

    ScreeningSurface.BlockOf(await ReadTheTableAsync(), "montant").ShouldNotBeNull().ShouldContain("retenue");

    ScreeningSurface.BlockOf(await ReadTheTableAsync("cotisations"), "montant")
      .ShouldNotBeNull()
      .ShouldContain("En attente");
  }

  /// <summary>
  /// <b>Une colonne déjà tranchée de la table ouverte n'est pas réécrite par le lot.</b> Le geste
  /// liquide ce qui attend ; il n'efface pas d'un clic ce qu'un humain avait dit.
  /// </summary>
  [Fact]
  public async Task LeavesAnAlreadyArbitratedColumnExactlyAsTheHumanWhoSettledItLeftIt()
  {
    await DepositAsync(
      ScreeningSurface.Column("montant", position: 1),
      ScreeningSurface.Column("quantite", position: 2));

    await _surface.ArbitrateAsync("montant", ScreenedColumnState.Retained.Name);

    await _surface.ArbitrateInBatchAsync(ScreenedColumnState.SetAside.Name);

    var table = await ReadTheTableAsync();
    var settled = ScreeningSurface.BlockOf(table, "montant").ShouldNotBeNull();

    // ⚠️ Elle porte toujours SON issue, et le lot ne l'a pas retournée.
    settled.ShouldContain("retenue");
    settled.ShouldNotContain("écartée");

    // Et celle qui attendait, elle, a bien été tranchée par le lot.
    ScreeningSurface.BlockOf(table, "quantite").ShouldNotBeNull().ShouldContain("écartée");
  }

  /// <summary>
  /// ⚠️ <b>Un lot qui ne tranche rien n'écrit pas une seule colonne</b>, et il ne l'a pas fait
  /// trente fois. <c>Awaiting</c> n'est pas une issue, en lot pas plus qu'à l'unité.
  /// </summary>
  [Fact]
  public async Task RefusesAWholeBatchThatPostsAwaitingAndLeavesEveryColumnAwaiting()
  {
    await DepositAsync(
      ScreeningSurface.Column("montant", position: 1),
      ScreeningSurface.Column("quantite", position: 2));

    var refused = await _surface.ArbitrateInBatchAsync(ScreenedColumnState.Awaiting.Name);

    // ⚠️ Le refus se relit SUR PLACE : le seul geste raisonnable après un refus est de corriger
    // devant le texte qui l'explique.
    refused.StatusCode.ShouldBe(HttpStatusCode.OK);

    var rendered = WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync());

    rendered.ShouldContain("Aucun arbitrage n'a été enregistré");

    // Le refus ne nomme aucune colonne : le geste n'en désigne pas.
    rendered.ShouldContain("Sur le geste de lot");

    var table = await ReadTheTableAsync();

    ScreeningSurface.BlockOf(table, "montant").ShouldNotBeNull().ShouldContain("En attente");
    ScreeningSurface.BlockOf(table, "quantite").ShouldNotBeNull().ShouldContain("En attente");
  }

  /// <summary>
  /// ⚠️ <b>La touche Entrée ne tranche pas une table entière, et c'est l'absence de commande
  /// textuelle qui le tient.</b> Un formulaire soumet sur Entrée son <b>premier</b> bouton d'envoi,
  /// mais seulement depuis une commande textuelle : le champ du nom parti, il n'en reste aucune.
  /// <b>En reposer une ici remettrait la trappe</b> — taper puis valider trancherait des dizaines
  /// de colonnes d'un coup, ce qui est le facteur qui rend la chose plus grave qu'à l'unité.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le test n'énumère pas les types interdits, il n'autorise que <c>hidden</c></b> — même
  /// raison qu'à l'unité : <c>search</c>, <c>email</c>, <c>url</c>, <c>number</c>, <c>tel</c> et
  /// <c>password</c> déclenchent la soumission implicite exactement comme <c>text</c>.
  /// </remarks>
  [Fact]
  public async Task CarriesNothingButHiddenInputsSoTheEnterKeyCannotSettleAWholeTable()
  {
    await DepositAsync(
      ScreeningSurface.Column("montant", position: 1),
      ScreeningSurface.Column("quantite", position: 2));

    var batchForm = Regex.Match(
      await ReadTheTableAsync(),
      @"<section class=""batch"">.*?</section>",
      RegexOptions.Singleline);

    batchForm.Success.ShouldBeTrue("L'écran d'une table n'offre aucun geste de lot.");

    batchForm.Value.ShouldNotContain("<textarea");

    var fields = Regex.Matches(batchForm.Value, @"<input[^>]*>")
      .Select(field => field.Value)
      .ToList();

    fields.ShouldNotBeEmpty("Le geste de lot ne poste plus le rapport lu.");

    fields.ShouldAllBe(
      field => field.Contains("type=\"hidden\"", StringComparison.Ordinal),
      customMessage: "Toute commande non cachée rendrait à la touche Entrée la soumission implicite.");
  }

  /// <summary>
  /// ⚠️ <b>Un rapport déposé pendant la lecture ne reçoit pas le lot de celui qui lisait l'autre.</b>
  /// Le facteur est ce qui rend ce refus plus grave qu'à l'unité : sans lui, le geste d'un humain
  /// atterrit d'un seul coup sur des dizaines de colonnes d'un rapport qu'il n'a jamais vu.
  /// </summary>
  [Fact]
  public async Task RefusesTheBatchWhenANewerScreeningWasDepositedDuringTheReading()
  {
    await DepositAsync(ScreeningSurface.Column("montant", position: 1));

    var read = ScreeningOf(await ReadTheTableAsync());

    await DepositAsync(ScreeningSurface.Column("montant", position: 1));

    var refused = await _surface.ArbitrateInBatchAsync(
      ScreenedColumnState.Retained.Name, screening: read);

    refused.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    refused.Headers.Location!.OriginalString.ShouldContain(ScreeningSurface.Report);

    ScreeningSurface.BlockOf(await ReadTheTableAsync(), "montant").ShouldNotBeNull().ShouldContain("En attente");

    var report = WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.Report));

    report.ShouldContain("rapport de détection plus récent");
    report.ShouldContain("n'a pas été enregistré");
  }

  /// <summary>
  /// ⚠️ <b>Le lot dit ce qu'il a fait ET ce qu'il n'a pas touché.</b> Un geste qui annoncerait
  /// seulement « deux colonnes tranchées » laisserait l'<c>Operator</c> devant une table qu'il croit
  /// finie alors que ses suspicions, elles, n'ont pas bougé.
  /// </summary>
  [Fact]
  public async Task SaysHowManyColumnsItSettledAndHowManyFlaggedOnesItLeftToBeReadOneByOne()
  {
    await DepositAsync(
      ScreeningSurface.Column("email", position: 1),
      ScreeningSurface.Column("montant", position: 2),
      ScreeningSurface.Column("quantite", position: 3));

    await _surface.ArbitrateInBatchAsync(ScreenedColumnState.Retained.Name);

    var table = await ReadTheTableAsync();

    table.ShouldContain("2 colonnes où rien n'avait été vu ont été retenues dans cette table");
    table.ShouldContain("1 colonne signalée y attend toujours");
    table.ShouldContain("elles se lisent une par une");
  }

  /// <summary>
  /// <b>Un lot qui n'atteint rien le dit</b>, plutôt que de rendre la table sans un mot : deux
  /// redirections identiques ne se distinguent pas, et l'<c>Operator</c> repartirait en croyant avoir
  /// tranché.
  /// </summary>
  [Fact]
  public async Task SaysThatNothingWasSettledWhenTheTableHasNothingLeftWithinReach()
  {
    await DepositAsync(ScreeningSurface.Column("montant", position: 1));

    await _surface.ArbitrateInBatchAsync(ScreenedColumnState.Retained.Name);

    // Le second lot ne trouve plus rien : tout ce qu'il pouvait atteindre a été relu.
    await _surface.ArbitrateInBatchAsync(ScreenedColumnState.SetAside.Name);

    var table = await ReadTheTableAsync();

    table.ShouldContain("Aucune colonne n'a été tranchée par ce geste");

    // Et l'arbitrage rendu n'a pas été écrasé par le lot qui n'atteignait rien.
    ScreeningSurface.BlockOf(table, "montant").ShouldNotBeNull().ShouldContain("retenue");
  }

  /// <summary>
  /// <b>Une table que le rapport courant ne porte pas ramène au rapport, et le dit</b> — un écran
  /// affiché il y a une minute peut nommer une table qu'un second rapport de détection vient
  /// d'emporter.
  /// </summary>
  [Fact]
  public async Task SendsBackToTheReportWhenTheTableHasLeftTheCurrentScreening()
  {
    await DepositAsync(ScreeningSurface.Column("montant", position: 1));

    var read = ScreeningOf(await ReadTheTableAsync());

    var stale = await _surface.ArbitrateInBatchAsync(
      ScreenedColumnState.Retained.Name,
      table: "une_table_qui_n_existe_pas",
      screening: read,
      renderedFrom: "adherents");

    stale.StatusCode.ShouldBe(HttpStatusCode.Redirect);
    stale.Headers.Location!.OriginalString.ShouldContain(ScreeningSurface.Report);

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
}
