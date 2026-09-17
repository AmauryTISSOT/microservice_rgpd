using MicroserviceRgpd.Core.Configuration;

namespace MicroserviceRgpd.UseCases.Configuration.SetRightRabbitMqRouting;

/// <summary>
/// Écrit le routage RabbitMQ d'un droit dans le Paramétrage, et <b>seulement celui-là</b> : le droit
/// s'exerce désormais par une publication, et l'adresse HTTP qu'il portait peut-être est oubliée du
/// même geste (ADR-0027).
/// </summary>
/// <remarks>
/// <para>
/// <b>Naissance paresseuse.</b> Tant que rien n'a été enregistré, la ligne unique n'existe pas : le
/// premier enregistrement la matérialise à partir du Paramétrage vierge. Aucun seed ne l'a posée.
/// </para>
/// <para>
/// ⚠️ <b>La ligne existante est enregistrée par le suivi des modifications, jamais par un
/// <c>Update</c> global.</b> Un <c>Update</c> marquerait toutes les colonnes comme modifiées et
/// réécrirait les cinq autres droits avec la valeur lue un instant plus tôt — un enregistrement
/// concurrent sur un autre droit serait écrasé en silence. Seules les colonnes du droit touché
/// partent en base.
/// </para>
/// <para>
/// <b>Aucun appel au broker.</b> Le routage est écrit, pas éprouvé : ni connexion, ni déclaration
/// d'exchange, ni message d'essai. Déclarer précède publier.
/// </para>
/// </remarks>
/// <param name="settings">Le Paramétrage persisté — au plus une ligne.</param>
public sealed class SetRightRabbitMqRoutingHandler(IRepository<Settings> settings)
  : ICommandHandler<SetRightRabbitMqRoutingCommand, Result>
{
  /// <inheritdoc />
  public async ValueTask<Result> Handle(
    SetRightRabbitMqRoutingCommand command,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (!Settings.ConfigurableRights.Contains(command.Right))
    {
      return Result.Invalid(new ValidationError
      {
        Identifier = nameof(SetRightRabbitMqRoutingCommand.Right),
        ErrorMessage = $"« {command.Right.Name} » n'est pas un droit du Paramétrage.",
        Severity = ValidationSeverity.Error,
      });
    }

    var channel = new ExerciseChannel.RabbitMq(command.Routing);

    var persisted = await settings.GetByIdAsync(Settings.SingletonId, cancellationToken);

    if (persisted is null)
    {
      var born = Settings.Unconfigured();
      born.SetChannel(command.Right, channel);

      await settings.AddAsync(born, cancellationToken);

      return Result.Success();
    }

    persisted.SetChannel(command.Right, channel);

    await settings.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}
