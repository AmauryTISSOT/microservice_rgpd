using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Text;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// <b>Le canari à cinq surfaces</b> : un scan joué par le <b>vrai</b> pilote contre une base
/// authentique dont toute valeur est une sentinelle, suivi d'un arbitrage et des deux exports. Il
/// rougit si une sentinelle se retrouve sur l'une des cinq surfaces par lesquelles ce service peut
/// laisser échapper ce qu'un client lui a confié.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Les cinq assertions restent nommées séparément, en deux groupes.</b> Trois disent
/// <c>Ce qui entre est borné</c> — les journaux, les tags de trace, les exceptions : ce qui
/// <b>fuit</b>, vers un collecteur, un signalement de bogue, un onglet. Deux disent
/// <c>Rien de réel ne reste</c> — la base du service, les deux exports : ce qui <b>reste</b>, après
/// que l'écran s'est fermé. Les fondre en un seul « aucune sentinelle nulle part » referait au banc
/// d'essai le montage à champ unique que le glossaire refuse dans le modèle : deux promesses
/// distinctes, deux modes de panne distincts, et un rouge qui ne dirait plus laquelle est tombée.
/// </para>
/// <para>
/// ⚠️ <b>C'est le seul test fonctionnel qui ouvre une base réelle, et il le doit.</b> Une doublure
/// de scanner ne peut, par construction, rien laisser fuir d'une base qu'elle n'ouvre pas : un
/// canari posé sur elle aurait été vert le jour où le pilote se serait mis à recopier une valeur
/// dans un message d'erreur. Les <b>écrans</b> de scan, eux, restent éprouvés sur la doublure.
/// </para>
/// <para>
/// ⚠️ <b>Le scénario est rejoué par chacune des cinq assertions, et ce n'est pas du gaspillage.</b>
/// Retenir le résultat d'un unique passage dans un état de classe aurait rendu les cinq
/// dépendantes de leur ordre, et un rouge se serait déplacé d'un test à l'autre au gré du
/// coureur. Un scan de cinq lignes coûte moins qu'un diagnostic faux.
/// </para>
/// </remarks>
[Collection(ARealScannerWebCollection.Name)]
public class TheCanaryOfFiveSurfaces(ARealScannerWebApplicationFactory factory)
{
  private readonly ARealScannerWebApplicationFactory _factory = factory;

  // ─── Ce qui entre est borné : ce qui FUIT ───────────────────────────────────────────────────

  /// <summary>
  /// Aucune ligne de journal ne porte de sentinelle, ni en message rendu, ni en propriété
  /// structurée, ni en portée.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Un journal se recopie, se centralise et se garde des mois</b> : ce qui y entre n'en sort
  /// plus. C'est la surface par laquelle une chaîne de connexion s'échappe le plus banalement — un
  /// <c>LoggingBehavior</c> qui recopie les propriétés d'une requête suffit.
  /// </remarks>
  [Fact]
  public async Task NoLogLineCarriesASentinel()
  {
    using var database = ABaseOfSentinels.Written();

    _factory.Logs.Clear();

    await RunAsync(database);

    foreach (var sentinel in ABaseOfSentinels.All)
    {
      _factory.Logs.Mention(sentinel).ShouldBeFalse(
        $"La sentinelle « {sentinel} » a été journalisée. Un journal se recopie, se centralise et "
        + "se garde des mois : ce qui y entre n'en sort plus.");
    }
  }

