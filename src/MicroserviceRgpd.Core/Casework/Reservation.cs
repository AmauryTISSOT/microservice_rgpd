namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Une ligne qu'un <c>Adapter</c> a trouvée <b>sans trancher</b> : sa référence opaque, la
/// <b>prose française</b> qui dit pourquoi l'application hésite, et les <see cref="Designation"/>
/// qu'elle propose — s'il y en a.
/// </summary>
/// <remarks>
/// <para>
/// <b>La prose de motif est lue telle quelle par l'<c>Operator</c>.</b> C'est ce qui distingue cet
/// écran d'une case à cocher : « deux comptes portent ce nom, l'un créé en 2019, l'autre en 2024 »
/// se juge ; « ambigu » ne se juge pas. Le service n'en interprète pas un mot.
/// </para>
/// <para>
/// <b><see cref="Designations"/> est le seul champ que le service interprète</b>, et il ne
/// l'interprète qu'<b>après</b> l'arbitrage d'un humain : confirmer <c>Jean.Dupont@Example.fr</c>
/// trouvé en base verse cette adresse au sac, et l'appel suivant ouvre le journal applicatif qui la
/// cherchait sous une autre casse. <b>Absent, la réserve reste locale et opaque</b> : le service
/// n'apprendra jamais ce que « #1203 » désigne, et c'est très bien ainsi.
/// </para>
/// <para>
/// <b>C'est de la prose de travail.</b> Elle dit quelle ligne appartient à qui, elle nomme donc par
/// nature des tiers non demandeurs, elle vit sur le <see cref="Case"/> et <b>meurt à sa clôture</b>.
/// Le <c>Ledger</c> n'en garde que le fait daté — « 1 réserve arbitrée le 12/04 » — et il n'existe
/// aucune colonne où ce texte pourrait atterrir.
/// </para>
/// <para>
/// <b>Elle n'est pas un agrégat, et n'a aucun dépôt.</b> Comme le <see cref="Step"/>, elle naît, se
/// modifie et meurt par la racine.
/// </para>
/// </remarks>
public sealed class Reservation
{
  /// <summary>
  /// Le plafond de la prose de motif, en unités UTF-16. Large : c'est une explication qu'un humain
  /// doit pouvoir juger, pas une étiquette.
  /// </summary>
  public const int MaxReasonLength = 2000;

  private readonly List<Designation> _designations;

  internal Reservation(OpaqueReference reference, string reason, IEnumerable<Designation> designations)
  {
    Reference = reference;
    Reason = DeclaredText.OrThrow(reason, "Le motif de la réserve", MaxReasonLength, nameof(reason));
    State = ReservationState.Awaiting;
    _designations = [.. designations];
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private Reservation()
  {
    Reference = null!;
    Reason = string.Empty;
    State = ReservationState.Awaiting;
    _designations = [];
  }

  /// <summary>
  /// Ce que l'application nomme, opaque de bout en bout. C'est aussi ce qui identifie la réserve à
  /// l'intérieur d'un système : un <c>Adapter</c> qui rendrait deux fois la même référence parlerait
  /// deux fois de la même ligne.
  /// </summary>
  public OpaqueReference Reference { get; private set; }

  /// <summary>
  /// Pourquoi l'application hésite, <b>en prose française</b>, affichée telle quelle. Elle n'est
  /// jamais vide : une réserve sans motif serait une case à cocher, et l'<c>Operator</c> arbitrerait
  /// sans rien savoir.
  /// </summary>
  public string Reason { get; private set; }

  /// <summary>Où en est l'arbitrage. Né <see cref="ReservationState.Awaiting"/> : personne n'a tranché.</summary>
  public ReservationState State { get; private set; }

  /// <summary>
  /// Ce que cette réserve propose de verser au sac si elle est rattachée — <b>éventuellement
  /// vide</b>, et c'est le régime le plus courant. Vide, la réserve ne fait rien remonter au service.
  /// </summary>
  public IReadOnlyList<Designation> Designations => _designations;

  /// <summary>Cette réserve attend-elle encore qu'un humain la tranche ?</summary>
  public bool AwaitsAnArbitration => !State.IsSettled;

  /// <summary>
  /// Porte l'issue qu'un humain vient de rendre. <b>Le premier arbitrage est le bon</b> : un second
  /// ne réécrit pas le premier, la preuve gardant déjà la date et le nom de celui qui a tranché.
  /// </summary>
  /// <param name="ruling">Rattachée, ou écartée. Jamais <see cref="ReservationState.Awaiting"/>.</param>
  /// <returns><c>true</c> si l'arbitrage a été porté ; <c>false</c> si un autre l'avait déjà été.</returns>
  internal bool Arbitrate(ReservationState ruling)
  {
    if (State.IsSettled)
    {
      return false;
    }

    State = ReservationState.RulingOrThrow(ruling, nameof(ruling));

    return true;
  }
}
