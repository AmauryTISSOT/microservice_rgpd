using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.ReadManifest;

/// <summary>
/// Relire le paysage déclaré, en entier.
/// </summary>
/// <remarks>
/// <b>Aucune pagination, aucun filtre, aucun tri au choix.</b> Un catalogue se lit d'un bloc :
/// c'est un paysage de quelques systèmes, pas une liste qui grossit avec l'usage, et une page
/// suivante qu'on n'ouvre pas est exactement la forme d'<c>Omission silencieuse</c> qu'un écran
/// peut fabriquer.
/// </remarks>
public sealed record ReadManifestQuery : IQuery<Manifest>;
