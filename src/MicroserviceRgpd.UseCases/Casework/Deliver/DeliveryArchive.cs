using System.IO.Compression;
using System.Text;
using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.Deliver;

/// <summary>
/// Range une <see cref="Delivery"/> dans une archive : <b>un dossier par système</b>, et la
/// <c>CoverSheet</c> à la racine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il assemble sans jamais fusionner.</b> Les pièces sont posées côte à côte, telles qu'elles
/// sont arrivées : le service ne sait pas lire ce qu'elles portent — c'est le prix, et le propos,
/// d'une réponse écrite dans le vocabulaire de chaque application — et concaténer deux formats
/// qu'on n'a pas ouverts fabriquerait un fichier que personne ne peut relire.
/// </para>
/// <para>
/// <b>Un dossier par système, nommé par son identifiant.</b> Deux applications peuvent servir deux
/// pièces du même nom ; les mettre à plat en aurait écrasé une, et la personne aurait reçu une
/// réponse incomplète que rien n'aurait signalée — l'<c>Omission silencieuse</c>, fabriquée par une
/// commodité d'assemblage.
/// </para>
/// <para>
/// <b>Même entrée pour une pièce vide.</b> Elle est une réponse datée, et non un silence : la faire
/// disparaître de l'archive ferait passer « interrogé, rien » pour « pas interrogé ».
/// </para>
/// </remarks>
internal static class DeliveryArchive
{
  /// <summary>
  /// Les octets de l'archive, assemblés en mémoire — les données de quelqu'un ne touchent aucun
  /// disque du service en chemin.
  /// </summary>
  /// <param name="delivery">La remise à ranger.</param>
  /// <param name="assembledAt">L'instant de l'assemblage, porté par la page de garde.</param>
  public static byte[] Of(Delivery delivery, DateTimeOffset assembledAt)
  {
    using var bytes = new MemoryStream();

    using (var archive = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
    {
      var page = archive.CreateEntry(CoverSheet.FileName, CompressionLevel.Optimal);
      page.LastWriteTime = assembledAt;

      using (var writing = new StreamWriter(page.Open(), Utf8WithSignature))
      {
        writing.Write(delivery.CoverSheet.Write());
      }

      foreach (var piece in delivery.Pieces)
      {
        // Le dossier porte l'identifiant du système, le fichier le nom que le transport a donné :
        // la personne voit de quelle application vient chaque pièce, sans que le service ait eu à
        // renommer ce qu'il n'a pas ouvert.
        var entry = archive.CreateEntry(
          $"{piece.DeclaredSystem.Value}/{piece.Envelope.FileName}",
          CompressionLevel.Optimal);

        entry.LastWriteTime = piece.RetrievedAt;

        using var writing = entry.Open();
        writing.Write(piece.Content);
      }
    }

    return bytes.ToArray();
  }

  /// <summary>
  /// L'UTF-8 <b>avec sa marque d'ordre</b> : la page de garde nomme des systèmes avec les mots de
  /// leur champ « contient », accents compris, et un bloc-notes qui devine l'encodage les rendrait
  /// illisibles à la personne à qui ils sont écrits.
  /// </summary>
  private static UTF8Encoding Utf8WithSignature { get; } = new(encoderShouldEmitUTF8Identifier: true);
}
