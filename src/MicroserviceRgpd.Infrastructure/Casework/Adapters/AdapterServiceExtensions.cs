using MicroserviceRgpd.Core.Casework.Adapters;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MicroserviceRgpd.Infrastructure.Casework.Adapters;

/// <summary>
/// Branche les appels sortants vers les <c>Adapter</c> du client, et <b>refuse de démarrer sans le
/// secret</b>.
/// </summary>
public static class AdapterServiceExtensions
{
  /// <summary>
  /// Le secret partagé, <b>un seul pour tout ce que le service appelle</b>. Il vit dans la
  /// configuration de déploiement, jamais dans le <c>Manifest</c> — un catalogue se relit à
  /// l'écran, et un champ « secret » dedans serait un secret affiché.
  /// <para>
  /// Publique parce que l'AppHost la pose par variable d'environnement : deux noms tenus en accord
  /// de tête finiraient par diverger en silence.
  /// </para>
  /// </summary>
  public const string SecretKey = "Casework:AdapterSecret";

  /// <summary>
  /// Enregistre le client d'<c>Adapter</c> et le signalement des désaccords.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Aucun mode « sans » ne survit à l'intégration.</b> Le secret absent arrête le démarrage —
  /// il n'existe ni repli, ni valeur d'usine, ni drapeau qui l'éteindrait. Un tel drapeau serait
  /// une case à laisser pourrir, et le jour où quelqu'un la coche « le temps de tester », la route
  /// la plus dangereuse de l'application du client est ouverte à qui l'atteint.
  /// </para>
  /// <para>
  /// <b>Un seul secret valide.</b> La configuration n'en accepte qu'un : la rotation est un
  /// redémarrage coordonné des deux côtés, sans recouvrement. Un second secret « encore accepté »
  /// aurait une date de retrait que personne n'aurait à prouver.
  /// </para>
  /// <para>
  /// <b>Le signalement des désaccords est un singleton</b>, et c'est ce qui lui donne le grain du
  /// déploiement : une panne unique se dit une fois, quel que soit le nombre de dossiers en cours.
  /// </para>
  /// </remarks>
  /// <exception cref="ArgumentException">Le secret est absent ou vide de la configuration.</exception>
  public static IServiceCollection AddAdapterCalls(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    ArgumentNullException.ThrowIfNull(configuration);

    var secret = configuration[SecretKey];

    Guard.Against.NullOrWhiteSpace(secret, SecretKey,
      $"Aucun secret d'Adapter configure : renseigner {SecretKey}. Le service n'appelle pas un "
      + "Adapter sans secret, et il n'existe aucun mode « sans ».");

    // Le client n'a pas d'adresse de base : chaque appel porte celle que le Manifest déclare pour
    // son système. Le pipeline de résilience standard du dépôt s'applique — l'échéance qu'il porte
    // est ce qui fait qu'un Adapter lent est une panne nommée plutôt qu'une connexion tenue.
    services.TryAddSingleton(new AdapterSecret(secret));
    services.AddHttpClient<IAdapterCalls, HttpAdapterCalls>();

    services.TryAddSingleton<IAdapterDisagreements, AdapterDisagreements>();

    return services;
  }
}
