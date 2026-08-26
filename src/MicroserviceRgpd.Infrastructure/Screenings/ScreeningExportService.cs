using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Screenings;

/// <summary>
/// Le rendu de la <c>Cartographie</c> en deux fichiers : le JSON pour qui outille, le CSV pour qui
/// double-clique.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce service est licite, et ce qui le sépare du pont interdit vers le <c>Manifest</c> n'est
/// pas son nom.</b> <b>L'export a un destinataire humain qui l'a demandé ; le pont aurait un
/// destinataire machine que personne n'a demandé.</b> Ce qui doit arrêter une revue de code est donc
/// une <i>écriture</i> côté <c>Casework</c>, jamais le mot « export » : il n'y en a aucune ici, ce
/// rendu ne connaissant ni dépôt, ni horloge, ni file.
/// </para>
/// <para>
/// <b>Il ne calcule rien.</b> Toute la matière arrive déjà tranchée dans la
/// <see cref="PersonalDataMap"/> : ce type ne fait que citer, formater et concaténer. Une décision
/// prise ici — un filtre, un compte, une phrase — serait une seconde vérité sur les arbitrages, à
/// l'endroit du produit le plus difficile à relire.
/// </para>
/// <para>
/// ⚠️ <b>Aucune clause d'incomplétude n'est écrite ici, sous aucune forme</b> — ni bloc avant
/// l'en-tête, ni bloc après les données, ni colonne répétée, ni second fichier, ni ZIP, ni colonne de
/// provenance par ligne. Les cinq mécanismes ont été construits ou examinés, puis écartés : voir
/// <see cref="PersonalDataMap"/>, où le motif est écrit une fois.
/// </para>
/// </remarks>
public sealed class ScreeningExportService : IScreeningExport
{
  /// <summary>Ce que le <c>Content-Type</c> annonce pour le JSON.</summary>
  public const string JsonContentType = "application/json; charset=utf-8";

  /// <summary>
  /// Ce que le <c>Content-Type</c> annonce pour le CSV.
  /// </summary>
  /// <remarks>
  /// <b>Le jeu de caractères est annoncé bien qu'un BOM le porte déjà</b> : le tableur lit le BOM, le
  /// reste du monde lit l'en-tête, et les deux disent la même chose.
  /// </remarks>
  public const string CsvContentType = "text/csv; charset=utf-8";

  /// <summary>Les seize colonnes du CSV, dans l'ordre où elles paraissent.</summary>
  /// <remarks>
  /// ⚠️ <b>L'ordre est celui de la spec, et les noms sont ceux de la spec</b> : <c>rendu_le</c> en
  /// miroir de <c>Arbitration.RenderedOn</c> que l'ADR-0014 a fixé. <c>signataire</c> et
  /// <c>signe_le</c> n'existent nulle part — ce contexte n'enregistre pas qui a arbitré.
  /// </remarks>
  private const string CsvHeader =
    "schema;table;colonne;position;type;nullable;commentaire_colonne;commentaire_table;"
    + "signalee;categorie;categorie_libelle;degre;degre_libelle;motif;etat;rendu_le";

  /// <summary>
  /// La fin de ligne du CSV, telle que la RFC 4180 la fixe.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Elle ne suit pas l'hôte.</b> Le service tourne sous Linux et le fichier s'ouvre sous
  /// Windows : rendre <c>\n</c> parce que la machine qui écrit en est là aurait fait dépendre la
  /// lisibilité du fichier de l'endroit où le service est déployé.
  /// </remarks>
  private const string CsvNewLine = "\r\n";

  /// <summary>
  /// ⚠️ <b>L'horodatage du CSV perd son décalage</b>, et c'est la seule forme qu'un tableur français
  /// reconnaisse comme une date plutôt que comme du texte. Le JSON, lui, garde l'ISO 8601 complet.
  /// </summary>
  private const string CsvTimestamp = "yyyy-MM-dd HH:mm:ss";

  /// <summary>
  /// Les trois caractères qui obligent un champ à se citer : le séparateur, le guillemet, et le
  /// retour à la ligne sous ses deux octets.
  /// </summary>
  private static readonly SearchValues<char> CsvNeedsQuoting = SearchValues.Create(";\"\r\n");

