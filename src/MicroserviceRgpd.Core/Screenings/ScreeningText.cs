namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Ce qu'on exige d'un texte que le service <b>recopie sans l'interpréter</b> : un nom de colonne
/// tiré du relevé, un motif en prose.
/// </summary>
/// <remarks>
/// <para>
/// <b>Trois exigences, et pas une de plus.</b> Non vide, borné, et sans caractère de contrôle. Au
/// delà, <b>rien n'est validé</b> : <c>Enregistré, jamais vérifié</c> — le service enregistre ce qu'on lui
/// a mis dans la main et n'en juge jamais la valeur.
/// </para>
/// <para>
/// ⚠️ <b>Il n'est pas factorisable.</b> Un garde commun vivrait dans un autre contexte, et le lire
/// d'ici serait une traversée que le garde d'ADR-0003 refuse ; et <c>Screening</c> n'a « aucune
/// intersection avec le reste du dépôt — pas même le noyau partagé ». Recopier quarante lignes le
/// jour où un autre contexte en voudra autant est le prix, connu et payé, de la clause
/// <c>Separate Ways</c>.
/// </para>
/// <para>
/// <b>Elle lève, elle ne rend pas de verdict.</b> Ce qui arrive ici est censé être déclarable : le
/// refus lisible sort à la frontière d'entrée, et un refus à ce niveau est une programmation
/// fautive.
/// </para>
/// </remarks>
internal static class ScreeningText
{
  /// <summary>
  /// Le texte, bordures nettoyées, ou une exception nommant ce qui n'allait pas.
  /// </summary>
  /// <param name="value">Ce qui a été déclaré ou recopié.</param>
  /// <param name="subject">
  /// Ce dont on parle, au singulier et sans article final — « Le nom de la colonne », « Le motif de
  /// la ligne ». Il ouvre les trois messages, qui sont lus par un humain.
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

  /// <summary>
  /// Un champ que le SGBD n'a pas rendu et un champ vide sont la <b>même absence</b>, et elle
  /// s'écrit <c>null</c>. Sans cette normalisation, « ce SGBD ne rend aucun commentaire » et « cette
  /// colonne n'en a pas » se compteraient différemment selon que le relevé écrit <c>""</c> ou rien.
  /// </summary>
  internal static string? OrAbsent(string? value)
  {
    var trimmed = value?.Trim();

    return string.IsNullOrEmpty(trimmed) ? null : trimmed;
  }
}
