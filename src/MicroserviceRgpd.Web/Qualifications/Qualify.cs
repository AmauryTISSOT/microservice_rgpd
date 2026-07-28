using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.UseCases.Qualifications.Qualify;

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
public class Qualify(IMediator mediator) : Endpoint<QualifyRequest, QualifyResponse>
{
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

    var result = await mediator.Send(command, cancellationToken);

    if (result.Status != ResultStatus.Ok)
    {
      // Inatteignable tant qu'un seul des deux moteurs suffit à qualifier : les statuts d'échec
      // arriveront avec les échéances, les deux codes de double panne et la trace d'audit. Le
      // refuser plutôt que le supposer évite qu'un statut ajouté plus tard sorte en 200 avec un
      // corps vide.
      throw new InvalidOperationException(
        $"La qualification a rendu un statut que l'endpoint ne sait pas traduire : {result.Status}.");
    }

    await Send.OkAsync(QualifyResponse.From(result.Value), cancellationToken);
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
