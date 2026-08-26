namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Le SGBD que le <c>Scan</c> va joindre. Trois membres, et pas un de plus : ce sont les trois
/// dialectes dont le service sait produire le format pivot, et le quatrième naîtra d'un ticket, pas
/// d'une chaîne de caractères passée en douce.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le nom pivot est celui que la requête du chemin collé écrit déjà.</b> <c>releves/*.sql</c>
/// déclare <c>postgresql</c>, <c>mariadb</c> et <c>sqlite</c> dans son en-tête ; la voie connectée
/// écrit <b>les mêmes</b>. Deux vocabulaires pour un même champ seraient deux relevés de la même
/// base qui ne se compareraient plus — « un seul format pivot » ne tient pas si le dialecte s'écrit
/// de deux façons selon le chemin.
/// </para>
/// <para>
/// ⚠️ <b>MariaDB et MySQL ne font qu'un membre.</b> Le service les joint par le même pilote, lit le
/// même catalogue et écrit le même nom pivot ; les séparer donnerait deux membres que rien, dans le
/// code, ne saurait distinguer — et un <c>Operator</c> qui choisit mal serait puni d'un choix qui
/// ne change rien.
/// </para>
/// </remarks>
public sealed class DatabaseDialect : SmartEnum<DatabaseDialect>
{
  public static readonly DatabaseDialect PostgreSql = new(
    nameof(PostgreSql),
    1,
    "postgresql",
    "PostgreSQL");

  public static readonly DatabaseDialect MySql = new(
    nameof(MySql),
    2,
    "mariadb",
    "MariaDB/MySQL");

  public static readonly DatabaseDialect Sqlite = new(nameof(Sqlite), 3, "sqlite", "SQLite");

  private DatabaseDialect(string name, int value, string pivotName, string frenchLabel)
    : base(name, value)
  {
    PivotName = pivotName;
    FrenchLabel = frenchLabel;
  }

  /// <summary>
  /// Ce que la ligne d'en-tête du pivot déclare, à l'octet près, des deux côtés du chemin.
  /// </summary>
  public string PivotName { get; }

  /// <summary>Ce que l'écran de connexion propose à l'<c>Operator</c>.</summary>
  public string FrenchLabel { get; }
}
