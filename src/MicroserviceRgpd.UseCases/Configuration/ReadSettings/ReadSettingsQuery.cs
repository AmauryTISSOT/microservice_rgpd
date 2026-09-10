using MicroserviceRgpd.Core.Configuration;

namespace MicroserviceRgpd.UseCases.Configuration.ReadSettings;

/// <summary>
/// Relire le Paramétrage : la projection des six droits et de leur état.
/// </summary>
/// <remarks>
/// <b>Aucun paramètre.</b> La configuration est un singleton, propriété du service : il n'y a qu'une
/// seule chose à lire, et rien à filtrer ni à paginer. Sur un service vierge, la lecture rend les
/// six droits « non configuré » sans qu'aucune ligne n'ait été persistée.
/// </remarks>
public sealed record ReadSettingsQuery : IQuery<Settings>;
