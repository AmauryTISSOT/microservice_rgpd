using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using Polly.Timeout;

namespace MicroserviceRgpd.Infrastructure.Casework.Adapters;

/// <summary>
/// Les deux sondes de vérification sur le fil : un <c>Locate</c> sous le secret du déploiement, et
/// un <c>Locate</c> sous un secret <b>délibérément faux</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elles parlent exactement le contrat que le client a implémenté</b> — même en-tête, même
/// paramètre, même corps, via <see cref="AdapterWire"/>. Une sonde qui aurait pris un chemin à elle
/// n'aurait vérifié qu'elle-même : c'est la route réelle, celle qu'un dossier emprunte, dont on veut
/// savoir si elle est gardée.
/// </para>
/// <para>
/// ⚠️ <b>Aucune sonde ne lit le corps de la réponse.</b> Le corps d'un <c>200</c> rendu à un secret
/// faux est fait des données personnelles que l'<c>Adapter</c> n'aurait pas dû servir : le lire les
/// ferait entrer dans le service au titre de la vérification qui vient les dénoncer. Le statut
/// suffit — et c'est aussi pourquoi ce type ne partage pas l'implémentation de
/// <see cref="HttpAdapterCalls"/>, qui, elle, désérialise.
/// </para>
/// <para>
/// <b>Le corps envoyé est un sac de désignations vide.</b> Le contrat le prévoit en toutes lettres :
/// une recherche sous rien n'est pas une erreur. Aucune personne réelle ne part donc chez un client
/// au titre d'aucun dossier.
/// </para>
/// </remarks>
/// <param name="client">
/// Le client HTTP des sondes. Aucune adresse de base : celle-ci est déclarée par système dans le
/// <c>Manifest</c>.
/// </param>
/// <param name="secret">Le secret partagé du déploiement, pour la sonde de plancher — et pour elle seule.</param>
public sealed class HttpAdapterProbes(HttpClient client, AdapterSecret secret) : IAdapterProbes
{
  /// <summary>
  /// Le secret que la sonde présente pour se faire refuser. <b>Il est fixe, et sans aucun rapport
  /// avec celui du déploiement</b> : un faux dérivé du vrai — un préfixe, un suffixe, une variante —
  /// livrerait le vrai, octet par octet, à l'<c>Adapter</c> même dont on soupçonne qu'il ne garde
  /// rien.
  /// <para>
  /// Il se lit, et c'est voulu : l'intégrateur qui le verra dans ses journaux doit reconnaître une
  /// sonde du service plutôt que soupçonner une attaque. Sa publicité ne coûte rien — il n'ouvre
  /// aucune porte, il est fait pour s'en faire fermer une.
  /// </para>
  /// </summary>
  public const string FalseSecret = "sonde-de-verification-du-manifest-secret-deliberement-faux";

  /// <summary>Le corps de toute sonde : un sac de désignations <b>vide</b>, et rien d'autre.</summary>
  private static readonly EmptyBag NothingToSearchUnder = new([]);

  /// <inheritdoc />
  public async Task<AdapterOutcome> AskLocateAsync(
    AdapterAddress address,
    DeclaredSystemId declaredSystem,
    CancellationToken cancellationToken = default)
  {
    using var response = await Knock(address, declaredSystem, secret.Value, cancellationToken);

    return response.StatusCode switch
    {
      HttpStatusCode.OK => AdapterOutcome.Served,

      // Un différé est rapporté comme tel, et non replié sur « servi » : le vocabulaire des réponses
      // d'Adapter est fermé, et lui faire dire d'un 202 qu'il a servi le viderait de sens à
      // l'endroit même où il sert. L'échéance déclarée, en revanche, n'est pas relue — la sonde ne
      // lit aucun corps, et la règle qui fait d'un 202 sans échéance une panne protège un appel qui
      // repassera, ce qu'une vérification ne fait jamais.
      HttpStatusCode.Accepted => AdapterOutcome.Deferred,

      HttpStatusCode.Unauthorized => AdapterOutcome.SecretRefused,
      HttpStatusCode.NotFound => AdapterOutcome.SystemNotServed,

      _ => throw new AdapterFailure(
        $"L'Adapter de « {declaredSystem.Value} » a répondu {(int)response.StatusCode} à la sonde de "
        + "vérification, que le contrat ne prévoit pas : ce n'est ni une réponse, ni un refus."),
    };
  }

  /// <inheritdoc />
  public async Task<AdapterExposure> ProbeWithAFalseSecretAsync(
    AdapterAddress address,
    DeclaredSystemId declaredSystem,
    CancellationToken cancellationToken = default)
  {
    HttpResponseMessage response;

    try
    {
      response = await Knock(address, declaredSystem, FalseSecret, cancellationToken);
    }
    catch (AdapterFailure)
    {
      // Une application muette n'est pas une application gardée. La sonde n'a rien démontré, et
      // c'est ce qu'elle rapporte : ranger un silence avec les portes fermées ferait lire une
      // sécurité que personne n'a constatée.
      return AdapterExposure.Inconclusive;
    }

    using (response)
    {
      return response.StatusCode switch
      {
        // Servi, ou pris en charge : dans les deux cas l'Adapter a travaillé pour un appelant qu'il
        // aurait dû refuser. Les séparer aurait fait passer pour prudent un Adapter qui accepte des
        // ordres de n'importe qui au motif qu'il les exécute plus tard.
        HttpStatusCode.OK or HttpStatusCode.Accepted => AdapterExposure.Naked,

        HttpStatusCode.Unauthorized => AdapterExposure.Guarded,

        // Tout le reste — y compris un 404, qui répond du système et non du secret — laisse la
        // question ouverte, et la sonde ne la referme pas à sa place.
        _ => AdapterExposure.Inconclusive,
      };
    }
  }

  /// <summary>
  /// Frappe à la porte : un <c>POST</c> sur l'opération <c>locate</c> du système, sous le secret
  /// donné, avec un sac vide.
  /// </summary>
  /// <remarks>
  /// <b>Le corps de la réponse n'est jamais lu</b> — ni ici, ni chez les appelants. Ce qui revient
  /// est un statut, et le reste est refermé avec le message.
  /// </remarks>
  private async Task<HttpResponseMessage> Knock(
    AdapterAddress address,
    DeclaredSystemId declaredSystem,
    string presented,
    CancellationToken cancellationToken)
  {
    using var request = new HttpRequestMessage(
      HttpMethod.Post,
      AdapterWire.AddressOf(address, declaredSystem, Capability.Locate))
    {
      Content = JsonContent.Create(NothingToSearchUnder, options: JsonSerializerOptions.Web),
    };

    request.Headers.TryAddWithoutValidation(AdapterWire.SecretHeader, presented);

    // `ResponseHeadersRead` n'est pas une optimisation : c'est la promesse « aucune sonde ne lit le
    // corps » tenue par le transport lui-même, plutôt que par la discipline de ce qui suit.
    return await AdapterWire.AnswerTo(
      client, request, declaredSystem, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
  }

  /// <summary>Le sac de désignations d'une sonde : vide, par construction et sans paramètre pour le remplir.</summary>
  private sealed record EmptyBag(IReadOnlyList<object> Designations);
}
