using Mono.Cecil;
using Mono.Cecil.Cil;

namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// <b>Rien du pilote ne survit au scan.</b> La chaîne de connexion d'un client — hôte, utilisateur,
/// mot de passe — vit le temps d'un écran ; deux gestes du pilote PostgreSQL suffiraient à la faire
/// vivre plus longtemps, et aucun des deux ne se voit à la lecture du code qui les emploie.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b><c>new NpgsqlConnection(chaîne)</c> ne fait pas ce qu'il a l'air de faire.</b> Il n'ouvre
/// pas seulement une connexion : Npgsql range une <c>NpgsqlDataSource</c> dans un <b>cache statique
/// de processus</b>, indexé par la chaîne. Couper le pool ne ferme alors qu'une moitié du casier —
/// les connexions se referment, la chaîne du client reste, dans une variable que personne ne vide et
/// que rien n'affiche. Le scanner construit donc sa source explicitement, dans un
/// <c>await using</c>, et ce garde est ce qui empêche le raccourci de revenir un jour de hâte.
/// </para>
/// <para>
/// ⚠️ <b>Un <c>NpgsqlBatch</c> est enveloppé dans une transaction implicite.</b> L'échec d'une seule
/// de ses commandes annule <b>tout</b> le lot : c'est l'exact opposé de la tolérance attendue d'un
/// prélèvement, où la table qui rate reçoit une raison nommée et les autres gardent leurs valeurs.
/// Grouper fausserait par-dessus le marché le dénominateur de l'avancement, qui se compte en tables
/// prélevées.
/// </para>
/// <para>
/// Le contrôle se fait au niveau de l'<b>IL</b>, et non des signatures : ces deux gestes vivent dans
/// des corps de méthode, où aucune surface publique ne les montre.
/// </para>
/// <para>
/// ⚠️ <b>Et les trois pilotes ne vivent que dans <c>Infrastructure</c>.</b> Ce qui ne survit pas au
/// scan et ce qui n'a pas le droit d'en sortir sont la même frontière, lue par les deux bouts :
/// aucun casier du pilote ne garde une chaîne, et aucun assemblage au-dessus ne nomme un pilote.
/// </para>
/// </remarks>
public class NoDriverStateOutlivesAScanTests
{
  private const string Connection = "Npgsql.NpgsqlConnection";
  private const string Batch = "Npgsql.NpgsqlBatch";

  /// <summary>Les trois familles de pilote, par le début de leur espace de noms.</summary>
  private static readonly string[] DriverFamilies =
    ["Npgsql", "MySqlConnector", "Microsoft.Data.Sqlite"];

  /// <summary>
  /// La source de données est bâtie et disposée par le scan ; elle n'est jamais tirée du cache que
  /// le constructeur par chaîne alimente.
  /// </summary>
  [Fact]
  public void BuildsNoConnectionOutOfAConnectionString()
  {
    var caught = Constructions(Connection)
      .Where(construction => construction.Method.Parameters.Any(parameter =>
        parameter.ParameterType.FullName == "System.String"))
      .Select(Where)
      .ToArray();

    caught.ShouldBeEmpty(
      "Une connexion est construite depuis une chaîne :" + Environment.NewLine
      + string.Join(Environment.NewLine, caught) + Environment.NewLine
      + "Ce constructeur range une NpgsqlDataSource dans un cache statique de processus, indexé "
      + "par la chaîne : celle du client survivrait au scan, à l'écran et à la session "
      + "d'arbitrage. Passez par un NpgsqlDataSourceBuilder, dans un await using.");
  }

  /// <summary>
  /// Une requête par table, jamais un lot : la table qui rate reçoit une raison, les autres gardent
  /// leurs valeurs.
  /// </summary>
  [Fact]
  public void BatchesNothing()
  {
    var caught = Constructions(Batch).Select(Where).ToArray();

    caught.ShouldBeEmpty(
      "Un lot de commandes est construit :" + Environment.NewLine
      + string.Join(Environment.NewLine, caught) + Environment.NewLine
      + "Un NpgsqlBatch court dans une transaction implicite : l'échec d'une commande annule tout "
      + "le lot, là où le prélèvement doit perdre une table et garder les autres.");
  }

  /// <summary>
  /// <b>Le garde mord, et on le prouve.</b> Un garde qui ne trouve rien à lire afficherait vert pour
  /// toujours : on vérifie ici qu'il voit bien le pilote qu'il surveille.
  /// </summary>
  [Fact]
  public void SeesThePostgreSqlDriverItWatches()
  {
    Constructions("Npgsql.NpgsqlDataSourceBuilder").ShouldNotBeEmpty(
      "L'inspecteur ne trouve aucune construction de NpgsqlDataSourceBuilder dans Infrastructure. "
      + "Soit le dialecte PostgreSQL a changé de façon d'ouvrir sa connexion, soit ce garde lit un "
      + "assemblage où il n'y a rien à lire — et il afficherait vert pour toujours.");
  }

