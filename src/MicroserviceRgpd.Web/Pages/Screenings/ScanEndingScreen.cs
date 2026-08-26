using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// Ce qu'un écran de <b>fin de scan</b> dit, quelle que soit la fin : <b>où</b> le scan s'est
/// arrêté, <b>d'où</b> vient la cause, et — sur la seule fin qui en appelle un — le <b>geste</b> à
/// poser. La relance, elle, est de toutes les fins et vit dans le bloc partagé.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Une seule forme pour toutes les fins, et c'est ce qui la rend lisible.</b> Un écran par
/// fin aurait dérivé au fil des tickets : l'un aurait perdu la phase, l'autre aurait oublié de dire
/// que le rapport courant n'a pas bougé, et un troisième aurait proposé de « reprendre » un scan
/// dont la chaîne de connexion n'a jamais survécu. Ici, ce qui est commun est écrit <b>une fois</b>,
/// et ce qui diffère tient dans les quatre champs ci-dessous.
/// </para>
/// <para>
/// ⚠️ <b>Aucun de ces champs ne peut porter la prose d'un pilote.</b> Ils sont bâtis par les
/// fabriques de ce type, à partir de <see cref="ScanPhase"/> et de <see cref="ScanFailureFamily"/> —
/// deux vocabulaires clos, écrits par le service. Un champ libre où l'on aurait pu recopier le
/// message d'une exception aurait été le morceau par lequel la chaîne de connexion se reconstitue :
/// l'hôte, l'utilisateur et le nom de base sont tous dans ce message-là.
/// </para>
/// <para>
/// ⚠️ <b>Un seul geste, et il n'appartient qu'à une fin.</b> Poser un geste sur chacune ferait de
/// toutes des choses à corriger — alors que « la base n'a aucune table » n'appelle rien du tout, et
/// que le dire serait envoyer l'<c>Operator</c> chercher un droit qui ne manque pas.
/// </para>
/// </remarks>
/// <param name="Title">Le titre de l'écran, qui nomme la fin.</param>
/// <param name="Where">Là où le scan s'est arrêté — la phase, ou ce qui en tient lieu.</param>
/// <param name="Cause">D'où vient la cause : la famille, dans les mots du service.</param>
/// <param name="Statement">La phrase que l'<c>Operator</c> lit, et qui dit ce qui s'est passé.</param>
/// <param name="Gesture">Le geste à poser, sur la seule fin qui en appelle un.</param>
public sealed record ScanEndingScreen(
  string Title,
  string Where,
  string Cause,
  string Statement,
  string? Gesture = null)
{
  /// <summary>
  /// L'<c>Operator</c> a arrêté ce scan lui-même. ⚠️ <b>Aucune famille de panne</b> : rien n'a raté,
  /// et lui en nommer une l'enverrait chercher un incident du réseau ou de la base qui n'a pas eu
  /// lieu.
  /// </summary>
  /// <param name="phase">La phase où le scan en était quand il a été coupé.</param>
  /// <exception cref="ArgumentNullException"><paramref name="phase"/> est absent.</exception>
  public static ScanEndingScreen Abandoned(ScanPhase phase)
  {
    ArgumentNullException.ThrowIfNull(phase);

    return new ScanEndingScreen(
      "Scan abandonné",
      $"en phase « {phase.FrenchLabel} »",
      "vous : vous avez arrêté ce scan",
      "Vous avez arrêté ce scan, et la lecture en cours sur la base a été coupée — pas seulement "
      + "la boucle qui l'entourait. Rien n'a été relevé à moitié.");
  }

  /// <summary>
  /// Le scan est tombé. ⚠️ <b>La phase et la famille, et rien d'autre</b> : ni message du pilote, ni
  /// hôte, ni utilisateur.
  /// </summary>
  /// <param name="failure">Où il est tombé, et de quel côté venait la cause.</param>
  /// <exception cref="ArgumentNullException"><paramref name="failure"/> est absent.</exception>
  public static ScanEndingScreen Failed(ScanFailure failure)
  {
    ArgumentNullException.ThrowIfNull(failure);

    return new ScanEndingScreen(
      "Scan échoué",
      $"en phase « {failure.Phase.FrenchLabel} »",
      failure.Family.FrenchLabel,
      failure.Family.Statement,
      // ⚠️ Seule la famille de ce que l'Operator a FOURNI appelle un geste de sa part : les deux
      // autres se règlent ailleurs, et lui demander d'agir sur le réseau ou sur la base d'un tiers
      // serait lui demander ce qu'il ne peut pas faire.
      failure.Family == ScanFailureFamily.Supplied
        ? "Vérifiez le SGBD choisi, la chaîne de connexion et les droits du compte, puis relancez."
        : null);
  }

  /// <summary>
  /// L'adresse nomme un scan que le processus ne connaît plus.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Cela se <i>dit</i>, cela ne se redirige pas.</b> Mener en silence au rapport courant
  /// aurait laissé l'<c>Operator</c> croire que c'est celui de son scan — et prendre pour un relevé
  /// d'aujourd'hui un rapport d'avant-hier.
  /// </remarks>
  public static ScanEndingScreen NoLongerKnown()
  {
    return new ScanEndingScreen(
      "Ce scan n'existe plus",
      "le service ne le sait plus : un scan ne survit pas à son redémarrage",
      "le service : il a redémarré, ou un autre scan a pris la place",
      "Ce scan n'existe plus — le service a redémarré, ou un autre scan a pris sa place. Le "
      + "service n'en retient qu'un à la fois, et aucun ne survit à l'arrêt du processus.");
  }

  /// <summary>
  /// La base a répondu, et elle ne porte aucune table.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Elle n'appelle aucun geste, et c'est ce qui la sépare de l'autre fin à zéro objet.</b>
  /// Le compte voit la base ; la base est vide. Suggérer de demander un accès enverrait
  /// l'<c>Operator</c> réclamer un droit qui ne lui manque pas.
  /// </remarks>
  public static ScanEndingScreen NoTable()
  {
    return new ScanEndingScreen(
      "Cette base ne porte aucune table",
      $"en phase « {ScanPhase.Cataloguing.FrenchLabel} »",
      "la base : elle a répondu, et son catalogue est vide",
      "La base a répondu, et elle ne porte aucune table. Rien n'a raté : il n'y avait rien à "
      + "relever. Aucun rapport de zéro colonne n'a été écrit — un rapport vide aurait renvoyé à "
      + "l'archive celui que vous aviez, et détruit des jours d'arbitrage pour une connexion "
      + "d'essai.");
  }

  /// <summary>
  /// La base est absente du catalogue de schémas : le compte de connexion ne voit rien.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est la seule des deux fins à zéro objet qui appelle un geste</b>, et l'écran le dit.
  /// Confondre les deux enverrait l'<c>Operator</c> chercher une base vide quand ce qu'il lui faut
  /// est demander un accès.
  /// </remarks>
  public static ScanEndingScreen NothingVisibleToThisAccount()
  {
    return new ScanEndingScreen(
      "Ce compte ne voit aucune base",
      $"en phase « {ScanPhase.Cataloguing.FrenchLabel} »",
      "ce qui a été fourni au service : la base est absente du catalogue de schémas",
      "La base ne figure pas au catalogue de schémas que ce compte présente au service. Ce n'est "
      + "pas une base vide : c'est un compte qui ne la voit pas. Le service relève le schéma tel "
      + "que ce compte le lui présente, et il ne peut ni détecter ni signaler qu'une partie lui a "
      + "été masquée.",
      "Demandez un accès en lecture sur l'ensemble de la base pour ce compte, puis relancez le "
      + "scan.");
  }

  /// <summary>
  /// L'écran de la fin qu'un <c>Scan</c> connu du processus a connue, ou <c>null</c> tant qu'il
  /// court.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La fin qui produit un relevé n'a pas d'écran, et n'en aura pas.</b> Elle se rend par un
  /// <c>303</c> vers le rapport : lui écrire un écran « le scan a réussi » ferait, entre le scan et
  /// son rapport, une page de plus à traverser pour ce que l'<c>Operator</c> attendait depuis le
  /// début.
  /// </remarks>
  /// <param name="snapshot">Où en est ce scan, pris d'un seul coup.</param>
  /// <exception cref="ArgumentNullException"><paramref name="snapshot"/> est absent.</exception>
  public static ScanEndingScreen? Of(ScanSnapshot snapshot)
  {
    ArgumentNullException.ThrowIfNull(snapshot);

    return snapshot.Ending switch
    {
      null => null,
      var ending when ending == ScanEnding.Listed => null,
      var ending when ending == ScanEnding.Abandoned => Abandoned(snapshot.Phase),
      var ending when ending == ScanEnding.NoTable => NoTable(),
      var ending when ending == ScanEnding.DatabaseAbsentFromCatalogue =>
        NothingVisibleToThisAccount(),
      // ⚠️ Un échec SANS sa phase et sa famille serait un écran qui ne dit rien : la ScanFailure
      // accompagne toujours cette fin, et son absence est une faute de programmation, pas un cas.
      _ => Failed(
        snapshot.Failure
        ?? throw new ArgumentException(
          "Un scan échoué sans sa phase ni sa famille n'est pas une fin que cet écran sait dire.",
          nameof(snapshot))),
    };
  }
}
