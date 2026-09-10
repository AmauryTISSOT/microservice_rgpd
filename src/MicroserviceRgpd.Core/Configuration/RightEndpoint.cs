using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Configuration;

/// <summary>
/// L'état d'un droit dans le <see cref="Settings"/> : le <see cref="DataSubjectRight"/> lui-même —
/// qui porte déjà son libellé français et son article —, et l'<see cref="EndpointUrl"/> à laquelle
/// le service l'exercera, <b>ou son absence</b>.
/// </summary>
/// <remarks>
/// ⚠️ <b>Le libellé et l'article ne sont pas recopiés ici</b> : ils se lisent sur le droit, seule
/// source de vérité (SharedKernel). Ce type ne porte que ce que le SmartEnum ne sait pas — l'adresse
/// choisie par l'intégrateur — et laisse le reste là où il est écrit une fois.
/// <para>
/// <b>Un endpoint absent est un état, jamais un manque.</b> Un droit sans adresse est « non
/// configuré », et c'est le régime d'un service qu'on vient d'installer — pas une déclaration
/// inachevée.
/// </para>
/// </remarks>
/// <param name="Right">Le droit RGPD, qui porte son libellé français et son article.</param>
/// <param name="Endpoint">L'adresse à laquelle exercer ce droit, ou <c>null</c> — « non configuré ».</param>
public sealed record RightEndpoint(DataSubjectRight Right, EndpointUrl? Endpoint)
{
  /// <summary>Ce droit porte-t-il une adresse ? Sinon, il est « non configuré ».</summary>
  public bool IsConfigured => Endpoint is not null;
}
