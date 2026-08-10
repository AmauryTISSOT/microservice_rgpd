using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings;
using Microsoft.Extensions.DependencyInjection;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings;

/// <summary>
/// Le moteur de dépistage, tel que le service l'obtient — <b>par son port et par son câblage</b>.
/// </summary>
/// <remarks>
/// ⚠️ <b>Aucun test ne connaît les classes internes du moteur.</b> Ils passent tous par
/// <see cref="IScreeningEngine"/>, et le seul endroit du dépôt de tests qui nomme une classe
/// d'implémentation est cette ligne-ci, où c'est le <b>câblage</b> qu'on emprunte, pas
/// l'implémentation. Le lexique, la découpe des identifiants et les cinq règles n'ont ainsi aucun
/// test qui les épingle : un moteur qui reviendrait en Python n'invaliderait rien de ce dossier —
/// c'est la couture de réversibilité d'ADR-0004 prise au mot.
/// </remarks>
internal static class AScreeningEngine
{
  /// <summary>Le moteur que le service câble, résolu par son port.</summary>
  internal static IScreeningEngine Wired()
  {
    return new ServiceCollection()
      .AddScreeningEngine()
      .BuildServiceProvider()
      .GetRequiredService<IScreeningEngine>();
  }
}
