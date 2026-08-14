namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// <b>Un terme retiré ne revient pas.</b> Le renommage de vocabulaire ne vit qu'en partie dans le
/// code : sur les quatorze termes retirés, <b>dix sont des clauses de doctrine</b> qui n'existent
/// que dans de la prose — documentation, ADR, commentaires XML, messages d'échec de test. Le
/// compilateur ne les voit pas, et rien n'empêcherait le vocabulaire de se défaire ligne à ligne au
/// fil des PR suivantes.
/// <para>
/// Le cas concret qui exige ce garde était déjà présent : <see cref="ContextIsolationTests"/>
/// portait une clause <b>en dur dans un message d'échec</b>, invisible à toute vérification de
/// types.
/// </para>
/// <para>
/// C'est la couture la plus haute qu'on puisse poser : elle porte sur le <b>dépôt entier</b>, en un
/// seul test, plutôt qu'un contrôle par fichier ou par module. Elle ne juge pas qu'un nom soit
/// « plus lisible » — ce n'est pas une propriété vérifiable par une machine ; elle tient seulement
/// que l'ancien mot ne réapparaisse pas.
/// </para>
/// <para>
/// ⚠️ Comme <see cref="ContextRosterTests"/>, <b>un garde qui ne trouve rien à lire ne doit pas
/// afficher vert</b> : l'absence de racine et un balayage anormalement court sont l'un et l'autre
/// un échec bruyant.
/// </para>
/// <para>
/// Une seule exemption de vocabulaire existe — <see cref="ExemptedMigrationClassNames"/>, les
/// quatre noms de migration d'août 2026 — et
/// <see cref="EveryExemptedMigrationStillExistsOnDisk"/> tient qu'elle ne s'élargisse ni ne
/// pourrisse.
/// </para>
/// </summary>
public class RetiredVocabularyTests
{
  /// <summary>L'ancre du dépôt : le fichier que la racine porte et qu'aucun sous-dossier n'a.</summary>
  private const string RootAnchor = "CONTEXT-MAP.md";

  /// <summary>
  /// En dessous de ce compte, le balayage n'a manifestement pas trouvé le dépôt. Le vert d'un garde
  /// qui n'a rien lu et le vert d'un dépôt propre sont exactement le même vert.
  /// </summary>
  private const int LeastPlausibleFileCount = 200;

  /// <summary>
  /// Les quatorze termes retirés, chacun avec son remplaçant. Le remplaçant est là pour le
  /// <b>message d'échec</b> : la réparation ne doit demander aucune recherche.
  /// </summary>
  /// <remarks>
  /// <para>
  /// L'ancien mot du journal est cherché <b>sans égard à la casse</b>, et c'est délibéré : il vivait
  /// aussi en variable locale, en paramètre et dans des commentaires criés en majuscules, qu'une
  /// recherche à la casse aurait laissés passer. Ses seules occurrences minuscules légitimes sont
  /// celles d'un tiers, dans <c>corpus/</c>, et ce dossier n'est pas balayé.
  /// </para>
  /// <para>
  /// Les clauses de doctrine sont cherchées <b>sans égard à la casse</b> : elles vivent dans de la
  /// prose, où la même clause s'écrit en tête de phrase comme au milieu.
  /// </para>
  /// <para>
  /// <c>Texte qui meurt / texte qui reste</c> est <b>une</b> entrée de glossaire à deux moitiés :
  /// elle occupe deux lignes ici parce que ses deux moitiés se citent séparément.
  /// </para>
  /// </remarks>
  private static readonly RetiredTerm[] RetiredTerms =
  [
    new("ledger", "EvidenceLog", CaseSensitive: false),
    new("CoverSheet", "DeliveryLetter", CaseSensitive: true),
    new("SignatureRegime", "SignerVerification", CaseSensitive: true),
    new("WitnessOpinion", "LexiconOpinion", CaseSensitive: true),
    new("Greffier, pas témoin", "Enregistré, jamais vérifié", CaseSensitive: false),
    new("prose de travail", "texte qui meurt", CaseSensitive: false),
    new("prose de preuve", "texte qui reste", CaseSensitive: false),
    new("sonde à secret", "Appel au faux secret", CaseSensitive: false),
    new("Suggéré, jamais déclaré", "Aucune modification vers le Manifest", CaseSensitive: false),
    new("Le nom, jamais la valeur", "Aucune donnée réelle n'entre", CaseSensitive: false),
    new("Clause de revoyure", "Clause de réexamen", CaseSensitive: false),
    new("niveau de l'IL", "Test du code compilé", CaseSensitive: false),
    new("Garde en liste blanche", "Tout interdit sauf exceptions écrites", CaseSensitive: false),
    new("Garde de vacuité", "Garde contre le vert vide", CaseSensitive: false),
    new("Le verdict commande l'emplacement", "Le banc décide où vit le moteur", CaseSensitive: false),
  ];

