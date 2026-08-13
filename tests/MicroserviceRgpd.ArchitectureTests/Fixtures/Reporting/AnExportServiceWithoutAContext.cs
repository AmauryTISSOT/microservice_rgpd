using MicroserviceRgpd.ArchitectureTests.Fixtures.Casework;
using MicroserviceRgpd.ArchitectureTests.Fixtures.Screening;

namespace MicroserviceRgpd.ArchitectureTests.Fixtures.Reporting;

/// <summary>
/// Le témoin de l'angle mort, écrit avec les noms du dépôt : un service <b>sans contexte</b> —
/// aucun segment de son espace de noms n'en nomme un — qui lit d'un côté et écrit de l'autre.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est le cas qui fait mal, et <c>ContextIsolationTests</c> reste vert sur toute la
/// matrice devant lui.</b> Un type qui n'habite aucun contexte n'est jamais un <c>from</c> ni un
/// <c>to</c> : il n'apparaît dans aucune paire ordonnée, et la matrice n'a donc rien à examiner.
/// Le <c>ScreeningExportService</c> que la liste <i>Avoid</i> de <c>Suggéré, jamais déclaré</c>
/// bannit nommément est exactement cette forme — il rapprocherait les colonnes retenues des
/// <c>DeclaredSystem</c> pour pré-remplir le <c>Manifest</c>.
/// </para>
/// <para>
/// Il vit dans un dossier <c>Reporting/</c> pour la même raison : c'est le nom qu'un tel fichier
/// prendrait vraiment. Personne n'appelle le sien <c>Contourne/</c>.
/// </para>
/// </remarks>
internal sealed class AnExportServiceWithoutAContext
{
  internal static string Export()
  {
    return new AnAggregateCarryingALibraryMarker().Name + new AHandlerThatStaysHome().Handle(" ");
  }
}
