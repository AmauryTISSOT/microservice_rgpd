using MicroserviceRgpd.Core.Casework.Ledger;

namespace MicroserviceRgpd.Infrastructure.Data.Casework;

/// <summary>
/// Ajoute une ligne, et rien d'autre.
/// </summary>
/// <remarks>
/// <para>
/// <b>Une seule méthode, et c'est tout le propos.</b> Il n'existe ici ni mise à jour, ni suppression
/// ligne à ligne, ni relecture — pas même privée : ce que l'adaptateur ne sait pas faire, personne
/// n'aura à jurer qu'il ne l'a pas fait. L'ajout d'un tel geste serait visible dans ce fichier.
/// </para>
/// <para>
/// <b>Il ne lit aucune ligne antérieure.</b> Ni la précédente, ni un rang, ni un total courant : la
/// projection ci-dessous ne dépend que de l'écrit qu'on lui donne, et c'est ce qui rend l'ajout seul
/// vérifiable plutôt que promis.
/// </para>
/// <para>
/// <b>Il ne passe pas par le dépôt générique</b>, contraint aux agrégats racines, et
/// <b>il n'attrape rien</b> : une base indisponible est une panne du service, et il n'y a ici aucun
/// repli qui rendrait un dossier ouvert dont la preuve n'a rien enregistré.
/// </para>
/// </remarks>
public sealed class Ledger(AppDbContext dbContext) : ILedger
{
  /// <inheritdoc />
  public async Task AppendAsync(LedgerEntry entry, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(entry);

    dbContext.LedgerEntries.Add(RowOf(entry));

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  /// <summary>
  /// Met l'écrit à plat. La traduction ne décide de rien : ce qui est nul dans l'écrit l'est dans
  /// la ligne, et aucune valeur n'est complétée ni devinée.
  /// </summary>
  private static LedgerRow RowOf(LedgerEntry entry)
  {
    return new LedgerRow
    {
      EntryId = entry.Id.Value,
      CaseId = entry.Case.Value,
      OccurredAt = entry.OccurredAt,
      Fact = entry.Fact.Name,
      SignatoryKind = entry.Signatory.Kind.Name,
      SignatoryName = entry.Signatory.Name,
      IdentityDeclaration = entry.IdentityDeclaration?.Name,
      DesignationCount = entry.DesignationCount,
    };
  }
}