  /// <summary>
  /// Les dossiers qu'on ne balaie pas : ni l'histoire de Git, ni ce que le compilateur a écrit, ni
  /// ce qu'un outil a déposé. Aucun d'eux n'est du texte que quelqu'un relit.
  /// </summary>
  /// <remarks>
  /// ⚠️ <c>corpus/</c> porte des schémas de <b>logiciels tiers</b>, où <c>ledger_account</c> est le
  /// nom d'une table de comptabilité chez Dolibarr. Ce ne sont pas nos mots, et les renommer serait
  /// mentir sur ce que le client exploite.
  /// </remarks>
  private static readonly string[] SkippedDirectories =
  [
    ".git", "bin", "obj", "node_modules", "TestResults", ".vs", ".idea", ".claude", "artifacts",
    "corpus",
  ];

  /// <summary>Le dossier dont les fichiers décrivent des gestes déjà appliqués.</summary>
  private const string MigrationsDirectory = "Migrations";

  /// <summary>
  /// ⚠️ <b>La liste d'exemption : les quatre noms de classe de migration d'août 2026, et eux
  /// seuls.</b> Ils portent l'ancien mot du journal et sont <b>inéditables</b> — ils sont écrits
  /// dans <c>__EFMigrationsHistory</c> de chaque déploiement, et les renommer ferait rejouer des
  /// migrations déjà appliquées.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Elle est écrite en dur, comme celle de <see cref="ContextIsolationTests"/> : l'élargir doit
  /// demander un geste délibéré, et l'exception doit être <b>écrite plutôt que découverte un jour
  /// de panne</b>.
  /// </para>
  /// <para>
  /// Motif retenu : une migration porte <b>une date dans son nom</b>, donc un lecteur comprend sans
  /// effort qu'elle parle avec les mots de sa date.
  /// </para>
  /// <para>
  /// ⚠️ <b>L'exemption ne dispense pas le dossier du balayage</b> — c'est ce qui la distingue d'un
  /// dossier écarté. Les fichiers de <c>Migrations/</c> sont lus comme les autres ; seules ces
  /// quatre chaînes-ci et les <see cref="HistoricalSqlIdentifiers"/> y sont tolérées. Tout autre
  /// terme retiré qu'on y écrirait — une clause de doctrine en commentaire, par exemple — fait
  /// rougir le garde.
  /// </para>
  /// </remarks>
  private static readonly string[] ExemptedMigrationClassNames =
  [
    "CreateCasesAndLedger",
    "AddDeclaredSystemToLedger",
    "AddDeliveryGesturesAndLedgerCounts",
    "AddExtensionDeclarationAndLedgerIndex",
  ];

  /// <summary>
  /// Les noms d'objets SQL tels qu'ils s'appelaient avant le renommage. Une migration passée décrit
  /// un geste <b>déjà appliqué</b> : la réécrire lui ferait décrire un geste qui n'a jamais eu lieu.
  /// Et la migration de renommage elle-même <b>doit</b> nommer l'ancienne table — c'est
  /// précisément ce qu'elle renomme.
  /// </summary>
  /// <remarks>
  /// Tolérés dans <c>Migrations/</c> <b>et nulle part ailleurs</b> : partout ailleurs dans le
  /// dépôt, <c>ledger_entries</c> reste un terme retiré.
  /// </remarks>
  private static readonly string[] HistoricalSqlIdentifiers =
  [
    "ix_ledger_entries_case_id",
    "pk_ledger_entries",
    "ledger_entries",
  ];

