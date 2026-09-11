using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UseCases.Requests.DeleteDataSubjectRequest;

/// <summary>
/// <b>Supprimer une demande</b> — la retirer définitivement du service, quel que soit son statut.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce n'est pas un <c>Gesture</c></b> (ADR-0022) : un <c>Gesture</c> laisse une trace datée, la
/// suppression efface celle qu'avait laissée l'enregistrement. Rien n'est écrit à sa place — ni date,
/// ni auteur, ni identifiant opaque.
/// </para>
/// <para>
/// ⚠️ <b>« Supprimer », jamais « effacer »</b> : l'effacement est le droit de l'art. 17, que la demande
/// peut invoquer. Supprimer une demande qui l'invoque ne l'exerce pas.
/// </para>
/// </remarks>
/// <param name="DataSubjectRequest">La demande qu'on supprime.</param>
public sealed record DeleteDataSubjectRequestCommand(DataSubjectRequestId DataSubjectRequest) : ICommand<Result>;
