using MicroserviceRgpd.Core.Casework.EvidenceLog;

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
public sealed class EvidenceLog(AppDbContext dbContext) : IEvidenceLog
{
  /// <inheritdoc />
  public async Task AppendAsync(EvidenceLogEntry entry, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(entry);

    // `Set<T>()` plutôt qu'un `DbSet` du contexte : le contexte n'en expose aucun pour l'EvidenceLog,
    // afin que `Remove` et `Update` ne soient à portée de personne.
    dbContext.Set<EvidenceLogRow>().Add(RowOf(entry));

    await dbContext.SaveChangesAsync(cancellationToken);
  }

  /// <summary>
  /// Met l'écrit à plat. La traduction ne décide de rien : ce qui est nul dans l'écrit l'est dans
  /// la ligne, et aucune valeur n'est complétée ni devinée.
  /// </summary>
  private static EvidenceLogRow RowOf(EvidenceLogEntry entry)
  {
    return new EvidenceLogRow
    {
      EntryId = entry.Id.Value,
      CaseId = entry.Case.Value,
      OccurredAt = entry.OccurredAt,
      Fact = entry.Fact.Name,
      SignatoryKind = entry.Signatory.Kind.Name,
      SignatoryName = entry.Signatory.Name,
      IdentityDeclaration = entry.IdentityDeclaration?.Name,
      DesignationCount = entry.DesignationCount,
      DeclaredSystem = entry.DeclaredSystem?.Value,
      SignerVerification = entry.Signatory.Verification?.Name,
      DataSubjectRight = entry.Right?.Name,
      StepState = entry.DeclaredState?.Name,
      Prose = entry.Prose,
      ReceptionWasDefaulted = entry.ReceptionWasDefaulted,
      ReceivedOn = entry.ReceivedOn,
      IdentityVerificationMethod = entry.VerificationMethod?.Name,
      DeclaredDeadline = entry.DeclaredDeadline,
      CoveredSystemCount = entry.CoveredSystemCount,
      DeclaredSystemCount = entry.DeclaredSystemCount,
      ClosingCause = entry.ClosingCause?.Name,
      InformedOn = entry.InformedOn,
    };
  }
}
