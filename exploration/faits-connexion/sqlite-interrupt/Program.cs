using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;

// Sonde SQLite : peut-on interrompre une requête en cours, et à quel grain ?
// Microsoft documente `SqliteCommand.Cancel()` comme « Does nothing. » et ne dit
// nulle part que `SqliteConnection.Handle` puisse servir à appeler
// `sqlite3_interrupt`. On éprouve les deux voies, plus les points secondaires.

const string RequeteLongue = """
    WITH RECURSIVE compteur(n) AS (
      SELECT 1 UNION ALL SELECT n + 1 FROM compteur WHERE n < 900000000
    )
    SELECT count(*) FROM compteur;
    """;

var fichier = Path.Combine(Path.GetTempPath(), $"sonde-{Environment.ProcessId}.sqlite");
File.Delete(fichier);

Console.WriteLine($"SQLite : {VersionSqlite()}");
Console.WriteLine($"Microsoft.Data.Sqlite : {typeof(SqliteConnection).Assembly.GetName().Version}");
Console.WriteLine();

PreparerLaBase(fichier);

Sonde1_InterruptParPInvoke(fichier, RequeteLongue);
Sonde2_InterruptParSqlitePclRaw(fichier, RequeteLongue);
Sonde3_CancelDeSqliteCommand(fichier, RequeteLongue);
Sonde4_CancellationTokenSurExecuteReaderAsync(fichier, RequeteLongue);
Sonde5_ModeReadOnly(fichier);
Sonde6_TypeofALaValeur(fichier);
Sonde7_RelevePivot(fichier);

try { File.Delete(fichier); } catch (IOException) { /* connexions interrompues encore ouvertes */ }
return;

static string VersionSqlite()
{
    using var c = new SqliteConnection("Data Source=:memory:");
    c.Open();
    using var cmd = c.CreateCommand();
    cmd.CommandText = "select sqlite_version()";
    return (string)cmd.ExecuteScalar()!;
}

static void PreparerLaBase(string fichier)
{
    using var c = new SqliteConnection($"Data Source={fichier}");
    c.Open();
    using var cmd = c.CreateCommand();
    cmd.CommandText = """
        CREATE TABLE adherents (id INTEGER PRIMARY KEY, courriel TEXT, piece ANY);
        INSERT INTO adherents (courriel, piece) VALUES
          ('ada@example.org', 'une chaîne'),
          ('bob@example.org', x'0102039fff'),
          ('cyd@example.org', 1234567890),
          ('dee@example.org', NULL);
        """;
    cmd.ExecuteNonQuery();
}

// --- Voie 1 : P/Invoke direct sur la bibliothèque native embarquée -----------

[DllImport("e_sqlite3", CallingConvention = CallingConvention.Cdecl)]
static extern void sqlite3_interrupt(IntPtr db);

static void Sonde1_InterruptParPInvoke(string fichier, string requete)
{
    Console.WriteLine("=== 1. sqlite3_interrupt via SqliteConnection.Handle (P/Invoke direct)");
    using var c = new SqliteConnection($"Data Source={fichier}");
    c.Open();

    var handle = c.Handle;
    Console.WriteLine($"    Handle : {(handle is null ? "null" : handle.GetType().FullName)}");
    if (handle is null) { Console.WriteLine("    ABANDON : pas de handle exposé."); return; }

    var brut = handle.DangerousGetHandle();
    Console.WriteLine($"    Pointeur natif : 0x{brut:X}");

    using var cmd = c.CreateCommand();
    cmd.CommandText = requete;

    var chrono = Stopwatch.StartNew();
    using var declencheur = new Timer(_ => sqlite3_interrupt(brut), null, 400, Timeout.Infinite);
    RapporterIssue(chrono, () => cmd.ExecuteScalar());
}

// --- Voie 2 : la même chose, par l'API managée de SQLitePCLRaw --------------

