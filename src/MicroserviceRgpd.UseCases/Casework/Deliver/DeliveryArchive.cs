using System.Globalization;
using System.IO.Compression;
using System.Text;
using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.Deliver;

/// <summary>
/// Une <see cref="Delivery"/> rangée dans une archive : <b>un dossier par système</b>, et la
/// <c>DeliveryLetter</c> à la racine. C'est ce que l'<c>Operator</c> télécharge.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle assemble sans jamais fusionner.</b> Les pièces sont posées côte à côte, telles qu'elles
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
/// <b>Elle n'est jamais gardée.</b> Elle est assemblée en mémoire à chaque geste depuis les pièces
/// détenues : l'entreposer aurait fait un second exemplaire des données de quelqu'un, dont
/// l'effacement serait devenu une seconde chose à ne pas oublier.
/// </para>
/// <para>
/// <b>Le service ne la remet à personne.</b> Il la tend à l'<c>Operator</c>, qui la remettra par le
/// canal dont il répond. Aucun lien à jeton, aucun SMTP : le jour où le service enverrait lui-même,
/// il deviendrait comptable d'une adresse qu'il n'a pas vérifiée.
/// </para>
/// </remarks>
/// <param name="FileName">Le nom sous lequel le navigateur l'enregistre.</param>
/// <param name="Content">Les octets de l'archive.</param>
public sealed record DeliveryArchive(string FileName, byte[] Content)
{
  /// <summary>Le type de contenu de l'archive. Une constante, le service n'assemblant qu'un format.</summary>
  public const string ContentType = "application/zip";

  /// <summary>
  /// Range la remise, et la nomme.
  /// </summary>
  /// <param name="delivery">La remise à ranger.</param>
  /// <param name="assembledAt">L'instant de l'assemblage, porté par la page de garde et par le nom.</param>
  /// <exception cref="ArgumentNullException"><paramref name="delivery"/> est absent.</exception>
  public static DeliveryArchive Of(Delivery delivery, DateTimeOffset assembledAt)
  {
    ArgumentNullException.ThrowIfNull(delivery);

    return new DeliveryArchive(NameOf(delivery, assembledAt), BytesOf(delivery, assembledAt));
  }

  /// <summary>
  /// Les octets, assemblés en mémoire — les données de quelqu'un ne touchent aucun disque du service
  /// en chemin.
  /// </summary>
  private static byte[] BytesOf(Delivery delivery, DateTimeOffset assembledAt)
  {
    using var bytes = new MemoryStream();

    using (var archive = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true))
    {
      var page = archive.CreateEntry(DeliveryLetter.FileName, CompressionLevel.Optimal);
      page.LastWriteTime = assembledAt;

      using (var writing = new StreamWriter(page.Open(), Utf8WithSignature))
      {
        writing.Write(delivery.DeliveryLetter.Write());
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
  /// Le nom du fichier : le droit, le jour, et le dossier. <b>Aucun nom de personne concernée</b> —
  /// le dossier est anonyme côté personne, et le nom d'un fichier voyage sur des machines dont le
  /// service ne répond pas.
  /// </summary>
  private static string NameOf(Delivery delivery, DateTimeOffset assembledAt)
  {
    var day = assembledAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    return $"remise-{delivery.Right.Name.ToLowerInvariant()}-{day}-{delivery.Case.Value}.zip";
  }

  /// <summary>
  /// L'UTF-8 <b>avec sa marque d'ordre</b> : la page de garde nomme des systèmes avec les mots de
  /// leur champ « contient », accents compris, et un bloc-notes qui devine l'encodage les rendrait
  /// illisibles à la personne à qui ils sont écrits.
  /// </summary>
  private static UTF8Encoding Utf8WithSignature { get; } = new(encoderShouldEmitUTF8Identifier: true);
}
