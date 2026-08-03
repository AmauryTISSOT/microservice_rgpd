using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.ReviseSystem;

/// <summary>
/// Reprendre ce qu'on avait déclaré d'un système, et le re-dater.
/// </summary>
/// <remarks>
/// <b>Rien n'est corrigé en silence.</b> Le <c>Manifest</c> se vérifie contre l'<c>Adapter</c>, il
/// ne s'aligne jamais sur lui : un écart est un signal bruyant, et sa résolution passe par ce
/// geste-ci, fait par un humain.
/// </remarks>
/// <param name="Id">
/// Le système visé. <b>Il ne se réécrit pas</b> : c'est ce que l'<c>Adapter</c> connaît de ce
/// système, et le changer ferait disparaître une ligne et en créer une autre sous couvert de
/// correction.
/// </param>
/// <param name="Label">Le nom sous lequel l'<c>Operator</c> reconnaît désormais ce système.</param>
/// <param name="Contents">Ce que ce système contient, en prose française libre.</param>
/// <param name="Capabilities">Ce que l'<c>Adapter</c> sait faire ici — éventuellement rien.</param>
/// <param name="AdapterAddress">L'adresse de l'<c>Adapter</c> qui sert ce système, ou rien.</param>
public sealed record ReviseSystemCommand(
  DeclaredSystemId Id,
  SystemLabel Label,
  SystemContents Contents,
  IReadOnlyCollection<Capability> Capabilities,
  AdapterAddress? AdapterAddress)
  : ICommand<Result<DeclaredSystem>>;
