using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings;

/// <summary>
/// Le moteur de détection retenu par le banc : <b>règles + lexique FR+EN</b>, porté en C# et servi
/// depuis <c>Infrastructure</c>, sans second sidecar (ADR-0004).
/// </summary>
/// <remarks>
/// <para>
/// <b>Il ne fait que ce que le montage gelé faisait.</b> Les cinq règles ci-dessous, leur ordre,
/// leurs degrés et la prose de leurs motifs sont ceux du banc (#134) ; les lexiques sont ceux du gel
/// <c>d413d55</c>, que <see cref="ScreeningLexicon"/> charge sans les rééditer. Le banc est clos :
/// ce moteur n'est pas une occasion de le rejouer, et le seul contrôle qu'on lui oppose est que le
/// portage rende, sur le pivot témoin, ce que le montage gelé rendait.
/// </para>
/// <para>
/// ⚠️ <b>Un lexique ne sait pas avouer, et c'est une propriété mesurée, pas un défaut à corriger.</b>
/// Au banc, le montage ne se replie que sur 0,65 % des lignes signalées, contre 17,3 % chez
/// l'annotateur humain : le rapport sera <b>pauvre en
/// <see cref="PersonalDataCategory.FreeTextAboutPerson"/></b>, où ce repli est désormais rangé.
/// Ajouter une heuristique hors gel pour « corriger » ce chiffre romprait le gel.
/// </para>
/// <para>
/// ⚠️ <b>Les degrés survivent comme ordre, pas comme promesse</b> : au banc, <c>exacte</c> se trompe
/// à 70 % et <c>morphologique</c> à 94 %. Le moteur ne promet donc rien de plus que la règle qui a
/// déclenché — et le motif, qui est du texte qui meurt, dit laquelle.
/// </para>
/// <para>
/// <b>Il est déterministe et local</b> : aucun réseau, aucun état, aucun aléa, aucune horloge. Deux
/// détections du même relevé rendent la même chose, mot pour mot, et la part moteur du budget de
/// rythme (#156) est celle que le banc a mesurée — p95 ≤ 0,103 ms/colonne.
/// </para>
/// </remarks>
public sealed class RulesAndLexiconScreeningEngine : IScreeningEngine
{
  /// <summary>
  /// Le nom du montage tel que le banc l'a publié, et sous lequel ce moteur se déclare. C'est ce nom
  /// que l'humain retrouvera dans <c>exploration/banc-screening/VERDICT.md</c> s'il veut savoir ce
  /// que valait le moteur qui a produit son rapport.
  /// </summary>
  public const string EngineName = "regles-lexique-fr-en";

  /// <summary>La génération des règles de <b>nom</b>, celles que le banc a mesurées.</summary>
  public const string RulesVersion = "regles-2";

  /// <summary>La génération des règles de <b>forme</b>, celles qui lisent les valeurs.</summary>
  public const string FormsVersion = "formes-1";

  /// <summary>
  /// Ce que la version dit quand <b>aucun aperçu</b> n'est arrivé : les règles de forme existent, et
  /// elles n'ont rien lu.
  /// <para>
  /// ⚠️ <b>C'est « inactives », et non l'absence de la pièce.</b> Une pièce absente se lirait comme
  /// un moteur d'avant les formes, et deux rapports qui ne portent pas les mêmes règles se
  /// compareraient comme s'ils les portaient. Le chemin collé déclare donc qu'il a des formes, et
  /// qu'elles n'ont pas parlé.
  /// </para>
  /// </summary>
  public const string InactiveForms = "formes-inactives";

  /// <summary>Le gel dont les lexiques sortent.</summary>
  public const string LexiconsVersion = "lexiques-d413d55";

  /// <summary>Le motif de la seule règle qui ne lit pas un nom, transférée sur ce moteur par #132.</summary>
  private const string FreeContainerReason = "conteneur libre : le contenu n'est pas lisible depuis le schéma";

