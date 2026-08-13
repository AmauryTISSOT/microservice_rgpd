namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Ce qu'un dépistage a rendu sur un <c>ColumnListing</c> : une <see cref="ScreenedColumn"/> par
/// colonne du relevé, et l'agrégat de ce contexte. C'est <b>l'acte et son résultat</b> — il n'existe
/// pas d'objet « lancement » distinct de l'objet rendu.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il n'a aucun état</b>, et c'est le point qu'on relit trois fois avant d'y toucher.
/// « Courant » est un <b>calcul</b> : le rapport le plus récent du déploiement est le courant, tous
/// les autres sont archivés par le seul fait qu'un plus récent existe — voir
/// <see cref="CurrentAmong"/>. Si <c>Archived</c> était un état, une transition ratée laisserait deux
/// rapports courants et l'<c>Operator</c> arbitrerait le mauvais. Même mécanique que le refus d'un
/// état « en retard » dans <c>Casework</c>, où le dépassement est un calcul pour que jamais un retard
/// non détecté ne devienne un retard inexistant. <b>Archiver n'écrit donc rien</b> : ni drapeau, ni
/// date, ni table à part.
/// </para>
/// <para>
/// <b>L'avancement non plus n'est pas un état.</b> « Douze colonnes en attente » est un
/// <b>compte</b> sur les <see cref="Columns"/> — voir <see cref="AwaitingCount"/> — jamais un état
/// de haut niveau rassurant.
/// </para>
/// <para>
/// <b>Il est détenu et vit plusieurs jours</b> : un relevé s'arbitre en plusieurs fois, colonne par
/// colonne. Son grain est le <b>déploiement</b>, jamais le dossier ; il n'écrit rien au
/// <c>Ledger</c>, n'a aucune échéance et vit jusqu'à ce qu'un <c>Operator</c> le supprime.
/// </para>
/// <para>
/// ⚠️ <b>Il est entier ou il n'existe pas.</b> Le relevé déclare le nombre de colonnes qu'il porte,
/// et un rapport bâti sur 99 % d'un relevé <b>se lirait comme complet</b> : l'<c>Omission relue</c>
/// repose entièrement sur le fait que le rapport rend <b>toutes</b> les colonnes. Trois colonnes que
/// personne ne relira jamais, dans un artefact qui promet qu'on relit tout, est la faille exacte que
/// ce contexte existe pour ne pas avoir.
/// </para>
/// </remarks>
public sealed class Screening : IAggregateRoot
{
  /// <summary>
  /// Le plafond du nom de base, en unités UTF-16 — même mesure que celle d'un nom d'objet, et pour
  /// la même raison vérifiée.
  /// </summary>
  public const int MaxDatabaseNameLength = 100;

  /// <summary>
  /// Le plafond du dialecte déclaré. C'est une constante <b>propre à ce contexte</b> : lire celle de
  /// <c>Casework</c> serait une traversée, pour l'économie d'un entier.
  /// </summary>
  public const int MaxDialectLength = 64;

  private readonly List<ScreenedColumn> _columns;