  /// <summary>
  /// ⚠️ <b>Les instantanés de modèle qu'EF écrit ne sont pas balayés, et le motif compte.</b> Un
  /// <c>.Designer.cs</c> décrit le modèle <b>tel qu'il était</b> à la date de sa migration —
  /// jusqu'aux noms de types et de propriétés d'alors, <c>LedgerRow</c> et <c>SignatureRegime</c>
  /// compris. Le réécrire avec les mots d'aujourd'hui lui ferait décrire un état que sa migration
  /// n'a jamais produit, et le garde, sans cette exclusion, pousserait chaque contributeur à
  /// retoucher de l'histoire immuable. Ce n'est pas de la prose : personne ne relit un instantané,
  /// et personne ne l'écrit à la main.
  /// </summary>
  private const string GeneratedSnapshotSuffix = ".Designer.cs";

  /// <summary>Les extensions qu'on ne lit pas : elles ne portent pas de prose.</summary>
  private static readonly string[] SkippedExtensions =
  [
    ".png", ".jpg", ".jpeg", ".gif", ".ico", ".pdf", ".zip", ".gz", ".dll", ".exe", ".pdb",
    ".woff", ".woff2", ".ttf", ".eot", ".webp", ".mp4",
  ];

  /// <summary>
  /// ⚠️ <b>Ce fichier-ci est le seul que le garde ne se lit pas à lui-même</b>, et le motif est
  /// écrit ici plutôt que laissé à deviner : il porte forcément les quatorze termes retirés, faute
  /// de quoi il ne saurait pas quoi chercher. Ce n'est pas une exemption de vocabulaire, c'est le
  /// garde qui s'exclut de son propre balayage.
  /// </summary>
  private static readonly string GuardFileName = typeof(RetiredVocabularyTests).Name + ".cs";

  [Fact]
  public void NoRetiredTermSurvivesAnywhereInTheRepository()
  {
    var root = RepositoryRoot();
    var scanned = 0;
    var offences = new List<string>();

    foreach (var file in ScannableFiles(root))
    {
      scanned++;

      var content = ReadText(file);

      foreach (var term in RetiredTerms)
      {
        if (content.Contains(term.Normalized, term.Comparison))
        {
          offences.Add(
            $"  {Path.GetRelativePath(root, file)} : « {term.Term} » a été retiré — écrire " +
            $"« {term.Replacement} ».");
        }
      }
    }

    scanned.ShouldBeGreaterThan(
      LeastPlausibleFileCount,
      $"Le garde n'a lu que {scanned} fichiers depuis {root}. Un garde qui ne trouve rien à lire " +
      "ne doit pas afficher vert : réparez le balayage plutôt que de retirer ce test.");

    offences.ShouldBeEmpty(
      "Du vocabulaire retiré est réapparu dans le dépôt. Chaque ligne nomme le fichier, le terme " +
      "retiré et son remplaçant :" + Environment.NewLine +
      string.Join(Environment.NewLine, offences.Order(StringComparer.Ordinal)) +
      Environment.NewLine +
      "Voir l'entrée correspondante des glossaires de docs/contexts/ ou de CONTEXT-MAP.md.");
  }

  /// <summary>
  /// <b>Une exemption qui ne désigne plus rien est une exemption qui ment.</b> Les quatre noms
  /// d'août 2026 sont tolérés parce qu'ils sont inéditables ; le jour où l'un d'eux disparaît du
  /// disque, la ligne qui l'exempte devient une porte ouverte sur un mot que plus rien ne justifie.
  /// Ce test tient que la liste reste <b>exactement</b> celle du ticket, et que chacun de ses noms
  /// désigne encore une migration réelle.
  /// </summary>
  [Fact]
  public void EveryExemptedMigrationStillExistsOnDisk()
  {
    var migrations = Directory
      .EnumerateFiles(
        Path.Combine(RepositoryRoot(), "src", "MicroserviceRgpd.Infrastructure", MigrationsDirectory),
        "*.cs")
      .Select(Path.GetFileNameWithoutExtension)
      .ToArray();

    ExemptedMigrationClassNames.Length.ShouldBe(
      4,
      "La liste d'exemption doit contenir les quatre noms de migration d'août 2026, et eux seuls. " +
      "L'élargir est un geste délibéré, qui se discute en revue.");

    foreach (var exempted in ExemptedMigrationClassNames)
    {
      migrations.ShouldContain(
        migration => migration!.EndsWith("_" + exempted, StringComparison.Ordinal),
        $"Aucune migration nommée « {exempted} » sur le disque, alors que la liste d'exemption la " +
        "tolère. Retirez la ligne : elle n'exempte plus rien et laisse passer l'ancien mot.");
    }
  }

