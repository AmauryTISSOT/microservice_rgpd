namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Le relevé que l'<c>Operator</c> a collé, <b>accepté en entier</b> : ce que son en-tête déclare, et
/// une <see cref="ListedColumn"/> par colonne dans l'ordre où la requête les a rendues.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ce type n'existe que s'il est entier.</b> Il n'a aucun constructeur public et le seul chemin
/// qui y mène est <see cref="ColumnListingIngestion.Ingest"/>, qui rend un relevé complet ou un
/// <see cref="ColumnListingRefusal"/> — jamais un relevé partiel. Un <c>Screening</c> bâti sur 99 %
/// d'un relevé se lirait comme complet, et les colonnes manquantes seraient précisément celles que
/// personne ne relirait jamais.
/// </para>
/// <para>
/// ⚠️ <b>Recopié tel quel, jamais vérifié ni complété.</b> <c>Enregistré, jamais vérifié</c> : le service
/// ne sait pas d'où vient ce relevé. Un relevé sincère, entier et bien formé, mais tiré de la base
/// de recette ou de celle d'hier, est <b>indiscernable du bon</b>. Aucun mécanisme n'attrape ce cas,
/// et aucun ne doit prétendre l'attraper — c'est pourquoi <see cref="Database"/> est recopié et
/// jamais recoupé avec quoi que ce soit.
/// </para>
/// <para>
/// ⚠️ <b><see cref="Dialect"/> vient de l'en-tête, jamais d'un choix de l'<c>Operator</c>.</b> La
/// requête porte son dialecte en dur : le pivot le déclare tout seul. Lui demander de le retaper
/// créerait deux sources de vérité sur le même fait, et ce recoupement n'attraperait de toute façon
/// pas le vrai danger, qui est de coller le pivot de la mauvaise base.
/// </para>
/// </remarks>
public sealed class ColumnListing
{
  /// <summary>
  /// La version de format que ce service sait lire, déclarée par l'en-tête du pivot. Une autre
  /// version est refusée plutôt que tentée : un pivot d'une forme future lu de travers rendrait un
  /// rapport qui se lit comme complet.
  /// </summary>
  public const string FormatVersion = "screening-pivot/1";

  /// <summary>
  /// Le plafond, <b>exprimé en colonnes et non en octets</b>, parce que c'est l'unité du domaine et
  /// la seule qui donne un refus lisible — « ton relevé annonce 31 000 colonnes, le plafond est
  /// 20 000 ». La borne est <b>incluse</b> : un relevé de 20 000 colonnes passe.
  /// <para>
  /// ⚠️ <b>Une contrainte d'ordonnancement le suit, et elle n'est pas un détail d'implémentation :
  /// la limite d'octets du corps de la requête HTTP doit être réglée au-dessus de ce que 20 000
  /// colonnes produisent</b>, sans quoi le refus muet du serveur sur la taille du corps sortirait
  /// avant le refus lisible, et l'<c>Operator</c> recevrait une erreur nue là où on avait écrit une
  /// phrase.
  /// </para>
  /// </summary>
  public const int MaxColumns = 20_000;

  internal ColumnListing(
    string dialect,
    string database,
    DateTimeOffset generatedOn,
    int declaredColumnCount,
    IReadOnlyList<ListedColumn> columns)
  {
    Dialect = dialect;
    Database = database;
    GeneratedOn = generatedOn;
    DeclaredColumnCount = declaredColumnCount;
    Columns = columns;
    Tables = GroupByTable(columns);
  }

  /// <summary>
  /// Le SGBD dont le relevé se déclare. Sans lui, « cette colonne n'a pas de commentaire » et « ce
  /// SGBD n'en rend jamais » se liraient pareil, ce qui est l'<c>Omission silencieuse</c> déplacée
  /// d'un cran ; avec lui, l'absence est <b>nommée</b>.
  /// </summary>
  public string Dialect { get; }

  /// <summary>
  /// Le nom de base que le SGBD a donné au relevé, <b>recopié sans jamais être vérifié</b>. C'est un
  /// repère pour l'humain qui relit un <c>Screening</c> trois jours plus tard, jamais une identité
  /// sur laquelle bâtir une comparaison de rapports.
  /// </summary>
  public string Database { get; }

  /// <summary>Quand la requête a produit ce relevé, tel que l'en-tête le dit. Recopié, jamais recoupé.</summary>
  public DateTimeOffset GeneratedOn { get; }

  /// <summary>
  /// Le nombre de colonnes que la ligne de fin <b>annonce</b>. Il est conservé à côté des lignes
  /// reçues parce que c'est leur égalité qui a rendu la troncature détectable, et parce que
  /// <see cref="Screening"/> le redemande.
  /// <para>
  /// Il est calculé dans la même requête que les lignes, jamais par un second passage sur le
  /// catalogue : c'est un contrôle d'intégrité <b>du collage</b>, pas de la base — deux lectures
  /// d'un catalogue vivant peuvent légitimement diverger.
  /// </para>
  /// </summary>
  public int DeclaredColumnCount { get; }

  /// <summary>
  /// Toutes les colonnes du relevé, <b>dans l'ordre où il les rend</b> — l'ordre du schéma, jamais
  /// l'alphabétique, sans quoi la colonne perd le voisinage qui la rend lisible : <c>adr_l1</c>,
  /// <c>adr_l2</c>, <c>cp</c>, <c>ville</c>.
  /// </summary>
  public IReadOnlyList<ListedColumn> Columns { get; }

  /// <summary>
  /// Les mêmes colonnes, <b>groupées par table</b>. C'est l'unité de travail de l'arbitrage : le
  /// commentaire de table éclaire toutes ses colonnes.
  /// <para>
  /// ⚠️ <b>Le groupement ne réordonne rien.</b> Les tables viennent dans l'ordre où le relevé les
  /// rencontre et leurs colonnes dans l'ordre du relevé : trier ici ferait de ce groupement une
  /// seconde vérité sur l'ordre, qui divergerait de <see cref="Columns"/> le jour où l'un des deux
  /// serait touché.
  /// </para>
  /// </summary>
  public IReadOnlyList<ListedTable> Tables { get; }

  /// <summary>Combien de colonnes ce relevé porte réellement. Égal à <see cref="DeclaredColumnCount"/>, ou le relevé n'existerait pas.</summary>
  public int ColumnCount => Columns.Count;

  /// <summary>Combien de tables distinctes il couvre.</summary>
  public int TableCount => Tables.Count;

  private static IReadOnlyList<ListedTable> GroupByTable(IReadOnlyList<ListedColumn> columns)
  {
    return
    [
      .. columns
        .GroupBy(column => column.Identity.TableIdentity)
        .Select(group => new ListedTable(group.Key, [.. group])),
    ];
  }
}

/// <summary>
/// Une table du relevé et ses colonnes, dans l'ordre du relevé. Elle existe parce que l'<c>Operator</c>
/// arbitre <b>une table à la fois</b>.
/// </summary>
/// <param name="Identity">Le couple schéma, table.</param>
/// <param name="Columns">Ses colonnes, dans l'ordre où le relevé les rend.</param>
public sealed record ListedTable(TableIdentity Identity, IReadOnlyList<ListedColumn> Columns);
