using System.Data;
using System.Data.Common;
using System.Reflection;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// Le catalogue <c>pg_catalog</c> de la <b>fixture PostgreSQL authentique</b>
/// (<c>exploration/releves-authentiques/pg-fixture.sql</c>), rendu ligne à ligne comme un pilote le
/// rendrait.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est ainsi que la fixture se rejoue sans conteneur.</b> Les faits par dialecte ont été
/// mesurés sur conteneur <b>hors du dépôt</b> (#275) et aucun test à conteneurs n'est ajouté ici :
/// ce qui reste éprouvable dans le processus, c'est tout ce qui vient <b>après</b> le pilote — la
/// renumérotation des positions, l'écriture du pivot, et le passage par l'ingestion. Le pilote
/// n'apporte, à ce niveau, rien de plus qu'un tableau de lignes.
/// </para>
/// <para>
/// ⚠️ <b>Les <c>attnum</c> portent le trou de la fixture, et c'est tout l'intérêt.</b>
/// <c>public.adherents</c> a perdu <c>a_supprimer</c> par un <c>DROP COLUMN</c> : son
/// <c>attnum</c> 2 ne reviendra jamais. Un relevé qui rendrait <c>attnum</c> tel quel se ferait
/// refuser par le pivot pour trou dans les positions — l'un des neuf refus, celui qui attrape la
/// troncature au milieu — sur une base parfaitement sincère qui a simplement vécu.
/// </para>
/// <para>
/// ⚠️ <b>Les lignes arrivent dans le désordre, exprès.</b> La requête embarquée ne porte aucun
/// <c>ORDER BY</c> : ce que PostgreSQL rend arrive dans l'ordre où il l'a trouvé, et c'est au C# de
/// ranger. Un jeu d'essai déjà trié ne prouverait que la patience de son auteur.
/// </para>
/// </remarks>
internal static class APostgreSqlCatalogue
{
  private static readonly object[][] Rows =
  [
    // public.cotisations — la table qui porte une clé étrangère.
    ["public", "cotisations", "adherent_id", (short)2, "bigint", "int8", true, "", "", "adherents"],
    ["public", "cotisations", "montant", (short)3, "numeric(10,2)", "numeric", true, "", "", ""],
    ["public", "cotisations", "id", (short)1, "bigint", "int8", false, "", "", ""],

    // audit.adherents — le même nom de table dans un second schéma.
    ["audit", "adherents", "quand", (short)3, "timestamp with time zone", "timestamptz", true, "", "", ""],
    ["audit", "adherents", "id", (short)1, "bigint", "int8", false, "", "", ""],
    ["audit", "adherents", "qui", (short)2, "text", "text", true, "", "", ""],

    // public.adherents — l'attnum 2 manque : a_supprimer a été droppée.
    ["public", "adherents", "id", (short)1, "bigint", "int8", false, "", "les adhérents", ""],
    ["public", "adherents", "adr_l1", (short)3, "character varying(255)", "varchar", true, "", "les adhérents", ""],
    ["public", "adherents", "courriel", (short)4, "text", "text", false, "adresse de contact", "les adhérents", ""],
    ["public", "adherents", "photo", (short)5, "bytea", "bytea", true, "", "les adhérents", ""],
    ["public", "adherents", "signature", (short)6, "bytea", "bytea", true, "", "les adhérents", ""],
    ["public", "adherents", "solde", (short)7, "numeric(12,2)", "numeric", true, "", "les adhérents", ""],
    ["public", "adherents", "cree_le", (short)8, "timestamp with time zone", "timestamptz", true, "", "les adhérents", ""],
    ["public", "adherents", "actif", (short)9, "boolean", "bool", true, "", "les adhérents", ""],
    ["public", "adherents", "jeton", (short)10, "uuid", "uuid", true, "", "les adhérents", ""],
    ["public", "adherents", "ip", (short)11, "inet", "inet", true, "", "les adhérents", ""],
    ["public", "adherents", "etiquettes", (short)12, "text[]", "_text", true, "", "les adhérents", ""],
    ["public", "adherents", "meta", (short)13, "jsonb", "jsonb", true, "", "les adhérents", ""],
    ["public", "adherents", "duree", (short)14, "interval", "interval", true, "", "les adhérents", ""],
    ["public", "adherents", "point_geo", (short)15, "point", "point", true, "", "les adhérents", ""],
  ];

  /// <summary>Le catalogue de la fixture, tel qu'un lecteur du pilote le rendrait.</summary>
  internal static DbDataReader Reader()
  {
    return Reader(Rows);
  }

  /// <summary>Un catalogue quelconque, pour les cas que la fixture ne porte pas.</summary>
  internal static DbDataReader Reader(IReadOnlyList<object[]> rows)
  {
    var table = new DataTable { Locale = System.Globalization.CultureInfo.InvariantCulture };

    table.Columns.Add("schema_nom", typeof(string));
    table.Columns.Add("table_nom", typeof(string));
    table.Columns.Add("colonne_nom", typeof(string));
    table.Columns.Add("attnum", typeof(short));
    table.Columns.Add("type_complet", typeof(string));
    table.Columns.Add("type_interne", typeof(string));
    table.Columns.Add("nullable", typeof(bool));
    table.Columns.Add("commentaire_colonne", typeof(string));
    table.Columns.Add("commentaire_table", typeof(string));
    table.Columns.Add("table_referencee", typeof(string));

    foreach (var row in rows)
    {
      table.Rows.Add(row);
    }

    return table.CreateDataReader();
  }
}

/// <summary>
/// La capture authentique d'un dialecte, lue depuis <c>exploration/releves-authentiques/</c> — la
/// source, jamais une copie.
/// </summary>
/// <remarks>
/// ⚠️ <b>Recopier la capture dans le test la ferait diverger de celle que le banc a laissée.</b> Le
/// test qui compare le relevé connecté à la capture ne prouverait alors plus que deux copies se
/// ressemblent.
/// </remarks>
internal static class TheAuthenticCapture
{
  internal static string For(string dialect)
  {
    var name = $"captures.{dialect}.txt";

    using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
      ?? throw new InvalidOperationException($"La ressource embarquée {name} est introuvable.");

    using var reader = new StreamReader(stream);

    return reader.ReadToEnd();
  }
}
