using System.Reflection;

using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// <b>La clause est structurelle, ou elle n'est qu'une intention.</b> C'est l'unique argument qui a
/// fait préférer une clause structurée à un paragraphe de prose : si le type de réponse se construit
/// sans elle, cet argument tombe rétroactivement et il faut revenir à la prose.
/// </summary>
public class ScreeningAnswerTests
{
  [Fact]
  public void CarriesTheContentAndTheClauseSideBySide()
  {
    var screening = AScreening.Of(AScreening.AFlaggedColumn());
    var answer = new ScreeningAnswer<Screening>(screening, IncompletenessClause.For(screening));

    answer.Content.ShouldBeSameAs(screening);
    answer.Clause.ShouldNotBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Un seul constructeur, et il exige la clause.</b> Aucun constructeur sans paramètre, donc
  /// aucun chemin qui produise une réponse muette sur ce qu'elle n'a pas regardé.
  /// </summary>
  [Fact]
  public void OffersNoConstructorThatOmitsTheClause()
  {
    var constructors = typeof(ScreeningAnswer<Screening>)
      .GetConstructors(BindingFlags.Public | BindingFlags.Instance);

    constructors.Length.ShouldBe(1);
    constructors.ShouldAllBe(constructor =>
      constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(IncompletenessClause)));
  }

  /// <summary>Et le seul qui existe refuse une clause absente plutôt que de la laisser nulle.</summary>
  [Fact]
  public void RefusesToBeBuiltWithNoClauseAtAll()
  {
    var screening = AScreening.Of(AScreening.AFlaggedColumn());

    Should.Throw<ArgumentNullException>(() => new ScreeningAnswer<Screening>(screening, null!));
  }

  /// <summary>
  /// Scellé, et une classe plutôt qu'un <c>record</c> : aucun héritier n'ouvre un second
  /// constructeur, et aucun <c>with</c> ne détache la clause après coup.
  /// </summary>
  [Fact]
  public void IsSealedAndOffersNoCopyThatCouldDropTheClause()
  {
    typeof(ScreeningAnswer<>).IsSealed.ShouldBeTrue();

    typeof(ScreeningAnswer<Screening>)
      .GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
      .ShouldBeNull();
  }

  /// <summary>Une lecture qui rend une seule ligne porte la clause au même titre qu'une lecture qui rend le rapport entier.</summary>
  [Fact]
  public void CarriesTheClauseOnASingleColumnJustAsOnAWholeReport()
  {
    var screening = AScreening.Of(AScreening.AFlaggedColumn());
    var column = screening.Columns[0];

    var answer = new ScreeningAnswer<ScreenedColumn>(column, IncompletenessClause.For(screening));

    answer.Content.ShouldBeSameAs(column);
    answer.Clause.BeyondReach.Categories.Count.ShouldBe(3);
  }
}
