using System.Globalization;
using System.Text.Json;

namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Ce qui lit le collage de l'<c>Operator</c> : un <see cref="ColumnListing"/> entier, ou un
/// <see cref="ColumnListingRefusal"/> qui nomme lequel des neuf cas s'applique.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Un pivot de 4 000 lignes dont 3 sont illisibles est refusé en entier.</b> L'acceptation
/// partielle est séduisante — l'<c>Operator</c> a fait un long trajet, tout jeter pour trois lignes
/// est brutal — et c'est exactement le piège. Un <c>Screening</c> bâti sur 99,9 % d'un relevé
/// <b>se lit comme complet</b>, et l'<c>Omission relue</c> repose entièrement sur le fait que le
/// rapport rende <b>toutes</b> les colonnes du relevé, <c>Unflagged</c> comprises. Le coût du refus
/// est faible et <b>réversible</b> : on relance une requête, on ne perd aucun arbitrage.
/// </para>
/// <para>
/// <b>Elle rend un refus, elle ne lève pas.</b> Un collage mal formé n'est pas une programmation
/// fautive : c'est le cas courant, et c'est ici — et ici seulement — qu'il produit une phrase que
/// l'<c>Operator</c> peut lire. Les fabriques du domaine, elles, lèvent, parce que ce qui leur
/// arrive est déjà passé par ici.
/// </para>
/// <para>
/// ⚠️ <b>L'ordre des contrôles n'est pas indifférent, et deux d'entre eux sont délibérés.</b> Le
/// plafond est lu sur le compte <b>annoncé</b> avant que la moindre ligne ne soit analysée, pour que
/// « ton relevé annonce 31 000 colonnes, le plafond est 20 000 » sorte sans qu'on ait payé l'analyse
/// de 31 000 lignes. Et le compte est vérifié <b>avant</b> le cas du relevé vide, sans quoi un
/// collage annonçant 400 colonnes et n'en portant aucune serait rendu comme « relevé vide » là où il
/// est une troncature.
/// </para>
/// </remarks>
public static class ColumnListingIngestion
{
  private const string FormatKey = "format";
  private const string DialectKey = "dialecte";
  private const string DatabaseKey = "base";
  private const string GeneratedOnKey = "genere_le";
  private const string ClosingMarkerKey = "fin";
  private const string ClosingCountKey = "colonnes";

  private const string SchemaKey = "schema";
  private const string TableKey = "table";
  private const string ColumnKey = "colonne";
  private const string PositionKey = "position";
  private const string DataTypeKey = "type";
  private const string NullableKey = "nullable";
  private const string ColumnCommentKey = "commentaire_colonne";
  private const string TableCommentKey = "commentaire_table";
  private const string ReferencedTableKey = "table_referencee";

