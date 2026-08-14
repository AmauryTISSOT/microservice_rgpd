using System.Text;

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
  /// Les identifiants C# sont cherchés <b>à la casse</b> : ils sont anglais, et une occurrence
  /// minuscule est un mot de la base ou d'un tiers, pas un identifiant du dépôt.
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
    new("Ledger", "EvidenceLog", CaseSensitive: true),
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
  /// <b>L'exception assumée, écrite plutôt que découverte un jour de panne.</b> Ces quatre noms de
  /// classe de migration portent <c>Ledger</c> et sont <b>inéditables</b> : ils sont écrits dans
  /// <c>__EFMigrationsHistory</c> de chaque déploiement, et les renommer ferait rejouer des
  /// migrations déjà appliquées.
  /// <para>
  /// Le motif retenu : une migration porte <b>une date dans son nom</b>, donc un lecteur comprend
  /// sans effort qu'elle parle avec les mots de sa date.
  /// </para>
  /// <para>
  /// ⚠️ L'exemption porte sur <b>ces quatre chaînes</b>, et non sur les fichiers qui les portent :
  /// elles sont retirées du texte avant le balayage, et tout le reste de ces fichiers reste gardé.
  /// Elle est écrite en dur, comme celle de <see cref="ContextIsolationTests"/>, pour que
  /// l'élargir demande un geste délibéré.
  /// </para>
  /// </summary>
  private static readonly string[] ExemptMigrationNames =
  [
    "CreateCasesAndLedger",
    "AddDeclaredSystemToLedger",
    "AddDeliveryGesturesAndLedgerCounts",
    "AddExtensionDeclarationAndLedgerIndex",
  ];

  /// <summary>
  /// Les dossiers qu'on ne balaie pas : ni l'histoire de Git, ni ce que le compilateur a écrit, ni
  /// ce qu'un outil a déposé. Aucun d'eux n'est du texte que quelqu'un relit.
  /// </summary>
  private static readonly string[] SkippedDirectories =
    [".git", "bin", "obj", "node_modules", "TestResults", ".vs", ".idea", ".claude", "artifacts"];

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

      var content = Exempted(ReadText(file));

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
  /// Retire du texte les quatre noms de classe de migration exemptés, pour que ce qui reste soit
  /// gardé — y compris dans les fichiers qui les portent.
  /// </summary>
  private static string Exempted(string content)
  {
    var builder = new StringBuilder(content);

    foreach (var name in ExemptMigrationNames)
    {
      builder.Replace(name, string.Empty);
    }

    return builder.ToString();
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
  private static string ReadText(string file)
  {
    return Normalized(File.ReadAllText(file));
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
