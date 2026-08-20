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
/// ⚠️ <b>Une indisponibilité partielle ne bloque pas l'<c>Operator</c>, une indisponibilité totale
/// ne lui rend pas un écran cassé.</b> Un seul moteur muet donne un verdict, assorti de la ligne
/// d'entièreté qui dit que le service n'était pas entier — et cet état se lit <b>à côté</b> de
/// l'urgence à relire, jamais à sa place : ce sont deux axes, et un verdict sans contrôle ne doit
/// jamais se lire comme un verdict contrôlé. Les <b>deux moteurs</b> muets ne laissent rien à
/// qualifier, et l'écran le dit alors en français, sous un <c>503</c>.
/// </para>
/// <para>
/// <b>La double panne ne se subdivise pas ici</b>, à la différence de <c>POST /qualifications</c>
/// qui distingue le dépassement d'échéance de l'indisponibilité par son code. La distinction sert
/// une <b>application</b> qui décide de rejouer ou non ; l'<c>Operator</c>, lui, resoumet son texte
/// dans les deux cas, et une seconde phrase l'aurait fait choisir entre deux gestes identiques.
/// </para>
/// <para>
/// <b>Le service propose, il ne décide jamais.</b> Ce que l'écran rend est une aide à la décision :
/// l'humain qui le lit valide ou corrige.
/// </para>
/// </remarks>
public class QualifyModel(IMediator mediator, ILogger<QualifyModel> logger) : PageModel
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
  /// Vrai quand <b>aucun moteur n'a rendu d'avis</b> : il n'y a alors rien à qualifier, et l'écran
  /// dit en français qu'il ne peut rien faire pour l'instant.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Ce n'est pas le mode dégradé.</b> Un seul moteur muet rend un verdict, assorti de la
  /// ligne d'entièreté qui dit que le service n'était pas entier. Ici, les deux se sont tus : il n'y
  /// a pas de verdict à rendre, et en fabriquer un aux cases vides se lirait comme un verdict que
  /// personne n'a prononcé.
  /// </remarks>
  public bool CouldNotQualify { get; private set; }

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
    Result<QualificationOutcome> qualified;

    try
    {
      qualified = await mediator.Send(new QualifyCommand(text, CallerReference: null), cancellationToken);
    }
    catch (QualificationEngineFailure doubleFailure)
    {
      // ⚠️ LES DEUX MOTEURS SE SONT TUS, ET C'EST LE SEUL CAS QUI PASSE ICI : un moteur seul muet a
      // déjà été absorbé par le repli, et rend un verdict marqué « service non entier ». Il ne reste
      // rien à qualifier, et l'Operator lit une phrase française plutôt qu'une page d'erreur nue.
      //
      // ⚠️ LE MESSAGE DU MOTEUR NE FRANCHIT PAS CETTE FRONTIÈRE, exactement comme à l'endpoint : il
      // nomme le moteur qui s'est tu, ce qui est de l'exploitation et vit dans les traces. Il est
      // journalisé ici pour ne pas disparaître avec l'exception rattrapée.
      logger.LogError(doubleFailure, "Aucun moteur n'a rendu d'avis : l'écran ne peut rien qualifier.");

      CouldNotQualify = true;

      // ⚠️ LE STATUT DIT LA MÊME CHOSE QUE LA PHRASE, et c'est le même code que celui de
      // `POST /qualifications` sur la même panne : un 200 aurait annoncé aux caches et à la
      // supervision une page rendue normalement, là où le service est indisponible.
      return new PageResult { StatusCode = StatusCodes.Status503ServiceUnavailable };
    }

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