static void Sonde2_InterruptParSqlitePclRaw(string fichier, string requete)
{
    Console.WriteLine("=== 2. SQLitePCL.raw.sqlite3_interrupt(connexion.Handle)");
    using var c = new SqliteConnection($"Data Source={fichier}");
    c.Open();
    var handle = c.Handle!;

    using var cmd = c.CreateCommand();
    cmd.CommandText = requete;

    var chrono = Stopwatch.StartNew();
    using var declencheur = new Timer(_ => SQLitePCL.raw.sqlite3_interrupt(handle), null, 400, Timeout.Infinite);
    RapporterIssue(chrono, () => cmd.ExecuteScalar());
}

// --- Voie 3 : ce que la doc dit ne rien faire -------------------------------

static void Sonde3_CancelDeSqliteCommand(string fichier, string requete)
{
    Console.WriteLine("=== 3. SqliteCommand.Cancel() — documenté « Does nothing. »");
    using var c = new SqliteConnection($"Data Source={fichier}");
    c.Open();
    using var cmd = c.CreateCommand();
    cmd.CommandText = requete;

    var chrono = Stopwatch.StartNew();
    using var declencheur = new Timer(_ => cmd.Cancel(), null, 400, Timeout.Infinite);
    RapporterIssue(chrono, () => cmd.ExecuteScalar(), plafondSecondes: 6,
                   nettoyage: () => SQLitePCL.raw.sqlite3_interrupt(c.Handle!));
}

// --- Voie 4 : le CancellationToken de la façade *Async ----------------------

static void Sonde4_CancellationTokenSurExecuteReaderAsync(string fichier, string requete)
{
    Console.WriteLine("=== 4. ExecuteReaderAsync(CancellationToken) — la façade *Async est synchrone");
    using var c = new SqliteConnection($"Data Source={fichier}");
    c.Open();
    using var cmd = c.CreateCommand();
    cmd.CommandText = requete;

    using var jeton = new CancellationTokenSource(400);
    var chrono = Stopwatch.StartNew();
    RapporterIssue(chrono, () => cmd.ExecuteReaderAsync(jeton.Token).GetAwaiter().GetResult(),
                   plafondSecondes: 6,
                   nettoyage: () => SQLitePCL.raw.sqlite3_interrupt(c.Handle!));
}

// --- Voie 5 : Mode=ReadOnly refuse-t-il vraiment l'écriture ? ---------------

static void Sonde5_ModeReadOnly(string fichier)
{
    Console.WriteLine("=== 5. Mode=ReadOnly");
    using var c = new SqliteConnection($"Data Source={fichier};Mode=ReadOnly");
    try
    {
        c.Open();
        using var lecture = c.CreateCommand();
        lecture.CommandText = "SELECT count(*) FROM adherents";
        Console.WriteLine($"    Lecture : OK ({lecture.ExecuteScalar()} lignes)");

        using var ecriture = c.CreateCommand();
        ecriture.CommandText = "INSERT INTO adherents (courriel) VALUES ('eve@example.org')";
        ecriture.ExecuteNonQuery();
        Console.WriteLine("    Écriture : ACCEPTÉE — le mode ne protège rien");
    }
    catch (SqliteException e)
    {
        Console.WriteLine($"    Écriture : REFUSÉE — SqliteErrorCode={e.SqliteErrorCode}, "
                        + $"ExtendedErrorCode={e.SqliteExtendedErrorCode} : {e.Message}");
    }
    Console.WriteLine();
}

// --- Voie 6 : sur SQLite, l'exclusion se décide à la valeur -----------------