  /// <summary>Aucun tag de trace, sur aucune <c>Activity</c>, ne porte de sentinelle.</summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Les tags de trace partent chez un tiers.</b> Une valeur posée là voyage vers un
  /// collecteur que le service ne contrôle pas, et elle y reste aussi longtemps que la rétention de
  /// ce collecteur — c'est-à-dire hors de portée de toute promesse que ce contexte peut tenir.
  /// </para>
  /// <para>
  /// ⚠️ <b>Et l'on vérifie d'abord que l'écouteur a entendu quelque chose.</b> C'est la seule des
  /// cinq surfaces qu'on ne puisse pas faire rougir en injectant une sentinelle dans du code de
  /// production — il faudrait poser un tag pour l'occasion. Elle a donc besoin de sa propre preuve
  /// de morsure : une liste vide — parce que le scan aurait cessé d'ouvrir une <c>Activity</c>, ou
  /// parce que l'écouteur ne serait plus branché — rendrait cette assertion verte pour toujours,
  /// sur une surface que plus personne ne regarde. C'est le motif de
  /// <c>SeesThePostgreSqlDriverItWatches</c>, appliqué à une trace.
  /// </para>
  /// </remarks>
  [Fact]
  public async Task NoActivityTagCarriesASentinel()
  {
    using var database = ABaseOfSentinels.Written();

    var tagged = new List<string>();

    using (var listener = new ActivityListener
    {
      ShouldListenTo = _ => true,
      Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllData,
      SampleUsingParentId = (ref ActivityCreationOptions<string> _) =>
        ActivitySamplingResult.AllData,
      ActivityStopped = activity =>
      {
        lock (tagged)
        {
          tagged.AddRange(activity.Tags.Select(tag => $"{tag.Key}={tag.Value}"));
          tagged.Add(activity.DisplayName);
        }
      },
    })
    {
      ActivitySource.AddActivityListener(listener);

      await RunAsync(database);
    }

    lock (tagged)
    {
      tagged.ShouldNotBeEmpty(
        "L'écouteur n'a capté aucune Activity pendant le scénario du canari. Soit le scan a cessé "
        + "d'en ouvrir une, soit cet écouteur n'est plus branché — dans les deux cas l'assertion "
        + "qui suit afficherait vert pour toujours, sur une surface que plus personne ne lit.");

      foreach (var sentinel in ABaseOfSentinels.All)
      {
        tagged.ShouldNotContain(
          tag => tag.Contains(sentinel, StringComparison.OrdinalIgnoreCase),
          $"La sentinelle « {sentinel} » est posée sur un tag de trace, qui part chez un tiers.");
      }
    }
  }

  /// <summary>
  /// Aucune exception qui traverse le port de scan ne porte de sentinelle, ni dans son message, ni
  /// dans sa pile.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Les quatre chemins sont joués, pas seulement l'heureux.</b> Le message d'un pilote est
  /// très exactement l'endroit où un chemin de fichier se recopie : « base introuvable » nomme le
  /// fichier, « ce n'est pas une base » aussi. Un canari qui n'aurait scanné que ce qui marche
  /// n'aurait rien gardé du seul cas où le secret sort tout seul.
  /// </para>
  /// <para>
  /// ⚠️ <b>Ce que le port rend n'est pas une exception, et c'est le fond de l'affaire.</b> Un échec
  /// nommé — phase et famille — ne porte aucun texte du pilote ; si une exception traverse malgré
  /// tout, elle est ici, et son <c>ToString()</c> porte message <b>et</b> pile.
  /// </para>
  /// </remarks>
  [Fact]
  public async Task NoCrossingExceptionCarriesASentinel()
  {
    using var database = ABaseOfSentinels.Written();

    var crossing = new List<string>();

    crossing.Add(await WhatCrossedAsync(() => RunAsync(database)));

    var scanner = _factory.Services.GetRequiredService<IDatabaseScanner>();

    foreach (var refused in RefusedConnectionStrings(database))
    {
      crossing.Add(await WhatCrossedAsync(
        () => scanner.ScanAsync(DatabaseDialect.Sqlite, refused)));
    }

    foreach (var sentinel in ABaseOfSentinels.All)
    {
      crossing.ShouldNotContain(
        crossed => crossed.Contains(sentinel, StringComparison.OrdinalIgnoreCase),
        $"Une exception a traversé le port en portant la sentinelle « {sentinel} ». Une exception "
        + "finit dans un signalement de bogue, une page d'erreur ou un collecteur, et elle y "
        + "emmène tout ce que son message recopie.");
    }
  }

  // ─── Rien de réel ne reste : ce qui RESTE ───────────────────────────────────────────────────

  /// <summary>
  /// Aucune colonne d'aucune table de la base du service ne porte de sentinelle après le scan.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Le balayage est fait sur le catalogue, pas sur une liste écrite à la main.</b> Une liste
  /// de tables serait juste le jour où on l'écrit et fausse à la migration suivante : c'est
  /// précisément la table neuve, celle que personne n'a pensé à ajouter, qui retiendrait la valeur.
  /// </para>
  /// <para>
  /// ⚠️ <b>Toutes les colonnes sont lues en texte, pas seulement celles de type texte.</b> Une
  /// valeur peut se ranger dans un <c>jsonb</c>, un tableau ou une colonne binaire ; ne balayer que
  /// le texte aurait laissé trois formes de rétention hors du canari.
  /// </para>
  /// </remarks>
  [Fact]
  public async Task TheServiceDatabaseCarriesNoSentinelAfterTheScan()
  {
    using var database = ABaseOfSentinels.Written();

    await RunAsync(database);

    var holding = await WhereTheServiceDatabaseHoldsASentinelAsync();

    holding.ShouldBeEmpty(
      "La base du service retient une sentinelle : "
      + string.Join(", ", holding)
      + ". Elle survivrait au processus, à l'écran, et à la session d'arbitrage — ce qu'aucune "
      + "valeur lue dans la base d'un client ne doit faire.");
  }

