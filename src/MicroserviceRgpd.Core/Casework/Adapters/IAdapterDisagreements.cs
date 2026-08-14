namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// Le désaccord entre ce que le <c>Manifest</c> déclare et ce que l'<c>Adapter</c> répond, signalé
/// <b>une seule fois, au grain du déploiement</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Une panne unique n'est pas N pannes.</b> Un secret périmé, ou une adresse qui ne sert plus ce
/// système, est un fait du déploiement : il vaut pour tous les dossiers à la fois, et le consigner
/// dans chacun ferait cent lignes de bruit là où il n'y a qu'une chose à réparer — et ferait
/// dépendre le volume du signal du nombre de demandes en cours, qui n'en dit rien.
/// </para>
/// <para>
/// <b>C'est un signal d'exploitation, pas une matière de preuve.</b> Ce qui entre au <c>EvidenceLog</c>
/// est la <b>tentative datée</b>, dans le dossier au titre duquel elle a eu lieu ; ce qui sort
/// d'ici s'adresse à qui exploite le service, et ne survit pas au processus qui l'a émis.
/// </para>
/// <para>
/// <b>Le grain est la paire (système, refus)</b>, et non le seul système : un <c>Adapter</c> dont
/// le secret vient d'être rejeté et qui, une fois le secret réparé, ne sert pas le système
/// demandé, a deux choses à dire — les taire l'une après l'autre cacherait la seconde derrière la
/// première.
/// </para>
/// </remarks>
public interface IAdapterDisagreements
{
  /// <summary>
  /// Signale qu'un <c>Adapter</c> a refusé un appel. Le deuxième signal identique <b>ne dit
  /// rien</b> : il n'y a toujours qu'une chose à réparer.
  /// </summary>
  /// <param name="declaredSystem">Le système au titre duquel l'appel est parti.</param>
  /// <param name="refusal">Lequel des deux refus a été rendu.</param>
  void Signal(DeclaredSystemId declaredSystem, AdapterOutcome refusal);
}
