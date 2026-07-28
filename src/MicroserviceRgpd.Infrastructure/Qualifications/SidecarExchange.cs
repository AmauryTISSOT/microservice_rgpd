using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MicroserviceRgpd.Core.Qualifications;
using Polly.Timeout;

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
  /// Demande son avis à un point d'entrée du sidecar, et fait de sa réponse un avis du domaine.
  /// </summary>
  /// <remarks>
  /// <para>
  /// L'échange entier vit sous une trace à lui, qui <b>nomme le moteur et son mode de panne</b> :
  /// c'est le seul canal par lequel un échec se compte, la trace d'audit n'enregistrant que les
  /// verdicts rendus. Elle couvre donc aussi la lecture, et non le seul transport — un avis refusé
  /// parce qu'il lui manque une confiance est un échec du moteur exactement comme un serveur muet.
  /// </para>
  /// <para>
  /// La lecture voyage en paramètre parce qu'elle est <b>tout ce que les deux moteurs ne partagent
  /// pas</b> : le LLM exige de son avis une confiance et une justification, le lexique les interdit.
  /// La faire entrer ici ferait un gardien unique de deux contrats qui n'ont pas la même forme.
  /// </para>
  /// </remarks>
  /// <exception cref="QualificationEngineDeadlineExceeded">
  /// Le moteur n'a pas répondu dans son échéance — la sienne, ou celle du client qui l'appelle.
  /// </exception>
  /// <exception cref="QualificationEngineFailure">
  /// Le moteur n'a pas rendu d'avis : statut d'échec, réponse illisible, corps vide, ou avis que son
  /// propre contrat refuse.
  /// </exception>
  internal static async Task<QualificationOpinion> AskAsync<TResponse>(
    HttpClient client,
    string endpoint,
    string engine,
    RightsRequestText text,
    Func<TResponse, QualificationOpinion> read,
    CancellationToken cancellationToken)
  {
    using var exchange = QualificationTelemetry.Source.StartActivity(endpoint, ActivityKind.Client);

    exchange?.SetTag(QualificationTelemetry.EndpointTag, endpoint);

    try
    {
      return read(await ExchangeAsync<TResponse>(client, endpoint, engine, text, cancellationToken));
    }
    catch (QualificationEngineFailure silence)
    {
      // L'annulation, elle, n'est pas rattrapée : un appelant parti n'est pas un moteur en panne, et
      // la compter comme telle gonflerait le seul chiffre d'exploitation dont on dispose.
      Record(exchange, silence);

      throw;
    }
  }

  /// <summary>
  /// L'échange lui-même : porter le texte, et faire de tout ce qui n'est pas un avis une panne nommée.
  /// </summary>
  private static async Task<TResponse> ExchangeAsync<TResponse>(
    HttpClient client,
    string endpoint,
    string engine,
    RightsRequestText text,
    CancellationToken cancellationToken)
  {
    HttpResponseMessage response;

    try
    {
      // Le texte, et rien d'autre : pas d'identifiant — la corrélation passe par `traceparent`,
      // propagé par ServiceDefaults — et pas de langue, le français étant la seule option.
      response = await client.PostAsJsonAsync(
        endpoint,
        new OpinionRequest(text.Value),
        WireFormat,
        cancellationToken);
    }
    // L'échéance du pipeline est passée avant celle de l'appelant. Anormale par construction — celle
    // du sidecar vers son amont est strictement plus courte —, elle dit donc que le sidecar lui-même
    // n'a pas rendu la main, et elle mérite d'arriver nommée plutôt que sous une annulation muette.
    catch (TimeoutRejectedException tooSlow)
    {
      throw new QualificationEngineDeadlineExceeded(
        $"{engine} n'a pas répondu dans l'échéance que le service lui laisse.", tooSlow);
    }

    using (response)
    {
      if (!response.IsSuccessStatusCode)
      {
        // Le sidecar a renoncé à attendre son amont et le dit par un code à lui : c'est ce que
        // l'ordre strict des deux échéances achète, et le perdre ici rendrait la lenteur anonyme.
        throw response.StatusCode is HttpStatusCode.GatewayTimeout
          ? new QualificationEngineDeadlineExceeded(
            $"{engine} a répondu {(int)response.StatusCode} : son échéance est passée avant qu'il ne rende un avis.")
          : new QualificationEngineFailure(
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
  }

  /// <summary>Inscrit la panne dans la trace, en distinguant la lenteur du reste.</summary>
  private static void Record(Activity? exchange, QualificationEngineFailure silence)
  {
    exchange?.SetStatus(ActivityStatusCode.Error, silence.Message);
    exchange?.SetTag(
      QualificationTelemetry.FailureTag,
      silence is QualificationEngineDeadlineExceeded
        ? QualificationTelemetry.DeadlineFailure
        : QualificationTelemetry.EngineFailure);
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
