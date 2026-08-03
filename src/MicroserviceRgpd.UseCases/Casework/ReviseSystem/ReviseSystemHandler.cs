using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.ReviseSystem;

/// <summary>
/// Réécrit ce qu'un humain avait déclaré d'un système, et redate sa déclaration.
/// </summary>
/// <param name="manifest">Le catalogue persisté, tenu système par système.</param>
/// <param name="clock">L'horloge, injectée pour que la date de déclaration se dicte en test.</param>
public sealed class ReviseSystemHandler(IRepository<DeclaredSystem> manifest, TimeProvider clock)
  : ICommandHandler<ReviseSystemCommand, Result<DeclaredSystem>>
{
  /// <inheritdoc />
  public async ValueTask<Result<DeclaredSystem>> Handle(
    ReviseSystemCommand command,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var floor = CapabilityFloor.Violation(command.Capabilities, nameof(ReviseSystemCommand.Capabilities));

    if (floor is not null)
    {
      return Result<DeclaredSystem>.Invalid(floor);
    }

    var declared = await manifest.FirstOrDefaultAsync(new DeclaredSystemByIdSpec(command.Id), cancellationToken);

    if (declared is null)
    {
      return Result<DeclaredSystem>.NotFound(
        $"L'identifiant « {command.Id.Value} » ne désigne aucun système déclaré.");
    }

    declared.Revise(
      command.Label,
      command.Contents,
      command.Capabilities,
      command.AdapterAddress,
      clock.GetUtcNow());

    await manifest.UpdateAsync(declared, cancellationToken);

    return Result<DeclaredSystem>.Success(declared);
  }
}
