using Microsoft.Data.Sqlite;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// Une base SQLite <b>authentique</b> dont <b>toute</b> valeur est une sentinelle improbable, posée
/// sous un dossier dont le nom en est une lui aussi. C'est la matière du canari à cinq surfaces :
/// tout ce que le service pourrait laisser échapper d'une base réelle est, ici, reconnaissable.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La sentinelle du chemin et celles des valeurs sont deux familles distinctes.</b> Elles ne
/// se cherchent pas au même endroit et ne se perdent pas de la même façon : le chemin est un
/// <b>secret d'accès</b>, que seuls les journaux, les traces et les exceptions peuvent trahir ; les
/// valeurs sont des <b>données personnelles</b>, que seuls la base du service et les exports peuvent
/// retenir. Une seule sentinelle pour les deux aurait rendu vert un canari qui ne garde qu'une
/// moitié.
/// </para>
/// <para>
/// ⚠️ <b>Le nom du fichier, lui, n'est pas une sentinelle, et c'est délibéré.</b> Le scanner déclare
/// le nom du fichier comme <c>base</c> du pivot — il <b>doit</b> ressortir, dans le rapport comme
/// dans la cartographie. Le mettre en sentinelle aurait fait rougir le canari sur la seule chose que
/// la chaîne de connexion a le droit de laisser derrière elle. C'est le <b>dossier</b> qui porte la
/// sentinelle : « seul le nom du fichier part dans le pivot, jamais le chemin » est très exactement
/// ce qui est éprouvé.
/// </para>
/// <para>
/// ⚠️ <b>Elle n'est pas en mémoire.</b> Le scanner ouvre en <c>Mode=ReadOnly</c>, et une base
/// <c>:memory:</c> ne se rouvre pas depuis une seconde connexion : le fichier est le seul support
/// qui éprouve le scanner tel qu'il tourne.
/// </para>
/// </remarks>
internal sealed class ABaseOfSentinels : IDisposable
{
  /// <summary>Le préfixe de toute valeur que la base porte — et de rien d'autre au monde.</summary>
  internal const string ValuePrefix = "SENTINELLE-311-VALEUR";

  /// <summary>
  /// Le segment de chemin de la chaîne de connexion, qui n'a le droit d'apparaître nulle part.
  /// </summary>
  internal const string PathSentinel = "sentinelle-311-chemin-ne-doit-pas-fuiter";

  /// <summary>Le nom du fichier, seul <c>base</c> que le pivot a le droit de déclarer.</summary>
  internal const string FileName = "galette-prod.db";

  /// <summary>Le schéma que SQLite nomme, et sous lequel les tables s'arbitrent.</summary>
  internal const string Schema = "main";

  /// <summary>La seule table de la base, celle dont les colonnes s'arbitrent.</summary>
  internal const string Table = "adherents";

  /// <summary>Combien de colonnes le relevé doit porter, pas une de moins.</summary>
  internal const int Columns = 4;

  /// <summary>
  /// Ce que la base porte : quatre colonnes dont trois qu'un moteur à lexiques gelés signale, une
  /// table, et cinq lignes de valeurs toutes sentinelles.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Les noms d'objets sont ordinaires, et ils le restent.</b> Une cartographie <b>est</b> une
  /// liste de noms de colonnes : les rendre sentinelles aurait fait rougir le canari sur son propre
  /// livrable. Ce qui ne doit pas ressortir, ce sont les <b>valeurs</b> de ces colonnes.
  /// </remarks>
  private const string Script = """
    CREATE TABLE adherents (
      id_adherent TEXT NOT NULL,
      nom TEXT,
      courriel TEXT,
      date_naissance TEXT
    );
    """;

  private ABaseOfSentinels(string directory, string path)
  {
    Directory = directory;
    Path = path;
  }

  /// <summary>Toutes les sentinelles, celle du chemin comprise : ce que le canari cherche.</summary>
  internal static IReadOnlyList<string> All => [ValuePrefix, PathSentinel];

  /// <summary>Le chemin du fichier. Seul son nom devra ressortir du scan.</summary>
  internal string Path { get; }

  /// <summary>
  /// La chaîne de connexion telle qu'un <c>Operator</c> la fournirait — sentinelle comprise.
  /// </summary>
  internal string ConnectionString => $"Data Source={Path}";

  /// <summary>Le dossier dont le nom porte la sentinelle du chemin.</summary>
  private string Directory { get; }

  /// <summary>Écrit la base et y pose cinq lignes dont chaque valeur est une sentinelle.</summary>
  internal static ABaseOfSentinels Written()
  {
    var directory = System.IO.Path.Combine(
      System.IO.Path.GetTempPath(),
      $"{PathSentinel}-{Guid.NewGuid():N}");

    System.IO.Directory.CreateDirectory(directory);

    var path = System.IO.Path.Combine(directory, FileName);

    var writable = new SqliteConnectionStringBuilder
    {
      DataSource = path,
      Mode = SqliteOpenMode.ReadWriteCreate,
      Pooling = false,
    }.ConnectionString;

    using (var connection = new SqliteConnection(writable))
    {
      connection.Open();

      using (var schema = connection.CreateCommand())
      {
        schema.CommandText = Script;
        schema.ExecuteNonQuery();
      }

      for (var row = 1; row <= 5; row++)
      {
        using var insert = connection.CreateCommand();

        insert.CommandText =
          "INSERT INTO adherents (id_adherent, nom, courriel, date_naissance) "
          + "VALUES ($id, $nom, $courriel, $naissance)";

        // ⚠️ La clé technique en est une aussi, et c'est pourquoi elle est déclarée en TEXT. « Toutes
        // les valeurs sont des sentinelles » ne souffre pas d'exception : une seule colonne laissée
        // en clair serait le trou par lequel une fuite passerait sans faire rougir le canari.
        insert.Parameters.AddWithValue("$id", $"{ValuePrefix}-id-{row}");
        insert.Parameters.AddWithValue("$nom", $"{ValuePrefix}-nom-{row}");
        insert.Parameters.AddWithValue("$courriel", $"{ValuePrefix}-courriel-{row}@exemple.test");
        insert.Parameters.AddWithValue("$naissance", $"{ValuePrefix}-naissance-{row}");

        insert.ExecuteNonQuery();
      }
    }

    return new ABaseOfSentinels(directory, path);
  }

  public void Dispose()
  {
    SqliteConnection.ClearAllPools();

    try
    {
      System.IO.Directory.Delete(Directory, recursive: true);
    }
    catch (IOException)
    {
      // Le dossier temporaire d'un test qui a fini : s'il résiste, le système s'en chargera. Faire
      // échouer le canari là-dessus n'apprendrait rien de ce qu'il garde.
    }
    catch (UnauthorizedAccessException)
    {
      // Idem : un fichier encore tenu par le pilote sur un système qui verrouille.
    }
  }
}
