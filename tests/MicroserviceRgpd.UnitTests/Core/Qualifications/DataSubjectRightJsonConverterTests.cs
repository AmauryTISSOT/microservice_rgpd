using System.Text.Json;
using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.UnitTests.Core.Qualifications;

/// <summary>
/// La taxonomie voyage sur le fil sous ses <b>noms canoniques anglais</b>, en <c>PascalCase</c> —
/// vers l'application tierce comme vers le sidecar. Un droit sur un fil JSON est un identifiant,
/// jamais un texte humain et jamais un entier : l'ordinal du <c>SmartEnum</c> ne franchit aucune
/// frontière.
/// </summary>
public class DataSubjectRightJsonConverterTests
{
  [Fact]
  public void WritesTheCanonicalNameNeverTheOrdinal()
  {
    JsonSerializer.Serialize(DataSubjectRight.Erasure).ShouldBe("\"Erasure\"");
  }

  [Fact]
  public void ReadsTheCanonicalName()
  {
    JsonSerializer.Deserialize<DataSubjectRight>("\"OutOfScope\"").ShouldBe(DataSubjectRight.OutOfScope);
  }

  [Theory]
  [MemberData(nameof(EveryRight))]
  public void RoundTripsEveryMemberOfTheTaxonomy(DataSubjectRight right)
  {
    var wire = JsonSerializer.Serialize(right);

    JsonSerializer.Deserialize<DataSubjectRight>(wire).ShouldBe(right);
  }

  /// <summary>
  /// Une valeur hors taxonomie n'est pas un droit inconnu à ranger quelque part : c'est une panne
  /// du moteur qui l'a émise, et elle se présente comme telle.
  /// </summary>
  [Theory]
  [InlineData("\"Deletion\"")]
  [InlineData("\"erasure\"")]
  [InlineData("\"hors-perimetre\"")]
  [InlineData("\"\"")]
  public void RefusesAnyNameTheTaxonomyDoesNotKnow(string wire)
  {
    Should.Throw<JsonException>(() => JsonSerializer.Deserialize<DataSubjectRight>(wire));
  }

  /// <summary>
  /// L'ordinal est un détail interne : le lire depuis le fil rouvrirait la porte que
  /// <see cref="WritesTheCanonicalNameNeverTheOrdinal"/> ferme.
  /// </summary>
  [Theory]
  [InlineData("2")]
  [InlineData("null")]
  [InlineData("[\"Erasure\"]")]
  public void RefusesAnythingThatIsNotAString(string wire)
  {
    Should.Throw<JsonException>(() => JsonSerializer.Deserialize<DataSubjectRight>(wire));
  }

  public static TheoryData<DataSubjectRight> EveryRight()
  {
    var rights = new TheoryData<DataSubjectRight>();

    foreach (var right in DataSubjectRight.List)
    {
      rights.Add(right);
    }

    return rights;
  }
}
