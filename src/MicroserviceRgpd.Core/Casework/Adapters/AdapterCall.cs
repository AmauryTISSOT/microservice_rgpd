namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// Un appel sortant vers un <c>Adapter</c> : <b>une <see cref="Capability"/></b>, exercée sur
/// <b>un</b> <see cref="DeclaredSystem"/>, sous le sac de <see cref="Designation"/> du dossier.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le <c>system_id</c> voyage avec l'adresse, et non dedans.</b> Un même <c>Adapter</c> sert
/// plusieurs systèmes — le système est une unité de recensement, jamais de déploiement — et c'est
/// ce qui garde le découpage du <c>Manifest</c> libre : un client peut déclarer « la boutique », « la
/// messagerie support » et « la reprise de 2019 » là où il n'a qu'une base.
/// </para>
/// <para>
/// <b>Aucun secret ici.</b> Il n'existe aucun emplacement pour en porter un : le secret est de la
/// <b>topologie</b>, un par <c>Adapter</c> et jamais par système, et il vit dans la configuration
/// de déploiement. Ce type ne décrit que ce que l'appel demande.
/// </para>
/// <para>
/// <b>Aucun <c>Case</c> non plus.</b> Ce qui part sur le fil est un sac de désignations, pas un
/// dossier : c'est ce qui rend structurellement vrai qu'<b>un appel refusé ne modifie rien dans le
/// <c>Case</c></b> — cet objet-là n'en connaît aucun.
/// </para>
/// </remarks>
/// <param name="Address">L'adresse de l'<c>Adapter</c>, telle que le <c>Manifest</c> la déclare.</param>
/// <param name="DeclaredSystem">Le système sur lequel on demande à exercer, en paramètre de l'appel.</param>
/// <param name="Capability">Ce qu'on demande d'exercer. Une opération par <c>Capability</c>.</param>
/// <param name="Designations">
/// Le sac sous lequel chercher la personne — éventuellement vide, un appel sous rien n'étant pas
/// une erreur de programmation mais une recherche qui ne trouvera rien, ce que l'<c>Adapter</c> dit
/// mieux que nous.
/// </param>
public sealed record AdapterCall(
  AdapterAddress Address,
  DeclaredSystemId DeclaredSystem,
  Capability Capability,
  IReadOnlyList<Designation> Designations);
