using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings;

/// <summary>
/// Les cinq comptes du rapport. <b>L'avancement est un compte, jamais un état de haut niveau
/// rassurant</b> : « douze colonnes en attente » se lit, « en cours » ne se lit pas.
/// </summary>
/// <remarks>
/// ⚠️ <b>Ils portent sur le rapport entier, y compris sur l'écran d'une table.</b> Des comptes
/// bornés à la table ouverte auraient dit « rien n'attend » à un <c>Operator</c> qui a trente-neuf
/// tables intactes.
/// </remarks>
/// <param name="Flagged">
/// Combien de colonnes la détection a signalées. ⚠️ Le complément est ce qu'elle <b>n'a pas vu</b>,
/// jamais ce qui serait inoffensif : le service n'a jamais vu une seule valeur.
/// </param>
/// <param name="Retained">Combien un humain a retenues, sous son nom.</param>
/// <param name="SetAside">Combien un humain a écartées, sous son nom.</param>
/// <param name="Awaiting">Combien attendent encore qu'un humain les tranche.</param>
/// <param name="RetainedOnUnflagged">
/// Combien un humain a retenues là où la détection n'avait <b>rien vu</b>. ⚠️ <b>C'est la mesure
/// directe de ce que l'<c>Omission relue</c> a rattrapé</b>, et elle vaut zéro tant que personne n'a
/// relu — ce qui est très exactement ce qu'on lui demande de dire.
/// </param>
public sealed record ScreeningTally(
  int Flagged,
  int Retained,
  int SetAside,
  int Awaiting,
  int RetainedOnUnflagged)
{
  internal static ScreeningTally Of(ScreeningCounts counts)
  {
    return new ScreeningTally(
      counts.Flagged,
      counts.Retained,
      counts.SetAside,
      counts.Awaiting,
      counts.RetainedOnUnflagged);
  }
}
