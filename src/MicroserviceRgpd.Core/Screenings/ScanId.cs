using Vogen;

namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// L'identité d'un <c>Scan</c>, engendrée au moment où l'<c>Operator</c> lance la connexion.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Elle est distincte de tout <see cref="ScreeningId"/>, et ce n'est pas une commodité.</b> Un
/// scan peut être abandonné, tomber, ou rendre l'une des deux fins à zéro objet : il meurt alors
/// sans avoir jamais produit de <see cref="Screening"/>. Réutiliser l'identité du rapport aurait
/// obligé à en engendrer un avant de savoir s'il en existerait un — c'est-à-dire à écrire l'identité
/// d'un rapport qui n'aura peut-être jamais de ligne.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne descend jamais en base.</b> Elle n'identifie que le transitoire — voir
/// <see cref="ScanProgress"/> —, qui vit en mémoire du processus et meurt avec lui. Une colonne qui
/// la porterait ferait survivre à un redémarrage l'identité d'un scan qui, lui, n'y survit pas.
/// </para>
/// <para>
/// <b>En version 7</b>, comme les autres identités du dépôt : ordonnée dans le temps, et lisible
/// dans une adresse d'écran d'attente sans rien apprendre à qui la lit.
/// </para>
/// </remarks>
[ValueObject<Guid>]
public readonly partial struct ScanId
{
  /// <summary>L'identité d'un scan qui se lance à l'instant.</summary>
  public static ScanId Next() => From(Guid.CreateVersion7());

  private static Validation Validate(Guid value)
  {
    return value == Guid.Empty
      ? Validation.Invalid("L'identifiant du Scan est vide.")
      : Validation.Ok;
  }
}
