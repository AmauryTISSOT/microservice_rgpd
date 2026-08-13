using System.Reflection;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// Les requêtes de relevé que le service fournit à l'<c>Operator</c>, une par SGBD.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Une seule source, et ce sont les fichiers du dépôt.</b> Ce sont les mêmes requêtes que
/// celles qui ont extrait le corpus, embarquées depuis <c>releves/</c> plutôt que recopiées : deux
/// exemplaires auraient divergé au premier correctif, et l'écran aurait donné une requête dont le
/// format pivot n'a jamais été éprouvé.
/// </para>
/// <para>
/// <b>La requête produit elle-même le pivot</b> plutôt que de laisser un client SQL le mettre en
/// forme : c'est ce qui lui permet de déclarer le SGBD dont elle vient et le nombre de colonnes
/// qu'elle porte, et c'est ce qui rend une <b>troncature au collage détectable</b>. Sans elle, un
/// relevé amputé de trois colonnes se lirait comme entier.
/// </para>
/// <para>
/// <b>Le service ne se connecte à rien.</b> L'<c>Operator</c> exécute cette requête chez lui, dans
/// son propre client, et colle ce qu'elle rend : il n'existe aucune chaîne de connexion, et aucun
/// secret d'accès à la base du client n'entre dans ce service.
/// </para>
/// </remarks>
public sealed record ListingQuery(string Dialect, string FrenchLabel, string Sql)
{
  /// <summary>
  /// Les trois SGBD servis, dans l'ordre où l'écran les propose. ⚠️ <b>Le dialecte est le mot que
  /// le pivot déclarera</b> — c'est le même vocabulaire des deux côtés, et non deux listes à tenir
  /// d'accord.
  /// </summary>
  private static readonly (string Dialect, string Label, string Resource)[] Served =
  [
    ("postgresql", "PostgreSQL", "Releves.postgresql.sql"),
    ("mariadb", "MariaDB / MySQL", "Releves.mariadb.sql"),
    ("sqlite", "SQLite", "Releves.sqlite.sql"),
  ];

  /// <summary>
  /// Les requêtes, chargées une fois. Elles sont gelées dans l'assemblage : rien ne les relit, et
  /// rien ne les modifie en cours de route.
  /// </summary>
  public static IReadOnlyList<ListingQuery> All { get; } =
  [
    .. Served.Select(served => new ListingQuery(served.Dialect, served.Label, Read(served.Resource))),
  ];

  /// <summary>Le dialecte proposé par défaut, faute d'un moyen honnête de deviner celui du client.</summary>
  public static ListingQuery Default => All[0];

  /// <summary>La requête d'un dialecte, ou celle par défaut lorsque le mot n'en désigne aucune.</summary>
  /// <remarks>
  /// ⚠️ <b>Un dialecte inconnu ne lève pas ici</b>, et n'a pas à le faire : ce champ ne décide de
  /// rien d'autre que du texte affiché à côté du collage. Ce qui compte est le dialecte que le
  /// <b>pivot</b> déclare, et celui-là est éprouvé par l'ingestion, où le refus est en bloc.
  /// </remarks>
  public static ListingQuery For(string? dialect)
  {
    return All.FirstOrDefault(query => query.Dialect == dialect) ?? Default;
  }

  /// <summary>La version du format que ces requêtes produisent, telle que le domaine la nomme.</summary>
  public static string FormatVersion => ColumnListing.FormatVersion;

  private static string Read(string resource)
  {
    using var stream = typeof(ListingQuery).GetTypeInfo().Assembly.GetManifestResourceStream(resource)
      ?? throw new InvalidOperationException(
        $"La requête de relevé « {resource} » n'est pas dans l'assemblage. L'écran de dépôt ne peut "
        + "pas demander à l'Operator de coller un relevé sans lui fournir la requête qui le produit.");

    using var reader = new StreamReader(stream);

    return reader.ReadToEnd();
  }
}
