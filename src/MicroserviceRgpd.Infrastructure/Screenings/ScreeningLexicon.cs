using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings;

/// <summary>
/// Les <b>lexiques gelés du banc</b> (commit <c>d413d55</c>), chargés une fois et lus par le moteur :
/// un terme normalisé, une <see cref="PersonalDataCategory"/>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Les rééditer est interdit.</b> Les trois lexiques du banc ont été rédigés <b>en aveugle</b>,
/// gelés par le commit qui les introduit, et toute édition d'une entrée après ce commit rendrait le
/// banc non concluant — c'est le prédicat de la cause ③ de #130, exécutée par #155. Les fichiers
/// embarqués ici sont la copie exacte du gel, et un test l'ancre par empreinte : ce ne sont pas des
/// données de configuration, ce sont des <b>pièces</b>.
/// </para>
/// <para>
/// <b>Le montage FR+EN est l'union mécanique des deux dictionnaires</b>, sans fichier propre — en
/// avoir un serait une occasion d'éditer. Deux termes portent des valeurs différentes selon la
/// langue : <c>conviction</c> et <c>coord</c>. Ce n'est pas un défaut à corriger — dans l'union, les
/// deux entrées déclenchent, et c'est l'<b>ordre d'arbitrage</b> de la taxonomie, hérité et jamais
/// redécidé, qui tranche.
/// </para>
/// <para>
/// <b>L'ordre d'insertion est porté, et il n'est pas décoratif</b> : le rapprochement morphologique
/// retient la <b>première</b> entrée qui partage un préfixe avec le jeton, exactement comme au banc,
/// où l'ordre est celui du fichier français puis du fichier anglais. Une table de hachage seule
/// laisserait cet ordre à l'implémentation, et le portage cesserait de rendre ce que le montage gelé
/// rendait.
/// </para>
/// </remarks>
internal sealed class ScreeningLexicon
{
  /// <summary>
  /// La longueur minimale d'un terme comme d'un jeton pour qu'un rapprochement morphologique soit
  /// tenté. Trois lettres rapprocheraient à peu près tout de à peu près tout.
  /// </summary>
  private const int MinimumMorphologicalLength = 4;

  private readonly Dictionary<string, PersonalDataCategory> _entries;
  private readonly List<string> _termsInOrder;

  private ScreeningLexicon(Dictionary<string, PersonalDataCategory> entries, List<string> termsInOrder)
  {
    _entries = entries;
    _termsInOrder = termsInOrder;
  }

  /// <summary>Combien de termes distincts l'union porte.</summary>
  internal int Count => _entries.Count;

  /// <summary>
  /// Charge l'union des dictionnaires embarqués, dans l'ordre où ils sont donnés.
  /// </summary>
  /// <remarks>
  /// Une <b>collision</b> entre deux fichiers — même terme, deux valeurs — se règle par l'ordre
  /// d'arbitrage : on garde la valeur la plus coûteuse à omettre, comme pour tout double
  /// déclenchement. La position du terme, elle, reste celle de sa première apparition.
  /// </remarks>
  internal static ScreeningLexicon Load(params string[] resourceNames)
  {
    var entries = new Dictionary<string, PersonalDataCategory>(StringComparer.Ordinal);
    var termsInOrder = new List<string>();

    foreach (var (term, category) in resourceNames.SelectMany(Read))
    {
      if (entries.TryGetValue(term, out var known))
      {
        entries[term] = PersonalDataCategory.MostCostlyToOmit([known, category]);

        continue;
      }

      entries[term] = category;
      termsInOrder.Add(term);
    }

    return new ScreeningLexicon(entries, termsInOrder);
  }

  /// <summary>La valeur que ce terme porte, s'il est au lexique.</summary>
  internal bool TryLookUp(string term, out PersonalDataCategory category)
  {
    return _entries.TryGetValue(term, out category!);
  }

  /// <summary>
  /// L'entrée que ce jeton <b>rapproche</b> : la première dont le préfixe partagé couvre entièrement
  /// le plus court des deux, longueur minimale quatre — <c>ancien</c> et <c>anciennete</c>,
  /// <c>passee</c> et <c>passe</c>.
  /// </summary>
  /// <returns>Le terme rapproché et sa valeur, ou <c>null</c> si aucun ne l'est.</returns>
  internal (string Term, PersonalDataCategory Category)? MorphologicalMatch(string token)
  {
    if (token.Length < MinimumMorphologicalLength)
    {
      return null;
    }

    foreach (var term in _termsInOrder)
    {
      if (term.Length < MinimumMorphologicalLength)
      {
        continue;
      }

      if (token.StartsWith(term, StringComparison.Ordinal) || term.StartsWith(token, StringComparison.Ordinal))
      {
        return (term, _entries[term]);
      }
    }

    return null;
  }

