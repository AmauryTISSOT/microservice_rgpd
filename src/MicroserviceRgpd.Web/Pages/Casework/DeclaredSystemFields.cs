using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.Web.Pages.Casework;

/// <summary>
/// Le formulaire une fois passé dans les types du domaine — ce que la page confie au gestionnaire.
/// </summary>
/// <param name="Id">L'identifiant du système.</param>
/// <param name="Label">Son libellé.</param>
/// <param name="Contents">Ce qu'il contient, en prose.</param>
/// <param name="Capabilities">Ses capacités, éventuellement aucune.</param>
/// <param name="AdapterAddress">L'adresse de son <c>Adapter</c>, ou rien.</param>
public sealed record DeclaredSystemFields(
  DeclaredSystemId Id,
  SystemLabel Label,
  SystemContents Contents,
  IReadOnlyCollection<Capability> Capabilities,
  AdapterAddress? AdapterAddress);
