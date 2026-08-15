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
  /// L'historique est le <b>complément</b> du courant, calculé au même endroit et par la même règle.
  /// Un déploiement qui n'a qu'un rapport n'a aucun archivé : le courant n'est pas son propre passé.
  /// </summary>
  [Fact]
  public void ReadsEveryReportButTheCurrentOneAsTheHistory()
  {
    var monday = AScreening.LaunchedAt(Monday);
    var thursday = AScreening.LaunchedAt(Monday.AddDays(3));

    Screening.ArchivedAmong([]).ShouldBeEmpty();
    Screening.ArchivedAmong([thursday]).ShouldBeEmpty();
    Screening.ArchivedAmong([monday, thursday]).ShouldBe([monday]);
  }

  /// <summary>
  /// L'historique se lit du plus récent au plus ancien, et l'ordre dans lequel la base a rendu les
  /// lignes ne le décide pas : deux lectures du même déploiement se lisent dans le même ordre.
  /// </summary>
  [Fact]
  public void ReadsTheHistoryMostRecentFirstWhateverOrderTheReportsComeIn()
  {
    var monday = AScreening.LaunchedAt(Monday);
    var tuesday = AScreening.LaunchedAt(Monday.AddDays(1));
    var thursday = AScreening.LaunchedAt(Monday.AddDays(3));

    Screening.ArchivedAmong([monday, thursday, tuesday]).ShouldBe([tuesday, monday]);
    Screening.ArchivedAmong([tuesday, monday, thursday]).ShouldBe([tuesday, monday]);
  }

  /// <summary>
  /// ⚠️ <b>Sur le même tic d'horloge, un seul rapport reste dehors</b> — le même que
  /// <c>CurrentAmong</c> désigne, et pas un autre. Un historique qui aurait tranché la départie
  /// autrement aurait rendu deux fois le même rapport : une fois comme courant, une fois comme
  /// archivé.
  /// </summary>
  [Fact]
  public void NeverLetsTheCurrentReportAppearInItsOwnHistoryEvenOnTheSameTick()
  {
    var first = AScreening.LaunchedAt(Monday, ScreeningId.From(new Guid("00000000-0000-0000-0000-000000000001")));
    var second = AScreening.LaunchedAt(Monday, ScreeningId.From(new Guid("00000000-0000-0000-0000-000000000002")));
    Screening[] deployment = [first, second];

    var archived = Screening.ArchivedAmong(deployment);

    archived.Count.ShouldBe(1);
    archived.ShouldNotContain(Screening.CurrentAmong(deployment)!);
    Screening.ArchivedAmong([second, first]).ShouldBe(archived);
  }

  /// <summary>
  /// ⚠️ <b>Aucun état d'archivage n'existe sur l'agrégat</b>, et la surface est énumérée en toutes
  /// lettres pour que l'y ajouter soit un geste délibéré. « Courant » ne se lit que <i>parmi</i> des
  /// rapports — d'où <c>IsCurrentAmong</c>, qui exige ses frères et ne peut pas devenir un champ.
  /// <para>
  /// ⚠️ <b>Elle ne porte aucun booléen, et le verrou « ce rapport de détection est inachevé » n'en
  /// est pas
  /// devenu un.</b> Il vit en <c>UnreadUnflaggedCount</c> : un <c>IsUnfinished</c> aurait été le
  /// même calcul, mais un booléen sur un agrégat <b>se lit</b> comme l'état que #126 refuse, et le
  /// jour où quelqu'un chercherait à le rendre plus rapide il le persisterait. Le compte, lui, ne
  /// se persiste pas sans qu'on voie qu'on le fait — et il en dit plus à l'humain qui le lit.
  /// </para>
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
      "RetainedOnUnflaggedCount",
      "SetAsideCount",
      "TableCount",
      "Tables",
      "UncategorisedCount",
      "UnreadUnflaggedCount",
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

  /// <summary>
  /// ⚠️ <b>Les tables sont retriées par le service, dans l'ordre des octets</b> — et non dans celui
  /// de la collation du SGBD source, qui range <c>_</c> à sa façon. Deux <c>Operator</c> collant le
  /// même schéma depuis deux réplicas configurés différemment doivent lire le même écran.
  /// </summary>
  [Fact]
  public void SortsTheTablesItselfRatherThanTrustingTheCollationOfTheSourceDatabase()
  {
    // L'ordre de collation d'une base range « llx_bom_bomline » avant « llx_bom_bom_extrafields » ;
    // l'ordre des octets fait l'inverse, parce que « _ » précède « l ». Le relevé arrive donc ici
    // dans l'ordre que la source lui a donné, et c'est celui qu'on refuse de garder.
    var screening = AScreening.Of(
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("rowid", table: "llx_bom_bomline")),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("rowid", table: "llx_bom_bom_extrafields")),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("rowid", table: "adherents")));

    screening.Tables
      .Select(table => table.Table)
      .ShouldBe(["adherents", "llx_bom_bom_extrafields", "llx_bom_bomline"]);
  }

  /// <summary>Une table n'est nommée qu'une fois, quel que soit le nombre de colonnes qu'elle porte.</summary>
  [Fact]
  public void NamesEachTableOnceHoweverManyColumnsItCarries()
  {
    var screening = AScreening.Of(
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 1)),
      AScreening.AFlaggedColumn("adr_l1", position: 2),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("montant", table: "cotisations")));

    screening.Tables.ShouldBe(
    [
      new TableIdentity("public", "adherents"),
      new TableIdentity("public", "cotisations"),
    ]);
  }

  /// <summary>
  /// <b>Le verrou est un compte, et il se recalcule.</b> « Ce rapport de détection est inachevé »
  /// tant qu'une
  /// colonne où rien n'a été vu n'a pas été relue : sans lui, un <c>Operator</c> qui a arbitré ses
  /// lignes signalées croit le travail fini, et l'<c>Omission relue</c> n'a rien rattrapé.
  /// </summary>
  [Fact]
  public void CountsTheColumnsWhereNothingWasSeenThatNobodyHasReReadYet()
  {
    var screening = AScreening.Of(
      AScreening.AFlaggedColumn("adr_l1", position: 1),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 2)),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("date_crea", position: 3)));

    screening.UnreadUnflaggedCount.ShouldBe(2);

    screening.Arbitrate(
      ColumnIdentity.Of("public", "adherents", "id_adh"),
      ScreenedColumnState.SetAside,
      "A. Tissot",
      Monday);

    screening.UnreadUnflaggedCount.ShouldBe(1);
  }

  /// <summary>
  /// ⚠️ <b>Arbitrer toutes les colonnes signalées n'éteint pas le verrou</b>, et c'est très
  /// exactement le mode de panne qu'il existe pour attraper : les signalées sont la minorité du
  /// rapport, et les relire toutes ne relit rien de ce qui a été omis.
  /// </summary>
  [Fact]
  public void KeepsTheLockOnWhenOnlyTheFlaggedColumnsHaveBeenArbitrated()
  {
    var screening = AScreening.Of(
      AScreening.AFlaggedColumn("adr_l1", position: 1),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 2)));

    screening.Arbitrate(
      ColumnIdentity.Of("public", "adherents", "adr_l1"),
      ScreenedColumnState.Retained,
      "A. Tissot",
      Monday);

    screening.AwaitingCount.ShouldBe(1);
    screening.UnreadUnflaggedCount.ShouldBe(1);
  }

  /// <summary>Le verrou tombe quand la dernière colonne où rien n'a été vu a été relue, et pas avant.</summary>
  [Fact]
  public void DropsTheLockOnlyOnceEveryColumnWhereNothingWasSeenHasBeenReRead()
  {
    var screening = AScreening.Of(
      AScreening.AFlaggedColumn("adr_l1", position: 1),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 2)));

    screening.Arbitrate(
      ColumnIdentity.Of("public", "adherents", "id_adh"),
      ScreenedColumnState.SetAside,
      "A. Tissot",
      Monday);

    screening.UnreadUnflaggedCount.ShouldBe(0);
  }

  /// <summary>
  /// <b>Ce que l'<c>Omission relue</c> a rattrapé se compte</b> : une colonne retenue par un humain
  /// là où le service n'avait rien vu. Elle vaut zéro tant que personne n'a relu, et une colonne
  /// <em>écartée</em> ne la fait pas monter — il n'y a rien à rattraper dans un écartement.
  /// </summary>
  [Fact]
  public void CountsWhatAHumanRetainedWhereTheScreeningHadSeenNothing()
  {
    var screening = AScreening.Of(
      AScreening.AFlaggedColumn("adr_l1", position: 1),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 2)),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("date_crea", position: 3)));

    screening.RetainedOnUnflaggedCount.ShouldBe(0);

    screening.Arbitrate(
      ColumnIdentity.Of("public", "adherents", "id_adh"),
      ScreenedColumnState.Retained,
      "A. Tissot",
      Monday);

    screening.Arbitrate(
      ColumnIdentity.Of("public", "adherents", "date_crea"),
      ScreenedColumnState.SetAside,
      "A. Tissot",
      Monday);

    // Retenue par un humain sur une ligne signalée : c'est un accord avec le service, pas un
    // rattrapage, et le compte ne bouge pas.
    screening.Arbitrate(
      ColumnIdentity.Of("public", "adherents", "adr_l1"),
      ScreenedColumnState.Retained,
      "A. Tissot",
      Monday);

    screening.RetainedOnUnflaggedCount.ShouldBe(1);
    screening.RetainedCount.ShouldBe(2);
  }

  /// <summary>
  /// <b>Le geste de lot est borné à la table ouverte et à ses seules colonnes non signalées encore en
  /// attente.</b> Un rapport de cinq mille colonnes doit rester tenable : un écran intenable rétablit
  /// l'<c>Omission silencieuse</c> par l'épuisement.
  /// </summary>
  [Fact]
  public void ReachesOnlyTheUnflaggedColumnsOfTheOpenTableThatAreStillAwaiting()
  {
    var screening = AScreening.Of(
      AScreening.AFlaggedColumn("adr_l1", position: 1),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 2)),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("date_crea", position: 3)),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("montant", table: "cotisations")));

    var arbitrated = screening.ArbitrateInBatch(
      new TableIdentity("public", "adherents"),
      ScreenedColumnState.SetAside,
      "A. Tissot",
      Monday);

    arbitrated.Select(column => column.Identity.Column).ShouldBe(["id_adh", "date_crea"]);

    // ⚠️ La table voisine n'a pas bougé : le lot ne franchit jamais la table ouverte.
    screening.ColumnAt(ColumnIdentity.Of("public", "cotisations", "montant"))
      .ShouldNotBeNull()
      .State.ShouldBe(ScreenedColumnState.Awaiting);
  }

  /// <summary>
  /// ⚠️ <b>Aucun geste de lot ne porte sur une colonne signalée.</b> Une suspicion ne s'écarte jamais
  /// sans avoir été lue une par une : l'écarter en masse est très exactement ce que le rapport existe
  /// pour empêcher.
  /// </summary>
  [Fact]
  public void NeverLetsABatchGestureSettleAColumnTheScreeningFlagged()
  {
    var screening = AScreening.Of(
      AScreening.AFlaggedColumn("adr_l1", position: 1),
      AScreening.AFlaggedColumn("adr_l2", position: 2),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 3)));

    screening.ArbitrateInBatch(
      new TableIdentity("public", "adherents"),
      ScreenedColumnState.SetAside,
      "A. Tissot",
      Monday);

    screening.SetAsideCount.ShouldBe(1);

    // Les deux signalées attendent toujours qu'on les lise, une par une.
    screening.ColumnsOf(new TableIdentity("public", "adherents"))
      .Where(column => column.IsFlagged)
      .ShouldAllBe(column => column.AwaitsAnArbitration);
  }

  /// <summary>
  /// ⚠️ <b>Le lot pose n arbitrages individuels, jamais un état de lot</b> : chaque colonne porte sa
  /// propre signature et sa propre date. Sans cela, la seule trace qu'un humain ait tranché serait
  /// portée par un objet que le rapport ne rend nulle part.
  /// </summary>
  [Fact]
  public void PosesOneSignedAndDatedArbitrationPerColumnRatherThanASingleBatchState()
  {
    var screening = AScreening.Of(
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 1)),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("date_crea", position: 2)));

    var arbitrated = screening.ArbitrateInBatch(
      new TableIdentity("public", "adherents"),
      ScreenedColumnState.Retained,
      "A. Tissot",
      Monday);

    arbitrated.Count.ShouldBe(2);

    foreach (var column in arbitrated)
    {
      var rendered = column.Arbitration.ShouldNotBeNull();

      rendered.State.ShouldBe(ScreenedColumnState.Retained);
      rendered.SignedBy.ShouldBe("A. Tissot");
      rendered.SignedOn.ShouldBe(Monday);
    }

    // ⚠️ Chaque ligne porte le SIEN : une instance partagée aurait fait de n arbitrages un seul objet,
    // et la persistance n'aurait plus eu n lignes à écrire.
    arbitrated[0].Arbitration.ShouldNotBeSameAs(arbitrated[1].Arbitration);
  }

  /// <summary>
  /// <b>Une colonne déjà tranchée dans la table ouverte n'est pas réécrite par le lot.</b> Le geste
  /// sert à liquider ce qui attend, jamais à effacer sous un autre nom ce qu'un humain avait dit.
  /// </summary>
  [Fact]
  public void LeavesAlreadyArbitratedColumnsOfTheOpenTableExactlyAsTheyWere()
  {
    var screening = AScreening.Of(
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 1)),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("date_crea", position: 2)));

    screening.Arbitrate(
      ColumnIdentity.Of("public", "adherents", "id_adh"),
      ScreenedColumnState.Retained,
      "C. Roux",
      Monday);

    screening.ArbitrateInBatch(
      new TableIdentity("public", "adherents"),
      ScreenedColumnState.SetAside,
      "A. Tissot",
      Monday.AddDays(1));

    var untouched = screening.ColumnAt(ColumnIdentity.Of("public", "adherents", "id_adh"))
      .ShouldNotBeNull()
      .Arbitration
      .ShouldNotBeNull();

    untouched.State.ShouldBe(ScreenedColumnState.Retained);
    untouched.SignedBy.ShouldBe("C. Roux");
    untouched.SignedOn.ShouldBe(Monday);
  }

  /// <summary>
  /// ⚠️ <b>Un lot refusé n'écrit rien du tout</b> — pas même sa première colonne. Un lot à moitié posé
  /// serait le pire des deux mondes : l'<c>Operator</c> lirait un refus devant un écran déjà tranché
  /// en partie, sans savoir où le geste s'est arrêté.
  /// </summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void WritesNotASingleColumnWhenTheBatchCarriesNoSignature(string? signedBy)
  {
    var screening = AScreening.Of(
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 1)),
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("date_crea", position: 2)));

    Should.Throw<ArgumentException>(() => screening.ArbitrateInBatch(
      new TableIdentity("public", "adherents"),
      ScreenedColumnState.Retained,
      signedBy,
      Monday));

    screening.AwaitingCount.ShouldBe(2);
  }

  /// <summary>
  /// <b><c>Awaiting</c> n'est pas plus une issue en lot qu'à l'unité</b> : personne ne signe une
  /// absence de décision, fût-elle répétée trente fois.
  /// </summary>
  [Fact]
  public void RefusesToSignTheAbsenceOfADecisionAcrossAWholeTable()
  {
    var screening = AScreening.Of(
      ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh", position: 1)));

    Should.Throw<ArgumentException>(() => screening.ArbitrateInBatch(
      new TableIdentity("public", "adherents"),
      ScreenedColumnState.Awaiting,
      "A. Tissot",
      Monday));

    screening.AwaitingCount.ShouldBe(1);
  }

  /// <summary>
  /// Une table que le rapport ne porte pas ne lève pas : elle n'atteint rien, et le dire est la
  /// réponse — un écran affiché il y a une minute peut nommer une table qu'un second rapport de
  /// détection vient
  /// d'emporter.
  /// </summary>
  [Fact]
  public void ReachesNothingWhenTheNamedTableIsNotInTheReport()
  {
    var screening = AScreening.Of(AScreening.AFlaggedColumn());

    screening.ArbitrateInBatch(
      new TableIdentity("public", "cotisations"),
      ScreenedColumnState.Retained,
      "A. Tissot",
      Monday).ShouldBeEmpty();
  }
}
