using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Infrastructure.Casework.Adapters;

/// <summary>
/// Le contrat d'<c>Adapter</c> sur le fil : un <c>POST</c> par <c>Capability</c>, le
/// <c>system_id</c> en paramètre, le sac de désignations en corps, et le secret partagé en en-tête.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le secret est un, et il est de la topologie.</b> Un seul secret vaut pour tout ce que le
/// service appelle : il n'est ni par système — le <c>system_id</c> est une unité de recensement —
/// ni négocié, ni recouvert. La rotation est un redémarrage coordonné des deux côtés ; il n'existe
/// donc aucun second secret « encore accepté », et personne n'aura à jurer qu'il a bien été retiré.
/// </para>
/// <para>
/// ⚠️ <b>La clause de périmètre est inséparable de ce fichier.</b> Le contrat ne dit pas
/// « authentifiez-vous » : il dit <b>un secret partagé <i>et</i> un <c>Adapter</c> hors d'atteinte
/// de l'extérieur</b>, les deux ensemble, jamais l'un sans l'autre. La confidentialité du transport
/// est explicitement hors menace — c'est ce qui disqualifie mTLS, HMAC et JWT, qui coûteraient au
/// développeur du client précisément ce que le sens unique des appels lui a fait économiser. Voir
/// <c>docs/api/adapter.md</c>, § 2.
/// </para>
/// <para>
/// <b>Aucune connexion n'est tenue.</b> Un travail long se répond par un <c>202</c> portant une
/// échéance déclarée ; le service la relit, repart, et repassera à l'ouverture du dossier. Il n'y a
/// ici ni attente, ni sondage, ni reprise programmée.
/// </para>
/// </remarks>
/// <param name="client">
/// Le client HTTP de l'<c>Adapter</c>. Il n'a <b>aucune adresse de base</b> : celle-ci est déclarée
/// par système dans le <c>Manifest</c>, et un <c>Adapter</c> par déploiement serait exactement la
/// topologie que le <c>Manifest</c> refuse de décrire.
/// </param>
/// <param name="secret">
/// Le secret partagé, tel que la configuration de déploiement le donne. Il ne vient jamais du
/// <c>Manifest</c>, qui se relit à l'écran.
/// </param>
public sealed class HttpAdapterCalls(HttpClient client, AdapterSecret secret) : IAdapterCalls
{
  /// <summary>
  /// Le fil parle <c>camelCase</c>, comme celui du sidecar. Les natures de désignation et les
  /// capacités y voyagent par leur <b>mot canonique</b>, jamais par un nom de membre C# ni par un
  /// ordinal : le contrat public ne doit pas dépendre de l'ordre de déclaration d'un type.
  /// </summary>
  private static readonly JsonSerializerOptions WireFormat = new(JsonSerializerOptions.Web);

  /// <inheritdoc />
  public async Task<AdapterAnswer<TServed>> AskAsync<TServed>(
    AdapterCall call,
    CancellationToken cancellationToken = default)
    where TServed : class
  {
    ArgumentNullException.ThrowIfNull(call);

    using var request = new HttpRequestMessage(
      HttpMethod.Post,
      AdapterWire.AddressOf(call.Address, call.DeclaredSystem, call.Capability))
    {
      Content = JsonContent.Create(BodyOf(call), options: WireFormat),
    };

    // `TryAddWithoutValidation` n'est pas un contournement : un secret est une chaîne opaque, et la
    // validation d'en-tête de `HttpClient` refuserait des octets qu'un déploiement a le droit de
    // choisir.
    request.Headers.TryAddWithoutValidation(AdapterWire.SecretHeader, secret.Value);

    using var response = await AdapterWire.AnswerTo(
      client, request, call.DeclaredSystem, HttpCompletionOption.ResponseContentRead, cancellationToken);

    return response.StatusCode switch
    {
      HttpStatusCode.OK => AdapterAnswer<TServed>.Serving(await ServedBy<TServed>(response, call, cancellationToken)),
      HttpStatusCode.Accepted => AdapterAnswer<TServed>.Deferring(await DeclaredDeadline(response, call, cancellationToken)),

      // Les deux refus, et eux seuls, sont des réponses. `401` ne dit rien du système appelé : il
      // dit que le secret n'est pas celui qu'attend cet Adapter, ce qui se répare des deux côtés à
      // la fois.
      HttpStatusCode.Unauthorized => AdapterAnswer<TServed>.Refusing(AdapterOutcome.SecretRefused),

      // `404` couvre volontairement deux lectures — « je ne sers pas ce système » et « il n'y a
      // rien à cette adresse » — parce qu'elles sont le même désaccord vu du service : le Manifest
      // désigne un Adapter qui ne sert pas ce système. Les distinguer aurait demandé au client un
      // statut inventé pour un diagnostic qu'il ne peut pas faire à notre place.
      HttpStatusCode.NotFound => AdapterAnswer<TServed>.Refusing(AdapterOutcome.SystemNotServed),

      _ => throw new AdapterFailure(
        $"L'Adapter de « {call.DeclaredSystem.Value} » a répondu {(int)response.StatusCode}, que le "
        + "contrat ne prévoit pas : ce n'est ni une réponse, ni un refus."),
    };
  }

