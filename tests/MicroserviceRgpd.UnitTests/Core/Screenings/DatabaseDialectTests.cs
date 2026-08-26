using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// Le dialecte est clos, et son nom pivot est celui que la requête du chemin collé écrit déjà.
/// </summary>
public class DatabaseDialectTests
{
  /// <summary>
  /// ⚠️ <b>Deux vocabulaires pour le même champ, ce sont deux relevés de la même base qui ne se
  /// comparent plus.</b> Le nom pivot n'est donc pas vérifié contre une constante recopiée ici mais
  /// contre <c>releves/*.sql</c> : la source que l'<c>Operator</c> exécute vraiment.
  /// </summary>
  [Theory]
  [InlineData("PostgreSql", "postgresql")]
  [InlineData("MySql", "mariadb")]
  [InlineData("Sqlite", "sqlite")]
  public void WritesTheSameDialectNameAsThePastedQuery(string member, string pivotName)
  {
    var dialect = DatabaseDialect.FromName(member);

    dialect.PivotName.ShouldBe(pivotName);
    ThePastedQuery.For(pivotName).ShouldContain($"'dialecte',  '{pivotName}'");
  }

  /// <summary>
  /// ⚠️ <b>Trois membres, et le quatrième naîtra d'un ticket.</b> Un dialecte qui entrerait par une
  /// chaîne de caractères serait un pilote que personne n'a écrit, découvert en production.
  /// </summary>
  [Fact]
  public void KnowsThreeDialectsAndNoMore()
  {
    DatabaseDialect.List.Count.ShouldBe(3);
    DatabaseDialect.List.Select(dialect => dialect.PivotName)
      .ShouldBe(["postgresql", "mariadb", "sqlite"], ignoreOrder: true);
  }

  /// <summary>
  /// MariaDB et MySQL ne font qu'un membre : même pilote, même catalogue, même nom pivot. Les
  /// séparer donnerait un choix qui ne change rien, et qui punirait quand même celui qui choisit
  /// mal.
  /// </summary>
  [Fact]
  public void SaysMariaDbAndMySqlInOneBreath()
  {
    DatabaseDialect.MySql.FrenchLabel.ShouldBe("MariaDB/MySQL");
  }
}
