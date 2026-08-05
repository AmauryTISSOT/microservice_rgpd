namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// Ce que la vérification a constaté d'<b>un</b> <see cref="DeclaredSystem"/> doté d'une adresse
/// d'<c>Adapter</c> : son exposition, et l'accord — ou l'écart — entre les
/// <see cref="Capability"/> déclarées et celles qui sont réellement servies.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le grain est le déploiement, jamais le dossier.</b> Ce type ne connaît aucun <c>Case</c> et
/// n'a aucun emplacement pour en porter un : ce qu'il rapporte vaut pour tous les dossiers à la
/// fois, et l'attacher à l'un d'eux ferait dépendre le volume du signal du nombre de demandes en
/// cours, qui n'en dit rien.
/// </para>
/// <para>
/// <b>Les systèmes sans adresse d'<c>Adapter</c> n'y figurent pas</b>, et c'est le régime
/// majoritaire : il n'y a rien à confronter à un système traité à la main. Leur absence d'ici n'est
/// pas un silence — elle se lit au <c>Manifest</c>, qui les nomme un par un.
/// </para>
/// </remarks>
/// <param name="DeclaredSystem">Le système confronté, tel que le <c>Manifest</c> le nomme.</param>
/// <param name="Exposure">Ce que la sonde à secret délibérément faux a appris de son <c>Adapter</c>.</param>
/// <param name="Capabilities">
/// Les capacités en cause, dans l'ordre du catalogue : celles que le <c>Manifest</c> déclare, et
/// celles que l'<c>Adapter</c> sert sans qu'il les déclare.
/// </param>
public sealed record AdapterVerification(
  DeclaredSystemId DeclaredSystem,
  AdapterExposure Exposure,
  IReadOnlyList<CapabilityVerification> Capabilities)
{
  /// <summary>
  /// Le catalogue et l'<c>Adapter</c> se sont-ils contredits sur ce système ? <b>Un
  /// <see cref="AdapterExposure.Naked"/> n'en est pas un</b> : c'est un fait plus grave et d'une
  /// autre nature, qui se lit à part et ne se laisse pas ranger dans un compte d'écarts.
  /// </summary>
  public bool Disagrees => Capabilities.Any(capability => capability.Agreement.IsDisagreement);
}
