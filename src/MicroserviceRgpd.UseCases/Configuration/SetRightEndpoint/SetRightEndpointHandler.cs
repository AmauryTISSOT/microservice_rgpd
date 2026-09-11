using MicroserviceRgpd.Core.Configuration;

namespace MicroserviceRgpd.UseCases.Configuration.SetRightEndpoint;

/// <summary>
/// Écrit l'adresse d'un droit dans le Paramétrage, et <b>seulement celle-là</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Naissance paresseuse.</b> Tant que rien n'a été enregistré, la ligne unique n'existe pas : le
/// premier enregistrement la matérialise à partir du Paramétrage vierge. Aucun seed ne l'a posée.
/// </para>
/// <para>
/// ⚠️ <b>La ligne existante est enregistrée par le suivi des modifications, jamais par un
/// <c>Update</c> global.</b> Un <c>Update</c> marquerait les six colonnes comme modifiées et réécrirait
/// les cinq autres droits avec la valeur lue un instant plus tôt — un enregistrement concurrent sur un
/// autre droit serait écrasé en silence. Seule la colonne du droit touché part en base.
/// </para>
/// <para>
/// <b>Aucun appel réseau.</b> L'adresse est écrite, pas jointe : qu'elle réponde ou non se
/// découvrira lorsque le service l'appellera pour exercer le droit.
/// </para>
/// </remarks>
/// <param name="settings">Le Paramétrage persisté — au plus une ligne.</param>
public sealed class SetRightEndpointHandler(IRepository<Settings> settings)
  : ICommandHandler<SetRightEndpointCommand, Result>
{
  /// <inheritdoc />
  public async ValueTask<Result> Handle(SetRightEndpointCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (!Settings.ConfigurableRights.Contains(command.Right))
    {
      return Result.Invalid(new ValidationError
      {
        Identifier = nameof(SetRightEndpointCommand.Right),
        ErrorMessage = $"« {command.Right.Name} » n'est pas un droit du Paramétrage.",
        Severity = ValidationSeverity.Error,
      });
    }

    var persisted = await settings.GetByIdAsync(Settings.SingletonId, cancellationToken);

    if (persisted is null)
    {
      var born = Settings.Unconfigured();
      born.SetEndpoint(command.Right, command.Endpoint);

      await settings.AddAsync(born, cancellationToken);

      return Result.Success();
    }

    persisted.SetEndpoint(command.Right, command.Endpoint);

    await settings.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}
