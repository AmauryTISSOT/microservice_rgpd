using System.Text.Json;
using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.Infrastructure.Requests;

/// <summary>
/// <b>Le corps tel qu'il part au système hôte</b> : cinq clés en camelCase, le droit sous son nom
/// canonique, l'identifiant en texte (ADR-0026).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Un seul contrat pour l'intégrateur, quel que soit le canal</b> (ADR-0028) : l'appel HTTP et
/// la publication RabbitMQ portent <b>exactement</b> les mêmes valeurs, sérialisées de la même
/// façon. Ce type existe pour que ce soit vrai par construction plutôt que par vigilance — recopié
/// dans chaque adaptateur, le corps aurait divergé au premier champ ajouté.
/// </para>
/// <para>
/// ⚠️ <b>Les nuls sont écrits</b> : un prénom ou un nom absent part à <c>null</c>, pour que les cinq
/// clés soient toujours présentes.
/// </para>
/// </remarks>
/// <param name="RequestId">La demande exécutée, en texte.</param>
/// <param name="Right">Le droit invoqué, sous son nom canonique.</param>
/// <param name="Email">L'email de la personne.</param>
/// <param name="FirstName">Le prénom de la personne, ou <c>null</c>.</param>
/// <param name="LastName">Le nom de la personne, ou <c>null</c>.</param>
internal sealed record ExecutionWireBody(
  string RequestId,
  string Right,
  string Email,
  string? FirstName,
  string? LastName)
{
  /// <summary>Les clés en camelCase, comme partout ailleurs sur le fil.</summary>
  private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

  /// <summary>Le corps de <paramref name="body"/>, sérialisé — la forme qu'un appel HTTP prend.</summary>
  public static string Of(ExecutionBody body) => JsonSerializer.Serialize(Projected(body), Wire);

  /// <summary>Le même corps, en octets UTF-8 — la forme qu'une publication prend.</summary>
  public static ReadOnlyMemory<byte> BytesOf(ExecutionBody body) =>
    JsonSerializer.SerializeToUtf8Bytes(Projected(body), Wire);

  /// <summary>Les cinq valeurs de <paramref name="body"/>, telles qu'elles partent.</summary>
  private static ExecutionWireBody Projected(ExecutionBody body)
  {
    ArgumentNullException.ThrowIfNull(body);

    return new ExecutionWireBody(
      body.RequestId.Value.ToString(),
      body.Right.Name,
      body.Email.Value,
      body.FirstName?.Value,
      body.LastName?.Value);
  }
}
