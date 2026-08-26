namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// La <c>Cartographie</c> : le <see cref="Screening"/> <b>lu à travers les arbitrages de
/// l'<c>Operator</c></b> — les mêmes lignes, chacune portant ce qu'un humain en a dit, avec sa date.
/// C'est ce qui s'exporte, et le seul artefact de ce contexte qui sorte jamais du service.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Elle porte <i>toutes</i> les lignes</b> — <c>Retained</c>, <c>SetAside</c> et
/// <c>Awaiting</c>, signalées comme non signalées. Une cartographie réduite aux <c>Retained</c>
/// serait le filtre que l'<c>Omission relue</c> interdit, déplacé du rapport vers l'export : elle se
/// lirait comme la liste <b>complète</b> des données personnelles du client, ce qu'aucun artefact
/// d'ici ne peut être.
/// </para>
/// <para>
/// ⚠️ <b>C'est un calcul, jamais un objet enregistré.</b> Rien ne se persiste qui s'appelle une
/// cartographie : on lit les <see cref="ScreenedColumn"/> et leurs arbitrages. Même mécanique que
/// « courant », que l'avancement et que la sensibilité — un objet posé à côté de sa source serait
/// une seconde vérité sur les mêmes arbitrages, et le premier réarbitrage la rendrait fausse sans
/// que rien ne le signale.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne porte aucune valeur lue.</b> Un <see cref="ColumnPreview"/> meurt avec la session
/// d'arbitrage et n'atteint jamais l'export : il n'est atteignable ni depuis un
/// <see cref="Screening"/>, ni depuis une <see cref="ScreenedColumn"/>, et rien ici ne va le
/// chercher ailleurs. Aucun compte de valeurs conformes ne sort non plus — un tableur ferait
/// comparer deux nombres qui ne se comparent pas.
/// </para>
/// <para>
/// ⚠️ <b>Et elle ne porte <i>aucune</i> <see cref="IncompletenessClause"/>, sous aucune forme.</b>
/// C'est la seule réponse de ce contexte qui rende un <see cref="Screening"/> sans sa clause, et le
/// renversement est assumé : <b>l'<c>Operator</c> tranche chaque ligne et choisit d'expédier le
/// fichier ; ce qu'il en dit au destinataire lui appartient.</b> Le service ne parle pas par-dessus
/// son épaule dans un document qu'il n'expédie pas. Le prix est nommé — un fichier ainsi titré peut
/// se lire comme une liste complète —, et ce qui le borne est que la clause reste sur <b>tous</b>
/// les écrans et que l'<c>Omission relue</c> est tenue dans le fichier lui-même, qui porte toutes
/// les lignes. ⚠️ <b>Quatre mécanismes de rattrapage ont été construits puis écartés</b> — bloc
/// avant l'en-tête, bloc après les données, colonne répétée, ZIP — et un cinquième, une colonne de
/// provenance par ligne, examiné et écarté : qui viendra en reproposer un lira ceci d'abord.
/// </para>
/// </remarks>
/// <param name="Id">L'identité du rapport dont elle est tirée.</param>
/// <param name="Database">
/// Le nom de base du relevé. ⚠️ <b>Il ne porte jamais un chemin</b> — voir
/// <see cref="Screening.Database"/> : ce champ quitte le service, et le dossier parent est l'endroit
/// où l'on écrit le nom du client.
/// </param>
/// <param name="Dialect">Le SGBD dont le relevé se déclarait.</param>
/// <param name="Engine">
/// Qui a détecté, et dans quelle version. ⚠️ <b>Un rapport a <i>un</i> moteur</b> : il vit en tête et
/// n'est jamais répété sur la ligne, sans quoi on laisserait croire qu'il pourrait varier d'une
/// colonne à l'autre.
/// </param>
/// <param name="LaunchedOn">Quand la détection a été lancée.</param>
/// <param name="ExportedOn">Quand l'<c>Operator</c> a demandé le fichier.</param>
/// <param name="Counts">Les comptes du rapport, recalculés à cet instant.</param>
/// <param name="Columns">Une ligne par colonne du rapport, sans exception.</param>
public sealed record PersonalDataMap(
  ScreeningId Id,
  string Database,
  string Dialect,
  ScreeningEngineIdentity Engine,
  DateTimeOffset LaunchedOn,
  DateTimeOffset ExportedOn,
  ScreeningCounts Counts,
  IReadOnlyList<MappedColumn> Columns)
{
  /// <summary>
  /// La cartographie d'un rapport <b>chargé avec toutes ses colonnes</b>, à l'instant où
  /// l'<c>Operator</c> la demande.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>L'ordre est celui des écrans</b> : les tables retriées par le service — schéma puis table,
  /// dans l'ordre des octets —, et à l'intérieur de chacune l'ordre du schéma, qui porte le
  /// voisinage de <c>adr_l1</c>, <c>adr_l2</c>, <c>cp</c>, <c>ville</c>. Un autre ordre aurait rendu
  /// incomparables le fichier et l'écran d'où il sort, sur un travail qu'on reprend pendant trois
  /// jours.
  /// </remarks>
  /// <param name="screening">Le rapport, chargé avec ses colonnes.</param>
  /// <param name="exportedOn">L'instant où le fichier a été demandé.</param>
  /// <exception cref="ArgumentNullException"><paramref name="screening"/> est absent.</exception>
  /// <exception cref="InvalidOperationException"><paramref name="screening"/> a été chargé sans ses colonnes.</exception>
  public static PersonalDataMap Of(Screening screening, DateTimeOffset exportedOn)
  {
    ArgumentNullException.ThrowIfNull(screening);

    return new PersonalDataMap(
      screening.Id,
      screening.Database,
      screening.Dialect,
      screening.Engine,
      screening.LaunchedOn,
      exportedOn,
      // Les comptes exigent un rapport entier, et lèvent sinon : une cartographie bâtie sur un
      // rapport chargé sans ses colonnes aurait porté des comptes sincères et faux à côté d'une
      // liste vide, dans un fichier qui promet de porter toutes les lignes.
      ScreeningCounts.Of(screening),
      [
        .. screening.Tables
          .SelectMany(screening.ColumnsOf)
          .Select(MappedColumn.Of),
      ]);
  }

  /// <summary>
  /// L'<b>arbitrage inachevé</b>, en une phrase qui le compte — ou <c>null</c> quand plus rien
  /// n'attend.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Elle est absente, et non à zéro, quand le compte est nul.</b> « 0 colonne attend encore »
  /// est une phrase qu'on lit comme une réserve alors qu'elle n'en est pas une, et une cartographie
  /// entièrement tranchée n'a rien à déclarer.
  /// </para>
  /// <para>
  /// ⚠️ <b>Elle ne fusionne pas avec la <see cref="IncompletenessClause"/>, et n'en est pas un
  /// reste.</b> C'est une incomplétude <b>du travail humain</b>, d'une autre nature que celle de la
  /// méthode : la première se répare en repassant, la seconde ne se répare pas. Les confondre aurait
  /// fait croire qu'en finissant d'arbitrer on lève la seconde.
  /// </para>
  /// <para>
  /// ⚠️ <b>Elle compte les colonnes <i>en attente</i>, toutes confondues</b>, et non les seules non
  /// signalées non relues du verrou de l'écran : ce que le destinataire du fichier doit savoir est
  /// combien de lignes personne n'a encore tranchées, quelle qu'en soit la raison.
  /// </para>
  /// </remarks>
  public string? UnfinishedArbitration => Counts.Awaiting == 0
    ? null
    : Counts.Awaiting == 1
      ? "1 colonne de cette cartographie n'a pas encore été arbitrée."
      : $"{Counts.Awaiting} colonnes de cette cartographie n'ont pas encore été arbitrées.";
}

