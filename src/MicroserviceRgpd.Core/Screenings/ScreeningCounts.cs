namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Les comptes d'un rapport, <b>recalculés à chaque rendu</b> : ceux que porte la
/// <see cref="IncompletenessClause"/>, ceux que l'écran affiche, et celui dont dépend le verrou.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ils existent comme type parce que l'écran d'une table ne charge pas le rapport.</b> Une
/// table de treize colonnes se lit sur le <c>DbSet</c> des <see cref="ScreenedColumn"/> — c'est
/// toute la raison de la seconde table — mais le verrou, la clause et les comptes portent sur le
/// rapport <b>entier</b> : sans eux, l'écran d'une table se lirait comme si la table était le
/// rapport. Les faire calculer par la base plutôt que par l'agrégat est la seule façon de les avoir
/// sans rematérialiser cinq mille lignes pour en montrer treize.
/// </para>
/// <para>
/// <b>Rien de tout cela n'est persisté.</b> Un compte en base aurait eu besoin de quelque chose pour
/// le mettre à jour, et ce quelque chose serait le processus de fond que ce dépôt interdit.
/// </para>
/// </remarks>
/// <param name="Columns">Combien de colonnes ce relevé porte, <b>toutes</b>.</param>
/// <param name="Tables">Combien de tables il couvre.</param>
/// <param name="ColumnsWithoutAComment">
/// Combien n'ont de commentaire ni à leur niveau ni à celui de leur table.
/// </param>
/// <param name="Flagged">
/// Combien le dépistage en a signalées. ⚠️ Le complément est ce qu'il <b>n'a pas vu</b>, jamais ce
/// qui serait inoffensif : le service n'a jamais vu une seule valeur.
/// </param>
/// <param name="Retained">Combien un humain a retenues, sous son nom.</param>
/// <param name="SetAside">Combien un humain a écartées, sous son nom.</param>
/// <param name="Awaiting">Combien attendent encore qu'un humain les tranche.</param>
/// <param name="RetainedOnUnflagged">
/// Combien un humain a retenues là où le dépistage n'avait <b>rien vu</b> — la mesure directe de ce
/// que l'<c>Omission relue</c> a rattrapé.
/// </param>
/// <param name="UnreadUnflagged">
/// Combien de colonnes où rien n'a été vu n'ont pas encore été relues — le compte du verrou.
/// </param>
public sealed record ScreeningCounts(
  int Columns,
  int Tables,
  int ColumnsWithoutAComment,
  int Flagged,
  int Retained,
  int SetAside,
  int Awaiting,
  int RetainedOnUnflagged,
  int UnreadUnflagged)
{
  /// <summary>
  /// Les comptes d'un rapport <b>chargé avec toutes ses colonnes</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Un rapport chargé sans ses colonnes rendrait ici des zéros sincères et faux</b>, et le
  /// pire d'entre eux est <see cref="UnreadUnflagged"/> : à zéro, le verrou déclare « toutes les
  /// colonnes ont été relues » sur un rapport que personne n'a ouvert — la surface rassurante contre
  /// laquelle ce verrou existe. Le rapport <b>déclare</b> combien de colonnes il porte, alors on le
  /// lui demande plutôt que de le documenter : un en-tête seul se refuse ici, bruyamment.
  /// <para>
  /// C'est pourquoi la lecture d'une table ne passe pas par ce chemin : elle ne charge jamais
  /// l'agrégat, et demande ses comptes à la base — voir <see cref="IScreenedColumns.CountsOfAsync"/>.
  /// </para>
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="screening"/> est absent.</exception>
  /// <exception cref="InvalidOperationException">
  /// <paramref name="screening"/> a été chargé sans ses colonnes.
  /// </exception>
  public static ScreeningCounts Of(Screening screening)
  {
    ArgumentNullException.ThrowIfNull(screening);

    if (screening.ColumnCount != screening.DeclaredColumnCount)
    {
      throw new InvalidOperationException(
        $"Ce dépistage déclare {screening.DeclaredColumnCount} colonnes et n'en porte que "
        + $"{screening.ColumnCount} : ses comptes seraient sincères et faux. Chargez-le avec ses "
        + "colonnes, ou demandez ses comptes à la base.");
    }

    return new ScreeningCounts(
      screening.ColumnCount,
      screening.TableCount,
      screening.ColumnsWithoutACommentCount,
      screening.FlaggedCount,
      screening.RetainedCount,
      screening.SetAsideCount,
      screening.AwaitingCount,
      screening.RetainedOnUnflaggedCount,
      screening.UnreadUnflaggedCount);
  }
}
