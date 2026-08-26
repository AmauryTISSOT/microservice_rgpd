using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings.Scanning.PostgreSql;

/// <summary>
/// Ce qu'une base PostgreSQL sait répondre : sa présence au catalogue de schémas, son catalogue, et
/// les quelques valeurs d'une table. <b>Trois lectures, et rien d'autre.</b>
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Cette couture existe pour que le scan s'éprouve sans conteneur.</b> Les faits par dialecte
/// ont été mesurés une fois, hors du dépôt (#275), et aucun test à conteneurs n'est ajouté ; ce qui
/// reste à tenir dans le processus, ce sont les <b>décisions</b> — les deux fins à zéro objet, la
/// table qui rate sans emporter le relevé, l'avancement qui compte pour de vrai. Sans elle, ces
/// décisions ne seraient éprouvées nulle part, et un dialecte de plus les réécrirait au jugé.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne connaît ni chaîne de connexion, ni pilote.</b> Ce qu'elle rend est déjà du
/// vocabulaire du service — un <see cref="CataloguedColumn"/>, un <see cref="PreviewedValue"/> ; ce
/// qui rate remonte en exception du pilote, que <see cref="PostgreSqlScan"/> traduit à un seul
/// endroit.
/// </para>
/// </remarks>
internal interface IPostgreSqlSession
{
  /// <summary>Le nom de la base, et combien de schémas applicatifs le catalogue lui connaît.</summary>
  Task<PostgreSqlPresence> ReadPresenceAsync(CancellationToken cancellationToken);

  /// <summary>Le catalogue entier, positions déjà renumérotées.</summary>
  Task<List<CataloguedColumn>> ReadCatalogueAsync(CancellationToken cancellationToken);

  /// <summary>
  /// Les valeurs lues pour chacune des colonnes demandées, <b>dans le même ordre qu'elles</b> — une
  /// liste vide pour une colonne dont la lecture n'a rien retourné.
  /// </summary>
  Task<IReadOnlyList<IReadOnlyList<PreviewedValue>>> SampleAsync(
    string schema,
    string table,
    IReadOnlyList<string> columns,
    CancellationToken cancellationToken);
}

/// <summary>
/// Ce que la lecture d'existence rapporte : le nom que le pivot déclarera, et de quoi séparer les
/// deux fins à zéro objet.
/// </summary>
/// <remarks>
/// ⚠️ <b>Zéro schéma applicatif et zéro table ne disent pas la même chose.</b> Une base <b>absente
/// du catalogue de schémas</b> envoie l'<c>Operator</c> demander un accès ; une base <b>sans
/// table</b> ne lui fait rien faire. Les confondre le ferait chercher une base vide quand il lui
/// faut un droit.
/// </remarks>
internal sealed record PostgreSqlPresence(string Database, long ApplicationSchemas);
