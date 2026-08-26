using MicroserviceRgpd.Core.Screenings;
using Npgsql;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning.PostgreSql;

/// <summary>
/// Les <b>décisions</b> d'un scan PostgreSQL, une fois la connexion ouverte : quelle fin rendre,
/// quelle raison donner à une colonne, et quand rapporter un pas franchi.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Rien ici ne connaît le pilote, et c'est ce qui rend tout cela éprouvable sans
/// conteneur.</b> Ce fichier ne parle qu'à un <see cref="IPostgreSqlSession"/> ; les faits par
/// dialecte ont été mesurés hors du dépôt (#275), et ce qui reste à tenir dans le processus — les
/// deux fins à zéro objet, la table qui rate sans emporter le relevé, « droits refusés » posé sur
/// chaque colonne d'une table interdite — se tient ici.
/// </para>
/// <para>
/// ⚠️ <b>La traduction des pannes du pilote vit à un seul endroit.</b> Écrite à la main sur chaque
/// <c>catch</c>, elle aurait fini par oublier une famille sur l'un d'eux — et une
/// <c>NpgsqlException</c> qui traverse le port est une chaîne de connexion qui remonte à l'écran.
/// </para>
/// </remarks>
internal static class PostgreSqlScan
{
  internal static async Task<ScanOutcome> RunAsync(
    IPostgreSqlSession session,
    TimeProvider clock,
    IProgress<ScanStep>? progress,
    CancellationToken cancellationToken)
  {
    PostgreSqlPresence presence;
    List<CataloguedColumn> catalogued;

    try
    {
      presence = await session.ReadPresenceAsync(cancellationToken).ConfigureAwait(false);

      if (presence.ApplicationSchemas == 0)
      {
        return ScanOutcome.DatabaseAbsentFromCatalogue();
      }

      catalogued = await session.ReadCatalogueAsync(cancellationToken).ConfigureAwait(false);
    }
    catch (Exception failure) when (IsDriverFailure(failure))
    {
      ThrowIfCancellation(failure, cancellationToken);

      return ScanOutcome.Failed(ScanPhase.Cataloguing, PostgreSqlFailures.FamilyOf(failure));
    }
    catch (ArgumentException)
    {
      // ⚠️ Un nom d'objet que le domaine refuse — vide, démesuré, porteur d'un caractère de
      // contrôle — vient du schéma du client, pas d'un défaut du service. Le laisser remonter ferait
      // traverser le port une exception, là où l'écran attend une fin nommée ; et le recopier dans
      // un message rendrait au passage un nom de table du client.
      return ScanOutcome.Failed(ScanPhase.Cataloguing, ScanFailureFamily.Database);
    }

    var tables = catalogued
      .GroupBy(column => column.Scanned.Identity.TableIdentity)
      .ToList();

    progress?.Report(ScanStep.CatalogueRead(tables.Count));

    if (catalogued.Count == 0)
    {
      return ScanOutcome.NoTable();
    }

    var previews = new Dictionary<ColumnIdentity, ColumnPreview>();
    var sampled = 0;

    foreach (var table in tables)
    {
      cancellationToken.ThrowIfCancellationRequested();

      await SampleTableAsync(session, [.. table], previews, cancellationToken)
        .ConfigureAwait(false);

      progress?.Report(ScanStep.TableSampled(++sampled, tables.Count));
    }

    var pivot = PivotWriter.Write(
      DatabaseDialect.PostgreSql,
      presence.Database,
      clock.GetUtcNow(),
      [.. catalogued.Select(column => column.Scanned)]);

    return ScanOutcome.Listed(pivot, previews);
  }

  /// <summary>
  /// Ce qui vient du pilote, et qui doit donc devenir une fin nommée plutôt que traverser le port.
  /// </summary>
  internal static bool IsDriverFailure(Exception failure)
  {
    return failure is NpgsqlException or TimeoutException;
  }

  /// <summary>
  /// Traduit en abandon ce qui est notre propre annulation revenue par le serveur, et laisse tout
  /// le reste à l'appelant, qui en fera une fin nommée.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>L'annulation n'est pas une fin du scan.</b> C'est l'appelant qui reprend la main, et un
  /// <see cref="ScanOutcome.Failed"/> ferait d'un abandon volontaire un écran rouge.
  /// </remarks>
  private static void ThrowIfCancellation(Exception failure, CancellationToken cancellationToken)
  {
    if (PostgreSqlFailures.IsCancellation(failure))
    {
      throw new OperationCanceledException(cancellationToken);
    }
  }

  private static async Task SampleTableAsync(
    IPostgreSqlSession session,
    IReadOnlyList<CataloguedColumn> columns,
    Dictionary<ColumnIdentity, ColumnPreview> previews,
    CancellationToken cancellationToken)
  {
    // ⚠️ Le type est porté par la colonne : le binaire s'écarte ici, sur le catalogue, et sa colonne
    // n'entre jamais dans une requête. C'est ce qui laisse la requête de prélèvement sans filtre, et
    // ce qui rend « zéro ligne » univoque — voir PostgreSqlScanQueries.Sample.
    foreach (var binary in columns.Where(column => !column.IsSampleable))
    {
      previews[binary.Scanned.Identity] =
        ColumnPreview.Absent(PreviewAbsenceReason.UnsampleableType);
    }

    var sampleable = columns.Where(column => column.IsSampleable).ToList();

    if (sampleable.Count == 0)
    {
      return;
    }

    // ⚠️ Les noms qui entrent dans la requête sont ceux du catalogue, jamais ceux que le domaine a
    // normalisés — voir ScannedColumn.RawSchema.
    var schema = sampleable[0].Scanned.RawSchema;
    var table = sampleable[0].Scanned.RawTable;

    for (var start = 0;
      start < sampleable.Count;
      start += PostgreSqlScanQueries.MaxColumnsPerQuery)
    {
      // Une table très large se prélève en plusieurs requêtes : sans ce contrôle, l'annulation
      // n'aurait aucune prise entre deux d'entre elles.
      cancellationToken.ThrowIfCancellationRequested();

      var slice = sampleable
        .Skip(start)
        .Take(PostgreSqlScanQueries.MaxColumnsPerQuery)
        .ToList();

      try
      {
        var read = await session.SampleAsync(
          schema,
          table,
          [.. slice.Select(column => column.Scanned.RawColumn)],
          cancellationToken).ConfigureAwait(false);

        for (var index = 0; index < slice.Count; index++)
        {
          var values = read[index];

          // ⚠️ Zéro valeur ne veut ici qu'une chose : la lecture n'a rien retourné. Le binaire a
          // déjà été écarté sur son type, et la phrase attachée à la raison se garde bien de dire
          // que la table est vide — une politique de sécurité au niveau ligne rend zéro ligne d'une
          // table qui en porte des millions.
          previews[slice[index].Scanned.Identity] = values.Count > 0
            ? ColumnPreview.Read(values)
            : ColumnPreview.Absent(PreviewAbsenceReason.NoValueReturned);
        }
      }
      catch (Exception failure) when (IsDriverFailure(failure))
      {
        ThrowIfCancellation(failure, cancellationToken);

        // ⚠️ L'échec d'une table ne fait pas tomber le scan : chaque colonne qu'elle porte reçoit
        // une raison nommée, et le relevé reste entier. Aucune exception du pilote ne remonte, et
        // rien de son message n'est recopié.
        var reason = PostgreSqlFailures.ReasonFor(failure);

        foreach (var column in slice)
        {
          previews.TryAdd(column.Scanned.Identity, ColumnPreview.Absent(reason));
        }
      }
    }
  }
}
