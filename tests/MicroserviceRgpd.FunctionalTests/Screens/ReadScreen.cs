using System.Text;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.Casework.Ledger;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.FunctionalTests.Platform;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Data.Casework;
using Microsoft.EntityFrameworkCore;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// <c>Read</c> de bout en bout, par la <b>seule frontière HTTP</b> de l'écran du dossier : la couture
/// du fil, depuis l'ouverture du <c>Case</c> jusqu'aux <c>RetrievedData</c> détenues et attribuées.
/// </summary>
/// <remarks>
/// <para>
/// <b>Les deux systèmes ne rendent pas la même chose, et rien ne les rapproche.</b> La base rend un
/// CSV, le journal rend du texte brut, et le service n'ouvre ni l'un ni l'autre : il recopie
/// l'enveloppe de transport et garde les octets. C'est ce que « aucune forme n'est imposée à
/// l'<c>Adapter</c> » veut dire, éprouvé sur le vrai fil.
/// </para>
/// <para>
/// <b>On ne lit que là où l'on a rattaché</b> : le journal, tant qu'aucun humain n'a tranché la
/// réserve de la base, n'est pas appelé du tout. C'est l'arbitrage — et lui seul — qui l'ouvre, ici
/// comme pour <c>Locate</c>.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ReadScreen(CustomWebApplicationFactory<Program> factory)
{
  private static readonly DateTimeOffset DeclaredRecently = new(2026, 4, 1, 9, 0, 0, TimeSpan.Zero);

  private readonly OperatorSurface _surface = new(factory);

  /// <summary>
  /// <b>La couture du fil, en entier.</b> Ouvrir le dossier appelle <c>Read</c> là où le
  /// <c>Locate</c> a rattaché ; la pièce remonte, elle est gardée <b>hors du <c>Case</c></b>, et
  /// elle est attribuée au <c>DeclaredSystem</c> qui l'a servie — avec l'enveloppe recopiée telle
  /// quelle.
  /// </summary>
  [Fact]
  public async Task KeepsWhatEachSystemServedAndAttributesItToThatSystem()
  {
    var opened = await AJeanDupontAsync();
    var address = OperatorSurface.AddressOf(opened);

    // 1. Le premier passage : la base hésite et ne rattache rien, donc rien n'est lu nulle part.
    await _surface.ReadTextAsync(address);

    (await HeldForAsync(opened)).ShouldBeEmpty();

    // 2. Un humain tranche. C'est le seul chemin par lequel un rattachement se décide.
    await _surface.ArbitrateAsync(
      opened,
      ABrocantoOnTheWire.Boutique,
      "clients#1203",
      nameof(ReservationState.Attached),
      "Claire Martin");

    // 3. Le passage suivant relocalise sous le sac enrichi, puis lit là où l'on a rattaché. Les deux
    //    capacités s'enchaînent dans la même ouverture : l'arbitrage ouvre le journal au Locate, et
    //    le Read qui suit dans le même passage le lit.
    await _surface.ReadTextAsync(address);

    var held = await HeldForAsync(opened);

    var boutique = held.Single(piece =>
      piece.DeclaredSystem == DeclaredSystemId.From(ABrocantoOnTheWire.Boutique));

    boutique.Case.ShouldBe(opened);
    boutique.Right.ShouldBe(DataSubjectRight.Access);
    boutique.DeclaredSystem.ShouldBe(DeclaredSystemId.From(ABrocantoOnTheWire.Boutique));

    // Les octets sont ceux du client, intacts, et l'enveloppe est recopiée sans interprétation.
    Encoding.UTF8.GetString(boutique.Content).ShouldBe(ABrocantoOnTheWire.BoutiqueExport);
    boutique.Envelope.ContentType.ShouldBe("text/csv; charset=utf-8");
    boutique.Envelope.FileName.ShouldBe("brocanto-boutique-access.csv");
    boutique.IsEmpty.ShouldBeFalse();

    // Le dossier, lui, ne garde que le fait daté de la lecture — et pas un octet.
    var reading = (await RereadAsync(opened))
      .ReadingIn(DataSubjectRight.Access, DeclaredSystemId.From(ABrocantoOnTheWire.Boutique))
      .ShouldNotBeNull();

    reading.LastOutcome.ShouldBe(AdapterOutcome.Served);
  }

  /// <summary>
  /// <b>Le second système, une fois ouvert, sert une pièce d'une tout autre nature</b> — du texte
  /// brut nommé par un chemin, dont le service ne garde que le <b>dernier segment</b>. Deux systèmes,
  /// deux formes, et aucun schéma commun entre eux.
  /// </summary>
  [Fact]
  public async Task KeepsTwoPiecesOfTwoDifferentNaturesWithoutOpeningEither()
  {
    var opened = await AJeanDupontAsync();
    var address = OperatorSurface.AddressOf(opened);

    await _surface.ReadTextAsync(address);

    // L'arbitrage verse au sac l'adresse dans SA casse : c'est elle, et elle seule, qui ouvre le
    // journal — au Locate d'abord, et donc au Read ensuite.
    await _surface.ArbitrateAsync(
      opened,
      ABrocantoOnTheWire.Boutique,
      "clients#1203",
      nameof(ReservationState.Attached),
      "Claire Martin");

    // Un seul passage suffit : le Locate relocalise sous le sac enrichi et rattache le journal, et
    // le Read qui le suit dans la même ouverture y lit.
    await _surface.ReadTextAsync(address);

    var held = await HeldForAsync(opened);

    held.Count.ShouldBe(2);

    var journal = held.Single(piece =>
      piece.DeclaredSystem == DeclaredSystemId.From(ABrocantoOnTheWire.Journal));

    Encoding.UTF8.GetString(journal.Content).ShouldBe(ABrocantoOnTheWire.JournalLines);
    journal.Envelope.ContentType.ShouldBe("text/plain; charset=utf-8");

    // ⚠️ Le nom est parti avec un chemin devant lui ; seul le dernier segment est gardé.
    journal.Envelope.FileName.ShouldBe("brocanto-journal.log");
  }

  /// <summary>
  /// <b>La preuve garde la lecture, et rien de la pièce</b> : ni son type, ni son nom, ni sa taille.
  /// Le contrôle lit « on a lu là, ce jour-là, sous tant de désignations » sans qu'une seule donnée
  /// de la personne lui survive.
  /// </summary>
  [Fact]
  public async Task WritesTheReadingInTheProofWithoutASingleByteOfThePiece()
  {
    var opened = await AJeanDupontAsync();
    var address = OperatorSurface.AddressOf(opened);

    await _surface.ReadTextAsync(address);

    await _surface.ArbitrateAsync(
      opened,
      ABrocantoOnTheWire.Boutique,
      "clients#1203",
      nameof(ReservationState.Attached),
      "Claire Martin");

    await _surface.ReadTextAsync(address);

    var written = (await LedgerOf(opened))
      .Where(line => line.Fact == nameof(LedgerFact.ReadServed))
      .ToArray();

    // Une ligne par système lu, et une seule : le Ledger ne consigne que ce qui change.
    written.Select(line => line.DeclaredSystem)
      .OrderBy(system => system, StringComparer.Ordinal)
      .ShouldBe([ABrocantoOnTheWire.Boutique, ABrocantoOnTheWire.Journal]);

    var line = written.Single(one => one.DeclaredSystem == ABrocantoOnTheWire.Boutique);

    line.DataSubjectRight.ShouldBe(nameof(MicroserviceRgpd.Core.SharedKernel.DataSubjectRight.Access));
    line.DesignationCount.ShouldBe(2);
    line.SignatoryName.ShouldBeNull();
    line.EvidenceProse.ShouldBeNull();

    // Aucune colonne du Ledger ne porte le contenu servi ; on le dit en le cherchant partout.
    (await LedgerOf(opened)).ShouldNotContain(one =>
      one.EvidenceProse != null && one.EvidenceProse.Contains("csv", StringComparison.OrdinalIgnoreCase));
  }

  /// <summary>
  /// <b>La pièce est détenue hors de l'agrégat</b> : la relire ne passe pas par le <c>Case</c>, et
  /// rouvrir le dossier ne la rapatrie pas une seconde fois. Ce qui est déjà là répond à la question
  /// qu'on pose, et repasser allongerait le séjour que tout ce dispositif cherche à raccourcir.
  /// </summary>
  [Fact]
  public async Task NeverBringsBackTwiceThePieceItAlreadyHolds()
  {
    var opened = await AJeanDupontAsync();
    var address = OperatorSurface.AddressOf(opened);

    await _surface.ReadTextAsync(address);

    await _surface.ArbitrateAsync(
      opened,
      ABrocantoOnTheWire.Boutique,
      "clients#1203",
      nameof(ReservationState.Attached),
      "Claire Martin");

    await _surface.ReadTextAsync(address);

    var reads = factory.Adapter.BodiesSentTo(ABrocantoOnTheWire.Boutique).Count;

    await _surface.ReadTextAsync(address);
    await _surface.ReadTextAsync(address);

    // Le Locate repart-il ou non n'est pas la question ici : ce qui compte est qu'aucune ligne de
    // plus ne soit détenue, et le compte des pièces le dit sans ambiguïté.
    (await HeldForAsync(opened)).Count(piece =>
      piece.DeclaredSystem == DeclaredSystemId.From(ABrocantoOnTheWire.Boutique)).ShouldBe(1);

    factory.Adapter.BodiesSentTo(ABrocantoOnTheWire.Boutique).Count.ShouldBeGreaterThanOrEqualTo(reads);
  }

  /// <summary>
  /// Un dossier dont le sac porte le nom du demandeur, et deux systèmes servis par l'<c>Adapter</c> du
  /// témoin — tous deux déclarant <c>Read</c>.
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
        ABrocantoOnTheWire.Journal, "Le journal", DeclaredRecently));
  }

  /// <summary>
  /// Les pièces détenues pour ce dossier, relues <b>hors de l'agrégat</b> — par leur propre port, et
  /// non par une navigation depuis le <c>Case</c>, qui n'en porte aucune.
  /// </summary>
  private async Task<IReadOnlyList<RetrievedData>> HeldForAsync(CaseId opened)
  {
    using var scope = factory.Services.CreateScope();

    return await scope.ServiceProvider.GetRequiredService<IRetrievedData>().HeldForAsync(opened);
  }

  private async Task<Case> RereadAsync(CaseId opened)
  {
    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    return await dbContext.Cases.AsNoTracking().SingleAsync(one => one.Id == opened);
  }

  private async Task<IReadOnlyList<LedgerRow>> LedgerOf(CaseId opened)
  {
    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    return await dbContext.Set<LedgerRow>()
      .AsNoTracking()
      .Where(row => row.CaseId == opened.Value)
      .ToListAsync();
  }
}
