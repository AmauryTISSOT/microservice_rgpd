namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Pourquoi une colonne n'a <b>aucun</b> <see cref="ColumnPreview"/> à montrer. Énumération close à
/// <b>quatre familles</b>, et le nombre compte autant que la liste.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Quatre familles, et non une raison par panne rencontrée.</b> Le grain est celui du
/// <b>geste que l'<c>Operator</c> peut poser</b> : <see cref="AccessDenied"/> vit à part parce
/// qu'elle est la seule qu'il puisse corriger — il ira demander un accès. « Délai dépassé »,
/// « connexion tombée » et « scan interrompu » ne lui offrent rien de plus les unes que les autres
/// et se rangent ensemble dans <see cref="ReadFailed"/>. Une énumération qui grandirait au fil des
/// pannes de production serait, à chaque panne neuve, une valeur de plus que du code déjà écrit ne
/// sait pas afficher.
/// </para>
/// <para>
/// ⚠️ <b>Aucune absence n'est muette.</b> Sans raison nommée, « cette colonne ne contenait rien » et
/// « on n'a pas regardé cette colonne » se liraient pareil à l'écran, ce qui est l'<c>Omission
/// silencieuse</c> réintroduite par une cellule vide.
/// </para>
/// <para>
/// ⚠️ <b><see cref="Statement"/> ne cite jamais le message du pilote.</b> C'est par là que
/// remonteraient la valeur lue, l'hôte, l'utilisateur — la chaîne de connexion reconstituée par
/// morceaux, que <c>Rien de réel ne reste</c> interdit. La prose est <b>attachée au membre</b>,
/// écrite une fois : composée à l'écran, elle aurait divergé d'une surface à l'autre, et c'est le
/// jour de cette divergence qu'une phrase se met à dire un constat sur la donnée.
/// </para>
/// <para>
/// ⚠️ <b>Elle est <em>enregistrée</em> sur la <see cref="ScreenedColumn"/>, à la différence des
/// valeurs.</b> L'aperçu meurt avec la session d'arbitrage ; la raison, elle, descend en base —
/// sans quoi les quatre comptes de la <see cref="IncompletenessClause"/> seraient incalculables une
/// heure après le scan, et l'écran d'archive deviendrait <b>plus rassurant</b> que celui du jour
/// même. Ce n'est pas une entorse à <c>Rien de réel ne reste</c> : <b>une raison n'est pas une
/// valeur lue</b>.
/// </para>
/// </remarks>
public sealed class PreviewAbsenceReason : SmartEnum<PreviewAbsenceReason>
{
  /// <summary>
  /// Famille n° 1 — la colonne porte un type qu'on ne prélève pas. Un binaire, dont cinq valeurs ne
  /// diraient rien à un humain qui les regarde.
  /// </summary>
  public static readonly PreviewAbsenceReason UnsampleableType = new(
    nameof(UnsampleableType),
    1,
    "type non prélevable",
    "Cette colonne porte un type dont le service ne prélève aucune valeur : cinq valeurs binaires ne "
    + "diraient rien à qui les regarde.");

  /// <summary>
  /// Famille n° 2 — les droits ont été refusés. ⚠️ <b>Elle vit à part parce que c'est la seule que
  /// l'<c>Operator</c> puisse corriger</b> : elle l'envoie demander un accès, là où les trois autres
  /// ne lui font rien faire.
  /// </summary>
  public static readonly PreviewAbsenceReason AccessDenied = new(
    nameof(AccessDenied),
    2,
    "droits refusés",
    "Le compte de connexion n'a pas eu le droit de lire cette colonne.");

  /// <summary>
  /// Famille n° 3 — la lecture a réussi et n'a rendu aucune ligne.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La phrase dit l'observation, jamais un constat sur la donnée.</b> Deux situations la
  /// produisent, <b>indiscernables du dehors</b> : la table est réellement vide, ou elle est pleine
  /// et filtrée — sous une politique de sécurité au niveau ligne, un <c>SELECT</c> réussit, ne lève
  /// rien, et rend zéro ligne d'une table qui en porte des millions. Écrire « cette table ne
  /// contient aucune ligne » serait l'<c>Omission silencieuse</c> reconstituée <b>dans le champ créé
  /// pour l'empêcher</b>, sur la table <c>patients</c> d'un hôpital. Les deux reçoivent donc la même
  /// famille et la même phrase, et savoir les distinguer n'a plus d'usage.
  /// </remarks>
  public static readonly PreviewAbsenceReason NoValueReturned = new(
    nameof(NoValueReturned),
    3,
    "aucune valeur retournée",
    "La lecture de cette colonne n'a retourné aucune valeur.");

  /// <summary>
  /// Famille n° 4 — la lecture a échoué : délai dépassé, connexion tombée, scan interrompu. Les
  /// trois se rangent ensemble parce qu'aucune n'offre à l'<c>Operator</c> un geste que les autres
  /// ne lui offrent pas.
  /// </summary>
  public static readonly PreviewAbsenceReason ReadFailed = new(
    nameof(ReadFailed),
    4,
    "lecture échouée",
    "La lecture de cette colonne a échoué.");

  private PreviewAbsenceReason(string name, int value, string frenchLabel, string statement)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
    Statement = statement;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>
  /// Ce que l'<c>Operator</c> lit à la place de l'aperçu, en une phrase. Elle <b>dit ce qui a eu
  /// lieu</b>, jamais ce que la colonne contient — et elle ne cite jamais le message du pilote.
  /// </summary>
  public string Statement { get; }
}
