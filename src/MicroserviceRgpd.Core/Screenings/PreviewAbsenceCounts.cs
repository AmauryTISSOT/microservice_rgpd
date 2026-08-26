namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Combien de colonnes n'ont <b>aucun aperçu</b>, et pourquoi — un compte par famille de
/// <see cref="PreviewAbsenceReason"/>, <b>zéros compris</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Quatre comptes plutôt qu'un seul, et l'agrégat est refusé nommément.</b> « 412 colonnes
/// sans aperçu » ne fait rien faire à personne ; « 18 par droits refusés » envoie l'<c>Operator</c>
/// demander un accès à son DBA et rescanner. Agréger, c'est éteindre la seule des quatre familles
/// qui appelle un geste — et c'est très exactement le motif qui a fait vivre
/// <see cref="PreviewAbsenceReason.AccessDenied"/> à part.
/// </para>
/// <para>
/// ⚠️ <b>Les zéros s'affichent</b>, pour la raison exacte qui interdit un cas spécial au seuil zéro
/// dans le reste de la <see cref="IncompletenessClause"/> : un énoncé particulier quand il n'y a
/// rien à dire rétablirait la réassurance un cran plus haut, là où elle est plus difficile à voir.
/// </para>
/// <para>
/// ⚠️ <b>Sur le chemin collé, ce n'est pas <see cref="None"/> qui s'affiche : rien ne s'affiche.</b>
/// Rendre « 0 par droits refusés » sur un relevé collé affirmerait qu'un prélèvement a eu lieu et
/// n'a rien refusé — une incomplétude inventée là où il n'y en a pas. C'est
/// <see cref="ReadPerimeter.ColumnsWithoutAPreview"/> qui porte cette absence, en valant
/// <c>null</c>.
/// </para>
/// <para>
/// <b>Quatre entiers plutôt qu'un dictionnaire</b> : <see cref="ScreeningCounts"/> est un record
/// dont l'égalité entière est ce qui tient le compte calculé par la base au compte calculé par
/// l'agrégat, et un dictionnaire s'y serait comparé par référence.
/// </para>
/// </remarks>
/// <param name="UnsampleableType">Combien portent un type dont le service ne prélève rien.</param>
/// <param name="AccessDenied">
/// Combien le compte de connexion n'a pas eu le droit de lire. <b>C'est le seul des quatre qui
/// appelle un geste</b>, et c'est pourquoi il ne se fond dans aucun autre.
/// </param>
/// <param name="NoValueReturned">Combien n'ont retourné aucune valeur — table vide ou table filtrée, indiscernables.</param>
/// <param name="ReadFailed">Combien ont échoué à la lecture : délai dépassé, connexion tombée, scan interrompu.</param>
public sealed record PreviewAbsenceCounts(
  int UnsampleableType,
  int AccessDenied,
  int NoValueReturned,
  int ReadFailed)
{
  /// <summary>Aucune colonne sans aperçu, dans aucune des quatre familles.</summary>
  /// <remarks>
  /// ⚠️ <b>Ce n'est pas ce que rend le chemin collé</b> — voir le troisième avertissement du type.
  /// C'est ce que rend un scan dont <b>tous</b> les prélèvements ont abouti.
  /// </remarks>
  public static PreviewAbsenceCounts None { get; } = new(0, 0, 0, 0);

  /// <summary>
  /// Combien de colonnes n'ont aucun aperçu, familles confondues. <b>Il ne s'affiche jamais seul</b>
  /// : c'est l'agrégat que le type refuse, et il ne sert qu'à savoir si <b>aucun</b> aperçu n'a
  /// abouti.
  /// </summary>
  public int Total => UnsampleableType + AccessDenied + NoValueReturned + ReadFailed;

  /// <summary>
  /// Les quatre comptes <b>attachés à leur famille</b>, dans l'ordre de l'énumération — c'est sous
  /// cette forme que l'écran les rend, chacun avec le libellé que son membre porte.
  /// </summary>
  /// <remarks>
  /// Elle se calcule et n'entre donc pas dans l'égalité du record, qui reste celle des quatre
  /// entiers.
  /// </remarks>
  public IReadOnlyList<PreviewAbsenceCount> ByReason =>
  [
    new PreviewAbsenceCount(PreviewAbsenceReason.UnsampleableType, UnsampleableType),
    new PreviewAbsenceCount(PreviewAbsenceReason.AccessDenied, AccessDenied),
    new PreviewAbsenceCount(PreviewAbsenceReason.NoValueReturned, NoValueReturned),
    new PreviewAbsenceCount(PreviewAbsenceReason.ReadFailed, ReadFailed),
  ];

  /// <summary>
  /// Les quatre comptes d'un ensemble de colonnes, dont celles qui portent un aperçu — leur raison
  /// est absente, et elles ne comptent nulle part.
  /// </summary>
  /// <param name="reasons">La raison d'absence d'aperçu de chaque colonne, ou <c>null</c> quand elle en a un.</param>
  /// <exception cref="ArgumentNullException"><paramref name="reasons"/> est absent.</exception>
  public static PreviewAbsenceCounts Of(IEnumerable<PreviewAbsenceReason?> reasons)
  {
    ArgumentNullException.ThrowIfNull(reasons);

    var counted = reasons.ToList();

    return new PreviewAbsenceCounts(
      counted.Count(reason => reason == PreviewAbsenceReason.UnsampleableType),
      counted.Count(reason => reason == PreviewAbsenceReason.AccessDenied),
      counted.Count(reason => reason == PreviewAbsenceReason.NoValueReturned),
      counted.Count(reason => reason == PreviewAbsenceReason.ReadFailed));
  }
}

/// <summary>
/// <b>Un</b> des quatre comptes, et la famille dont il parle. Le compte n'est jamais rendu sans
/// elle : c'est le libellé du membre qui dit à l'<c>Operator</c> s'il a un geste à poser.
/// </summary>
/// <param name="Reason">La famille comptée.</param>
/// <param name="Columns">Combien de colonnes en relèvent, <b>zéro compris</b>.</param>
public sealed record PreviewAbsenceCount(PreviewAbsenceReason Reason, int Columns);
