using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

namespace MicroserviceRgpd.UseCases.Requests.ModifyDataSubjectRequest;

/// <summary>
/// <b>Modifier une demande</b> — le <c>Gesture</c> par lequel l'<c>Operator</c> corrige une erreur de
/// saisie dans une demande déjà enregistrée. Rend la demande corrigée telle que le tableau la lit :
/// l'écran en rend la ligne sans relire la base, comme après un enregistrement.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Corriger le dossier n'est pas exercer le droit de rectification</b> (art. 16) : celui-ci
/// porte sur les données de la personne concernée chez le responsable, jamais sur la demande qui
/// l'enregistre.
/// </para>
/// <para>
/// ⚠️ <b>Une correction qui ne change rien rend la demande quand même</b> : le succès n'a qu'une
/// forme, et la demande rendue est la bonne — elle n'a pas changé.
/// </para>
/// </remarks>
/// <param name="DataSubjectRequest">La demande qu'on corrige.</param>
/// <param name="Entry">Les valeurs brutes saisies, ni trimées ni validées : c'est le domaine qui en juge.</param>
public sealed record ModifyDataSubjectRequestCommand(
  DataSubjectRequestId DataSubjectRequest,
  DataSubjectRequestEntry Entry) : ICommand<Result<RecordedDataSubjectRequest>>;
