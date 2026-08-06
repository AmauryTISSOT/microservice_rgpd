using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.Read;

/// <summary>
/// Lire les données de la personne là où le dossier en a <b>rattaché</b> : un <c>Read</c> par
/// (<see cref="Claim"/>, <see cref="DeclaredSystem"/>) déclarant la <see cref="Capability.Read"/>,
/// portant le <c>DataSubjectRight</c> <b>au titre duquel</b> on lit.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle vient après <c>Locate</c>, et la dépendance est réelle.</b> On ne lit que là où l'on a
/// trouvé : lire un système où rien n'a été rattaché ferait demander à l'application les données
/// d'une personne dont elle vient de dire qu'elle ne la connaît pas — et ce que l'<c>Adapter</c>
/// rendrait alors, quel qu'il soit, n'aurait plus de sujet.
/// </para>
/// <para>
/// <b>Elle part à l'ouverture du dossier, et de nulle part ailleurs</b>, comme <c>Locate</c> : c'est
/// là que se fait la relance d'un <c>202</c>, jamais depuis la file et jamais par une minuterie.
/// </para>
/// <para>
/// <b>Aucune forme n'est demandée à l'<c>Adapter</c>.</b> Ce qui part est le droit ; ce qui revient
/// est un flux d'octets dont le service ne connaîtra que l'enveloppe de transport.
/// </para>
/// </remarks>
/// <param name="Case">Le dossier au titre duquel les appels partent.</param>
public sealed record ReadCommand(CaseId Case) : ICommand<Result>;
