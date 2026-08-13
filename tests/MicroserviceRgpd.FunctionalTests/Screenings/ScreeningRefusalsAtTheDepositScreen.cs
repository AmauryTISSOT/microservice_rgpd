using System.Globalization;
using System.Text;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.UseCases.Screenings.DepositListing;
using MicroserviceRgpd.Web.Pages.Screenings;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// Le <b>refus lisible</b> : chacun des cas de refus, éprouvé là où l'<c>Operator</c> le rencontre —
/// l'écran de dépôt —, et rendant la phrase française qui dit <b>lequel</b> s'applique.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Un cas nommé, jamais un « format invalide ».</b> C'est la propriété que ce fichier existe
/// pour tenir. L'<c>Operator</c> a fait un long trajet pour produire ce relevé — ouvrir un client
/// SQL chez le client, exécuter la requête, rapatrier la sortie — et rien dans « format invalide »
/// ne lui dit s'il doit rejouer sa requête, changer de schéma ou remonter son plafond. Un refus
/// générique le fait recommencer <b>au hasard</b>, sur un collage de plusieurs mégaoctets qu'il ne
/// relira pas à l'œil.
/// </para>
/// <para>
/// ⚠️ <b>Les cas sont énumérés depuis <see cref="RefusalCause.List"/>, jamais recopiés ici.</b> Une
/// liste tenue à la main dans le test aurait laissé un dixième cas naître sans que rien n'exige sa
/// phrase à l'écran — et c'est très exactement le cas non couvert qui rendrait un jour « format
/// invalide » à un <c>Operator</c>.
/// </para>
/// <para>
/// ⚠️ <b>Aucun refus ne laisse de trace</b>, et c'est éprouvé en base :
/// voir <see cref="LeavesNeitherAReportNorASingleColumnBehindWhenItRefuses"/>. Une ingestion
/// partielle rendrait un rapport bâti sur une part du relevé, qui <b>se lirait comme complet</b>.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ScreeningRefusalsAtTheDepositScreen(CustomWebApplicationFactory<Program> factory)
{
  private readonly ScreeningSurface _surface = new(factory);

  /// <summary>Les cas de refus, tels que le domaine les énumère — et ils sont neuf.</summary>
  public static TheoryData<string> TheRefusalCases => [.. RefusalCause.List.Select(cause => cause.Name)];

  /// <summary>
  /// <b>Chacun des neuf cas rend son message français, et le message désigne son cas.</b>
  /// </summary>
  /// <remarks>
  /// <para>
  /// Le message porte quatre choses, et aucune n'est décorative : <b>le cas nommé</b> et son numéro,
  /// pour que deux <c>Operator</c> parlent du même refus ; <b>la ligne</b> du collage, pour qu'il la
  /// retrouve dans ce qu'il a sous les yeux ; <b>ce qui était attendu</b>, sans quoi il recommence au
  /// hasard ; et <b>que rien n'a été ingéré</b>, sans quoi il croira devoir nettoyer avant de
  /// recoller.
  /// </para>
  /// <para>
  /// ⚠️ <b>Le refus se rend sur l'écran de dépôt, jamais en erreur nue.</b> Une exception remontée à
  /// la surface aurait rendu une page d'erreur générique — c'est-à-dire « format invalide » sous une
  /// autre forme, et le collage perdu avec.
  /// </para>
  /// </remarks>
  [Theory]
  [MemberData(nameof(TheRefusalCases))]
  public async Task NamesWhichOfTheRefusalCasesAppliesRatherThanSayingTheFormatIsInvalid(string caseName)
  {
    var cause = RefusalCause.FromName(caseName);

    var rendered = await _surface.DepositAndReadTheRefusalAsync(PasteRefusedFor(cause));

    // Le refus se dit EN BLOC : c'est ce qui apprend à l'Operator qu'il n'y a rien à nettoyer.
    rendered.ShouldContain("refusé en bloc", Case.Insensitive);

    // LE CAS SE NOMME, et ne se fond pas dans un « format invalide ».
    rendered.ShouldContain(cause.FrenchLabel);
    rendered.ShouldContain($"cas {cause.CaseNumber}");

    // Ce qui était attendu — sans quoi il recommence au hasard.
    rendered.ShouldContain(cause.Expectation);

    // Sans cette phrase, l'Operator croit devoir défaire quelque chose avant de recoller.
    rendered.ShouldContain("Aucune colonne n'a été ingérée");
  }

  /// <summary>
  /// <b>Le dépassement du plafond de colonnes est refusé, jamais tronqué</b> — et le message dit le
  /// plafond.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Tronquer en silence à vingt mille colonnes est l'<c>Omission silencieuse</c> sous sa forme
  /// la plus pure</b> : le rapport se lirait comme entier, et les colonnes coupées seraient
  /// précisément celles que personne ne relirait jamais. La limite doit <b>se voir au lieu de se
  /// subir</b>, ce qui veut dire : porter son chiffre dans la phrase.
  /// <para>
  /// Le plafond est lu sur le compte <b>annoncé</b> par la ligne de fin, avant qu'une seule ligne de
  /// colonne ne soit analysée — c'est ce qui permet de répondre à un relevé de trente mille colonnes
  /// sans en avoir payé l'analyse.
  /// </para>
  /// </remarks>
  [Fact]
  public async Task RefusesAListingBeyondTheColumnCeilingAndSaysWhatTheCeilingIs()
  {
    var rendered = await _surface.DepositAndReadTheRefusalAsync(ScreeningSurface.Lines(
      ScreeningSurface.Header(),
      ScreeningSurface.Column("email"),
      ScreeningSurface.ClosingLine(ColumnListing.MaxColumns + 1)));

    rendered.ShouldContain(RefusalCause.CeilingExceeded.FrenchLabel);
    rendered.ShouldContain($"cas {RefusalCause.CeilingExceeded.CaseNumber}");

    // Le chiffre du plafond, et celui que le relevé annonçait : la limite se voit.
    rendered.ShouldContain(ColumnListing.MaxColumns.ToString(CultureInfo.InvariantCulture));
    rendered.ShouldContain((ColumnListing.MaxColumns + 1).ToString(CultureInfo.InvariantCulture));

    // REFUSÉ, et non coupé : c'est toute la différence, et elle se dit en toutes lettres.
    rendered.ShouldContain("refusé plutôt que tronqué");
  }

  /// <summary>
  /// <b>Le dépassement du plafond d'octets rend un message lisible</b>, et non un refus muet du
  /// transport.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>C'est la condition d'atteignabilité de tous les autres refus.</b> Un plafond de transport
  /// posé au niveau du plafond du geste rendrait un <c>413</c> nu — sans phrase, sans cas, sans
  /// l'écran de dépôt — et le refus lisible serait <b>inatteignable derrière lui</b>. Le plafond du
  /// transport doit donc être posé <b>au-dessus</b> de celui du geste, pour que le refus qui sort
  /// soit toujours celui qui parle français.
  /// </para>
  /// <para>
  /// Le collage éprouvé ici est <b>sincère</b> : un relevé d'une seule colonne dont le commentaire de
  /// table est démesuré. Rien dans son format ne cloche — s'il passait le plafond, il serait ingéré.
  /// C'est bien le poids, et lui seul, qui le refuse.
  /// </para>
  /// </remarks>
  [Fact]
  public async Task RefusesAPasteBeyondTheByteCeilingWithAReadableMessageRatherThanAMuteRejection()
  {
    var oversized = ScreeningSurface.Paste(ScreeningSurface.Column(
      "email",
      tableComment: new string('a', (int)DepositListingCommand.MaxPasteBytes)));

    // Le collage de ce test doit bel et bien franchir le plafond d'octets.
    ((long)Encoding.UTF8.GetByteCount(oversized))
      .ShouldBeGreaterThan(DepositListingCommand.MaxPasteBytes);

    var rendered = await _surface.DepositAndReadTheRefusalAsync(oversized);

    rendered.ShouldContain("refusé en bloc", Case.Insensitive);
    rendered.ShouldContain("trop volumineux");

    // Le plafond en mégaoctets, tel que la phrase le dit à l'Operator.
    rendered.ShouldContain("8 Mo au plus");

    // ⚠️ Le poids reçu ne se lit JAMAIS comme le plafond. « 8 Mo reçus, 8 Mo au plus » est une
    // phrase qui refuse en donnant deux fois le même chiffre : l'Operator y lit un service qui se
    // trompe, et non un collage à alléger.
    rendered.ShouldNotContain("8 Mo reçus");
    rendered.ShouldContain("reçus");

    // Un collage trop lourd n'est pas à moitié lu : il est refusé en bloc comme les neuf cas.
    rendered.ShouldContain("Aucune colonne n'a été ingérée");
  }

  /// <summary>
  /// <b>Le plafond muet est posé au-dessus du plafond qui parle</b>, et jamais au même octet.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>C'est le seul garde de cette propriété, et il est nécessaire.</b> Le <c>TestServer</c> de
  /// la factory n'applique pas <c>RequestSizeLimit</c> — il n'y a pas de Kestrel derrière lui —, si
  /// bien qu'<b>aucun test fonctionnel ne peut voir le <c>413</c></b> :
  /// <see cref="RefusesAPasteBeyondTheByteCeilingWithAReadableMessageRatherThanAMuteRejection"/>
  /// resterait vert sur un service qui, en production, refuserait muettement le même collage. La
  /// propriété s'épingle donc là où elle se décide, sur les deux constantes.
  /// </para>
  /// <para>
  /// L'écart exigé n'est pas symbolique : le collage arrive <b>encodé en formulaire</b>, où les
  /// accolades, guillemets et deux-points du pivot pèsent trois octets chacun — mesuré à ~1,45× sur
  /// une ligne réelle. Un plafond de transport qui ne couvrirait pas cette enflure refuserait
  /// muettement des collages <b>en deçà</b> du plafond annoncé.
  /// </para>
  /// </remarks>
  [Fact]
  public void KeepsTheMuteTransportCeilingAboveTheOneThatRefusesInFrench()
  {
    DepositModel.TransportCeilingInBytes.ShouldBeGreaterThan(
      DepositListingCommand.MaxPasteBytes * 3 / 2,
      "Le transport doit laisser entrer un collage encodé en formulaire — ~1,45× le collage — au-delà "
      + "du plafond du geste, sans quoi le refus lisible est inatteignable derrière un 413 muet.");
  }

  /// <summary>
  /// <b>Aucun refus ne laisse de trace en base</b> : ni rapport, ni la moindre colonne.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>C'est ce qui donne son sens au mot « en bloc ».</b> Un dépôt qui écrirait les lignes
  /// lisibles avant de buter sur la dixième laisserait un <c>Screening</c> amputé que rien ne
  /// signalerait comme tel — et l'<c>Omission relue</c> repose entièrement sur le fait que le rapport
  /// rende <b>toutes</b> les colonnes du relevé.
  /// </para>
  /// <para>
  /// Les comptes sont pris <b>avant et après</b>, et non comparés à zéro : la collection est
  /// partagée, et d'autres tests ont légitimement déposé leurs rapports. Ce qu'on exige est qu'aucun
  /// des refus n'ait rien ajouté.
  /// </para>
  /// </remarks>
  [Fact]
  public async Task LeavesNeitherAReportNorASingleColumnBehindWhenItRefuses()
  {
    var (reportsBefore, columnsBefore) = await CountedAsync();

    foreach (var cause in RefusalCause.List)
    {
      await _surface.DepositAndReadTheRefusalAsync(PasteRefusedFor(cause));
    }

    var (reportsAfter, columnsAfter) = await CountedAsync();

    reportsAfter.ShouldBe(
      reportsBefore,
      "Un collage refusé ne crée aucun Screening : il n'y a pas d'ingestion partielle.");

    columnsAfter.ShouldBe(
      columnsBefore,
      "Un collage refusé n'écrit pas une seule ScreenedColumn, pas même celles de ses lignes lisibles.");
  }

  /// <summary>
  /// Un collage que le cas donné refuse, et lui seul.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le <c>switch</c> est exhaustif et lève sur l'inconnu.</b> C'est ce qui fait qu'un dixième
  /// cas ajouté au domaine casse ce fichier plutôt que de s'y glisser sans phrase à l'écran.
  /// </remarks>
  private static string PasteRefusedFor(RefusalCause cause)
  {
    // Cas n° 1 — rien ne déclare le format : la première ligne n'est même pas un objet.
    if (cause == RefusalCause.MissingHeader)
    {
      return ScreeningSurface.Lines(
        "-- Relevé de colonnes, généré le 10/08/2026",
        ScreeningSurface.Column("email"),
        ScreeningSurface.ClosingLine(1));
    }

    // Cas n° 2 — la troncature au presse-papier : la ligne de fin n'a pas survécu au collage.
    if (cause == RefusalCause.MissingClosingLine)
    {
      return ScreeningSurface.Lines(
        ScreeningSurface.Header(),
        ScreeningSurface.Column("id_adh", position: 1),
        ScreeningSurface.Column("email", position: 2));
    }

    // Cas n° 3 — la ligne de fin en annonce quatre, deux seulement sont arrivées.
    if (cause == RefusalCause.CountMismatch)
    {
      return ScreeningSurface.Paste(
        declaredColumnCount: 4,
        ScreeningSurface.Column("id_adh", position: 1),
        ScreeningSurface.Column("email", position: 2));
    }

    // Cas n° 4 — une ligne à qui il manque six des neuf clés : elle ne vient pas de la requête.
    if (cause == RefusalCause.UnreadableColumnLine)
    {
      return ScreeningSurface.Lines(
        ScreeningSurface.Header(),
        """{"schema":"public","table":"adherents","colonne":"email"}""",
        ScreeningSurface.ClosingLine(1));
    }

    // Cas n° 5 — le trou au milieu : deux morceaux collés qui se recouvrent mal.
    if (cause == RefusalCause.RankGap)
    {
      return ScreeningSurface.Paste(
        ScreeningSurface.Column("id_adh", position: 1),
        ScreeningSurface.Column("email", position: 3));
    }

    // Cas n° 6 — deux pivots collés bout à bout : le même triplet deux fois.
    if (cause == RefusalCause.DuplicateColumn)
    {
      return ScreeningSurface.Paste(
        ScreeningSurface.Column("email", position: 1),
        ScreeningSurface.Column("email", position: 1));
    }

    // Cas n° 7 — le pivot vide et parfaitement formé : ce que rend une requête lancée contre le
    // mauvais schéma. L'accepter produirait un Screening vide et rassurant.
    if (cause == RefusalCause.EmptyListing)
    {
      return ScreeningSurface.Lines(
        ScreeningSurface.Header(),
        ScreeningSurface.ClosingLine(0));
    }

    // Cas n° 8 — un relevé produit par une requête d'une autre version que celle que l'écran rend.
    if (cause == RefusalCause.UnknownFormatVersion)
    {
      return ScreeningSurface.Lines(
        ScreeningSurface.Header(format: "screening-pivot/0"),
        ScreeningSurface.Column("email"),
        ScreeningSurface.ClosingLine(1));
    }

    // Cas n° 9 — le plafond, lu sur le compte annoncé avant toute analyse.
    if (cause == RefusalCause.CeilingExceeded)
    {
      return ScreeningSurface.Lines(
        ScreeningSurface.Header(),
        ScreeningSurface.Column("email"),
        ScreeningSurface.ClosingLine(ColumnListing.MaxColumns + 1));
    }

    throw new NotSupportedException(
      $"Le cas de refus « {cause.Name} » n'a aucun collage qui l'exerce à l'écran de dépôt. Un cas "
      + "que rien n'éprouve à la surface est un cas qui rendra un jour « format invalide » à un "
      + "Operator : écrivez son collage ici.");
  }

  /// <summary>Ce que la base porte, des deux tables du contexte.</summary>
  private async Task<(int Reports, int Columns)> CountedAsync()
  {
    using var scope = factory.Services.CreateScope();

    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    return (
      await database.Screenings.CountAsync(),
      await database.ScreenedColumns.CountAsync());
  }
}
