using System.Reflection;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// Recueille les pas rapportés par un scan, <b>sur le fil qui les rapporte</b>.
/// </summary>
/// <remarks>
/// ⚠️ <b>Ce n'est pas un <see cref="Progress{T}"/>, et ce n'est pas un détail.</b>
/// <see cref="Progress{T}"/> poste ses rappels sur le contexte de synchronisation — donc, en test,
/// sur le pool de fils : les pas arriveraient après l'assertion une fois sur trois, et le test
/// échouerait sans que rien ne soit cassé.
/// </remarks>
internal sealed class ARecordOfSteps : IProgress<ScanStep>
{
  private readonly List<ScanStep> _steps = [];
  private readonly Action<ScanStep>? _onEachStep;

  public ARecordOfSteps(Action<ScanStep>? onEachStep = null)
  {
    _onEachStep = onEachStep;
  }

  public IReadOnlyList<ScanStep> Steps => _steps;

  public void Report(ScanStep value)
  {
    _steps.Add(value);
    _onEachStep?.Invoke(value);
  }
}

/// <summary>
/// Les requêtes du chemin collé, lues depuis <c>releves/</c> — la source, jamais une copie.
/// </summary>
/// <remarks>
/// ⚠️ <b>Recopier la requête dans le test la ferait diverger de celle que l'<c>Operator</c>
/// exécute.</b> Le test qui compare les deux chemins ne prouverait alors plus que deux copies se
/// ressemblent.
/// </remarks>
internal static class ThePastedQuery
{
  internal static string For(string dialect)
  {
    var name = $"releves.{dialect}.sql";

    using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
      ?? throw new InvalidOperationException($"La ressource embarquée {name} est introuvable.");

    using var reader = new StreamReader(stream);

    return reader.ReadToEnd();
  }
}
