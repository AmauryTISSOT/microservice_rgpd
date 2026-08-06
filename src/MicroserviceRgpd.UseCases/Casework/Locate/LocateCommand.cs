using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.Locate;

/// <summary>
/// Désigner la personne dans les systèmes qui déclarent un <c>Adapter</c> : un <c>Locate</c> par
/// <see cref="DeclaredSystem"/> qui déclare la <see cref="Capability.Locate"/>, sous le sac de
/// <see cref="Designation"/> du dossier.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle part à l'ouverture du dossier, et de nulle part ailleurs.</b> C'est là que se fait la
/// <b>relance</b> d'un <c>202</c> : jamais depuis la file, qui n'émet aucun appel, et jamais par une
/// minuterie. Il n'existe ni compteur de tentatives, ni temporisation, ni abandon automatique, ni
/// escalade — l'<c>Operator</c> n'a jamais cessé d'être le seul à produire une issue.
/// </para>
/// <para>
/// <b>Elle ne barre jamais la route.</b> L'ordre des appels est <b>signifiant</b> — on cherche la
/// personne avant d'affirmer quoi que ce soit sur elle — mais rien n'attend : l'écran s'affiche, le
/// dossier s'instruit, et une panne d'<c>Adapter</c> laisse une ligne « non appelé » plutôt qu'un
/// écran vide.
/// </para>
/// </remarks>
/// <param name="Case">Le dossier au titre duquel les appels partent.</param>
public sealed record LocateCommand(CaseId Case) : ICommand<Result>;