  /// <summary>
  /// Lit le collage. <b>Entier ou refusé</b> : il n'existe aucun chemin qui rende une part du relevé.
  /// </summary>
  /// <param name="paste">Ce que l'<c>Operator</c> a collé, tel quel.</param>
  /// <returns>
  /// Un <see cref="IngestionOutcome"/> portant le relevé accepté, ou le refus et le cas qui
  /// s'applique. La méthode ne lève jamais sur un collage mal formé.
  /// </returns>
  public static IngestionOutcome Ingest(string? paste)
  {
    var lines = ReadLines(paste);

    if (lines.Count == 0)
    {
      return Refused(RefusalCause.MissingHeader, 1, "un collage vide");
    }

    var header = lines[0];

    if (!TryReadObject(header.Text, out var headerFields))
    {
      return Refused(RefusalCause.MissingHeader, header.Number, header.Text);
    }

    if (ReadText(headerFields, FormatKey) is not { } format)
    {
      return Refused(RefusalCause.MissingHeader, header.Number, header.Text);
    }

    if (!string.Equals(format, ColumnListing.FormatVersion, StringComparison.Ordinal))
    {
      return Refused(RefusalCause.UnknownFormatVersion, header.Number, format);
    }

    if (ReadDeclaredName(headerFields, DialectKey, "Le dialecte du relevé", Screening.MaxDialectLength) is not { } dialect
      || ReadDeclaredName(headerFields, DatabaseKey, "Le nom de la base", Screening.MaxDatabaseNameLength) is not { } database
      || ReadInstant(headerFields, GeneratedOnKey) is not { } generatedOn)
    {
      return Refused(RefusalCause.MissingHeader, header.Number, header.Text);
    }

    if (lines.Count < 2)
    {
      return Refused(RefusalCause.MissingClosingLine, header.Number, "rien après l'en-tête");
    }

    var closing = lines[^1];

    if (!TryReadObject(closing.Text, out var closingFields)
      || ReadBoolean(closingFields, ClosingMarkerKey) is not true
      || ReadCount(closingFields, ClosingCountKey) is not { } announced
      || announced < 0)
    {
      return Refused(RefusalCause.MissingClosingLine, closing.Number, closing.Text);
    }

    var body = lines.GetRange(1, lines.Count - 2);

    if (announced > ColumnListing.MaxColumns || body.Count > ColumnListing.MaxColumns)
    {
      return Refused(
        RefusalCause.CeilingExceeded,
        closing.Number,
        Math.Max(announced, body.Count).ToString(CultureInfo.InvariantCulture) + " colonnes");
    }

    var declaredColumnCount = (int)announced;

    if (declaredColumnCount != body.Count)
    {
      return Refused(
        RefusalCause.CountMismatch,
        closing.Number,
        $"{declaredColumnCount.ToString(CultureInfo.InvariantCulture)} colonnes annoncées, "
        + $"{body.Count.ToString(CultureInfo.InvariantCulture)} lignes reçues");
    }

    if (body.Count == 0)
    {
      return Refused(RefusalCause.EmptyListing, closing.Number, "aucune ligne de colonne");
    }

    var columns = new List<ListedColumn>(body.Count);

    foreach (var line in body)
    {
      if (ReadColumn(line.Text) is not { } column)
      {
        return Refused(RefusalCause.UnreadableColumnLine, line.Number, line.Text);
      }

      columns.Add(column);
    }

    return FirstDuplicate(body, columns)
      ?? FirstRankAnomaly(body, columns)
      ?? IngestionOutcome.Accepted(
        new ColumnListing(dialect, database, generatedOn, declaredColumnCount, columns));
  }

  /// <summary>
  /// Les lignes non vides du collage, chacune portant son numéro <b>d'origine</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Les lignes vides sont ignorées et les numéros ne le sont pas.</b> Un collage porte les fins
  /// de ligne de la machine qui l'a produit et un tour par le presse-papier en ajoute souvent une ;
  /// aucune des deux n'est une troncature. Mais renuméroter ferait citer à l'<c>Operator</c> une
  /// ligne qu'il ne trouverait pas dans ce qu'il a sous les yeux — d'où la normalisation des trois
  /// conventions de fin de ligne <b>avant</b> le découpage, plutôt qu'un découpage sur les deux
  /// caractères, qui compterait chaque <c>CRLF</c> pour deux lignes.
  /// <para>
  /// ⚠️ <b>La marque d'ordre d'octets est retirée, et elle ne se retire pas toute seule.</b> Un
  /// <c>U+FEFF</c> n'est pas un blanc pour .NET : <c>Trim</c> le laisse, <c>JsonDocument</c> le
  /// refuse, et un pivot sincère passé par <c>Out-File</c> ou le Bloc-notes se verrait répondre
  /// « en-tête absent ou illisible » à propos d'un en-tête que l'<c>Operator</c> a sous les yeux et
  /// qui est parfaitement correct. Un refus qui nomme le mauvais problème est pire qu'inutile : il
  /// fait relancer une requête qui produira exactement le même collage.
  /// </para>
  /// </remarks>
  private static List<PastedLine> ReadLines(string? paste)
  {
    var lines = new List<PastedLine>();

    if (string.IsNullOrWhiteSpace(paste))
    {
      return lines;
    }

    var number = 0;
    var normalised = paste
      .TrimStart('﻿')
      .Replace("\r\n", "\n", StringComparison.Ordinal)
      .Replace('\r', '\n');

    foreach (var line in normalised.Split('\n'))
    {
      number++;

      var trimmed = line.Trim();

      if (trimmed.Length > 0)
      {
        lines.Add(new PastedLine(number, trimmed));
      }
    }

    return lines;
  }

