namespace MicroserviceRgpd.Core.Qualifications;

/// <summary>
/// Ce que le service dit à l'opérateur humain de l'urgence à relire la qualification. Il priorise
/// la relecture, il ne la déclenche pas — un humain valide de toute façon chaque qualification.
/// <para>
/// La définition est <b>sémantique, jamais mécanique</b> : elle ne parle ni de LLM, ni de lexique,
/// ni de deux moteurs. Un troisième moteur, ou l'abandon du lexique, laisse le contrat intact.
/// </para>
/// <para>
/// <b>Exactement trois valeurs.</b> En ajouter une est une rupture du contrat public — et une
/// quatrième valeur devrait acheter une action différente, ce qu'aucune n'achète ici.
/// </para>
/// </summary>
public enum ReviewSignal
{
  /// <summary>
  /// Le verdict a reçu un contrôle indépendant favorable <b>et</b> la confiance était haute.
  /// Inatteignable en mode dégradé.
  /// </summary>
  Corroborated,

  /// <summary>
  /// Ni l'un ni l'autre pleinement : accord des moteurs sans confiance haute, ou contrôle
  /// impossible faute d'un moteur. C'est le seul signal possible en mode dégradé.
  /// </summary>
  NeedsReview,

  /// <summary>
  /// Le contrôle indépendant est en désaccord. Signal le plus fort, parce qu'il est le seul à ne
  /// rien devoir à l'auto-évaluation du moteur principal.
  /// </summary>
  Contested,
}
