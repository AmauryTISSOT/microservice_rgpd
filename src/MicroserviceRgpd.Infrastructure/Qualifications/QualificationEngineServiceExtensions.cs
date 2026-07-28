using System.Globalization;
using MicroserviceRgpd.Core.Qualifications;
using Polly;

namespace MicroserviceRgpd.Infrastructure.Qualifications;

/// <summary>
/// Branche les moteurs de qualification sur le sidecar qui les héberge.
/// </summary>
public static class QualificationEngineServiceExtensions
{
  /// <summary>La section de configuration où vivent l'adresse du sidecar et les deux échéances.</summary>
  private const string Section = "Qualification";

  /// <summary>
  /// Enregistre les deux moteurs. Ils sont déclarés <b>par le même port</b>, et distingués par leur
  /// <b>rôle</b> seul : le use case qui les consomme ne doit pouvoir apprendre ni qu'un sidecar
  /// Python existe, ni lequel des deux est un LLM.
  /// </summary>
  /// <remarks>
  /// L'adresse et les échéances vivent <b>en configuration seule</b>, sans repli codé en dur. Un
  /// repli ferait exister deux vérités qui finiraient par diverger, et surtout il transformerait une
  /// configuration oubliée en pannes de qualification au premier appel, là où elle doit arrêter le
  /// démarrage — exactement le traitement que reçoit déjà la chaîne de connexion.
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

    var sidecar = configuration[$"{Section}:SidecarBaseAddress"];
    Guard.Against.NullOrEmpty(sidecar, nameof(sidecar),
      "Aucune adresse de sidecar de qualification configuree : renseigner Qualification:SidecarBaseAddress.");

    var address = new Uri(sidecar, UriKind.Absolute);

    // L'échéance du LLM doit rester strictement plus longue que celle du sidecar vers son amont —
    // inégalité tenue en configuration, et vérifiée au démarrage par le sidecar, seul des deux à
    // connaître les deux chiffres.
    services.AddEngineClient<LlmQualificationEngine>(address, Deadline(configuration, "LlmDeadlineSeconds"));

    // Franchement plus courte : c'est ce qui garantit que le témoin ne puisse jamais rallonger le
    // temps de réponse du service. Au-delà, son avis est traité comme absent.
    services.AddEngineClient<LexiconQualificationEngine>(address, Deadline(configuration, "LexiconDeadlineSeconds"));

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

  /// <summary>
  /// Donne à un moteur son client, et à ce client un pipeline de résilience <b>explicite</b> qui
  /// remplace le pipeline standard hérité des réglages du dépôt.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Remplacer, et non compléter.</b> Les valeurs d'usine du pipeline standard contredisent le
  /// contrat interne sur trois points : trente secondes couperaient un appel LLM légitime, et trois
  /// reprises mettraient quatre générations à la queue leu leu sur un GPU qui sérialise, pour une
  /// seule requête entrante — dont trois abandonnées sans être lues. Le `504` bâti pour arriver
  /// nommé ne serait jamais émis, l'échéance du sidecar étant quatre fois plus longue que celle du
  /// client qui l'appelle.
  /// </para>
  /// <para>
  /// <b>Le défaut global reste en place pour tout autre client</b> : le retirer priverait
  /// silencieusement de résilience tout client créé plus tard.
  /// </para>
  /// <para>
  /// <b>Aucune reprise, aucun disjoncteur, une seule échéance.</b> À température nulle et seed fixe,
  /// rejouer est une opération nulle, et depuis qu'il existe un repli lexical la reprise est
  /// dominée ; elle masquerait de surcroît dans les métriques un problème de qualité du modèle qui
  /// doit rester visible. Le disjoncteur, lui, est un seuil, et personne n'a mesuré la fréquence ni
  /// la forme des échecs de l'amont.
  /// </para>
  /// </remarks>
  private static void AddEngineClient<TEngine>(
    this IServiceCollection services,
    Uri address,
    TimeSpan deadline)
    where TEngine : class
  {
    // `RemoveAllResilienceHandlers` est marquée expérimentale par le paquet, et pourtant elle est le
    // cœur de la décision : c'est elle qui fait de ce pipeline un *remplacement*. La contourner en
    // dupliquant le nom du gestionnaire standard serait plus fragile encore, et muet le jour où ce
    // nom changerait.
#pragma warning disable EXTEXP0001
    services
      .AddHttpClient<TEngine>(client =>
      {
        client.BaseAddress = address;

        // L'échéance appartient au pipeline, et à lui seul. Les cent secondes par défaut de
        // `HttpClient` couperaient l'appel LLM avant elle, et le dépassement arriverait alors sous
        // une annulation muette — exactement ce que l'ordre des deux échéances cherche à éviter.
        // Reste hors de sa portée le corps qui s'arrêterait après ses en-têtes ; le sidecar rend
        // quelques centaines d'octets d'un seul tenant, et le risque est nommé plutôt que couvert
        // par un second chiffre à tenir en accord de tête.
        client.Timeout = Timeout.InfiniteTimeSpan;
      })
      .RemoveAllResilienceHandlers()
      .AddResilienceHandler(
        $"qualification-{typeof(TEngine).Name}",
        pipeline => pipeline.AddTimeout(deadline));
#pragma warning restore EXTEXP0001
  }

  /// <summary>Lit une échéance, ou refuse — une valeur absente n'est pas une valeur par défaut.</summary>
  private static TimeSpan Deadline(IConfiguration configuration, string key)
  {
    var raw = configuration[$"{Section}:{key}"];

    Guard.Against.NullOrEmpty(raw, key,
      $"Aucune echeance de moteur configuree : renseigner {Section}:{key}.");

    if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
    {
      throw new ArgumentException(
        $"Le reglage {Section}:{key} vaut « {raw} », qui n'est pas une duree en secondes strictement positive.",
        key);
    }

    return TimeSpan.FromSeconds(seconds);
  }
}
