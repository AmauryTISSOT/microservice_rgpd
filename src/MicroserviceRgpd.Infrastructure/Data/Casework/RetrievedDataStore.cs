using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;

namespace MicroserviceRgpd.Infrastructure.Data.Casework;

/// <summary>
/// Garde une pièce, et rend celles d'un dossier. <b>Rien d'autre</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il ne passe pas par le dépôt générique</b>, contraint aux agrégats racines : l'emprunter aurait
/// déclaré agrégat ce qui est hors de l'agrégat par construction, et fait dépendre l'effacement
/// d'une pièce de la mise à jour d'une racine qui lui survit.
/// </para>
/// <para>
/// <b>Garder remplace, et le remplacement est un geste explicite.</b> Une relecture sous un sac
/// enrichi rend une autre pièce pour le même (dossier, droit, système) ; l'empiler ferait deux
/// réponses dues à la personne sur la même question, et laisserait dans le service un exemplaire de
/// plus des données de quelqu'un — c'est-à-dire l'inverse exact du séjour minimal qu'on lui doit.
/// </para>
/// <para>
/// <b>Aucun <c>DbSet</c> ne l'expose sur le contexte</b>, comme pour le <c>Ledger</c> : EF Core
/// connaît la table par sa configuration d'entité, et les gestes disponibles sur ces octets sont
/// ceux de ce fichier, pas ceux que tout porteur du contexte se serait trouvé avoir.
/// </para>
/// </remarks>
public sealed class RetrievedDataStore(AppDbContext dbContext) : IRetrievedData
{
  /// <inheritdoc />
  public async Task KeepAsync(RetrievedData piece, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(piece);

    var held = await dbContext.Set<RetrievedData>()
      .FirstOrDefaultAsync(
        one => one.Case == piece.Case && one.Right == piece.Right && one.DeclaredSystem == piece.DeclaredSystem,
        cancellationToken);

    if (held is null)
    {
      dbContext.Set<RetrievedData>().Add(piece);
    }
    else
    {
      // La pièce d'aujourd'hui prend la place de celle d'hier **sur la ligne d'hier** : les deux
      // portent la même identité — le triplet est la clé —, et détruire puis recréer aurait demandé
      // deux écritures pour une identité qui ne change pas, en laissant entre elles un instant où la
      // personne n'a plus ni l'ancienne pièce ni la nouvelle.
      held.Replace(new RetrievedPiece(piece.Envelope, piece.Content), piece.RetrievedAt);
    }

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  /// <inheritdoc />
  public async Task<IReadOnlyList<RetrievedData>> HeldForAsync(
    CaseId caseId,
    CancellationToken cancellationToken = default)
  {
    // L'ordre est celui de la taxonomie des droits puis du catalogue, et non celui de l'arrivée : un
    // écran ordonné par l'ordre où des appels ont répondu changerait d'un passage à l'autre sans que
    // rien n'ait bougé. Il se fait en mémoire — la colonne porte le NOM du droit, et trier dessus
    // rangerait `Access` après `Portability` un jour où quelqu'un renomme un membre.
    var held = await dbContext.Set<RetrievedData>()
      .Where(piece => piece.Case == caseId)
      .ToListAsync(cancellationToken);

    return
    [
      .. held
        .OrderBy(piece => piece.Right.Value)
        .ThenBy(piece => piece.DeclaredSystem.Value, StringComparer.Ordinal),
    ];
  }
}
