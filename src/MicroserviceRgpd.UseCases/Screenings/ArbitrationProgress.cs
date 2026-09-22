namespace MicroserviceRgpd.UseCases.Screenings;

/// <summary>
/// L'<b>avancement de l'arbitrage</b> sur le rapport entier : combien de colonnes ont été tranchées
/// sur combien, et ce qui reste, <b>dit en deux restes</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>L'avancement est un compte, jamais un état de haut niveau rassurant.</b> « 12 / 37
/// colonnes tranchées » se lit ; « en cours » ne se lit pas.
/// </para>
/// <para>
/// ⚠️ <b>Les deux restes ne se fondent jamais en un seul.</b> Les signalées se tranchent une par une,
/// motif sous les yeux ; celles où rien n'a été vu se relisent, et peuvent se trancher d'un geste
/// de lot. Un reste unique aurait laissé croire qu'un geste de lot vient à bout de tout.
/// </para>
/// <para>
/// ⚠️ <b>Rien ici n'est mémorisé</b> : tout se déduit des comptes et du verrou, eux-mêmes
/// recalculés à chaque rendu.
/// </para>
/// </remarks>
/// <param name="Columns">Toutes les colonnes du rapport, signalées ou non.</param>
/// <param name="Settled">Combien un humain en a tranchées, retenues et écartées ensemble.</param>
/// <param name="FlaggedAwaiting">Combien de colonnes signalées attendent encore.</param>
/// <param name="UnflaggedAwaiting">Combien de colonnes où rien n'a été vu attendent encore.</param>
public sealed record ArbitrationProgress(
  int Columns,
  int Settled,
  int FlaggedAwaiting,
  int UnflaggedAwaiting)
{
  /// <summary>Combien de colonnes attendent encore, les deux restes ensemble.</summary>
  public int Awaiting => FlaggedAwaiting + UnflaggedAwaiting;

  /// <summary>
  /// Plus rien n'attend : l'export porte alors une réponse sur chaque ligne. ⚠️ <b>Un rapport vide
  /// n'est pas « achevé »</b> — il n'a rien à exporter qu'on ait tranché.
  /// </summary>
  public bool IsComplete => Columns > 0 && Awaiting == 0;

  /// <summary>
  /// La part tranchée, en millièmes entiers, pour la <b>largeur</b> d'une barre — jamais pour un
  /// texte : ce que l'humain lit reste le compte.
  /// </summary>
  public int SettledPerMille => Columns == 0 ? 0 : (int)((long)Settled * 1000 / Columns);

  /// <summary>L'avancement que les comptes et le verrou du rapport disent ensemble.</summary>
  /// <remarks>
  /// ⚠️ <b>Les colonnes où rien n'a été vu et qui attendent sont celles du verrou</b>, et non un
  /// second calcul : deux comptes de la même chose auraient fini par diverger.
  /// </remarks>
  public static ArbitrationProgress Of(ScreeningTally tally, UnfinishedScreening unfinished)
  {
    ArgumentNullException.ThrowIfNull(tally);
    ArgumentNullException.ThrowIfNull(unfinished);

    return new ArbitrationProgress(
      Columns: tally.Retained + tally.SetAside + tally.Awaiting,
      Settled: tally.Retained + tally.SetAside,
      FlaggedAwaiting: tally.Awaiting - unfinished.UnreadUnflagged,
      UnflaggedAwaiting: unfinished.UnreadUnflagged);
  }
}
