using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

namespace MicroserviceRgpd.UseCases.Requests.ExtendDataSubjectRequest;

/// <summary>
/// <b>Prolonger une demande</b> — le <c>Gesture</c> par lequel l'<c>Operator</c> reporte de deux mois
/// la date limite de réponse, au titre de l'article 12 §3 (ADR-0029). Rend la demande prolongée telle
/// que le tableau la lit : l'écran en rend la ligne sans relire la base.
/// </summary>
/// <remarks>
/// ⚠️ <b>La durée n'est pas commandée</b> : deux mois est une constante du domaine. La commande ne
/// porte que ce que l'<c>Operator</c> a saisi — le motif et la justification.
/// </remarks>
/// <param name="DataSubjectRequest">La demande qu'on prolonge.</param>
/// <param name="Entry">Les valeurs brutes saisies, ni trimées ni validées : c'est le domaine qui en juge.</param>
public sealed record ExtendDataSubjectRequestCommand(
  DataSubjectRequestId DataSubjectRequest,
  ExtensionEntry Entry) : ICommand<Result<RecordedDataSubjectRequest>>;
