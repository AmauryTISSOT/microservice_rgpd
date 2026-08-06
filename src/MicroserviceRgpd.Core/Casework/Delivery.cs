using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Ce que le service tend à l'<c>Operator</c> au titre d'<b>un</b> <see cref="Claim"/> : la
/// <see cref="CoverSheet"/> et les <see cref="RetrievedData"/> de ce droit, rassemblées.
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
/// <c>Ledger</c>.
/// </para>
/// </remarks>
public sealed class Delivery
{
  private Delivery(CaseId caseId, DataSubjectRight right, CoverSheet coverSheet, IReadOnlyList<RetrievedData> pieces)
  {
    Case = caseId;
    Right = right;
    CoverSheet = coverSheet;
    Pieces = pieces;
  }

  /// <summary>Le dossier au titre duquel cette remise est faite.</summary>
  public CaseId Case { get; }

  /// <summary>Le droit auquel cette remise répond — <b>un seul</b>, et c'est tout le propos.</summary>
  public DataSubjectRight Right { get; }

  /// <summary>La page que le service écrit lui-même, seul texte du paquet dont il soit l'auteur.</summary>
  public CoverSheet CoverSheet { get; }

  /// <summary>
  /// Les pièces, <b>côte à côte</b> et dans l'ordre du catalogue : une par <see cref="DeclaredSystem"/>
  /// qui en a servi une, jamais fusionnées ni concaténées.
  /// </summary>
  public IReadOnlyList<RetrievedData> Pieces { get; }

  /// <summary>
  /// Rassemble la remise d'un droit : sa page de garde, et les pièces que ce droit a rapportées.
  /// </summary>
  /// <param name="opened">Le dossier, pour ses <see cref="Step"/> et ce que ses appels ont rapporté.</param>
  /// <param name="right">Le droit au titre duquel on remet.</param>
  /// <param name="manifest">Le catalogue d'aujourd'hui, qui donne les mots pour nommer les systèmes.</param>
  /// <param name="held">Les pièces détenues pour ce dossier ; celles des autres droits sont écartées.</param>
  /// <exception cref="ArgumentNullException">Un argument obligatoire est absent.</exception>
  /// <exception cref="ArgumentException">Le dossier ne porte pas ce droit.</exception>
  public static Delivery Of(
    Case opened,
    DataSubjectRight right,
    Manifest manifest,
    IReadOnlyList<RetrievedData> held)
  {
    ArgumentNullException.ThrowIfNull(opened);
    ArgumentNullException.ThrowIfNull(right);
    ArgumentNullException.ThrowIfNull(held);

    var pieces = held
      .Where(piece => piece.Right == right)
      .OrderBy(piece => piece.DeclaredSystem.Value, StringComparer.Ordinal)
      .ToArray();

    return new Delivery(opened.Id, right, CoverSheet.Compose(opened, right, manifest, held), pieces);
  }
}
