using System.Net.Http.Json;
using System.Text.Json;
using MicroserviceRgpd.Core.Qualifications;

namespace MicroserviceRgpd.Infrastructure.Qualifications;

/// <summary>
/// Le moteur témoin, vu du domaine : un lexique déterministe qui vit dans le sidecar Python et
/// qu'on joint en HTTP.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cet adaptateur est du transport, et rien de plus.</b> Il ne qualifie rien, ne pondère rien,
/// n'arbitre rien : il porte un texte jusqu'au moteur et rapporte un avis. Toute la matière de
/// décision vit dans le domaine, hors d'atteinte de HTTP.
/// </para>
/// <para>
/// Il <b>re-porte les invariants</b> que le sidecar tient déjà. Ce n'est pas de la défiance
/// gratuite : c'est ce qui garantit la promesse du port — un avis, ou rien — même le jour où
/// l'autre bout se trompera. Ce qui n'est pas un avis valide ressort en
/// <see cref="QualificationEngineFailure"/>, jamais en verdict boiteux qu'un appelant devrait
/// deviner.
/// </para>
/// </remarks>
public sealed class LexiconQualificationEngine(HttpClient client) : IQualificationEngine
{
  /// <summary>Le point d'entrée du seul moteur qui n'a aucun amont — donc aucune panne d'amont.</summary>
  private const string Endpoint = "opinions/lexicon";

  /// <summary>
  /// Le fil parle <c>camelCase</c> des deux côtés. Les droits, eux, se lisent par le convertisseur
  /// attaché à la taxonomie : un nom hors des sept est refusé à la lecture, pas plus tard.
  /// </summary>
  private static readonly JsonSerializerOptions WireFormat = JsonSerializerOptions.Web;

  /// <inheritdoc />
  public async Task<QualificationOpinion> QualifyAsync(
    RightsRequestText text,
    CancellationToken cancellationToken = default)
  {
    // Le texte, et rien d'autre : pas d'identifiant — la corrélation passe par `traceparent`,
    // propagé par ServiceDefaults — et pas de langue, le français étant la seule option.
    using var response = await client.PostAsJsonAsync(
      Endpoint,
      new LexiconOpinionRequest(text.Value),
      WireFormat,
      cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
      throw new QualificationEngineFailure(
        $"Le moteur lexical a répondu {(int)response.StatusCode} au lieu de rendre un avis.");
    }

    LexiconOpinionResponse? opinion;

    try
    {
      opinion = await response.Content.ReadFromJsonAsync<LexiconOpinionResponse>(WireFormat, cancellationToken);
    }
    catch (JsonException illegible)
    {
      throw new QualificationEngineFailure(
        "Le moteur lexical a répondu autre chose qu'un avis lisible.", illegible);
    }

    if (opinion?.Rights is null)
    {
      throw new QualificationEngineFailure("Le moteur lexical a répondu sans aucun droit : ce n'est pas un avis.");
    }

    try
    {
      // Aucune confiance, aucune justification : le lexique n'a pas d'avis sur sa propre fiabilité,
      // et une constante lui en donnerait l'apparence — quelqu'un finirait par écrire une règle
      // qui la consomme.
      return new QualificationOpinion(Qualification.Of(opinion.Rights));
    }
    catch (ArgumentException invalid)
    {
      throw new QualificationEngineFailure(
        $"Le moteur lexical a rendu un avis que le domaine refuse : {invalid.Message}", invalid);
    }
  }

  /// <summary>Le texte, seul champ du contrat interne — qui se resserre plutôt qu'il ne tolère.</summary>
  private sealed record LexiconOpinionRequest(string Text);

  /// <summary>
  /// L'avis tel qu'il arrive. L'identité du moteur voyage aussi sur le fil ; elle n'est pas lue ici
  /// parce qu'il n'existe encore rien qui la conserve — la trace d'audit lui donnera sa place.
  /// </summary>
  private sealed record LexiconOpinionResponse(IReadOnlyList<DataSubjectRight>? Rights);
}
