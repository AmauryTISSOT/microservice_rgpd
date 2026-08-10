using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// L'agrégat n'a <b>aucun état</b> : « courant » et « archivé » sont des calculs, et l'archivage
/// n'écrit rien. Si <c>Archived</c> était un état, une transition ratée laisserait deux rapports
/// courants et l'<c>Operator</c> arbitrerait le mauvais.
/// </summary>
public class ScreeningTests
{
  private static readonly DateTimeOffset Monday = new(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);

  [Fact]
  public void HoldsTheListingItWasGivenWithTheEngineThatProducedIt()
  {
    var screening = AScreening.Of(AScreening.AFlaggedColumn());

    screening.Database.ShouldBe("galette_prod");
    screening.Dialect.ShouldBe("postgresql");
    screening.Engine.ShouldBe(new ScreeningEngineIdentity("lexique-fr-en", "1.0.0"));
    screening.ColumnCount.ShouldBe(1);
  }

  /// <summary>
  /// ⚠️ <b>Il est entier ou il n'existe pas.</b> Un rapport bâti sur 99 % d'un relevé se lirait comme
  /// ayant tout rendu, et les colonnes manquantes seraient précisément celles que personne ne
  /// relirait jamais.
  /// </summary>
  [Fact]
  public void RefusesAReportThatRendersFewerColumnsThanTheListingDeclared()
  {
    Should.Throw<ArgumentException>(() => Screening.Of(
      ScreeningId.Next(),
      "galette_prod",
      "postgresql",
      AScreening.Engine,
      declaredColumnCount: 3,
      [AScreening.AFlaggedColumn()],
      AScreening.LaunchedOn));
  }

  /// <summary>
  /// Le triplet identifie une colonne <b>à l'intérieur</b> d'un rapport : deux lignes qui le
  /// partagent parlent de la même colonne, et l'un des deux arbitrages écraserait l'autre.
  /// </summary>
  [Fact]
  public void RefusesTwoLinesSharingTheSameTriple()
  {
    Should.Throw<ArgumentException>(() => AScreening.Of(
      AScreening.AFlaggedColumn(position: 1),
      AScreening.AFlaggedColumn(position: 2)));
  }