  /// <inheritdoc />
  public async Task<AdapterAnswer<RetrievedPiece>> ReadAsync(
    AdapterCall call,
    DataSubjectRight right,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(call);
    ArgumentNullException.ThrowIfNull(right);

    using var request = new HttpRequestMessage(
      HttpMethod.Post,
      AdapterWire.AddressOf(call.Address, call.DeclaredSystem, call.Capability))
    {
      Content = JsonContent.Create(new AdapterReadBody(BodyOf(call).Designations, right), options: WireFormat),
    };

    request.Headers.TryAddWithoutValidation(AdapterWire.SecretHeader, secret.Value);

    using var response = await AdapterWire.AnswerTo(
      client, request, call.DeclaredSystem, HttpCompletionOption.ResponseContentRead, cancellationToken);

    return response.StatusCode switch
    {
      HttpStatusCode.OK => AdapterAnswer<RetrievedPiece>.Serving(await PieceOf(response, call, cancellationToken)),
      HttpStatusCode.Accepted => AdapterAnswer<RetrievedPiece>.Deferring(
        await DeclaredDeadline(response, call, cancellationToken)),

      HttpStatusCode.Unauthorized => AdapterAnswer<RetrievedPiece>.Refusing(AdapterOutcome.SecretRefused),
      HttpStatusCode.NotFound => AdapterAnswer<RetrievedPiece>.Refusing(AdapterOutcome.SystemNotServed),

      _ => throw new AdapterFailure(
        $"L'Adapter de « {call.DeclaredSystem.Value} » a répondu {(int)response.StatusCode}, que le "
        + "contrat ne prévoit pas : ce n'est ni une réponse, ni un refus."),
    };
  }

  /// <summary>
  /// La pièce servie : son <b>enveloppe de transport</b>, recopiée sans être interprétée, et ses
  /// octets, qui ne sont pas ouverts.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Un corps vide n'est pas une panne ici</b>, à la différence d'un <c>200</c> à <c>locate</c> :
  /// c'est la <b>pièce vide</b>, une déclaration datée disant « interrogé, rien ». C'est elle qui
  /// rend l'incomplétude gratuite pour l'<c>Adapter</c>, qui n'a aucun export d'une forme convenue à
  /// fabriquer pour dire qu'il n'a rien.
  /// </para>
  /// <para>
  /// <b>Le nom vient de l'accesseur typé du transport</b>, <c>filename*</c> d'abord : c'est lui qui
  /// sait déjà décoder un nom accentué, et le réécrire ici aurait fait vivre dans ce fichier une
  /// seconde lecture de la RFC 6266. Un en-tête que le transport n'arrive pas à lire rend
  /// <c>null</c>, et le nom dégrade alors sur le <c>system_id</c> — dégrader proprement est
  /// exactement ce que le contrat promet.
  /// </para>
  /// </remarks>
  private static async Task<RetrievedPiece> PieceOf(
    HttpResponseMessage response,
    AdapterCall call,
    CancellationToken cancellationToken)
  {
    var disposition = response.Content.Headers.ContentDisposition;

    return new RetrievedPiece(
      TransportEnvelope.Of(
        response.Content.Headers.ContentType?.ToString(),
        disposition?.FileNameStar ?? disposition?.FileName,
        call.DeclaredSystem),
      await response.Content.ReadAsByteArrayAsync(cancellationToken));
  }

  /// <summary>
  /// Le corps : le sac de désignations, et rien d'autre. Aucun identifiant de dossier, aucune date,
  /// aucun nom d'<c>Operator</c> — <b>l'<c>Adapter</c> n'a pas à connaître le dossier</b>, et ce
  /// qu'il ne reçoit pas ne peut pas se retrouver dans ses journaux.
  /// </summary>
  private static AdapterCallBody BodyOf(AdapterCall call)
  {
    return new AdapterCallBody(
      [.. call.Designations.Select(designation => new DesignationOnTheWire(designation.Kind.Token, designation.Value))]);
  }