  /// <summary>
  /// Ni la cartographie en JSON ni celle en CSV ne portent de sentinelle.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est le fichier qui quitte le service, et il part sans retour.</b> Une cartographie se
  /// colle dans un courriel, se dépose sur un partage, s'ouvre dans un tableur : une valeur qui y
  /// entre n'a plus aucune durée de vie. La cartographie porte des <b>noms de colonnes</b> et des
  /// états d'arbitrage ; elle ne porte aucune valeur, et c'est ce qui est éprouvé ici.
  /// <para>
  /// ⚠️ <b>L'écran d'une table, lui, porte bien les valeurs, et le canari ne le lui reproche pas.</b>
  /// Un aperçu vit en mémoire du processus sous deux heures glissantes et douze de plafond : il est
  /// ce que l'<c>Operator</c> lit pour arbitrer, et il meurt avec la session. Un export n'a pas de
  /// fin — c'est ce qui sépare les deux, et c'est pourquoi une seule des deux surfaces est ici.
  /// </para>
  /// </remarks>
  [Fact]
  public async Task NeitherExportCarriesASentinel()
  {
    using var database = ABaseOfSentinels.Written();

    var surface = await RunAsync(database);

    foreach (var (format, exported) in surface.Exports)
    {
      foreach (var sentinel in ABaseOfSentinels.All)
      {
        exported.ShouldNotContain(
          sentinel,
          Case.Insensitive,
          $"La cartographie exportée en {format} porte la sentinelle « {sentinel} ». Un fichier "
          + "exporté n'a plus de durée de vie : ce qu'il emporte ne s'efface plus.");
      }
    }
  }

  // ─── Le scénario, et ce qu'il laisse derrière lui ───────────────────────────────────────────

  /// <summary>
  /// Les chaînes que le canari fait refuser, toutes porteuses de la sentinelle de chemin : un
  /// fichier absent, un fichier qui n'est pas une base, et une chaîne que le pilote ne sait pas
  /// lire.
  /// </summary>
  private static IEnumerable<string> RefusedConnectionStrings(ABaseOfSentinels database)
  {
    var absent = Path.Combine(
      Path.GetDirectoryName(database.FilePath)!,
      "celle-qui-n-existe-pas.db");

    var notADatabase = Path.Combine(
      Path.GetDirectoryName(database.FilePath)!,
      "pas-une-base.db");

    File.WriteAllText(notADatabase, "ceci n'est pas une base SQLite");

    yield return $"Data Source={absent}";
    yield return $"Data Source={notADatabase}";

    // Une chaîne que le pilote refuse de lire : c'est son message d'erreur, s'il traversait, qui la
    // recopierait entière.
    yield return $"Data Source={database.FilePath};Mode=CeQuiNExistePas";
  }

  /// <summary>Ce qu'une action a laissé traverser — message et pile —, ou rien.</summary>
  private static async Task<string> WhatCrossedAsync(Func<Task> acting)
  {
    try
    {
      await acting();

      return string.Empty;
    }
    catch (Exception crossed)
    {
      return crossed.ToString();
    }
  }