  /// <summary>
  /// Les premiers caractères qui font d'une case une <b>formule</b> aux yeux d'un tableur.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le tiret et le plus y sont</b> : un commentaire de table écrit « - à revoir » n'a rien
  /// d'hostile, mais un tableur le lit comme le début d'un calcul, et la porte qu'on laisserait
  /// ouverte pour lui est la même que celle de <c>=HYPERLINK(…)</c>.
  /// </remarks>
  private static readonly SearchValues<char> FormulaLead = SearchValues.Create("=+-@\t\r");

  /// <inheritdoc />
  public ExportedFile AsJson(PersonalDataMap map)
  {
    ArgumentNullException.ThrowIfNull(map);

    using var stream = new MemoryStream();

    // Indenté : ce fichier se relit à l'œil aussi souvent qu'il se parse, et l'octet économisé sur
    // cinq mille lignes ne rachète pas une ligne unique illisible dans un courriel.
    //
    // ⚠️ L'encodeur relâché est le PENDANT de ce choix, et non un détail. Le codeur par défaut est
    // taillé pour du JSON qu'on incruste dans une page HTML : il échappe l'apostrophe, l'esperluette
    // et tout le non-ASCII, si bien qu'« à arbitrer » part en « à arbitrer » et qu'un motif
    // français devient illisible pour le destinataire humain. Ici rien n'est incrusté nulle part :
    // ces octets partent tels quels dans un téléchargement annoncé application/json.
    // ⚠️ Et la conséquence portait au-delà du confort : échappé, aucun texte français ne se cherche
    // plus par sous-chaîne, et le garde qui vérifie qu'aucun mot de la clause d'incomplétude n'entre
    // dans le fichier serait resté vert quoi qu'on y écrive.
    var options = new JsonWriterOptions
    {
      Indented = true,
      Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    using (var json = new Utf8JsonWriter(stream, options))
    {
      json.WriteStartObject();

      WriteListing(json, map);

      json.WriteStartArray("colonnes");

      foreach (var column in map.Columns)
      {
        WriteColumn(json, column);
      }

      json.WriteEndArray();
      json.WriteEndObject();
    }

    return new ExportedFile(NameOf(map, "json"), JsonContentType, stream.ToArray());
  }

  /// <inheritdoc />
  public ExportedFile AsCsv(PersonalDataMap map)
  {
    ArgumentNullException.ThrowIfNull(map);

    var csv = new StringBuilder();

    csv.Append(CsvHeader).Append(CsvNewLine);

    foreach (var column in map.Columns)
    {
      AppendRow(csv, column);
    }

    // Le BOM est écrit : sans lui, un tableur français ouvre « prénom » en « prÃ©nom » sur un
    // fichier dont tout le propos est de se lire sans outillage.
    return new ExportedFile(
      NameOf(map, "csv"),
      CsvContentType,
      [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())]);
  }

  /// <summary>
  /// L'objet <c>releve</c>, en tête et <b>une seule fois</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Rien de ce qui est ici ne se répète sur la ligne.</b> Un rapport a une base, un dialecte,
  /// un moteur et une date de lancement : les recopier cinq mille fois aurait laissé croire qu'ils
  /// pourraient varier d'une colonne à l'autre, et transformé un fichier de travail en une matière à
  /// vérifier.
  /// </remarks>
  private static void WriteListing(Utf8JsonWriter json, PersonalDataMap map)
  {
    json.WriteStartObject("releve");

    json.WriteString("screeningId", map.Id.Value);
    json.WriteString("base", map.Database);
    json.WriteString("dialecte", map.Dialect);

    json.WriteStartObject("moteur");
    json.WriteString("nom", map.Engine.Name);
    json.WriteString("version", map.Engine.Version);
    json.WriteEndObject();

    // L'ISO 8601 complet, décalage compris : le JSON a un destinataire outillé, à qui l'heure exacte
    // est utile et le décalage indispensable.
    json.WriteString("lanceLe", map.LaunchedOn);
    json.WriteString("exporteLe", map.ExportedOn);

    json.WriteStartObject("comptes");
    json.WriteNumber("colonnes", map.Counts.Columns);
    json.WriteNumber("tables", map.Counts.Tables);
    json.WriteNumber("colonnesSansCommentaire", map.Counts.ColumnsWithoutAComment);
    json.WriteNumber("signalees", map.Counts.Flagged);
    json.WriteNumber("retenues", map.Counts.Retained);
    json.WriteNumber("ecartees", map.Counts.SetAside);
    json.WriteNumber("enAttente", map.Counts.Awaiting);
    json.WriteNumber("retenuesSurNonSignalees", map.Counts.RetainedOnUnflagged);
    json.WriteNumber("nonSignaleesNonRelues", map.Counts.UnreadUnflagged);
    json.WriteEndObject();

    // ⚠️ Absente, et non à zéro, quand plus rien n'attend : « 0 colonne attend encore » se lit comme
    // une réserve alors qu'elle n'en est pas une. Voir PersonalDataMap.UnfinishedArbitration.
    if (map.UnfinishedArbitration is { } unfinished)
    {
      json.WriteString("arbitrageInacheve", unfinished);
    }

    json.WriteEndObject();
  }

