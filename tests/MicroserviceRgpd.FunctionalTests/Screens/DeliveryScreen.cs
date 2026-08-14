using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.Casework.EvidenceLog;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.FunctionalTests.Platform;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Data.Casework;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// La remise de bout en bout, par la <b>seule frontière HTTP</b> : <c>Read</c>, puis les
/// <b>deux gestes</b> — télécharger, puis déclarer remis.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le second geste seul date la remise et détruit les pièces.</b> C'est ce que cette classe
/// éprouve sur le vrai fil : après le premier clic, les pièces sont toujours là et l'<c>EvidenceLog</c>
/// ne porte aucune remise ; après le second, la remise est datée et il ne reste plus un octet.
/// </para>
/// <para>
/// <b>Le « 2 sur 3 » s'arrête au <c>EvidenceLog</c>.</b> La page de garde nomme les systèmes un par un,
/// avec les mots de leur champ « contient », et ne porte <b>pas un seul chiffre</b>.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class DeliveryScreen(CustomWebApplicationFactory<Program> factory)
{
  private const string Agence = "brocanto-agence";

  private const string AgenceContents =
    "Un export commercial transmis chaque mois à notre agence.";

  private static readonly DateTimeOffset DeclaredRecently = new(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);

  private readonly OperatorSurface _surface = new(factory);

  /// <summary>
  /// <b>La couture entière</b> : lire, télécharger, déclarer remis. Les pièces survivent au premier
  /// geste et meurent au second, la remise est datée au <c>EvidenceLog</c> par le second seul.
  /// </summary>
  [Fact]
  public async Task DestroysThePiecesAndDatesTheDeliveryOnTheSecondGestureAlone()
  {
    var opened = await AJeanDupontAsync();

    await ReadEverythingAsync(opened);

    // Le catalogue est commun à toute la suite : on affirme ce que CE dossier a lu, jamais un total
    // que le voisin ferait bouger.
    var held = await HeldForAsync(opened);

    held.ShouldContain(piece => piece.DeclaredSystem.Value == ABrocantoOnTheWire.Boutique);
    held.ShouldContain(piece => piece.DeclaredSystem.Value == ABrocantoOnTheWire.Journal);

    // 1. Télécharger. Un fichier revient, et rien ne bouge de ce que le service détient.
    var taken = await _surface.TakeDeliveryAsync(opened, nameof(DataSubjectRight.Access));

    taken.StatusCode.ShouldBe(HttpStatusCode.OK);
    taken.Content.Headers.ContentType!.MediaType.ShouldBe("application/zip");

    (await HeldForAsync(opened)).Count.ShouldBe(held.Count);

    (await EvidenceLogOf(opened)).ShouldNotContain(line => line.Fact == nameof(EvidenceLogFact.DeliveryDeclared));

    var systemsNamedOnThePage = (await DeliveryLetterOfAsync(opened))
      .Split(Environment.NewLine)
      .Count(line => line.StartsWith("- ", StringComparison.Ordinal));

    // 2. Déclarer remis. Ce clic seul date la remise et détruit les pièces.
    var declared = await _surface.DeclareDeliveryAsync(
      opened,
      nameof(DataSubjectRight.Access),
      "Claire Martin");

    declared.StatusCode.ShouldBe(HttpStatusCode.Redirect);

    (await HeldForAsync(opened)).ShouldBeEmpty();

    var line = (await EvidenceLogOf(opened))
      .Single(one => one.Fact == nameof(EvidenceLogFact.DeliveryDeclared));

    line.DataSubjectRight.ShouldBe(nameof(DataSubjectRight.Access));
    line.SignatoryName.ShouldBe("Claire Martin");
    line.SignerVerification.ShouldBe(nameof(SignerVerification.Unauthenticated));

    // Le rapport que le contrôle vient lire. Le numérateur est le compte des pièces PLEINES ; le
    // dénominateur est celui des systèmes que la page de garde énumère — le MÊME ensemble, une ligne
    // « - » par système. Il est strictement plus grand : l'export de l'agence n'est joignable par
    // rien, et la réponse ne le couvre pas.
    line.CoveredSystemCount.ShouldBe(held.Count(piece => !piece.IsEmpty));
    line.DeclaredSystemCount.ShouldBe(systemsNamedOnThePage);
    line.CoveredSystemCount!.Value.ShouldBeLessThan(line.DeclaredSystemCount!.Value);

    // Et rien de ce qui a été remis : ni système, ni nom de fichier, ni prose.
    line.DeclaredSystem.ShouldBeNull();
    line.Prose.ShouldBeNull();
  }

  /// <summary>
  /// <b>Les pièces sont côte à côte, jamais fusionnées</b> : une entrée par système, sous un dossier
  /// portant son identifiant, et les octets sont ceux du client, intacts.
  /// </summary>
  [Fact]
  public async Task PutsOnePiecePerSystemSideBySideWithoutConcatenatingAnything()
  {
    var opened = await AJeanDupontAsync();

    await ReadEverythingAsync(opened);

    var held = await HeldForAsync(opened);

    using var archive = await ArchiveOfAsync(opened);

    // Une entrée par pièce détenue, chacune sous le dossier de SON système, plus la page de garde.
    // Rien n'est fusionné, rien n'est mis à plat : deux systèmes peuvent servir un fichier du même
    // nom, et l'un aurait écrasé l'autre.
    archive.Entries.Select(entry => entry.FullName)
      .OrderBy(name => name, StringComparer.Ordinal)
      .ShouldBe(
      [
        .. held
          .Select(piece => $"{piece.DeclaredSystem.Value}/{piece.Envelope.FileName}")
          .Append(DeliveryLetter.FileName)
          .OrderBy(name => name, StringComparer.Ordinal),
      ]);

    archive.Entries.Select(entry => entry.FullName).ShouldContain(
      $"{ABrocantoOnTheWire.Journal}/brocanto-journal.log");

    var boutique = archive.GetEntry($"{ABrocantoOnTheWire.Boutique}/brocanto-boutique-access.csv")
      .ShouldNotBeNull();

    await using var reading = boutique.Open();
    using var text = new StreamReader(reading, Encoding.UTF8);

    (await text.ReadToEndAsync()).ShouldBe(ABrocantoOnTheWire.BoutiqueExport);
  }

  /// <summary>
  /// La page de garde <b>énumère sans jamais compter</b> : trois listes, les systèmes non couverts
  /// nommés un par un avec les mots de leur champ « contient », et <b>pas un chiffre</b>.
  /// </summary>
  [Fact]
  public async Task WritesThreeListsThatNameEverySystemAndCountNothing()
  {
    var opened = await AJeanDupontAsync();

    await ReadEverythingAsync(opened);

    var page = await DeliveryLetterOfAsync(opened);

    // Première liste : les systèmes dont une pièce est jointe.
    page.ShouldContain("La boutique");
    page.ShouldContain("Le journal");

    // Troisième liste : le système que cette réponse ne couvre pas, nommé DANS LES MOTS de son
    // champ « contient » — c'est cela qui dit à la personne quoi réclamer, là où « 2 sur 3 » ne lui
    // apprendrait rien.
    page.ShouldContain("L'agence");
    page.ShouldContain(AgenceContents);

    // La formule de la seconde liste : un doute, jamais un verdict. « Vous n'avez rien chez nous »
    // aurait fermé une porte que le service n'a pas le droit de fermer.
    page.ShouldContain("sans trouver de rattachement sous les éléments dont nous disposons");
    page.ShouldNotContain("vous n'avez rien chez nous");

    // La clause de non-exhaustivité, en dernier.
    // La prose de la page est retournée à la ligne à la main : on cite donc un fragment qui ne
    // traverse pas un retour.
    page.ShouldContain("Cette liste est celle des systèmes recensés par le responsable de traitement");
    page.ShouldContain("garantit pas qu'il n'en existe pas d'autres.");

    // ⚠️ AUCUN CHIFFRE DE LA MAIN DU SERVICE. Ni compte, ni taux, ni « N sur M » : le « 2 sur 3 »
    // vit au EvidenceLog, et descendre ici donnerait à un recensement faussable en silence l'autorité
    // d'un inventaire. Les lignes qui NOMMENT un système sont hors du compte : leurs mots sont ceux
    // du client, et le service ne réécrit pas ce qu'un humain a déclaré.
    var written = page
      .Split(Environment.NewLine)
      .Where(line => !line.StartsWith("- ", StringComparison.Ordinal));

    written.ShouldNotContain(
      line => Regex.IsMatch(line, @"\d"),
      $"La page de garde porte un chiffre :{Environment.NewLine}{page}");
  }

  /// <summary>
  /// <b>Une remise téléchargée et jamais déclarée remonte dans la file</b>, comme colonne sur une
  /// ligne déjà présente — et elle en disparaît quand quelqu'un déclare l'avoir rendue.
  /// </summary>
  [Fact]
  public async Task BringsBackIntoTheQueueADeliveryTakenAndNeverDeclared()
  {
    var opened = await AJeanDupontAsync();

    await ReadEverythingAsync(opened);

    (await QueueLineOfAsync(opened)).ShouldNotContain("téléchargée, non déclarée");

    await _surface.TakeDeliveryAsync(opened, nameof(DataSubjectRight.Access));

    (await QueueLineOfAsync(opened)).ShouldContain("téléchargée, non déclarée");

    await _surface.DeclareDeliveryAsync(opened, nameof(DataSubjectRight.Access), "Claire Martin");

    (await QueueLineOfAsync(opened)).ShouldNotContain("téléchargée, non déclarée");
  }

  /// <summary>
  /// <b>La ligne de CE dossier</b> dans la file, et non la page entière : la colonne est une colonne
  /// sur une ligne, et l'affirmer sur toute la page ferait dépendre le test des dossiers que les
  /// autres ont laissés ouverts.
  /// </summary>
  private async Task<string> QueueLineOfAsync(CaseId opened)
  {
    var rows = (await _surface.ReadAsync(OperatorSurface.Queue)).Split("<tr");

    return rows.Single(row => row.Contains(opened.Value.ToString(), StringComparison.Ordinal));
  }

  /// <summary>
  /// <b>Le second geste demande le premier.</b> Déclarer remis un fichier que personne n'a jamais eu
  /// en main daterait un geste qui n'a pas eu lieu — et détruirait des pièces que personne n'a
  /// tendues.
  /// </summary>
  [Fact]
  public async Task RefusesToDeclareTheDeliveryOfSomethingNoOneEverTook()
  {
    var opened = await AJeanDupontAsync();

    await ReadEverythingAsync(opened);

    var declared = await _surface.DeclareDeliveryAsync(
      opened,
      nameof(DataSubjectRight.Access),
      "Claire Martin");

    declared.StatusCode.ShouldBe(HttpStatusCode.NotFound);

    (await HeldForAsync(opened)).ShouldNotBeEmpty();
    (await EvidenceLogOf(opened)).ShouldNotContain(line => line.Fact == nameof(EvidenceLogFact.DeliveryDeclared));
  }

  /// <summary>
  /// <b>Le service ne remet rien à personne.</b> L'écran ne porte aucun lien à jeton, et rien ne
  /// part vers la personne concernée : ce que le second geste consigne est le constat signé d'un
  /// humain.
  /// </summary>
  [Fact]
  public async Task NeverHandsThePackageToTheDataSubjectItself()
  {
    var opened = await AJeanDupontAsync();

    await ReadEverythingAsync(opened);

    // Le second geste ne s'offre qu'après le premier : c'est lui qui porte la phrase.
    await _surface.TakeDeliveryAsync(opened, nameof(DataSubjectRight.Access));

    var screen = await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    screen.ShouldNotContain("mailto:");
    screen.ShouldContain("Le service ne remet rien à personne");

    // Aucun lien à jeton : ce qui sort du service sort par la main de l'Operator, jamais par une
    // adresse qu'un tiers pourrait suivre.
    Regex.IsMatch(
        await _surface.ReadAsync(OperatorSurface.AddressOf(opened)),
        @"href=""[^""]*(token|jeton)",
        RegexOptions.IgnoreCase)
      .ShouldBeFalse();
  }

  /// <summary>
  /// La page de garde, telle qu'elle sort de l'archive téléchargée.
  /// </summary>
  private async Task<string> DeliveryLetterOfAsync(CaseId opened)
  {
    using var archive = await ArchiveOfAsync(opened);

    await using var reading = archive.GetEntry(DeliveryLetter.FileName).ShouldNotBeNull().Open();
    using var text = new StreamReader(reading, Encoding.UTF8);

    return await text.ReadToEndAsync();
  }

  private async Task<ZipArchive> ArchiveOfAsync(CaseId opened)
  {
    var taken = await _surface.TakeDeliveryAsync(opened, nameof(DataSubjectRight.Access));

    taken.StatusCode.ShouldBe(HttpStatusCode.OK);

    return new ZipArchive(
      new MemoryStream(await taken.Content.ReadAsByteArrayAsync()),
      ZipArchiveMode.Read);
  }

  /// <summary>
  /// Ouvre le dossier, tranche la réserve de la boutique, et rouvre : c'est la couture de
  /// <c>Read</c>, dont la remise est la suite.
  /// </summary>
  private async Task ReadEverythingAsync(CaseId opened)
  {
    var address = OperatorSurface.AddressOf(opened);

    await _surface.ReadTextAsync(address);

    await _surface.ArbitrateAsync(
      opened,
      ABrocantoOnTheWire.Boutique,
      "clients#1203",
      nameof(ReservationState.Attached),
      "Claire Martin");

    await _surface.ReadTextAsync(address);
  }

  /// <summary>
  /// Un dossier sur <b>trois</b> systèmes recensés : deux que l'<c>Adapter</c> du témoin sert, et un
  /// qu'aucun <c>Adapter</c> ne joint — celui que la réponse ne couvrira pas.
  /// </summary>
  private async Task<CaseId> AJeanDupontAsync()
  {
    return await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      ClaimOrigin.Named,
      motivation: null,
      [Designation.Of(DesignationKind.PersonName, "Jean Dupont")],
      [DataSubjectRight.Access],
      OperatorSurface.AReadableSystemServedByAnAdapter(
        ABrocantoOnTheWire.Boutique, "La boutique", DeclaredRecently),
      OperatorSurface.AReadableSystemServedByAnAdapter(
        ABrocantoOnTheWire.Journal, "Le journal", DeclaredRecently),
      DeclaredSystem.Declare(
        DeclaredSystemId.From(Agence),
        SystemLabel.From("L'agence"),
        SystemContents.From(AgenceContents),
        [],
        adapterAddress: null,
        DeclaredRecently));
  }

  private async Task<IReadOnlyList<RetrievedData>> HeldForAsync(CaseId opened)
  {
    using var scope = factory.Services.CreateScope();

    return await scope.ServiceProvider.GetRequiredService<IRetrievedData>().HeldForAsync(opened);
  }

  private async Task<IReadOnlyList<EvidenceLogRow>> EvidenceLogOf(CaseId opened)
  {
    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    return await dbContext.Set<EvidenceLogRow>()
      .AsNoTracking()
      .Where(row => row.CaseId == opened.Value)
      .ToListAsync();
  }
}
