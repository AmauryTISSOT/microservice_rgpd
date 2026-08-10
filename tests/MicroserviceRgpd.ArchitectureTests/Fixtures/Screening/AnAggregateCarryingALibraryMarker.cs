using Ardalis.SharedKernel;

namespace MicroserviceRgpd.ArchitectureTests.Fixtures.Screening;

/// <summary>
/// Le témoin du faux positif : un agrégat de <c>Screening</c> qui implémente le marqueur
/// <c>IAggregateRoot</c>, lequel vit dans le paquet <c>Ardalis.SharedKernel</c>.
/// <para>
/// ⚠️ <b>Ce n'est pas une traversée vers le noyau partagé du dépôt</b>, qui est
/// <c>MicroserviceRgpd.Core.SharedKernel</c> et ne contient que <c>DataSubjectRight</c>. Sans ce
/// témoin, l'inspecteur redevient un jour incapable de faire la différence, et le premier agrégat du
/// troisième contexte se dénonce pour avoir utilisé la bibliothèque que les deux autres utilisent.
/// </para>
/// </summary>
internal sealed class AnAggregateCarryingALibraryMarker : IAggregateRoot
{
  internal string Name => nameof(AnAggregateCarryingALibraryMarker);
}
