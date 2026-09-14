namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Le moteur de détection n'a pas rendu de rapport : la famille « <b>moteur de détection
/// indisponible</b> », et la seule qu'un <see cref="IScreeningEngine"/> lève sur une panne.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Une famille, et non une exception par cause.</b> Ollama injoignable, échéance dépassée ou
/// encodeur non conforme n'offrent pas à l'<c>Operator</c> des gestes différents : il réessaie plus
/// tard, ou il prévient l'exploitant. La cause fine est <b>réservée au journal</b>, où l'exploitant
/// en a besoin pour corriger la bonne chose.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne porte aucun texte venu du moteur</b> — ni message d'Ollama, ni adresse, ni digest —
/// et <b>aucune exception interne</b> : une <c>HttpRequestException</c> accrochée ici recopierait
/// l'adresse d'Ollama sur toute surface qui affiche une exception. Son message est écrit une fois,
/// dans la langue du contexte.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne se rattrape jamais par un autre moteur</b> (ADR-0025) : un rapport ne change pas de
/// moteur dans le dos de l'<c>Operator</c>. Elle se rattrape par le geste, qui ne produit aucun
/// <see cref="Screening"/> et le dit.
/// </para>
/// </remarks>
public sealed class ScreeningEngineUnavailable : Exception
{
  /// <summary>Le nom de la famille, tel que les écrans le disent.</summary>
  public const string FrenchLabel = "moteur de détection indisponible";

  /// <summary>
  /// Ce que la panne dit, écrit ici et nulle part ailleurs : le message de l'exception, et la phrase
  /// que les écrans reprennent.
  /// </summary>
  public const string Statement =
    "Moteur de détection indisponible : aucun rapport de détection n'a été produit.";

  /// <summary>
  /// ⚠️ <b>Le seul constructeur.</b> Ni message ni exception interne ne s'y passent : un constructeur
  /// qui les accepterait serait le chemin par lequel un texte d'Ollama finirait par traverser.
  /// </summary>
  public ScreeningEngineUnavailable()
    : base(Statement)
  {
  }
}
