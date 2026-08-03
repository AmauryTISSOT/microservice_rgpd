using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.DeclareSystem;

/// <summary>
/// Inscrit un système de plus au catalogue, et lui donne sa date.
/// </summary>
/// <remarks>
/// <b>Il ne complète rien et ne devine rien.</b> Ce qui n'a pas été déclaré n'entre pas : c'est
/// tout l'objet d'un catalogue déclaré plutôt que découvert, et le seul endroit du dispositif où
/// l'on parle des systèmes que le service ne touche pas.
/// </remarks>
/// <param name="manifest">Le catalogue persisté, tenu système par système.</param>
/// <param name="clock">
/// L'horloge, injectée pour que la date de déclaration se dicte en test plutôt que d'être lue sur
/// la machine qui l'exécute.
/// </param>
public sealed class DeclareSystemHandler(IRepository<DeclaredSystem> manifest, TimeProvider clock)
  : ICommandHandler<DeclareSystemCommand, Result<DeclaredSystem>>
{
  /// <inheritdoc />
  public async ValueTask<Result<DeclaredSystem>> Handle(
    DeclareSystemCommand command,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var floor = CapabilityFloor.Violation(command.Capabilities, nameof(DeclareSystemCommand.Capabilities));

    if (floor is not null)
    {
      return Result<DeclaredSystem>.Invalid(floor);
    }

    // L'unicité se vérifie ici ET se tient en base par la clé primaire : la lecture nomme le
    // conflit à l'humain, la clé garantit qu'aucune course ne le contourne en silence.
    var alreadyDeclared = await manifest.FirstOrDefaultAsync(new DeclaredSystemByIdSpec(command.Id), cancellationToken);

    if (alreadyDeclared is not null)
    {
      return Result<DeclaredSystem>.Invalid(new ValidationError
      {
        Identifier = nameof(DeclareSystemCommand.Id),
        ErrorMessage = $"L'identifiant « {command.Id.Value} » désigne déjà un système déclaré.",
        Severity = ValidationSeverity.Error,
      });
    }

    var declared = DeclaredSystem.Declare(
      command.Id,
      command.Label,
      command.Contents,
      command.Capabilities,
      command.AdapterAddress,
      clock.GetUtcNow());

    await manifest.AddAsync(declared, cancellationToken);

    return Result<DeclaredSystem>.Success(declared);
  }
}
