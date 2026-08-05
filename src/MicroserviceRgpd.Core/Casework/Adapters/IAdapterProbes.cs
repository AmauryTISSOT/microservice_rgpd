namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// Les deux sondes non destructrices par lesquelles le service confronte le <c>Manifest</c> à ce
/// qu'un <c>Adapter</c> sert réellement.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucune sonde ne peut exercer <see cref="Capability.Erase"/> ni
/// <see cref="Capability.Rectify"/>, et la règle tient par la forme.</b> Ce port ne reçoit pas de
/// <see cref="Capability"/> : les deux méthodes portent un <see cref="Capability.Locate"/> écrit
/// dans leur nom, et il n'existe ici aucun emplacement où glisser autre chose. Un port qui aurait
/// accepté une capacité en paramètre aurait fait dépendre la promesse « non destructeur » de la
/// vigilance de chaque appelant — c'est-à-dire d'une relecture, sur la seule surface du dispositif
/// où une erreur détruit des données réelles.
/// </para>
/// <para>
/// <b>Le sac de désignations est vide, et il n'est pas un paramètre.</b> Une vérification cherche à
/// savoir si l'<c>Adapter</c> répond, jamais qui il connaît : y faire voyager une personne réelle
/// enverrait une désignation chez un client au titre d'aucun dossier. Le contrat le prévoit — un sac
/// vide est une recherche qui ne trouvera rien, et ce n'est pas une erreur.
/// </para>
/// <para>
/// <b>Aucune sonde ne lit le corps de ce qu'elle reçoit.</b> Le statut porte tout ce qu'on vient
/// chercher, et le corps d'un <c>200</c> rendu à un secret faux est précisément la fuite que la
/// sonde vient constater.
/// </para>
/// <para>
/// ⚠️ <b>Ce port ne connaît aucun <c>Case</c></b>, et ce qu'il rapporte n'entre dans aucun
/// <c>Ledger</c> : une vérification est un fait du déploiement, et la consigner dossier par dossier
/// ferait dépendre la preuve d'une opération d'exploitation.
/// </para>
/// </remarks>
public interface IAdapterProbes
{
  /// <summary>
  /// Envoie un <see cref="Capability.Locate"/> sous le secret du déploiement, et rapporte ce que
  /// l'<c>Adapter</c> a répondu — servi, différé, ou l'un des deux refus.
  /// </summary>
  /// <remarks>
  /// C'est le <b>plancher</b>, et la seule capacité qu'une vérification sache exercer : une requête,
  /// et rien de destructeur.
  /// </remarks>
  /// <param name="address">L'adresse de l'<c>Adapter</c>, telle que le <c>Manifest</c> la déclare.</param>
  /// <param name="declaredSystem">Le système sur lequel on demande à localiser.</param>
  /// <param name="cancellationToken">L'annulation de l'échange en cours.</param>
  /// <exception cref="AdapterFailure">L'<c>Adapter</c> n'a rendu ni réponse ni refus.</exception>
  Task<AdapterOutcome> AskLocateAsync(
    AdapterAddress address,
    DeclaredSystemId declaredSystem,
    CancellationToken cancellationToken = default);

  /// <summary>
  /// Envoie un <see cref="Capability.Locate"/> sous un secret <b>délibérément faux</b>, et rapporte
  /// si l'<c>Adapter</c> s'est laissé faire.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Le secret envoyé n'a aucun rapport avec celui du déploiement</b> — il n'en est ni dérivé, ni
  /// un préfixe, ni une variante. Un secret faux fabriqué à partir du vrai le livrerait, octet par
  /// octet, à l'<c>Adapter</c> même dont on soupçonne qu'il ne garde rien.
  /// </para>
  /// <para>
  /// <b>Un <c>200</c> est le seul résultat qui démontre quelque chose</b> : l'<c>Adapter</c> a
  /// travaillé pour un appelant qu'il aurait dû refuser, et il est nu.
  /// </para>
  /// </remarks>
  /// <param name="address">L'adresse de l'<c>Adapter</c>, telle que le <c>Manifest</c> la déclare.</param>
  /// <param name="declaredSystem">Le système sur lequel la sonde frappe.</param>
  /// <param name="cancellationToken">L'annulation de l'échange en cours.</param>
  Task<AdapterExposure> ProbeWithAFalseSecretAsync(
    AdapterAddress address,
    DeclaredSystemId declaredSystem,
    CancellationToken cancellationToken = default);
}
