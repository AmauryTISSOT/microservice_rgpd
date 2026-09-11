using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.UseCases.Requests.DeleteDataSubjectRequest;

/// <summary>
/// Supprime la demande nommée — ou rend « introuvable » quand elle n'existe plus.
/// </summary>
/// <remarks>
/// <b>Une demande introuvable n'est pas une faute de programmation</b> : deux onglets ouverts sur le
/// tableau, et l'autre l'a déjà supprimée. L'écran le lit comme une réussite (ADR-0022).
/// </remarks>
/// <param name="requests">Les demandes enregistrées.</param>
public sealed class DeleteDataSubjectRequestHandler(IRepository<DataSubjectRequest> requests)
  : ICommandHandler<DeleteDataSubjectRequestCommand, Result>
{
  /// <inheritdoc />
  public async ValueTask<Result> Handle(DeleteDataSubjectRequestCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var deleted = await requests.GetByIdAsync(command.DataSubjectRequest, cancellationToken);

    if (deleted is null)
    {
      return Result.NotFound();
    }

    await requests.DeleteAsync(deleted, cancellationToken);

    return Result.Success();
  }
}
