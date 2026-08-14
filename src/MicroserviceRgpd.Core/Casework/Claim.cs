using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// La réclamation d'<b>un</b> <see cref="DataSubjectRight"/> à l'intérieur d'un <see cref="Case"/>.
/// C'est l'unité à laquelle le service répond à la personne au sujet d'un droit — elle atteste
/// l'<b>acte de répondre</b>, jamais le fait qu'un droit ait été satisfait.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Homonyme dormant</b> : <c>System.Security.Claims.Claim</c>. Le nom du glossaire l'emporte,
/// et aucun <c>using</c> de ce namespace n'entre dans ce contexte.
/// </para>
/// <para>
/// <b>Il n'est pas un agrégat, et n'a aucun dépôt.</b> Comme le <see cref="Step"/>, il naît, se
/// modifie et meurt par la racine — sans constructeur public ni propriété qu'on puisse écrire de
/// l'extérieur. La justification est l'invariant « lire avant d'effacer », qui traverse
/// <b>deux</b> <c>Claim</c> et qu'aucune frontière plus fine ne pourrait tenir.
/// </para>
/// <para>
/// <b>Un droit au plus par <see cref="Case"/>.</b> Deux <c>Claim</c> portant le même droit dans un
/// même dossier seraient deux réponses dues à la personne sur la même question — le droit
/// lui-même identifie donc la réclamation, et aucun identifiant de substitution n'est engendré.
/// </para>
/// </remarks>
public sealed class Claim
{
  private readonly List<Step> _steps;

