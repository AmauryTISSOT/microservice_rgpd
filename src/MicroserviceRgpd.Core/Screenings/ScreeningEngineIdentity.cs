namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Le nom et la version que le moteur joint au <see cref="Screening"/> qu'il a produit — celle de
/// ses règles, celle du modèle servi le cas échéant.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle ne sert qu'à l'humain</b> qui relit un rapport plusieurs jours après l'avoir lancé, ou qui
/// en compare deux. Elle prend tout son sens du fait qu'un rapport de détection neuf <b>ne fusionne
/// pas</b> :
/// relancer produit un rapport neuf, les arbitrages du précédent ne sont pas repris, et cette
/// identité dit au moins <b>pourquoi</b> le nouveau diffère.
/// </para>
/// <para>
/// <b>Le domaine ne l'interprète jamais</b> : ni comparaison de versions, ni reconnaissance d'un nom.
/// C'est une donnée qu'on conserve, pas une donnée dont on décide. Décalque exact de
/// <c>QualificationEngineIdentity</c>, retenue comprise — et décalque de source, jamais de type : le
/// lire d'ici serait une traversée que le garde d'ADR-0003 refuse.
/// </para>
/// <para>
/// ⚠️ <b>Ce n'est pas une signature.</b> Le mot ne nommerait rien ici — ce contexte n'enregistre
/// pas qui arbitre, et le moteur, lui, ne tranche jamais. Voir <see cref="Arbitration"/> et
/// l'<c>ADR-0014</c>.
/// </para>
/// </remarks>
/// <param name="Name">Le nom sous lequel le moteur se déclare, tel qu'il arrive.</param>
/// <param name="Version">
/// La version que le moteur déclare de lui-même. Une chaîne opaque : le service ne la compare pas,
/// il l'enregistre.
/// </param>
public sealed record ScreeningEngineIdentity(string Name, string Version);
