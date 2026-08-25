using MicroserviceRgpd.Core.Screenings;

// Le service accepte-t-il les relevés que ses propres requêtes viennent de produire ?
// On ne lit pas le parseur : on lui donne à manger les fichiers sortis des conteneurs.

var racine = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

string[][] cas =
[
    ["PostgreSQL 17 — superutilisateur", Path.Combine(racine, "postgresql", "capture-super.txt")],
    ["PostgreSQL 17 — rôle sans_droits", Path.Combine(racine, "postgresql", "capture-sans.txt")],
    ["MySQL 8.4 — compte plein",         Path.Combine(racine, "mysql", "sortie-plein.txt")],
    ["MySQL 8.4 — compte partiel",       Path.Combine(racine, "mysql", "sortie-partiel.txt")],
    ["SQLite 3.49",                      Path.Combine(racine, "sqlite-interrupt", "capture-pivot.txt")],
    // Les mêmes, ligne de fin rapiécée : révèle le défaut SUIVANT.
    ["PostgreSQL — fin rapiécée",        Path.Combine(racine, "ingestion", "captures-rapiecees", "postgresql.txt")],
    ["MySQL — fin rapiécée",             Path.Combine(racine, "ingestion", "captures-rapiecees", "mysql.txt")],
    ["SQLite — fin rapiécée",            Path.Combine(racine, "ingestion", "captures-rapiecees", "sqlite-interrupt.txt")],
    // Fin rapiécée ET `nullable` en booléen : reste-t-il un troisième défaut ?
    ["PostgreSQL — fin + booléen",       Path.Combine(racine, "ingestion", "captures-rapiecees", "postgresql-booleen.txt")],
    ["MySQL — fin + booléen",            Path.Combine(racine, "ingestion", "captures-rapiecees", "mysql-booleen.txt")],
    ["SQLite — fin + booléen",           Path.Combine(racine, "ingestion", "captures-rapiecees", "sqlite-interrupt-booleen.txt")],
    // Le relevé AMPUTÉ par les droits, une fois les deux défauts corrigés :
    // l'ingestion rattrape-t-elle l'omission, ou l'accepte-t-elle en silence ?
    ["MySQL AMPUTÉ — fin + booléen",     Path.Combine(racine, "ingestion", "captures-rapiecees", "mysql-partiel-booleen.txt")],
];

foreach (var cas_ in cas)
{
    var (titre, chemin) = (cas_[0], cas_[1]);
    Console.WriteLine($"=== {titre}");
    if (!File.Exists(chemin)) { Console.WriteLine($"    (capture absente : {chemin})\n"); continue; }

    // On ne garde que les lignes du pivot : les captures portent aussi les
    // avertissements du client en ligne de commande.
    var pivot = string.Join('\n',
        File.ReadAllLines(chemin).Where(l => l.TrimStart().StartsWith('{')));

    if (pivot.Length == 0) { Console.WriteLine("    (aucune ligne de pivot)\n"); continue; }

    var issue = ColumnListingIngestion.Ingest(pivot);
    if (issue.IsAccepted)
    {
        Console.WriteLine($"    ACCEPTÉ — {issue.Listing!.Columns.Count} colonnes");
    }
    else
    {
        Console.WriteLine($"    REFUSÉ — {issue.Refusal!.Cause}");
        Console.WriteLine($"             {issue.Refusal}");
    }
    Console.WriteLine();
}
