using MicroserviceRgpd.Core.Casework.Adapters;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// La <b>localisation</b> menée sur <b>un</b> <see cref="DeclaredSystem"/> pour ce dossier, et ce
/// qu'elle a rapporté : un <b>noyau certain</b> de références opaques, des <see cref="Reservation"/>
/// motivées, ou rien du tout.
/// </summary>
/// <remarks>
/// <para>
/// <b>Son grain est le système, jamais le droit</b> — à la différence du <see cref="Step"/>, qui est
/// dû par (<c>Claim</c>, système). Un <c>Locate</c> ne porte aucun <c>DataSubjectRight</c> : il
/// cherche <b>la personne</b>, et la chercher deux fois parce qu'elle réclame deux droits enverrait
/// deux fois la même requête chez le client pour la même réponse.
/// </para>
/// <para>
/// <b>Zéro rattachement est une valeur, et une valeur unique.</b> L'<c>Adapter</c> n'a pas à
/// distinguer « cherché, rien trouvé » de « désignation insuffisante » — distinction qu'il ne peut
/// pas faire. C'est le service qui, voyant <b>tous</b> ses <c>Locate</c> à zéro, en tire une
/// <see cref="OpenQuestion"/> sur le dossier.
/// </para>
/// <para>
/// <b>Elle garde ce qu'il faut pour ne pas répéter le <c>Ledger</c>, et rien de plus.</b>
/// <see cref="LastOutcome"/> est ce que l'<c>Adapter</c> a répondu la dernière fois : c'est
/// l'<b>appelant</b> qui s'en sert pour ne consigner qu'un verdict qui change, le <c>Ledger</c> ne se
/// relisant jamais lui-même. <see cref="DesignationsAtCall"/> est ce sous quoi on a cherché : le sac
/// s'enrichit, et un appel mené sous deux désignations n'a pas répondu à la question qu'on pose sous
/// trois.
/// </para>
/// <para>
/// <b>Elle n'est pas un agrégat, et n'a aucun dépôt.</b> Elle naît, se modifie et meurt par la
/// racine, comme le <see cref="Step"/> et la <see cref="Reservation"/>.
/// </para>
/// </remarks>
public sealed class Locating
{
  private readonly List<OpaqueReference> _certain;
  private readonly List<Reservation> _reserved;

