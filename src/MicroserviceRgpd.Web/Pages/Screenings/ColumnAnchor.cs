using System.Globalization;
using System.Text;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// L'<b>ancre</b> d'une colonne sur l'écran de sa table : l'identifiant que le bloc porte, et le
/// fragment que la redirection d'un arbitrage vise.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Elle vise la colonne qu'on vient de trancher, jamais la suivante.</b> Ancrer sur la
/// suivante ferait de l'écran un tapis roulant : l'<c>Operator</c> perdrait de vue ce qu'il vient
/// de dire à l'instant même où il pourrait encore se raviser — et se raviser doit rester sans
/// cérémonie sur une surface qu'on reprend pendant trois jours.
/// </para>
/// <para>
/// ⚠️ <b>Le nom de colonne ne se recopie pas tel quel.</b> Une base rend des noms qui portent des
/// espaces, des accents, des guillemets ou une croisette — tout ce qu'un identifiant cité accepte —
/// et un fragment d'URL n'en accepte presque rien. L'échappement est donc <b>injectif</b> : deux
/// colonnes distinctes d'une même table ne peuvent pas atterrir sur la même ancre, faute de quoi
/// un arbitrage renverrait le lecteur sur une colonne qu'il n'a pas tranchée.
/// </para>
/// <para>
/// <b>Le caractère d'échappement est le tiret</b>, et il est lui-même échappé : les noms ordinaires
/// — <c>email</c>, <c>id_adh</c>, <c>adr_l1</c> — traversent intacts, ce qui garde l'ancre lisible
/// dans la barre d'adresse, et tout le reste devient <c>-XXXX</c>.
/// </para>
/// </remarks>
internal static class ColumnAnchor
{
  /// <summary>
  /// Ce qui préfixe toute ancre de colonne. <b>Il dit ce que l'ancre désigne</b> : un identifiant nu
  /// tiré d'un nom de base entrerait en collision avec ceux du reste de la page — un
  /// <c>&lt;section&gt;</c> nommé <c>batch</c>, un jour, et une colonne <c>batch</c> se disputeraient
  /// le même fragment.
  /// </summary>
  private const string Prefix = "colonne-";

  /// <summary>L'ancre d'une colonne, à poser sur son bloc comme à viser dans un fragment.</summary>
  /// <param name="column">Le nom de la colonne, tel que le relevé le rend.</param>
  /// <exception cref="ArgumentNullException"><paramref name="column"/> est absent.</exception>
  internal static string For(string column)
  {
    ArgumentNullException.ThrowIfNull(column);

    var anchor = new StringBuilder(Prefix.Length + column.Length);

    anchor.Append(Prefix);

    foreach (var character in column)
    {
      if (IsCarriedAsIs(character))
      {
        anchor.Append(character);
      }
      else
      {
        anchor.Append('-').Append(((int)character).ToString("X4", CultureInfo.InvariantCulture));
      }
    }

    return anchor.ToString();
  }

  /// <summary>
  /// Ce qui traverse sans être échappé : les lettres latines sans accent, les chiffres et le
  /// souligné — l'alphabet des noms de colonne ordinaires, et rien de plus.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le tiret n'en est pas</b>, et c'est ce qui rend l'échappement injectif : sans cela,
  /// <c>e-mail</c> et <c>e</c> suivi d'un caractère échappé en <c>-002Dmail</c> auraient produit la
  /// même ancre.
  /// </remarks>
  private static bool IsCarriedAsIs(char character)
  {
    return character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_';
  }
}
