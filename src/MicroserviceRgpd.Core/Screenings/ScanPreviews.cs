namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Les <see cref="ColumnPreview"/> du rapport courant, <b>en mémoire du processus</b> : un seul jeu
/// vivant à la fois, l'ancien évincé quand le suivant se dépose.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est le seul endroit du service où une valeur lue se pose, et elle n'y descend pas.</b>
/// Rien ici n'est persisté : le processus redémarré, les aperçus n'existent plus, et le rapport
/// reste entier — les <b>raisons</b> d'absence, elles, sont sur la ligne enregistrée, et c'est ce
/// qui laisse la clause d'incomplétude calculable une heure après le scan.
/// </para>
/// <para>
/// ⚠️ <b>Un seul jeu, et l'éviction est immédiate.</b> Un rapport qui part à l'archive n'a plus
/// d'écran qui montre ses aperçus : les garder ferait du cache une rétention, alors que le geste
/// qui archive est très exactement celui qui dépose le jeu suivant.
/// </para>
/// <para>
/// ⚠️ <b>Aucun plafond en octets, et c'est délibéré.</b> Un seul jeu vivant à la fois pèse
/// 5 × 4 980 × 254 ≈ 6 Mo au pire cas du corpus. Un plafond aurait exigé d'inventer une cinquième
/// raison nommée pour les lignes sacrifiées — c'est-à-dire d'ajouter une famille d'absence dont
/// personne n'a besoin.
/// </para>
/// <para>
/// ⚠️ <b>L'expiration glissante et l'affichage ne sont pas ici.</b> Les deux heures réarmées, le
/// plafond de douze heures et le bloc qui quitte l'écran viennent avec le ticket des aperçus ; ce
/// qui est tenu ici est l'unicité du jeu et l'éviction, sans lesquelles ce ticket-là n'aurait rien
/// où poser sa durée.
/// </para>
/// </remarks>
public sealed class ScanPreviews
{
  private readonly Lock _turn = new();

  private ScreeningId? _of;

  private IReadOnlyDictionary<ColumnIdentity, ColumnPreview> _previews =
    new Dictionary<ColumnIdentity, ColumnPreview>();

  /// <summary>
  /// Dépose le jeu d'aperçus d'un rapport, et <b>évince celui d'avant</b>.
  /// </summary>
  /// <param name="of">Le rapport que ces aperçus accompagnent.</param>
  /// <param name="previews">Un aperçu par colonne du relevé.</param>
  /// <exception cref="ArgumentNullException"><paramref name="previews"/> est absent.</exception>
  public void Keep(ScreeningId of, IReadOnlyDictionary<ColumnIdentity, ColumnPreview> previews)
  {
    ArgumentNullException.ThrowIfNull(previews);

    lock (_turn)
    {
      _of = of;
      _previews = previews;
    }
  }

  /// <summary>
  /// Évince le jeu vivant <b>sans en déposer un autre</b> : le rapport qu'il accompagnait vient de
  /// partir à l'archive, et rien n'a été lu pour celui qui le remplace.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est le chemin collé qui appelle ceci, et c'est le seul.</b> Un collage archive le
  /// rapport courant exactement comme un scan le fait ; sans cette ligne, les aperçus d'un rapport
  /// scanné — des valeurs réelles du client — resteraient en mémoire du processus <b>indéfiniment</b>
  /// derrière un rapport que plus aucun écran ne montre. « L'éviction est immédiate » ne peut pas
  /// dépendre de la voie par laquelle le rapport suivant est arrivé.
  /// </remarks>
  public void Forget()
  {
    lock (_turn)
    {
      _of = null;
      _previews = new Dictionary<ColumnIdentity, ColumnPreview>();
    }
  }

  /// <summary>
  /// Les aperçus de ce rapport, ou <b>rien</b> — parce qu'un autre rapport les a évincés, ou parce
  /// que le processus a redémarré.
  /// </summary>
  /// <param name="of">Le rapport dont on cherche les aperçus.</param>
  public IReadOnlyDictionary<ColumnIdentity, ColumnPreview> Of(ScreeningId of)
  {
    lock (_turn)
    {
      return _of == of ? _previews : new Dictionary<ColumnIdentity, ColumnPreview>();
    }
  }
}
