using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

namespace MicroserviceRgpd.UseCases.Requests.RecordDataSubjectRequest;

/// <summary>
/// <b>Enregistrer une demande</b> — le <c>Gesture</c> par lequel l'<c>Operator</c> fait entrer dans
/// le service une demande qu'il vient de recevoir. Rend la demande enregistrée telle que le tableau
/// la lit : l'écran en rend la ligne sans relire la base.
/// </summary>
/// <param name="Entry">Les valeurs brutes saisies, ni trimées ni validées : c'est le domaine qui en juge.</param>
public sealed record RecordDataSubjectRequestCommand(DataSubjectRequestEntry Entry)
  : ICommand<Result<RecordedDataSubjectRequest>>;
