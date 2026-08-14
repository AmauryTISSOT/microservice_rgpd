using System.Reflection;

using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// La clause est structurée <b>pour être testable</b> : en prose, l'une de ses deux dimensions —
/// les sources et les catégories — disparaîtrait dans une réécriture sans que rien ne casse. Ce
/// fichier est la contrepartie de ce choix ; sans lui, la forme structurée n'a plus de raison
/// d'avoir été préférée au paragraphe.
/// </summary>
public class IncompletenessClauseTests
{
  /// <summary>Les quatre parties sont là, et aucune n'est vide.</summary>
  [Fact]
  public void CarriesItsFourPartsAndLeavesNoneOfThemEmpty()
  {
    var clause = IncompletenessClause.For(AScreening.Of(AScreening.AFlaggedColumn()));

    clause.Perimeter.ReadAsSignal.ShouldNotBeEmpty();
    clause.Perimeter.ReadOnlyToFilter.ShouldNotBeEmpty();
    clause.Beyond.Examples.ShouldNotBeEmpty();
    clause.BeyondReach.Categories.ShouldNotBeEmpty();
    clause.RelationToManifest.ShouldNotBeNullOrWhiteSpace();
  }

  /// <summary>
  /// ⚠️ <b>Les deux régimes de clôture, et l'inversion est le résultat central.</b> L'ensemble de ce
  /// qu'on n'a pas regardé est infini et toute fermeture y ment ; l'ensemble de ce qui a été lu
  /// compte exactement un élément. La liste fermée est donc celle du périmètre lu, jamais celle du
  /// hors périmètre — l'inverse ferait des exemples un référentiel qu'un <c>Operator</c> croit avoir
  /// coché jusqu'au bout.
  /// </summary>
  [Fact]
  public void ClosesTheReadPerimeterAndDeclaresTheRestOpen()
  {
    var clause = IncompletenessClause.For(AScreening.Of(AScreening.AFlaggedColumn()));

    clause.Perimeter.IsClosed.ShouldBeTrue();
    clause.Beyond.IsClosed.ShouldBeFalse();
    clause.Beyond.OpennessStatement.ShouldNotBeNullOrWhiteSpace();
  }

  /// <summary>
  /// <b>Le périmètre lu sépare le signal du filtre.</b> Écrire « j'ai lu les types » ferait croire
  /// qu'un <c>varchar(10)</c> et un <c>date</c> sont deux indices de qualité différente, alors qu'ils
  /// ne sont un indice ni l'un ni l'autre.
  /// </summary>
  [Fact]
  public void SeparatesWhatWasReadAsASignalFromWhatWasReadOnlyToFilter()
  {
    var perimeter = IncompletenessClause.For(AScreening.Of(AScreening.AFlaggedColumn())).Perimeter;

    perimeter.ReadAsSignal.ShouldNotContain(entry => entry.Contains("type", StringComparison.OrdinalIgnoreCase));
    perimeter.ReadOnlyToFilter.ShouldContain(entry => entry.Contains("type", StringComparison.OrdinalIgnoreCase));
    perimeter.FilterStatement.ShouldNotBeNullOrWhiteSpace();
  }

  /// <summary>
  /// ⚠️ <b>Les commentaires portent leur condition.</b> Plusieurs SGBD n'en rendent jamais, et sans
  /// elle la clause serait fausse sur la majorité des relevés réels.
  /// </summary>
  [Fact]
  public void CarriesTheConditionAttachedToEveryMentionOfAComment()
  {
    var perimeter = IncompletenessClause.For(AScreening.Of(AScreening.AFlaggedColumn())).Perimeter;

    perimeter.ReadAsSignal
      .Where(entry => entry.Contains("commentaire", StringComparison.OrdinalIgnoreCase))
      .ShouldAllBe(entry => entry.Contains(ReadPerimeter.CommentCondition, StringComparison.Ordinal));
  }

