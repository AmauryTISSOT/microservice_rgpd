using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.UseCases.Qualifications.Qualify;
using Microsoft.AspNetCore.Http.Features;

// FastEndpoints porte un type du même nom, réservé à ses propres réponses d'erreur. Celui de la
// plateforme est le seul que `IProblemDetailsService` sait écrire — donc le seul qui reçoive la
// référence de diagnostic posée une fois pour toute l'API.
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace MicroserviceRgpd.Web.Qualifications;

/// <summary>
/// Qualifie un texte reçu d'une application tierce, et rend le verdict <b>dans le même échange</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Un <c>POST</c>, un <c>200</c>, et aucun <c>GET</c>.</b> Un <c>201 Created</c> avec
/// <c>Location</c> ferait de la trace d'audit une ressource métier exposée — précisément l'entité
/// que la conception a refusée — et un <c>Location</c> qui ne mène nulle part est un mensonge de
/// contrat. La qualification n'est pas une ressource qu'on relit : c'est un acte dont on repart
/// avec le résultat.
/// </para>
/// <para>
/// <b>Pas de segment de version dans le chemin.</b> Dette réelle et assumée : le jour où une v2
/// arrive, <c>POST /qualifications</c> restera l'alias permanent de la v1 et le segment ne sera
/// exigé que des nouveaux appelants.
/// </para>
/// </remarks>
public class Qualify(IMediator mediator, IProblemDetailsService problemDetails)
  : Endpoint<QualifyRequest, QualifyResponse>
{
  /// <summary>
  /// La double panne dont le moteur principal a dépassé son échéance. <b>Une branche explicite</b>,
  /// et une entorse assumée au mapping standard : la bibliothèque de résultats n'a aucun statut de
  /// délai dépassé, et ce code est payé parce que la réaction attendue diffère — une indisponibilité
  /// invite à réessayer plus tard, un dépassement dit que le travail a commencé sans aboutir, et
  /// qu'à température nulle avec seed fixe un simple rejeu redonnera le même dépassement.
  /// </summary>
  private const int GatewayTimeout = StatusCodes.Status504GatewayTimeout;

  /// <summary>Toute autre double panne : le service n'a rien à qualifier, et le dit.</summary>
  private const int ServiceUnavailable = StatusCodes.Status503ServiceUnavailable;

  /// <inheritdoc />
  public override void Configure()
  {
    Post("/qualifications");
    AllowAnonymous();

    Summary(summary =>
    {
      summary.Summary = "Qualifie un texte au regard des droits RGPD";
      summary.Description =
        "Reçoit un texte libre en français et rend, dans le même échange, les droits que ce texte " +
        "est jugé exercer. C'est une aide à la décision : un humain valide ou corrige le verdict.";
      summary.Responses[200] = "Qualification rendue";
      summary.Responses[400] = "Texte absent, vide ou trop long, ou référence appelante invalide";
      summary.Responses[ServiceUnavailable] =
        "Qualification indisponible : les deux moteurs sont restés muets. La cause réaliste n'est " +
        "pas qu'un serveur de modèles soit éteint — ce cas rend un 200 dégradé — mais que le " +
        "sidecar entier soit mort. Ce code est donc quasi inatteignable en usage normal.";
      summary.Responses[GatewayTimeout] =
        "Même double panne, le moteur principal ayant en outre dépassé son échéance. Rejouer la " +
        "requête redonnera le même dépassement : la génération est déterministe.";
    });

    Tags("Qualifications");
  }

  /// <inheritdoc />
  public override async Task HandleAsync(QualifyRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var command = new QualifyCommand(
      // La validation a déjà refusé l'absent, le vide et le démesuré : la conversion ne peut plus
      // échouer ici, et le texte entre dans le domaine par son type plutôt que par une chaîne nue.
      RightsRequestText.From(request.Text!),
      NormalizeCallerReference(request.CallerReference));

    Result<QualificationOutcome> result;

    try
    {
      result = await mediator.Send(command, cancellationToken);
    }
    catch (QualificationEngineFailure doubleFailure)
    {
      // Les deux moteurs se sont tus : il n'y a rien à qualifier, et la panne qui remonte est celle
      // du **moteur principal** — c'est son mode de défaillance qui décide du code, celle du témoin
      // n'ayant fait que priver le service de son filet. Un moteur seul muet n'arrive jamais ici :
      // le repli l'a déjà absorbé en un 200 dégradé.
      await SendDoubleFailureAsync(doubleFailure is QualificationEngineDeadlineExceeded);

      return;
    }

    if (result.Status != ResultStatus.Ok)
    {
      // Inatteignable tant qu'un seul des deux moteurs suffit à qualifier : les statuts d'échec
      // arriveront avec la trace d'audit. Le refuser plutôt que le supposer évite qu'un statut
      // ajouté plus tard sorte en 200 avec un corps vide.
      throw new InvalidOperationException(
        $"La qualification a rendu un statut que l'endpoint ne sait pas traduire : {result.Status}.");
    }

    await Send.OkAsync(QualifyResponse.From(result.Value), cancellationToken);
  }

  /// <summary>
  /// Rend la double panne dans la <b>seule forme d'erreur de l'API</b>, assortie de la référence de
  /// diagnostic.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Aucune qualification n'a eu lieu : il n'y a rien à référencer dans l'audit — la trace
  /// n'enregistre que les verdicts rendus —, et la seule identité qui vaille est donc celle du
  /// diagnostic. Elle est posée par le même réglage que sur les erreurs de la plateforme, plutôt que
  /// recopiée ici, pour qu'un ticket de support cite toujours la même chose.
  /// </para>
  /// <para>
  /// <b>Le message du moteur ne franchit pas cette frontière.</b> Il nomme le moteur qui s'est tu,
  /// ce dont l'exploitant a besoin et ce que le contrat public tait délibérément ; il vit dans les
  /// traces, que la référence de diagnostic permet précisément de rejoindre.
  /// </para>
  /// </remarks>
  private async Task SendDoubleFailureAsync(bool tooSlow)
  {
    HttpContext.Response.StatusCode = tooSlow ? GatewayTimeout : ServiceUnavailable;

    await problemDetails.WriteAsync(new ProblemDetailsContext
    {
      HttpContext = HttpContext,
      ProblemDetails = new ProblemDetails
      {
        Status = HttpContext.Response.StatusCode,
        Title = tooSlow
          ? "La qualification n'a pas abouti dans le délai imparti"
          : "La qualification est indisponible",
        Detail = tooSlow
          ? "Aucun moteur n'a rendu d'avis, le moteur principal ayant dépassé son échéance. Rejouer "
            + "la requête telle quelle redonnera le même dépassement."
          : "Aucun moteur n'a rendu d'avis. Le service ne peut rien qualifier pour l'instant.",
      },
    });
  }

  /// <summary>
  /// Nettoie les bordures, et rien d'autre. Une référence vide une fois nettoyée <b>vaut absente</b> :
  /// rendre <c>""</c> ferait croire à l'appelant qu'il a fourni quelque chose.
  /// </summary>
  /// <remarks>
  /// Le service <b>ne l'interprète jamais</b> : ni unicité, ni format, ni sens. C'est la clé de
  /// corrélation de l'appelant, elle lui appartient.
  /// </remarks>
  private static string? NormalizeCallerReference(string? callerReference)
  {
    var trimmed = callerReference?.Trim();

    return string.IsNullOrEmpty(trimmed) ? null : trimmed;
  }
}
