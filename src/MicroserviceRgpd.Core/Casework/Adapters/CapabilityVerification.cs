namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// Une <see cref="Capability"/>, et ce que la vérification en a constaté.
/// </summary>
/// <param name="Capability">La capacité en cause, déclarée au catalogue ou servie par l'<c>Adapter</c>.</param>
/// <param name="Agreement">Ce que le catalogue et l'<c>Adapter</c> en disent, ensemble.</param>
public sealed record CapabilityVerification(Capability Capability, CapabilityAgreement Agreement);
