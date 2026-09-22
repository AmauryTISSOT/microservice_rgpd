using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ReadCurrentScreening;

/// <summary>
/// Le <b>sommaire</b> du rapport de détection courant : son entête, ses tables retriées, ses
/// comptes et
/// son verrou.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il ne filtre rien, parce qu'il ne rend aucune colonne.</b> C'est la vue d'où l'on choisit
/// une table, et l'interdit de l'<c>Omission relue</c> porte sur ce qu'on montre <em>d'une table
/// ouverte</em> : masquer les <c>Unflagged</c> là serait fatal. Ici, chaque table est nommée avec
/// <b>toutes</b> ses colonnes comptées, signalées et non signalées ensemble — et le verrou dit
/// combien restent à relire.
/// </para>
/// <para>
/// <b>Tout ce qu'il porte est calculé à l'instant du rendu.</b> Aucun de ces nombres n'est persisté,
/// et aucun n'aurait de quoi se mettre à jour s'il l'était.
/// </para>
/// </remarks>
/// <param name="Id">L'identité du rapport, celle sous laquelle on ouvrira ses tables.</param>
/// <param name="Database">
/// Le nom de base que le relevé rapportait. ⚠️ <b>Un repère pour l'humain</b> qui relit son rapport
/// trois jours plus tard — jamais une identité sur laquelle bâtir une comparaison.
/// </param>
/// <param name="Dialect">
/// Le SGBD dont le relevé se déclarait. Il est rendu parce que <b>sans lui, « cette colonne n'a pas
/// de commentaire » et « ce SGBD n'en rend jamais » se lisent pareil</b>.
/// </param>
/// <param name="Engine">
/// Qui a détecté, et dans quelle version. ⚠️ Elle ne sert qu'à l'humain qui relit ou qui compare
/// deux rapports : le domaine ne l'interprète jamais.
/// </param>
/// <param name="LaunchedOn">Quand la détection a été lancée — le seul fait dont dépend « courant ».</param>
/// <param name="Tables">Les tables du relevé, retriées par le service.</param>
/// <param name="Tally">Les comptes du rapport.</param>
/// <param name="Lock">Le verrou d'inachèvement, recalculé à ce rendu.</param>
public sealed record ScreeningSummary(
  ScreeningId Id,
  string Database,
  string Dialect,
  ScreeningEngineIdentity Engine,
  DateTimeOffset LaunchedOn,
  IReadOnlyList<SummarisedTable> Tables,
  ScreeningTally Tally,
  UnfinishedScreening Lock)
{
  /// <summary>Le sommaire d'un rapport, à l'instant où on le regarde.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="screening"/> est absent.</exception>
  internal static ScreeningSummary Of(Screening screening)
  {
    ArgumentNullException.ThrowIfNull(screening);

    // ⚠️ Un seul regroupement, et ce n'est pas de l'optimisation prématurée. Interroger le rapport
    // table par table le rebalaie en entier à chaque table : au pire cas que le contrat de format
    // autorise — 20 000 colonnes, ~1 540 tables —, cela fait des dizaines de millions de
    // comparaisons À CHAQUE RENDU de l'écran, dont autant d'allocations, car TableIdentity est
    // reconstruite à chaque lecture. Le budget de 10 s couvre le dépôt ; ce chemin-ci est celui que
    // l'Operator reparcourt à chaque rafraîchissement, et rien ne le chiffrait.
    var columnsByTable = screening.Columns
      .GroupBy(column => column.Identity.TableIdentity)
      .ToDictionary(group => group.Key, group => group.ToArray());

    // Un seul balayage pour les neuf comptes, plutôt qu'un par lecteur : ils sont le même relevé vu
    // deux fois, et les faire diverger n'aurait demandé qu'une distraction.
    var counts = ScreeningCounts.Of(screening);

    return new ScreeningSummary(
      screening.Id,
      screening.Database,
      screening.Dialect,
      screening.Engine,
      screening.LaunchedOn,
      // ⚠️ L'ORDRE reste celui de l'agrégat : c'est lui qui porte la règle du retri par le service.
      // Retrier ici aurait écrit la même règle à deux endroits, et l'un des deux aurait dérivé.
      [.. screening.Tables.Select(table => SummarisedTable.Of(table, columnsByTable[table]))],
      ScreeningTally.Of(counts),
      UnfinishedScreening.Of(counts));
  }

  /// <summary>L'avancement de l'arbitrage sur le rapport entier, en comptes.</summary>
  public ArbitrationProgress Progress => ArbitrationProgress.Of(Tally, Lock);

  /// <summary>
  /// La table à ouvrir ensuite : la première, <b>après</b> <paramref name="after"/> dans l'ordre du
  /// rapport, où une colonne attend encore — voir <see cref="ArbitrationOrder"/>.
  /// </summary>
  public SummarisedTable? NextToArbitrate(TableIdentity? after = null)
  {
    return ArbitrationOrder.NextAfter(Tables, after);
  }
}

/// <summary>
/// Une table du relevé, telle que le sommaire la nomme : ce qu'elle porte, et ce qu'il y reste à
/// faire.
/// </summary>
/// <param name="Identity">Le schéma et la table.</param>
/// <param name="ColumnCount">Combien de colonnes elle porte, <b>toutes</b>.</param>
/// <param name="FlaggedCount">Combien la détection en a signalées.</param>
/// <param name="AwaitingCount">Combien attendent encore qu'un humain les tranche.</param>
/// <param name="FlaggedAwaitingCount">
/// Combien, parmi celles qui attendent, la détection avait signalées. ⚠️ <b>La liste des tables le
/// dit à part</b>, parce que ces colonnes-là ne se tranchent qu'une par une : une table qui n'attend
/// plus que des colonnes où rien n'a été vu se finit d'un geste de lot, une table où attend une
/// suspicion ne se finit jamais ainsi.
/// </param>
public sealed record SummarisedTable(
  TableIdentity Identity,
  int ColumnCount,
  int FlaggedCount,
  int AwaitingCount,
  int FlaggedAwaitingCount)
{
  /// <summary>Combien de ses colonnes un humain a déjà tranchées, retenues et écartées ensemble.</summary>
  public int SettledCount => ColumnCount - AwaitingCount;

  /// <summary>Combien de ses colonnes où rien n'a été vu attendent encore d'être relues.</summary>
  public int UnflaggedAwaitingCount => AwaitingCount - FlaggedAwaitingCount;

  /// <summary>Plus rien n'y attend : chacune de ses colonnes a été tranchée.</summary>
  public bool IsSettled => AwaitingCount == 0;

  internal static SummarisedTable Of(TableIdentity table, IReadOnlyList<ScreenedColumn> columns)
  {
    return new SummarisedTable(
      table,
      columns.Count,
      columns.Count(column => column.IsFlagged),
      columns.Count(column => column.AwaitsAnArbitration),
      columns.Count(column => column.IsFlagged && column.AwaitsAnArbitration));
  }
}