  /// <summary>
  /// Les paires d'un dictionnaire embarqué, en-tête sautée, termes normalisés comme le sont les
  /// jetons qu'on leur comparera.
  /// </summary>
  private static IEnumerable<(string Term, PersonalDataCategory Category)> Read(string resourceName)
  {
    using var stream = typeof(ScreeningLexicon).GetTypeInfo().Assembly.GetManifestResourceStream(resourceName)
      ?? throw new InvalidOperationException(
        $"Le lexique gelé « {resourceName} » n'est pas embarqué dans l'assemblage. Le moteur de "
        + "dépistage n'a pas de repli : un dictionnaire absent est une panne de déploiement, pas un "
        + "moteur qui ne signale rien.");

    using var reader = new StreamReader(stream, Encoding.UTF8);

    var pairs = new List<(string, PersonalDataCategory)>();
    var header = true;

    while (reader.ReadLine() is { } line)
    {
      if (header)
      {
        header = false;

        continue;
      }

      if (line.Length == 0)
      {
        continue;
      }

      var separator = line.IndexOf('\t', StringComparison.Ordinal);

      if (separator < 0)
      {
        // Le gel rend ce cas impossible, et c'est précisément pourquoi il lève ici plutôt que de
        // sauter la ligne : un lexique amputé d'une entrée rendrait un rapport plus pauvre sans un
        // mot, et c'est l'Omission silencieuse au seul endroit où elle serait invisible.
        throw new InvalidOperationException(
          $"Le lexique gelé « {resourceName} » porte une ligne sans tabulation : « {line} ». "
          + "Un lexique n'est pas une configuration : il est le gel d413d55 ou il n'est rien.");
      }

      var term = ScreeningIdentifiers.Normalise(line[..separator]);

      pairs.Add((term, PersonalDataCategory.FromName(line[(separator + 1)..], ignoreCase: false)));
    }

    return pairs;
  }
}

/// <summary>
/// La mécanique <b>générique</b> qui découpe et normalise un identifiant de schéma : la seule
/// connaissance lexicale du moteur vit dans les fichiers gelés, jamais ici.
/// </summary>
/// <remarks>
/// ⚠️ <b>Ce n'est pas de la prose, et ce n'est donc pas un NER.</b> <c>dt_naiss</c> ne se lit pas
/// comme une phrase : ce qui opère sur des noms d'identifiants relève du lexique, des règles et de
/// la morphologie.
/// </remarks>
internal static partial class ScreeningIdentifiers
{
  /// <summary>Les chiffres qu'un jeton porte à ses bords — <c>ligne1</c>, <c>adr_l2</c> — n'ont aucun sens ici.</summary>
  private static readonly char[] Digits = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

  /// <summary>Sans accents et en minuscules : <c>Prénom</c>, <c>PRENOM</c> et <c>prenom</c> sont un seul jeton.</summary>
  internal static string Normalise(string text)
  {
    var decomposed = text.Normalize(NormalizationForm.FormD);
    var stripped = new StringBuilder(decomposed.Length);

    foreach (var character in decomposed)
    {
      if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
      {
        stripped.Append(character);
      }
    }

    return stripped.ToString().ToLowerInvariant();
  }

  /// <summary>
  /// Les jetons d'un identifiant : séparateurs, <c>camelCase</c>, chiffres de bord ôtés.
  /// <c>adresseLivraison_2</c> rend <c>adresse</c> et <c>livraison</c>.
  /// </summary>
  internal static IReadOnlyList<string> Tokenise(string identifier)
  {
    var tokens = new List<string>();

    foreach (var piece in NonAlphanumeric().Split(identifier))
    {
      foreach (var word in CamelCase().Split(piece))
      {
        var token = Normalise(word).Trim(Digits);

        if (token.Length > 0)
        {
          tokens.Add(token);
        }
      }
    }

    return tokens;
  }

  /// <summary>Les mots d'un commentaire en prose, normalisés. Un commentaire absent n'a aucun mot.</summary>
  internal static IReadOnlyList<string> Words(string? prose)
  {
    if (prose is null)
    {
      return [];
    }

    return
    [
      .. NonAlphanumeric()
        .Split(prose)
        .Where(word => word.Length > 0)
        .Select(Normalise),
    ];
  }

  [GeneratedRegex("[^a-zA-Z0-9]+")]
  private static partial Regex NonAlphanumeric();

  [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
  private static partial Regex CamelCase();
}