  /// <summary>Le même nom de colonne dans deux schémas n'est pas un doublon : c'est ce que le couple (table, colonne) aurait confondu.</summary>
  [Fact]
  public void TellsApartTheSameColumnNameLivingInTwoSchemas()
  {
    var screening = AScreening.Of(
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("email", schema: "public")),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("email", schema: "archive")));

    screening.ColumnCount.ShouldBe(2);
  }

  /// <summary>
  /// Le rapport le plus récemment lancé est le courant, et les autres sont archivés par le seul fait
  /// qu'il existe. <b>Rien n'a été écrit pour cela.</b>
  /// </summary>
  [Fact]
  public void ReadsTheMostRecentlyLaunchedReportAsTheCurrentOne()
  {
    var monday = AScreening.LaunchedAt(Monday);
    var thursday = AScreening.LaunchedAt(Monday.AddDays(3));
    Screening[] deployment = [monday, thursday];

    Screening.CurrentAmong(deployment).ShouldBeSameAs(thursday);
    thursday.IsCurrentAmong(deployment).ShouldBeTrue();
    monday.IsArchivedAmong(deployment).ShouldBeTrue();
  }

  /// <summary>L'ordre dans lequel on présente les rapports ne décide rien : seule la date de lancement le fait.</summary>
  [Fact]
  public void ReadsTheSameCurrentReportWhateverOrderTheyComeIn()
  {
    var monday = AScreening.LaunchedAt(Monday);
    var thursday = AScreening.LaunchedAt(Monday.AddDays(3));

    Screening.CurrentAmong([monday, thursday]).ShouldBeSameAs(thursday);
    Screening.CurrentAmong([thursday, monday]).ShouldBeSameAs(thursday);
  }

  /// <summary>
  /// ⚠️ <b>Il ne peut pas y avoir deux courants</b>, même sur le même tic d'horloge : c'est très
  /// exactement le mode de panne qu'un état <c>Archived</c> aurait produit, et le calcul doit être
  /// total pour ne pas le rouvrir.
  /// </summary>
  [Fact]
  public void NeverLeavesTwoReportsCurrentEvenWhenLaunchedOnTheSameTick()
  {
    var first = AScreening.LaunchedAt(Monday, ScreeningId.From(new Guid("00000000-0000-0000-0000-000000000001")));
    var second = AScreening.LaunchedAt(Monday, ScreeningId.From(new Guid("00000000-0000-0000-0000-000000000002")));
    Screening[] deployment = [first, second];

    Screening.CurrentAmong(deployment).ShouldBeSameAs(Screening.CurrentAmong([second, first]));
    deployment.Count(screening => screening.IsCurrentAmong(deployment)).ShouldBe(1);
  }

  /// <summary>
  /// Deux instances du même rapport <b>sont</b> le même rapport : EF Core rematérialise, et un
  /// contrôle de référence aurait répondu « archivé » à un jumeau sorti d'un autre contexte de suivi.
  /// </summary>
  [Fact]
  public void RecognisesItselfThroughItsIdentityRatherThanThroughItsInstance()
  {
    var id = ScreeningId.Next();
    var current = AScreening.LaunchedAt(Monday, id);
    var rematerialised = AScreening.LaunchedAt(Monday, id);

    rematerialised.IsCurrentAmong([current]).ShouldBeTrue();
  }

  /// <summary>
  /// ⚠️ <b>Un rapport absent du lot lève, plutôt que d'être réputé archivé.</b> Répondre « archivé »
  /// barrerait silencieusement l'arbitrage du seul rapport qu'on ait le droit d'arbitrer ; répondre
  /// « courant » ferait arbitrer le mauvais, ce qui est le mode de panne que le refus d'un état
  /// <c>Archived</c> existe pour empêcher. La question n'a pas de bonne réponse par défaut.
  /// </summary>
  [Fact]
  public void RefusesToSituateItselfInADeploymentItDoesNotBelongTo()
  {
    var stranger = AScreening.LaunchedAt(Monday);
    Screening[] elsewhere = [AScreening.LaunchedAt(Monday.AddDays(3))];

    Should.Throw<ArgumentException>(() => stranger.IsCurrentAmong(elsewhere));
    Should.Throw<ArgumentException>(() => stranger.IsArchivedAmong(elsewhere));
  }

  /// <summary>Le tout premier démarrage chez un client : aucun rapport, donc aucun courant, et ce n'est pas une panne.</summary>
  [Fact]
  public void ReadsNoCurrentReportWhenTheDeploymentHasLaunchedNone()
  {
    Screening.CurrentAmong([]).ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Aucun état d'archivage n'existe sur l'agrégat</b>, et la surface est énumérée en toutes
  /// lettres pour que l'y ajouter soit un geste délibéré. « Courant » ne se lit que <i>parmi</i> des
  /// rapports — d'où <c>IsCurrentAmong</c>, qui exige ses frères et ne peut pas devenir un champ.
  /// </summary>
  [Fact]
  public void OffersNoArchiveFlagNoStatusAndNoStateOfItsOwn()
  {
    var surface = typeof(Screening)
      .GetProperties()
      .Select(property => property.Name)
      .Order(StringComparer.Ordinal);

    surface.ShouldBe(
    [
      "AwaitingCount",
      "ColumnCount",
      "Columns",
      "ColumnsWithoutACommentCount",
      "Database",
      "DeclaredColumnCount",
      "Dialect",
      "Engine",
      "FlaggedCount",
      "Id",
      "LaunchedOn",
      "RetainedCount",
      "SetAsideCount",
      "TableCount",
      "UncategorisedCount",
    ]);
  }

  /// <summary>
  /// L'avancement est un <b>compte</b> sur les lignes, jamais un état de haut niveau rassurant :
  /// « douze colonnes en attente » se vérifie, « en cours » se croit.
  /// </summary>
  [Fact]
  public void CountsWhatIsAwaitingRetainedAndSetAsideRatherThanNamingAnOverallState()
  {
    var screening = AScreening.Of(
      AScreening.AFlaggedColumn("adr_l1", position: 1),
      AScreening.AFlaggedColumn("adr_l2", position: 2),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 3)));

    screening.Arbitrate(
      ColumnIdentity.Of("public", "adherents", "adr_l1"),
      ScreenedColumnState.Retained,
      "A. Tissot",
      Monday);

    screening.Arbitrate(
      ColumnIdentity.Of("public", "adherents", "adr_l2"),
      ScreenedColumnState.SetAside,
      "A. Tissot",
      Monday);

    screening.RetainedCount.ShouldBe(1);
    screening.SetAsideCount.ShouldBe(1);
    screening.AwaitingCount.ShouldBe(1);
  }

  /// <summary>
  /// Le taux de repli est l'instrument de mesure de la taxonomie : il se compte, et il n'est pas un
  /// défaut à cacher.
  /// </summary>
  [Fact]
  public void CountsWhatFellBackOnTheOnlyFallbackThereIs()
  {
    var screening = AScreening.Of(
      ScreenedColumn.Flagged(
        AScreening.AListedColumn("cfdata", position: 1, dataType: "jsonb"),
        PersonalDataCategory.PersonalDataUncategorised,
        RuleStrength.TypeHeuristic,
        "conteneur libre : le contenu n'est pas lisible depuis le schéma"),
      AScreening.AFlaggedColumn("adr_l1", position: 2),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 3)));

    screening.UncategorisedCount.ShouldBe(1);
    screening.FlaggedCount.ShouldBe(2);
  }

  /// <summary>
  /// La table est l'unité de travail, et ses colonnes sortent dans l'ordre du schéma — jamais
  /// l'alphabétique, sans quoi <c>adr_l1</c>, <c>adr_l2</c>, <c>cp</c>, <c>ville</c> perdent le
  /// voisinage qui les rend lisibles.
  /// </summary>
  [Fact]
  public void RendersTheColumnsOfATableInTheOrderTheSchemaGivesThem()
  {
    var screening = AScreening.Of(
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("ville", position: 4)),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("adr_l1", position: 2)),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("cp", position: 3)),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 1)));

    screening.ColumnsOf(new TableIdentity("public", "adherents"))
      .Select(column => column.Identity.Column)
      .ShouldBe(["id_adh", "adr_l1", "cp", "ville"]);
  }

  /// <summary>Un triplet qui ne désigne rien ne lève pas : il ne désigne rien, et le dire est la réponse.</summary>
  [Fact]
  public void SaysSoPlainlyWhenATripleDesignatesNothingHere()
  {
    var screening = AScreening.Of(AScreening.AFlaggedColumn());

    screening.Arbitrate(
      ColumnIdentity.Of("public", "cotisations", "montant"),
      ScreenedColumnState.Retained,
      "A. Tissot",
      Monday).ShouldBeNull();
  }
}