  private static IngestionOutcome? FirstDuplicate(List<PastedLine> body, List<ListedColumn> columns)
  {
    var seen = new HashSet<ColumnIdentity>();

    for (var index = 0; index < columns.Count; index++)
    {
      if (!seen.Add(columns[index].Identity))
      {
        return Refused(
          RefusalCause.DuplicateColumn,
          body[index].Number,
          columns[index].Identity.ToString());
      }
    }

    return null;
  }

  /// <summary>
  /// Le trou dans les rangs d'une table, et la <b>répétition</b> d'un rang avec lui : les deux disent
  /// la même chose, que la table n'a plus l'ordre que le relevé prétend rendre.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Aucun rang de départ n'est exigé.</b> PostgreSQL numérote à partir de 1 et SQLite à partir
  /// de 0 ; exiger 1 refuserait tout relevé SQLite. Ce qui n'est jamais légitime est le trou, jamais
  /// le point de départ.
  /// <para>
  /// ⚠️ <b>La ligne citée est celle où la suite se casse, jamais la dernière de la table.</b> Un trou
  /// entre les rangs 3 et 5 d'une table de deux cents colonnes se signalerait sinon sur la deux
  /// centième ligne, où l'<c>Operator</c> trouverait une colonne parfaitement formée et n'aurait
  /// aucun moyen de retrouver le trou. Le refus promet de nommer « la ligne du collage qui l'a
  /// déclenché » ; c'est celle-là.
  /// </para>
  /// </remarks>
  private static IngestionOutcome? FirstRankAnomaly(List<PastedLine> body, List<ListedColumn> columns)
  {
    foreach (var table in columns.Select((column, index) => (column, index))
      .GroupBy(entry => entry.column.Identity.TableIdentity))
    {
      var byRank = table.OrderBy(entry => entry.column.Position).ToList();

      for (var step = 1; step < byRank.Count; step++)
      {
        var previous = byRank[step - 1].column.Position;
        var current = byRank[step].column.Position;

        if (current == previous + 1)
        {
          continue;
        }

        return Refused(
          RefusalCause.RankGap,
          body[byRank[step].index].Number,
          current == previous
            ? $"la table {table.Key} porte deux colonnes au rang {current.ToString(CultureInfo.InvariantCulture)}"
            : $"la table {table.Key} passe du rang {previous.ToString(CultureInfo.InvariantCulture)} "
              + $"au rang {current.ToString(CultureInfo.InvariantCulture)}");
      }
    }

    return null;
  }

  /// <summary>
  /// Une ligne de colonne, ou <c>null</c> si elle n'en est pas une.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Les neuf clés sont exigées, valeurs vides comprises.</b> La requête les émet toutes ; une
  /// clé absente n'est donc pas une valeur absente mais une ligne qui ne vient pas de la requête —
  /// un collage retouché à la main, ou deux pivots de formes différentes mis bout à bout. C'est
  /// aussi ce qui distingue « SQLite ne rend aucun commentaire », qui écrit <c>null</c>, de « cette
  /// ligne n'a pas été produite par la requête », qui n'écrit rien.
  /// <para>
  /// ⚠️ <b>Le <i>type</i> de chaque valeur est exigé avec la clé, et c'est la moitié qui manquait.</b>
  /// Sans lui, une clé présente mais mal typée se lirait comme une absence : l'<c>information_schema</c>
  /// de MariaDB rend la nullabilité en <c>'YES'</c>/<c>'NO'</c>, et un <c>"nullable":"YES"</c> pris
  /// pour <c>null</c> désactiverait le filtre de nullabilité sur <b>toute</b> une base, sans un mot,
  /// dans un rapport qui se lit comme complet. C'est l'<c>Omission silencieuse</c> exacte que les neuf
  /// cas existent pour empêcher, et refuser une valeur mal typée coûte un relancement de requête.
  /// </para>
  /// </remarks>
  private static ListedColumn? ReadColumn(string line)
  {
    if (!TryReadObject(line, out var fields))
    {
      return null;
    }

    if (!HasKind(fields, SchemaKey, JsonValueKind.String)
      || !HasKind(fields, TableKey, JsonValueKind.String)
      || !HasKind(fields, ColumnKey, JsonValueKind.String)
      || !HasKind(fields, PositionKey, JsonValueKind.Number)
      || !HasKind(fields, DataTypeKey, JsonValueKind.String, JsonValueKind.Null)
      || !HasKind(fields, NullableKey, JsonValueKind.True, JsonValueKind.False, JsonValueKind.Null)
      || !HasKind(fields, ColumnCommentKey, JsonValueKind.String, JsonValueKind.Null)
      || !HasKind(fields, TableCommentKey, JsonValueKind.String, JsonValueKind.Null)
      || !HasKind(fields, ReferencedTableKey, JsonValueKind.String, JsonValueKind.Null))
    {
      return null;
    }

    if (ReadWholeNumber(fields, PositionKey) is not { } position || position < 0)
    {
      return null;
    }

    try
    {
      return ListedColumn.Of(
        ColumnIdentity.Of(
          ReadText(fields, SchemaKey),
          ReadText(fields, TableKey),
          ReadText(fields, ColumnKey)),
        position,
        ReadText(fields, DataTypeKey),
        ReadBoolean(fields, NullableKey),
        ReadText(fields, ColumnCommentKey),
        ReadText(fields, TableCommentKey),
        ReadText(fields, ReferencedTableKey));
    }
    catch (ArgumentException)
    {
      // Un nom vide, démesuré ou porteur d'un caractère de contrôle : la ligne ne désigne aucune
      // colonne. Le domaine lève parce que ce qui lui arrive est censé être déclarable ; ici, c'est
      // un refus lisible, et c'est la frontière qui le rend tel.
      return null;
    }
  }