  /// <summary>
  /// Ce qu'un motif dit quand il a fallu l'abréger, <b>collé tel quel à sa fin</b> : les points de
  /// suspension, l'espace et les parenthèses comptent dans la place à réserver.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est le seul endroit où ce moteur s'écarte du montage gelé</b>, et l'écart est déclaré :
  /// le banc n'avait aucune borne sur la longueur d'un motif, le domaine en a une
  /// (<see cref="ScreenedColumn.MaxReasonLength"/>). Sans cette coupe, un commentaire de colonne
  /// portant quarante mots du lexique ferait échouer le relevé <b>entier</b> — une détection qui
  /// s'effondre sur une colonne est un rapport de détection qui n'existe pas, pour un motif trop long
  /// à lire.
  /// Aucune colonne du corpus n'en approche, et la coupe <b>se dit</b> plutôt que de se faire en
  /// silence : un motif tronqué muet se lirait comme entier.
  /// </remarks>
  private const string AbbreviationNote = "… (motif abrégé : d'autres règles ont déclenché sur cette colonne)";

  private readonly ScreeningLexicon _lexicon;

  /// <summary>
  /// Monte le moteur sur l'union mécanique des deux dictionnaires gelés, <b>français puis anglais</b>
  /// — l'ordre du banc, dont dépend le rapprochement morphologique.
  /// </summary>
  public RulesAndLexiconScreeningEngine()
    : this(ScreeningLexicon.Load(
      "MicroserviceRgpd.Infrastructure.Screenings.Lexicons.dictionnaire-fr.tsv",
      "MicroserviceRgpd.Infrastructure.Screenings.Lexicons.dictionnaire-en.tsv"))
  {
  }

  private RulesAndLexiconScreeningEngine(ScreeningLexicon lexicon)
  {
    _lexicon = lexicon;
  }

  /// <summary>
  /// Le nom et la version que ce moteur joint à ce qu'il rend, <b>selon qu'un aperçu lui soit
  /// parvenu ou non</b>. C'est la <b>même</b> identité à une pièce près : le moteur ne se renomme pas
  /// quand il lit des valeurs, il dit seulement que ses formes ont tourné.
  /// </summary>
  /// <param name="formsRan">Un aperçu, au moins, est-il arrivé jusqu'aux règles de forme ?</param>
  public static ScreeningEngineIdentity IdentityWhen(bool formsRan)
  {
    return new ScreeningEngineIdentity(
      EngineName,
      $"{RulesVersion}+{(formsRan ? FormsVersion : InactiveForms)}+{LexiconsVersion}");
  }

  /// <inheritdoc />
  public Task<ScreenedListing> ScreenAsync(
    ColumnListing listing,
    IReadOnlyDictionary<ColumnIdentity, ColumnPreview> previews,
    IProgress<int>? columnsScreened = null,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(listing);
    ArgumentNullException.ThrowIfNull(previews);

    var screened = new List<ScreenedColumn>(listing.ColumnCount);

    foreach (var column in listing.Columns)
    {
      cancellationToken.ThrowIfCancellationRequested();

      screened.Add(Screen(column, previews.GetValueOrDefault(column.Identity)));
    }

    // Local et sans lot : le compte se rapporte une fois, quand tout est détecté.
    columnsScreened?.Report(screened.Count);

    // ⚠️ La déclaration porte sur ce que l'APPELANT a fourni, jamais sur ce que les règles ont
    // trouvé : un relevé scanné dont aucune colonne ne porte de forme reconnue a bel et bien fait
    // tourner ses formes, et le dire autrement rendrait les deux chemins indistinguables.
    return Task.FromResult(new ScreenedListing(IdentityWhen(previews.Count > 0), screened));
  }

  /// <summary>
  /// Ce que les règles disent d'<b>une</b> colonne : rien vu, ou une catégorie, un degré et un motif.
  /// </summary>
  private ScreenedColumn Screen(ListedColumn column, ColumnPreview? preview)
  {
    var triggered = Triggers(column, preview);
    var absence = preview?.Absence;

    if (triggered.Count == 0)
    {
      return ScreenedColumn.NothingSeen(column, absence);
    }

    // L'ordre est celui du lexique, et de lui seul : la taxonomie n'en porte plus, et aucun autre
    // moteur ne l'hérite.
    var category = LexiconTaxonomy.MostCostlyToOmit(triggered.Select(trigger => trigger.Category));

    var retained = triggered.Where(trigger => trigger.Category == category).ToList();

    // ⚠️ Les degrés se comparent ici pour dire *laquelle des règles retenues* a parlé le plus
    // directement — jamais pour désigner la catégorie, qui est déjà tranchée par l'ordre ci-dessus.
    // Un degré qui départagerait deux catégories serait un score produisant une issue.
    var strength = retained.MinBy(trigger => trigger.Strength.Value)!.Strength;

    return ScreenedColumn.Flagged(
      column,
      category,
      strength,
      Reason(retained, triggered, category),
      absence);
  }