  private Screening(
    ScreeningId id,
    string database,
    string dialect,
    ScreeningEngineIdentity engine,
    int declaredColumnCount,
    List<ScreenedColumn> columns,
    DateTimeOffset launchedOn)
  {
    Id = id;
    Database = database;
    Dialect = dialect;
    Engine = engine;
    DeclaredColumnCount = declaredColumnCount;
    _columns = columns;
    LaunchedOn = launchedOn;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private Screening()
  {
    Database = string.Empty;
    Dialect = string.Empty;
    Engine = null!;
    _columns = [];
  }

  /// <summary>L'identité engendrée au lancement.</summary>
  public ScreeningId Id { get; private set; }

  /// <summary>
  /// Le nom de base que le SGBD a donné au relevé, enregistré <b>sans jamais être vérifié</b>. C'est
  /// un repère pour l'humain qui relit trois jours plus tard, jamais une identité sur laquelle bâtir
  /// une comparaison.
  /// </summary>
  public string Database { get; private set; }

  /// <summary>
  /// Le SGBD dont le relevé se déclare. Sans lui, « cette colonne n'a pas de commentaire » et « ce
  /// SGBD n'en rend jamais » se liraient pareil, ce qui est l'<c>Omission silencieuse</c> déplacée
  /// d'un cran ; avec lui, l'absence est <b>nommée</b>.
  /// </summary>
  public string Dialect { get; private set; }

  /// <summary>Qui a dépisté, et dans quelle version. Le domaine ne l'interprète jamais.</summary>
  public ScreeningEngineIdentity Engine { get; private set; }

  /// <summary>
  /// Le nombre de colonnes que le relevé <b>déclare</b> porter. Il est conservé à côté du compte
  /// réel parce que c'est leur égalité qui rend une troncature au collage détectable.
  /// </summary>
  public int DeclaredColumnCount { get; private set; }

  /// <summary>Quand le dépistage a été lancé. C'est ce qui décide, et seul, quel rapport est le courant.</summary>
  public DateTimeOffset LaunchedOn { get; private set; }

  /// <summary>
  /// Toutes les colonnes du relevé, dans l'ordre où il les rend. ⚠️ <b>Toutes</b> : filtrer les
  /// <c>Unflagged</c> — dans une requête, dans l'écran, dans une pagination par défaut — rétablit
  /// l'<c>Omission silencieuse</c> sans qu'aucune ligne de doctrine n'ait été modifiée.
  /// </summary>
  public IReadOnlyList<ScreenedColumn> Columns => _columns;

  /// <summary>Combien de colonnes attendent encore qu'un humain les tranche.</summary>
  public int AwaitingCount => _columns.Count(column => column.AwaitsAnArbitration);

  /// <summary>Combien de colonnes un humain a retenues, sous son nom.</summary>
  public int RetainedCount => _columns.Count(column => column.State == ScreenedColumnState.Retained);

  /// <summary>Combien de colonnes un humain a écartées, sous son nom.</summary>
  public int SetAsideCount => _columns.Count(column => column.State == ScreenedColumnState.SetAside);

  /// <summary>Combien de colonnes le dépistage a signalées. Le complément est ce qu'il n'a pas vu, jamais ce qui est inoffensif.</summary>
  public int FlaggedCount => _columns.Count(column => column.IsFlagged);

  /// <summary>
  /// Combien de colonnes sont tombées sur le repli. <b>C'est l'instrument de mesure de la
  /// taxonomie</b>, et ce n'est pas un défaut à minimiser : un taux qui monte est le signal qu'il
  /// manque une valeur.
  /// </summary>
  public int UncategorisedCount =>
    _columns.Count(column => column.Category == PersonalDataCategory.PersonalDataUncategorised);

  /// <summary>Combien de colonnes ce relevé porte réellement.</summary>
  public int ColumnCount => _columns.Count;

  /// <summary>Combien de colonnes n'ont de commentaire ni à leur niveau ni à celui de leur table.</summary>
  public int ColumnsWithoutACommentCount => _columns.Count(column => !column.Listed.CarriesAComment);

  /// <summary>Combien de tables distinctes ce relevé couvre.</summary>
  public int TableCount => _columns.Select(column => column.Identity.TableIdentity).Distinct().Count();

  /// <summary>
  /// Combien de colonnes un humain a retenues alors que le dépistage n'avait <b>rien vu</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est la mesure directe de ce que l'<c>Omission relue</c> a rattrapé</b>, et le seul
  /// compte du rapport qui ne parle pas du dépistage mais de sa relecture : un <c>Retained</c> posé
  /// sur une colonne <c>Unflagged</c> prouve qu'un <b>humain</b> l'a retenue, jamais que le service
  /// l'avait vue. Il vaut zéro tant que personne n'a relu, et c'est très exactement ce qu'on lui
  /// demande de dire.
  /// </remarks>
  public int RetainedOnUnflaggedCount => _columns.Count(column =>
    column.State == ScreenedColumnState.Retained && !column.IsFlagged);

  /// <summary>
  /// Combien de colonnes où <b>rien n'a été vu</b> n'ont pas encore été relues — le compte que porte
  /// le verrou « ce dépistage est inachevé ».
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est un compte, jamais un état</b>, et la nuance est celle que ce contexte relit trois
  /// fois : le <c>Screening</c> n'a aucun état, mais il se compte. Sans ce verrou, un
  /// <c>Operator</c> qui a arbitré ses tables signalées voit une surface qui se tait et
  /// <b>croit le travail fini</b>, alors que l'<c>Omission relue</c> n'a précisément rien rattrapé.
  /// Il se recalcule à chaque rendu : rien ne le mémorise, et rien n'aurait à le mettre à jour.
  /// </remarks>
  public int UnreadUnflaggedCount => _columns.Count(column =>
    !column.IsFlagged && column.AwaitsAnArbitration);

  /// <summary>
  /// Les tables du rapport, <b>retriées par le service</b> — schéma puis table, dans l'ordre des
  /// octets.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Le pivot arrive déjà trié, mais selon la collation du SGBD source</b>, qui n'ordonne pas
  /// <c>_</c> comme l'ordre des octets. Sans ce retri, deux <c>Operator</c> collant le même schéma
  /// depuis deux réplicas configurés différemment voient deux écrans ordonnés différemment — sur une
  /// surface qu'on reprend pendant trois jours et où l'on cherche une table de mémoire.
  /// </para>
  /// <para>
  /// ⚠️ <b>C'est l'inverse de la règle des colonnes, et pour la bonne raison</b> : à l'intérieur
  /// d'une table le voisinage porte du sens — <c>adr_l1</c>, <c>adr_l2</c>, <c>cp</c>,
  /// <c>ville</c> —, et <see cref="ColumnsOf"/> garde donc l'ordre du schéma. Entre deux tables il
  /// n'en porte aucun.
  /// </para>
  /// </remarks>
  public IReadOnlyList<TableIdentity> Tables =>
  [
    .. _columns
      .Select(column => column.Identity.TableIdentity)
      .Distinct()
      .OrderBy(table => table.Schema, StringComparer.Ordinal)
      .ThenBy(table => table.Table, StringComparer.Ordinal),
  ];

  /// <summary>
  /// Lance un dépistage, ou refuse. Le refus est une <b>programmation fautive</b> : un relevé mal
  /// formé se refuse en bloc à l'ingestion, où le refus est lisible et où l'<c>Operator</c> n'a qu'à
  /// relancer sa requête.
  /// </summary>
  /// <param name="id">L'identité engendrée.</param>
  /// <param name="database">Le nom de base que le relevé rapporte.</param>
  /// <param name="dialect">Le SGBD dont le relevé se déclare.</param>
  /// <param name="engine">Qui a dépisté, et dans quelle version.</param>
  /// <param name="declaredColumnCount">Le nombre de colonnes que le relevé déclare porter.</param>
  /// <param name="columns">Une ligne par colonne du relevé, dans son ordre.</param>
  /// <param name="launchedOn">L'instant du lancement.</param>
  /// <exception cref="ArgumentNullException">Un des arguments est absent.</exception>
  /// <exception cref="ArgumentException">
  /// Le nom de base ou le dialecte est vide, démesuré ou porte un caractère de contrôle ; le compte
  /// rendu diffère du compte déclaré ; ou deux lignes portent le même triplet.
  /// </exception>
  public static Screening Of(
    ScreeningId id,
    string? database,
    string? dialect,
    ScreeningEngineIdentity engine,
    int declaredColumnCount,
    IEnumerable<ScreenedColumn> columns,
    DateTimeOffset launchedOn)
  {
    ArgumentNullException.ThrowIfNull(engine);
    ArgumentNullException.ThrowIfNull(columns);
    ArgumentOutOfRangeException.ThrowIfNegative(declaredColumnCount);

    var screened = columns.ToList();

    if (screened.Count != declaredColumnCount)
    {
      throw new ArgumentException(
        $"Le relevé déclare {declaredColumnCount} colonnes et le dépistage en rend {screened.Count}. "
        + "Un relevé est entier ou il n'existe pas : un rapport bâti sur une part du relevé se lirait "
        + "comme complet, et les colonnes manquantes seraient précisément celles que personne ne "
        + "relirait jamais.",
        nameof(columns));
    }

    var duplicated = screened
      .GroupBy(column => column.Identity)
      .FirstOrDefault(group => group.Count() > 1);

    if (duplicated is not null)
    {
      throw new ArgumentException(
        $"Le triplet {duplicated.Key} apparaît {duplicated.Count()} fois. Il identifie une colonne à "
        + "l'intérieur d'un rapport : deux lignes qui le partagent parlent de la même colonne, et "
        + "l'un des deux arbitrages écraserait l'autre.",
        nameof(columns));
    }

    return new Screening(
      id,
      ScreeningText.OrThrow(database, "Le nom de la base", MaxDatabaseNameLength, nameof(database)),
      ScreeningText.OrThrow(dialect, "Le dialecte du relevé", MaxDialectLength, nameof(dialect)),
      engine,
      declaredColumnCount,
      screened,
      launchedOn);
  }

  /// <summary>
  /// Le rapport <b>courant</b> parmi ceux qu'on lui donne : le plus récemment lancé. Les autres sont
  /// archivés par le seul fait que celui-ci existe.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>C'est un calcul, et il est total.</b> Aucune écriture n'a lieu pour archiver quoi que ce
  /// soit, et il ne peut pas y avoir deux courants : à égalité de <see cref="LaunchedOn"/>,
  /// l'identité départage. ⚠️ <b>Ce second critère ne prétend rien de plus qu'être total et stable</b>
  /// — l'ordre de <c>Guid</c> en .NET compare ses champs, et non ses octets, si bien que la monotonie
  /// temporelle d'un identifiant de version 7 n'y survit pas. C'est sans importance ici : ce qu'on
  /// exige d'un départage est qu'il désigne toujours le même, jamais qu'il désigne le plus récent —
  /// la récence est déjà tranchée par le premier critère. Sans lui, deux rapports lancés sur le même
  /// tic rendraient le calcul dépendant de l'ordre d'énumération, ce qui est exactement le mode de
  /// panne qu'un état aurait produit.
  /// </para>
  /// <para>
  /// Un rapport archivé reste <b>intégralement lisible</b> — ce sont les mêmes lignes — et
  /// <b>non arbitrable</b> : l'écriture ne s'autorise que sur le courant, ce qui reste un calcul et
  /// n'introduit aucun état. Sans cette restriction, un <c>Operator</c> arbitre le mauvais rapport.
  /// </para>
  /// </remarks>
  /// <returns>Le courant, ou <c>null</c> quand le déploiement n'a encore lancé aucun dépistage.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="screenings"/> est absent.</exception>
  public static Screening? CurrentAmong(IEnumerable<Screening> screenings)
  {
    ArgumentNullException.ThrowIfNull(screenings);

    return screenings
      .OrderByDescending(screening => screening.LaunchedOn)
      .ThenByDescending(screening => screening.Id.Value)
      .FirstOrDefault();
  }

  /// <summary>Ce rapport est-il le courant du déploiement ? Un calcul, jamais une lecture d'état.</summary>
  /// <remarks>
  /// <para>
  /// <b>La comparaison porte sur l'identité, jamais sur la référence.</b> EF Core rematérialise, et
  /// deux instances du même rapport sont le même rapport : un contrôle de référence aurait répondu
  /// « archivé » à un jumeau sorti d'un autre contexte de suivi.
  /// </para>
  /// <para>
  /// ⚠️ <b>Un rapport absent du lot est une programmation fautive, et il lève.</b> Répondre « archivé »
  /// serait le mauvais côté sur lequel se tromper : l'écriture ne s'autorise que sur le courant, et un
  /// faux « archivé » barrerait silencieusement l'arbitrage du seul rapport qu'on ait le droit
  /// d'arbitrer. Répondre « courant » serait pire encore — c'est le mode de panne que le refus d'un
  /// état <c>Archived</c> existe pour empêcher. La question n'a pas de bonne réponse par défaut ;
  /// c'est donc qu'elle est mal posée.
  /// </para>
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="screenings"/> est absent.</exception>
  /// <exception cref="ArgumentException">Ce rapport ne figure pas dans le lot où on lui demande de se situer.</exception>
  public bool IsCurrentAmong(IEnumerable<Screening> screenings)
  {
    ArgumentNullException.ThrowIfNull(screenings);

    var deployment = screenings.ToList();

    if (!deployment.Any(screening => screening.Id == Id))
    {
      throw new ArgumentException(
        $"Le Screening {Id.Value} ne figure pas dans le lot où on lui demande s'il est le courant. "
        + "« Courant » est un calcul sur les rapports d'un déploiement : hors du lot, la question n'a "
        + "pas de réponse, et en inventer une ferait arbitrer le mauvais rapport ou barrerait le bon.",
        nameof(screenings));
    }

    return CurrentAmong(deployment)!.Id == Id;
  }

  /// <summary>
  /// Ce rapport est-il archivé ? La négation du précédent, écrite parce que c'est sous ce mot que
  /// l'<c>Operator</c> le lit — et pour qu'il reste évident qu'aucun champ ne le porte.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="screenings"/> est absent.</exception>
  public bool IsArchivedAmong(IEnumerable<Screening> screenings)
  {
    return !IsCurrentAmong(screenings);
  }

  /// <summary>La ligne que ce triplet désigne, ou <c>null</c> s'il ne désigne rien dans ce rapport.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="identity"/> est absent.</exception>
  public ScreenedColumn? ColumnAt(ColumnIdentity identity)
  {
    ArgumentNullException.ThrowIfNull(identity);

    return _columns.FirstOrDefault(column => column.Identity == identity);
  }

  /// <summary>Les colonnes d'une table, dans l'ordre du schéma. C'est l'unité de travail de l'arbitrage.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="table"/> est absent.</exception>
  public IReadOnlyList<ScreenedColumn> ColumnsOf(TableIdentity table)
  {
    ArgumentNullException.ThrowIfNull(table);

    return
    [
      .. _columns
        .Where(column => column.Identity.TableIdentity == table)
        .OrderBy(column => column.Listed.Position),
    ];
  }

  /// <summary>
  /// Porte l'issue qu'un humain vient de rendre sur une colonne, <b>signée et datée</b>. Il n'existe
  /// aucun autre chemin d'écriture, et celui-ci ne sait pas poser un état sans signature.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Ce qui manque ici, écrit plutôt que découvert un jour de panne : « un archivé n'est pas
  /// arbitrable » n'est pas tenu par l'agrégat, et ne peut pas l'être.</b> La restriction est réelle
  /// — sans elle un <c>Operator</c> arbitre le mauvais rapport, ce qui est le mode de panne invoqué
  /// pour refuser un état <c>Archived</c> — mais « courant » est un calcul <b>sur le lot</b> des
  /// rapports d'un déploiement, et un agrégat ne voit pas ses frères. La lui faire voir demanderait
  /// de passer le déploiement entier à chaque arbitrage d'une colonne, c'est-à-dire de charger cinq
  /// mille lignes pour en écrire une. <b>Elle se pose donc au geste</b>, qui lit le courant avant
  /// d'écrire — voir <see cref="IsCurrentAmong"/>, qui existe pour lui et lève plutôt que de deviner.
  /// </remarks>
  /// <param name="identity">Le triplet de la colonne arbitrée.</param>
  /// <param name="ruling">Retenue, ou écartée. Jamais <see cref="ScreenedColumnState.Awaiting"/>.</param>
  /// <param name="signedBy">Le nom saisi par celui qui tranche. Non authentifié, et non facultatif.</param>
  /// <param name="signedOn">L'instant où il a tranché.</param>
  /// <returns>La ligne arbitrée, ou <c>null</c> si ce triplet ne désigne rien ici.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="identity"/> ou <paramref name="ruling"/> est absent.</exception>
  /// <exception cref="ArgumentException">L'état n'est pas une issue, ou la signature est vide, démesurée, ou porte un caractère de contrôle.</exception>
  public ScreenedColumn? Arbitrate(
    ColumnIdentity identity,
    ScreenedColumnState ruling,
    string? signedBy,
    DateTimeOffset signedOn)
  {
    ArgumentNullException.ThrowIfNull(ruling);

    var column = ColumnAt(identity);

    column?.Arbitrate(ruling, signedBy, signedOn);

    return column;
  }

  /// <summary>
  /// Le <b>geste de lot</b> : pose d'un seul coup, sur les colonnes d'<b>une</b> table où rien n'a été
  /// vu et que personne n'a tranchées, <b>n arbitrages individuels</b> — un par colonne, chacun signé
  /// du nom saisi et daté par le service.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Il existe pour qu'un rapport de cinq mille colonnes reste tenable</b> : un écran intenable
  /// rétablit l'<c>Omission silencieuse</c> par l'épuisement, ce qui est le mode de panne le plus
  /// probable de ce contexte — personne n'abandonne en déclarant qu'il abandonne.
  /// </para>
  /// <para>
  /// ⚠️ <b>Il est borné à la table nommée, et à elle seule.</b> Un lot qui porterait sur le rapport
  /// entier trancherait d'un clic ce que l'<c>Operator</c> n'a pas sous les yeux, et la table est
  /// l'unité de travail précisément parce que c'est l'unité qu'on lit.
  /// </para>
  /// <para>
  /// ⚠️ <b>Il n'atteint aucune colonne signalée</b>, ni aucune colonne déjà tranchée — voir
  /// <see cref="ScreenedColumn.IsWithinReachOfABatchGesture"/>, où la règle est écrite une fois.
  /// </para>
  /// <para>
  /// ⚠️ <b>Ce ne sont pas des états de lot : ce sont n arbitrages.</b> Chaque ligne porte sa propre
  /// signature et sa propre date, comme si l'<c>Operator</c> les avait posées une par une — un état
  /// de lot aurait été une quatrième valeur d'arbitrage que rien du rapport ne rend, et le premier
  /// réarbitrage individuel l'aurait fait mentir.
  /// </para>
  /// <para>
  /// ⚠️ <b>Un refus n'écrit pas une seule ligne.</b> La signature et l'issue se vérifient avant que
  /// la première ne bouge : un lot à moitié posé laisserait l'<c>Operator</c> devant un refus sans
  /// savoir où le geste s'est arrêté, sur une table de plusieurs dizaines de colonnes.
  /// </para>
  /// <para>
  /// <b>Comme l'arbitrage à l'unité, il ne sait pas qu'un archivé n'est pas arbitrable</b> : un
  /// agrégat ne voit pas ses frères, et la restriction se pose au geste, par la lecture — voir
  /// <see cref="Arbitrate"/>.
  /// </para>
  /// </remarks>
  /// <param name="table">La table ouverte. Le lot ne sort jamais d'elle.</param>
  /// <param name="ruling">L'issue portée sur chacune. Retenue, ou écartée. Jamais <see cref="ScreenedColumnState.Awaiting"/>.</param>
  /// <param name="signedBy">Le nom saisi par celui qui tranche. Non authentifié, et non facultatif.</param>
  /// <param name="signedOn">L'instant où il a tranché.</param>
  /// <returns>Les colonnes que le lot a tranchées, dans l'ordre du relevé — vide s'il n'en atteignait aucune.</returns>
  /// <exception cref="ArgumentNullException"><paramref name="table"/> ou <paramref name="ruling"/> est absent.</exception>
  /// <exception cref="ArgumentException">L'état n'est pas une issue, ou la signature est vide, démesurée, ou porte un caractère de contrôle.</exception>
  public IReadOnlyList<ScreenedColumn> ArbitrateInBatch(
    TableIdentity table,
    ScreenedColumnState ruling,
    string? signedBy,
    DateTimeOffset signedOn)
  {
    ArgumentNullException.ThrowIfNull(table);
    ArgumentNullException.ThrowIfNull(ruling);

    // ⚠️ Le refus se lève ICI, avant la moindre écriture, et l'arbitrage fabriqué est jeté : chaque
    // ligne recevra le sien. Un arbitrage partagé entre n lignes aurait fait de n actes un seul
    // objet — que la persistance refuse de poser sur n lignes possédées, et que le premier
    // réarbitrage individuel aurait rendu faux partout ailleurs.
    _ = Arbitration.Rendered(ruling, signedBy, signedOn);

    var reached = ColumnsOf(table)
      .Where(column => column.IsWithinReachOfABatchGesture)
      .ToList();

    foreach (var column in reached)
    {
      column.Arbitrate(ruling, signedBy, signedOn);
    }

    return reached;
  }
}
