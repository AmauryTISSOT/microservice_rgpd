using MicroserviceRgpd.Infrastructure.Screenings.Scanning.MySql;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning.PostgreSql;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning.Sqlite;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// <b>Le pool est coupé sur les trois dialectes</b>, et la question se pose une fois, ici, sur les
/// <b>trois chaînes effectives</b> — celles que le service remet réellement à son pilote.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Une connexion rendue au pool reste ouverte, et souvent authentifiée, après l'écran.</b> La
/// fin du scan doit être la fin de la connexion : un pool garderait une session vivante sur la base
/// de production d'un client pendant des minutes, pour un scan que l'<c>Operator</c> croit terminé.
/// C'est la moitié « accès » de <c>Rien de réel ne reste</c> — l'autre étant les valeurs.
/// </para>
/// <para>
/// ⚠️ <b>Chaque dialecte a déjà son propre test de chaîne effective, et celui-ci ne les remplace
/// pas.</b> Ils éprouvent, chacun, ce que <i>leur</i> pilote fait de ce qu'on lui donne. Celui-ci
/// éprouve la seule chose qu'aucun d'eux ne peut dire : que les <b>trois</b> le font, et qu'un
/// quatrième dialecte câblé un jour sans cette ligne ferait rougir un test qui parle de lui.
/// </para>
/// <para>
/// ⚠️ <b>Il lit la chaîne préparée, jamais l'intention écrite dans le code.</b> Chercher
/// <c>Pooling = false</c> à la relecture aurait été vert sur un réglage écrit puis écrasé deux
/// lignes plus bas ; les trois chaînes que voici sont celles qui partent au pilote.
/// </para>
/// </remarks>
public class ThePoolIsCutOnAllThreeDialects
{
  /// <summary>
  /// ⚠️ <b>Les trois chaînes fournies demandent le pool, et l'une d'elles fixe même une taille
  /// minimale.</b> Une chaîne muette sur le sujet aurait été verte sur un défaut du pilote plutôt
  /// que sur une décision du service ; ici, le service <b>contredit</b> ce qu'on lui a demandé, et
  /// c'est précisément ce qui doit être vrai.
  /// </summary>
  [Fact]
  public void OnEveryEffectiveConnectionString()
  {
    PostgreSqlDialectScanner.TryPrepare(
      "Host=exemple.test;Database=epreuve;Username=lecteur;Pooling=true;Maximum Pool Size=50",
      out var postgres).ShouldBeTrue();

    new NpgsqlConnectionStringBuilder(postgres).Pooling.ShouldBeFalse(
      "PostgreSQL rendrait sa connexion au pool, et la session resterait ouverte sur la base du "
      + "client après que l'Operator a quitté l'écran.");

    MySqlConnectionSettings.TryPrepare(
      "Server=exemple.test;Database=epreuve;User ID=lecteur;Pooling=true;MinimumPoolSize=4",
      out var mysql).ShouldBeTrue();

    new MySqlConnectionStringBuilder(mysql.ConnectionString).Pooling.ShouldBeFalse(
      "MySQL rendrait sa connexion au pool, et elle y resterait authentifiée.");

    SqliteDialectScanner.TryPrepare(
      "Data Source=galette.db;Pooling=True",
      out var sqlite,
      out _).ShouldBeTrue();

    new SqliteConnectionStringBuilder(sqlite).Pooling.ShouldBeFalse(
      "SQLite garderait le fichier du client ouvert après la fin du scan.");
  }
}
