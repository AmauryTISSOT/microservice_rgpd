using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Data.Audit;

namespace MicroserviceRgpd.Infrastructure.Data;
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
  // Déclarer ici un DbSet par table persistée. Toutes ne sont pas des agrégats : la trace
  // d'audit est un écrit, pas une entité instruite dans le temps.

  /// <summary>
  /// La trace d'audit, en <b>écriture seule</b>. Ce <c>DbSet</c> est ce par quoi EF Core connaît la
  /// table ; il n'est lu par personne, et aucun <c>GET</c> n'expose ce qu'il contient.
  /// </summary>
  public DbSet<QualificationAuditRow> QualificationAuditEntries => Set<QualificationAuditRow>();

  /// <summary>
  /// Le <c>Settings</c> — le Paramétrage : la configuration applicative du service, <b>singleton</b>.
  /// La table ne porte qu'une ligne, sa clé est figée, et elle naît paresseusement au premier
  /// enregistrement d'une adresse.
  /// </summary>
  public DbSet<Settings> Settings => Set<Settings>();

  /// <summary>
  /// Les <c>DataSubjectRequest</c> — les demandes enregistrées à leur réception, et l'agrégat racine
  /// du contexte <c>Requests</c>.
  /// </summary>
  public DbSet<DataSubjectRequest> DataSubjectRequests => Set<DataSubjectRequest>();

  /// <summary>
  /// Les <c>ExecutionAttempt</c> — le journal d'exécution des demandes, un agrégat à part qui
  /// survit à la suppression de la demande qu'il référence (ADR-0026).
  /// </summary>
  public DbSet<ExecutionAttempt> ExecutionAttempts => Set<ExecutionAttempt>();

  /// <summary>
  /// Les <c>Screening</c> — le rapport de détection, et l'agrégat racine de son contexte.
  /// </summary>
  public DbSet<Screening> Screenings => Set<Screening>();

  /// <summary>
  /// Les <c>ScreenedColumn</c> — <b>une entité fille avec son propre <c>DbSet</c></b>, ce qui rompt
  /// délibérément le précédent des entités possédées, qui ne s'atteignent que par leur racine. Le
  /// motif est écrit une fois, là où la table se décide : voir <c>ScreenedColumnConfiguration</c>.
  /// </summary>
  public DbSet<ScreenedColumn> ScreenedColumns => Set<ScreenedColumn>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
  }

  public override int SaveChanges() =>
        SaveChangesAsync().GetAwaiter().GetResult();
}