  /// <summary>
  /// ⚠️ <b>Les catégories hors de portée sont énumérées nommément, pas énoncées en principe.</b> Le
  /// générique — « certaines catégories ne sont pas atteignables » — est la phrase qu'on survole, et
  /// elle perd ce qui coûte. C'est le seul endroit du produit où se dit la seconde moitié de ce que
  /// la taxonomie affirme.
  /// </summary>
  [Fact]
  public void NamesTheThreeCategoriesThisRegimeCannotReachAndSaysWhyForEach()
  {
    var beyondReach = IncompletenessClause.For(AScreening.Of(AScreening.AFlaggedColumn())).BeyondReach;

    beyondReach.Categories.Select(entry => entry.Category).ShouldBe(
      [
        PersonalDataCategory.HealthData,
        PersonalDataCategory.SpecialCategoryData,
        PersonalDataCategory.CriminalOffenceData,
      ],
      ignoreOrder: true);

    beyondReach.Categories.ShouldAllBe(entry => entry.Reason.Length > 0);
  }

  /// <summary>
  /// Les trois hors de portée sont exactement les trois valeurs fermées par le texte : ce sont celles
  /// que la taxonomie garde au titre de ce qu'elles <i>sont</i>, et dont il faut donc dire qu'on ne
  /// sait pas les voir.
  /// </summary>
  [Fact]
  public void ReachesBeyondExactlyTheValuesTheTaxonomyKeepsOnStatutoryGrounds()
  {
    var beyondReach = IncompletenessClause.For(AScreening.Of(AScreening.AFlaggedColumn())).BeyondReach;

    beyondReach.Categories.Select(entry => entry.Category).ShouldBe(
      PersonalDataCategory.List.Where(category => category.IsClosedByStatute),
      ignoreOrder: true);
  }

  /// <summary>
  /// ⚠️ <b>La clause dit une limite de méthode, jamais un résultat de corpus.</b> Que des
  /// applications libres ne portent aucune colonne d'art. 10 n'autorise pas à écrire qu'une base
  /// client n'en porte pas.
  /// </summary>
  [Fact]
  public void SpeaksOfTheMethodAndNeverOfWhatSomeCorpusHappenedToContain()
  {
    var beyondReach = IncompletenessClause.For(AScreening.Of(AScreening.AFlaggedColumn())).BeyondReach;

    var everything = string.Join(" ", beyondReach.Categories.Select(entry => entry.Reason).Append(beyondReach.Statement));

    everything.ShouldNotContain("corpus", Case.Insensitive);
    everything.ShouldNotContain("jamais rencontré", Case.Insensitive);
    everything.ShouldNotContain("aucune base", Case.Insensitive);
  }

  /// <summary>
  /// La relation au <c>Manifest</c> vit <b>dans la clause</b>, et pas seulement au glossaire : la
  /// borne <c>Aucune modification vers le Manifest</c> n'empêche que le pont technique, et rien en
  /// elle n'empêche un <c>Operator</c> pressé de lire le rapport comme son paysage.
  /// </summary>
  [Fact]
  public void SaysInTheAnswerItselfThatTheListingIsNotTheManifest()
  {
    var clause = IncompletenessClause.For(AScreening.Of(AScreening.AFlaggedColumn()));

    clause.RelationToManifest.ShouldContain("Manifest", Case.Sensitive);
    clause.RelationToManifest.ShouldContain("à la main", Case.Insensitive);
  }

  /// <summary>
  /// ⚠️ <b>Le mot « complet » et ses cousins sont radioactifs</b> : la clause existe pour refuser
  /// cette promesse, elle ne peut pas la porter dans son propre texte.
  /// </summary>
  [Fact]
  public void NeverUsesTheWordThisWholeClauseExistsToRefuse()
  {
    var clause = IncompletenessClause.For(AScreening.Of(AScreening.AFlaggedColumn()));

    var everything = string.Join(
      " ",
      [
        clause.Perimeter.Statement,
        clause.Perimeter.FilterStatement,
        .. clause.Perimeter.ReadAsSignal,
        .. clause.Perimeter.ReadOnlyToFilter,
        clause.Beyond.Statement,
        clause.Beyond.OpennessStatement,
        .. clause.Beyond.Examples,
        clause.BeyondReach.Statement,
        .. clause.BeyondReach.Categories.Select(entry => entry.Reason),
        clause.RelationToManifest,
      ]);

    everything.ShouldNotContain("complet", Case.Insensitive);
    everything.ShouldNotContain("exhaustif", Case.Insensitive);
    everything.ShouldNotContain("cartographie", Case.Insensitive);
  }

