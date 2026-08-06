using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// La <b>lecture</b> menée sur <b>un</b> <see cref="DeclaredSystem"/> au titre d'<b>un</b>
/// <see cref="DataSubjectRight"/>, et ce que l'<c>Adapter</c> en a répondu — servi, différé, ou l'un
/// des deux refus.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle ne porte aucun octet, et c'est tout le partage.</b> Ce qui a été lu vit dans une
/// <see cref="RetrievedData"/>, <b>hors de l'agrégat</b>, parce que la remise l'effacera sans
/// réécrire le dossier. Ce qui reste ici est ce qui <b>meurt avec le dossier</b> : qu'on ait appelé,
/// quand, sous combien de désignations, et ce qu'on nous a répondu.
/// </para>
/// <para>
/// <b>Son grain est (droit, système)</b> — à la différence du <see cref="Locating"/>, dont le grain
/// est le système seul. Un <c>Locate</c> cherche <em>la personne</em> et la chercher deux fois parce
/// qu'elle réclame deux droits enverrait deux fois la même requête pour la même réponse ; un
/// <c>Read</c> lit <em>au titre d'un droit</em>, et le périmètre matériel de l'art. 20 n'est pas
/// celui de l'art. 15.
/// </para>
/// <para>
/// <b>Elle garde l'échéance d'un <c>202</c> parce que c'est elle qui fait repasser</b> — à
/// l'ouverture du dossier, jamais par une minuterie, et sans compteur de tentatives.
/// </para>
/// <para>
/// <b>Elle n'est pas un agrégat, et n'a aucun dépôt.</b> Elle naît, se modifie et meurt par la
/// racine, comme le <see cref="Step"/> et le <see cref="Locating"/>.
/// </para>
/// </remarks>
public sealed class Reading
{
  internal Reading(DataSubjectRight right, DeclaredSystemId declaredSystem, DateTimeOffset askedAt, int designationsAtCall)
  {
    Right = right;
    DeclaredSystem = declaredSystem;
    AskedAt = askedAt;
    DesignationsAtCall = designationsAtCall;
    LastOutcome = AdapterOutcome.Served;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private Reading()
  {
    Right = DataSubjectRight.Access;
    LastOutcome = AdapterOutcome.Served;
  }

  /// <summary>Le droit au titre duquel on a lu — celui-là même que l'appel a porté chez l'<c>Adapter</c>.</summary>
  public DataSubjectRight Right { get; private set; }

  /// <summary>Le système où l'on a lu, par l'identifiant que l'<c>Adapter</c> a reçu.</summary>
  public DeclaredSystemId DeclaredSystem { get; private set; }

  /// <summary>
  /// Ce que l'<c>Adapter</c> a répondu la <b>dernière</b> fois. Aucune valeur ne dit « en panne » :
  /// une panne n'est ni réponse ni refus, et elle ne laisse donc rien ici.
  /// </summary>
  public AdapterOutcome LastOutcome { get; private set; }

  /// <summary>L'instant du dernier appel. C'est une date de <b>tentative</b>, jamais un état.</summary>
  public DateTimeOffset AskedAt { get; private set; }

  /// <summary>
  /// L'échéance que l'<c>Adapter</c> a <b>déclarée</b> en différant, ou <c>null</c> pour toute autre
  /// réponse. Le service repassera après elle, <b>à l'ouverture du dossier</b>.
  /// </summary>
  public DateTimeOffset? DeclaredDeadline { get; private set; }

  /// <summary>
  /// Le <b>nombre</b> de désignations sous lesquelles ce dernier appel est parti — jamais
  /// lesquelles. Le sac s'enrichit, et une lecture menée sous moins n'a pas la même portée.
  /// </summary>
  public int DesignationsAtCall { get; private set; }

  /// <summary>
  /// L'<c>Adapter</c> a <b>servi</b>. La pièce, elle, ne passe pas par ici : elle est gardée hors de
  /// l'agrégat, et le dossier ne retient que le fait qu'on l'ait obtenue.
  /// </summary>
  internal void Served(DateTimeOffset askedAt, int designationsAtCall)
  {
    LastOutcome = AdapterOutcome.Served;
    DeclaredDeadline = null;
    AskedAt = askedAt;
    DesignationsAtCall = designationsAtCall;
  }

  /// <summary>
  /// L'<c>Adapter</c> a <b>différé</b> et déclaré son échéance. Rien n'a été lu : il n'a pas répondu
  /// à la question, il a dit quand il y répondrait.
  /// </summary>
  internal void Deferred(DateTimeOffset declaredDeadline, DateTimeOffset askedAt, int designationsAtCall)
  {
    LastOutcome = AdapterOutcome.Deferred;
    DeclaredDeadline = declaredDeadline;
    AskedAt = askedAt;
    DesignationsAtCall = designationsAtCall;
  }

  /// <summary>
  /// L'<c>Adapter</c> a <b>refusé</b>. Rien n'a été lu : un refus dit que le service et l'application
  /// ne sont pas d'accord, jamais ce que le système porte.
  /// </summary>
  /// <exception cref="ArgumentException">La réponse donnée n'est pas un refus.</exception>
  internal void Refused(AdapterOutcome refusal, DateTimeOffset askedAt, int designationsAtCall)
  {
    LastOutcome = AdapterOutcome.RefusalOrThrow(refusal, nameof(refusal));
    DeclaredDeadline = null;
    AskedAt = askedAt;
    DesignationsAtCall = designationsAtCall;
  }
}
