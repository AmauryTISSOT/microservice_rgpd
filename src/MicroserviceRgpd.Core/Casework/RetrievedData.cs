using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Une pièce que <c>Read</c> a ramenée d'<b>un</b> <see cref="DeclaredSystem"/>, au titre d'<b>un</b>
/// <see cref="DataSubjectRight"/> — et <b>hors de l'agrégat <see cref="Case"/></b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Hors de l'agrégat, parce que sa durée de vie est la sienne.</b> Le geste « déclarer remis »
/// la détruira <b>sans réécrire le dossier</b> : la faire vivre dans le <c>Case</c> aurait attaché
/// l'effacement d'un contenu à la mise à jour d'une racine qui, elle, survit à la remise. Son séjour
/// est inévitable — la lecture précède l'effacement — et il doit être minimal : un service qui
/// entreposerait les exports deviendrait la donnée la plus concentrée du système d'information de
/// son client.
/// </para>
/// <para>
/// <b>Elle n'a aucun identifiant de substitution.</b> Le triplet (dossier, droit, système) l'identifie :
/// une seconde pièce pour la même paire serait deux réponses dues à la personne sur la même
/// question. Une relecture <b>remplace</b> donc, elle n'empile pas.
/// </para>
/// <para>
/// <b>Le grain est (droit, système), et non le système seul</b> — à la différence du
/// <see cref="Locating"/>. Un <c>Locate</c> cherche <em>la personne</em> et ne porte aucun droit ; un
/// <c>Read</c> lit <em>au titre d'un droit</em>, et l'application peut légitimement rendre d'un même
/// système deux pièces différentes selon qu'on lit sous l'art. 15 ou sous l'art. 20.
/// </para>
/// <para>
/// ⚠️ <b>Le corps ne s'ouvre jamais, et il n'entre jamais au <c>EvidenceLog</c>.</b> La preuve dit « un
/// fichier a été remis le 12/04 couvrant 2 systèmes sur 6 », jamais ce qu'il y avait dedans.
/// </para>
/// </remarks>
public sealed class RetrievedData
{
  private RetrievedData(
    CaseId caseId,
    DataSubjectRight right,
    DeclaredSystemId declaredSystem,
    TransportEnvelope envelope,
    byte[] content,
    DateTimeOffset retrievedAt)
  {
    Case = caseId;
    Right = right;
    DeclaredSystem = declaredSystem;
    Envelope = envelope;
    Content = content;
    RetrievedAt = retrievedAt;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private RetrievedData()
  {
    Right = DataSubjectRight.Access;
    Envelope = new TransportEnvelope(TransportEnvelope.UnnamedContentType, string.Empty);
    Content = [];
  }

  /// <summary>
  /// Le dossier au titre duquel la pièce a été lue. <b>Une référence, jamais une navigation</b> :
  /// cette pièce est hors de l'agrégat, et rien ne la charge avec le dossier.
  /// </summary>
  public CaseId Case { get; private set; }

  /// <summary>
  /// Le droit <b>au titre duquel</b> on a lu — celui-là même que l'appel a porté chez l'<c>Adapter</c>.
  /// C'est lui qui range la pièce dans la <c>Delivery</c> d'un <c>Claim</c> : deux droits sont deux
  /// réponses, deux dates de remise, et le service ne sait de toute façon pas fusionner.
  /// </summary>
  public DataSubjectRight Right { get; private set; }

  /// <summary>Le système qui a servi la pièce, par l'identifiant que l'<c>Adapter</c> a reçu.</summary>
  public DeclaredSystemId DeclaredSystem { get; private set; }

  /// <summary>
  /// Ce que le <b>transport</b> a mis dans la main du service, recopié sans être interprété. C'est
  /// tout ce qu'il sait de la pièce.
  /// </summary>
  public TransportEnvelope Envelope { get; private set; }

  /// <summary>
  /// Les octets, tels qu'ils sont arrivés et <b>jamais ouverts</b>. Éventuellement vides : la pièce
  /// vide est une réponse datée, et non un silence.
  /// </summary>
  public byte[] Content { get; private set; }

  /// <summary>L'instant de la lecture. C'est une date de <b>tentative servie</b>, jamais un état.</summary>
  public DateTimeOffset RetrievedAt { get; private set; }

  /// <summary>
  /// La pièce est-elle <b>vide</b> ? Vrai d'un corps de zéro octet — l'application a répondu, et il
  /// n'y avait rien.
  /// </summary>
  /// <remarks>
  /// <b>Vide n'est pas absent.</b> Une pièce vide dit « interrogé, rien » et porte sa date ; une
  /// pièce absente dit qu'on n'a rien obtenu de ce système. Les confondre ferait écrire sur la
  /// <c>DeliveryLetter</c> un constat que personne n'a fait.
  /// </remarks>
  public bool IsEmpty => Content.Length == 0;

  /// <summary>
  /// La pièce telle qu'un <c>Read</c> servi vient de la rendre, attribuée au dossier, au droit et au
  /// système au titre desquels l'appel est parti.
  /// </summary>
  /// <param name="caseId">Le dossier au titre duquel l'appel est parti.</param>
  /// <param name="right">Le droit au titre duquel on a lu.</param>
  /// <param name="declaredSystem">Le système qui a servi.</param>
  /// <param name="served">L'enveloppe et les octets, tels que le fil les a portés.</param>
  /// <param name="retrievedAt">L'instant de la lecture.</param>
  /// <exception cref="ArgumentNullException">Un argument obligatoire est absent.</exception>
  public static RetrievedData Of(
    CaseId caseId,
    DataSubjectRight right,
    DeclaredSystemId declaredSystem,
    RetrievedPiece served,
    DateTimeOffset retrievedAt)
  {
    ArgumentNullException.ThrowIfNull(right);
    ArgumentNullException.ThrowIfNull(served);

    return new RetrievedData(
      caseId,
      right,
      declaredSystem,
      served.Envelope,
      served.Content,
      retrievedAt.ToUniversalTime());
  }

  /// <summary>
  /// La pièce d'aujourd'hui <b>prend la place</b> de celle d'hier, sur la même identité.
  /// </summary>
  /// <remarks>
  /// <b>Remplacer, et non empiler.</b> Une relecture sous un sac enrichi rend une autre pièce pour le
  /// même (dossier, droit, système) ; en garder deux ferait deux réponses dues à la personne sur la
  /// même question, et laisserait dans le service un exemplaire de plus des données de quelqu'un —
  /// c'est-à-dire l'inverse exact du séjour minimal qu'on lui doit.
  /// <para>
  /// C'est une <b>écriture sur place</b>, et cela compte : détruire puis recréer aurait demandé deux
  /// écritures pour une identité qui ne change pas, et laissé, entre les deux, un instant où la
  /// personne n'a plus ni l'ancienne pièce ni la nouvelle.
  /// </para>
  /// </remarks>
  /// <param name="served">L'enveloppe et les octets qui viennent d'arriver.</param>
  /// <param name="retrievedAt">L'instant de cette lecture-ci.</param>
  /// <exception cref="ArgumentNullException"><paramref name="served"/> est absent.</exception>
  public void Replace(RetrievedPiece served, DateTimeOffset retrievedAt)
  {
    ArgumentNullException.ThrowIfNull(served);

    Envelope = served.Envelope;
    Content = served.Content;
    RetrievedAt = retrievedAt.ToUniversalTime();
  }
}
