using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// Le <b>cycle de vie</b> d'un rapport, par sa seule frontière HTTP : un nouveau dépôt archive
/// l'ancien, l'archivé se relit en entier sans s'arbitrer, et un humain le supprime quand il le
/// décide.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>« Archivé » n'est écrit nulle part.</b> C'est le calcul du plus récemment lancé, refait à
/// chaque rendu. Ces tests l'éprouvent par la seule chose qui le prouve : rien n'a été posté sur
/// l'ancien rapport entre le moment où il était courant et celui où il ne l'est plus.
/// </para>
/// <para>
/// ⚠️ <b>Le re-dépôt ne fusionne rien</b>, et c'est le prix déclaré de cette version. Ce que ces
/// tests garantissent n'est pas que les arbitrages soient repris — ils ne le sont pas — mais qu'ils
/// ne soient pas <b>perdus</b> : ils restent lisibles sur le rapport qui les porte.
/// </para>
/// <para>
/// <b>La collection est partagée</b>, et le déploiement porte les rapports des autres tests : chaque
/// test ne parle donc que des rapports qu'il a lui-même déposés, jamais du compte de l'historique.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ScreeningLifecycle(CustomWebApplicationFactory<Program> factory)
{
  private readonly ScreeningSurface _surface = new(factory);

  /// <summary>
  /// ⚠️ <b>Le test central de ce fichier.</b> Un second dépôt range le premier rapport sans
  /// <b>toucher</b> à ce qu'un humain y avait tranché : la signature d'alors se relit, à la date et
  /// au nom près, sur le rapport archivé.
  /// </summary>
  [Fact]
  public async Task ANewDepositArchivesThePreviousReportWithoutTouchingItsArbitrations()
  {
    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(
        ScreeningSurface.Column("email", position: 1),
        ScreeningSurface.Column("montant", position: 2)));

    await _surface.ArbitrateAsync("email", ScreenedColumnState.Retained.Name, "Camille Roux");

    var archived = await _surface.CurrentScreeningAsync();

    // Le second dépôt : c'est LUI, et rien d'autre, qui archive le premier.
    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(ScreeningSurface.Column("email", position: 1)));

    var current = await _surface.CurrentScreeningAsync();
    current.ShouldNotBe(archived, "Un nouveau dépôt produit un rapport neuf.");

    // ⚠️ L'arbitrage d'alors est INTACT sur le rapport qui le portait : ni repris, ni effacé.
    var table = await _surface.ReadAsync(ScreeningSurface.ArchivedTableOf(archived));
    table.ShouldContain("Camille Roux");
    table.ShouldContain(ScreenedColumnState.Retained.FrenchLabel);

    // Et le courant est reparti d'une page blanche : il ne l'a pas repris.
    var live = await _surface.ReadAsync(ScreeningSurface.TableOf());
    live.ShouldNotContain("Camille Roux");
  }

  /// <summary>L'historique nomme les archivés — et distingue celui qui ne l'est pas.</summary>
  [Fact]
  public async Task ListsTheArchivedReportsInTheHistory()
  {
    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(ScreeningSurface.Column("email", position: 1)));

    var archived = await _surface.CurrentScreeningAsync();

    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(ScreeningSurface.Column("email", position: 1)));

    var current = await _surface.CurrentScreeningAsync();
    var history = await _surface.ReadAsync(ScreeningSurface.History);

    // L'ancien est atteignable depuis l'historique, par le lien qui l'ouvre en archivé.
    history.ShouldContain($"screening={archived}");

    // ⚠️ Et le courant n'y figure pas comme archivé : la ligne qui le porte est celle du courant,
    // qui ne renvoie pas à l'écran des archivés.
    history.ShouldNotContain($"/depistage/archive?screening={current}");
  }

  /// <summary>
  /// ⚠️ <b>Un archivé se lit EN ENTIER</b> — les colonnes où le dépistage n'avait rien vu comprises —
  /// <b>et n'offre aucun geste d'arbitrage</b>. Un archivé amputé serait un rapport dont plus
  /// personne ne peut vérifier ce qui avait été omis.
  /// </summary>
  [Fact]
  public async Task ReadsAnArchivedReportInFullAndOffersNoArbitrationGesture()
  {
    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(
        ScreeningSurface.Column("email", position: 1),
        ScreeningSurface.Column("montant", position: 2),
        ScreeningSurface.Column("id_adh", position: 3)));

    var archived = await _surface.CurrentScreeningAsync();

    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(ScreeningSurface.Column("email", position: 1)));

    var table = await _surface.ReadAsync(ScreeningSurface.ArchivedTableOf(archived));

    // Les trois colonnes sont là, signalées comme non signalées.
    RowCountOf(table).ShouldBe(3);
    table.ShouldContain("Rien n'a été vu");

    // ⚠️ AUCUN FORMULAIRE DU TOUT : ni arbitrage à l'unité, ni geste de lot, ni suppression. « Non
    // arbitrable » n'est pas une condition posée dans un rendu — c'est un écran qui n'a pas de quoi
    // poster.
    table.ShouldNotContain("<form");
    table.ShouldNotContain("<button");

    // Le sommaire de l'archivé non plus.
    var report = await _surface.ReadAsync(ScreeningSurface.ArchiveOf(archived));
    report.ShouldNotContain("<form");
    report.ShouldNotContain("<button");
  }

  /// <summary>
  /// La suppression emporte le rapport <b>et ses colonnes</b> : après elle, ni l'historique ni les
  /// écrans de l'archivé ne le rendent plus.
  /// </summary>
  [Fact]
  public async Task DeletesAReportWithAllOfItsColumns()
  {
    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(
        ScreeningSurface.Column("email", position: 1),
        ScreeningSurface.Column("montant", position: 2)));

    var deleted = await _surface.CurrentScreeningAsync();

    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(ScreeningSurface.Column("email", position: 1)));

    var gone = await _surface.DeleteAsync(deleted, "galette_prod");
    gone.StatusCode.ShouldBe(
      HttpStatusCode.Redirect,
      "Le succès redirige : un rechargement ne doit pas reproposer d'envoyer ce formulaire-là.");

    var history = await _surface.ReadAsync(ScreeningSurface.History);
    history.ShouldContain("a été supprimé");
    history.ShouldNotContain($"screening={deleted}");

    // ⚠️ Le rapport n'est plus lisible NULLE PART — et ses colonnes sont parties avec lui : l'écran
    // d'une de ses tables ne rend rien, faute de colonnes à rendre.
    var reopened = await _surface.Client.GetAsync(ScreeningSurface.ArchiveOf(deleted));
    reopened.StatusCode.ShouldBe(HttpStatusCode.Redirect);

    var reopenedTable = await _surface.Client.GetAsync(ScreeningSurface.ArchivedTableOf(deleted));
    reopenedTable.StatusCode.ShouldBe(HttpStatusCode.Redirect);
  }

  /// <summary>
  /// ⚠️ <b>Un nom de base mal retapé ne supprime rien.</b> La confrontation est faite par le service,
  /// contre le nom qu'il détient : c'est le seul garde-fou d'un geste sans retour et sans trace.
  /// </summary>
  [Fact]
  public async Task RefusesToDeleteWhenTheDatabaseNameIsNotRetypedExactly()
  {
    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(ScreeningSurface.Column("email", position: 1)));

    var spared = await _surface.CurrentScreeningAsync();

    var refused = await _surface.DeleteAsync(spared, "galette");
    refused.StatusCode.ShouldBe(
      HttpStatusCode.OK,
      "Un refus se relit sur l'historique : il ne redirige pas, et il ne rend pas une erreur nue.");

    var rendered = WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync());
    rendered.ShouldContain("galette_prod");

    // Le rapport est toujours là, et il s'arbitre toujours.
    (await _surface.CurrentScreeningAsync()).ShouldBe(spared);
  }

  /// <summary>
  /// Supprimer le <b>courant</b> rend son rang au plus récent de ceux qui restent — <b>sans qu'une
  /// seule écriture n'ait eu lieu</b> pour cela — et l'écran le dit, parce qu'un
  /// <c>Operator</c> qui l'ignore arbitrerait un rapport qu'il croyait rangé.
  /// </summary>
  [Fact]
  public async Task GivesTheCurrentRankBackToThePreviousReportWhenTheCurrentOneIsDeleted()
  {
    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(ScreeningSurface.Column("email", position: 1)));

    var previous = await _surface.CurrentScreeningAsync();

    await _surface.DepositAndReadTheReportAsync(
      ScreeningSurface.Paste(ScreeningSurface.Column("email", position: 1)));

    var current = await _surface.CurrentScreeningAsync();

    await _surface.DeleteAsync(current, "galette_prod");

    var history = WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.History));
    history.ShouldContain("C'était le dépistage courant");

    // ⚠️ Le rang s'est déplacé tout seul : le précédent s'arbitre de nouveau, et son écran
    // d'archivé n'existe plus.
    (await _surface.CurrentScreeningAsync()).ShouldBe(previous);

    var reopened = await _surface.Client.GetAsync(ScreeningSurface.ArchiveOf(previous));
    reopened.StatusCode.ShouldBe(
      HttpStatusCode.Redirect,
      "Un rapport redevenu courant ne se rend plus comme archivé : l'historique dit ce qui est vrai.");
  }

  private static int RowCountOf(string rendered)
  {
    var body = Regex.Match(rendered, "<tbody>(.*?)</tbody>", RegexOptions.Singleline);

    body.Success.ShouldBeTrue("L'écran d'une table archivée doit porter un tableau de colonnes.");

    return Regex.Matches(body.Groups[1].Value, "<tr>").Count;
  }
}
