namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Ce qu'on exige d'un texte <b>déclaré par quelqu'un</b> et que le service recopie sans
/// l'interpréter : une valeur de <see cref="Designation"/>, le nom d'un signataire.
/// </summary>
/// <remarks>
/// <para>
/// <b>Trois exigences, et pas une de plus.</b> Non vide, borné, et sans caractère de contrôle —
/// parce que ces textes partent sur le fil vers un <c>Adapter</c> ou descendent dans une colonne,
/// et que le service ne les échappera pour personne. Au-delà, <b>rien n'est validé</b> : ni format,
/// ni unicité, ni exactitude. La personne n'a pas d'identifiant, et prétendre valider une
/// désignation reviendrait à prétendre savoir qui elle est.
/// </para>
/// <para>
/// <b>Les bordures sont retirées avant validation et avant stockage</b> ; l'intérieur reste intact.
/// </para>
/// <para>
/// <b>Elle lève, elle ne rend pas de verdict.</b> Ce qui arrive ici est censé être déclarable : la
/// frontière d'entrée a déjà nommé à l'humain — ou à l'application — ce qu'il avait mal rempli, et
/// un refus à ce niveau est une programmation fautive.
/// </para>
/// </remarks>
internal static class DeclaredText
{
  /// <summary>
  /// Le texte, bordures nettoyées, ou une exception nommant ce qui n'allait pas.
  /// </summary>
  /// <param name="value">Ce qui a été déclaré.</param>
  /// <param name="subject">
  /// Ce dont on parle, au singulier et sans article final — « La valeur de la désignation », « Le
  /// nom du signataire ». Il ouvre les trois messages, qui sont lus par un humain.
  /// </param>
  /// <param name="maxLength">Le plafond, en unités UTF-16.</param>
  /// <param name="parameterName">Le paramètre de l'appelant que l'exception doit nommer.</param>
  /// <exception cref="ArgumentException">Le texte est vide, démesuré, ou porte un caractère de contrôle.</exception>
  internal static string OrThrow(string? value, string subject, int maxLength, string parameterName)
  {
    var trimmed = value?.Trim() ?? string.Empty;

    if (trimmed.Length == 0)
    {
      throw new ArgumentException($"{subject} est absent ou vide.", parameterName);
    }

    if (trimmed.Length > maxLength)
    {
      throw new ArgumentException($"{subject} dépasse {maxLength} caractères.", parameterName);
    }

    if (trimmed.Any(char.IsControl))
    {
      throw new ArgumentException($"{subject} porte un caractère de contrôle.", parameterName);
    }

    return trimmed;
  }
}
