using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ReadScreeningTable;

/// <summary>
/// Une table du rapport telle que l'<c>Operator</c> la lit : <b>toutes</b> ses colonnes dans l'ordre
/// du relevé, ce que le dépistage a dit de chacune, et le verrou et les comptes du rapport entier.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce sont les colonnes de la table, sans exception et sans filtre.</b> Les <c>Unflagged</c>
/// <b>sont</b> l'écran — 92,5 % du contenu réel d'un relevé — et les masquer, même derrière un filtre
/// que l'<c>Operator</c> pourrait rouvrir, rétablit l'<c>Omission silencieuse</c> sans qu'aucune
/// ligne de doctrine n'ait été modifiée.
/// </para>
/// <para>
/// ⚠️ <b>Le verrou et les comptes portent sur le rapport entier, jamais sur la table ouverte.</b>
/// C'est ici qu'ils comptent le plus : une table relue jusqu'au bout est l'instant précis où l'on
/// croit avoir fini.
/// </para>
/// </remarks>
/// <param name="Screening">Le rapport dont cette table fait partie.</param>
/// <param name="Database">Le nom de base que le relevé rapportait — un repère, jamais une identité.</param>
/// <param name="Dialect">
/// Le SGBD dont le relevé se déclarait. Sans lui, « cette colonne n'a pas de commentaire » et « ce
/// SGBD n'en rend jamais » se lisent pareil.
/// </param>
/// <param name="Identity">Le schéma et la table qu'on lit.</param>
/// <param name="Comment">
/// Le commentaire de la table, ou <c>null</c>. <b>C'est lui qui fait de la table l'unité de
/// travail</b> : il éclaire toutes ses colonnes à la fois.
/// </param>
/// <param name="Columns">Ses colonnes, toutes, dans l'ordre du relevé.</param>
/// <param name="Tally">Les comptes du rapport entier.</param>
/// <param name="Lock">Le verrou d'inachèvement du rapport entier, recalculé à ce rendu.</param>
public sealed record ScreenedTable(
  ScreeningId Screening,
  string Database,
  string Dialect,
  TableIdentity Identity,
  string? Comment,
  IReadOnlyList<ScreenedColumn> Columns,
  ScreeningTally Tally,
  UnfinishedScreening Lock)
{
  /// <summary>Combien de colonnes cette table porte, <b>toutes</b>.</summary>
  /// <remarks>
  /// ⚠️ <b>Les trois comptes qui suivent sont ceux de CETTE TABLE, et <see cref="Tally"/> porte les
  /// mêmes mots pour le rapport ENTIER.</b> Leur nom dit lequel des deux dénominateurs il compte —
  /// « douze signalées » sous deux dénominateurs différents dans le même écran est la façon la plus
  /// simple de faire lire une table relue comme un rapport fini.
  /// </remarks>
  public int ColumnCountInThisTable => Columns.Count;

  /// <summary>Combien le dépistage en a signalées, <b>dans cette table</b>.</summary>
  public int FlaggedCountInThisTable => Columns.Count(column => column.IsFlagged);

  /// <summary>Combien attendent encore qu'un humain les tranche, <b>dans cette table</b>.</summary>
  public int AwaitingCountInThisTable => Columns.Count(column => column.AwaitsAnArbitration);

  /// <summary>
  /// La table telle qu'on la rend, à l'instant où on la regarde.
  /// </summary>
  /// <remarks>
  /// <b>Le commentaire de table est lu sur la première colonne</b>, parce que le relevé le recopie
  /// sur chacune : la table n'a pas de ligne à elle dont on le tirerait.
  /// </remarks>
  /// <param name="screening">L'entête du rapport, chargé sans ses colonnes.</param>
  /// <param name="table">Le schéma et la table.</param>
  /// <param name="columns">Ses colonnes, déjà rendues dans l'ordre du relevé.</param>
  /// <param name="counts">Les comptes du rapport entier.</param>
  /// <exception cref="ArgumentNullException">Un des arguments est absent.</exception>
  internal static ScreenedTable Of(
    Screening screening,
    TableIdentity table,
    IReadOnlyList<ScreenedColumn> columns,
    ScreeningCounts counts)
  {
    ArgumentNullException.ThrowIfNull(screening);
    ArgumentNullException.ThrowIfNull(table);
    ArgumentNullException.ThrowIfNull(columns);
    ArgumentNullException.ThrowIfNull(counts);

    return new ScreenedTable(
      screening.Id,
      screening.Database,
      screening.Dialect,
      table,
      columns.Count == 0 ? null : columns[0].Listed.TableComment,
      columns,
      ScreeningTally.Of(counts),
      UnfinishedScreening.Of(counts));
  }
}
