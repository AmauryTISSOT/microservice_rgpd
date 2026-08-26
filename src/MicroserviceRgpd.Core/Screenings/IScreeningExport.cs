namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Ce qui rend une <see cref="PersonalDataMap"/> en <b>fichier</b> : le JSON pour qui outille, le
/// CSV pour qui double-clique.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce ne sont pas deux vues d'un même lecteur : ce sont deux lecteurs.</b> Le JSON dit d'où il
/// vient et garde l'ISO 8601 complet ; le CSV n'a pas d'en-tête libre et ses horodatages perdent
/// leur décalage, seule forme qu'un tableur reconnaisse comme une date. Fondre les deux en un aurait
/// obligé l'un des deux destinataires à s'accommoder du format de l'autre.
/// </para>
/// <para>
/// <b>C'est un port, et le rendu vit dans l'assemblage d'infrastructure.</b> Ce qui le consomme —
/// deux <c>PageModel</c> — ne doit apprendre ni comment un CSV se cite, ni qu'un BOM existe.
/// </para>
/// <para>
/// ⚠️ <b>Il ne lit rien et n'écrit nulle part.</b> Il reçoit la cartographie déjà calculée et rend
/// des octets : aucun dépôt, aucune horloge, aucune trace. C'est ce qui garde vraie la ligne qui
/// sépare cet export du pont interdit vers le <c>Manifest</c> — <b>l'export a un destinataire humain
/// qui l'a demandé, le pont a un destinataire machine que personne n'a demandé</b> —, et ce qui doit
/// arrêter une revue de code est l'écriture côté <c>Casework</c>, jamais le mot « export ».
/// </para>
/// </remarks>
public interface IScreeningExport
{
  /// <summary>La cartographie en JSON, telle qu'un outil la lit.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="map"/> est absent.</exception>
  ExportedFile AsJson(PersonalDataMap map);

  /// <summary>La cartographie en CSV, telle qu'un tableur français l'ouvre.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="map"/> est absent.</exception>
  ExportedFile AsCsv(PersonalDataMap map);
}

/// <summary>
/// Un fichier prêt à partir : son nom, son type de contenu, ses octets.
/// </summary>
/// <remarks>
/// ⚠️ <b>Le nom n'est pas décoratif, il est tout ce que le CSV dit de sa provenance.</b> Le CSV n'a
/// pas d'en-tête libre et rien n'a été ajouté pour lui en fabriquer un : deux exports de deux bases
/// sont indiscernables une fois renommés, et le moteur qui a produit un CSV est irrécupérable depuis
/// ce CSV. C'est une conséquence sèche, consignée pour n'être pas redécouverte.
/// </remarks>
/// <param name="Name">Le nom sous lequel le navigateur l'enregistre.</param>
/// <param name="ContentType">Ce que l'en-tête <c>Content-Type</c> annonce.</param>
/// <param name="Content">Les octets, BOM compris le cas échéant.</param>
public sealed record ExportedFile(string Name, string ContentType, byte[] Content);
