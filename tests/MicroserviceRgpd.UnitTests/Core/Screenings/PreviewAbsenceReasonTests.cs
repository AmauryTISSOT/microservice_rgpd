using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// Les quatre familles d'absence d'aperçu. <b>Quatre, et le nombre compte autant que la liste</b> :
/// une énumération qui grandirait au fil des pannes de production serait, à chaque panne neuve, une
/// valeur de plus que du code déjà écrit ne sait pas afficher.
/// </summary>
public class PreviewAbsenceReasonTests
{
  [Fact]
  public void ListsExactlyTheFourFamilies()
  {
    PreviewAbsenceReason.List.Select(reason => reason.Name).ShouldBe(
      ["UnsampleableType", "AccessDenied", "NoValueReturned", "ReadFailed"],
      ignoreOrder: true);
  }

  [Theory]
  [InlineData("UnsampleableType", "type non prélevable")]
  [InlineData("AccessDenied", "droits refusés")]
  [InlineData("NoValueReturned", "aucune valeur retournée")]
  [InlineData("ReadFailed", "lecture échouée")]
  public void CarriesTheFrenchLabelOfEachFamily(string name, string frenchLabel)
  {
    PreviewAbsenceReason.FromName(name).FrenchLabel.ShouldBe(frenchLabel);
  }

  /// <summary>
  /// <b>Aucune absence n'est muette</b> : chaque famille porte la phrase que l'<c>Operator</c> lit à
  /// la place de l'aperçu, et deux familles ne se lisent jamais pareil.
  /// </summary>
  [Fact]
  public void SaysWhatHappenedForEachFamilyAndSaysItDifferently()
  {
    PreviewAbsenceReason.List.ShouldAllBe(reason => reason.Statement.Length > 0);
    PreviewAbsenceReason.List.Select(reason => reason.Statement).Distinct().Count().ShouldBe(4);
  }

  /// <summary>
  /// ⚠️ <b>La phrase dit l'observation, jamais un constat sur la donnée.</b> La table réellement vide
  /// et la table pleine filtrée sous une politique de sécurité au niveau ligne sont
  /// <b>indiscernables du dehors</b> : elles reçoivent la même famille et la même phrase. Écrire
  /// « cette table ne contient aucune ligne » serait l'<c>Omission silencieuse</c> reconstituée dans
  /// le champ créé pour l'empêcher, sur la table <c>patients</c> d'un hôpital.
  /// </summary>
  [Fact]
  public void NeverClaimsAnythingAboutWhatTheColumnHolds()
  {
    PreviewAbsenceReason.NoValueReturned.Statement.ShouldBe(
      "La lecture de cette colonne n'a retourné aucune valeur.");

    var claimsAboutTheData = new[] { "ne contient", "est vide", "aucune donnée" };

    foreach (var reason in PreviewAbsenceReason.List)
    {
      claimsAboutTheData.ShouldAllBe(claim =>
        !reason.Statement.Contains(claim, StringComparison.OrdinalIgnoreCase));
    }
  }

  /// <summary>
  /// ⚠️ <b>La prose ne cite jamais le message du pilote.</b> C'est par là que remonteraient la valeur
  /// lue, l'hôte, l'utilisateur — la chaîne de connexion reconstituée par morceaux. Le garde le plus
  /// simple qui puisse le dire : la phrase est <b>attachée au membre</b>, donc close, et rien ne la
  /// compose à l'exécution.
  /// </summary>
  [Fact]
  public void CarriesAProseThatNothingComposesAtRunTime()
  {
    PreviewAbsenceReason.List.ShouldAllBe(reason =>
      !reason.Statement.Contains('{', StringComparison.Ordinal)
      && !reason.Statement.Contains('}', StringComparison.Ordinal));
  }

  /// <summary>
  /// « Droits refusés » vit à part parce que c'est <b>la seule que l'<c>Operator</c> puisse
  /// corriger</b> : elle l'envoie demander un accès, là où les trois autres ne lui font rien faire.
  /// </summary>
  [Fact]
  public void KeepsTheOneReasonTheOperatorCanActOnAsAFamilyOfItsOwn()
  {
    PreviewAbsenceReason.List
      .Where(reason => reason != PreviewAbsenceReason.AccessDenied)
      .ShouldAllBe(reason => reason.Statement != PreviewAbsenceReason.AccessDenied.Statement);

    PreviewAbsenceReason.AccessDenied.Statement.ShouldContain("droit de lire");
  }

  [Fact]
  public void IsSealedSoNoOneAddsAFamilyPerProductionIncident()
  {
    typeof(PreviewAbsenceReason).IsSealed.ShouldBeTrue();
  }
}
