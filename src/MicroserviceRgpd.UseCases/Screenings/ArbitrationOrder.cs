using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UseCases.Screenings.ReadCurrentScreening;

namespace MicroserviceRgpd.UseCases.Screenings;

/// <summary>
/// <b>La table suivante</b> : où l'arbitrage reprend quand on a fini de lire une table.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>L'ordre est celui du rapport</b>, retrié par le service : deux <c>Operator</c> qui
/// reprennent le même rapport sont menés dans le même ordre.
/// </para>
/// <para>
/// ⚠️ <b>La suivante est cherchée APRÈS la table ouverte, puis depuis le début</b> : un rapport
/// repris au milieu ne ramène pas en tête, et une table laissée derrière soi n'est pas perdue — on
/// y revient au tour suivant.
/// </para>
/// <para>
/// ⚠️ <b>La table ouverte n'est jamais sa propre suivante</b>, même s'il y reste une colonne en
/// attente : « Table suivante » qui rouvrirait la même page se lirait comme un bouton en panne. Ce
/// qu'elle garde en attente reste compté dans la liste des tables, et dans l'avancement.
/// </para>
/// </remarks>
public static class ArbitrationOrder
{
  /// <summary>
  /// La première table, après <paramref name="after"/> et en reprenant depuis le début, où une
  /// colonne attend encore — ou <c>null</c> quand aucune autre n'attend plus rien.
  /// </summary>
  /// <param name="tables">Les tables du rapport, dans son ordre.</param>
  /// <param name="after">La table ouverte, ou <c>null</c> pour partir du début.</param>
  public static SummarisedTable? NextAfter(
    IReadOnlyList<SummarisedTable> tables, TableIdentity? after)
  {
    ArgumentNullException.ThrowIfNull(tables);

    var start = after is null ? 0 : IndexOf(tables, after) + 1;

    for (var offset = 0; offset < tables.Count; offset++)
    {
      var candidate = tables[(start + offset) % tables.Count];

      if (candidate.AwaitingCount > 0 && candidate.Identity != after)
      {
        return candidate;
      }
    }

    return null;
  }

  private static int IndexOf(IReadOnlyList<SummarisedTable> tables, TableIdentity table)
  {
    for (var index = 0; index < tables.Count; index++)
    {
      if (tables[index].Identity == table)
      {
        return index;
      }
    }

    // Une table que le rapport ne porte pas : on repart du début, comme sans table ouverte.
    return -1;
  }
}