  /// <summary>
  /// <b>Le texte est constant ; seuls les comptes sont calculés.</b> Deux rapports très différents
  /// portent mot pour mot la même clause, et ne diffèrent que par ce que le service peut compter de
  /// ce qu'il a bel et bien lu.
  /// </summary>
  [Fact]
  public void KeepsTheVerySameWordsFromOneReportToAnother()
  {
    var lean = IncompletenessClause.For(AScreening.Of(AScreening.AFlaggedColumn()));
    var fuller = IncompletenessClause.For(AScreening.Of(
      AScreening.AFlaggedColumn("adr_l1", position: 1),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 2))));

    fuller.Perimeter.ReadAsSignal.ShouldBe(lean.Perimeter.ReadAsSignal);
    fuller.Perimeter.Statement.ShouldBe(lean.Perimeter.Statement);
    fuller.Beyond.ShouldBe(lean.Beyond);
    fuller.BeyondReach.ShouldBe(lean.BeyondReach);
    fuller.RelationToManifest.ShouldBe(lean.RelationToManifest);
  }

  /// <summary>Les comptes, eux, sont ceux de <b>ce</b> relevé.</summary>
  [Fact]
  public void CountsWhatThisVeryListingLetItRead()
  {
    var screening = AScreening.Of(
      AScreening.AFlaggedColumn("adr_l1", position: 1),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 2)),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn(
        "montant",
        table: "cotisations",
        position: 1,
        columnComment: "montant réglé, en centimes")));

    var perimeter = IncompletenessClause.For(screening).Perimeter;

    perimeter.ColumnsRead.ShouldBe(3);
    perimeter.TablesRead.ShouldBe(2);
    perimeter.ColumnsWithoutAComment.ShouldBe(2);
  }

  /// <summary>
  /// ⚠️ <b>Aucun cas spécial au seuil zéro, et c'est un refus argumenté.</b> Un énoncé particulier
  /// quand rien n'est signalé dirait implicitement que le rapport non vide, lui, va bien : la
  /// réassurance serait rétablie d'un cran plus haut, là où elle est plus difficile à voir.
  /// </summary>
  [Fact]
  public void HasTheSameForceAtZeroFlaggedColumnsAsAtNineHundred()
  {
    var silent = AScreening.Of(
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("livret_modaccomp_code", position: 1)));
    var loud = AScreening.Of(AScreening.AFlaggedColumn("adr_l1", position: 1));

    silent.FlaggedCount.ShouldBe(0);

    var atZero = IncompletenessClause.For(silent);
    var atOne = IncompletenessClause.For(loud);

    atZero.Perimeter.Statement.ShouldBe(atOne.Perimeter.Statement);
    atZero.Beyond.ShouldBe(atOne.Beyond);
    atZero.BeyondReach.ShouldBe(atOne.BeyondReach);
    atZero.RelationToManifest.ShouldBe(atOne.RelationToManifest);
  }

  /// <summary>
  /// La clause n'existe pas détachée d'un rapport : ses comptes ne voudraient rien dire, et elle
  /// n'accompagne jamais rien d'autre qu'une réponse.
  /// </summary>
  [Fact]
  public void OffersNoWayToBuildAClauseThatNoReportProduced()
  {
    typeof(IncompletenessClause)
      .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
      .ShouldBeEmpty();

    // Les deux chemins, et il n'y en a que deux : le rapport chargé, et les comptes que la base
    // calcule pour l'écran d'une table. ⚠️ Le second n'affaiblit pas la règle — la clause reste
    // inconstruisible sans les comptes de CE relevé ; ce qui change est qui les a calculés.
    Should.Throw<ArgumentNullException>(() => IncompletenessClause.For((Screening)null!));
    Should.Throw<ArgumentNullException>(() => IncompletenessClause.For((ScreeningCounts)null!));
  }
}
