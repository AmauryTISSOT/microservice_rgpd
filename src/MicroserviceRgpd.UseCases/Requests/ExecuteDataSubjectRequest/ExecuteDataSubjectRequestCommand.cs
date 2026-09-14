using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UseCases.Requests.ExecuteDataSubjectRequest;

/// <summary>
/// <b>Exécuter une demande</b> — le <c>Gesture</c> par lequel l'<c>Operator</c> fait appliquer le droit
/// invoqué par le système hôte, à l'adresse que le Paramétrage associe à ce droit (ADR-0026).
/// </summary>
/// <remarks>
/// <para>
/// Sa trace est la tentative du journal d'exécution, une par appel parti ; il ne pose aucune empreinte
/// sur la demande. Le passage à Terminée est un état, pas une trace.
/// </para>
/// <para>
/// ⚠️ <b>Le résultat rend la demande dans tous les cas</b> — réussite comme refus —, telle que le
/// tableau la lit : l'écran en rend la ligne à jour sans relire la base.
/// </para>
/// </remarks>
/// <param name="DataSubjectRequest">La demande qu'on exécute.</param>
public sealed record ExecuteDataSubjectRequestCommand(DataSubjectRequestId DataSubjectRequest)
  : ICommand<Result<DataSubjectRequestExecution>>;
