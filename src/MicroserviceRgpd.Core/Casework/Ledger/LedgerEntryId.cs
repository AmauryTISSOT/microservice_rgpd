using Vogen;

namespace MicroserviceRgpd.Core.Casework.Ledger;

/// <summary>
/// L'identité d'une ligne du <c>Ledger</c>, engendrée au moment de l'écrire.
/// </summary>
/// <remarks>
/// <b>Ce n'est pas un numéro de séquence.</b> Un rang ne se déduit d'aucune ligne antérieure ici :
/// l'écriture d'une ligne ne lit jamais celles qui la précèdent — c'est la forme même de l'ajout
/// seul — et une numérotation continue serait exactement la lecture qu'on refuse. En version 7,
/// donc ordonnée dans le temps sans que rien n'ait eu à compter.
/// </remarks>
[ValueObject<Guid>]
public readonly partial struct LedgerEntryId
{
  /// <summary>L'identité de la ligne qu'on écrit à l'instant.</summary>
  public static LedgerEntryId Next() => From(Guid.CreateVersion7());

  private static Validation Validate(Guid value)
  {
    return value == Guid.Empty
      ? Validation.Invalid("L'identifiant de la ligne du Ledger est vide.")
      : Validation.Ok;
  }
}
