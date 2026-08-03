using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.DeclareSystem;

/// <summary>
/// Faire entrer un système de plus dans le paysage déclaré du client.
/// </summary>
/// <remarks>
/// <b>La date de déclaration n'est pas un paramètre.</b> Elle est lue sur l'horloge du service au
/// moment du geste : la laisser saisir permettrait de dater d'aujourd'hui une affirmation d'il y a
/// deux ans, ce qui est exactement le mensonge que le vieillissement du <c>Manifest</c> est là pour
/// rendre visible.
/// </remarks>
/// <param name="Id">
/// L'identifiant choisi par l'humain, et celui que l'<c>Adapter</c> recevra. Il est <b>unique dans
/// le catalogue</b> : deux lignes le partageant feraient de tout appel sortant une loterie.
/// </param>
/// <param name="Label">Le nom sous lequel l'<c>Operator</c> reconnaît ce système.</param>
/// <param name="Contents">Ce que ce système contient, en prose française libre.</param>
/// <param name="Capabilities">
/// Ce que l'<c>Adapter</c> sait faire ici — <b>éventuellement rien</b>, et c'est le régime
/// majoritaire, pas une déclaration inachevée.
/// </param>
/// <param name="AdapterAddress">L'adresse de l'<c>Adapter</c> qui sert ce système, ou rien.</param>
public sealed record DeclareSystemCommand(
  DeclaredSystemId Id,
  SystemLabel Label,
  SystemContents Contents,
  IReadOnlyCollection<Capability> Capabilities,
  AdapterAddress? AdapterAddress)
  : ICommand<Result<DeclaredSystem>>;
