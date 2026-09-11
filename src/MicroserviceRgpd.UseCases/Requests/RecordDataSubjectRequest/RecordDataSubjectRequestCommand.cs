using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UseCases.Requests.RecordDataSubjectRequest;

/// <summary>
/// <b>Enregistrer une demande</b> — le <c>Gesture</c> par lequel l'<c>Operator</c> fait entrer dans
/// le service une demande qu'il vient de recevoir.
/// </summary>
/// <param name="Entry">Les valeurs brutes saisies, ni trimées ni validées : c'est le domaine qui en juge.</param>
public sealed record RecordDataSubjectRequestCommand(DataSubjectRequestEntry Entry)
  : ICommand<Result<DataSubjectRequestId>>;
