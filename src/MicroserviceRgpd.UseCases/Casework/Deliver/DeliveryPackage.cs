namespace MicroserviceRgpd.UseCases.Casework.Deliver;

/// <summary>
/// Le fichier que l'<c>Operator</c> télécharge : les pièces <b>côte à côte</b> et la
/// <c>CoverSheet</c> qui les énumère, dans une archive.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il n'est jamais gardé.</b> L'archive est assemblée à chaque geste à partir des pièces
/// détenues : l'entreposer aurait fait un second exemplaire des données de quelqu'un, dont
/// l'effacement serait devenu une seconde chose à ne pas oublier.
/// </para>
/// <para>
/// <b>Le service ne le remet à personne.</b> Il le tend à l'<c>Operator</c>, qui le remettra par le
/// canal dont il répond. Aucun lien à jeton, aucun SMTP : le jour où le service enverrait lui-même,
/// il deviendrait comptable d'une adresse qu'il n'a pas vérifiée.
/// </para>
/// </remarks>
/// <param name="FileName">Le nom sous lequel le navigateur l'enregistre.</param>
/// <param name="Content">Les octets de l'archive.</param>
public sealed record DeliveryPackage(string FileName, byte[] Content)
{
  /// <summary>Le type de contenu de l'archive. Une constante, le service n'assemblant qu'un format.</summary>
  public const string ContentType = "application/zip";
}
