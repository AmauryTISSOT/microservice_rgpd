using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// La <c>Cartographie</c> en CSV — celle qui s'ouvre d'un double-clic.
/// </summary>
/// <remarks>
/// ⚠️ <b>Elle ne dit de sa provenance que son nom de fichier</b>, et c'est une conséquence sèche
/// assumée : deux exports de deux bases sont indiscernables une fois renommés. Rien n'a été ajouté
/// pour la rattraper — un bloc en tête aurait cassé l'ouverture en tableau, qui est tout l'intérêt
/// de ce format.
/// </remarks>
/// <param name="mediator">Par où la cartographie se demande.</param>
/// <param name="export">Ce qui la rend en fichier.</param>
public class PersonalDataMapCsvModel(IMediator mediator, IScreeningExport export)
  : PersonalDataMapPage(mediator, export)
{
  /// <inheritdoc />
  protected override Func<IScreeningExport, PersonalDataMap, ExportedFile> Render =>
    static (export, map) => export.AsCsv(map);
}
