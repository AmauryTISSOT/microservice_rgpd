using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.Infrastructure.Qualifications;

/// <summary>
/// Branche les moteurs de qualification sur le sidecar qui les héberge.
/// </summary>
public static class QualificationEngineServiceExtensions
{
  /// <summary>
  /// Enregistre les deux moteurs. Ils sont déclarés <b>par le même port</b>, et distingués par leur
  /// <b>rôle</b> seul : le use case qui les consomme ne doit pouvoir apprendre ni qu'un sidecar
  /// Python existe, ni lequel des deux est un LLM.
  /// </summary>
  /// <remarks>
  /// L'adresse vit <b>en configuration seule</b>, sans repli codé en dur. Un repli ferait exister
  /// deux vérités qui finiraient par diverger, et surtout il transformerait une configuration
  /// oubliée en pannes de qualification au premier appel, là où elle doit arrêter le démarrage —
  /// exactement le traitement que reçoit déjà la chaîne de connexion.
  /// <para>
  /// Les deux moteurs partagent l'adresse du sidecar et rien d'autre : deux clients distincts, donc
  /// deux pipelines réglables séparément — ce dont l'un a besoin, une échéance longue, tuerait
  /// précisément ce qui fait l'intérêt de l'autre.
  /// </para>
  /// </remarks>
  public static IServiceCollection AddQualificationEngines(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    var sidecar = configuration["Qualification:SidecarBaseAddress"];
    Guard.Against.NullOrEmpty(sidecar, nameof(sidecar),
      "Aucune adresse de sidecar de qualification configuree : renseigner Qualification:SidecarBaseAddress.");

    var address = new Uri(sidecar, UriKind.Absolute);

    services.AddHttpClient<LlmQualificationEngine>(client => client.BaseAddress = address);
    services.AddHttpClient<LexiconQualificationEngine>(client => client.BaseAddress = address);

    // Les clients typés restent enregistrés sous leur classe concrète ; ce sont ces deux lignes, et
    // elles seules, qui décident quel moteur tient quel rôle. Personne d'autre n'a à le savoir.
    services.AddKeyedTransient<IQualificationEngine>(
      QualificationEngineRole.Verdict,
      (provider, _) => provider.GetRequiredService<LlmQualificationEngine>());

    services.AddKeyedTransient<IQualificationEngine>(
      QualificationEngineRole.Witness,
      (provider, _) => provider.GetRequiredService<LexiconQualificationEngine>());

    return services;
  }
}
