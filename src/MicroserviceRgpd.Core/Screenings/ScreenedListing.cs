namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Ce qu'un <see cref="IScreeningEngine"/> rend : une <see cref="ScreenedColumn"/> par colonne du
/// relevé, <b>dans l'ordre du relevé</b>, et l'identité du moteur qui les a produites.
/// </summary>
/// <remarks>
/// <para>
/// <b>L'identité voyage avec les lignes, elle ne se lit pas à côté.</b> Le glossaire dit que c'est
/// le nom et la version que le moteur <b>joint</b> au rapport qu'il a produit : un moteur servi
/// apprend la version qu'on lui sert au moment où il répond, et une propriété posée à côté de
/// l'appel dirait la version configurée plutôt que celle qui a répondu.
/// </para>
/// <para>
/// ⚠️ <b>Ce n'est pas encore un <see cref="Screening"/>.</b> Il manque ce que le moteur n'a pas à
/// décider : l'identité du rapport, l'instant du lancement, le nom de base et le dialecte que le
/// relevé déclare, et l'<see cref="ListingOrigin"/> par laquelle il est entré. C'est le geste qui
/// assemble, pas le moteur — et le moteur, lui, <b>ne sait pas</b> lequel des deux chemins il lit.
/// </para>
/// </remarks>
/// <param name="Engine">Qui a détecté, et dans quelle version. Le domaine ne l'interprète jamais.</param>
/// <param name="Columns">Une ligne par colonne du relevé, dans l'ordre du relevé.</param>
public sealed record ScreenedListing(ScreeningEngineIdentity Engine, IReadOnlyList<ScreenedColumn> Columns);
