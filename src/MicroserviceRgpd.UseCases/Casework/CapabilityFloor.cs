using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework;

/// <summary>
/// Le plancher <see cref="Capability.Locate"/>, dit à l'humain plutôt que levé sur lui.
/// </summary>
/// <remarks>
/// <para>
/// <b>La règle est déjà portée par <see cref="DeclaredSystem"/>, qui lève.</b> La redite est
/// voulue, et c'est la même que celle du texte reçu à qualifier : le type protège le domaine en
/// <b>levant</b>, la frontière parle à celui qui a saisi en lui <b>nommant</b> ce qu'il a mal
/// rempli. Un plancher franchi n'est pas un accident de programmation ici — c'est une case cochée
/// de travers dans un formulaire.
/// </para>
/// <para>
/// Elle vit à un seul endroit parce que <b>deux gestes la traversent</b> — déclarer et réviser —
/// et que deux copies finiraient par ne plus dire la même chose à l'écran.
/// </para>
/// </remarks>
internal static class CapabilityFloor
{
  /// <summary>Le mot que lit celui qui a coché « effacer » sans cocher « localiser ».</summary>
  internal const string Message =
    "Locate est le plancher : un système déclare Locate, ou aucune capacité. Sans lui, ce que le "
    + "client affirmera sur ce système n'aura pas de dénominateur.";

  /// <summary>
  /// L'écart au plancher, ou <c>null</c> si les capacités sont déclarables — l'ensemble vide y
  /// compris, qui est le niveau 0 et un état pleinement normal.
  /// </summary>
  internal static ValidationError? Violation(IReadOnlyCollection<Capability> capabilities, string identifier)
  {
    if (capabilities.Count == 0 || capabilities.Contains(Capability.Locate))
    {
      return null;
    }

    return new ValidationError
    {
      Identifier = identifier,
      ErrorMessage = Message,
      Severity = ValidationSeverity.Error,
    };
  }
}
