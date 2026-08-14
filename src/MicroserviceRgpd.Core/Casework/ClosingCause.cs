namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Ce par quoi un <see cref="Case"/> s'est clos. Trois valeurs, <b>toujours signées par un humain</b>
/// — jamais par la machine seule : l'issue est le fait d'une personne nommée et datée.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aucune des trois ne promet qu'un droit a été honoré.</b> <see cref="Answered"/> atteste que le
/// service a répondu, et rien de plus : un dossier se clôt sur des <see cref="Step"/> restés
/// inatteints, et l'incomplétude reste lisible dans les <c>Step</c> plutôt que masquée par une cause
/// rassurante.
/// </para>
/// <para>
/// ⚠️ <b>Les noms écartés le sont pour cette raison exacte.</b> <c>Rejected</c> et <c>Cancelled</c>
/// diraient une issue défavorable à la personne là où <see cref="Abandoned"/> dit qu'on a cessé
/// d'instruire ; <c>OutOfScope</c> est un mot pris — c'est une valeur de <c>DataSubjectRight</c>,
/// donc du noyau partagé.
/// </para>
/// <para>
/// <b>Il n'existe aucune fonction d'effacement de <c>Case</c> distincte de la clôture.</b> Une
/// personne qui demande l'effacement de son dossier encore ouvert est servie par
/// <see cref="Abandoned"/>, et tout le nominatif tombe à l'instant : l'arbitrage réel — poursuivre
/// exige ses <c>Designations</c>, donc poursuivre ou effacer, jamais les deux — lui est posé
/// franchement plutôt que contourné par un second geste.
/// </para>
/// </remarks>
public sealed class ClosingCause : SmartEnum<ClosingCause>
{
  /// <summary>
  /// Le service a <b>répondu</b>. Dérivée de ce que les <see cref="Claim"/> portent — et elle
  /// n'affirme rien de plus que l'acte de répondre.
  /// </summary>
  public static readonly ClosingCause Answered =
    new(nameof(Answered), 0, "répondu", motiveIsDemanded: false);

  /// <summary>
  /// On a <b>cessé d'instruire</b> — la personne s'est ravisée, elle a demandé l'effacement de son
  /// dossier, ou plus rien ne permet d'avancer. <b>Non dérivable</b> : rien du dossier ne la dit, et
  /// c'est pourquoi un motif est exigé.
  /// </summary>
  public static readonly ClosingCause Abandoned =
    new(nameof(Abandoned), 1, "abandonné", motiveIsDemanded: true);

  /// <summary>
  /// La demande <b>n'exerçait aucun droit</b>. C'est le pendant, à la clôture, du <c>Case</c> ouvert
  /// sans aucun <see cref="Claim"/> : un dossier vide de réclamations est un fait, et il se clôt sous
  /// la signature d'un humain comme les autres.
  /// </summary>
  public static readonly ClosingCause NotApplicable =
    new(nameof(NotApplicable), 2, "sans objet", motiveIsDemanded: false);

  private ClosingCause(string name, int value, string frenchLabel, bool motiveIsDemanded)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
    MotiveIsDemanded = motiveIsDemanded;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>
  /// Cette cause <b>exige-t-elle un motif</b> de l'humain qui la nomme ? Vrai de
  /// <see cref="Abandoned"/>, et de rien d'autre.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Pourquoi <c>Abandoned</c>, et lui seul.</b> Les deux autres causes se relisent sur le dossier
  /// — les <see cref="Claim"/> répondus d'un côté, l'absence de <c>Claim</c> de l'autre — et un motif
  /// n'y ajouterait qu'une ligne de rien à chaque clôture, où le motif qui compte se noierait.
  /// <c>Abandoned</c>, lui, ne se relit nulle part : sans un mot, la preuve dirait qu'on a cessé
  /// d'instruire sans dire pourquoi, le jour même où tout le nominatif disparaît.
  /// </para>
  /// <para>
  /// <b>C'est du texte qui reste</b> : il est écrit à un point de décision, il dit
  /// <i>pourquoi on a décidé cela</i>, il n'est pas nominatif par nature — et il <b>survit</b>
  /// dans l'<c>EvidenceLog</c>, quand tout le reste du dossier tombe.
  /// </para>
  /// <para>
  /// ⚠️ <b>Ce n'est pas un blocage de la clôture.</b> Ce qui est exigé est un motif, jamais un état
  /// du dossier : aucun <c>Step</c> inachevé, aucun <c>Claim</c> ouvert ne barre jamais la route.
  /// </para>
  /// </remarks>
  public bool MotiveIsDemanded { get; }
}
