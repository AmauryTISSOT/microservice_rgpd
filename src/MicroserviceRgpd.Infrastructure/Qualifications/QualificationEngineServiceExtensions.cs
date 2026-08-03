using System.Globalization;
using MicroserviceRgpd.Core.Qualifications;
using Polly;

namespace MicroserviceRgpd.Infrastructure.Qualifications;

/// <summary>
/// Branche les moteurs de qualification sur le sidecar qui les héberge.
/// </summary>
public static class QualificationEngineServiceExtensions
{
  /// <summary>La section de configuration où vivent l'adresse du sidecar et l'échéance du lexique.</summary>
  private const string Section = "Qualification";

  /// <summary>
  /// La sous-section propre au moteur LLM. Ses réglages y sont regroupés pour qu'éteindre le moteur
  /// rende visiblement inertes <b>ses</b> réglages, plutôt que de laisser une échéance orpheline à
  /// côté d'un booléen.
  /// </summary>
  private const string LlmSection = $"{Section}:Llm";

  /// <summary>
  /// Le drapeau qui commande l'existence du moteur LLM. <b>Absent, il vaut « éteint »</b>.
  /// </summary>
  public const string LlmEnabledKey = $"{LlmSection}:Enabled";

  /// <summary>
  /// L'échéance du client LLM, <b>lue seulement quand le moteur est allumé</b>. Publique parce que
  /// l'AppHost la pose par variable d'environnement : deux noms tenus en accord de tête finiraient
  /// par diverger en silence.
  /// </summary>
  public const string LlmDeadlineKey = $"{LlmSection}:DeadlineSeconds";

  /// <summary>L'échéance du client lexical, requise dans tous les cas. Posée elle aussi par l'AppHost.</summary>
  public const string LexiconDeadlineKey = $"{Section}:LexiconDeadlineSeconds";

  /// <summary>
  /// L'adresse du sidecar qui héberge les deux moteurs, requise dans tous les cas. Privée, à la
  /// différence des deux échéances : l'AppHost ne la pose pas, elle vient de la découverte de
  /// services.
  /// </summary>
  private const string SidecarBaseAddressKey = $"{Section}:SidecarBaseAddress";

  /// <summary>
  /// Enregistre les moteurs. Ils sont déclarés <b>par le même port</b>, et distingués par leur
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
  /// <para>
  /// <b>Le moteur LLM, lui, peut ne pas être là du tout</b>, et c'est le défaut. Éteint, le rôle
  /// <c>Verdict</c> n'est pourvu par rien : ni client typé, ni pipeline, ni entrée clé. Ce qui n'est
  /// pas branché n'existe pas, et rien n'a donc à décider de ne pas l'appeler — aucun appel ne part,
  /// aucun avertissement de panne n'est journalisé. Un moteur factice qui lèverait toujours a été
  /// écarté pour cette raison même, et le lexique sous les deux rôles parce qu'il se corroborerait
  /// lui-même.
  /// </para>
  /// <para>
  /// Le seul repli de toute la section est celui de ce drapeau, et il est délibéré : un déploiement
  /// qui ne dit rien ne soumet jamais le texte d'une personne concernée à un modèle génératif.
  /// </para>
  /// </remarks>
  public static IServiceCollection AddQualificationEngines(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    var sidecar = configuration[SidecarBaseAddressKey];
    Guard.Against.NullOrEmpty(sidecar, nameof(sidecar),
      $"Aucune adresse de sidecar de qualification configuree : renseigner {SidecarBaseAddressKey}.");

    var address = new Uri(sidecar, UriKind.Absolute);

    // Requis même LLM éteint : le lexique vit dans ce même sidecar, et éteindre le moteur génératif
    // ne retire pas cette dépendance. Échéance franchement plus courte que celle du LLM, ce qui
    // garantit que le témoin ne puisse jamais rallonger le temps de réponse du service. Au-delà, son
    // avis est traité comme absent.
    services.AddEngineClient<LexiconQualificationEngine>(address, Deadline(configuration, LexiconDeadlineKey));

    services.AddKeyedTransient<IQualificationEngine>(
      QualificationEngineRole.Witness,
      (provider, _) => provider.GetRequiredService<LexiconQualificationEngine>());

    if (!LlmIsOn(configuration))
    {
      return services;
    }

    // L'échéance du LLM doit rester strictement plus longue que celle du sidecar vers son amont —
    // inégalité tenue en configuration, et vérifiée au démarrage par le sidecar, seul des deux à
    // connaître les deux chiffres. Elle n'est lue qu'ici, donc seulement moteur allumé : laissée
    // derrière par un moteur éteint, elle est ignorée sans bruit, et rallumer ne demande pas de
    // recomposer ses réglages.
    services.AddEngineClient<LlmQualificationEngine>(address, Deadline(configuration, LlmDeadlineKey));

    // Le client typé reste enregistré sous sa classe concrète ; c'est cette ligne-ci, et elle seule,
    // qui décide quel moteur tient le rôle de verdict. Personne d'autre n'a à le savoir.
    services.AddKeyedTransient<IQualificationEngine>(
      QualificationEngineRole.Verdict,
      (provider, _) => provider.GetRequiredService<LlmQualificationEngine>());

    return services;
  }

  /// <summary>
  /// Dit si le moteur LLM existe. <b>Absent de la configuration, il n'existe pas</b> — seule clé de
  /// la section à disposer d'un repli, et le repli est le choix sûr.
  /// </summary>
  /// <remarks>
  /// Une valeur qui n'est ni « true » ni « false » arrête le démarrage plutôt que d'éteindre : lue
  /// comme un « non », elle ferait passer une faute de frappe pour une décision.
  /// </remarks>
  private static bool LlmIsOn(IConfiguration configuration)
  {
    var raw = configuration[LlmEnabledKey];

    if (string.IsNullOrEmpty(raw))
    {
      return false;
    }

    if (!bool.TryParse(raw, out var enabled))
    {
      throw new ArgumentException(
        $"Le reglage {LlmEnabledKey} vaut « {raw} », qui n'est ni « true » ni « false ».",
        LlmEnabledKey);
    }

    return enabled;
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
    var raw = configuration[key];

    Guard.Against.NullOrEmpty(raw, key,
      $"Aucune echeance de moteur configuree : renseigner {key}.");

    if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
    {
      throw new ArgumentException(
        $"Le reglage {key} vaut « {raw} », qui n'est pas une duree en secondes strictement positive.",
        key);
    }

    return TimeSpan.FromSeconds(seconds);
  }
}
