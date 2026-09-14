namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Ce qu'un <see cref="Screening"/> dit d'<b>une</b> colonne du relevé : sa
/// <see cref="PersonalDataCategory"/>, la <see cref="RuleStrength"/> de la règle qui l'a produite,
/// un motif en prose française, et son état d'arbitrage.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il y en a une par colonne du relevé, sans exception</b> — y compris là où le service n'a
/// rien vu. Ce n'est pas un détail de présentation : c'est le mécanisme entier de l'<c>Omission
/// relue</c>. Une colonne absente du <see cref="Screening"/> serait une colonne que personne ne relit
/// jamais.
/// </para>
/// <para>
/// <b>L'invariant du contexte est porté par les deux fabriques, et non par un garde qu'on pourrait
/// oublier d'appeler</b> : <see cref="Flagged"/> exige un motif et refuse
/// <see cref="PersonalDataCategory.Unflagged"/> ; <see cref="NothingSeen"/> n'en accepte aucun. Il
/// n'existe donc aucun chemin qui produise une ligne signalée sans motif, ni une ligne
/// <c>Unflagged</c> qui en porte un — <b>motif présent ⇔ ce n'est pas <c>Unflagged</c></b>, et c'est
/// ce qui sépare « rien vu » de « vu et écarté ».
/// </para>
/// <para>
/// <b>Le motif est du texte qui meurt, lu tel quel et jamais analysé.</b>
/// « <c>adr_l1</c> → <c>ContactDetails</c>, degré bas, motif : préfixe <c>adr</c> reconnu »
/// s'arbitre ; « <c>ContactDetails</c>, 0,72 » ne s'arbitre pas.
/// </para>
/// <para>
/// <b>Elle n'est pas un agrégat.</b> Elle naît et se modifie par la racine — mais elle reçoit, elle,
/// son propre <c>DbSet</c>, délibérément, parce qu'un <see cref="Screening"/> en a cinq mille et
/// que l'écran n'ouvre qu'une table à la fois.
/// </para>
/// </remarks>
public sealed class ScreenedColumn
{
  /// <summary>
  /// Le plafond de la prose de motif, en unités UTF-16. Large : c'est une explication qu'un humain
  /// doit pouvoir juger, pas une étiquette.
  /// </summary>
  public const int MaxReasonLength = 2000;

