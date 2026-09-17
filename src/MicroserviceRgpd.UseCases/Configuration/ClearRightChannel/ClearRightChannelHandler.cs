using MicroserviceRgpd.Core.Configuration;

namespace MicroserviceRgpd.UseCases.Configuration.ClearRightChannel;

/// <summary>
/// Ramène un droit du Paramétrage à « non configuré », et <b>seulement celui-là</b> : ses colonnes passent
/// à <c>NULL</c>, celles des cinq autres ne bougent pas. <b>Un seul geste pour les deux canaux</b> :
/// effacer une adresse et effacer un routage, c'est ramener un droit au même état (ADR-0027).
/// </summary>
/// <remarks>
/// <para>
/// <b>Rien à effacer sur un service vierge.</b> Tant que la ligne unique n'existe pas, les six droits
/// sont déjà « non configuré » : l'effacement réussit sans la matérialiser — seul un enregistrement
/// la fait naître.
/// </para>
/// <para>
/// ⚠️ <b>La ligne est enregistrée par le suivi des modifications, jamais par un <c>Update</c>
/// global</b>, pour la même raison qu'à l'enregistrement : un <c>Update</c> réécrirait les cinq autres
/// droits avec la valeur lue un instant plus tôt, et écraserait en silence un enregistrement
/// concurrent. Seules les colonnes du droit effacé partent en base.
/// </para>
/// </remarks>
/// <param name="settings">Le Paramétrage persisté — au plus une ligne.</param>
public sealed class ClearRightChannelHandler(IRepository<Settings> settings)
  : ICommandHandler<ClearRightChannelCommand, Result>
{
  /// <inheritdoc />
  public async ValueTask<Result> Handle(ClearRightChannelCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    if (!Settings.ConfigurableRights.Contains(command.Right))
    {
      return Result.Invalid(new ValidationError
      {
        Identifier = nameof(ClearRightChannelCommand.Right),
        ErrorMessage = $"« {command.Right.Name} » n'est pas un droit du Paramétrage.",
        Severity = ValidationSeverity.Error,
      });
    }

    var persisted = await settings.GetByIdAsync(Settings.SingletonId, cancellationToken);

    if (persisted is null)
    {
      return Result.Success();
    }

    persisted.ClearChannel(command.Right);

    await settings.SaveChangesAsync(cancellationToken);

    return Result.Success();
  }
}