static void Sonde6_TypeofALaValeur(string fichier)
{
    Console.WriteLine("=== 6. typeof() à la valeur, et substr() sur autre chose que du texte");
    using var c = new SqliteConnection($"Data Source={fichier}");
    c.Open();
    using var cmd = c.CreateCommand();
    cmd.CommandText = """
        SELECT courriel,
               typeof(piece)                                   AS type_valeur,
               CASE WHEN typeof(piece) = 'blob' THEN NULL
                    ELSE substr(CAST(piece AS TEXT), 1, 6) END AS tronque,
               substr(piece, 1, 6)                             AS substr_brut
        FROM adherents ORDER BY id;
        """;
    using var lecteur = cmd.ExecuteReader();
    Console.WriteLine("    courriel          | typeof  | tronqué  | substr brut");
    while (lecteur.Read())
    {
        var brut = lecteur.IsDBNull(3) ? "NULL" : lecteur.GetValue(3) switch
        {
            byte[] o => "0x" + Convert.ToHexString(o),
            var v => v.ToString() ?? "",
        };
        Console.WriteLine($"    {lecteur.GetString(0),-17} | {lecteur.GetString(1),-7} | "
                        + $"{(lecteur.IsDBNull(2) ? "NULL" : lecteur.GetString(2)),-8} | {brut}");
    }
    Console.WriteLine();
}

// --- Voie 7 : le relevé pivot de releves/sqlite.sql tourne-t-il tel quel ? --

static void Sonde7_RelevePivot(string fichier)
{
    Console.WriteLine("=== 7. releves/sqlite.sql, joué tel quel par Microsoft.Data.Sqlite");
    var chemin = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "releves", "sqlite.sql");
    chemin = Path.GetFullPath(chemin);
    if (!File.Exists(chemin)) { Console.WriteLine($"    Introuvable : {chemin}"); return; }

    using var c = new SqliteConnection($"Data Source={fichier};Mode=ReadOnly");
    c.Open();
    using var cmd = c.CreateCommand();
    cmd.CommandText = File.ReadAllText(chemin);
    try
    {
        using var lecteur = cmd.ExecuteReader();
        while (lecteur.Read()) Console.WriteLine($"    {lecteur.GetString(0)}");
    }
    catch (SqliteException e)
    {
        Console.WriteLine($"    ÉCHEC — SqliteErrorCode={e.SqliteErrorCode} : {e.Message}");
    }
    Console.WriteLine();
}

// --- Rapport commun ---------------------------------------------------------

static void RapporterIssue(Stopwatch chrono, Func<object?> action,
                           int plafondSecondes = 30, Action? nettoyage = null)
{
    try
    {
        var tache = Task.Run(action);
        if (!tache.Wait(TimeSpan.FromSeconds(plafondSecondes)))
        {
            Console.WriteLine($"    PLAFOND ATTEINT ({plafondSecondes} s) — la requête n'a PAS été interrompue.");
            // On la coupe nous-mêmes, par la voie qui marche, pour libérer la connexion.
            nettoyage?.Invoke();
            tache.Wait(TimeSpan.FromSeconds(10));
            Console.WriteLine();
            return;
        }
        Console.WriteLine($"    Terminée normalement en {chrono.ElapsedMilliseconds} ms — "
                        + $"résultat {tache.Result} — PAS d'interruption.");
    }
    catch (AggregateException paquet) when (paquet.InnerException is SqliteException e)
    {
        Console.WriteLine($"    INTERROMPUE après {chrono.ElapsedMilliseconds} ms");
        Console.WriteLine($"    Type      : {e.GetType().FullName}");
        Console.WriteLine($"    ErrorCode : SqliteErrorCode={e.SqliteErrorCode}, "
                        + $"SqliteExtendedErrorCode={e.SqliteExtendedErrorCode}");
        Console.WriteLine($"    Message   : {e.Message}");
    }
    catch (AggregateException paquet)
    {
        var e = paquet.InnerException!;
        Console.WriteLine($"    INTERROMPUE après {chrono.ElapsedMilliseconds} ms");
        Console.WriteLine($"    Type    : {e.GetType().FullName}");
        Console.WriteLine($"    Message : {e.Message}");
    }
    Console.WriteLine();
}
