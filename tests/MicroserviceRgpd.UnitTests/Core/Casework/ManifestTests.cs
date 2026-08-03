using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UnitTests.Core.Casework;

/// <summary>
/// La vue qu'on prend du catalogue à un instant. Elle range et elle date ; elle ne compte pas, et
/// elle ne se présente jamais comme complète.
/// </summary>
public class ManifestTests
{
  private static readonly DateTimeOffset Spring = new(2026, 3, 12, 8, 0, 0, TimeSpan.Zero);

  /// <summary>
  /// Un catalogue vide est l'état d'un service qu'on vient d'installer — et il ne se raconte pas
  /// comme un catalogue complet à zéro système.
  /// </summary>
  [Fact]
  public void HoldsNothingAndNoDateWhenNothingHasBeenDeclaredYet()
  {
    Manifest.Empty.Systems.ShouldBeEmpty();
    Manifest.Empty.OldestDeclaration.ShouldBeNull();
  }

  [Fact]
  public void RangesTheSystemsUnderTheLabelTheOperatorSearchesBy()
  {
    var manifest = Manifest.Of([ASystem("photos", "Les photos des annonces"), ASystem("boutique", "La base de la boutique")]);

    manifest.Systems.Select(system => system.Id.Value).ShouldBe(["boutique", "photos"]);
  }

  /// <summary>
  /// La fraîcheur d'un catalogue est celle de sa ligne <b>la plus vieille</b>, jamais celle de la
  /// dernière touchée : c'est cette date-là qu'on nomme en disant de quand date ce recensement.
  /// </summary>
  [Fact]
  public void DatesTheManifestByItsOldestDeclarationRatherThanItsFreshest()
  {
    var manifest = Manifest.Of(
    [
      ASystem("boutique", "La base de la boutique", Spring.AddMonths(4)),
      ASystem("compta", "La comptabilité scellée", Spring),
      ASystem("photos", "Les photos des annonces", Spring.AddMonths(2)),
    ]);

    manifest.OldestDeclaration.ShouldBe(Spring);
  }

  private static DeclaredSystem ASystem(string id, string label, DateTimeOffset? declaredOn = null)
  {
    return DeclaredSystem.Declare(
      DeclaredSystemId.From(id),
      SystemLabel.From(label),
      SystemContents.From("Ce que ce système contient, dans les mots de qui l'a déclaré."),
      [],
      adapterAddress: null,
      declaredOn ?? Spring);
  }
}
