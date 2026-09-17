using System.Text.RegularExpressions;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// Le <b>bandeau d'avertissement de déploiement</b>, lu dans une page rendue — écrit une fois, parce
/// que deux classes l'interrogent : la face RabbitMQ sur un déploiement sans connexion, et la même
/// face sur un déploiement qui en déclare une. Recopié, il aurait divergé, et l'une des deux aurait
/// fini par chercher un bandeau que la page ne pose plus.
/// </summary>
internal static class TheBrokerConnectionAdvisory
{
  /// <summary>
  /// Ce que le bandeau dit, s'il paraît — <c>null</c> quand la page n'en porte aucun.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La lecture s'arrête au premier <c>&lt;/div&gt;</c></b>, et c'est juste tant que le bandeau
  /// ne porte aucun <c>div</c> imbriqué — il n'a que des paragraphes. Le jour où il en porterait un,
  /// c'est ici qu'il faudrait le dire, en un seul endroit.
  /// </remarks>
  internal static string? In(string screen)
  {
    var advisory = Regex.Match(
      screen, @"<div\b[^>]*\bclass=""advisory""[^>]*>(.*?)</div>", RegexOptions.Singleline);

    return advisory.Success ? advisory.Groups[1].Value : null;
  }
}