  /// <summary>
  /// Les règles qui déclenchent sur une colonne, <b>dans l'ordre où elles s'énoncent</b>, avec le
  /// degré que chacune porte et le motif qu'elle écrit.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Les cinq règles du montage gelé :
  /// <list type="number">
  /// <item>jeton du nom de colonne égal à une entrée — <see cref="RuleStrength.ExactName"/> ;</item>
  /// <item>jeton du nom de colonne rapproché par préfixe — <see cref="RuleStrength.Morphological"/> ;</item>
  /// <item>mot du commentaire de colonne égal à une entrée — <see cref="RuleStrength.Morphological"/> ;</item>
  /// <item>
  /// héritage du domaine par la table — nom ou commentaire de table — <b>seulement si rien n'a
  /// déclenché au niveau de la colonne</b>, et jamais sur les clés purement techniques
  /// <c>id</c>/<c>rowid</c>, qu'une table nommée <c>patients</c> rendrait autrement toutes
  /// médicales ;
  /// </item>
  /// <item>type <c>json</c>/<c>jsonb</c> — <see cref="RuleStrength.TypeHeuristic"/>.</item>
  /// </list>
  /// </para>
  /// <para>
  /// ⚠️ <b>Le type, la nullabilité et la table référencée restent des filtres, jamais des signaux</b>
  /// (#128). La règle du conteneur libre est la seule exception, transférée nommément comme règle du
  /// moteur par #132 : elle ne dit pas ce que la colonne <i>est</i>, elle dit que le schéma ne
  /// permet pas de le lire.
  /// </para>
  /// </remarks>
  private List<Trigger> Triggers(ListedColumn column, ColumnPreview? preview)
  {
    var triggers = new List<Trigger>();
    var name = column.Identity.Column;
    var tokens = ScreeningIdentifiers.Tokenise(name);

    // Les deux règles du nom sont énoncées l'une après l'autre plutôt que fondues en un seul
    // parcours : c'est la forme du montage gelé, et elle se relit ligne à ligne contre lui. Un jeton
    // qui a déjà parlé exactement ne se rapproche de rien — il n'a rien à rapprocher.
    var exact = new HashSet<string>(StringComparer.Ordinal);

    foreach (var token in tokens)
    {
      if (_lexicon.TryLookUp(token, out var category))
      {
        exact.Add(token);

        triggers.Add(new Trigger(
          category,
          RuleStrength.ExactName,
          $"jeton « {token} » du nom de colonne, entrée du lexique"));
      }
    }

    foreach (var token in tokens)
    {
      if (exact.Contains(token))
      {
        continue;
      }

      if (_lexicon.MorphologicalMatch(token) is { } approached)
      {
        triggers.Add(new Trigger(
          approached.Category,
          RuleStrength.Morphological,
          $"jeton « {token} » rapproché de l'entrée « {approached.Term} »"));
      }
    }

    foreach (var word in ScreeningIdentifiers.Words(column.ColumnComment))
    {
      if (_lexicon.TryLookUp(word, out var category))
      {
        triggers.Add(new Trigger(
          category,
          RuleStrength.Morphological,
          $"mot « {word} » du commentaire de colonne"));
      }
    }

    if (triggers.Count == 0 && !IsTechnicalKey(name))
    {
      var table = column.Identity.Table;

      foreach (var token in ScreeningIdentifiers.Tokenise(table))
      {
        if (_lexicon.TryLookUp(token, out var category))
        {
          triggers.Add(new Trigger(
            category,
            RuleStrength.Morphological,
            $"héritage : jeton « {token} » du nom de la table « {table} »"));
        }
      }

      foreach (var word in ScreeningIdentifiers.Words(column.TableComment))
      {
        if (_lexicon.TryLookUp(word, out var category))
        {
          triggers.Add(new Trigger(
            category,
            RuleStrength.Morphological,
            $"héritage : mot « {word} » du commentaire de table"));
        }
      }
    }

    if (IsFreeContainer(column.DataType))
    {
      triggers.Add(new Trigger(
        PersonalDataCategory.FreeTextAboutPerson,
        RuleStrength.TypeHeuristic,
        FreeContainerReason));
    }

    // ⚠️ Les formes sont VERSÉES DANS LE MÊME SAC, et c'est tout le dessin. Aucun étage n'arbitre
    // « ce que dit le nom » contre « ce que disent les valeurs » : une règle de forme est un
    // Trigger de plus, et l'ordre du lexique tranche comme il l'a toujours fait. C'est
    // aussi pourquoi une forme ne peut qu'AJOUTER un signalement — la liste ne sait qu'accueillir.
    triggers.AddRange(FormTriggers(preview));

    return triggers;
  }

