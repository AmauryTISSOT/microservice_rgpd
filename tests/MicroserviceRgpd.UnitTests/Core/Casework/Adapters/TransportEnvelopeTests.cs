using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;

namespace MicroserviceRgpd.UnitTests.Core.Casework.Adapters;

/// <summary>
/// Tout ce que le service sait d'une pièce : deux en-têtes recopiés, et un repli qui ne laisse
/// jamais une pièce sans nom.
/// </summary>
public class TransportEnvelopeTests
{
  private static readonly DeclaredSystemId Boutique = DeclaredSystemId.From("brocanto-boutique");

  /// <summary>
  /// Le <c>Content-Type</c> est <b>recopié</b>, paramètres compris. Le service ne le normalise pas :
  /// ce qu'il rendra à l'<c>Operator</c> est ce que l'application a déclaré servir.
  /// </summary>
  [Theory]
  [InlineData("text/csv")]
  [InlineData("application/json; charset=utf-8")]
  [InlineData("application/vnd.oasis.opendocument.spreadsheet")]
  public void CopiesTheContentTypeWithoutReadingIt(string declared)
  {
    TransportEnvelope.Of(declared, "export.csv", Boutique).ContentType.ShouldBe(declared);
  }

  /// <summary>
  /// <b>Un en-tête absent ne se devine pas.</b> Le service ne renifle pas les octets pour nommer un
  /// type que personne n'a déclaré : il dit « des octets », qui est exactement ce qu'il sait.
  /// </summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void NamesUndeclaredBytesForWhatTheyAre(string? absent)
  {
    TransportEnvelope.Of(absent, "export.csv", Boutique).ContentType
      .ShouldBe(TransportEnvelope.UnnamedContentType);
  }

  /// <summary>
  /// <b>Du nom, seul le dernier segment est gardé</b>, quel que soit le séparateur — celui de la
  /// machine du client, jamais celui de la machine qui exécute.
  /// </summary>
  [Theory]
  [InlineData("export.csv", "export.csv")]
  [InlineData("/var/exports/2026/export.csv", "export.csv")]
  [InlineData("../../etc/passwd", "passwd")]
  [InlineData("C:\\exports\\jean.csv", "jean.csv")]
  [InlineData("\"rapport mensuel.csv\"", "rapport mensuel.csv")]
  public void KeepsOnlyTheLastSegmentOfTheName(string declared, string expected)
  {
    TransportEnvelope.Of("text/csv", declared, Boutique).FileName.ShouldBe(expected);
  }

  /// <summary>
  /// <b>À défaut d'en-tête, le nom dégrade sur le <c>system_id</c>.</b> Un intégrateur qui n'écrit
  /// aucun <c>Content-Disposition</c> n'a rien fait de mal, et « fichier sans nom » n'apprendrait
  /// rien à l'humain qui recevra la pièce.
  /// </summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("\"\"")]
  [InlineData("/var/exports/")]
  public void FallsBackOnTheSystemWhenTheHeaderNamesNothing(string? absent)
  {
    TransportEnvelope.Of("text/csv", absent, Boutique).FileName.ShouldBe(Boutique.Value);
  }

  /// <summary>
  /// Un nom démesuré est <b>coupé</b>, jamais refusé : une pièce n'a pas à être perdue parce que
  /// son nom était long, et la borne est celle d'une colonne, pas d'un jugement.
  /// </summary>
  [Fact]
  public void CutsAnOutsizedNameRatherThanLosingThePiece()
  {
    var enormous = new string('e', TransportEnvelope.MaxFileNameLength + 40) + ".csv";

    TransportEnvelope.Of("text/csv", enormous, Boutique).FileName
      .Length.ShouldBe(TransportEnvelope.MaxFileNameLength);
  }

  /// <summary>
  /// <b>Rien n'est refusé ici.</b> Une enveloppe entièrement absente reste une enveloppe : c'est un
  /// <c>Adapter</c> qui a servi sans se soucier de nommer, et non une panne.
  /// </summary>
  [Fact]
  public void NeverRefusesAnEnvelopeThatDeclaresNothing()
  {
    var envelope = TransportEnvelope.Of(null, null, Boutique);

    envelope.ContentType.ShouldBe(TransportEnvelope.UnnamedContentType);
    envelope.FileName.ShouldBe(Boutique.Value);
  }
}
