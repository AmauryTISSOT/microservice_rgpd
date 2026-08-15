using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UseCases.Screenings.ReadCurrentScreening;

namespace MicroserviceRgpd.UseCases.Screenings.ReadArchivedScreening;

/// <summary>
/// Le <b>sommaire</b> d'un rapport de détection archivé : son entête, ses tables retriées et ses
/// comptes — <b>sans verrou et sans geste</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est un type distinct de <see cref="ScreeningSummary"/>, et la différence est le point de
/// cette tranche.</b> « Non arbitrable » n'est pas une condition posée dans un écran : c'est un type
/// qui ne porte pas de quoi arbitrer. Un drapeau <c>EstArchivé</c> sur le sommaire du courant aurait
/// mis la règle dans un <c>if</c> de rendu, où le premier écran distrait l'aurait perdue.
/// </para>
/// <para>
/// ⚠️ <b>Il ne porte pas le verrou d'inachèvement</b>, et c'est délibéré. « Ce rapport de détection
/// est
/// inachevé — relisez les colonnes où rien n'a été vu » est un appel à un geste qui n'existe plus
/// ici : le verrou pousse à finir un travail, et sur un archivé il n'y a plus rien à finir. Ce qui
/// reste vrai — combien de colonnes n'ont jamais été tranchées — est dans les comptes, sous le mot
/// qui convient à un rapport rangé.
/// </para>
/// <para>
/// <b>Ses comptes sont ceux d'alors, recalculés maintenant.</b> Rien n'est figé à l'archivage,
/// puisque l'archivage n'écrit rien : les arbitrages posés avant le re-dépôt sont là, intacts, et
/// se recomptent à chaque rendu.
/// </para>
/// </remarks>
/// <param name="Id">L'identité du rapport, celle sous laquelle on ouvre ses tables ou on le supprime.</param>
/// <param name="Database">Le nom de base que le relevé rapportait — un repère, jamais une identité.</param>
/// <param name="Dialect">Le SGBD dont le relevé se déclarait.</param>
/// <param name="Engine">
/// Qui a détecté, et dans quelle version. ⚠️ <b>C'est ici qu'elle sert le plus</b> : elle dit à
/// l'humain qui compare deux rapports pourquoi le plus récent ne dit pas la même chose.
/// </param>
/// <param name="LaunchedOn">Quand ce rapport de détection a été lancé.</param>
/// <param name="Tables">Les tables du relevé, retriées par le service.</param>
/// <param name="Tally">Les comptes du rapport, tels qu'ils étaient quand il a été rangé.</param>
public sealed record ArchivedScreeningReport(
  ScreeningId Id,
  string Database,
  string Dialect,
  ScreeningEngineIdentity Engine,
  DateTimeOffset LaunchedOn,
  IReadOnlyList<SummarisedTable> Tables,
  ScreeningTally Tally)
{
  /// <summary>Le sommaire d'un rapport archivé, <b>chargé avec toutes ses colonnes</b>.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="screening"/> est absent.</exception>
  /// <exception cref="InvalidOperationException">
  /// <paramref name="screening"/> a été chargé sans ses colonnes : ses comptes seraient sincères et
  /// faux.
  /// </exception>
  internal static ArchivedScreeningReport Of(Screening screening)
  {
    ArgumentNullException.ThrowIfNull(screening);

    // Un seul regroupement, comme sur le sommaire du courant : interroger le rapport table par table
    // le rebalaie en entier à chaque table, et un archivé a la même taille que ce qu'il était.
    var columnsByTable = screening.Columns
      .GroupBy(column => column.Identity.TableIdentity)
      .ToDictionary(group => group.Key, group => group.ToArray());

    var counts = ScreeningCounts.Of(screening);

    return new ArchivedScreeningReport(
      screening.Id,
      screening.Database,
      screening.Dialect,
      screening.Engine,
      screening.LaunchedOn,
      // ⚠️ L'ORDRE reste celui de l'agrégat, qui porte la règle du retri par le service : un archivé
      // se relit dans le même ordre que le courant, sans quoi comparer deux rapports demanderait de
      // chercher chaque table deux fois.
      [.. screening.Tables.Select(table => SummarisedTable.Of(table, columnsByTable[table]))],
      ScreeningTally.Of(counts));
  }
}
