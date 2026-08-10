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
/// <b>Le motif est de la prose de travail, lue telle quelle et jamais analysée.</b>
/// « <c>adr_l1</c> → <c>ContactDetails</c>, degré bas, motif : préfixe <c>adr</c> reconnu »
/// s'arbitre ; « <c>ContactDetails</c>, 0,72 » ne s'arbitre pas.
/// </para>
/// <para>
/// <b>Elle n'est pas un agrégat.</b> Elle naît et se modifie par la racine — mais elle reçoit, elle,
/// son propre <c>DbSet</c> : le précédent de <c>Claim</c> et <c>Step</c> est rompu délibérément,
/// parce qu'un <c>Case</c> a quelques dizaines d'enfants là où un <see cref="Screening"/> en a cinq
/// mille, et que l'écran n'ouvre qu'une table à la fois.
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
    string? reason)
  {
    Listed = listed;
    Category = category;
    Strength = strength;
    Reason = reason;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private ScreenedColumn()
  {
    Listed = null!;
    Category = PersonalDataCategory.Unflagged;
  }

  /// <summary>La ligne du relevé, recopiée telle quelle.</summary>
  public ListedColumn Listed { get; private set; }

  /// <summary>Ce que le dépistage a reconnu. <b>Toujours une valeur</b> : l'absence de signalement en est une.</summary>
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
  /// L'issue signée, ou <c>null</c> tant que personne n'a tranché. <b>C'est le seul porteur de
  /// l'état</b> : voir <see cref="State"/>.
  /// </summary>
  public Arbitration? Arbitration { get; private set; }

  /// <summary>
  /// Où en est l'arbitrage — et c'est un <b>calcul</b>, jamais un champ. Un état stocké à côté de la
  /// signature pourrait s'en dissocier ; ici <see cref="ScreenedColumnState.Awaiting"/> est très
  /// exactement « aucune signature », et il ne peut pas mentir.
  /// </summary>
  public ScreenedColumnState State => Arbitration?.State ?? ScreenedColumnState.Awaiting;

  /// <summary>Cette colonne attend-elle encore qu'un humain la tranche ?</summary>
  public bool AwaitsAnArbitration => Arbitration is null;

  /// <summary>Le dépistage a-t-il signalé quelque chose ici ? Équivaut exactement à « elle porte un motif ».</summary>
  public bool IsFlagged => Category.IsFlagged;

  /// <summary>Ce qui nomme cette colonne : le triplet.</summary>
  public ColumnIdentity Identity => Listed.Identity;

  /// <summary>
  /// Une ligne <b>signalée</b> : une catégorie, le degré de la règle qui a déclenché, et le motif
  /// qui les justifie.
  /// </summary>
  /// <remarks>
  /// <b>Le motif est obligatoire dès que la ligne est signalée</b>, sur le modèle exact de
  /// <c>Reservation</c>, dont le glossaire dit qu'« une réserve <b>sans</b> motif est une panne du
  /// contrat, pas une réserve ». Sans lui, l'<c>Operator</c> arbitrerait sans rien savoir.
  /// </remarks>
  /// <param name="listed">La ligne du relevé.</param>
  /// <param name="category">Ce qui a été reconnu. Jamais <see cref="PersonalDataCategory.Unflagged"/>.</param>
  /// <param name="strength">Le degré de la règle qui a déclenché.</param>
  /// <param name="reason">La prose qui dit pourquoi.</param>
  /// <exception cref="ArgumentNullException">Un des arguments est absent.</exception>
  /// <exception cref="ArgumentException">La catégorie est <see cref="PersonalDataCategory.Unflagged"/>, ou le motif est vide, démesuré, ou porte un caractère de contrôle.</exception>
  public static ScreenedColumn Flagged(
    ListedColumn listed,
    PersonalDataCategory category,
    RuleStrength strength,
    string? reason)
  {
    ArgumentNullException.ThrowIfNull(listed);
    ArgumentNullException.ThrowIfNull(category);
    ArgumentNullException.ThrowIfNull(strength);

    if (!category.IsFlagged)
    {
      throw new ArgumentException(
        "Unflagged dit que rien n'a été vu : elle ne se signale pas, et elle ne porte pas de motif. "
        + "Une colonne vue mais qu'aucune valeur ne décrit est PersonalDataUncategorised, qui est un "
        + "verdict et non un aveu d'ignorance.",
        nameof(category));
    }

    return new ScreenedColumn(
      listed,
      category,
      strength,
      ScreeningText.OrThrow(reason, "Le motif de la ligne", MaxReasonLength, nameof(reason)));
  }

  /// <summary>
  /// Une ligne où <b>rien n'a été vu</b>. Elle est rendue comme les autres, et elle s'arbitre comme
  /// les autres.
  /// </summary>
  /// <remarks>
  /// ⚠️ Ce n'est pas « cette colonne ne porte pas de données personnelles » : c'est un constat sur le
  /// dépistage, et non sur la donnée — que le service n'a jamais vue.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="listed"/> est absent.</exception>
  public static ScreenedColumn NothingSeen(ListedColumn listed)
  {
    ArgumentNullException.ThrowIfNull(listed);

    return new ScreenedColumn(listed, PersonalDataCategory.Unflagged, strength: null, reason: null);
  }

  /// <summary>
  /// Porte l'issue qu'un humain vient de rendre, <b>signée et datée</b>.
  /// </summary>
  /// <remarks>
  /// <b>Un second arbitrage écrase le premier</b>, à l'inverse de <c>Reservation</c> dont « le
  /// premier arbitrage est le bon » parce qu'un <c>Ledger</c> en garde la trace. Ici il n'y a pas de
  /// <c>Ledger</c> : la trace <b>est</b> l'état courant, et se raviser doit rester possible sur une
  /// surface qu'on reprend pendant trois jours. Le coût est déclaré — qui avait dit quoi est effacé.
  /// </remarks>
  /// <param name="ruling">Retenue, ou écartée. Jamais <see cref="ScreenedColumnState.Awaiting"/>.</param>
  /// <param name="signedBy">Le nom saisi par celui qui tranche.</param>
  /// <param name="signedOn">L'instant où il a tranché.</param>
  /// <exception cref="ArgumentNullException"><paramref name="ruling"/> est absent.</exception>
  /// <exception cref="ArgumentException">L'état n'est pas une issue, ou la signature est vide, démesurée, ou porte un caractère de contrôle.</exception>
  internal void Arbitrate(ScreenedColumnState ruling, string? signedBy, DateTimeOffset signedOn)
  {
    Arbitration = Screenings.Arbitration.Rendered(ruling, signedBy, signedOn);
  }
}