  private static bool TryReadObject(string line, out Dictionary<string, JsonElement> fields)
  {
    fields = [];

    try
    {
      using var document = JsonDocument.Parse(line);

      if (document.RootElement.ValueKind != JsonValueKind.Object)
      {
        return false;
      }

      foreach (var field in document.RootElement.EnumerateObject())
      {
        fields[field.Name] = field.Value.Clone();
      }

      return true;
    }
    catch (JsonException)
    {
      return false;
    }
  }

  private static string? ReadText(Dictionary<string, JsonElement> fields, string key)
  {
    if (!fields.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.String)
    {
      return null;
    }

    var text = value.GetString()?.Trim();

    return string.IsNullOrEmpty(text) ? null : text;
  }

  private static bool? ReadBoolean(Dictionary<string, JsonElement> fields, string key)
  {
    return fields.TryGetValue(key, out var value)
      ? value.ValueKind switch
      {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
      }
      : null;
  }

  private static int? ReadWholeNumber(Dictionary<string, JsonElement> fields, string key)
  {
    return fields.TryGetValue(key, out var value)
      && value.ValueKind == JsonValueKind.Number
      && value.TryGetInt32(out var number)
      ? number
      : null;
  }

  /// <summary>
  /// La valeur porte-t-elle bien l'une des formes JSON attendues ? Une clé présente mais mal typée
  /// n'est <b>pas</b> une valeur absente.
  /// </summary>
  private static bool HasKind(
    Dictionary<string, JsonElement> fields,
    string key,
    params JsonValueKind[] kinds)
  {
    return fields.TryGetValue(key, out var value) && Array.IndexOf(kinds, value.ValueKind) >= 0;
  }

  /// <summary>
  /// Un nom que l'en-tête déclare, passé <b>au même garde que le domaine</b> — non vide, borné, sans
  /// caractère de contrôle — ou <c>null</c>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Sans ce contrôle, l'invariant écrit sur cette classe serait faux.</b> L'ingestion promet
  /// que ce qui arrive aux fabriques du domaine est déjà passé par ici ; un <c>base</c> de 150
  /// caractères — le chemin d'un fichier SQLite, cas courant et non tordu — traverserait
  /// <see cref="Ingest"/> et ferait lever <see cref="Screening"/>, c'est-à-dire une erreur nue à la
  /// surface au lieu de l'un des neuf refus nommés, et un collage perdu sans une phrase disant quoi
  /// changer.
  /// </remarks>
  private static string? ReadDeclaredName(
    Dictionary<string, JsonElement> fields,
    string key,
    string subject,
    int maxLength)
  {
    if (ReadText(fields, key) is not { } text)
    {
      return null;
    }

    try
    {
      return ScreeningText.OrThrow(text, subject, maxLength, key);
    }
    catch (ArgumentException)
    {
      return null;
    }
  }

