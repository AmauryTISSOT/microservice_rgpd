using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Ce que le service tend à l'<c>Operator</c> au titre d'<b>un</b> <see cref="Claim"/> : la
/// <see cref="DeliveryLetter"/> et les <see cref="RetrievedData"/> de ce droit, rassemblées.
/// </summary>
/// <remarks>
/// <para>
/// <b>Une par <see cref="Claim"/>, jamais une par <see cref="Case"/>.</b> Deux droits sont deux
/// réponses et deux dates de remise, et le service ne sait de toute façon pas fusionner.
/// </para>
/// <para>
/// <b>Il rassemble sans jamais fusionner.</b> Une pièce par <see cref="DeclaredSystem"/>, côte à
/// côte : le service n'ouvre aucun corps, et il ne concatène pas ce qu'il ne sait pas lire.
/// </para>
/// <para>
/// <b>Le service ne tend jamais le paquet à la personne</b> — ni lien à jeton, ni SMTP. Il ne
/// s'expose pas hors du réseau de son client et ne fait confiance à aucune coordonnée que personne
/// n'a vérifiée, l'<see cref="IdentityDeclaration"/> pouvant valoir <c>Unverified</c>.
/// </para>
/// <para>
/// <b>Elle n'est pas persistée, et c'est délibéré.</b> Elle se recompose à chaque geste depuis le
/// dossier, le catalogue et les pièces détenues : la garder aurait fait exister un troisième
/// exemplaire des données de quelqu'un, et daté d'hier un paquet que la remise décrit aujourd'hui.
/// Ce qui survit d'elle est ailleurs — les deux gestes sur le <see cref="Claim"/>, la remise au
/// <c>EvidenceLog</c>.
/// </para>
/// </remarks>
public sealed class Delivery
{
  private Delivery(CaseId caseId, DataSubjectRight right, DeliveryLetter deliveryLetter, IReadOnlyList<RetrievedData> pieces)
  {
    Case = caseId;
    Right = right;
    DeliveryLetter = deliveryLetter;
    Pieces = pieces;
  }

  /// <summary>Le dossier au titre duquel cette remise est faite.</summary>
  public CaseId Case { get; }

  /// <summary>Le droit auquel cette remise répond — <b>un seul</b>, et c'est tout le propos.</summary>
  public DataSubjectRight Right { get; }

  /// <summary>La page que le service écrit lui-même, seul texte du paquet dont il soit l'auteur.</summary>
  public DeliveryLetter DeliveryLetter { get; }

  /// <summary>
  /// Les pièces, <b>côte à côte</b> : une par <see cref="DeclaredSystem"/> qui en a servi une de
  /// pleine, jamais fusionnées ni concaténées.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Ce sont exactement celles que la première liste de la <see cref="DeliveryLetter"/> annonce.</b>
  /// Une pièce vide n'est pas une réponse : la joindre ferait tendre à la personne un fichier de
  /// zéro octet que la page de garde range, elle, parmi les systèmes interrogés sans rattachement —
  /// deux dires contradictoires dans le même envoi. Le fait qu'on ait interrogé ce système est dit
  /// <b>en toutes lettres</b> sur la page, ce qu'un fichier vide n'aurait jamais dit.
  /// </para>
  /// <para>
  /// <b>L'ordre est celui de l'identifiant du système</b>, et non celui du catalogue : deux remises
  /// du même dossier rangent les mêmes pièces dans le même ordre, quoi qu'un humain ait renommé
  /// entre-temps. C'est aussi sous cet identifiant que chaque pièce est rangée dans l'archive.
  /// </para>
  /// </remarks>
  public IReadOnlyList<RetrievedData> Pieces { get; }

  /// <summary>
  /// Rassemble la remise d'un droit : sa page de garde, et les pièces que ce droit a rapportées.
  /// </summary>
  /// <param name="opened">Le dossier, pour ses <see cref="Step"/> et ce que ses appels ont rapporté.</param>
  /// <param name="right">Le droit au titre duquel on remet.</param>
  /// <param name="declaredSystems">
  /// Les <see cref="DeclaredSystem"/> d'aujourd'hui, qui donnent les mots pour nommer les systèmes.
  /// </param>
  /// <param name="held">Les pièces détenues pour ce dossier ; celles des autres droits sont écartées.</param>
  /// <exception cref="ArgumentNullException">Un argument obligatoire est absent.</exception>
  /// <exception cref="ArgumentException">Le dossier ne porte pas ce droit.</exception>
  public static Delivery Of(
    Case opened,
    DataSubjectRight right,
    IEnumerable<DeclaredSystem> declaredSystems,
    IReadOnlyList<RetrievedData> held)
  {
    ArgumentNullException.ThrowIfNull(opened);
    ArgumentNullException.ThrowIfNull(right);
    ArgumentNullException.ThrowIfNull(held);

    // Les pièces PLEINES de ce droit, et elles seules : une pièce vide est une réponse datée, mais
    // ce n'est pas une réponse à joindre — la page de garde la dit en toutes lettres.
    var pieces = held
      .Where(piece => piece.Right == right && !piece.IsEmpty)
      .OrderBy(piece => piece.DeclaredSystem.Value, StringComparer.Ordinal)
      .ToArray();

    return new Delivery(opened.Id, right, DeliveryLetter.Compose(opened, right, declaredSystems, held), pieces);
  }
}
