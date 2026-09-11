using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;
using MicroserviceRgpd.UseCases.Requests.RecordDataSubjectRequest;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MicroserviceRgpd.Web.Pages.Requests;

/// <summary>
/// Le <b>tableau des demandes RGPD</b> : le nom de l'écran, le bouton « Créer une demande », le
/// tableau de toutes les demandes enregistrées, et la modale de création que le serveur rend fermée,
/// avec son formulaire à ses valeurs par défaut.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>« Aujourd'hui » se lit sur l'horloge du service, à Paris</b> — voir <see cref="ParisCalendar"/>.
/// C'est la valeur par défaut et la borne haute du champ de date au chargement ; le module les
/// recalcule à chaque ouverture, pour une page restée ouverte au-delà de minuit.
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

    Rows = [.. recorded.Select(RequestRow.Of)];
  }

  /// <summary>
  /// <b>Enregistre une demande</b> et répond 201 — ou 400 <c>ValidationProblem</c>, les refus indexés
  /// par les clés du corps, sans rien avoir enregistré.
  /// </summary>
  /// <remarks>
  /// Ni redirection ni page en retour : c'est le script qui appelle, et c'est lui qui ferme la modale
  /// et dit « Demande créée ».
  /// </remarks>
  public async Task<IActionResult> OnPostCreateAsync(CreationForm form, CancellationToken cancellationToken)
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

    return StatusCode(StatusCodes.Status201Created);
  }

  private static ObjectResult ValidationProblem(IDictionary<string, string[]> errors) =>
    new(new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest })
    {
      StatusCode = StatusCodes.Status400BadRequest,
    };
}
