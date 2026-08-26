namespace MicroserviceRgpd.UseCases.Screenings.ArbitrateTableInBatch;

/// <summary>
/// Ce que le <b>geste de lot</b> a fait de la table ouverte : combien de colonnes il a tranchées, et
/// combien de signalées y attendent toujours d'être lues une par une.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le second compte est la moitié utile de la réponse.</b> Un lot qui dirait seulement
/// « 38 colonnes tranchées » laisserait l'<c>Operator</c> devant une table qu'il croit finie alors
/// que ses suspicions, elles, n'ont pas bougé — et c'est très exactement l'épuisement que ce geste
/// existe pour éviter qui les lui ferait oublier.
/// </para>
/// <para>
/// <b>Zéro colonne tranchée n'est pas une panne</b> : c'est une table dont tout ce qui pouvait
/// l'être l'a déjà été. Le dire est la réponse — le taire aurait fait lire un geste sans effet comme
/// un geste réussi.
/// </para>
/// </remarks>
/// <param name="Arbitrated">Combien de colonnes le lot a tranchées, chacune datée pour elle-même.</param>
/// <param name="FlaggedStillAwaiting">
/// Combien de colonnes <b>signalées</b> de cette table attendent encore. Le lot ne les a pas
/// touchées, et aucun lot ne les touchera jamais.
/// </param>
public sealed record BatchArbitration(int Arbitrated, int FlaggedStillAwaiting);
