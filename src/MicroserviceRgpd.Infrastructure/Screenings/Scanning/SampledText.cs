using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning;

/// <summary>
/// Le garde-fou de coupure, partagé par tous les dialectes : ce qui rentre dans un
/// <see cref="PreviewedValue"/> tient dans la borne, quoi qu'ait compté le SGBD.
/// </summary>
/// <remarks>
/// ⚠️ <b>Les SGBD comptent en caractères Unicode, .NET en unités UTF-16.</b> Un caractère hors du
/// plan multilingue de base en vaut deux : <c>substr(col, 1, 254)</c> comme <c>left(col, 254)</c>
/// peuvent rendre jusqu'à 508 unités, que <see cref="PreviewedValue.Of"/> refuserait. Le garde-fou
/// vit ici plutôt qu'en trois copies parce qu'une copie qui dériverait des deux autres ne le ferait
/// savoir que le jour où un client stocke des émojis.
/// </remarks>
internal static class SampledText
{
  /// <summary>
  /// Coupe un texte à la borne du domaine, <b>jamais au milieu d'une paire de substituts</b> : une
  /// demi-paire rendue à l'écran est un losange noir, et l'<c>Operator</c> lirait un défaut du
  /// service là où la base a très bien répondu.
  /// </summary>
  internal static string Fit(string text)
  {
    if (text.Length <= ColumnPreview.MaxValueLength)
    {
      return text;
    }

    var length = ColumnPreview.MaxValueLength;

    if (char.IsHighSurrogate(text[length - 1]))
    {
      length--;
    }

    return text[..length];
  }
}
