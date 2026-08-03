using Vogen;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Le nom sous lequel l'<c>Operator</c> reconnaît un <see cref="DeclaredSystem"/> dans sa propre
/// maison — « la base de la boutique », « la comptabilité scellée ».
/// </summary>
/// <remarks>
/// Il est distinct de l'identifiant parce que leurs lecteurs le sont : l'identifiant part sur le
/// fil vers l'<c>Adapter</c> et ne bouge jamais, le libellé s'écrit pour un humain et se corrige le
/// jour où le mot change. Les fondre obligerait à choisir lequel des deux trahir.
/// </remarks>
[ValueObject<string>]
public readonly partial struct SystemLabel
{
  /// <summary>Le plafond, en unités UTF-16. Chiffre arbitraire et assumé : une ligne, pas un paragraphe.</summary>
  public const int MaxLength = 200;

  /// <summary>Les bordures sont retirées avant validation <b>et</b> avant stockage.</summary>
  private static string NormalizeInput(string? input)
  {
    return input?.Trim() ?? string.Empty;
  }

  private static Validation Validate(string value)
  {
    return value.Length switch
    {
      0 => Validation.Invalid("Le libellé du système est absent ou vide."),
      > MaxLength => Validation.Invalid($"Le libellé du système dépasse {MaxLength} caractères."),
      _ => Validation.Ok,
    };
  }
}
