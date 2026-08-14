using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Screenings;
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
  /// Les <c>Screening</c> — le rapport d'un dépistage, et l'agrégat racine de son contexte.
  /// </summary>
  public DbSet<Screening> Screenings => Set<Screening>();

  /// <summary>
  /// Les <c>ScreenedColumn</c> — <b>une entité fille avec son propre <c>DbSet</c></b>, ce qui rompt
  /// délibérément le précédent de <c>Claim</c> et de <c>Step</c> juste au-dessus. Le motif est écrit
  /// une fois, là où la table se décide : voir <c>ScreenedColumnConfiguration</c>.
  /// </summary>
  public DbSet<ScreenedColumn> ScreenedColumns => Set<ScreenedColumn>();

  // ⚠️ Aucun DbSet de l'EvidenceLog, et c'est délibéré. Il en existe un pour la trace d'audit, qui n'a
  // qu'un invariant d'écriture seule ; l'EvidenceLog, lui, promet qu'aucune opération de mise à jour ni
  // de suppression ligne à ligne n'existe sur lui — et un DbSet public rendrait `Remove` et
  // `Update` à quiconque tient ce contexte, c'est-à-dire à tout le service. EF Core connaît la
  // table par sa configuration d'entité, qui suffit ; le seul chemin d'écriture est l'adaptateur
  // du port `IEvidenceLog`, dont la seule méthode est un ajout.

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
  }

  public override int SaveChanges() =>
        SaveChangesAsync().GetAwaiter().GetResult();
}
