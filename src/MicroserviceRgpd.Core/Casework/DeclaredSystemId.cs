using Vogen;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// L'identifiant qu'un humain donne à un <see cref="DeclaredSystem"/> au moment de le déclarer.
/// <para>
/// Il est <b>choisi, jamais engendré</b> : c'est lui que le service passera à l'<c>Adapter</c> en
/// paramètre d'appel, et un <c>Adapter</c> ne peut refuser explicitement un identifiant inconnu que
/// s'il en connaît les siens. Un GUID obligerait le développeur du client à recopier une chaîne
/// qu'il ne reconnaîtrait pas dans son propre code.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Il ne nomme rien du schéma du client.</b> Le grain est le système — « la boutique », « la
/// reprise de 2019 » —, jamais une table ni une colonne : c'est ce qui met le service à l'abri de
/// pourrir en silence sur un changement de schéma.
/// </para>
/// <para>
/// <b>Le jeu de caractères est étroit et assumé.</b> L'identifiant voyage dans un chemin d'URL — la
/// page de révision — et dans un appel sortant vers l'<c>Adapter</c> ; l'y échapper à chaque
/// traversée serait une occasion d'oubli de plus. Minuscules seulement, pour qu'aucune paire ne
/// diffère par sa seule casse et ne se lise comme deux systèmes.
/// </para>
/// </remarks>
[ValueObject<string>]
public readonly partial struct DeclaredSystemId
{
  /// <summary>Le plafond, en unités UTF-16. Chiffre arbitraire et assumé, large pour un identifiant lisible.</summary>
  public const int MaxLength = 64;

  /// <summary>Les bordures sont retirées avant validation <b>et</b> avant stockage.</summary>
  private static string NormalizeInput(string? input)
  {
    return input?.Trim() ?? string.Empty;
  }

  private static Validation Validate(string value)
  {
    if (value.Length == 0)
    {
      return Validation.Invalid("L'identifiant du système est absent ou vide.");
    }

    if (value.Length > MaxLength)
    {
      return Validation.Invalid($"L'identifiant du système dépasse {MaxLength} caractères.");
    }

    // Le premier caractère est alphanumérique : un identifiant ouvrant sur un tiret ou un point se
    // lit comme une option de ligne de commande ou comme un segment de chemin relatif.
    if (!IsLowerAlphanumeric(value[0]) || !value.All(IsAllowed))
    {
      return Validation.Invalid(
        "L'identifiant du système s'écrit en minuscules, chiffres, tirets, points et tirets bas, "
        + "et commence par une lettre minuscule ou un chiffre.");
    }

    return Validation.Ok;
  }

  private static bool IsAllowed(char character)
  {
    return IsLowerAlphanumeric(character) || character is '-' or '.' or '_';
  }

  private static bool IsLowerAlphanumeric(char character)
  {
    return character is >= 'a' and <= 'z' or >= '0' and <= '9';
  }
}
