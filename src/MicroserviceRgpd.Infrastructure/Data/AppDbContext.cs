using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Infrastructure.Data.Audit;
using MicroserviceRgpd.Infrastructure.Data.Casework;

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

  /// <summary>
  /// Les <c>Case</c> — <b>la racine, et la seule</b>. Il n'existe volontairement aucun <c>DbSet</c>
  /// de <c>Claim</c> ni de <c>Step</c> : ils sont <em>possédés</em> par le dossier, ne s'atteignent
  /// que par lui, et n'ont donc structurellement ni requête ni dépôt à eux.
  /// </summary>
  public DbSet<Case> Cases => Set<Case>();

  /// <summary>
  /// Le <c>Ledger</c>, en <b>ajout seul</b>. Ce <c>DbSet</c> est ce par quoi EF Core connaît la
  /// table ; l'adaptateur qui l'alimente n'y lit rien, et aucune route n'expose ce qu'il contient.
  /// </summary>
  public DbSet<LedgerRow> LedgerEntries => Set<LedgerRow>();

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
  }

  public override int SaveChanges() =>
        SaveChangesAsync().GetAwaiter().GetResult();
}
