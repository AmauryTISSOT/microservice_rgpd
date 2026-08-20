using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.UseCases.Qualifications.Qualify;

namespace MicroserviceRgpd.Web.Pages.Qualifications;

/// <summary>
/// Ce que l'écran <b>met sous les yeux</b> de l'<c>Operator</c> d'une qualification rendue : cinq
/// textes français, et rien du domaine.
/// </summary>
/// <remarks>
/// <para>
/// <b>La mise en français vit ici, pas dans le gabarit.</b> Une vue qui aurait tenu un
/// <c>DataSubjectRight</c> ou un <c>ReviewSignal</c> aurait mis du domaine dans un fichier que rien
/// ne relit, et la moindre phrase s'y serait écrite deux fois — une par branche du gabarit.
/// </para>
/// <para>
/// ⚠️ <b>Les libellés des droits sont ceux de la taxonomie, jamais réécrits.</b> Une seule source
/// de vérité : le droit de l'art. 17 se dit « droit à l'effacement » parce que
/// <see cref="DataSubjectRight.Erasure"/> le dit, et un second mot pour le même droit ferait
/// diverger l'écran du contrat public le jour où l'un des deux bouge.
/// </para>
/// </remarks>
/// <param name="Rights">
/// Les droits que le texte est jugé exercer, sous leur libellé français — ou le <b>verdict nommé</b>
/// qui dit qu'aucun n'a été reconnu.
/// </param>
/// <param name="ReviewUrgency">Ce que le service dit de l'urgence à relire ce verdict.</param>
/// <param name="Wholeness">L'état d'entièreté du service au moment où il a qualifié.</param>
/// <param name="Justification">La phrase du moteur, ou <c>null</c> quand il n'en a donné aucune.</param>
/// <param name="Identifier">L'identifiant de la qualification, et de la trace qu'elle a gravée.</param>
public sealed record Verdict(
  string Rights,
  string ReviewUrgency,
  string Wholeness,
  string? Justification,
  string Identifier)
{
  /// <summary>Ce qu'une qualification rendue donne à lire.</summary>
  public static Verdict Of(QualificationOutcome outcome)
  {
    ArgumentNullException.ThrowIfNull(outcome);

    return new Verdict(
      RightsOf(outcome.Qualification),
      UrgencyOf(outcome.ReviewSignal),
      WholenessOf(outcome.Degraded),
      outcome.Justification,
      outcome.QualificationId.ToString());
  }

  /// <summary>
  /// Les droits, dans l'ordre de la taxonomie — <b>une <c>Qualification</c> est un ensemble</b>,
  /// jamais un droit unique, et l'écran les rend tous.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>« Aucun droit reconnu » est un verdict nommé</b>, jamais une case vide : le service ne
  /// confond pas « je n'y reconnais aucun droit » avec « il ne s'est rien passé ». Le nom vient de
  /// la taxonomie, et la phrase qui le suit dit ce qu'il veut dire à qui ne connaît pas le mot.
  /// <para>
  /// <b>Le dépliant rend les avis des moteurs par cette même méthode</b> : les droits qu'un moteur a
  /// reconnus sont des droits, et une seconde mise en français les aurait fait diverger de celle du
  /// verdict — jusqu'à nommer autrement, sous le dépliant, ce qui est nommé au-dessus.
  /// </para>
  /// </remarks>
  internal static string RightsOf(Qualification qualification)
  {
    var rights = qualification.Rights.OrderBy(right => right.Value).ToArray();

    return rights is [var only] && only.Equals(DataSubjectRight.OutOfScope)
      ? $"{only.FrenchLabel} — aucun droit reconnu"
      : string.Join(", ", rights.Select(right => right.FrenchLabel));
  }

  /// <summary>
  /// Ce que le signal de relecture dit à l'humain. <b>Il priorise la relecture, il ne la déclenche
  /// pas</b> : un humain valide de toute façon chaque qualification.
  /// </summary>
  private static string UrgencyOf(ReviewSignal signal)
  {
    return signal switch
    {
      ReviewSignal.Corroborated =>
        "Corroborée — les deux moteurs s'accordent, et celui dont l'avis fait verdict se dit sûr.",
      ReviewSignal.Contested =>
        "Contestée — les deux moteurs ne disent pas la même chose. À relire en premier.",
      ReviewSignal.NeedsReview =>
        "À relire — le verdict n'a reçu aucun contrôle indépendant assorti d'une confiance haute.",
      _ => throw new ArgumentOutOfRangeException(
        nameof(signal),
        signal,
        "Un signal de relecture que l'écran ne sait pas dire ne doit pas se rendre en silence."),
    };
  }

  /// <summary>
  /// L'état d'entièreté du service, dit en clair. Le <c>Mode dégradé</c> n'est pas une panne : un
  /// déploiement configuré sans moteur LLM qualifie ainsi à chaque appel, et l'<c>Operator</c> a le
  /// droit de le savoir en lisant son verdict.
  /// </summary>
  private static string WholenessOf(bool degraded)
  {
    return degraded
      ? "Service non entier — l'un des deux moteurs n'a rendu aucun avis, et le verdict n'a donc "
        + "pas été confronté."
      : "Service entier — les deux moteurs ont rendu leur avis.";
  }
}
