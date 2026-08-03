using MicroserviceRgpd.Core.SharedKernel;
using Vogen;

namespace MicroserviceRgpd.Core.Qualifications;

/// <summary>
/// Le texte libre reçu de l'application tierce, présumé être une demande d'exercice de droits sans
/// qu'on préjuge qu'il en soit une.
/// <para>
/// <b>Aucun plancher de longueur au-delà de la non-vacuité.</b> Un seul caractère est un texte
/// valide : sur <c>🙂</c> seul, la bonne réponse est <see cref="DataSubjectRight.OutOfScope"/>, pas
/// un rejet. Rejeter un texte parce qu'il est court, ce serait confondre « je n'y reconnais aucun
/// droit » avec « ta requête est malformée ».
/// </para>
/// </summary>
[ValueObject<string>]
public readonly partial struct RightsRequestText
{
  /// <summary>
  /// Le plafond, en <b>unités UTF-16</b> — où <c>🙂</c> vaut 2. Approximation assumée plutôt que
  /// d'introduire les éléments de texte Unicode dans un contrat public.
  /// </summary>
  /// <remarks>
  /// Chiffre arbitraire et assumé : huit fois le plus long exemple du corpus. Il ne protège pas
  /// d'un attaquant — l'authentification est hors périmètre — mais de l'usage normal : sur un GPU
  /// unique qui sérialise, un seul texte démesuré immobilise le service. Le garde-fou qui protège
  /// réellement est la borne de 64 Ko sur le corps, appliquée avant toute désérialisation.
  /// </remarks>
  public const int MaxLength = 10_000;

  /// <summary>
  /// Les espaces de bordure sont retirés avant validation <b>et</b> avant stockage : deux textes
  /// qui ne diffèrent que par leurs bordures sont la même demande.
  /// </summary>
  private static string NormalizeInput(string? input)
  {
    return input?.Trim() ?? string.Empty;
  }

  private static Validation Validate(string value)
  {
    // Le plafond se mesure après nettoyage des bordures, sur le texte qui sera effectivement
    // conservé : c'est ce texte-là, et non ses espaces, qui occupe le contexte du moteur.
    return value.Length switch
    {
      0 => Validation.Invalid("Le texte de la demande est absent ou vide."),
      > MaxLength => Validation.Invalid($"Le texte de la demande dépasse {MaxLength} caractères."),
      _ => Validation.Ok,
    };
  }
}
