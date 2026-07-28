namespace MicroserviceRgpd.Core.Qualifications;

/// <summary>
/// Qui a parlé, et dans quelle version : l'identité que chaque moteur joint à son avis.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle ne sert qu'à la trace d'audit</b>, et ne franchit aucune frontière publique. Sans elle,
/// la trace conserverait des avis sans savoir de quelle version de moteur ils relèvent — et une
/// qualification contestée deviendrait inexplicable le jour où le modèle aura changé sous elle.
/// </para>
/// <para>
/// <b>Le domaine ne l'interprète jamais.</b> Ni comparaison de versions, ni reconnaissance d'un
/// nom : une règle qui lirait « si le moteur est le lexique » ferait entrer dans le domaine
/// l'identité des moteurs, que <see cref="QualificationEngineRole"/> en tient précisément dehors.
/// C'est une donnée qu'on conserve, pas une donnée dont on décide.
/// </para>
/// </remarks>
/// <param name="Name">Le nom sous lequel le moteur se déclare, tel qu'il arrive.</param>
/// <param name="Version">
/// La version que le moteur déclare de lui-même — celle de ses règles pour un lexique, celle du
/// modèle servi pour un LLM. Une chaîne opaque : le service ne la compare pas, il l'enregistre.
/// </param>
public sealed record QualificationEngineIdentity(string Name, string Version);
