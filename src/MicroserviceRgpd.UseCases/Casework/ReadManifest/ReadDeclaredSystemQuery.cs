using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.ReadManifest;

/// <summary>
/// Relire <b>un</b> système déclaré — ce que l'écran de révision affiche avant qu'on y touche.
/// </summary>
/// <param name="Id">L'identifiant sous lequel il a été déclaré.</param>
public sealed record ReadDeclaredSystemQuery(DeclaredSystemId Id) : IQuery<Result<DeclaredSystem>>;