  internal Claim(
    DataSubjectRight right,
    ClaimOrigin origin,
    IdentityDeclaration identityAtOrigin,
    IEnumerable<DeclaredSystemId> declaredSystems)
  {
    Right = right;
    State = ClaimState.Open;
    Origin = origin;
    IdentityAtOrigin = identityAtOrigin;
    Confirmed = origin.ConfirmedAtBirth;
    _steps = [.. declaredSystems.Select(system => new Step(system))];
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private Claim()
  {
    Right = DataSubjectRight.Access;
    State = ClaimState.Open;
    Origin = ClaimOrigin.Named;
    IdentityAtOrigin = IdentityDeclaration.Unverified;
    _steps = [];
  }

  /// <summary>
  /// Le droit réclamé. <b>Jamais <see cref="DataSubjectRight.OutOfScope"/></b> : celui-ci est le
  /// verdict qu'aucun droit n'a été reconnu, et une réclamation de rien n'existe pas. Une demande
  /// n'exerçant aucun droit ouvre un <see cref="Case"/> sans aucun <c>Claim</c>, et se clora
  /// <c>NotApplicable</c> sous la signature d'un humain.
  /// </summary>
  public DataSubjectRight Right { get; private set; }

  /// <summary>Où en est la réponse du service sur ce droit. Trois valeurs.</summary>
  public ClaimState State { get; private set; }

  /// <summary>
  /// Le travail dû, <b>un <see cref="Step"/> par <see cref="DeclaredSystem"/></b> du catalogue au
  /// moment où le dossier s'est ouvert. Éventuellement vide — c'est l'état d'un service dont le
  /// <c>Manifest</c> n'a pas encore été déclaré, et l'écran doit pouvoir le dire.
  /// </summary>
  public IReadOnlyList<Step> Steps => _steps;

  /// <summary>
  /// D'où vient la reconnaissance de ce droit. <b>Figée à la naissance</b> : un <c>Claim</c> garde
  /// la porte sous laquelle il est né.
  /// </summary>
  public ClaimOrigin Origin { get; private set; }

  /// <summary>
  /// Ce que le canal déclarait de l'identité du demandeur <b>à l'instant où ce droit s'est
  /// ouvert</b>, et non ce qu'il en déclare aujourd'hui.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>C'est une copie, et c'est le propos.</b> Le <see cref="Case"/> porte l'identité déclarée
  /// courante — l'identité est une propriété de la personne, et elle se reprend. Mais une déclaration
  /// relevée en fin de dossier ne réécrit pas la preuve d'hier : l'accès ouvert lundi l'a été sous
  /// <c>Unverified</c>, et le rappel passé vendredi ne le rend pas rétroactivement propre.
  /// </para>
  /// <para>
  /// La copie vit ici plutôt que dans le <c>EvidenceLog</c> seul parce que l'écran doit pouvoir la
  /// montrer à côté du droit qu'elle concerne, sans faire relire à l'<c>Operator</c> la preuve pour
  /// savoir ce qu'il a sous les yeux.
  /// </para>
  /// </remarks>
  public IdentityDeclaration IdentityAtOrigin { get; private set; }

  /// <summary>
  /// Un humain a-t-il repris ce droit à son compte ? Vrai dès la naissance sauf pour
  /// <see cref="ClaimOrigin.Proposed"/>, qu'une machine seule a reconnu.
  /// </summary>
  public bool Confirmed { get; private set; }

  /// <summary>
  /// Le dossier doit-il <b>réclamer une motivation</b> pour ce droit ? C'est une lecture de
  /// <see cref="IdentityMotivation.IsDemandedBy"/> sur l'identité <b>d'origine</b>, jamais sur
  /// l'identité courante : ce qui est en cause est la porte sous laquelle ce droit s'est ouvert.
  /// </summary>
  public bool MotivationIsDemanded => IdentityMotivation.IsDemandedBy(IdentityAtOrigin, Right);

  /// <summary>
  /// Ce droit attend-il encore qu'un humain le reprenne à son compte ? Vrai d'un
  /// <see cref="ClaimOrigin.Proposed"/> non confirmé, et <b>de rien d'autre</b>.
  /// </summary>
  /// <remarks>
  /// C'est une <c>OpenQuestion</c>, jamais un blocage : le dossier est ouvert, le délai court, et
  /// l'attente est <b>visible dans le <see cref="Case"/></b> plutôt que rangée dans un vestibule que
  /// personne ne regarde.
  /// </remarks>
  public bool AwaitsConfirmation => !Confirmed;

  /// <summary>
  /// Un humain reprend ce droit à son compte. <b>Idempotent</b> : reconfirmer ne défait rien, et un
  /// second clic n'est pas une faute qu'il faudrait signaler à qui l'a fait.
  /// </summary>
  /// <remarks>
  /// <b>Elle ne consigne rien.</b> La ligne de preuve est écrite par l'appelant, hors de l'agrégat,
  /// pour la même raison qu'ailleurs : le <c>EvidenceLog</c> survit au dossier de cinq ans.
  /// </remarks>
  internal void Confirm() => Confirmed = true;

  /// <summary>
  /// Ce droit attend-il encore une <b>issue</b> ? Vrai tant qu'il est <see cref="ClaimState.Open"/>.
  /// </summary>
  /// <remarks>
  /// C'est ce que la clôture <b>réclame</b>, et jamais ce qu'elle exige : un dossier se clôt sur des
  /// <c>Claim</c> restés ouverts, et l'écran l'aura dit avant de laisser signer.
  /// </remarks>
  public bool AwaitsAnOutcome => State == ClaimState.Open;

  /// <summary>
  /// Un humain déclare que le service a <b>répondu</b> sur ce droit.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Elle n'affirme que l'acte de répondre.</b> Des <see cref="Step"/> restés inatteints ne la
  /// barrent pas : l'incomplétude reste lisible un <c>Step</c> à la fois, plutôt que masquée par un
  /// état de haut niveau rassurant — voir <see cref="ClaimState.Answered"/>.
  /// </para>
  /// <para>
  /// <b>Elle ne se rejoue pas et ne défait rien.</b> Un <c>Claim</c> déjà répondu — ou refusé —
  /// rend <c>false</c> plutôt que de lever : le second clic n'est pas une panne, et réécrire un
  /// <see cref="ClaimState.Refused"/> en <c>Answered</c> effacerait la charge probatoire propre du
  /// refus.
  /// </para>
  /// <para>
  /// <b>Elle ne consigne rien.</b> La ligne de preuve est écrite par l'appelant, hors de l'agrégat,
  /// parce que le <c>EvidenceLog</c> survit au dossier de cinq ans.
  /// </para>
  /// </remarks>
  /// <returns><c>true</c> si le droit vient de passer à <c>Answered</c> ; <c>false</c> s'il était déjà clos.</returns>
  internal bool Answer()
  {
    if (State != ClaimState.Open)
    {
      return false;
    }

    State = ClaimState.Answered;

    return true;
  }

  /// <summary>
  /// L'instant où le paquet de ce droit est <b>sorti du service</b> pour la première fois, ou
  /// <c>null</c> si personne ne l'a encore pris. C'est le <b>premier</b> des deux gestes de la remise.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Il ne date rien de ce que le service prouve</b>, et n'entre donc pas au <c>EvidenceLog</c> :
  /// prendre le paquet n'est pas remettre, et confondre les deux ferait dater la preuve du moment où
  /// un fichier a quitté un serveur. Ce qu'il sert est la <b>file</b> — une remise commencée et non
  /// déclarée doit rester une ligne vue tous les jours, plutôt qu'une ligne manquante.
  /// </para>
  /// <para>
  /// <b>Le premier instant est gardé, jamais le dernier.</b> Reprendre le paquet ne défait pas qu'un
  /// exemplaire soit déjà hors de portée pour toujours, et réécrire la date ferait mentir la file sur
  /// le jour où cela a commencé.
  /// </para>
  /// </remarks>
  public DateTimeOffset? DeliveryTakenOn { get; private set; }

  /// <summary>
  /// L'instant où un humain a <b>déclaré la remise</b> de ce droit, ou <c>null</c> si personne ne l'a
  /// déclarée. C'est le <b>second</b> geste, et lui seul date la remise au <c>EvidenceLog</c> et détruit
  /// les <see cref="RetrievedData"/>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La remise est une affirmation, pas un transfert d'octets.</b> Le service ne prouvera
  /// jamais que la personne a reçu quoi que ce soit — <c>Enregistré, jamais vérifié</c>.
  /// </remarks>
  public DateTimeOffset? DeliveryDeclaredOn { get; private set; }

  /// <summary>
  /// Le paquet est-il sorti sans que personne n'ait déclaré la remise ? C'est la colonne que la file
  /// porte sur une ligne <b>déjà présente</b> : elle trie et rappelle, elle ne fait apparaître aucun
  /// dossier.
  /// </summary>
  public bool DeliveryAwaitsDeclaration => DeliveryTakenOn is not null && DeliveryDeclaredOn is null;

  /// <summary>
  /// Le paquet sort du service. <b>Rien n'est remis</b> : c'est le geste par lequel un humain ouvre
  /// le ZIP et vérifie qu'il n'est pas vide, sans dater la preuve du mauvais instant.
  /// </summary>
  /// <returns><c>true</c> si c'est la première sortie ; <c>false</c> si un exemplaire était déjà dehors.</returns>
  internal bool TakeDelivery(DateTimeOffset takenOn)
  {
    if (DeliveryTakenOn is not null)
    {
      return false;
    }

    DeliveryTakenOn = takenOn.ToUniversalTime();

    return true;
  }

  /// <summary>
  /// Un humain <b>déclare</b> la remise. Le geste ne se rejoue pas : une remise déjà déclarée l'est
  /// une fois pour toutes, et la redéclarer écrirait une seconde ligne de preuve pour un seul fait.
  /// </summary>
  /// <returns><c>true</c> si la remise vient d'être déclarée ; <c>false</c> si elle l'était déjà.</returns>
  internal bool DeclareDelivered(DateTimeOffset declaredOn)
  {
    if (DeliveryDeclaredOn is not null)
    {
      return false;
    }

    DeliveryDeclaredOn = declaredOn.ToUniversalTime();

    return true;
  }
}