  /// <summary>Ce que l'<c>Adapter</c> a servi, ou la panne d'un corps qui n'en est pas un.</summary>
  private static async Task<TServed> ServedBy<TServed>(
    HttpResponseMessage response,
    AdapterCall call,
    CancellationToken cancellationToken)
    where TServed : class
  {
    TServed? served;

    try
    {
      served = await response.Content.ReadFromJsonAsync<TServed>(WireFormat, cancellationToken);
    }
    catch (JsonException illegible)
    {
      throw new AdapterFailure(
        $"L'Adapter de « {call.DeclaredSystem.Value} » a servi un corps illisible.", illegible);
    }

    return served ?? throw new AdapterFailure(
      $"L'Adapter de « {call.DeclaredSystem.Value} » a répondu 200 avec un corps vide, qui ne sert rien.");
  }

  /// <summary>
  /// L'échéance qu'un différé déclare. <b>Elle est exigée</b> : un <c>202</c> sans elle est une
  /// rupture du contrat, et le service ne la complète pas — une date qu'il aurait inventée
  /// deviendrait une preuve que personne n'a déclarée.
  /// </summary>
  private static async Task<DateTimeOffset> DeclaredDeadline(
    HttpResponseMessage response,
    AdapterCall call,
    CancellationToken cancellationToken)
  {
    DeferredOnTheWire? deferred;

    try
    {
      deferred = await response.Content.ReadFromJsonAsync<DeferredOnTheWire>(WireFormat, cancellationToken);
    }
    catch (JsonException illegible)
    {
      throw new AdapterFailure(
        $"L'Adapter de « {call.DeclaredSystem.Value} » a répondu 202 sans échéance lisible.", illegible);
    }

    if (deferred?.Deadline is null || !DeclaresAnInstant(deferred.Deadline, out var deadline))
    {
      throw new AdapterFailure(
        $"L'Adapter de « {call.DeclaredSystem.Value} » a répondu 202 sans déclarer d'échéance "
        + "relisible : le service n'en invente pas.");
    }

    return deadline;
  }

  /// <summary>
  /// L'échéance déclare-t-elle un <b>instant</b> ? Une date lue sous une culture invariante, et
  /// portant un <b>décalage explicite</b>.
  /// </summary>
  /// <remarks>
  /// <b>« 2026-08-05 09:00 » est refusé</b>, et ce n'est pas de la pédanterie de format : sans
  /// fuseau, cette échéance vaudrait deux heures de moins sur un serveur et deux de plus sur un
  /// autre. Le service ne devine pas le fuseau de son client — il exigerait alors, pour dater une
  /// preuve, une hypothèse que personne n'a déclarée.
  /// </remarks>
  private static bool DeclaresAnInstant(string raw, out DateTimeOffset deadline)
  {
    deadline = default;

    // La sonde dit ce que la chaîne portait : `Unspecified` est exactement « aucun décalage écrit ».
    // `DateTimeOffset` seul ne saurait pas le dire — il complète alors par le fuseau de la machine,
    // c'est-à-dire par la chose qu'on refuse.
    return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var written)
      && written.Kind != DateTimeKind.Unspecified
      && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out deadline);
  }

  /// <summary>
  /// Le corps de l'appel, tel qu'il part. La <see cref="DesignationOnTheWire"/> qu'il porte est
  /// celle-là même que le retour d'un <c>locate</c> emprunte : <b>une forme, écrite une fois</b>.
  /// </summary>
  private sealed record AdapterCallBody(IReadOnlyList<DesignationOnTheWire> Designations);

  /// <summary>
  /// Le corps d'un <c>read</c> : le sac, et le <b>droit au titre duquel on lit</b>.
  /// </summary>
  /// <remarks>
  /// <b>Aucune forme attendue n'y figure</b> — ni type de contenu souhaité, ni liste de champs, ni
  /// version de schéma —, et il n'existe aucun emplacement pour en porter une. Le droit part sous son
  /// <b>nom canonique anglais</b>, comme sur tout fil de ce dépôt.
  /// </remarks>
  private sealed record AdapterReadBody(IReadOnlyList<DesignationOnTheWire> Designations, DataSubjectRight Right);

  /// <summary>Ce qu'un différé porte : une échéance déclarée, et rien d'autre.</summary>
  private sealed record DeferredOnTheWire(string? Deadline);
}
