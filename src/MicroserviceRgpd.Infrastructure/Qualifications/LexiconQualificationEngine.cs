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

  /// <summary>Le nom sous lequel ce moteur se déclare en panne, pour que l'exploitant sache où réparer.</summary>
  private const string Engine = "Le moteur lexical";

  /// <inheritdoc />
  public async Task<QualificationOpinion> QualifyAsync(
    RightsRequestText text,
    CancellationToken cancellationToken = default)
  {
    var opinion = await SidecarExchange.AskAsync<LexiconOpinionResponse>(
      client, Endpoint, Engine, text, cancellationToken);

    if (opinion.Rights is null)
    {
      throw new QualificationEngineFailure($"{Engine} a répondu sans aucun droit : ce n'est pas un avis.");
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
        $"{Engine} a rendu un avis que le domaine refuse : {invalid.Message}", invalid);
    }
  }

  /// <summary>
  /// L'avis tel qu'il arrive. L'identité du moteur voyage aussi sur le fil ; elle n'est pas lue ici
  /// parce qu'il n'existe encore rien qui la conserve — la trace d'audit lui donnera sa place.
  /// </summary>
  private sealed record LexiconOpinionResponse(IReadOnlyList<DataSubjectRight>? Rights);
}