  /// <summary>
  /// Les fichiers de texte du dépôt. L'énumération descend à la main plutôt que par
  /// <c>EnumerateFiles</c> récursif : il faut pouvoir <b>ne pas descendre</b> dans <c>bin/</c>,
  /// dont le contenu ferait échouer le garde sur des copies de ce qu'il garde déjà.
  /// </summary>
  private static IEnumerable<string> ScannableFiles(string root)
  {
    var pending = new Stack<string>([root]);

    while (pending.Count > 0)
    {
      var directory = pending.Pop();

      foreach (var child in Directory.EnumerateDirectories(directory))
      {
        if (!SkippedDirectories.Contains(new DirectoryInfo(child).Name, StringComparer.Ordinal))
        {
          pending.Push(child);
        }
      }

      foreach (var file in Directory.EnumerateFiles(directory))
      {
        var name = Path.GetFileName(file);

        if (name.Equals(GuardFileName, StringComparison.Ordinal)
          || name.EndsWith(GeneratedSnapshotSuffix, StringComparison.Ordinal)
          || SkippedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
        {
          continue;
        }

        yield return file;
      }
    }
  }

  /// <summary>
  /// Le texte d'un fichier, apostrophes typographiques ramenées à l'apostrophe droite. Sans cette
  /// normalisation, <c>Test au niveau de l'IL</c> passerait au travers du garde le jour où
  /// quelqu'un le recopie depuis un traitement de texte.
  /// </summary>
  /// <remarks>
  /// Dans <c>Migrations/</c>, les chaînes exemptées sont <b>effacées avant la recherche</b> plutôt
  /// que le fichier écarté : le reste du fichier est balayé comme n'importe quel autre.
  /// </remarks>
  private static string ReadText(string file)
  {
    var content = Normalized(File.ReadAllText(file));

    if (!IsUnderMigrations(file))
    {
      return content;
    }

    foreach (var exempted in ExemptedMigrationClassNames.Concat(HistoricalSqlIdentifiers))
    {
      content = content.Replace(exempted, string.Empty, StringComparison.Ordinal);
    }

    return content;
  }

  private static bool IsUnderMigrations(string file)
  {
    var directory = Path.GetFileName(Path.GetDirectoryName(file));

    return string.Equals(directory, MigrationsDirectory, StringComparison.Ordinal);
  }

  private static string Normalized(string text)
  {
    return text.Replace('’', '\'');
  }

  /// <summary>
  /// La racine du dépôt, trouvée en remontant depuis le répertoire de sortie. L'absence est un échec
  /// bruyant, pour le motif de <see cref="ProductionAssembly.PathOf"/> : un garde qui ne trouve rien
  /// à lire ne doit pas afficher vert.
  /// </summary>
  private static string RepositoryRoot()
  {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);

    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, RootAnchor)))
    {
      directory = directory.Parent;
    }

    directory.ShouldNotBeNull(
      $"Aucun {RootAnchor} trouvé en remontant depuis {AppContext.BaseDirectory}. " +
      "L'ancre du garde est introuvable : réparez le chemin plutôt que de retirer ce test.");

    return directory.FullName;
  }

  /// <summary>Un terme retiré, son remplaçant, et le régime de casse sous lequel on le cherche.</summary>
  private sealed record RetiredTerm(string Term, string Replacement, bool CaseSensitive)
  {
    public string Normalized { get; } = Term.Replace('’', '\'');

    public StringComparison Comparison { get; } =
      CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
  }
}
