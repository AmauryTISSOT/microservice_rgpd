using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ReadScreeningTable;

/// <summary>
/// Rend une table du rapport de détection courant, <b>clause comprise</b> — ou rien du tout quand
/// aucune détection n'a été lancée, ou quand le courant ne porte pas cette table.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il ne charge pas le rapport.</b> L'entête arrive seul, les colonnes de la table par leur
/// propre <c>DbSet</c>, et les comptes du rapport entier par la base : c'est exactement ce que la
/// seconde table de la persistance achète, et le seul chemin qui rende treize colonnes sans en
/// rematérialiser cinq mille à chaque rafraîchissement.
/// </para>
/// <para>
/// ⚠️ <b>La clause est attachée ici, et elle porte sur le rapport entier.</b> Une clause bornée à la
/// table ouverte aurait dit d'un écran de treize colonnes qu'il est le périmètre lu de la détection.
/// </para>
/// <para>
/// ⚠️ <b>C'est le seul écran qui touche au cache d'aperçus, et le toucher <em>est</em> réarmer.</b>
/// « Deux heures réarmées par les seuls écrans qui montrent des aperçus » est tenu par là : le
/// rapport, l'historique, l'archive et l'accueil ne prolongent rien parce qu'aucun d'eux n'a de
/// <see cref="ScanPreviews"/> à appeler.
/// </para>
/// </remarks>
/// <param name="screenings">Les rapports du déploiement, en lecture seule.</param>
/// <param name="columns">Les colonnes du rapport, lues par table.</param>
/// <param name="previews">Le cache mémoire du jeu d'aperçus vivant.</param>
public sealed class ReadScreeningTableHandler(
  IReadRepository<Screening> screenings,
  IScreenedColumns columns,
  ScanPreviews previews)
  : IQueryHandler<ReadScreeningTableQuery, ScreeningAnswer<ScreenedTable>?>
{
  /// <inheritdoc />
  public async ValueTask<ScreeningAnswer<ScreenedTable>?> Handle(
    ReadScreeningTableQuery query,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var current = await screenings.FirstOrDefaultAsync(
      new CurrentScreeningHeaderSpec(), cancellationToken);

    if (current is null)
    {
      return null;
    }

    var read = await columns.OfTableAsync(current.Id, query.Table, cancellationToken);

    // Le courant ne porte pas cette table : une adresse mal recopiée, ou une seconde détection sur
    // une
    // base d'où la table a disparu. Une table vide portant la clause aurait fait passer l'un pour
    // l'autre.
    if (read.Count == 0)
    {
      return null;
    }

    var counts = await columns.CountsOfAsync(current.Id, cancellationToken);

    // ⚠️ LE CACHE EST TOUCHÉ ICI, ET CE TOUCHER RÉARME LES DEUX HEURES GLISSANTES. Il l'est APRÈS
    // les deux refus au-dessus : réarmer sur une adresse mal recopiée aurait fait prolonger les
    // aperçus par un écran qui ne se rend pas.
    //
    // ⚠️ ET L'ORIGINE EST LUE ICI, JAMAIS DANS LE CACHE. Un relevé COLLÉ n'a rien à annoncer : rien
    // n'y a jamais été prélevé, et lui demander l'état de ses aperçus aurait fait dire au cache
    // « ils ont expiré » d'un prélèvement qui n'a pas eu lieu. Le cache ne connaît qu'une durée ;
    // savoir par quel chemin un relevé est entré est une affaire de cas d'usage.
    var living = current.Origin == ListingOrigin.Scanned
      ? previews.Show(current.Id)
      : ScreeningPreviews.NeverTaken;

    return new ScreeningAnswer<ScreenedTable>(
      ScreenedTable.Of(current, query.Table, read, counts, living),
      IncompletenessClause.For(counts, current.Origin));
  }
}
