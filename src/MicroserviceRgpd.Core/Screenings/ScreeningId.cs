using Vogen;

namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// L'identité d'un <see cref="Screening"/>, engendrée par le service au moment où la détection est
/// lancé.
/// </summary>
/// <remarks>
/// <para>
/// <b>Une clé de substitution, et rien d'autre.</b> Ce qui identifie une <i>colonne</i> est le
/// triplet — voir <see cref="ColumnIdentity"/> — mais un rapport n'a pas d'identité naturelle : le
/// nom de base que le SGBD rend est « un repère pour l'humain qui relit un <c>Screening</c> trois
/// jours plus tard, jamais une identité sur laquelle bâtir une comparaison ».
/// </para>
/// <para>
/// <b>En version 7</b> : ordonnée dans le temps, donc sans fragmentation d'index à l'insertion, et
/// c'est le même choix que celui de l'identité d'un dossier.
/// </para>
/// </remarks>
[ValueObject<Guid>]
public readonly partial struct ScreeningId
{
  /// <summary>L'identité d'un rapport de détection qui se lance à l'instant.</summary>
  public static ScreeningId Next() => From(Guid.CreateVersion7());

  private static Validation Validate(Guid value)
  {
    return value == Guid.Empty
      ? Validation.Invalid("L'identifiant du Screening est vide.")
      : Validation.Ok;
  }
}