  /// <summary>
  /// Ce que les <b>valeurs</b> disent, quand il y en a. Une colonne sans aperçu — le chemin collé
  /// entier — n'en rend aucun, et le relevé se détecte exactement comme avant.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Trois valeurs ne comptent nulle part</b> — ni au numérateur, ni au dénominateur : le
  /// <c>NULL</c>, qui n'est pas une valeur ; la valeur <b>tronquée</b> par le SGBD, dont on ne sait
  /// pas ce que la coupe a emporté ; et le <b>doublon</b> d'une valeur déjà comptée, qui n'apporte
  /// aucune observation nouvelle. Le prix est assumé et écrit ici : <b>un IBAN distinct unique ne
  /// signale pas par la forme</b>, parce que son seuil est de deux.
  /// </para>
  /// <para>
  /// ⚠️ <b>Une forme qui n'atteint pas son seuil n'écrit rien</b> — pas même une phrase disant
  /// qu'elle a failli. Le motif ne parle jamais de ce qui n'a pas déclenché : ce serait une
  /// quantité de preuve rendue par la prose, et l'<c>Operator</c> lirait « certaines valeurs
  /// ressemblent à… » comme un signalement de second rang.
  /// </para>
  /// </remarks>
  private static IEnumerable<Trigger> FormTriggers(ColumnPreview? preview)
  {
    if (preview is null || !preview.CarriesValues)
    {
      yield break;
    }

    var counted = CountedValues(preview);

    if (counted.Count == 0)
    {
      yield break;
    }

    foreach (var rule in ValueFormRules.All)
    {
      var recognised = counted.Count(rule.Recognises);

      if (!rule.Strength.Threshold!.IsReachedBy(recognised, counted.Count))
      {
        continue;
      }

      // Le quantificateur est un MOT, jamais un chiffre : « cinq sur cinq » se comparerait d'une
      // ligne à l'autre alors que les deux nombres ne veulent pas dire la même chose, et une
      // valeur recopiée survivrait à l'aperçu qui l'a montrée.
      var quantifier = recognised == counted.Count
        ? "toutes les valeurs lues"
        : "certaines des valeurs lues";

      yield return new Trigger(
        rule.Category,
        rule.Strength,
        $"{quantifier} {rule.FormPhrase}",
        FromValues: true);
    }
  }

  /// <summary>
  /// Les valeurs qui <b>comptent</b> : ni <c>NULL</c>, ni tronquée, ni doublon d'une valeur déjà
  /// comptée. L'ordre de lecture est conservé — le premier exemplaire d'un doublon compte, les
  /// suivants non.
  /// </summary>
  private static IReadOnlyList<string> CountedValues(ColumnPreview preview)
  {
    var counted = new List<string>(ColumnPreview.MaxValues);
    var seen = new HashSet<string>(StringComparer.Ordinal);

    foreach (var value in preview.Values)
    {
      if (value.IsNull || value.IsTruncated || value.Text is not { } text)
      {
        continue;
      }

      if (seen.Add(text))
      {
        counted.Add(text);
      }
    }

    return counted;
  }

