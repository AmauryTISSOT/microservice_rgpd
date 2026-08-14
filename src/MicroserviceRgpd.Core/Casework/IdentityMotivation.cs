using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Ce que l'humain a pesé avant d'ouvrir un droit sous une identité que personne n'a vérifiée : une
/// <b>méthode</b> et un <b>détail</b>, en deux champs qui ne se confondent jamais.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deux champs parce qu'ils n'ont ni le même lecteur ni la même durée de vie.</b> La
/// <see cref="Method"/> est un vocabulaire fermé : elle <b>se compte</b>, son lecteur est le contrôle,
/// et elle entre au <c>EvidenceLog</c> où elle <b>survit à la clôture</b>. Le <see cref="Detail"/> est de
/// la prose libre : il dit <i>qui</i> a été rappelé et <i>sur quoi</i>, il est donc irréductiblement
/// nominatif, son lecteur est l'<c>Operator</c> d'à côté, et il <b>meurt avec le <see cref="Case"/></b>.
/// Un champ unique aurait fait choisir entre compter et raconter, et le contrôle aurait dû lire de la
/// prose nominative pour juger une pratique.
/// </para>
/// <para>
/// ⚠️ <b>La règle tient par le placement, jamais par la discipline.</b> C'est le même régime que le
/// texte qui meurt et le texte qui reste : deux champs à deux endroits, dont un seul survit.
/// </para>
/// <para>
/// <b>Elle est réclamée, elle ne barre jamais la route.</b> Un dossier ouvert sans elle est un
/// dossier <b>faible et visible comme tel</b> — voir <see cref="IsDemandedBy"/> — et non un dossier
/// refusé : le service enregistre un fait laid plutôt qu'il ne renvoie la personne à son silence.
/// </para>
/// </remarks>
public sealed record IdentityMotivation
{
  /// <summary>
  /// Le plafond du détail, en unités UTF-16. Une phrase ou deux disant ce qui a été fait — pas le
  /// dossier recopié.
  /// </summary>
  public const int MaxDetailLength = 2000;

  private IdentityMotivation(IdentityVerificationMethod method, string? detail)
  {
    Method = method;
    Detail = detail;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private IdentityMotivation()
  {
    Method = IdentityVerificationMethod.None;
  }

  /// <summary>
  /// La méthode, dans un vocabulaire fermé. <b>Elle se compte, et elle survit</b> : c'est elle, et
  /// elle seule, que l'<c>EvidenceLog</c> garde.
  /// </summary>
  public IdentityVerificationMethod Method { get; private set; }

  /// <summary>
  /// Le détail, en prose libre, ou <c>null</c> quand l'humain n'en a pas écrit. <b>Prose de
  /// travail</b> : nominatif par nature, il vit sur le <see cref="Case"/> et meurt à sa clôture. Il
  /// n'a aucun chemin vers l'<c>EvidenceLog</c>.
  /// </summary>
  public string? Detail { get; private set; }

  /// <summary>
  /// Prend la motivation qu'un humain a écrite. Le détail est <b>accueilli sans être exigé</b> :
  /// exiger de la prose derrière chaque méthode ferait écrire une ligne de rien à chaque dépôt, et le
  /// détail qui compte se noierait dans les autres.
  /// </summary>
  /// <param name="method">La méthode déclarée, <see cref="IdentityVerificationMethod.None"/> comprise.</param>
  /// <param name="detail">Le détail en prose libre, ou rien.</param>
  /// <exception cref="ArgumentNullException"><paramref name="method"/> est absent.</exception>
  /// <exception cref="ArgumentException">Le détail est démesuré ou porte un caractère de contrôle.</exception>
  public static IdentityMotivation Of(IdentityVerificationMethod method, string? detail)
  {
    ArgumentNullException.ThrowIfNull(method);

    return new IdentityMotivation(
      method,
      // Rien à dire n'est pas une chaîne vide, qui se relirait comme un détail qu'on aurait effacé.
      string.IsNullOrWhiteSpace(detail)
        ? null
        : DeclaredText.OrThrow(detail, "Le détail de la motivation", MaxDetailLength, nameof(detail)));
  }

  /// <summary>
  /// La même motivation, <b>amputée de son détail</b> : la méthode survit, la prose meurt. C'est le
  /// geste que la clôture applique à ce champ.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Ce n'est pas un oubli de la méthode.</b> Elle se compte, son lecteur est le contrôle, elle
  /// est déjà au <c>EvidenceLog</c> — la faire disparaître du dossier clos ferait perdre <i>sous quel
  /// régime</i> ce dossier a été instruit, au moment même où l'on veut pouvoir en juger la pratique.
  /// </para>
  /// <para>
  /// <b>Le détail, lui, nomme.</b> Il dit qui a été rappelé et sur quoi ; il n'a aucun chemin vers le
  /// <c>EvidenceLog</c>, et rien ne justifierait qu'il survive à la personne dont il parle.
  /// </para>
  /// <para>
  /// ⚠️ Elle rend un <b>nouvel</b> exemplaire plutôt que d'effacer sur place : le type est un
  /// <c>record</c> immuable, et le <see cref="Case"/> remplace le sien.
  /// </para>
  /// </remarks>
  internal IdentityMotivation WithoutDetail() => new(Method, detail: null);

  /// <summary>
  /// Une motivation est-elle <b>réclamée</b> pour ce droit sous cette déclaration d'identité ?
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>La règle est écrite ici, une seule fois</b>, et lue aux deux endroits qui en ont besoin :
  /// l'écran pour réclamer, la relecture du dossier pour dire qu'elle manque encore. Deux rédactions
  /// finiraient par ne plus dire la même chose, et l'écran mentirait sur ce que le dossier montre.
  /// </para>
  /// <para>
  /// <b>Pourquoi ces deux-là, et pas une exigence générale.</b> Ce qu'on redoute est un <b>accès
  /// accordé à un imposteur</b> : c'est <see cref="DataSubjectRight.Access"/> qui remet des données à
  /// quelqu'un, et ce sont <see cref="IdentityDeclaration.Unverified"/> et
  /// <see cref="IdentityDeclaration.OperatorAttested"/> qui ne reposent sur aucun contrôle du canal.
  /// La croiser avec les cinq autres droits ferait réclamer une motivation à chaque dépôt, et celle
  /// qui compte se noierait dans les autres.
  /// </para>
  /// <para>
  /// ⚠️ Sur bien des installations, <see cref="IdentityDeclaration.ChannelControl"/> est
  /// inatteignable : <c>Unverified</c> + <c>Access</c> y sera le régime <b>courant</b> et non
  /// l'exception. La demande doit donc être tenable au quotidien, ce qui est la raison d'être de
  /// <see cref="IdentityVerificationMethod.None"/> et du détail facultatif.
  /// </para>
  /// </remarks>
  /// <param name="declaration">Ce que le canal d'entrée a déclaré de l'identité du demandeur.</param>
  /// <param name="right">Le droit qu'on s'apprête à ouvrir.</param>
  /// <exception cref="ArgumentNullException">Un argument obligatoire est absent.</exception>
  public static bool IsDemandedBy(IdentityDeclaration declaration, DataSubjectRight right)
  {
    ArgumentNullException.ThrowIfNull(declaration);
    ArgumentNullException.ThrowIfNull(right);

    return declaration.RestsOnNoControl && right == DataSubjectRight.Access;
  }
}
