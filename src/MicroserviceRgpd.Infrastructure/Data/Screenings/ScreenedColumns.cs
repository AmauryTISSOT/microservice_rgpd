using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Data.Screenings;

/// <summary>
/// Lit les colonnes d'un rapport <b>une table à la fois</b>, et compte le rapport entier <b>sans le
/// charger</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>C'est la lecture pour laquelle la seconde table existe.</b> L'index unique du triplet a pour
/// colonnes de tête <c>(screening_id, schema_name, table_name)</c> : la table ouverte se lit par cet
/// index, et rien d'autre du rapport n'est touché.
/// </para>
/// <para>
/// ⚠️ <b>Les comptes sont calculés par la base, en une requête.</b> Les rapatrier pour les compter en
/// mémoire aurait rendu la seconde table inutile — cinq mille lignes matérialisées pour en afficher
/// treize — et c'est très exactement ce que ce type existe pour ne pas faire.
/// </para>
/// <para>
/// ⚠️ <b>Aucune de ces requêtes ne filtre les <c>Unflagged</c>.</b> Le <c>where</c> ne porte que sur
/// le rapport et sur la table ; une clause de plus ici rétablirait l'<c>Omission silencieuse</c> un
/// cran plus bas que l'écran, là où personne ne la relit.
/// </para>
/// </remarks>
public sealed class ScreenedColumns(AppDbContext dbContext) : IScreenedColumns
{
  /// <inheritdoc />
  public async Task<IReadOnlyList<ScreenedColumn>> OfTableAsync(
    ScreeningId screening,
    TableIdentity table,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(table);

    return await Of(screening)
      .Where(column =>
        column.Listed.Identity.Schema == table.Schema
        && column.Listed.Identity.Table == table.Table)
      // L'ordre du relevé, jamais l'alphabétique : c'est le seul qui garde à `adr_l1` le voisinage
      // de `adr_l2`, `cp` et `ville`, et ce voisinage est ce qui rend l'arbitrage possible.
      .OrderBy(column => column.Listed.Position)
      .AsNoTracking()
      .ToListAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task<ScreeningCounts> CountsOfAsync(
    ScreeningId screening,
    CancellationToken cancellationToken = default)
  {
    var columns = Of(screening);

    // ⚠️ Les quatre traits sont dans la CLÉ du regroupement, et non dans des `count(filter)` : ce
    // qui les porte — la ligne du relevé, l'arbitrage — sont des types possédés, que le fournisseur
    // sait lire dans un `where` ou une clé mais pas à l'intérieur du prédicat d'un agrégat. Le
    // `group by` rend au plus seize lignes, qu'on additionne ici ; c'est le même aller-retour, et
    // le rapport reste hors de la mémoire du service.
    var buckets = await columns
      .GroupBy(column => new
      {
        Flagged = column.Category != PersonalDataCategory.Unflagged,
        Retained = column.Arbitration != null
          && column.Arbitration.State == ScreenedColumnState.Retained,
        SetAside = column.Arbitration != null
          && column.Arbitration.State == ScreenedColumnState.SetAside,
        WithoutAComment = column.Listed.ColumnComment == null && column.Listed.TableComment == null,
      })
      .Select(group => new
      {
        group.Key.Flagged,
        group.Key.Retained,
        group.Key.SetAside,
        group.Key.WithoutAComment,
        Count = group.Count(),
      })
      .ToListAsync(cancellationToken);

    // Le compte des tables est un `count(distinct (schéma, table))`, qui ne se plie pas dans le
    // regroupement précédent. Un second agrégat sur l'index du rapport reste sans commune mesure
    // avec le rapatriement de cinq mille lignes que ce type existe pour éviter.
    var tables = await columns
      .Select(column => new { column.Listed.Identity.Schema, column.Listed.Identity.Table })
      .Distinct()
      .CountAsync(cancellationToken);

    var total = buckets.Sum(bucket => bucket.Count);
    var retained = buckets.Where(bucket => bucket.Retained).Sum(bucket => bucket.Count);
    var setAside = buckets.Where(bucket => bucket.SetAside).Sum(bucket => bucket.Count);

    return new ScreeningCounts(
      total,
      tables,
      buckets.Where(bucket => bucket.WithoutAComment).Sum(bucket => bucket.Count),
      buckets.Where(bucket => bucket.Flagged).Sum(bucket => bucket.Count),
      retained,
      setAside,
      // « En attente » EST l'absence de signature, et les trois états sont exhaustifs : le déduire
      // évite un troisième aller-retour, et la contrainte de contrôle de la table — l'arbitrage est
      // entier ou absent — est ce qui rend la soustraction sûre plutôt qu'astucieuse.
      total - retained - setAside,
      buckets.Where(bucket => !bucket.Flagged && bucket.Retained).Sum(bucket => bucket.Count),
      buckets.Where(bucket => !bucket.Flagged && !bucket.Retained && !bucket.SetAside)
        .Sum(bucket => bucket.Count));
  }

  /// <summary>
  /// Les colonnes d'un rapport, et de lui seul. <b>La borne au rapport est la seule que toutes ces
  /// lectures partagent</b> — l'oublier ferait compter le déploiement entier.
  /// </summary>
  private IQueryable<ScreenedColumn> Of(ScreeningId screening)
  {
    return dbContext.ScreenedColumns.Where(column =>
      EF.Property<ScreeningId>(column, ScreenedColumnConfiguration.ScreeningForeignKey) == screening);
  }
}
