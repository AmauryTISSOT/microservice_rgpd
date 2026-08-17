using System.Reflection;

namespace MicroserviceRgpd.Web.Pages.Shared;

/// <summary>
/// <b>La version du produit</b>, lue <b>une fois</b> sur l'attribut de version informationnelle de
/// l'assemblage Web, et exposée sous la forme qui s'affiche : <c>v0.1.0</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La valeur n'est écrite qu'à un seul endroit</b> : la propriété <c>Version</c> de
/// <c>Directory.Build.props</c>, à la racine du dépôt, avec sa règle d'incrément en commentaire.
/// Le build la pose sur l'assemblage ; cette classe la relit. Ni le gabarit ni l'hôte ne la
/// recopient — le jour où elle change, elle change partout d'un coup.
/// </para>
/// <para>
/// Elle se lit et s'ignore : ni lien, ni infobulle, ni libellé technique, ni variation par
/// environnement. C'est l'<b>exception nommée et étroite</b> à la doctrine « aucun chiffre dans la
/// barre » de <see cref="Navigation"/>.
/// </para>
/// </remarks>
internal static class ProductVersion
{
  /// <summary>La version telle que l'assemblage la porte — <c>0.1.0</c>, sans SHA.</summary>
  internal static string Number { get; } =
    typeof(ProductVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
      ?.InformationalVersion
    ?? throw new InvalidOperationException(
      "L'assemblage Web ne porte aucune version informationnelle : la propriété Version de " +
      "Directory.Build.props manque.");

  /// <summary>La chaîne d'affichage — <c>v0.1.0</c> —, celle que la barre porte et que l'hôte journalise.</summary>
  internal static string Display { get; } = $"v{Number}";
}
