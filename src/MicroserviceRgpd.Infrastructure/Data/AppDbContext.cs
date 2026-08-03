using MicroserviceRgpd.Core.Casework;
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
  /// Le <c>Manifest</c> : le paysage déclaré du client, tenu <b>système par système</b>. Chacun est
  /// un agrégat racine à lui seul — il se déclare, se relit et se révise seul, et porte sa propre
  /// date de déclaration.
  /// </summary>
  public DbSet<DeclaredSystem> DeclaredSystems => Set<DeclaredSystem>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
  }

  public override int SaveChanges() =>
        SaveChangesAsync().GetAwaiter().GetResult();
}
