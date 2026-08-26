using Microsoft.Data.Sqlite;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// Une base SQLite <b>authentique</b>, écrite dans un fichier temporaire et détruite à la fin du
/// test. C'est la moitié du filet de sécurité que le lint sur le texte des requêtes ne couvre pas :
/// une requête peut être irréprochable à la lecture et fausse à l'exécution.
/// </summary>
/// <remarks>
/// ⚠️ <b>Elle n'est pas en mémoire, et c'est voulu.</b> Le scanner ouvre en <c>Mode=ReadOnly</c> —
/// ce qui empêche SQLite de créer en douce le fichier d'une faute de frappe —, et une base
/// <c>:memory:</c> ne se rouvre pas depuis une seconde connexion. Le fichier est donc le seul
/// support qui éprouve le scanner tel qu'il tourne.
/// </remarks>
internal sealed class ASqliteBase : IDisposable
{
  private ASqliteBase(string path)
  {
    Path = path;
  }

  /// <summary>Le chemin du fichier. Seul son nom devra ressortir du scan.</summary>
  public string Path { get; }

  /// <summary>Le nom du fichier, qui est le seul <c>base</c> que le pivot a le droit de déclarer.</summary>
  public string FileName => System.IO.Path.GetFileName(Path);

  /// <summary>La chaîne de connexion telle qu'un <c>Operator</c> la fournirait.</summary>
  public string ConnectionString => $"Data Source={Path}";

  /// <summary>Écrit une base neuve et y joue le script donné.</summary>
  public static ASqliteBase Holding(string script)
  {
    var path = System.IO.Path.Combine(
      System.IO.Path.GetTempPath(),
      $"scan-{Guid.NewGuid():N}.db");

    var writable = new SqliteConnectionStringBuilder
    {
      DataSource = path,
      Mode = SqliteOpenMode.ReadWriteCreate,
      Pooling = false,
    }.ConnectionString;

    using (var connection = new SqliteConnection(writable))
    {
      connection.Open();

      using var command = connection.CreateCommand();
      command.CommandText = script;
      command.ExecuteNonQuery();
    }

    return new ASqliteBase(path);
  }

  /// <summary>Une base vide : le fichier existe, il ne porte aucune table.</summary>
  public static ASqliteBase Empty()
  {
    // PRAGMA user_version force SQLite à matérialiser le fichier sans y créer d'objet.
    return Holding("PRAGMA user_version = 1;");
  }

  public void Dispose()
  {
    SqliteConnection.ClearAllPools();

    try
    {
      File.Delete(Path);
    }
    catch (IOException)
    {
      // Le fichier temporaire d'un test qui a fini : s'il résiste, le dossier temporaire s'en
      // chargera. Faire échouer le test là-dessus n'apprendrait rien.
    }
  }
}
