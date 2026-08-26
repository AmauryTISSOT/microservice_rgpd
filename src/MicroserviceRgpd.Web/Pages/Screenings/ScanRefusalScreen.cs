using System.Globalization;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// Ce qu'un <b>second lancement refusé</b> dit du scan qui court : son identité, son SGBD, sa phase,
/// et l'heure où il est parti. Le lien vers son attente, lui, se bâtit sur
/// <see cref="Scan"/>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le refus <i>nomme</i> le scan en cours, et ce n'est pas une politesse.</b> Un « réessayez
/// plus tard » laisserait l'<c>Operator</c> ignorer si le service travaille pour lui ou pour
/// quelqu'un d'autre — et il relancerait, sur la production d'un tiers, une lecture déjà en cours.
/// </para>
/// <para>
/// ⚠️ <b>Le SGBD, et rien de plus de ce que l'autre <c>Operator</c> a fourni.</b> Ni hôte, ni nom de
/// base, ni utilisateur : ceux-là ne se connaissent qu'en <b>découpant la chaîne de connexion</b>, et
/// une chaîne rendue par morceaux reste une chaîne rendue. Le dialecte, lui, est un choix fait dans
/// une liste fermée de trois, que le formulaire montre déjà.
/// </para>
/// <para>
/// ⚠️ <b>Le formatage vit ici, et non dans la vue.</b> Une vue qui appellerait elle-même
/// <c>ToString</c> avec sa culture aurait rendu une heure lisible sur la machine du développeur et
/// autrement ailleurs — et le seul endroit où l'on aurait pu le voir est l'écran d'un
/// <c>Operator</c> qu'on ne regarde pas.
/// </para>
/// </remarks>
/// <param name="Scan">L'identité du scan qui court, celle que porte son adresse d'attente.</param>
/// <param name="Dialect">Son SGBD, dans les mots de l'écran.</param>
/// <param name="Phase">Où il en est, dans les mots de l'écran.</param>
/// <param name="StartedAt">L'heure de son lancement, en temps universel.</param>
public sealed record ScanRefusalScreen(
  Guid Scan,
  string Dialect,
  string Phase,
  string StartedAt)
{
  /// <summary>Ce que l'écran de refus dit du scan qui vient d'empêcher celui-ci de partir.</summary>
  /// <param name="running">Le scan en vol, tel que le lanceur l'a rendu avec son refus.</param>
  /// <exception cref="ArgumentNullException"><paramref name="running"/> est absent.</exception>
  public static ScanRefusalScreen Of(ScanProgress running)
  {
    ArgumentNullException.ThrowIfNull(running);

    return new ScanRefusalScreen(
      running.Id.Value,
      running.Dialect.FrenchLabel,
      running.Snapshot.Phase.FrenchLabel,
      running.StartedOn.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " UTC");
  }
}