  private ScreenedColumn(
    ListedColumn listed,
    PersonalDataCategory category,
    RuleStrength? strength,
    string? reason,
    PreviewAbsenceReason? previewAbsence)
  {
    Listed = listed;
    Category = category;
    Strength = strength;
    Reason = reason;
    PreviewAbsence = previewAbsence;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private ScreenedColumn()
  {
    Listed = null!;
    Category = PersonalDataCategory.Unflagged;
  }

  /// <summary>La ligne du relevé, recopiée telle quelle.</summary>
  public ListedColumn Listed { get; private set; }

  /// <summary>Ce que la détection a reconnu. <b>Toujours une valeur</b> : l'absence de signalement en est une.</summary>
  public PersonalDataCategory Category { get; private set; }

  /// <summary>
  /// Le degré de la règle qui a déclenché, ou <c>null</c> quand rien n'a déclenché. Il n'y a pas de
  /// degré « nul » : une colonne non signalée n'a pas un doute faible, elle n'a pas de règle.
  /// </summary>
  public RuleStrength? Strength { get; private set; }

  /// <summary>
  /// Pourquoi cette colonne est signalée, en prose française, affichée telle quelle — ou
  /// <c>null</c> quand rien ne l'est. Il n'y a rien à motiver sur une colonne où rien n'a été vu.
  /// </summary>
  public string? Reason { get; private set; }

  /// <summary>
  /// Pourquoi cette colonne n'a eu <b>aucun aperçu</b>, ou <c>null</c> quand la question ne se pose
  /// pas — elle en a eu un, ou le relevé a été collé et rien n'a jamais été prélevé.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>C'est la seule chose d'un <see cref="ColumnPreview"/> qui descende en base, et ce n'est
  /// pas une entorse à <c>Rien de réel ne reste</c> : une raison n'est pas une valeur lue.</b> Sans
  /// elle, les quatre comptes de la <see cref="IncompletenessClause"/> seraient incalculables une
  /// heure après le scan, quand les aperçus ont expiré — et l'écran d'archive deviendrait <b>plus
  /// rassurant</b> que celui du jour même, ce qui est le mode de panne exact que ce contexte existe
  /// pour ne pas avoir.
  /// </para>
  /// <para>
  /// <b>Nulle sur le chemin collé, et nulle sur les rapports d'avant la connexion.</b> Les quatre
  /// comptes y sont <b>absents</b>, jamais à zéro : « zéro colonne sans aperçu » se lirait comme un
  /// prélèvement qui a tout réussi, sur un rapport où rien n'a jamais été prélevé.
  /// </para>
  /// </remarks>
  public PreviewAbsenceReason? PreviewAbsence { get; private set; }

  /// <summary>
  /// L'issue rendue, ou <c>null</c> tant que personne n'a tranché. <b>C'est le seul porteur de
  /// l'état</b> : voir <see cref="State"/>.
  /// </summary>
  public Arbitration? Arbitration { get; private set; }

  /// <summary>
  /// Où en est l'arbitrage — et c'est un <b>calcul</b>, jamais un champ. Un état stocké à côté de la
  /// date pourrait s'en dissocier ; ici <see cref="ScreenedColumnState.Awaiting"/> est très
  /// exactement « aucun arbitrage », et il ne peut pas mentir.
  /// </summary>
  public ScreenedColumnState State => Arbitration?.State ?? ScreenedColumnState.Awaiting;

  /// <summary>Cette colonne attend-elle encore qu'un humain la tranche ?</summary>
  public bool AwaitsAnArbitration => Arbitration is null;

  /// <summary>La détection a-t-elle signalé quelque chose ici ? Équivaut exactement à « elle porte un motif ».</summary>
  public bool IsFlagged => Category.IsFlagged;

  /// <summary>
  /// Le <b>geste de lot</b> peut-il atteindre cette ligne ? Il ne le peut que si rien n'y a été
  /// signalé et que personne ne l'a encore tranchée.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Aucun geste de lot ne porte sur une colonne signalée</b> : une suspicion ne s'écarte jamais
  /// sans avoir été lue une par une. C'est la borne entière du geste, et écarter en masse ce que le
  /// détection a vu serait exactement ce que le rapport de détection existe pour empêcher.
  /// </para>
  /// <para>
  /// <b>Une ligne déjà tranchée est hors de portée elle aussi</b>, pour l'autre raison : le lot
  /// liquide ce qui attend, il n'écrase pas d'un clic ce qu'un humain avait dit. Se raviser reste
  /// possible, mais colonne par colonne — c'est-à-dire en le voyant.
  /// </para>
  /// <para>
  /// ⚠️ <b>Le retrait du nom saisi ne rend pas le geste licite sur une signalée.</b> Il fut un
  /// temps où signer trois cent soixante-quatorze fois à la main rendait la chose impraticable ;
  /// ce prix a disparu, et il ne restera bientôt qu'un bouton tentant. <b>Le prix n'a jamais été
  /// le motif</b> — le nom n'en était que l'exécuteur incident. Le motif est celui du paragraphe
  /// au-dessus, et il n'a pas bougé d'un mot : <c>une suspicion ne s'écarte jamais sans avoir été
  /// lue une par une</c>. Qui viendra proposer d'élargir ce lot lira ceci d'abord.
  /// </para>
  /// <para>
  /// ⚠️ <b>La règle vit ici, sur la ligne, et à un seul endroit.</b> La poser dans la requête qui
  /// charge le lot l'aurait rendue invisible à qui lit le domaine, et un jour où quelqu'un
  /// élargirait cette requête, rien n'aurait rougi.
  /// </para>
  /// </remarks>
  public bool IsWithinReachOfABatchGesture => !IsFlagged && AwaitsAnArbitration;

  /// <summary>Ce qui nomme cette colonne : le triplet.</summary>
  public ColumnIdentity Identity => Listed.Identity;

  /// <summary>
  /// Une ligne <b>signalée</b> : une catégorie, le degré de la règle qui a déclenché, et le motif
  /// qui les justifie.
  /// </summary>
  /// <remarks>
  /// <b>Le motif est obligatoire dès que la ligne est signalée</b> : un signalement <b>sans</b>
  /// motif est une panne du contrat, pas un signalement. Sans lui, l'<c>Operator</c> arbitrerait
  /// sans rien savoir.
  /// </remarks>
  /// <param name="listed">La ligne du relevé.</param>
  /// <param name="category">Ce qui a été reconnu. Jamais <see cref="PersonalDataCategory.Unflagged"/>.</param>
  /// <param name="strength">Le degré de la règle qui a déclenché.</param>
  /// <param name="reason">La prose qui dit pourquoi.</param>
  /// <param name="previewAbsence">
  /// Pourquoi aucun aperçu n'accompagne cette ligne, ou <c>null</c> quand la question ne se pose
  /// pas. Facultative parce qu'elle n'existe que sur le chemin scanné, et <b>jamais</b> parce
  /// qu'elle serait optionnelle là où un prélèvement a échoué.
  /// </param>
  /// <exception cref="ArgumentNullException">Un des arguments est absent.</exception>
  /// <exception cref="ArgumentException">La catégorie est <see cref="PersonalDataCategory.Unflagged"/>, ou le motif est vide, démesuré, ou porte un caractère de contrôle.</exception>
  public static ScreenedColumn Flagged(
    ListedColumn listed,
    PersonalDataCategory category,
    RuleStrength strength,
    string? reason,
    PreviewAbsenceReason? previewAbsence = null)
  {
    ArgumentNullException.ThrowIfNull(listed);
    ArgumentNullException.ThrowIfNull(category);
    ArgumentNullException.ThrowIfNull(strength);

    if (!category.IsFlagged)
    {
      throw new ArgumentException(
        "Unflagged dit que rien n'a été vu : elle ne se signale pas, et elle ne porte pas de motif.",
        nameof(category));
    }

    return new ScreenedColumn(
      listed,
      category,
      strength,
      ScreeningText.OrThrow(reason, "Le motif de la ligne", MaxReasonLength, nameof(reason)),
      previewAbsence);
  }

  /// <summary>
  /// Une ligne où <b>rien n'a été vu</b>. Elle est rendue comme les autres, et elle s'arbitre comme
  /// les autres.
  /// </summary>
  /// <remarks>
  /// ⚠️ Ce n'est pas « cette colonne ne porte pas de données personnelles » : c'est un constat sur le
  /// détection, et non sur la donnée — que le service n'a jamais vue.
  /// </remarks>
  /// <param name="listed">La ligne du relevé.</param>
  /// <param name="previewAbsence">
  /// Pourquoi aucun aperçu n'accompagne cette ligne, ou <c>null</c> quand la question ne se pose
  /// pas. ⚠️ <b>Une absence d'aperçu n'est pas un signalement</b> : elle ne dit rien de ce que la
  /// colonne porte, et une ligne où rien n'a été vu peut parfaitement en porter une.
  /// </param>
  /// <exception cref="ArgumentNullException"><paramref name="listed"/> est absent.</exception>
  public static ScreenedColumn NothingSeen(
    ListedColumn listed,
    PreviewAbsenceReason? previewAbsence = null)
  {
    ArgumentNullException.ThrowIfNull(listed);

    return new ScreenedColumn(
      listed,
      PersonalDataCategory.Unflagged,
      strength: null,
      reason: null,
      previewAbsence);
  }

  /// <summary>
  /// Porte l'issue qu'un humain vient de rendre, <b>datée</b>.
  /// </summary>
  /// <remarks>
  /// <b>Un second arbitrage écrase le premier.</b> Il n'y a pas de journal de preuve ici : la trace
  /// <b>est</b> l'état courant, et se raviser doit rester possible sur une surface qu'on reprend
  /// pendant trois jours. Le coût est déclaré — la date de l'arbitrage remplacé est effacée.
  /// </remarks>
  /// <param name="ruling">Retenue, ou écartée. Jamais <see cref="ScreenedColumnState.Awaiting"/>.</param>
  /// <param name="renderedOn">L'instant où il a tranché.</param>
  /// <exception cref="ArgumentNullException"><paramref name="ruling"/> est absent.</exception>
  /// <exception cref="ArgumentException">L'état n'est pas une issue.</exception>
  internal void Arbitrate(ScreenedColumnState ruling, DateTimeOffset renderedOn)
  {
    Arbitration = Screenings.Arbitration.Rendered(ruling, renderedOn);
  }
}