  /// <summary>
  /// Le compte annoncé par la ligne de fin, en <see cref="long"/>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Il est lu large exprès.</b> Le borner à un <see cref="int"/> ferait rendre « ligne de fin
  /// absente ou illisible » à un relevé annonçant plus de deux milliards de colonnes, ou écrivant son
  /// compte <c>412.0</c> — c'est-à-dire le cas n° 2, la troncature au presse-papier, sur un collage
  /// qui n'a rien d'une troncature. L'<c>Operator</c> recollerait indéfiniment. Le plafond, lui,
  /// tranche ensuite et le dit en toutes lettres.
  /// </remarks>
  private static long? ReadCount(Dictionary<string, JsonElement> fields, string key)
  {
    if (!fields.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.Number)
    {
      return null;
    }

    if (value.TryGetInt64(out var whole))
    {
      return whole;
    }

    return value.TryGetDouble(out var number) && double.IsInteger(number)
      ? (long)Math.Clamp(number, long.MinValue, long.MaxValue)
      : null;
  }

  /// <summary>
  /// L'instant de génération, <b>et il doit porter son décalage</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Un instant sans décalage prendrait celui du serveur, et cette erreur est muette.</b>
  /// <c>DateTimeOffset.TryParse</c> ignore <c>RoundtripKind</c> : le même collage, lu sur un serveur
  /// à Tokyo puis sur un conteneur en UTC, donnerait deux instants distants de neuf heures. Cette
  /// date est la seule chose qui dise à l'<c>Operator</c> de quand date son relevé — un pivot vieux
  /// de neuf heures s'afficherait comme frais, ce qui est exactement la question à laquelle le
  /// service ne sait déjà pas répondre pour la provenance. Elle est donc exigée dans une forme
  /// unique, celle que <c>pivot-format.md</c> impose aux trois requêtes.
  /// </remarks>
  private static DateTimeOffset? ReadInstant(Dictionary<string, JsonElement> fields, string key)
  {
    if (ReadText(fields, key) is not { } text)
    {
      return null;
    }

    var withOffset = text.EndsWith('Z')
      ? string.Concat(text.AsSpan(0, text.Length - 1), "+00:00")
      : text;

    return DateTimeOffset.TryParseExact(
      withOffset,
      (string[])["yyyy-MM-ddTHH:mm:sszzz", "yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz"],
      CultureInfo.InvariantCulture,
      DateTimeStyles.None,
      out var instant)
      ? instant
      : null;
  }

  private static IngestionOutcome Refused(RefusalCause cause, int lineNumber, string observed)
  {
    return IngestionOutcome.Refused(new ColumnListingRefusal(cause, lineNumber, observed));
  }

  private readonly record struct PastedLine(int Number, string Text);
}

/// <summary>
/// Ce que l'ingestion rend : <b>un relevé entier, ou un refus</b>, jamais les deux et jamais ni l'un
/// ni l'autre.
/// </summary>
/// <remarks>
/// ⚠️ <b>Il n'y a pas de troisième cas, et surtout pas d'« accepté avec réserves ».</b> Un relevé
/// accepté dont on aurait mis trois lignes de côté est exactement ce que le refus en bloc existe
/// pour empêcher : le rapport se lirait comme complet, et les trois lignes écartées seraient celles
/// que personne ne relirait jamais.
/// </remarks>
public sealed class IngestionOutcome
{
  private IngestionOutcome(ColumnListing? listing, ColumnListingRefusal? refusal)
  {
    Listing = listing;
    Refusal = refusal;
  }

  /// <summary>Le relevé, entier — ou <c>null</c> si le collage a été refusé.</summary>
  public ColumnListing? Listing { get; }

  /// <summary>Le refus et le cas qui s'applique — ou <c>null</c> si le collage a été accepté.</summary>
  public ColumnListingRefusal? Refusal { get; }

  /// <summary>Le collage a-t-il été accepté ? Si oui, <see cref="Listing"/> est là.</summary>
  public bool IsAccepted => Listing is not null;

  /// <summary>Le collage a-t-il été refusé ? Si oui, <see cref="Refusal"/> nomme le cas.</summary>
  public bool IsRefused => Refusal is not null;

  internal static IngestionOutcome Accepted(ColumnListing listing)
  {
    return new IngestionOutcome(listing, null);
  }

  internal static IngestionOutcome Refused(ColumnListingRefusal refusal)
  {
    return new IngestionOutcome(null, refusal);
  }
}
