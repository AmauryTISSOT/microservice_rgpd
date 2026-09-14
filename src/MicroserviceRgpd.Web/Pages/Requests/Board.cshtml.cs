using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Requests.DeleteDataSubjectRequest;
using MicroserviceRgpd.UseCases.Requests.ExecuteDataSubjectRequest;
using MicroserviceRgpd.UseCases.Requests.ModifyDataSubjectRequest;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequestValues;
using MicroserviceRgpd.UseCases.Requests.RecordDataSubjectRequest;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

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
public class BoardModel(TimeProvider clock, IMediator mediator, IRazorViewEngine views) : PageModel
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
  /// <b>Rend les huit valeurs saisies d'une demande</b> en JSON, sous les clés mêmes du formulaire,
  /// et répond 200 — ou 404 quand la demande n'existe plus, ou 400 quand l'identifiant n'en est pas
  /// un.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>200 même sur une demande close.</b> Lire n'est pas modifier : refuser la lecture ferait de
  /// ce point de consultation le gardien d'une règle d'écriture.
  /// </para>
  /// <para>
  /// Comme la création et la suppression, c'est un handler de la page, pas une API : aucune route
  /// publique ne rend une demande, et le document Swagger n'en dit rien.
  /// </para>
  /// </remarks>
  public async Task<IActionResult> OnGetValuesAsync(string? id, CancellationToken cancellationToken)
  {
    if (ReadId(id) is not { } dataSubjectRequest)
    {
      return BadRequest();
    }

    var read = await mediator.Send(new ReadDataSubjectRequestValuesQuery(dataSubjectRequest), cancellationToken);

    if (read.Status is not ResultStatus.Ok)
    {
      return read.Status is ResultStatus.NotFound
        ? NotFound()
        : StatusCode(StatusCodes.Status500InternalServerError);
    }

    // ⚠️ Rien n'est gardé : la modale relit ces valeurs après chaque modification, et un cache de
    // navigateur lui rendrait celles d'avant.
    Response.Headers.CacheControl = "no-store";

    return new JsonResult(RequestForm.Of(read.Value));
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
      return ForgedOrigin(form);
    }

    var recorded = await mediator.Send(new RecordDataSubjectRequestCommand(entry), cancellationToken);

    if (!recorded.IsSuccess)
    {
      return ValidationProblem(recorded.ValidationErrors);
    }

    // La ligne du 201 se signale comme celles du tableau : contre « aujourd'hui », relu ici (ADR-0021).
    var row = Partial("_RequestRow", RequestRow.Of(recorded.Value, ParisCalendar.Today(clock)));
    row.StatusCode = StatusCodes.Status201Created;

    return row;
  }

  /// <summary>
  /// <b>Enregistre une correction</b> et répond 200, avec pour corps <b>la ligne mise à jour</b> — ou
  /// 400 <c>ValidationProblem</c>, les refus indexés par les clés du corps, ou 404 quand la demande
  /// n'existe plus, ou 409 quand elle est close, sans rien avoir enregistré.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Le succès n'a qu'une forme</b> : 200 et la ligne, <b>y compris quand la correction ne
  /// change rien</b>. Un second code de succès ferait un second chemin dans le script, pour une ligne
  /// qui reste correcte de toute façon — elle n'a pas changé.
  /// </para>
  /// <para>
  /// La ligne est rendue par la vue partielle <c>_RequestRow</c>, celle du tableau et celle de la
  /// création : un seul gabarit, aucune divergence d'affichage possible. Comme la création et la
  /// suppression, c'est un handler de la page, appelé par le script avec le jeton anti-rejeu que la
  /// page rend.
  /// </para>
  /// </remarks>
  public async Task<IActionResult> OnPostModifyAsync(string? id, RequestForm form, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(form);

    if (ReadId(id) is not { } dataSubjectRequest)
    {
      return BadRequest();
    }

    if (form.ToEntry() is not { } entry)
    {
      return ForgedOrigin(form);
    }

    var modified = await mediator.Send(
      new ModifyDataSubjectRequestCommand(dataSubjectRequest, entry),
      cancellationToken);

    if (modified.Status is not ResultStatus.Ok)
    {
      return modified.Status switch
      {
        ResultStatus.Invalid => ValidationProblem(modified.ValidationErrors),
        ResultStatus.NotFound => NotFound(),
        ResultStatus.Conflict => StatusCode(StatusCodes.Status409Conflict),
        _ => StatusCode(StatusCodes.Status500InternalServerError),
      };
    }

    // La ligne du 200 se signale comme celles du tableau : contre « aujourd'hui », relu ici (ADR-0021).
    return Partial("_RequestRow", RequestRow.Of(modified.Value, ParisCalendar.Today(clock)));
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
    if (ReadId(id) is not { } dataSubjectRequest)
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

  /// <summary>
  /// <b>Exécute une demande</b> — le système hôte applique le droit invoqué — et répond 200, avec pour
  /// corps <b>la ligne de la demande passée à Terminée</b> (ADR-0026).
  /// </summary>
  /// <remarks>
  /// <para>
  /// Refusée avant l'appel, sans appel ni tentative : <b>409</b> pour une demande close, <b>422</b> pour
  /// tout autre motif de blocage. Les deux portent un <c>ProblemDetails</c> dont <c>detail</c> est le
  /// motif, et dont <c>row</c> est la ligne à jour, rendue par <c>_RequestRow</c> — l'écran la remet
  /// dans le tableau sans recharger. <b>404</b> pour une demande disparue, <b>400</b> pour un
  /// identifiant illisible.
  /// </para>
  /// <para>
  /// Un appel parti qui n'aboutit pas — réponse non 2xx, délai dépassé, erreur réseau — répond
  /// <b>502</b>, avec le résultat de la tentative pour <c>detail</c> et la ligne inchangée.
  /// </para>
  /// <para>
  /// ⚠️ <b>L'annulation de la requête n'arrête pas l'appel</b> : le use case ne la propage pas au
  /// système hôte. Comme la création, la modification et la suppression, c'est un handler de la page,
  /// appelé avec le jeton anti-rejeu : aucune route publique, rien dans Swagger.
  /// </para>
  /// </remarks>
  public async Task<IActionResult> OnPostExecuteAsync(string? id, CancellationToken cancellationToken)
  {
    if (ReadId(id) is not { } dataSubjectRequest)
    {
      return BadRequest();
    }

    var executed = await mediator.Send(new ExecuteDataSubjectRequestCommand(dataSubjectRequest), cancellationToken);

    return executed.Status switch
    {
      ResultStatus.Ok when executed.Value.Call?.Outcome == ExecutionOutcome.Succeeded =>
        Partial("_RequestRow", RowOf(executed.Value)),
      ResultStatus.Ok => await ExecutionProblemAsync(
        StatusCodes.Status502BadGateway,
        executed.Value.Call?.Outcome.FrenchLabel,
        executed.Value),
      ResultStatus.Conflict or ResultStatus.Invalid => await ExecutionProblemAsync(
        executed.Status is ResultStatus.Conflict ? StatusCodes.Status409Conflict : StatusCodes.Status422UnprocessableEntity,
        executed.Value.Block?.FrenchLabelFor(executed.Value.Request.Right),
        executed.Value),
      ResultStatus.NotFound => NotFound(),
      _ => StatusCode(StatusCodes.Status500InternalServerError),
    };
  }

  /// <summary>
  /// L'identifiant d'une demande, tel qu'un handler le reçoit — <b>ou rien, et c'est un 400</b>,
  /// jamais un 404 : ce n'est pas une demande introuvable, que l'écran lirait comme une réussite
  /// (ADR-0022).
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>L'écran ne nomme que les identifiants qu'il a rendus</b> : un autre n'est pas une saisie
  /// de l'<c>Operator</c>, mais un envoi forgé.
  /// </remarks>
  private static DataSubjectRequestId? ReadId(string? id) =>
    Guid.TryParse(id, out var guid) && DataSubjectRequestId.TryFrom(guid, out var dataSubjectRequest)
      ? dataSubjectRequest
      : null;

  /// <summary>
  /// ⚠️ <b>Une origine hors des deux canaux n'est pas une saisie, c'est un envoi forgé</b> : le
  /// formulaire ne propose que <c>Email</c> et <c>Letter</c>. Le domaine ne la voit donc jamais, et
  /// c'est ici qu'elle se refuse — sous <c>origin</c>, comme les refus du domaine.
  /// </summary>
  private static ObjectResult ForgedOrigin(RequestForm form) =>
    ValidationProblem(new Dictionary<string, string[]>
    {
      [DataSubjectRequestField.Origin] = [$"« {form.Origin} » n'est pas une origine."],
    });

  /// <summary>
  /// Les refus du domaine, <b>regroupés par champ</b> : leurs identifiants sont déjà les noms des
  /// champs du formulaire, il n'y a aucune table de correspondance à tenir.
  /// </summary>
  private static ObjectResult ValidationProblem(IEnumerable<ValidationError> refusals) =>
    ValidationProblem(refusals
      .GroupBy(refusal => refusal.Identifier)
      .ToDictionary(field => field.Key, field => field.Select(refusal => refusal.ErrorMessage).ToArray()));

  /// <summary>La ligne de la demande exécutée, signalée contre « aujourd'hui », relu ici (ADR-0021).</summary>
  private RequestRow RowOf(DataSubjectRequestExecution execution) =>
    RequestRow.Of(execution.Request, ParisCalendar.Today(clock));

  /// <summary>
  /// Un <c>ProblemDetails</c> d'exécution : <paramref name="detail"/> pour <c>detail</c>, et la ligne à
  /// jour sous <c>row</c>, en HTML.
  /// </summary>
  private async Task<ObjectResult> ExecutionProblemAsync(int status, string? detail, DataSubjectRequestExecution execution) =>
    new(new Microsoft.AspNetCore.Mvc.ProblemDetails
    {
      Status = status,
      Detail = detail,
      Extensions = { ["row"] = await RenderedRowAsync(RowOf(execution)) },
    })
    {
      StatusCode = status,
    };

  /// <summary>
  /// La ligne rendue par <c>_RequestRow</c>, <b>en texte</b> — la même vue partielle que celle du
  /// tableau, pour qu'un refus rende la ligne au caractère près.
  /// </summary>
  private async Task<string> RenderedRowAsync(RequestRow row)
  {
    var found = views.FindView(PageContext, "_RequestRow", isMainPage: false);

    if (!found.Success)
    {
      throw new InvalidOperationException("La vue partielle _RequestRow est introuvable.");
    }

    await using var writer = new StringWriter();

    var context = new ViewContext(
      PageContext,
      found.View,
      new ViewDataDictionary<RequestRow>(ViewData, row),
      TempData,
      writer,
      new HtmlHelperOptions());

    await found.View.RenderAsync(context);

    return writer.ToString();
  }

  private static ObjectResult ValidationProblem(IDictionary<string, string[]> errors) =>
    new(new ValidationProblemDetails(errors) { Status = StatusCodes.Status400BadRequest })
    {
      StatusCode = StatusCodes.Status400BadRequest,
    };
}