  /// <summary>
  /// <b>Les trois pilotes ne vivent que dans <c>Infrastructure</c>.</b> Ni <c>Core</c>, ni
  /// <c>UseCases</c>, ni <c>Web</c> ne nomme <c>Npgsql</c>, <c>MySqlConnector</c> ou
  /// <c>Microsoft.Data.Sqlite</c>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>C'est une frontière, pas une préférence de rangement.</b> Le port sortant est
  /// <c>IDatabaseScanner</c>, et il ne parle que le vocabulaire du domaine : un pilote nommé dans
  /// <c>Core</c> ferait entrer une <c>SqliteException</c>, une <c>NpgsqlDataSource</c> ou un
  /// message de serveur dans le seul assemblage qui n'a aucun droit d'en connaître — et
  /// <c>Rien de réel ne reste</c> se tient très exactement à cette frontière-là.
  /// </para>
  /// <para>
  /// ⚠️ <b>Le graphe de projets ne suffit pas à le dire.</b> <c>Core</c> ne référence aujourd'hui
  /// aucun pilote, mais rien n'empêche une ligne de <c>PackageReference</c> de l'y faire entrer un
  /// jour de hâte, et la revue verrait passer une dépendance là où elle croit lire un domaine pur.
  /// </para>
  /// <para>
  /// ⚠️ <b>Les familles sont reconnues par le <b>début</b> de leur espace de noms</b>, comme les
  /// bibliothèques d'ordonnancement de <c>NothingRunsInTheBackgroundTests</c> : aucun type de ces
  /// familles n'a de raison d'entrer, quel que soit son nom.
  /// </para>
  /// </remarks>
  [Theory]
  [InlineData("MicroserviceRgpd.Core")]
  [InlineData("MicroserviceRgpd.UseCases")]
  [InlineData("MicroserviceRgpd.Web")]
  public void KeepsTheThreeDriversInsideInfrastructure(string assembly)
  {
    using var module = ModuleDefinition.ReadModule(ProductionAssembly.PathOf(assembly));

    var named = module.GetTypeReferences()
      .Select(reference => reference.FullName)
      .Where(fullName => DriverFamilies.Any(family =>
        fullName.StartsWith(family + ".", StringComparison.Ordinal)))
      .Distinct(StringComparer.Ordinal)
      .Order(StringComparer.Ordinal)
      .ToArray();

    named.ShouldBeEmpty(
      $"{assembly} nomme un pilote de base de données :" + Environment.NewLine
      + string.Join(Environment.NewLine, named) + Environment.NewLine
      + "Les trois pilotes vivent dans Infrastructure, derrière IDatabaseScanner, et nulle part "
      + "ailleurs : c'est la frontière où s'arrêtent les exceptions, les messages de serveur et "
      + "les chaînes de connexion des clients.");
  }

  /// <summary>
  /// <b>Le garde mord, et on le prouve.</b> Les trois familles sont bien nommées quelque part —
  /// dans <c>Infrastructure</c>, où elles ont le droit d'être.
  /// </summary>
  /// <remarks>
  /// Sans ceci, une faute de frappe dans un nom de famille rendrait les trois assertions ci-dessus
  /// vertes pour toujours, sur un garde qui ne chercherait plus rien.
  /// </remarks>
  [Fact]
  public void SeesTheThreeDriverFamiliesWhereTheyBelong()
  {
    using var module = ModuleDefinition.ReadModule(
      ProductionAssembly.PathOf("MicroserviceRgpd.Infrastructure"));

    var named = module.GetTypeReferences().Select(reference => reference.FullName).ToArray();

    foreach (var family in DriverFamilies)
    {
      named.ShouldContain(
        fullName => fullName.StartsWith(family + ".", StringComparison.Ordinal),
        $"Infrastructure ne nomme aucun type de {family} : soit le dialecte a changé de pilote, "
        + "soit ce garde cherche une famille qui n'existe plus — et il afficherait vert pour "
        + "toujours sur les trois assemblages qu'il surveille.");
    }
  }

  private static List<(MethodReference Method, MethodDefinition Site)> Constructions(string type)
  {
    using var module = ModuleDefinition.ReadModule(
      ProductionAssembly.PathOf("MicroserviceRgpd.Infrastructure"));

    return
    [
      .. module.GetTypes()
        .SelectMany(declaring => declaring.Methods)
        .Where(method => method.HasBody)
        .SelectMany(method => method.Body.Instructions
          .Where(instruction => instruction.OpCode == OpCodes.Newobj)
          .Select(instruction => instruction.Operand)
          .OfType<MethodReference>()
          .Where(called => called.DeclaringType.FullName == type)
          .Select(called => (Method: called, Site: method))),
    ];
  }

  private static string Where((MethodReference Method, MethodDefinition Site) construction)
  {
    return $"  {construction.Site.DeclaringType.FullName}.{construction.Site.Name}";
  }
}
