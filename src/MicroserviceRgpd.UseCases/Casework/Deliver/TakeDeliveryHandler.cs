using System.Globalization;
using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.Deliver;

/// <summary>
/// Compose la <see cref="Delivery"/> d'un droit, la range dans une archive, et note sur le
/// <c>Claim</c> qu'elle a été prise.
/// </summary>
/// <remarks>
/// <para>
/// <b>Rien n'est consigné, rien n'est détruit.</b> Ce handler ne touche ni le <c>Ledger</c> ni les
/// pièces : c'est ce qui distingue les deux gestes, et les réunir aurait daté la preuve à l'instant
/// où quelqu'un vérifiait.
/// </para>
/// <para>
/// <b>La <c>Delivery</c> est recomposée à chaque passage, jamais gardée.</b> Le catalogue vieillit
/// exprès, et une archive entreposée aurait fait un second exemplaire des données de quelqu'un.
/// </para>
/// <para>
/// <b>Une remise déjà déclarée ne se retélécharge pas.</b> Les pièces sont détruites : l'archive ne
/// porterait plus que la page de garde, et la tendre laisserait croire que le système n'avait rien
/// rendu ce jour-là.
/// </para>
/// </remarks>
/// <param name="cases">Le seul dépôt de ce contexte : les règles sont écrites une fois, sur la racine.</param>
/// <param name="manifest">Le catalogue, relu à chaque passage — il nomme les systèmes non couverts.</param>
/// <param name="retrieved">Les pièces détenues, hors de l'agrégat.</param>
/// <param name="clock">L'horloge, injectée pour que l'instant du geste se dicte en test.</param>
public sealed class TakeDeliveryHandler(
  IRepository<Case> cases,
  IReadRepository<DeclaredSystem> manifest,
  IRetrievedData retrieved,
  TimeProvider clock)
  : ICommandHandler<TakeDeliveryCommand, Result<DeliveryPackage>>
{
  /// <inheritdoc />
  public async ValueTask<Result<DeliveryPackage>> Handle(
    TakeDeliveryCommand command,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var opened = await cases.FirstOrDefaultAsync(new CaseByIdSpec(command.Case), cancellationToken);

    if (opened is null)
    {
      return Result<DeliveryPackage>.NotFound();
    }

    var claim = opened.Claims.SingleOrDefault(one => one.Right == command.Right);

    if (claim is null || claim.DeliveryDeclaredOn is not null)
    {
      // Le dossier ne porte pas ce droit, ou sa remise est déjà déclarée. Ce n'est pas une
      // programmation fautive : un écran affiché il y a une minute peut nommer un geste qu'un autre
      // vient de faire.
      return Result<DeliveryPackage>.NotFound();
    }

    var takenAt = clock.GetUtcNow();

    var delivery = Delivery.Of(
      opened,
      command.Right,
      Manifest.Of(await manifest.ListAsync(cancellationToken)),
      await retrieved.HeldForAsync(command.Case, cancellationToken));

    // L'archive est assemblée AVANT que le dossier ne bouge : noter un téléchargement dont
    // l'assemblage aurait échoué ferait remonter dans la file une remise que personne n'a eue.
    var package = new DeliveryPackage(NameOf(delivery, takenAt), DeliveryArchive.Of(delivery, takenAt));

    if (opened.TakeDelivery(command.Right, takenAt))
    {
      await cases.UpdateAsync(opened, cancellationToken);
    }

    return Result<DeliveryPackage>.Success(package);
  }

  /// <summary>
  /// Le nom du fichier : le droit, le jour, et le dossier. <b>Aucun nom de personne concernée</b> —
  /// le dossier est anonyme côté personne, et le nom d'un fichier voyage sur des machines dont le
  /// service ne répond pas.
  /// </summary>
  private static string NameOf(Delivery delivery, DateTimeOffset takenAt)
  {
    var day = takenAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    return $"remise-{delivery.Right.Name.ToLowerInvariant()}-{day}-{delivery.Case.Value}.zip";
  }
}
