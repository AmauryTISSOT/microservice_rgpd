using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Requests.DeleteDataSubjectRequest;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;
using MicroserviceRgpd.UseCases.Requests.RecordDataSubjectRequest;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Requests;

/// <summary>
/// Le <b>tableau des demandes RGPD</b> : le nom de l'écran, le bouton « Créer une demande », le
/// tableau de toutes les demandes enregistrées, la modale de création que le serveur rend fermée,
/// avec son formulaire à ses valeurs par défaut, et la confirmation de suppression d'une demande.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>« Aujourd'hui » se lit sur l'horloge du service, à Paris</b> — voir <see cref="ParisCalendar"/>.
/// C'est la valeur par défaut et la borne haute du champ de date au chargement ; le module les
/// recalcule à chaque ouverture, pour une page restée ouverte au-delà de minuit. C'est aussi contre
/// lui que la date limite de chaque ligne se signale — et ces signalements-là, le module ne les
/// recalcule pas : une page ouverte au-delà de minuit garde ceux de la veille (ADR-0021).
/// </para>
/// <para>
/// ⚠️ <b>La création est un handler de la page, pas une API.</b> Le script de la modale l'appelle
/// par <c>fetch</c>, avec le jeton anti-rejeu que la page rend : aucune route publique n'enregistre
/// une demande, et le document Swagger n'en dit rien.
/// </para>
/// </remarks>
public class BoardModel(TimeProvider clock, IMediator mediator) : PageModel
{
  /// <summary>
  /// Les droits qu'une personne peut invoquer, dans l'ordre des articles. ⚠️ Jamais
  /// <see cref="DataSubjectRight.OutOfScope"/> : c'est un verdict de qualification, pas un droit.
  /// </summary>
  public static IReadOnlyList<DataSubjectRight> InvocableRights { get; } =
  [
    .. DataSubjectRight.List
      .Where(right => right != DataSubjectRight.OutOfScope)
      .OrderBy(right => right.Value),
  ];

  /// <summary>Les canaux d'arrivée, l'email d'abord : c'est l'origine par défaut.</summary>
  public static IReadOnlyList<Origin> Origins { get; } = [.. Origin.List.OrderBy(origin => origin.Value)];

  /// <summary>Aujourd'hui à Paris, à l'instant où la page est rendue.</summary>
  public DateOnly Today { get; private set; }

  /// <summary>
  /// Les lignes de toutes les demandes enregistrées, <b>déjà dans l'ordre par défaut</b> : le serveur
  /// rend le tableau entier, sans chargement asynchrone.
  /// </summary>
  public IReadOnlyList<RequestRow> Rows { get; private set; } = [];

  public async Task OnGetAsync(CancellationToken cancellationToken)
  {
    Today = ParisCalendar.Today(clock);

    var recorded = await mediator.Send(new ReadDataSubjectRequestsQuery(), cancellationToken);

    Rows = [.. recorded.Select(request => RequestRow.Of(request, Today))];
  }

  /// <summary>
  /// <b>Enregistre une demande</b> et répond 201, avec pour corps <b>la ligne de la nouvelle
  /// demande</b> — ou 400 <c>ValidationProblem</c>, les refus indexés par les clés du corps, sans rien
  /// avoir enregistré.
  /// </summary>
  /// <remarks>
  /// Ni redirection ni page en retour : c'est le script qui appelle, et c'est lui qui insère la ligne
  /// dans le tableau, ferme la modale et dit « Demande créée ». ⚠️ La ligne est rendue par la vue
  /// partielle <c>_RequestRow</c>, celle du tableau : le script n'en écrit aucun mot.
  /// </remarks>
  public async Task<IActionResult> OnPostCreateAsync(RequestForm form, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(form);

    if (form.ToEntry() is not { } entry)
    {
      return ValidationProblem(new Dictionary<string, string[]>
      {
        [DataSubjectRequestField.Origin] = [$"« {form.Origin} » n'est pas une origine."],
      });
    }

    var recorded = await mediator.Send(new RecordDataSubjectRequestCommand(entry), cancellationToken);

    if (!recorded.IsSuccess)
    {
      return ValidationProblem(recorded.ValidationErrors
        .GroupBy(refusal => refusal.Identifier)
        .ToDictionary(field => field.Key, field => field.Select(refusal => refusal.ErrorMessage).ToArray()));
    }

    // La ligne du 201 se signale comme celles du tableau : contre « aujourd'hui », relu ici (ADR-0021).
    var row = Partial("_RequestRow", RequestRow.Of(recorded.Value, ParisCalendar.Today(clock)));
    row.StatusCode = StatusCodes.Status201Created;

    return row;
  }

  /// <summary>
  /// <b>Supprime une demande</b>, quel que soit son statut, et répond 204 — ou 404 quand elle n'existe
  /// plus, ou 400 quand l'identifiant n'en est pas un.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La suppression ne laisse aucune trace</b> (ADR-0022), et comme la création, elle est un
  /// handler de la page, pas une API : c'est le script de la confirmation qui l'appelle, avec le jeton
  /// anti-rejeu que la page rend.
  /// </remarks>
  public async Task<IActionResult> OnPostDeleteAsync(string? id, CancellationToken cancellationToken)
  {
    // L'écran ne poste que l'identifiant qu'il a rendu : un autre n'est pas une saisie, mais un envoi forgé.
    if (!Guid.TryParse(id, out var guid) || !DataSubjectRequestId.TryFrom(guid, out var dataSubjectRequest))
    {
      return BadRequest();
    }

    var outcome = await mediator.Send(new DeleteDataSubjectRequestCommand(dataSubjectRequest), cancellationToken);

    // ⚠️ Seul « introuvable » devient 404 : l'écran le lit comme une réussite (ADR-0022), et un autre
    // échec ne doit pas s'y faire passer pour une suppression.
    return outcome.Status switch
    {
      ResultStatus.Ok => StatusCode(StatusCodes.Status204NoContent),
      ResultStatus.NotFound => NotFound(),
      _ => StatusCode(StatusCodes.Status500InternalServerError),
    };
  }

  private static ObjectResult ValidationProblem(IDictionary<string, string[]> errors) =>
    new(new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest })
    {
      StatusCode = StatusCodes.Status400BadRequest,
    };
}
