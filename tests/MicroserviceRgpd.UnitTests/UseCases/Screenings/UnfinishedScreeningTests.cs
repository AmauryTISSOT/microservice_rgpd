using MicroserviceRgpd.UseCases.Screenings;

namespace MicroserviceRgpd.UnitTests.UseCases.Screenings;

/// <summary>
/// Ce que le verrou <b>dit</b>, et surtout ce qu'il refuse de dire.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le verrou n'est pas seulement un booléen : c'est une phrase que l'<c>Operator</c> lit.</b>
/// Un verrou juste sous une phrase fausse ne protège de rien — c'est la phrase qui est lue, pas le
/// booléen. Ces tests portent donc sur les mots.
/// </para>
/// </remarks>
public class UnfinishedScreeningTests
{
  /// <summary>Tant qu'il reste une colonne où rien n'a été vu à relire, le verrou est posé et le dit.</summary>
  [Fact]
  public void LocksTheScreeningWhileAColumnWhereNothingWasSeenHasNotBeenReRead()
  {
    var unfinished = new UnfinishedScreening(UnreadUnflagged: 12, Awaiting: 12);

    unfinished.IsUnfinished.ShouldBeTrue();
    unfinished.Statement.ShouldStartWith("Ce dépistage est inachevé");
    unfinished.Statement.ShouldContain("12 colonnes");
  }

  /// <summary>
  /// <b>Le mode de panne que ce test existe pour tenir.</b> Un relevé dont <em>aucune</em> colonne
  /// n'est non signalée a zéro colonne à relire dès sa naissance : le verrou tombe immédiatement.
  /// La phrase d'achèvement se serait alors affichée sur un rapport où personne n'avait rien
  /// tranché — juste au-dessus d'un compte « En attente » non nul, dans la même page.
  /// </summary>
  [Fact]
  public void RefusesToCallAScreeningReadWhenColumnsAreStillAwaitingAnArbitration()
  {
    var nothingUnflagged = new UnfinishedScreening(UnreadUnflagged: 0, Awaiting: 7);

    nothingUnflagged.Statement
      .Contains("Toutes les colonnes de ce dépistage ont été relues", StringComparison.Ordinal)
      .ShouldBeFalse(
        "Le rapport se déclarerait fini alors que sept colonnes attendent encore un arbitrage.");

    nothingUnflagged.Statement.ShouldContain("7 colonnes signalées");
    nothingUnflagged.Statement.ShouldContain("attendent encore");
  }

  /// <summary>Rien à relire, rien en attente : là seulement, le rapport peut se dire relu en entier.</summary>
  [Fact]
  public void CallsTheScreeningReadOnlyOnceNothingAwaitsAnyone()
  {
    var read = new UnfinishedScreening(UnreadUnflagged: 0, Awaiting: 0);

    read.IsUnfinished.ShouldBeFalse();
    read.Statement.ShouldBe(
      "Toutes les colonnes de ce dépistage ont été relues, y compris celles où rien n'a été vu.");
  }

  /// <summary>
  /// Une surface dont tout le propos est que l'humain lise le <b>nombre</b> plutôt qu'un mot d'état
  /// ne peut pas lui écrire « 1 colonnes … n'ont pas ».
  /// </summary>
  [Fact]
  public void AgreesTheCountWithWhatItCounts()
  {
    new UnfinishedScreening(UnreadUnflagged: 1, Awaiting: 1).Statement
      .ShouldContain("1 colonne où rien n'a été vu n'a pas encore été relue.");

    new UnfinishedScreening(UnreadUnflagged: 0, Awaiting: 1).Statement
      .ShouldContain("1 colonne signalée attend encore d'être tranchée.");

    new UnfinishedScreening(UnreadUnflagged: 2, Awaiting: 2).Statement
      .ShouldContain("2 colonnes où rien n'a été vu n'ont pas encore été relues.");
  }
}
