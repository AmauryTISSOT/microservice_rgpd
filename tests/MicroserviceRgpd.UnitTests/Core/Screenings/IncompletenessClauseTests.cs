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

    beyondReach.Categories.Select(entry => entry.FrenchName).ShouldBe(
      [
        PersonalDataCategory.HealthData.FrenchLabel,
        "autre catégorie particulière",
        "données relatives aux infractions",
      ],
      ignoreOrder: true);

    beyondReach.Categories.ShouldAllBe(entry => entry.Reason.Length > 0);
  }

  /// <summary>
  /// ⚠️ <b>La partie ne parle plus de la taxonomie.</b> Deux des trois catégories n'y ont plus de
  /// valeur : écrire « trois catégories de la taxonomie » affirmerait une liste qui n'existe pas.
  /// </summary>
  [Fact]
  public void NamesTheCategoriesBeyondReachWithoutClaimingTheTaxonomyCarriesThem()
  {
    var beyondReach = IncompletenessClause.For(AScreening.Of(AScreening.AFlaggedColumn())).BeyondReach;

    beyondReach.Statement.ShouldNotContain("taxonomie", Case.Insensitive);
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
  /// La relation aux systèmes déclarés vit <b>dans la clause</b>, et pas seulement au glossaire : la
  /// borne <c>Aucune modification vers le Manifest</c> n'empêche que le pont technique, et rien en
  /// elle n'empêche un <c>Operator</c> pressé de lire le rapport comme son paysage.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Le texte est gelé par ADR-0006, et ce test est ce qui le gèle.</b> L'ancienne phrase
  /// exigeait l'identifiant <c>Manifest</c> en clair ; il en est retiré, parce qu'ADR-0006 le fait
  /// désigner un écran qui ne porte plus ce nom — l'<c>Operator</c> doit chercher
  /// « Configuration » dans la barre.
  /// </para>
  /// <para>
  /// ⚠️ <b>Le nom cité est celui de LA BARRE.</b> Depuis ADR-0008, l'écran en porte deux :
  /// « Configuration » dans la barre, « Configuration du microservice RGPD » sur la carte de
  /// l'accueil et en titre. La phrase décrit un geste de navigation — elle envoie l'<c>Operator</c>
  /// cliquer —, et le nom qu'il doit reconnaître est celui qui est écrit là où il clique. Ce test
  /// est donc <b>aussi</b> ce qui empêche la phrase de repartir vers la forme pleine.
  /// </para>
  /// <para>
  /// ⚠️ <b>Le nom défini part, le verbe reste</b> — « ne recense pas… c'est vous qui les recensez ».
  /// C'est ce qui retire l'autorité d'un recensement <b>sans retirer le mot</b> : le défini
  /// d'identité conférait cette autorité, la forme verbale rend la tenue de la liste à celui qui la
  /// tient. Et rien n'y présente cette liste comme close — l'<c>Omission silencieuse</c> reste
  /// visible.
  /// </para>
  /// </remarks>
  [Fact]
  public void SaysInTheAnswerItselfThatTheOperatorIsTheOneWhoListsTheirSystems()
  {
    var clause = IncompletenessClause.For(AScreening.Of(AScreening.AFlaggedColumn()));

    // Le texte entier, mot pour mot : c'est un texte gelé, et rien de moins qu'une égalité ne le
    // gèle. ⚠️ Elle porte aussi le retrait de l'identifiant Manifest en clair, qui nommait un écran
    // que le renommage fait changer de nom.
    clause.RelationToManifest.ShouldBe(
      "Ce rapport de détection ne recense pas vos systèmes : c'est vous qui les recensez, à la "
      + "main, système par système, dans « Configuration ». La liste que vous "
      + "y tenez ne garantit pas qu'il n'en existe pas d'autres.");

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
    Should.Throw<ArgumentNullException>(
      () => IncompletenessClause.For(null!, ListingOrigin.Pasted));
  }

  /// <summary>
  /// ⚠️ <b>Le cas nul échoue bruyamment, à l'affichage comme à l'enregistrement.</b> Un rapport dont
  /// on ne sait pas s'il a été collé ou scanné rendrait l'une des deux clauses au hasard, et l'une
  /// des deux ment.
  /// </summary>
  [Fact]
  public void RefusesToRenderAClauseForAListingNobodySaidTheOriginOf()
  {
    var counts = ScreeningCounts.Of(AScreening.Of(AScreening.AFlaggedColumn()));

    Should.Throw<ArgumentException>(
      () => IncompletenessClause.For(counts, ListingOrigin.Unspecified));
    Should.Throw<ArgumentNullException>(() => IncompletenessClause.For(counts, null!));

    // ⚠️ Le refus vit AUSSI sur le record : un record positionnel a un constructeur public, et sans
    // ce garde le périmètre rendrait en silence les phrases du chemin collé sur un relevé dont
    // personne ne sait ce qu'il a lu.
    Should.Throw<ArgumentException>(() => new ReadPerimeter(
      ListingOrigin.Unspecified, [], [], 1, 1, 0, PreviewAbsenceCounts.None));

    // Et sur les comptes : absents, les quatre familles disparaîtraient de l'écran d'un rapport
    // scanné, qui se rendrait alors exactement comme un rapport collé.
    Should.Throw<ArgumentNullException>(() => new ScreeningCounts(1, 1, 0, 0, 0, 0, 0, 0, 0, null!));
  }

  /// <summary>
  /// <b>Une seule des quatre parties varie.</b> Les trois autres sont mot pour mot les mêmes des
  /// deux côtés — et pour la partie 3, c'est ce que la réécriture des trois motifs a acheté : ainsi
  /// écrits, ils sont vrais aussi bien d'un relevé collé que d'un relevé scanné.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Faire sortir la santé de la liste sur le chemin scanné aurait été le pire des trois
  /// choix</b> : promettre qu'on sait voir la santé parce qu'on a lu cinq valeurs, dans la seule
  /// partie du produit qui existe pour dire l'inverse.
  /// </remarks>
  [Fact]
  public void VariesTheReadPerimeterAndNotOneOfTheThreeOtherParts()
  {
    var pasted = IncompletenessClause.For(AScreening.Of(AScreening.AFlaggedColumn()));
    var scanned = IncompletenessClause.For(
      AScreening.OfListing(ListingOrigin.Scanned, AScreening.AFlaggedColumn()));

    scanned.Beyond.ShouldBe(pasted.Beyond);
    scanned.BeyondReach.ShouldBe(pasted.BeyondReach);
    scanned.RelationToManifest.ShouldBe(pasted.RelationToManifest);

    scanned.Perimeter.Statement.ShouldNotBe(pasted.Perimeter.Statement);
  }

  /// <summary>
  /// <b>Les trois motifs hors de portée sont vrais des deux côtés</b> : chacun nomme ce que ni le
  /// nom, ni le type, ni quelques valeurs ne peuvent atteindre — une forme qui n'existe pas, une
  /// finalité que rien ne déclare, une qualité du responsable que rien n'annonce.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Deux des trois motifs d'origine ne parlaient que du schéma ou du nom</b>, et le chemin
  /// scanné les rendait faux ou muets : « une détection <b>de schéma</b> ne la verra pas » est faux
  /// dès que le service lit des valeurs.
  /// </remarks>
  [Fact]
  public void GivesReasonsThatStayTrueOnceTheServiceHasReadSomeValues()
  {
    var reasons = IncompletenessClause
      .For(AScreening.OfListing(ListingOrigin.Scanned, AScreening.AFlaggedColumn()))
      .BeyondReach.Categories.ToDictionary(entry => entry.FrenchName, entry => entry.Reason);

    // Aucun des trois ne peut plus fonder sa limite sur le seul schéma : le service en a lu, des
    // valeurs, et un motif qui l'ignore ment.
    reasons.Values.ShouldAllBe(reason => !reason.Contains("détection de schéma", StringComparison.Ordinal));
    reasons.Values.ShouldAllBe(reason => !reason.Contains("lecteur de schéma", StringComparison.Ordinal));

    reasons[PersonalDataCategory.HealthData.FrenchLabel].ShouldContain("forme", Case.Insensitive);
    reasons["autre catégorie particulière"].ShouldContain("finalité", Case.Insensitive);
    reasons["données relatives aux infractions"].ShouldContain("responsable", Case.Insensitive);
  }

  /// <summary>
  /// <b>Le témoin de la clause, niveau 1 — sur l'objet, et symétrique.</b> Construit en
  /// <c>Scanné</c>, le périmètre porte les valeurs dans <see cref="ReadPerimeter.ReadAsSignal"/>, la
  /// mise en garde du premier venu et les quatre comptes ; en <c>Collé</c>, aucun des trois.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Symétrique, sans quoi une correspondance inversée passerait</b> : le code qui rendrait la
  /// phrase du scan sur un relevé collé serait vert.
  /// </remarks>
  [Fact]
  public void CarriesTheThreeScannedOnlyThingsOnTheScannedPathAndNoneOfThemOnThePastedOne()
  {
    var scanned = ScannedPerimeter();
    var pasted = IncompletenessClause.For(AScreening.Of(AScreening.AFlaggedColumn())).Perimeter;

    scanned.ReadAsSignal.ShouldContain(
      entry => entry.Contains("valeurs au plus de chaque colonne", StringComparison.Ordinal));
    scanned.FirstComeBias.ShouldNotBeNullOrWhiteSpace();
    scanned.ColumnsWithoutAPreview.ShouldNotBeNull();

    pasted.ReadAsSignal.ShouldNotContain(
      entry => entry.Contains("valeur", StringComparison.OrdinalIgnoreCase));
    pasted.FirstComeBias.ShouldBeNull();

    // ⚠️ ABSENTS, jamais à zéro : rendre « 0 par droits refusés » sur un relevé collé affirmerait
    // qu'un prélèvement a eu lieu et n'a rien refusé — une incomplétude inventée là où il n'y en a
    // pas.
    pasted.ColumnsWithoutAPreview.ShouldBeNull();
  }

  /// <summary>
  /// <b>Sur le chemin collé, pas un caractère ne bouge.</b> La promesse la plus forte du produit
  /// reste intégralement vraie chez l'<c>Operator</c> qui colle : on ne paie pas la vérité du chemin
  /// neuf avec la sienne.
  /// </summary>
  [Fact]
  public void LeavesEverySingleCharacterOfThePastedPathWhereItWas()
  {
    var perimeter = IncompletenessClause.For(AScreening.Of(AScreening.AFlaggedColumn())).Perimeter;

    perimeter.Statement.ShouldBe(
      "Ce rapport de détection n'a lu qu'un relevé de colonnes, celui que vous avez collé, et rien "
      + "d'autre.");

    perimeter.FilterStatement.ShouldBe(
      "Lus seulement pour écarter, jamais comme indice de sens : la forme d'une colonne ne dit pas "
      + "ce qu'elle porte, et le service n'a jamais vu une seule valeur.");
  }

  /// <summary>
  /// ⚠️ <b>Sur le chemin scanné, la seconde phrase ne peut pas être coupée : ici la queue est la
  /// preuve.</b> Ce que le service tient est ce qu'un compte de connexion lui a présenté, et lui
  /// seul sait ce qu'il a tu.
  /// </summary>
  [Fact]
  public void OwnsUpToNotKnowingWhatTheConnectionAccountKeptFromIt()
  {
    var perimeter = ScannedPerimeter();

    perimeter.Statement.ShouldBe(
      "Ce rapport de détection n'a lu qu'un relevé de colonnes, celui que le compte de connexion a "
      + "présenté au service, et rien d'autre. Le service ne peut pas savoir si ce compte lui a "
      + "présenté toute la base.");

    // La queue de FilterStatement, elle, tombe et rien ne la remplace : traitement inverse, et
    // délibéré — là-bas le début de phrase porte sa propre justification.
    perimeter.FilterStatement.ShouldBe(
      "Lus seulement pour écarter, jamais comme indice de sens : la forme d'une colonne ne dit pas "
      + "ce qu'elle porte.");
  }

  /// <summary>
  /// <b>Le témoin du chiffre unique.</b> « Cinq valeurs au plus » affiché dans la clause vient de la
  /// même source que la borne du prélèvement.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Écrit à la main des deux côtés, il divergerait au premier changement de borne</b>, et le
  /// texte mentirait sans que rien ne rougisse. ⚠️ Et c'est un <b>chiffre</b>, jamais « quelques » :
  /// un <c>Operator</c> qui lit « quelques » ne sait pas si le service a lu cinq lignes ou cinquante
  /// mille, imagine le pire, et bloque — alors que le chiffre exact est la bonne nouvelle.
  /// </remarks>
  [Fact]
  public void SpellsTheSamplingBoundFromTheOnlyPlaceThatHoldsIt()
  {
    var perimeter = ScannedPerimeter();

    var everything = string.Join(" ", perimeter.ReadAsSignal.Append(perimeter.FirstComeBias!));

    everything.ShouldContain(ColumnPreview.MaxValuesInWords, Case.Sensitive);
    everything.ShouldNotContain("quelques", Case.Insensitive);

    // La borne elle-même, écrite en toutes lettres : c'est la seule assertion qui rougirait si
    // MaxValues bougeait sans que MaxValuesInWords suive.
    ColumnPreview.MaxValuesInWords.ShouldBe("cinq");
    ColumnPreview.MaxValues.ShouldBe(5);
  }

  /// <summary>
  /// Les quatre comptes sont ceux de <b>ce</b> relevé, familles séparées et <b>zéros compris</b> :
  /// « 412 colonnes sans aperçu » ne fait rien faire à personne, « 18 par droits refusés » envoie
  /// l'<c>Operator</c> demander un accès à son DBA.
  /// </summary>
  [Fact]
  public void CountsTheColumnsWithoutAPreviewFamilyByFamilyAndShowsTheZeroes()
  {
    var perimeter = ScannedPerimeter();

    var counted = perimeter.ColumnsWithoutAPreview.ShouldNotBeNull();

    counted.AccessDenied.ShouldBe(1);
    counted.UnsampleableType.ShouldBe(1);
    counted.NoValueReturned.ShouldBe(0);
    counted.ReadFailed.ShouldBe(0);

    // Les quatre familles sont rendues, y compris celles qui valent zéro : un énoncé particulier
    // quand il n'y a rien à dire rétablirait la réassurance un cran plus haut.
    counted.ByReason.Count.ShouldBe(PreviewAbsenceReason.List.Count);
  }

  /// <summary>
  /// ⚠️ <b>Aucun aperçu n'a abouti se dit à l'échelle du rapport</b>, en plus de la raison portée par
  /// chaque ligne et jamais à sa place : un <c>Operator</c> qui déroule cinq mille lignes ne
  /// recompose pas ce fait, et c'est justement celui qui devrait le faire rescanner.
  /// </summary>
  [Fact]
  public void SaysAtTheScaleOfTheReportWhenNotOneSinglePreviewSucceeded()
  {
    // La première colonne porte un aperçu abouti : rien ne se dit à l'échelle du rapport.
    IncompletenessClause.For(AScreening.OfListing(
      ListingOrigin.Scanned,
      AScreening.AFlaggedColumn(),
      ScreenedColumn.NothingSeen(
        AScreening.AListedColumn("id_adh", position: 2),
        PreviewAbsenceReason.AccessDenied)))
      .Perimeter.NoPreviewSucceeded.ShouldBeFalse();

    IncompletenessClause.For(AScreening.OfListing(
      ListingOrigin.Scanned,
      ScreenedColumn.NothingSeen(
        AScreening.AListedColumn("id_adh", position: 1),
        PreviewAbsenceReason.AccessDenied),
      ScreenedColumn.NothingSeen(
        AScreening.AListedColumn("photo", position: 2),
        PreviewAbsenceReason.UnsampleableType)))
      .Perimeter.NoPreviewSucceeded.ShouldBeTrue();

    // ⚠️ Faux sur le chemin collé, et pas par accident : rien n'y a été tenté, donc rien n'y a
    // échoué.
    IncompletenessClause.For(AScreening.Of(AScreening.AFlaggedColumn()))
      .Perimeter.NoPreviewSucceeded.ShouldBeFalse();
  }

  /// <summary>
  /// Un relevé scanné dont deux colonnes n'ont aucun aperçu, une par famille corrigeable et une par
  /// famille structurelle : c'est le relevé qu'attendent les témoins du chemin scanné.
  /// </summary>
  private static ReadPerimeter ScannedPerimeter()
  {
    return IncompletenessClause.For(AScreening.OfListing(
      ListingOrigin.Scanned,
      AScreening.AFlaggedColumn(),
      ScreenedColumn.NothingSeen(
        AScreening.AListedColumn("id_adh", position: 2),
        PreviewAbsenceReason.AccessDenied),
      ScreenedColumn.NothingSeen(
        AScreening.AListedColumn("photo", position: 3),
        PreviewAbsenceReason.UnsampleableType))).Perimeter;
  }
}
