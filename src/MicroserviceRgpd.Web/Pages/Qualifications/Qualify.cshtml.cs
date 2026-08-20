using System.Globalization;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.UseCases.Qualifications.Qualify;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vogen;

namespace MicroserviceRgpd.Web.Pages.Qualifications;

/// <summary>
/// L'écran de la <b>qualification</b> : l'<c>Operator</c> y colle le texte d'une demande arrivée par
/// courriel, soumet, et <b>lit le verdict dans la même réponse</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>C'est la surface humaine de ce que <c>POST /qualifications</c> fait déjà pour les
/// applications tierces, et rien de plus.</b> Aucun dossier n'en découle, et le contrat HTTP
/// public ne bouge pas d'une ligne : l'écran passe par le même geste, par MediatR.
/// </para>
/// <para>
/// ⚠️ <b>DEUX TEMPS, JAMAIS TROIS — et c'est une dérogation assumée au motif POST-Redirect-GET</b>
/// que l'écran de dépôt d'un relevé applique. Elle est imposée par la conception de ce contexte :
/// <c>IQualificationAuditTrail</c> n'expose qu'une méthode d'écriture, et son contrat écrit que
/// « ce n'est pas une omission qu'on comblera ». Ouvrir un <c>GET</c> de relecture ferait de la
/// trace d'audit une <b>ressource métier exposée</b> — l'entité même que ce contexte a refusée.
/// </para>
/// <para>
/// ⚠️ <b>Conséquence acceptée, et dite en clair sous le verdict</b> : un rechargement <b>rejoue</b>
/// la qualification, et produit un nouvel identifiant et une nouvelle trace. La taire aurait fait
/// de la dérogation un piège plutôt qu'une décision.
/// </para>
/// <para>
/// ⚠️ <b><c>CallerReference</c> est laissée vide, et elle doit le rester.</b> Ce champ appartient à
/// l'appelant, et le service ne l'interprète jamais : l'y voir écrire d'office ferait qu'un lecteur
/// futur prendrait cette valeur pour une donnée fournie par un client. <b>Prix consigné</b> : rien,
/// dans la trace, ne distingue une qualification venue de l'écran d'une qualification venue d'une
/// application tierce. Le jour où cette provenance compte, elle mérite <b>sa propre colonne</b> et
/// une décision assumée — pas le détournement d'un champ existant.
/// </para>
/// <para>
/// ⚠️ <b>Les internes des moteurs paraissent, et ils paraissent sous le verdict.</b> L'écran range
/// les deux avis, la confiance déclarée, l'identité de chaque moteur et les trois latences sous un
/// <b>dépliant natif</b>, en bas du document : un verdict qu'on ne peut pas contredire n'est pas une
/// aide à la décision. L'ADR-0011 assume cette visibilité — elle n'ajoute <b>aucune capacité</b>,
/// <c>POST /qualifications</c> étant déjà anonyme et public —, et le contrat HTTP public ne change
/// pas d'une ligne : <c>QualifyResponse</c> ne projette rien de tout cela.
/// </para>
/// <para>
/// <b>Le service propose, il ne décide jamais.</b> Ce que l'écran rend est une aide à la décision :
/// l'humain qui le lit valide ou corrige.
/// </para>
/// </remarks>
public class QualifyModel(IMediator mediator) : PageModel
{
  /// <summary>Le texte collé, tel quel. Le service ne le découpe ni ne le complète.</summary>
  [BindProperty]
  public string? Text { get; set; }

  /// <summary>Le verdict rendu par le geste qui vient d'avoir lieu, ou rien avant lui.</summary>
  public Verdict? RenderedVerdict { get; private set; }

  /// <summary>
  /// Ce dont ce verdict-là est tiré — les deux avis, la confiance, les identités de moteurs et les
  /// trois latences —, ou rien tant qu'aucun verdict n'a été rendu.
  /// </summary>
  /// <remarks>
  /// Les prémisses ne sont <b>jamais</b> obtenues par un second chemin de qualification : elles
  /// arrivent avec le verdict, dans le même <see cref="QualificationOutcome"/>. Deux chemins
  /// auraient divergé sur la règle de corroboration elle-même, et l'écran aurait fini par afficher,
  /// sur le même texte, un signal de relecture calculé autrement que celui de l'API.
  /// </remarks>
  public Premises? RenderedPremises { get; private set; }

  /// <summary>
  /// Le plafond du texte tel que l'écran l'annonce — <b>celui du domaine, jamais recopié</b> : un
  /// nombre écrit dans le HTML aurait continué de s'afficher le jour où le plafond bouge, et
  /// l'écran aurait promis autre chose que ce que le service accepte.
  /// </summary>
  public static string Ceiling =>
    RightsRequestText.MaxLength.ToString(CultureInfo.InvariantCulture);

  public void OnGet()
  {
    // Rien à charger : l'écran ne relit aucune qualification, il en fait naître une.
  }

  public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
  {
    RightsRequestText text;

    try
    {
      // ⚠️ LE TYPE LÈVE, L'ÉCRAN NOMME. Les deux refus — le vide et le démesuré — sont ceux du
      // domaine, où le contrat du texte est écrit : le plafond y est dit une fois, et c'est cette
      // phrase-là que l'Operator lit. Une seconde rédaction ici aurait fini par ne plus dire la
      // même chose que le service accepte.
      text = RightsRequestText.From(Text ?? string.Empty);
    }
    catch (ValueObjectValidationException refusal)
    {
      // La clé est celle du champ lié, SANS préfixe : le refus porte sur « Text », qui est le nom
      // de la zone de texte.
      ModelState.AddModelError(nameof(Text), refusal.Message);

      return Page();
    }

    // ⚠️ AUCUNE RÉFÉRENCE APPELANTE. Voir le prix consigné plus haut : ce champ est celui de
    // l'appelant, et l'écran n'en est pas un.
    var qualified = await mediator.Send(new QualifyCommand(text, CallerReference: null), cancellationToken);

    if (qualified.Status != ResultStatus.Ok)
    {
      // Inatteignable tant qu'un seul des deux moteurs suffit à qualifier — comme à l'endpoint, dont
      // c'est le même geste. Refuser un statut inconnu plutôt que le supposer évite qu'un statut
      // ajouté plus tard rende un écran portant un verdict vide.
      throw new InvalidOperationException(
        $"La qualification a rendu un statut que l'écran ne sait pas traduire : {qualified.Status}.");
    }

    RenderedVerdict = Verdict.Of(qualified.Value);
    RenderedPremises = Premises.Of(qualified.Value);

    return Page();
  }
}