  /// <summary>
  /// Une ligne JSON : les seize champs, en camelCase, dans l'ordre des trois blocs.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Les champs absents sont écrits <c>null</c> plutôt qu'omis.</b> Un objet dont les clés
  /// varient d'une ligne à l'autre force qui le lit à écrire un test d'existence par champ, sur un
  /// fichier dont l'absence est le régime majoritaire — la plupart des colonnes n'ont ni commentaire,
  /// ni motif, ni arbitrage.
  /// </remarks>
  private static void WriteColumn(Utf8JsonWriter json, MappedColumn column)
  {
    json.WriteStartObject();

    json.WriteString("schema", column.Schema);
    json.WriteString("table", column.Table);
    json.WriteString("colonne", column.Column);
    json.WriteNumber("position", column.Position);
    json.WriteString("type", column.DataType);
    WriteNullableBoolean(json, "nullable", column.IsNullable);
    json.WriteString("commentaireColonne", column.ColumnComment);
    json.WriteString("commentaireTable", column.TableComment);

    json.WriteBoolean("signalee", column.IsFlagged);
    json.WriteString("categorie", column.Category);
    json.WriteString("categorieLibelle", column.CategoryLabel);
    json.WriteString("degre", column.Strength);
    json.WriteString("degreLibelle", column.StrengthLabel);
    json.WriteString("motif", column.Reason);

    json.WriteString("etat", column.State);

    if (column.RenderedOn is { } renderedOn)
    {
      json.WriteString("renduLe", renderedOn);
    }
    else
    {
      json.WriteNull("renduLe");
    }

    json.WriteEndObject();
  }

  private static void WriteNullableBoolean(Utf8JsonWriter json, string name, bool? value)
  {
    if (value is { } known)
    {
      json.WriteBoolean(name, known);
    }
    else
    {
      json.WriteNull(name);
    }
  }

  /// <summary>Une ligne CSV : les mêmes seize champs, tels qu'un tableur les ouvre.</summary>
  private static void AppendRow(StringBuilder csv, MappedColumn column)
  {
    Append(csv, column.Schema);
    Append(csv, column.Table);
    Append(csv, column.Column);
    Append(csv, column.Position.ToString(CultureInfo.InvariantCulture));
    Append(csv, column.DataType);
    Append(csv, YesNo(column.IsNullable));
    Append(csv, column.ColumnComment);
    Append(csv, column.TableComment);

    Append(csv, YesNo(column.IsFlagged));
    Append(csv, column.Category);
    Append(csv, column.CategoryLabel);
    Append(csv, column.Strength);
    Append(csv, column.StrengthLabel);
    Append(csv, column.Reason);

    Append(csv, column.State);
    Append(csv, column.RenderedOn?.ToString(CsvTimestamp, CultureInfo.InvariantCulture), last: true);
  }

  private static void Append(StringBuilder csv, string? value, bool last = false)
  {
    csv.Append(Quoted(value));
    csv.Append(last ? CsvNewLine : ";");
  }

