using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.EvidenceLog;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Casework.OpenCase;

namespace MicroserviceRgpd.Web.Casework;

/// <summary>
/// Fait <b>entrer</b> une demande postée par l'application du client depuis une session
/// authentifiée. Le dossier naît, et le service cesse d'oublier.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle fait entrer, elle n'instruit jamais.</b> C'est la seule route publique qui touche un
/// <c>Case</c>, et elle ne fait que l'ouvrir : aucune API n'arbitre, ne motive, ne constate ni ne
/// clôt. Le seul chemin vers l'instruction passe par un écran que nous écrivons, sans quoi
/// l'incomplétude cesserait d'être visible par construction et quelqu'un finirait par refaire chez
/// lui l'écran « ✅ demande traitée ».
/// </para>
/// <para>
/// <b>Le dossier naît en portant <c>ApplicationSession</c>, sans vérification supplémentaire de
/// l'appelant.</b> C'est l'application qui a authentifié la session ; le service enregistre cette
/// déclaration et n'en juge jamais la valeur — <c>Enregistré, jamais vérifié</c>. La valeur n'est
/// pas un champ de la requête : une application qui pourrait déclarer autre chose ferait enregistrer un faux au
/// service sur la foi d'un appelant qu'il ne vérifie pas.
/// </para>
/// <para>
/// <b>Un <c>201</c>, et aucun en-tête <c>Location</c>.</b> Quelque chose a bien été créé, et durable
/// cette fois — à la différence d'une qualification, dont on repart avec le résultat. Mais aucune
/// route publique ne relit un dossier : un <c>Location</c> pointerait vers un chemin qui n'existe
/// pas, et un <c>Location</c> qui ne mène nulle part est un mensonge de contrat.
/// </para>
/// <para>
/// <b>Pas de segment de version dans le chemin</b>, comme sur l'autre route publique du service :
/// dette réelle et assumée, réglée le jour où une v2 arrive.
/// </para>
/// </remarks>
public class OpenCase(IMediator mediator, TimeProvider clock) : Endpoint<OpenCaseRequest, OpenCaseResponse>
{
  /// <inheritdoc />
  public override void Configure()
  {
    Post("/cases");
    AllowAnonymous();

    Summary(summary =>
    {
      summary.Summary = "Fait entrer une demande d'exercice de droits";
      summary.Description =
        "Ouvre un dossier pour une demande postée par l'application du client depuis une session " +
        "qu'elle a elle-même authentifiée. Le dossier naît en portant `ApplicationSession` : le " +
        "service enregistre cette déclaration et ne vérifie aucune identité lui-même.\n\n" +
        "**Une demande donne un seul dossier**, quels que soient les droits qu'elle porte.\n\n" +
        "**Aucun emplacement pour une pièce jointe** n'existe sur ce canal : aucune pièce " +
        "d'identité n'entre dans le service, tous canaux confondus.\n\n" +
        "**Cette route fait entrer, elle n'instruit jamais.** Aucune route publique n'arbitre, ne " +
        "motive, ne constate ni ne clôt un dossier — le seul chemin vers l'instruction est la " +
        "surface de l'opérateur, livrée par le service.";
      summary.Responses[201] = "Dossier ouvert, et sa première ligne écrite au EvidenceLog";
      summary.Responses[400] =
        "Désignation malformée, droit hors taxonomie, ou OutOfScope réclamé comme s'il était un droit";
      summary.Responses[500] = "Défaillance interne, y compris l'échec d'écriture du dossier ou de l'EvidenceLog";
    });

    Tags("Casework");
  }

  /// <inheritdoc />
  public override async Task HandleAsync(OpenCaseRequest request, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(request);

    var command = new OpenCaseCommand(
      // Ce canal en porte une, et le service la pose lui-même.
      IdentityDeclaration.ApplicationSession,
      // Aucune motivation, et aucun champ pour en porter une : `ApplicationSession` repose sur un
      // contrôle du canal, et il n'y a donc rien à peser. Voir `IdentityDeclaration.RestsOnNoControl`.
      Motivation: null,
      // La validation a déjà refusé le mot inconnu, la valeur vide et la démesurée : les
      // conversions ne peuvent plus échouer, et les désignations entrent dans le domaine par leur
      // type plutôt que par des chaînes nues.
      [.. (request.Designations ?? []).Select(one => Designation.Of(DesignationKind.FromToken(one.Kind)!, one.Value))],
      [.. (request.Rights ?? []).Select(name => DataSubjectRight.FromName(name))],
      // La personne a coché ses droits dans l'application : elle les a désignés elle-même, et rien
      // ne reste à confirmer. `Proposed` n'entre pas par une route publique — une machine qui
      // pourrait faire naître un droit confirmé serait une machine qui produit une issue.
      ClaimOrigin.Named,
      // Une demande postée par l'application arrive à l'instant où elle est postée : la date de
      // réception est celle de l'appel, et il n'existe aucun champ pour la déclarer autrement. Elle
      // est donc *déclarée* — par l'application, qui sait quand elle a reçu la demande — et jamais
      // tenue pour défaut : le défaut est l'affaire du dépôt manuel.
      ReceptionDate.Declared(clock.GetUtcNow()),
      // Aucun humain n'a signé : l'application a appelé, et la ligne le dit plutôt que de le taire.
      Signatory.Application);

    var result = await mediator.Send(command, cancellationToken);

    if (result.Status == ResultStatus.Invalid)
    {
      // La seule invalidité qui atteigne le gestionnaire est OutOfScope réclamé comme un droit : la
      // frontière l'a laissé passer, la taxonomie le connaissant, et c'est le domaine qui dit qu'on
      // ne le réclame pas. Le message part tel quel à l'appelant, sous le nom du champ fautif.
      foreach (var error in result.ValidationErrors)
      {
        AddError(error.ErrorMessage, error.Identifier);
      }

      await Send.ErrorsAsync(cancellation: cancellationToken);

      return;
    }

    if (result.Status != ResultStatus.Ok)
    {
      // Refuser un statut inconnu plutôt que le supposer : sans quoi un statut ajouté plus tard
      // sortirait en 201 avec un corps vide, et un appelant repartirait avec l'identité d'un
      // dossier qui n'existe pas.
      throw new InvalidOperationException(
        $"L'ouverture d'un Case a rendu un statut que l'endpoint ne sait pas traduire : {result.Status}.");
    }

    // Le corps et le code, sans `Location` : `CreatedAtAsync` en poserait un vers cette route même,
    // qui ne relit aucun dossier — un en-tête qui promet une lecture qu'aucune route n'offre.
    await Send.ResponseAsync(
      OpenCaseResponse.From(result.Value),
      StatusCodes.Status201Created,
      cancellationToken);
  }
}
