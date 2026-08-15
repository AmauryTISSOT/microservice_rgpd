using MicroserviceRgpd.Core.Casework.Adapters;

namespace MicroserviceRgpd.UseCases.Casework.VerifyManifest;

/// <summary>
/// Confronter le catalogue déclaré à ce que les <c>Adapter</c> servent réellement, et rapporter
/// l'écart.
/// </summary>
/// <remarks>
/// <para>
/// <b>C'est une opération d'exploitation, pas une lecture de dossier.</b> Elle ne prend aucun
/// paramètre : il n'y a rien à filtrer — un catalogue est un paysage de quelques systèmes — et
/// vérifier « seulement celui-là » serait le premier pas vers une vérification qu'on croit avoir
/// faite en entier.
/// </para>
/// <para>
/// <b>Elle appelle</b>, et c'est ce qui la distingue de toute autre requête du dépôt : des
/// aller-retours partent vers les applications du client. Ils sont non destructeurs, sans
/// désignation, et au plancher — voir <see cref="IAdapterProbes"/>.
/// </para>
/// </remarks>
public sealed record VerifyManifestQuery : IQuery<ManifestVerification>;
