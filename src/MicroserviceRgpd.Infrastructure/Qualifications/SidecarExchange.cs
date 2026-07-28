using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.Infrastructure.Qualifications;

/// <summary>
/// Ce que les deux adaptateurs de moteur ont en commun : porter un texte jusqu'à un point d'entrée
/// du sidecar, en rapporter une réponse lisible, et faire de ses droits un verdict du domaine — ou
/// une panne nommée.
/// </summary>
/// <remarks>
/// <para>
/// Est mis en commun ce que <b>les deux moteurs promettent pareillement</b> : le transport, et des
/// droits qui satisfont les invariants du domaine. Ce que le seul moteur LLM promet en plus — une
/// confiance déclarée, une justification — reste dans son adaptateur : l'y faire entrer ferait un
/// gardien unique de deux contrats qui n'ont pas la même forme, et rendrait exprimable un avis
/// lexical assorti d'une confiance, que le domaine interdit.
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
    // `allowIntegerValues: false` n'est pas un durcissement décoratif : sans lui, un `2` sur le fil
    // vaudrait « haute », et le contrat interne dépendrait de l'ordre de déclaration d'un `enum`
    // que personne des deux côtés ne pense à tenir stable.
    Converters = { new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false) },
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

  /// <summary>
  /// Fait des droits arrivés sur le fil un verdict du domaine, ou nomme la panne du moteur.
  /// </summary>
  /// <remarks>
  /// Les invariants sont <b>re-portés de ce côté-ci de la frontière</b>. Ce n'est pas de la défiance
  /// gratuite : le sidecar les tient déjà, mais un adaptateur qui leur ferait confiance laisserait
  /// passer un avis boiteux le jour où l'autre bout se tromperait — et c'est ce qui garantit la
  /// promesse du port, un avis ou rien.
  /// </remarks>
  /// <exception cref="QualificationEngineFailure">
  /// Aucun droit n'est arrivé, ou ceux qui sont arrivés ne font pas un verdict que le domaine accepte.
  /// </exception>
  internal static Qualification VerdictOf(IReadOnlyList<DataSubjectRight>? rights, string engine)
  {
    if (rights is null)
    {
      throw new QualificationEngineFailure($"{engine} a répondu sans aucun droit : ce n'est pas un avis.");
    }

    try
    {
      return Qualification.Of(rights);
    }
    catch (ArgumentException invalid)
    {
      throw new QualificationEngineFailure(
        $"{engine} a rendu un avis que le domaine refuse : {invalid.Message}", invalid);
    }
  }

  /// <summary>Le texte, seul champ du contrat interne — qui se resserre plutôt qu'il ne tolère.</summary>
  private sealed record OpinionRequest(string Text);
}
