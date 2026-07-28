using FluentValidation;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.UseCases.Qualifications.Qualify;

namespace MicroserviceRgpd.Web.Qualifications;

/// <summary>
/// La frontière d'entrée : ce qui ne peut pas être qualifié est arrêté ici, avant qu'aucun moteur
/// ne soit dérangé.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle refuse peu, et le refuse tôt.</b> Un texte absent ou vide n'est pas une demande ; un
/// texte démesuré immobiliserait un GPU qui sérialise. Tout le reste passe — <b>le bruit n'est pas
/// une erreur d'entrée, c'est une qualification « hors périmètre »</b>, et un texte d'un seul
/// caractère est un texte.
/// </para>
/// <para>
/// Aucune détection de langue, aucune détection de bruit : placer un classifieur non mesuré devant
/// un moteur mesuré, ce serait protéger le mesuré par le non mesuré, et ses erreurs seraient
/// définitives — un texte refusé ici ne rencontrera jamais le moteur qui aurait su le qualifier.
/// </para>
/// </remarks>
public sealed class QualifyRequestValidator : Validator<QualifyRequest>
{
  public QualifyRequestValidator()
  {
    // Deux règles, deux chaînes : une condition posée en fin de chaîne s'applique à tous les
    // validateurs qui la précèdent, et fondre les deux ferait taire la première.
    RuleFor(request => request.Text)
      .Must(text => !string.IsNullOrWhiteSpace(text))
      .WithMessage("Le texte de la demande est absent ou vide.");

    // Ces deux règles redisent ce que porte déjà le type du domaine. La redite est voulue : le type
    // protège le domaine en <b>levant</b>, la frontière parle à l'appelant en lui <b>nommant</b> ce
    // qu'il a mal fait. Seul le plafond risquerait de diverger, et il n'existe qu'une fois.
    RuleFor(request => request.Text)
      // Le plafond se mesure après nettoyage des bordures, sur le texte qui atteindra
      // effectivement le moteur : c'est lui, et non ses espaces, qui occupe son contexte.
      .Must(text => text!.Trim().Length <= RightsRequestText.MaxLength)
      .WithMessage($"Le texte de la demande dépasse {RightsRequestText.MaxLength} caractères.")
      .When(request => !string.IsNullOrWhiteSpace(request.Text));

    // Les deux règles se prononcent sur la référence <b>une fois ses bordures nettoyées</b> — celle
    // qui sera effectivement rendue et conservée. Les faire diverger refuserait un simple retour
    // chariot final, qui est précisément ce que le nettoyage des bordures est là pour absorber.
    RuleFor(request => request.CallerReference)
      .Must(reference => reference!.Trim().Length <= QualifyCommand.MaxCallerReferenceLength)
      .WithMessage($"La référence appelante dépasse {QualifyCommand.MaxCallerReferenceLength} caractères.")
      // Une référence est renvoyée telle quelle dans une réponse JSON et conservée telle quelle :
      // un caractère de contrôle n'y a rien à faire, et le service ne l'échappera pas pour elle.
      .Must(reference => !reference!.Trim().Any(char.IsControl))
      .WithMessage("La référence appelante contient un caractère de contrôle.")
      .When(request => request.CallerReference is not null);
  }
}
