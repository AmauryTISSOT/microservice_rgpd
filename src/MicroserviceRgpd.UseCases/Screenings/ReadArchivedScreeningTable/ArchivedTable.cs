using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ReadArchivedScreeningTable;

/// <summary>
/// Une table d'un rapport archivé telle qu'on la relit : <b>toutes</b> ses colonnes dans l'ordre du
/// relevé, ce que la détection avait dit de chacune, ce qu'un humain en avait tranché — et
/// <b>aucun geste</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est un type distinct de <c>ScreenedTable</c>, et la différence est ce qu'il ne porte
/// pas</b> : ni le verrou d'inachèvement, ni le compte de ce qu'un geste de lot atteindrait. Ces
/// deux-là ne sont pas des informations, ce sont des <b>invitations à agir</b>, et sur un archivé
/// elles inviteraient à un geste qui n'existe pas.
/// </para>
/// <para>
/// <b>Les arbitrages, eux, sont là, entiers.</b> Qui a tranché et quand vivent sur la ligne : c'est
/// tout ce qu'un archivé a à dire, et c'est ce qu'on vient y lire trois mois plus tard.
/// </para>
/// </remarks>
/// <param name="Screening">Le rapport archivé dont cette table fait partie.</param>
/// <param name="Database">Le nom de base que le relevé rapportait — un repère, jamais une identité.</param>
/// <param name="Dialect">
/// Le SGBD dont le relevé se déclarait. Sans lui, « cette colonne n'a pas de commentaire » et « ce
/// SGBD n'en rend jamais » se lisent pareil.
/// </param>
/// <param name="LaunchedOn">Quand ce rapport de détection a été lancé.</param>
/// <param name="Identity">Le schéma et la table qu'on lit.</param>
/// <param name="Comment">Le commentaire de la table, ou <c>null</c>.</param>
/// <param name="Columns">Ses colonnes, toutes, dans l'ordre du relevé.</param>
/// <param name="Tally">Les comptes du rapport entier.</param>
public sealed record ArchivedTable(
  ScreeningId Screening,
  string Database,
  string Dialect,
  DateTimeOffset LaunchedOn,
  TableIdentity Identity,
  string? Comment,
  IReadOnlyList<ScreenedColumn> Columns,
  ScreeningTally Tally)
{
  /// <summary>Combien de colonnes cette table porte, <b>toutes</b>.</summary>
  public int ColumnCountInThisTable => Columns.Count;

  /// <summary>Combien la détection en avait signalées, <b>dans cette table</b>.</summary>
  public int FlaggedCountInThisTable => Columns.Count(column => column.IsFlagged);

  /// <summary>
  /// Combien de colonnes de cette table <b>n'ont jamais été tranchées</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le mot n'est pas « en attente », et le choix est le fond de cette tranche.</b> Sur le
  /// courant, une colonne en attente est du travail à faire ; ici, c'est du travail qui ne se fera
  /// plus — un re-dépôt ne fusionne aucun arbitrage, et ce compte est le prix déclaré de ce refus.
  /// </remarks>
  public int NeverArbitratedInThisTable => Columns.Count(column => column.AwaitsAnArbitration);

  /// <summary>La table telle qu'on la relit, à l'instant où on la regarde.</summary>
  /// <remarks>
  /// <b>Le commentaire de table est lu sur la première colonne</b>, parce que le relevé le recopie
  /// sur chacune : la table n'a pas de ligne à elle dont on le tirerait.
  /// </remarks>
  /// <param name="screening">L'entête du rapport archivé, chargé sans ses colonnes.</param>
  /// <param name="table">Le schéma et la table.</param>
  /// <param name="columns">Ses colonnes, déjà rendues dans l'ordre du relevé.</param>
  /// <param name="counts">Les comptes du rapport entier.</param>
  /// <exception cref="ArgumentNullException">Un des arguments est absent.</exception>
  internal static ArchivedTable Of(
    Screening screening,
    TableIdentity table,
    IReadOnlyList<ScreenedColumn> columns,
    ScreeningCounts counts)
  {
    ArgumentNullException.ThrowIfNull(screening);
    ArgumentNullException.ThrowIfNull(table);
    ArgumentNullException.ThrowIfNull(columns);
    ArgumentNullException.ThrowIfNull(counts);

    return new ArchivedTable(
      screening.Id,
      screening.Database,
      screening.Dialect,
      screening.LaunchedOn,
      table,
      columns.Count == 0 ? null : columns[0].Listed.TableComment,
      columns,
      ScreeningTally.Of(counts));
  }
}
