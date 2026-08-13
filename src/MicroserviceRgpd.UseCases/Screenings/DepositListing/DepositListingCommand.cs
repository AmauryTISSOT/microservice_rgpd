using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.DepositListing;

/// <summary>
/// Déposer un <c>ColumnListing</c> et obtenir son rapport — <b>d'un seul geste synchrone</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Synchrone sans exception, et le <c>202</c> de l'<c>Adapter</c> ne se transpose pas ici.</b>
/// Ce vocabulaire-là dit que <em>le système du client</em> diffère. Le reprendre demanderait une
/// file, un ouvrier de fond et un état que l'<c>Operator</c> sonde : très exactement « quelque chose
/// qui tourne », avec le mode de panne qui va avec — un dépistage interrompu rendant un rapport
/// <b>vide et rassurant</b>. Le moteur rend toutes les colonnes, le rapport est écrit, puis il
/// s'affiche.
/// </para>
/// <para>
/// <b>Le caractère synchrone est retourné en contrainte plutôt que subi</b> : le geste entier tient
/// dans un budget de 10 s au pire cas autorisé — lire le collage, éprouver les neuf refus, dépister,
/// écrire. Il ne couvre pas le seul dépistage : un moteur tenant 9,5 s aurait passé une barre
/// annoncée à 10 pendant que le geste réel en prenait 12.
/// </para>
/// <para>
/// ⚠️ <b>Le collage entre tel quel, non découpé.</b> Les neuf refus vivent dans le domaine, où le
/// contrat de format est écrit ; les loger dans un validateur de formulaire les mettrait dans la
/// couche web, où une seconde surface devrait les réécrire.
/// </para>
/// <para>
/// ⚠️ <b>Elle rend l'identité du rapport, jamais le rapport.</b> Toute réponse qui <em>rend</em> un
/// <c>Screening</c> doit porter la <c>Clause d'incomplétude</c> ; le dépôt, lui, n'a rien à rendre
/// qu'un <c>Operator</c> lise — il redirige vers la lecture, qui la porte. Rendre l'agrégat ici
/// aurait ouvert un second chemin vers un rapport, sans clause, pour une commodité nulle.
/// </para>
/// </remarks>
/// <param name="Paste">
/// Le relevé pivot, tel que l'<c>Operator</c> l'a collé. <c>null</c> et le vide sont des collages
/// comme les autres : ils se refusent par le contrat de format, jamais par une garde en amont.
/// </param>
public sealed record DepositListingCommand(string? Paste) : ICommand<Result<ScreeningId>>
{
  /// <summary>
  /// Le plafond d'octets <b>du geste</b>, mesuré sur le collage lui-même en UTF-8.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Le plafond est ici, et non au transport seul, parce qu'un refus de transport est
  /// muet.</b> Un <c>413</c> nu ne porte ni cas, ni phrase, ni l'écran de dépôt : il ne dit pas à
  /// l'<c>Operator</c> que c'est le <em>poids</em> qui a refusé son relevé, et il ne lui dit surtout
  /// pas que rien n'a été ingéré. Le plafond du transport est donc posé <b>au-dessus</b> de
  /// celui-ci, pour que le refus qui sort soit toujours celui qui parle français.
  /// </para>
  /// <para>
  /// <b>8 Mo est deux fois le pire cas autorisé</b> : vingt mille colonnes — le plafond de
  /// <see cref="ColumnListing.MaxColumns"/> — pèsent environ 4 Mo. Un collage qui franchit celui-ci
  /// sans franchir celui-là porte donc des noms ou des commentaires démesurés, et c'est un relevé
  /// qu'on refuse plutôt qu'un relevé qu'on tronque.
  /// </para>
  /// </remarks>
  public const long MaxPasteBytes = 8L * 1024 * 1024;
}
