using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequestValues;

/// <summary>
/// Relire <b>les valeurs d'une seule demande</b>, celles-là mêmes que l'<c>Operator</c> avait
/// saisies : de quoi pré-remplir le formulaire.
/// </summary>
/// <remarks>
/// ⚠️ <b>Lire n'est pas modifier</b> : le statut de la demande n'entre pas en compte. Une demande
/// close se lit comme une autre — refuser sa lecture ferait de ce point de consultation le gardien
/// d'une règle d'écriture.
/// </remarks>
/// <param name="DataSubjectRequest">La demande qu'on relit.</param>
public sealed record ReadDataSubjectRequestValuesQuery(DataSubjectRequestId DataSubjectRequest)
  : IQuery<Result<DataSubjectRequestValues>>;