/// <summary>
/// Une ligne de la <c>Cartographie</c> : les <b>seize champs</b> qu'un destinataire reçoit d'une
/// colonne, en trois blocs — d'où ça vient, ce que la machine a dit, ce que l'humain a dit.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Les deux paires nom canonique / libellé français sont gardées ensemble</b>, et c'est le seul
/// endroit du produit à le faire : l'anglais se traite, le français se lit, et n'en garder qu'un
/// force à choisir entre deux destinataires qui existent tous les deux.
/// </para>
/// <para>
/// ⚠️ <b><see cref="State"/> n'a pas sa paire, et c'est un coût assumé.</b> La spec ne nomme qu'un
/// champ pour ce que l'humain a dit ; il porte donc le <b>libellé français</b>, comme les
/// <c>oui</c>/<c>non</c> de <see cref="IsNullable"/> et de <see cref="IsFlagged"/> à côté de lui.
/// Le fichier reste lisible d'un bout à l'autre plutôt que français par endroits.
/// </para>
/// <para>
/// ⚠️ <b>Aucun champ ne porte une valeur lue</b>, et aucun ne compte les valeurs conformes. Ce qui
/// se rapproche le plus d'une valeur ici — les deux commentaires de schéma — vient du
/// <b>catalogue</b>, jamais des données : ils partent parce qu'ils portent souvent le seul indice
/// dont l'<c>Operator</c> disposait pour trancher, et le destinataire doit disposer du même.
/// </para>
/// </remarks>
/// <param name="Schema">Le schéma dont le relevé dit que la table vient.</param>
/// <param name="Table">La table, telle que le relevé la nomme.</param>
/// <param name="Column">La colonne, telle que le relevé la nomme.</param>
/// <param name="Position">Le rang dans le schéma, tel que le relevé le rend.</param>
/// <param name="DataType">Le type déclaré, ou <c>null</c>.</param>
/// <param name="IsNullable">La nullabilité, ou <c>null</c> si le relevé ne la porte pas.</param>
/// <param name="ColumnComment">Le commentaire de colonne, ou <c>null</c> — régime majoritaire.</param>
/// <param name="TableComment">Le commentaire de la table, ou <c>null</c>.</param>
/// <param name="IsFlagged">La détection a-t-elle signalé quelque chose ici ?</param>
/// <param name="Category">
/// Le nom canonique de ce que la détection a reconnu. ⚠️ <b>Toujours une valeur</b> : l'absence de
/// signalement en est une, <c>Unflagged</c>, et l'écrire est ce qui empêche une case vide de se lire
/// comme une ligne oubliée.
/// </param>
/// <param name="CategoryLabel">Son libellé français.</param>
/// <param name="Strength">Le nom canonique du degré de la règle qui a déclenché, ou <c>null</c>.</param>
/// <param name="StrengthLabel">Son libellé français, ou <c>null</c>.</param>
/// <param name="Reason">Le motif en prose française, ou <c>null</c> quand rien n'a été signalé.</param>
/// <param name="State">
/// Ce que l'humain a dit, en toutes lettres : <c>retenue</c>, <c>écartée</c>, ou <c>à arbitrer</c>
/// tant que personne n'a tranché.
/// </param>
/// <param name="RenderedOn">
/// Quand il l'a dit, ou <c>null</c> tant que personne n'a tranché. ⚠️ <b>Le nom du champ est
/// <c>rendu_le</c> / <c>renduLe</c></b>, en miroir de <c>Arbitration.RenderedOn</c> que l'ADR-0014 a
/// fixé : <c>signataire</c> et <c>signe_le</c> n'existent nulle part, ce contexte n'enregistrant pas
/// qui a arbitré.
/// </param>
public sealed record MappedColumn(
  string Schema,
  string Table,
  string Column,
  int Position,
  string? DataType,
  bool? IsNullable,
  string? ColumnComment,
  string? TableComment,
  bool IsFlagged,
  string Category,
  string CategoryLabel,
  string? Strength,
  string? StrengthLabel,
  string? Reason,
  string State,
  DateTimeOffset? RenderedOn)
{
  /// <summary>Une ligne du rapport, mise à plat pour le destinataire du fichier.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="column"/> est absent.</exception>
  internal static MappedColumn Of(ScreenedColumn column)
  {
    ArgumentNullException.ThrowIfNull(column);

    var listed = column.Listed;

    return new MappedColumn(
      listed.Identity.Schema,
      listed.Identity.Table,
      listed.Identity.Column,
      listed.Position,
      listed.DataType,
      listed.IsNullable,
      listed.ColumnComment,
      listed.TableComment,
      column.IsFlagged,
      column.Category.Name,
      column.Category.FrenchLabel,
      column.Strength?.Name,
      column.Strength?.FrenchLabel,
      column.Reason,
      column.State.FrenchLabel,
      column.Arbitration?.RenderedOn);
  }
}
