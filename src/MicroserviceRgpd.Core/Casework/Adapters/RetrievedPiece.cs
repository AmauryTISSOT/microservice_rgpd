namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// Ce qu'un <c>Read</c> servi a rendu : une <see cref="TransportEnvelope"/>, et un <b>flux
/// d'octets</b> que le service ne lit pas.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aucune forme n'est imposée à l'<c>Adapter</c>.</b> Il n'existe ici ni schéma, ni liste, ni
/// champ attendu : le corps est écrit dans le vocabulaire de l'application, et le service n'a que
/// l'enveloppe pour en dire quoi que ce soit. C'est ce qui laisse au client — et à lui seul — le
/// périmètre matériel de l'art. 20, plus étroit que celui de l'art. 15 et qui se tranche ligne par
/// ligne.
/// </para>
/// <para>
/// <b>Trois cas se distinguent par la seule enveloppe, et jamais par le corps.</b> Une pièce
/// <b>pleine</b> — l'<c>Adapter</c> a servi des octets ; une pièce <b>vide</b> — il a servi, et il
/// n'y avait rien, ce qui est une déclaration datée et non un silence ; une pièce <b>absente</b> —
/// il a différé, refusé, ou n'a pas été appelé, et rien n'est gardé. C'est ce qui rend
/// l'<b>incomplétude gratuite</b> : dire « rien » ne coûte pas à l'<c>Adapter</c> de fabriquer un
/// export vide d'une forme convenue.
/// </para>
/// <para>
/// ⚠️ <b>Le service ne saura donc jamais</b> qu'un <c>Adapter</c> a servi du PDF pour une
/// portabilité, ni qu'il a ignoré le <c>DataSubjectRight</c> de l'appel. Faute réelle, silencieuse,
/// imputable au client — même régime que le <c>Step</c> <c>Done</c> déclaré et jamais vérifié.
/// </para>
/// </remarks>
/// <param name="Envelope">Ce que le transport a mis dans la main du service, recopié sans être lu.</param>
/// <param name="Content">
/// Les octets, tels qu'ils sont arrivés. <b>Éventuellement vides</b> — c'est la pièce vide, qui est
/// une réponse.
/// </param>
public sealed record RetrievedPiece(TransportEnvelope Envelope, byte[] Content);
