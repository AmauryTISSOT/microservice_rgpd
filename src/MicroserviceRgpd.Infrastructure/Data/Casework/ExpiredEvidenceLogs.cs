using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.EvidenceLog;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.Infrastructure.Data.Casework;

/// <summary>
/// Voit ce qui est échu, et détruit un <c>EvidenceLog</c> <b>entier</b> — la seule suppression du
/// dispositif, et elle est écrite ici.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle vit à part de <see cref="EvidenceLog"/>, qui n'ajoute que.</b> L'adaptateur d'écriture ne sait
/// toujours ni relire ni effacer : ce qu'il ne sait pas faire, personne n'aura à jurer qu'il ne l'a
/// pas fait. Ce fichier-ci est le seul du dépôt qui supprime de la preuve, et il s'ouvre exprès.
/// </para>
/// <para>
/// <b>Aucune suppression ligne à ligne n'existe ici non plus.</b> La seule que ce type sache faire
/// emporte <b>toutes</b> les lignes d'un dossier d'un coup : un <c>EvidenceLog</c> amputé serait pire
/// qu'un <c>EvidenceLog</c> absent — il se lirait comme complet.
/// </para>
/// <para>
/// ⚠️ <b>Rien ne tourne ici.</b> Les deux méthodes sont appelées par un affichage et par un clic ;
/// il n'existe aucun balayage périodique, et l'architecture le garde par un test du code compilé.
/// </para>
/// </remarks>
public sealed class ExpiredEvidenceLogs(AppDbContext dbContext) : IExpiredEvidenceLogs
{
  /// <inheritdoc />
  public async Task<IReadOnlyList<ExpiredEvidenceLog>> ListAsync(
    DateTimeOffset observedAt,
    CancellationToken cancellationToken = default)
  {
    // La base ne fait que dégrossir : elle écarte les dossiers manifestement trop récents, et la
    // règle des cinq ans se tranche ensuite par le type du domaine qui la porte. Deux écritures de
    // la même règle — une en SQL, une en C# — finiraient par ne plus dire la même chose. La borne
    // elle-même vient du domaine : calculée à l'envers ici, elle perdrait le 29 février.
    var perhaps = EvidenceLogRetention.ClosedNoLaterThan(observedAt);

    var closed = await dbContext.Cases
      .AsNoTracking()
      .Where(one => one.ClosedOn != null && one.ClosedOn <= perhaps)
      .Select(one => new { one.Id, one.ClosedOn })
      .ToListAsync(cancellationToken);

    var expired = closed
      .Where(one => EvidenceLogRetention.IsExpiredAt(one.ClosedOn!.Value, observedAt))
      .ToArray();

    if (expired.Length == 0)
    {
      return [];
    }

    // ⚠️ La ligne est celle de la PREUVE, jamais celle du dossier clos : un EvidenceLog déjà détruit ne
    // reparaît pas, et c'est ainsi que le geste se voit avoir été fait — faute de pouvoir se
    // consigner lui-même.
    var identities = expired.Select(one => one.Id.Value).ToArray();

    var held = await dbContext.Set<EvidenceLogRow>()
      .AsNoTracking()
      .Where(row => identities.Contains(row.CaseId))
      .Select(row => row.CaseId)
      .Distinct()
      .ToListAsync(cancellationToken);

    var standing = held.ToHashSet();

    return
    [
      .. expired
        .Where(one => standing.Contains(one.Id.Value))
        .Select(one => new ExpiredEvidenceLog(
          one.Id,
          one.ClosedOn!.Value,
          EvidenceLogRetention.ExpiryOf(one.ClosedOn.Value)))
        // Le plus anciennement échu en tête, puis l'identité du dossier : deux affichages de suite
        // doivent ranger les mêmes lignes dans le même ordre.
        .OrderBy(line => line.ExpiredOn)
        .ThenBy(line => line.Case.Value),
    ];
  }

  /// <inheritdoc />
  public async Task<bool> DestroyAsync(
    CaseId evidenceLogOf,
    DateTimeOffset observedAt,
    CancellationToken cancellationToken = default)
  {
    // L'échéance est éprouvée ICI, sur l'instant du geste, et non crue sur la foi de l'écran d'où
    // part le clic : ce qui est irréversible ne se décide pas sur une page vieille d'une heure.
    var closedOn = await dbContext.Cases
      .AsNoTracking()
      .Where(one => one.Id == evidenceLogOf)
      .Select(one => one.ClosedOn)
      .SingleOrDefaultAsync(cancellationToken);

    if (closedOn is null || !EvidenceLogRetention.IsExpiredAt(closedOn.Value, observedAt))
    {
      return false;
    }

    // Toutes les lignes du dossier, en une seule instruction. ⚠️ Rien n'est écrit à la place : la
    // destruction ne laisse aucune trace d'elle-même, et on ne prouvera jamais avoir purgé.
    var destroyed = await dbContext.Set<EvidenceLogRow>()
      .Where(row => row.CaseId == evidenceLogOf.Value)
      .ExecuteDeleteAsync(cancellationToken);

    return destroyed > 0;
  }
}
