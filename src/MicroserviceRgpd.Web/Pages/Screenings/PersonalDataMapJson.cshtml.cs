using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Web.Pages.Screenings;

/// <summary>
/// La <c>Cartographie</c> en JSON — celle qui dit d'où elle vient.
/// </summary>
/// <remarks>
/// <b>Elle porte son <c>releve</c> en tête</b> : base, dialecte, moteur, dates et comptes, une seule
/// fois. Le CSV, lui, n'a pas d'en-tête libre où les mettre.
/// </remarks>
/// <param name="mediator">Par où la cartographie se demande.</param>
/// <param name="export">Ce qui la rend en fichier.</param>
public class PersonalDataMapJsonModel(IMediator mediator, IScreeningExport export)
  : PersonalDataMapPage(mediator, export)
{
  /// <inheritdoc />
  protected override Func<IScreeningExport, PersonalDataMap, ExportedFile> Render =>
    static (export, map) => export.AsJson(map);
}
