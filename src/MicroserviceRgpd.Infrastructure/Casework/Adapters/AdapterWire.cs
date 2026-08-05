using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using Polly.Timeout;

namespace MicroserviceRgpd.Infrastructure.Casework.Adapters;

/// <summary>
/// La forme du contrat d'<c>Adapter</c> sur le fil, écrite <b>une fois</b> : l'en-tête qui porte le
/// secret, le paramètre qui porte le système, et l'adresse d'une opération.
/// </summary>
/// <remarks>
/// Elle vit ici plutôt qu'en deux exemplaires chez l'appel et chez la sonde : ce que le développeur
/// du client implémente est <b>une</b> forme, et deux copies finiraient par ne plus décrire la même
/// — la seconde dérivant sans que rien, du côté du service, ne s'en aperçoive.
/// </remarks>
public static class AdapterWire
{
  /// <summary>
  /// L'en-tête qui porte le secret. Un en-tête propre plutôt qu'<c>Authorization</c> : le contrat
  /// n'a ni schéma, ni jeton, ni porteur à présenter, et emprunter le mot ferait croire à un
  /// <c>Bearer</c> que personne n'émet ni ne valide.
  /// </summary>
  public const string SecretHeader = "X-RGPD-Secret";

  /// <summary>Le paramètre qui porte le système — <b>en paramètre, jamais en corps</b>.</summary>
  public const string SystemParameter = "system_id";

  /// <summary>
  /// L'adresse d'une opération : l'adresse déclarée, <b>une opération par <see cref="Capability"/></b>,
  /// et le <c>system_id</c> en paramètre — jamais en corps, pour qu'un seul <c>Adapter</c> puisse
  /// servir plusieurs systèmes sans les démêler lui-même.
  /// </summary>
  public static Uri AddressOf(AdapterAddress address, DeclaredSystemId declaredSystem, Capability capability)
  {
    ArgumentNullException.ThrowIfNull(capability);

    // L'identifiant du système est déjà d'un jeu de caractères sûr en URL ; il est échappé quand
    // même, l'inverse étant une exception à retenir de tête à chaque nouvelle traversée.
    return new Uri(
      $"{address.Value.TrimEnd('/')}/{capability.Token}"
      + $"?{SystemParameter}={Uri.EscapeDataString(declaredSystem.Value)}",
      UriKind.Absolute);
  }

  /// <summary>
  /// Porte l'aller-retour, et traduit en <see cref="AdapterFailure"/> nommée ce qui n'arrive pas
  /// jusqu'à une réponse. <b>Une seule tentative</b>, ici comme partout : le contrat promet à
  /// l'intégrateur que le service ne relance jamais tout seul.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Les deux pannes de transport sont reconnues une fois, et non chez chaque appelant.</b> Un
  /// serveur muet et une échéance dépassée arrivent sous des exceptions du transport ; les laisser
  /// remonter telles quelles obligerait chaque appelant à les reconnaître au milieu d'autre chose,
  /// et deux copies de cette reconnaissance finiraient par ne plus dire la même chose.
  /// </para>
  /// <para>
  /// <b>L'annulation, elle, n'est pas rattrapée</b> : un appelant parti n'est pas un <c>Adapter</c>
  /// en panne, et la compter comme telle ferait porter à l'application du client un désaccord dont
  /// elle n'est pas l'auteur.
  /// </para>
  /// </remarks>
  /// <param name="client">Le client sur lequel l'échange part.</param>
  /// <param name="request">Ce qui part.</param>
  /// <param name="declaredSystem">Le système à nommer si rien ne revient.</param>
  /// <param name="completion">Jusqu'où lire la réponse — une sonde n'a besoin que des en-têtes.</param>
  /// <param name="cancellationToken">L'annulation de l'échange en cours.</param>
  /// <exception cref="AdapterFailure">L'<c>Adapter</c> n'a rendu ni réponse ni refus.</exception>
  public static async Task<HttpResponseMessage> AnswerTo(
    HttpClient client,
    HttpRequestMessage request,
    DeclaredSystemId declaredSystem,
    HttpCompletionOption completion,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(client);

    try
    {
      return await client.SendAsync(request, completion, cancellationToken);
    }
    catch (TimeoutRejectedException tooSlow)
    {
      // L'échéance du service est passée sans que l'Adapter ait ni servi, ni différé, ni refusé.
      // C'est précisément ce que le 202 existe pour éviter : un travail long se déclare, il ne se
      // fait pas attendre.
      throw new AdapterFailure(
        $"L'Adapter de « {declaredSystem.Value} » n'a rien répondu dans l'échéance que le service "
        + "lui laisse : un travail long se répond par un 202 et son échéance déclarée.",
        tooSlow);
    }
    catch (HttpRequestException unreachable)
    {
      throw new AdapterFailure(
        $"L'Adapter de « {declaredSystem.Value} » n'a pas répondu : ni réponse, ni refus.",
        unreachable);
    }
  }
}