  /// <summary>
  /// Joue le scénario entier : le scan par la voie connectée, l'arbitrage de deux colonnes, et les
  /// deux exports.
  /// </summary>
  private async Task<WhatTheRunLeft> RunAsync(ABaseOfSentinels database)
  {
    var scan = new ScanSurface(_factory);

    var scanId = await scan.LaunchAsync(
      database.ConnectionString,
      DatabaseDialect.Sqlite.PivotName);

    var ended = await scan.UntilItEndsAsync(scanId);

    // ⚠️ Le 303 est la fin heureuse, et l'exiger fait partie du canari : un scan qui aurait échoué
    // n'aurait lu aucune valeur, et les cinq surfaces seraient vertes pour la pire des raisons.
    ended.StatusCode.ShouldBe(
      HttpStatusCode.SeeOther,
      "Le scan n'a pas rendu de rapport : le canari n'aurait alors rien de réel à garder.");

    var screening = new ScreeningSurface(_factory);

    // ⚠️ L'arbitrage fait partie du scénario, et pas par décor : c'est lui qui écrit dans la base du
    // service, et donc le seul geste qui pourrait y déposer une valeur lue.
    // Le succès d'un arbitrage REDIRIGE : l'exiger garde le canari d'un scénario qui se serait
    // arrêté sur un refus, et qui n'aurait alors rien écrit dans la base du service.
    (await screening.ArbitrateAsync(
      "nom",
      ScreenedColumnState.Retained.Name,
      ABaseOfSentinels.Table,
      ABaseOfSentinels.Schema)).StatusCode.ShouldBe(HttpStatusCode.Redirect);

    (await screening.ArbitrateAsync(
      "courriel",
      ScreenedColumnState.SetAside.Name,
      ABaseOfSentinels.Table,
      ABaseOfSentinels.Schema)).StatusCode.ShouldBe(HttpStatusCode.Redirect);

    // ⚠️ L'écran de la table est lu, et il n'est PAS asserté — il porte les valeurs, et c'est sa
    // raison d'être. Un aperçu vit en mémoire du processus, sous deux heures glissantes et douze de
    // plafond : « rien de réel ne RESTE » porte sur ce qui survit à l'écran, pas sur ce que l'écran
    // montre à l'Operator pendant qu'il arbitre. Le lire fait partie du scénario parce que c'est le
    // seul geste par lequel une valeur lue traverse le rendu HTML — et donc le seul qui pourrait la
    // faire tomber, en passant, dans un journal ou sur un tag de trace.
    await screening.Client.GetStringAsync(
      $"{ScreeningSurface.Table}?schema={ABaseOfSentinels.Schema}&table={ABaseOfSentinels.Table}");

    var json = await screening.Client.GetStringAsync(ScreeningSurface.MapAsJson);
    var csv = await screening.Client.GetByteArrayAsync(ScreeningSurface.MapAsCsv);

    return new WhatTheRunLeft([("JSON", json), ("CSV", Encoding.UTF8.GetString(csv))]);
  }

  /// <summary>
  /// Les endroits de la base du service — table et colonne — où une sentinelle se lit. Vide est la
  /// seule réponse acceptable.
  /// </summary>
  private async Task<IReadOnlyList<string>> WhereTheServiceDatabaseHoldsASentinelAsync()
  {
    using var scope = _factory.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var connection = context.Database.GetDbConnection();

    await context.Database.OpenConnectionAsync();

    var columns = await CataloguedColumnsAsync(connection);
    var holding = new List<string>();

    foreach (var (schema, table, column) in columns)
    {
      foreach (var sentinel in ABaseOfSentinels.All)
      {
        using var command = connection.CreateCommand();

        command.CommandText =
          $"""SELECT COUNT(1) FROM "{schema}"."{table}" WHERE CAST("{column}" AS text) LIKE @motif""";

        var motif = command.CreateParameter();
        motif.ParameterName = "@motif";
        motif.Value = $"%{sentinel}%";
        command.Parameters.Add(motif);

        if (Convert.ToInt64(await command.ExecuteScalarAsync(), provider: null) > 0)
        {
          holding.Add($"{schema}.{table}.{column} porte « {sentinel} »");
        }
      }
    }

    return holding;
  }

  /// <summary>Toutes les colonnes que le catalogue de la base du service présente.</summary>
  private static async Task<IReadOnlyList<(string Schema, string Table, string Column)>>
    CataloguedColumnsAsync(DbConnection connection)
  {
    using var command = connection.CreateCommand();

    command.CommandText = """
      SELECT c.table_schema, c.table_name, c.column_name
        FROM information_schema.columns c
        JOIN information_schema.tables t
          ON t.table_schema = c.table_schema
         AND t.table_name = c.table_name
       WHERE t.table_type = 'BASE TABLE'
         AND c.table_schema NOT IN ('pg_catalog', 'information_schema')
      """;

    using var reader = await command.ExecuteReaderAsync();
    var columns = new List<(string, string, string)>();

    while (await reader.ReadAsync())
    {
      columns.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
    }

    return columns;
  }

  /// <summary>Tout ce qu'un passage du scénario a rendu lisible à qui n'était pas là.</summary>
  private sealed record WhatTheRunLeft(IReadOnlyList<(string Format, string Content)> Exports);
}
