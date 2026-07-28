using System.Net.Http.Json;
using System.Text.Json;
using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.Infrastructure.Qualifications;

/// <summary>
/// L'échange HTTP que les deux adaptateurs de moteur ont en commun : porter un texte jusqu'à un
/// point d'entrée du sidecar, et en rapporter une réponse lisible — ou une panne nommée.
/// </summary>
/// <remarks>
/// <para>
/// Ce qui est mis en commun est le <b>transport, et lui seul</b>. Ce que chaque moteur promet de
/// plus — des droits pour le lexique, une confiance et une justification en sus pour le LLM — reste
/// dans son adaptateur : c'est là que les invariants du domaine sont re-portés, et les y fondre
/// ferait un gardien unique de deux contrats qui n'ont pas la même forme.
/// </para>
/// <para>
/// Le nom du moteur voyage en paramètre pour que la panne dise <b>qui</b> n'a pas rendu d'avis.
/// L'exploitant ne répare pas au même endroit selon la réponse.
/// </para>
/// </remarks>
internal static class SidecarExchange
{
  /// <summary>
  /// Le fil parle <c>camelCase</c> des deux côtés. Les droits, eux, se lisent par le convertisseur
  /// attaché à la taxonomie : un nom hors des sept est refusé à la lecture, pas plus tard. Les
  /// degrés de confiance se lisent par leur nom, jamais par un ordinal — un ordinal ferait dépendre
  /// le fil de l'ordre de déclaration d'un <c>enum</c>.
  /// </summary>
  private static readonly JsonSerializerOptions WireFormat = new(JsonSerializerOptions.Web)
  {
    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
  };

  /// <summary>
  /// Demande son avis à un point d'entrée du sidecar, et rend la réponse telle qu'elle est arrivée.
  /// </summary>
  /// <exception cref="QualificationEngineFailure">
  /// Le moteur n'a pas rendu d'avis : statut d'échec, réponse illisible, ou corps vide.
  /// </exception>
  internal static async Task<TResponse> AskAsync<TResponse>(
    HttpClient client,
    string endpoint,
    string engine,
    RightsRequestText text,
    CancellationToken cancellationToken)
  {
    // Le texte, et rien d'autre : pas d'identifiant — la corrélation passe par `traceparent`,
    // propagé par ServiceDefaults — et pas de langue, le français étant la seule option.
    using var response = await client.PostAsJsonAsync(
      endpoint,
      new OpinionRequest(text.Value),
      WireFormat,
      cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      throw new QualificationEngineFailure(
        $"{engine} a répondu {(int)response.StatusCode} au lieu de rendre un avis.");
    }

    TResponse? opinion;

    try
    {
      opinion = await response.Content.ReadFromJsonAsync<TResponse>(WireFormat, cancellationToken);
    }
    catch (JsonException illegible)
    {
      throw new QualificationEngineFailure($"{engine} a répondu autre chose qu'un avis lisible.", illegible);
    }

    return opinion ?? throw new QualificationEngineFailure($"{engine} a répondu un corps vide, qui n'est pas un avis.");
  }

  /// <summary>Le texte, seul champ du contrat interne — qui se resserre plutôt qu'il ne tolère.</summary>
  private sealed record OpinionRequest(string Text);
}
