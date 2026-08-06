using System.Text;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Data.Casework;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.IntegrationTests.Data.Casework;

/// <summary>
/// Ce qu'un <c>Read</c> laisse en base : le <b>fait</b> dans le dossier, et la <b>pièce</b> ailleurs.
/// </summary>
/// <remarks>
/// <para>
/// La base est réelle parce que c'est elle, et elle seule, qui prouve la séparation : les
/// <see cref="RetrievedData"/> vivent dans leur propre table, sans clé étrangère vers <c>cases</c>,
/// et se relisent <b>sans passer par la racine</b>. C'est la condition d'une durée de vie qui n'est
/// pas celle du dossier — la remise détruira la pièce sans réécrire le <c>Case</c>.
/// </para>
/// <para>
/// L'autre moitié — le <see cref="Reading"/> — est <b>dans</b> l'agrégat, et remonte avec le dossier
/// sans qu'aucun <c>Include</c> ne l'ait demandée.
/// </para>
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public class RetrievedDataPersistenceTests(PostgreSqlFixture postgres)
{
  private static readonly DateTimeOffset Received = new(2026, 4, 1, 8, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset Asked = new(2026, 4, 12, 10, 0, 0, TimeSpan.Zero);
  private static readonly DeclaredSystemId Boutique = DeclaredSystemId.From("brocanto-boutique");
  private static readonly DeclaredSystemId Journal = DeclaredSystemId.From("brocanto-journal");

  private static readonly byte[] Csv = Encoding.UTF8.GetBytes("id;email\n1203;jean.dupont@example.fr\n");

  /// <summary>
  /// La pièce fait l'aller-retour <b>entière</b> : ses octets, son enveloppe recopiée, et son
  /// attribution au couple (droit, système) qui l'a servie.
  /// </summary>
  [Fact]
  public async Task RoundTripsThePieceAndTheEnvelopeItWasServedUnder()
  {
    var opened = await ACaseAsync();

    await KeepAsync(
      RetrievedData.Of(
        opened,
        DataSubjectRight.Access,
        Boutique,
        new RetrievedPiece(
          TransportEnvelope.Of("text/csv; charset=utf-8", "brocanto-boutique-access.csv", Boutique),
          Csv),
        Asked));

    var kept = (await HeldForAsync(opened)).ShouldHaveSingleItem();

    kept.Case.ShouldBe(opened);
    kept.Right.ShouldBe(DataSubjectRight.Access);
    kept.DeclaredSystem.ShouldBe(Boutique);
    kept.Content.ShouldBe(Csv);
    kept.Envelope.ContentType.ShouldBe("text/csv; charset=utf-8");
    kept.Envelope.FileName.ShouldBe("brocanto-boutique-access.csv");
    kept.RetrievedAt.ShouldBe(Asked);
    kept.IsEmpty.ShouldBeFalse();
  }

  /// <summary>
  /// <b>Une pièce vide se relit vide, et non absente.</b> C'est le cas que la base doit savoir
  /// écrire : « on a regardé, il n'y a rien » est une déclaration, et une colonne qui la
  /// confondrait avec un <c>NULL</c> l'effacerait.
  /// </summary>
  [Fact]
  public async Task TellsAnEmptyPieceApartFromNoPieceAtAll()
  {
    var opened = await ACaseAsync();

    await KeepAsync(
      RetrievedData.Of(
        opened,
        DataSubjectRight.Access,
        Journal,
        new RetrievedPiece(TransportEnvelope.Of(null, null, Journal), []),
        Asked));

    var kept = (await HeldForAsync(opened)).ShouldHaveSingleItem();

    kept.IsEmpty.ShouldBeTrue();
    kept.Content.ShouldBeEmpty();

    // Rien n'ayant été déclaré, l'enveloppe a dégradé sur ce qu'elle savait : le système.
    kept.Envelope.ContentType.ShouldBe(TransportEnvelope.UnnamedContentType);
    kept.Envelope.FileName.ShouldBe(Journal.Value);
  }

  /// <summary>
  /// <b>L'identité de la pièce est le triplet</b> (dossier, droit, système) : deux droits sur le même
  /// système sont deux pièces, et relire le même triplet en réécrit une seule.
  /// </summary>
  [Fact]
  public async Task GrainsAPieceByCaseRightAndSystem()
  {
    var opened = await ACaseAsync();

    await KeepAsync(APieceOf(opened, DataSubjectRight.Access, Boutique, Csv));
    await KeepAsync(APieceOf(opened, DataSubjectRight.Portability, Boutique, Csv));
    await KeepAsync(APieceOf(opened, DataSubjectRight.Access, Journal, Csv));

    (await HeldForAsync(opened)).Count.ShouldBe(3);

    // Le même triplet, servi une seconde fois : une pièce remplacée, jamais une de plus.
    await KeepAsync(APieceOf(opened, DataSubjectRight.Access, Boutique, [1, 2, 3]));

    var held = await HeldForAsync(opened);

    held.Count.ShouldBe(3);
    held.Single(piece => piece.Right == DataSubjectRight.Access && piece.DeclaredSystem == Boutique)
      .Content.ShouldBe(new byte[] { 1, 2, 3 });
  }

  /// <summary>
  /// <b>Les pièces d'un dossier ne remontent pas avec lui.</b> Le <c>Case</c> relu ne les porte pas,
  /// et c'est délibéré : un agrégat qui les traînerait chargerait des données personnelles à chaque
  /// affichage, et sa clôture les emporterait au moment où la <c>Delivery</c> en a besoin.
  /// </summary>
  [Fact]
  public async Task KeepsThePieceOutOfTheAggregateAndTheFactInside()
  {
    var opened = await ACaseAsync();

    await KeepAsync(APieceOf(opened, DataSubjectRight.Access, Boutique, Csv));

    await using (var dbContext = postgres.NewDbContext())
    {
      var reread = (await dbContext.Cases.SingleOrDefaultAsync(one => one.Id == opened)).ShouldNotBeNull();

      // Le fait est là — la lecture, datée, sans un octet — et remonte sans aucun Include.
      var reading = reread.Readings.ShouldHaveSingleItem();

      reading.Right.ShouldBe(DataSubjectRight.Access);
      reading.DeclaredSystem.ShouldBe(Boutique);
      reading.LastOutcome.ShouldBe(AdapterOutcome.Served);
      reading.AskedAt.ShouldBe(Asked);
      reading.DesignationsAtCall.ShouldBe(1);
    }

    // Et la pièce est ailleurs : c'est son propre port qui la rend, jamais la racine.
    (await HeldForAsync(opened)).ShouldHaveSingleItem();
  }

  /// <summary>
  /// Un différé garde son <b>échéance déclarée</b> sur la lecture, et ne détient aucune pièce : le
  /// service repassera après elle, à la prochaine ouverture du dossier.
  /// </summary>
  [Fact]
  public async Task RoundTripsTheDeclaredDeadlineOfADeferredRead()
  {
    var opened = ACase();

    opened.ReadDeferred(DataSubjectRight.Access, Journal, Asked.AddHours(17), Asked);

    await using (var writing = postgres.NewDbContext())
    {
      writing.Cases.Add(opened);
      await writing.SaveChangesAsync();
    }

    await using var dbContext = postgres.NewDbContext();

    var reread = (await dbContext.Cases.SingleOrDefaultAsync(one => one.Id == opened.Id)).ShouldNotBeNull();
    var reading = reread.ReadingIn(DataSubjectRight.Access, Journal).ShouldNotBeNull();

    reading.LastOutcome.ShouldBe(AdapterOutcome.Deferred);
    reading.DeclaredDeadline.ShouldBe(Asked.AddHours(17));

    (await HeldForAsync(opened.Id)).ShouldBeEmpty();
  }

  private static RetrievedData APieceOf(
    CaseId opened,
    DataSubjectRight right,
    DeclaredSystemId declaredSystem,
    byte[] content)
  {
    return RetrievedData.Of(
      opened,
      right,
      declaredSystem,
      new RetrievedPiece(TransportEnvelope.Of("text/csv", "export.csv", declaredSystem), content),
      Asked);
  }

  private static Case ACase()
  {
    return Case.Open(
      CaseId.Next(),
      IdentityDeclaration.ApplicationSession,
      motivation: null,
      [Designation.Of(DesignationKind.Email, "jean.dupont@example.fr")],
      [DataSubjectRight.Access, DataSubjectRight.Portability],
      ClaimOrigin.Named,
      Manifest.Empty,
      ReceptionDate.Declared(Received));
  }

  /// <summary>Un dossier écrit en base, portant déjà le <b>fait</b> d'une lecture servie.</summary>
  private async Task<CaseId> ACaseAsync()
  {
    var opened = ACase();

    opened.ReadServed(DataSubjectRight.Access, Boutique, Asked);

    await using var dbContext = postgres.NewDbContext();

    dbContext.Cases.Add(opened);

    await dbContext.SaveChangesAsync();

    return opened.Id;
  }

  private async Task KeepAsync(RetrievedData piece)
  {
    await using var dbContext = postgres.NewDbContext();

    await new RetrievedDataStore(dbContext).KeepAsync(piece);
  }

  private async Task<IReadOnlyList<RetrievedData>> HeldForAsync(CaseId opened)
  {
    // Un contexte neuf : relire depuis celui qui a écrit ne prouverait que le suivi des
    // modifications, jamais l'aller-retour à travers les colonnes.
    await using var dbContext = postgres.NewDbContext();

    return await new RetrievedDataStore(dbContext).HeldForAsync(opened);
  }
}
