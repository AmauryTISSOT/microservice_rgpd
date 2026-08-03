using MicroserviceRgpd.Core.Casework.Adapters;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polly;

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
  /// Ce que le service laisse à un <c>Adapter</c> pour répondre <b>quoi que ce soit</b> — servir,
  /// différer, ou refuser. Chiffre arbitraire et assumé : il n'a pas été mesuré, et il n'a pas à
  /// l'être, un travail plus long devant se répondre par un <c>202</c> et son échéance déclarée.
  /// </summary>
  private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

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
  /// <para>
  /// <b>Aucune reprise, et c'est le contrat qui l'exige.</b> Le pipeline standard du dépôt reprend
  /// jusqu'à trois fois un <c>POST</c> tombé sur un <c>5xx</c> ou sur une échéance ; ici, cela
  /// renverrait le même appel à un <c>Adapter</c> qui ne le sait pas — et, au lot où <c>Erase</c>
  /// arrive, une destruction rejouée trois fois. Le contrat promet à l'intégrateur que le service ne
  /// relance jamais tout seul, et cette ligne est ce qui rend la promesse vraie plutôt que polie.
  /// </para>
  /// <para>
  /// <b>Une échéance, fixe et assumée.</b> Elle ne se règle pas par déploiement, et c'est
  /// délibéré : le <c>202</c> existe précisément pour qu'un travail long n'ait pas besoin d'une
  /// échéance longue, et une case réglable inviterait à l'allonger — c'est-à-dire à défaire le
  /// <c>202</c>. Au-delà, l'<c>Adapter</c> n'a ni servi ni différé : c'est une panne.
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

    services.TryAddSingleton(new AdapterSecret(secret));

    // `RemoveAllResilienceHandlers` est marquée expérimentale par le paquet, et pourtant elle est le
    // cœur de la décision : c'est elle qui fait de ce pipeline un *remplacement*.
#pragma warning disable EXTEXP0001
    services
      .AddHttpClient<IAdapterCalls, HttpAdapterCalls>(client =>
      {
        // Aucune adresse de base n'est posée : chaque appel porte celle que le Manifest déclare
        // pour son système, et un Adapter par déploiement serait la topologie que le Manifest
        // refuse de décrire.
        //
        // L'échéance appartient au pipeline, et à lui seul : les cent secondes par défaut de
        // `HttpClient` couperaient l'appel avant lui, et le dépassement arriverait alors sous une
        // annulation muette plutôt que sous une panne nommée.
        client.Timeout = Timeout.InfiniteTimeSpan;
      })
      .RemoveAllResilienceHandlers()
      .AddResilienceHandler("casework-adapter", pipeline => pipeline.AddTimeout(Deadline));
#pragma warning restore EXTEXP0001

    services.TryAddSingleton<IAdapterDisagreements, AdapterDisagreements>();

    return services;
  }
}
