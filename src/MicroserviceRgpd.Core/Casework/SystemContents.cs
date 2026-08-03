using Vogen;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Ce qu'un <see cref="DeclaredSystem"/> <b>contient</b>, en prose française libre et dans les mots
/// de celui qui l'a déclaré — « l'export commercial transmis chaque mois à notre agence ».
/// <para>
/// <b>Le champ n'est pas décoratif, et c'est pourquoi il est obligatoire.</b> C'est avec ses mots
/// que la <c>CoverSheet</c> nommera les systèmes non couverts, dans une langue que la personne
/// concernée comprend : nommer « l'export commercial transmis chaque mois à notre agence » est
/// actionnable là où un identifiant technique ne lui apprend rien. Un système déclaré sans prose
/// serait un système que la <c>CoverSheet</c> ne saurait pas nommer.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>Prose, jamais structure.</b> Ni liste de champs, ni catégories fermées : le grain du
/// <c>Manifest</c> est le système, et une taxonomie de contenus rouvrirait par la bande la
/// précision de champ que le service a refusé de détenir.
/// </para>
/// <para>
/// <b>Elle se relit telle quelle</b> — aucune reformulation, aucune troncature, aucune
/// normalisation au-delà du nettoyage des bordures : le service n'est pas l'auteur de ce texte.
/// </para>
/// </remarks>
[ValueObject<string>]
public readonly partial struct SystemContents
{
  /// <summary>Le plafond, en unités UTF-16. Chiffre arbitraire et assumé : un paragraphe, pas une notice.</summary>
  public const int MaxLength = 1_000;

  /// <summary>
  /// Les bordures sont retirées avant validation <b>et</b> avant stockage ; l'intérieur est laissé
  /// intact, retours à la ligne compris — la prose de quelqu'un d'autre ne se reformate pas.
  /// </summary>
  private static string NormalizeInput(string? input)
  {
    return input?.Trim() ?? string.Empty;
  }

  private static Validation Validate(string value)
  {
    if (value.Length == 0)
    {
      return Validation.Invalid("Le champ « contient » est absent ou vide.");
    }

    if (value.Length > MaxLength)
    {
      return Validation.Invalid($"Le champ « contient » dépasse {MaxLength} caractères.");
    }

    // Les séparateurs de ligne et la tabulation sont de la mise en forme d'un texte multiligne ;
    // tout autre caractère de contrôle n'est pas de la prose et n'a aucune raison d'être conservé.
    if (value.Any(character => char.IsControl(character) && character is not ('\n' or '\r' or '\t')))
    {
      return Validation.Invalid("Le champ « contient » porte un caractère de contrôle.");
    }

    return Validation.Ok;
  }
}
