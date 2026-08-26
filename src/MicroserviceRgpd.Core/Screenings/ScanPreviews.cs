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
/// ⚠️ <b>La durée de vie est ici, et elle n'est pilotée que par le <see cref="TimeProvider"/>
/// injecté.</b> Deux heures <b>glissantes</b>, réarmées par le seul geste qui montre des aperçus —
/// <see cref="Show"/>, que l'écran d'une table appelle et que rien d'autre n'appelle —, sous un
/// plafond absolu de douze heures depuis le dépôt. Le glissant suit le rythme réel d'un arbitrage,
/// qui se compte en heures ; le plafond empêche qu'un onglet oublié fasse d'un cache une rétention.
/// </para>
/// <para>
/// ⚠️ <b>Ce n'est pas une couture de test, et il n'y en a pas à ouvrir.</b> Aucun test n'instancie
/// ce cache pour l'interroger : les écrans le traversent par la frontière HTTP, et l'horloge est le
/// seul levier — c'est-à-dire très exactement le levier dont l'exploitation dispose elle aussi. Une
/// méthode « faire expirer maintenant » aurait été un chemin que la production n'a pas.
/// </para>
/// </remarks>
public sealed class ScanPreviews(TimeProvider clock)
{
  /// <summary>
  /// La durée <b>glissante</b> : ce qu'il reste à vivre aux aperçus après chaque écran qui les
  /// montre. <b>Deux heures, parce qu'un arbitrage se compte en heures</b> — une pause déjeuner ne
  /// doit pas coûter les valeurs qui portent les motifs de forme.
  /// </summary>
  public static readonly TimeSpan SlidingLifetime = TimeSpan.FromHours(2);

  /// <summary>
  /// Le plafond <b>absolu</b> depuis le dépôt du jeu, que rien ne réarme. <b>Douze heures</b> : au
  /// delà, un onglet laissé ouvert ferait d'un cache une rétention, et la promesse « les valeurs
  /// meurent avec la session d'arbitrage » cesserait d'avoir une fin.
  /// </summary>
  public static readonly TimeSpan AbsoluteLifetime = TimeSpan.FromHours(12);

  private readonly Lock _turn = new();

  private ScreeningId? _of;

  private IReadOnlyDictionary<ColumnIdentity, ColumnPreview> _previews =
    new Dictionary<ColumnIdentity, ColumnPreview>();

  private DateTimeOffset _keptOn;

  private DateTimeOffset _lastShown;

  /// <summary>
  /// Dépose le jeu d'aperçus d'un rapport, et <b>évince celui d'avant</b>. C'est aussi l'instant
  /// d'où court le plafond absolu.
  /// </summary>
  /// <param name="of">Le rapport que ces aperçus accompagnent.</param>
  /// <param name="previews">Un aperçu par colonne du relevé.</param>
  /// <exception cref="ArgumentNullException"><paramref name="previews"/> est absent.</exception>
  public void Keep(ScreeningId of, IReadOnlyDictionary<ColumnIdentity, ColumnPreview> previews)
  {
    ArgumentNullException.ThrowIfNull(previews);

    var now = clock.GetUtcNow();

    lock (_turn)
    {
      _of = of;
      _previews = previews;
      _keptOn = now;
      _lastShown = now;
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
      _keptOn = default;
      _lastShown = default;
    }
  }

  /// <summary>
  /// Rend les aperçus d'un rapport <b>et réarme la durée glissante</b> — ou dit pourquoi il n'y en
  /// a pas.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Montrer <em>est</em> réarmer, et c'est pourquoi il n'existe qu'un seul geste.</b> Un
  /// couple « lire » / « prolonger » aurait laissé à chaque appelant le soin de décider s'il
  /// prolonge : « seuls les écrans qui montrent des aperçus réarment » aurait cessé d'être une
  /// propriété du code pour devenir une consigne. Le rapport, l'historique, l'archive et l'accueil
  /// ne prolongent donc rien parce qu'ils n'ont <b>rien à appeler</b>.
  /// </para>
  /// <para>
  /// ⚠️ <b>L'expiration efface les valeurs sur-le-champ, et retient le reste.</b> Le dictionnaire
  /// est lâché — les valeurs meurent pour de bon — mais le rapport qu'il accompagnait et l'instant
  /// du dépôt restent, sans quoi l'appel suivant ne saurait plus distinguer une durée écoulée d'un
  /// redémarrage, et l'écran annoncerait à l'<c>Operator</c> une panne qui n'a pas eu lieu.
  /// </para>
  /// </remarks>
  /// <param name="of">Le rapport dont l'écran montre les colonnes.</param>
  /// <param name="origin">
  /// Par quel chemin son relevé est entré. ⚠️ <b>Un relevé collé n'a rien à annoncer</b> : dire d'un
  /// rapport collé que ses aperçus ont expiré affirmerait qu'un prélèvement a eu lieu, exactement
  /// comme quatre comptes à zéro y inventeraient une incomplétude.
  /// </param>
  /// <exception cref="ArgumentNullException"><paramref name="origin"/> est absent.</exception>
  public ScreeningPreviews Show(ScreeningId of, ListingOrigin origin)
  {
    ArgumentNullException.ThrowIfNull(origin);

    if (origin != ListingOrigin.Scanned)
    {
      return ScreeningPreviews.NeverTaken;
    }

    var now = clock.GetUtcNow();

    lock (_turn)
    {
      // Le cache ne porte pas ce rapport : le processus a redémarré depuis son scan. Un autre
      // rapport ne peut pas être en cause — le courant est le dernier lancé, et le geste qui en
      // dépose un autre est très exactement celui qui évince ce jeu-ci.
      if (_of != of)
      {
        return ScreeningPreviews.ClearedByRestart;
      }

      var cap = _keptOn + AbsoluteLifetime;

      if (now >= cap || now >= _lastShown + SlidingLifetime)
      {
        _previews = new Dictionary<ColumnIdentity, ColumnPreview>();

        return ScreeningPreviews.Expired;
      }

      _lastShown = now;

      // ⚠️ Le décompte est borné par le plafond : les deux dernières heures d'un jeu déposé il y a
      // onze heures n'existent pas, et les annoncer aurait fait mentir la seule phrase de l'écran
      // dont l'Operator se sert pour décider s'il prend sa réunion maintenant.
      var until = now + SlidingLifetime < cap ? now + SlidingLifetime : cap;

      return ScreeningPreviews.Live(_previews, until - now);
    }
  }
}
