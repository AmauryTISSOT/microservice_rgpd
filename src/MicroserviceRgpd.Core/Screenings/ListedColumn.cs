namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Une ligne du <c>ColumnListing</c>, <b>recopiée telle quelle</b> : le triplet qui la nomme, son
/// rang dans le schéma, et les cinq champs que le relevé porte à côté.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le service ne vérifie ni ne complète rien.</b> <c>Enregistré, jamais vérifié</c> : il ne sait pas
/// d'où vient ce relevé, et un relevé sincère mais tiré de la base de recette est indiscernable du
/// bon. Aucun mécanisme n'attrape ce cas, et aucun ne doit prétendre l'attraper.
/// </para>
/// <para>
/// ⚠️ <b>Une absence est <c>null</c>, et elle est structurelle plus souvent qu'accidentelle.</b> Un
/// champ qu'un SGBD ne sait pas produire arrive vide — SQLite ne rend aucun commentaire, et sept des
/// dix pivots du corpus n'en rendent aucun. C'est pourquoi la <see cref="IncompletenessClause"/>
/// porte les commentaires <b>sous condition</b> : « lorsque le SGBD en rend et lorsqu'ils
/// existent ».
/// </para>
/// <para>
/// ⚠️ <b>Les cinq champs sont gardés sur la ligne alors même qu'ils entrent « comme <i>filtre</i>,
/// jamais comme <i>signal</i> ».</b> Trois raisons : l'unité de travail de l'écran est la table
/// <b>parce que</b> <see cref="TableComment"/> éclaire toutes ses colonnes ; un humain qui arbitre
/// <c>livret_modaccomp_code</c> juge sur le type autant que sur le nom ; et les comptes de la
/// <see cref="IncompletenessClause"/> deviennent calculables à tout moment plutôt que figés à
/// l'ingestion.
/// </para>
/// </remarks>
public sealed record ListedColumn
{
  private ListedColumn(
    ColumnIdentity identity,
    int position,
    string? dataType,
    bool? isNullable,
    string? columnComment,
    string? tableComment,
    string? referencedTable)
  {
    Identity = identity;
    Position = position;
    DataType = dataType;
    IsNullable = isNullable;
    ColumnComment = columnComment;
    TableComment = tableComment;
    ReferencedTable = referencedTable;
  }

  /// <summary>
  /// Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun
  /// invariant, et il est <b>indispensable</b> : le triplet est un type possédé, et le liage par
  /// constructeur d'EF Core ne sait pas alimenter autre chose qu'un champ simple.
  /// </summary>
  private ListedColumn()
  {
    Identity = null!;
  }

  /// <summary>Le triplet schéma / table / colonne.</summary>
  public ColumnIdentity Identity { get; }

  /// <summary>
  /// Le rang de la colonne <b>tel que le relevé le rend</b>, et c'est lui qui ordonne l'écran :
  /// l'ordre du schéma, jamais l'alphabétique, sans quoi la colonne perd le voisinage qui la rend
  /// lisible — <c>adr_l1</c>, <c>adr_l2</c>, <c>cp</c>, <c>ville</c>.
  /// </summary>
  public int Position { get; }

  /// <summary>Le type déclaré, ou <c>null</c>. Lu <b>seulement pour écarter</b>, jamais comme indice de sens.</summary>
  public string? DataType { get; }

  /// <summary>La nullabilité, ou <c>null</c> si le relevé ne la porte pas. Lue seulement pour écarter.</summary>
  public bool? IsNullable { get; }

  /// <summary>Le commentaire de colonne, ou <c>null</c> — et <c>null</c> est le régime majoritaire.</summary>
  public string? ColumnComment { get; }

  /// <summary>
  /// Le commentaire de la table, recopié sur chacune de ses colonnes. Il est ce qui fait de la table
  /// l'unité de travail de l'arbitrage.
  /// </summary>
  public string? TableComment { get; }

  /// <summary>
  /// La table que cette colonne référence, ou <c>null</c>. Lue <b>seulement pour écarter</b> : une
  /// clé vers une table de référence exclut des catégories plutôt qu'elle n'en désigne une.
  /// <para>
  /// ⚠️ <b>La colonne pointée n'est pas recopiée, seulement la table.</b> <c>clients.id</c>
  /// n'apprend rien que <c>clients</c> n'ait déjà dit, et la garder ferait croire que le relevé
  /// porte la contrainte entière.
  /// </para>
  /// </summary>
  public string? ReferencedTable { get; }

  /// <summary>Cette ligne porte-t-elle un commentaire, à un niveau ou à l'autre ?</summary>
  public bool CarriesAComment => ColumnComment is not null || TableComment is not null;

  /// <summary>
  /// Recopie une ligne du relevé, ou refuse. Le refus est une <b>programmation fautive</b> : un
  /// relevé mal formé se refuse en bloc à l'ingestion, et jamais ligne par ligne.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="identity"/> est absent.</exception>
  /// <exception cref="ArgumentOutOfRangeException">Le rang est négatif.</exception>
  public static ListedColumn Of(
    ColumnIdentity identity,
    int position,
    string? dataType = null,
    bool? isNullable = null,
    string? columnComment = null,
    string? tableComment = null,
    string? referencedTable = null)
  {
    ArgumentNullException.ThrowIfNull(identity);
    ArgumentOutOfRangeException.ThrowIfNegative(position);

    return new ListedColumn(
      identity,
      position,
      ScreeningText.OrAbsent(dataType),
      isNullable,
      ScreeningText.OrAbsent(columnComment),
      ScreeningText.OrAbsent(tableComment),
      ScreeningText.OrAbsent(referencedTable));
  }
}