  /// <summary>
  /// Un champ cité selon la RFC 4180 <b>quand il en a besoin, et seulement alors</b>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Les trois caractères qui l'exigent sont le séparateur, le guillemet et le retour à la
  /// ligne</b> — et le retour à la ligne se rencontre pour de bon : un commentaire de table écrit par
  /// un DBA en porte, et sans citation il coupe le fichier en deux au milieu d'une ligne. Le
  /// guillemet intérieur se double, ce que la RFC fixe et qu'aucun tableur ne lit autrement.
  /// </para>
  /// <para>
  /// <b>Citer systématiquement aurait été plus court à écrire</b> ; on ne le fait pas, parce qu'un
  /// fichier où chaque case porte des guillemets se relit mal à l'œil nu dans un éditeur de texte, ce
  /// qui est l'autre usage de ce fichier.
  /// </para>
  /// </remarks>
  private static string Quoted(string? value)
  {
    // ⚠️ L'absence est une case vide, jamais le mot « null » ni un tiret : un tableur trie et filtre
    // une case vide, il traite les deux autres comme une valeur.
    if (string.IsNullOrEmpty(value))
    {
      return string.Empty;
    }

    // ⚠️ Une case qui commence par « = », « + », « - », « @ » ou une tabulation est une FORMULE pour
    // un tableur, pas un texte. Tout ce qui remplit ces cases — commentaires de catalogue, noms de
    // schéma, de table et de colonne — est recopié d'un relevé que le service n'a jamais vérifié,
    // et ce fichier est fait pour être ouvert d'un double-clic puis transféré à un tiers : un
    // commentaire de table valant =HYPERLINK("https://…"&A2;"cliquer") s'exécuterait chez le
    // destinataire et emporterait les cases voisines avec lui.
    // ⚠️ Citer ne suffit pas — un tableur évalue la formule citée tout autant. C'est l'apostrophe
    // de tête qui neutralise, et elle est le coût assumé : le tableur ne l'affiche pas, un éditeur
    // de texte si. Neutraliser un commentaire qu'on lira mal vaut mieux qu'exécuter un commentaire
    // qu'on n'a jamais relu.
    var neutralised = FormulaLead.Contains(value[0]) ? "'" + value : value;

    return neutralised.AsSpan().IndexOfAny(CsvNeedsQuoting) >= 0
      ? $"\"{neutralised.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
      : neutralised;
  }

  /// <summary>
  /// Un booléen tel qu'il se lit : <c>oui</c>, <c>non</c>, ou rien du tout.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b><c>true</c>/<c>false</c> auraient été deux mots anglais dans un fichier français</b>, et
  /// <c>1</c>/<c>0</c> deux nombres qu'un tableur additionne. L'absence reste vide : « le relevé ne
  /// dit pas si cette colonne est nullable » n'est pas « elle ne l'est pas ».
  /// </remarks>
  private static string? YesNo(bool? value) => value switch
  {
    true => "oui",
    false => "non",
    null => null,
  };

  /// <summary>
  /// Le nom du fichier : <c>cartographie-&lt;base&gt;-&lt;AAAA-MM-JJ&gt;.&lt;extension&gt;</c>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>C'est tout ce que le CSV dit de sa provenance</b>, et c'est une conséquence sèche assumée :
  /// deux exports de deux bases sont indiscernables une fois renommés, et le moteur qui a produit un
  /// CSV est irrécupérable depuis ce CSV. Le JSON, lui, porte son <c>releve</c>.
  /// </para>
  /// <para>
  /// <b>La base est réduite aux caractères qu'un nom de fichier traverse sans dommage.</b> Elle ne
  /// porte déjà jamais de chemin — voir <see cref="Screening.Database"/> —, mais un espace ou un
  /// accent survit mal à un <c>Content-Disposition</c>, et un point de plus ferait lire une fausse
  /// extension.
  /// </para>
  /// </remarks>
  private static string NameOf(PersonalDataMap map, string extension)
  {
    var name = new StringBuilder(map.Database.Length);

    foreach (var character in map.Database)
    {
      name.Append(char.IsAsciiLetterOrDigit(character) ? character : '-');
    }

    var day = map.ExportedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    return $"cartographie-{name}-{day}.{extension}";
  }
}
