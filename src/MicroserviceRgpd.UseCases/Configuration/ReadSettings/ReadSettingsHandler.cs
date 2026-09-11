using MicroserviceRgpd.Core.Configuration;

namespace MicroserviceRgpd.UseCases.Configuration.ReadSettings;

/// <summary>
/// Rend le Paramétrage à cet instant. <b>Un Paramétrage vierge est un résultat, jamais une
/// absence</b> : c'est l'état d'un service qu'on vient d'installer, et l'écran doit pouvoir dire
/// « non configuré » pour les six droits sans qu'aucune ligne n'existe.
/// </summary>
/// <param name="settings">La configuration persistée, en lecture seule — au plus une ligne.</param>
public sealed class ReadSettingsHandler(IReadRepository<Settings> settings)
  : IQueryHandler<ReadSettingsQuery, Settings>
{
  /// <inheritdoc />
  public async ValueTask<Settings> Handle(ReadSettingsQuery query, CancellationToken cancellationToken)
  {
    var persisted = await settings.ListAsync(cancellationToken);

    // La naissance est paresseuse : tant que rien n'a été enregistré, il n'y a pas de ligne, et le
    // Paramétrage vierge se fabrique à la lecture plutôt que d'être semé au démarrage.
    return persisted.Count > 0 ? persisted[0] : Settings.Unconfigured();
  }
}