  internal Locating(DeclaredSystemId declaredSystem, DateTimeOffset askedAt, int designationsAtCall)
  {
    DeclaredSystem = declaredSystem;
    AskedAt = askedAt;
    DesignationsAtCall = designationsAtCall;
    LastOutcome = AdapterOutcome.Served;
    _certain = [];
    _reserved = [];
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private Locating()
  {
    LastOutcome = AdapterOutcome.Served;
    _certain = [];
    _reserved = [];
  }

  /// <summary>Le système où l'on a cherché, par l'identifiant que l'<c>Adapter</c> a reçu.</summary>
  public DeclaredSystemId DeclaredSystem { get; private set; }

  /// <summary>
  /// Ce que l'<c>Adapter</c> a répondu la <b>dernière</b> fois — servi, différé, ou l'un des deux
  /// refus. Aucune valeur ne dit « en panne » : une panne n'est ni réponse ni refus, et elle ne
  /// laisse donc rien ici.
  /// </summary>
  public AdapterOutcome LastOutcome { get; private set; }

  /// <summary>L'instant du dernier appel. C'est une date de <b>tentative</b>, jamais un état.</summary>
  public DateTimeOffset AskedAt { get; private set; }

  /// <summary>
  /// L'échéance que l'<c>Adapter</c> a <b>déclarée</b> en différant, ou <c>null</c> pour toute autre
  /// réponse. Le service repassera après elle — <b>à l'ouverture du dossier</b>, jamais depuis la
  /// file, et sans compteur de tentatives.
  /// </summary>
  public DateTimeOffset? DeclaredDeadline { get; private set; }

  /// <summary>
  /// Le <b>nombre</b> de désignations sous lesquelles ce dernier appel est parti — jamais
  /// lesquelles. Il dit ce que cette réponse a répondu : le sac s'enrichit, et un appel mené sous
  /// moins n'a pas la même portée.
  /// </summary>
  public int DesignationsAtCall { get; private set; }

  /// <summary>
  /// Le <b>noyau certain</b> : ce que l'application rattache à la personne sans hésiter, dans son
  /// vocabulaire. Le service ne l'ouvre pas.
  /// </summary>
  public IReadOnlyList<OpaqueReference> Certain => _certain;

  /// <summary>Les réserves, arbitrées ou non, dans l'ordre où l'<c>Adapter</c> les a rendues.</summary>
  public IReadOnlyList<Reservation> Reserved => _reserved;

  /// <summary>
  /// Le service détient-il ici un <b>rattachement</b> ? Vrai d'un noyau certain non vide, ou d'une
  /// réserve qu'un humain a rattachée — jamais d'une réserve que personne n'a tranchée.
  /// </summary>
  /// <remarks>
  /// <b>Une réserve en attente n'est pas un rattachement</b>, et c'est ce qui fait que le constat
  /// reste réclamé sur un <c>Step</c> déclaré fait : compter comme trouvé ce que personne n'a
  /// regardé serait l'<c>Omission silencieuse</c> déguisée en dénombrement.
  /// </remarks>
  public bool HoldsAnAttachment =>
    _certain.Count > 0 || _reserved.Any(reservation => reservation.State == ReservationState.Attached);

  /// <summary>Une réserve attend-elle encore qu'un humain la tranche ?</summary>
  public bool AwaitsAnArbitration => _reserved.Any(reservation => reservation.AwaitsAnArbitration);

  /// <summary>
  /// L'<c>Adapter</c> a servi : le noyau certain est repris tel qu'il vient d'être rendu, et les
  /// réserves nouvelles s'ajoutent aux anciennes.
  /// </summary>
  /// <remarks>
  /// <b>Une réserve déjà arbitrée n'est jamais réécrite ni retirée.</b> L'application peut changer
  /// d'avis d'un appel à l'autre ; l'issue qu'un humain a rendue, elle, est un fait daté au
  /// <c>Ledger</c>, et la faire disparaître de l'écran ferait ré-arbitrer ce qui l'a déjà été.
  /// </remarks>
  internal void Served(
    IEnumerable<OpaqueReference> certain,
    IEnumerable<Reservation> reserved,
    DateTimeOffset askedAt,
    int designationsAtCall)
  {
    LastOutcome = AdapterOutcome.Served;
    DeclaredDeadline = null;
    AskedAt = askedAt;
    DesignationsAtCall = designationsAtCall;

    _certain.Clear();

    foreach (var reference in certain)
    {
      if (!_certain.Contains(reference))
      {
        _certain.Add(reference);
      }
    }

    foreach (var reservation in reserved)
    {
      if (!_reserved.Any(known => known.Reference == reservation.Reference))
      {
        _reserved.Add(reservation);
      }
    }
  }

  /// <summary>
  /// L'<c>Adapter</c> a différé et déclaré son échéance. <b>Rien du rattachement ne bouge</b> : il
  /// n'a pas répondu à la question, il a dit quand il y répondrait.
  /// </summary>
  internal void Deferred(DateTimeOffset declaredDeadline, DateTimeOffset askedAt, int designationsAtCall)
  {
    LastOutcome = AdapterOutcome.Deferred;
    DeclaredDeadline = declaredDeadline;
    AskedAt = askedAt;
    DesignationsAtCall = designationsAtCall;
  }

  /// <summary>
  /// L'<c>Adapter</c> a refusé. <b>Rien du rattachement ne bouge</b> : un refus dit que le service et
  /// l'application ne sont pas d'accord, jamais ce que le système porte.
  /// </summary>
  /// <exception cref="ArgumentException">La réponse donnée n'est pas un refus.</exception>
  internal void Refused(AdapterOutcome refusal, DateTimeOffset askedAt, int designationsAtCall)
  {
    LastOutcome = AdapterOutcome.RefusalOrThrow(refusal, nameof(refusal));
    DeclaredDeadline = null;
    AskedAt = askedAt;
    DesignationsAtCall = designationsAtCall;
  }

  /// <summary>
  /// Porte l'issue qu'un humain vient de rendre sur une réserve, et dit si elle attendait d'être
  /// tranchée.
  /// </summary>
  internal Reservation? Arbitrate(OpaqueReference reference, ReservationState ruling)
  {
    var reservation = _reserved.SingleOrDefault(one => one.Reference == reference);

    return reservation is not null && reservation.Arbitrate(ruling) ? reservation : null;
  }
}
