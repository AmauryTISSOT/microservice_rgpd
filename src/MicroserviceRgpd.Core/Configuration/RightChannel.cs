using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Configuration;

/// <summary>
/// L'état d'un droit dans le <see cref="Settings"/> : le <see cref="DataSubjectRight"/> lui-même —
/// qui porte déjà son libellé français et son article — et l'<see cref="ExerciseChannel"/> par
/// lequel le service l'exercera, <b>« non configuré » compris</b>.
/// </summary>
/// <remarks>
/// ⚠️ <b>Le libellé et l'article ne sont pas recopiés ici</b> : ils se lisent sur le droit, seule
/// source de vérité (SharedKernel). Ce type ne porte que ce que le SmartEnum ne sait pas — le canal
/// choisi par l'intégrateur — et laisse le reste là où il est écrit une fois.
/// <para>
/// <b>Un droit sans canal est un état, jamais un manque.</b> « Non configuré » est le régime d'un
/// service qu'on vient d'installer — pas une déclaration inachevée —, et c'est un cas du canal, pas
/// un <c>null</c>.
/// </para>
/// </remarks>
/// <param name="Right">Le droit RGPD, qui porte son libellé français et son article.</param>
/// <param name="Channel">Le canal par lequel exercer ce droit — une adresse, un routage, ou « non configuré ».</param>
public sealed record RightChannel(DataSubjectRight Right, ExerciseChannel Channel)
{
  /// <summary>Ce droit porte-t-il un canal ? Sinon, il est « non configuré ».</summary>
  public bool IsConfigured => Channel is not ExerciseChannel.NotConfigured;
}