  /// <summary>
  /// La prose que l'<c>Operator</c> lira : ce qui a déclenché pour la valeur retenue, et — entre
  /// parenthèses — ce que l'ordre du lexique a écarté.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Le motif dit ce qui a été écarté</b>, parce que « la règle <i>vie professionnelle</i> a aussi
  /// déclenché » est précisément ce qui s'arbitre : sans lui, l'<c>Operator</c> ne verrait pas
  /// qu'une seconde lecture de la colonne existait.
  /// </para>
  /// <para>
  /// ⚠️ <b>La clause des écartées nomme les valeurs par leur nom canonique anglais</b> — « a aussi
  /// déclenché : ProfessionalLife » — et c'est le seul anglais d'une prose que le glossaire veut
  /// française. C'est <b>délibérément</b> ce que le montage gelé écrivait, et le critère de #166 est
  /// que le portage rende ce qu'il rendait ; <see cref="PersonalDataCategory.FrenchLabel"/> est prêt
  /// le jour où l'on décidera que l'écran d'arbitrage vaut plus que la fidélité littérale. La
  /// décision n'appartient pas à ce ticket, et l'écart est écrit ici plutôt que découvert.
  /// </para>
  /// </remarks>
  private static string Reason(
    List<Trigger> retained,
    List<Trigger> triggered,
    PersonalDataCategory category)
  {
    // ⚠️ Le NOM D'ABORD, LA FORME ENSUITE, et jamais l'inverse. C'est l'ordre dans lequel un
    // humain vérifie : il regarde le nom de la colonne, puis il regarde les valeurs. Un motif qui
    // ouvrirait sur les valeurs se lirait comme un moteur qui devine à partir du contenu — ce que
    // le glossaire refuse depuis la première ligne. À l'intérieur de chaque groupe, l'ordre est
    // celui du montage gelé : alphabétique, stable, sans quoi deux détections du même relevé
    // rendraient deux proses.
    var reason = string.Join(
      "; ",
      retained
        .OrderBy(trigger => trigger.FromValues)
        .ThenBy(trigger => trigger.Reason, StringComparer.Ordinal)
        .Select(trigger => trigger.Reason)
        .Distinct(StringComparer.Ordinal));

    var setAside = triggered
      .Select(trigger => trigger.Category)
      .Where(other => other != category)
      .Distinct()
      .OrderBy(LexiconTaxonomy.Rank)
      .Select(other => other.Name)
      .ToList();

    if (setAside.Count > 0)
    {
      reason += $" (a aussi déclenché : {string.Join(", ", setAside)})";
    }

    return reason.Length <= ScreenedColumn.MaxReasonLength ? reason : Abbreviate(reason);
  }

  /// <summary>
  /// Coupe un motif démesuré <b>en le disant</b>. Un motif tronqué en silence se lirait comme
  /// entier, ce qui est l'<c>Omission silencieuse</c> déplacée dans la prose.
  /// </summary>
  private static string Abbreviate(string reason)
  {
    var room = ScreenedColumn.MaxReasonLength - AbbreviationNote.Length;

    return reason[..room] + AbbreviationNote;
  }

  /// <summary>
  /// Une clé purement technique, que l'héritage par la table ne touche jamais. Le nom est lu tel
  /// quel, à la casse près : c'est ce que le banc lisait.
  /// </summary>
  private static bool IsTechnicalKey(string name)
  {
    return name.Equals("id", StringComparison.OrdinalIgnoreCase)
      || name.Equals("rowid", StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  /// Un conteneur libre : <c>json</c>, <c>jsonb</c>, et tout type qui les porte dans son nom. Le type
  /// absent n'en est pas un — un SGBD qui ne rend pas le type ne rend pas non plus un aveu.
  /// </summary>
  private static bool IsFreeContainer(string? dataType)
  {
    return dataType is not null && dataType.Contains("json", StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>Une règle qui a déclenché : ce qu'elle a reconnu, le degré qu'elle porte, et sa prose.</summary>
  /// <param name="Category">Ce que la règle désigne.</param>
  /// <param name="Strength">Le degré, dérivé de la règle — jamais d'une auto-évaluation.</param>
  /// <param name="Reason">Ce que la règle écrit dans le motif.</param>
  /// <param name="FromValues">
  /// La règle a-t-elle lu des <b>valeurs</b> ? Cela ne sert qu'à <b>ordonner la prose</b> — le nom
  /// d'abord, la forme ensuite. ⚠️ Cela n'arbitre rien : un déclenchement de forme pèse exactement
  /// ce que pèse un déclenchement de nom, et c'est l'ordre des catégories qui tranche.
  /// </param>
  private sealed record Trigger(
    PersonalDataCategory Category,
    RuleStrength Strength,
    string Reason,
    bool FromValues = false);
}
