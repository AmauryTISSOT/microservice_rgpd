using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning.MySql;
using MySqlConnector;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// Ce que le scanner MariaDB/MySQL fait d'une chaîne de connexion avant de s'en servir. Trois
/// décisions y tiennent : le pool coupé, la lecture de fichiers locaux fermée, et le nom de la base
/// sorti de la chaîne.
/// </summary>
public class MySqlConnectionSettingsTests
{
  private const string Supplied = "Server=base.client.exemple;User ID=lecteur;Password=secret;Database=epreuve";

  /// <summary>
  /// ⚠️ <b>Le pool est coupé quoi que la chaîne dise.</b> Une connexion rendue au pool garde
  /// vivante, dans un casier statique du pilote, la chaîne du client — hôte, compte, mot de passe —
  /// bien après que l'<c>Operator</c> a quitté l'écran.
  /// </summary>
  [Theory]
  [InlineData(Supplied)]
  [InlineData(Supplied + ";Pooling=true")]
  [InlineData(Supplied + ";Pooling=True;MinimumPoolSize=4")]
  public void ClosesThePoolWhateverWasSupplied(string connectionString)
  {
    MySqlConnectionSettings.TryPrepare(connectionString, out var settings).ShouldBeTrue();

    new MySqlConnectionStringBuilder(settings.ConnectionString).Pooling.ShouldBeFalse(
      "Le pool garderait la chaîne de connexion du client dans un casier statique du pilote après "
      + "la fin du scan.");
  }

  /// <summary>
  /// ⚠️ <b><c>AllowLoadLocalInfile</c> est remis à <c>false</c>, même écrit à <c>true</c>.</b> À
  /// <c>true</c>, c'est le <b>serveur</b> qui décide quel fichier le client lui envoie : un serveur
  /// hostile répond à un <c>SELECT</c> anodin par une demande de <c>LOAD DATA LOCAL INFILE</c>, et
  /// le microservice lui lit un fichier de son <b>propre</b> disque.
  /// </summary>
  [Theory]
  [InlineData(Supplied)]
  [InlineData(Supplied + ";AllowLoadLocalInfile=true")]
  [InlineData(Supplied + ";Allow Load Local Infile=True")]
  public void NeverLetsTheServerAskForAFileOfItsOwn(string connectionString)
  {
    MySqlConnectionSettings.TryPrepare(connectionString, out var settings).ShouldBeTrue();

    new MySqlConnectionStringBuilder(settings.ConnectionString).AllowLoadLocalInfile.ShouldBeFalse(
      "À true, un serveur hostile fait lire au microservice un fichier de son propre disque.");
  }

  /// <summary>
  /// ⚠️ <b>Le nom de la base sort de la chaîne, et c'est ce qui rend les deux fins à zéro objet
  /// distinguables.</b> Connecté <i>sur</i> une base, le serveur refuse l'ouverture elle-même quand
  /// elle n'existe pas (1049) ou quand le compte n'y a aucun droit (1044) : « base absente du
  /// catalogue » — la seule des deux fins qui appelle un geste — ne serait jamais rendue.
  /// </summary>
  [Fact]
  public void ConnectsToTheServerAndNotToTheDatabase()
  {
    MySqlConnectionSettings.TryPrepare(Supplied, out var settings).ShouldBeTrue();

    settings.Database.ShouldBe("epreuve");
    new MySqlConnectionStringBuilder(settings.ConnectionString).Database.ShouldBeNullOrEmpty(
      "La base reste hors de la chaîne : c'est la lecture d'existence, plus tard, qui dira si le "
      + "catalogue la connaît.");
  }

  /// <summary>
  /// Une chaîne sans base ne désigne rien à relever : ce qui a été fourni est en cause, et
  /// l'<c>Operator</c> peut le corriger seul.
  /// </summary>
  [Theory]
  [InlineData("Server=base.client.exemple;User ID=lecteur")]
  [InlineData("Server=base.client.exemple;Database=   ")]
  [InlineData("Database=epreuve")]
  [InlineData("")]
  public void RefusesAStringThatNamesNoDatabaseToRead(string connectionString)
  {
    MySqlConnectionSettings.TryPrepare(connectionString, out _).ShouldBeFalse();
  }

  /// <summary>
  /// ⚠️ <b>Une chaîne que le pilote ne sait pas lire est refusée sans un mot.</b> Le message que
  /// le pilote donnerait porte l'option fautive — donc un morceau de ce que l'<c>Operator</c> a
  /// saisi, et l'hôte avec.
  /// </summary>
  [Theory]
  [InlineData("Server=base;Database=epreuve;OptionQuiNExistePas=3")]
  [InlineData("Server=base;Database=epreuve;Port=pas-un-nombre")]
  public void RefusesAStringTheDriverCannotEvenRead(string connectionString)
  {
    Should.NotThrow(() => MySqlConnectionSettings.TryPrepare(connectionString, out _));

    MySqlConnectionSettings.TryPrepare(connectionString, out _).ShouldBeFalse();
  }

  /// <summary>
  /// ⚠️ <b>Un nom trop long est refusé, jamais rogné.</b> Le nom part deux fois : dans l'en-tête du
  /// pivot et — en paramètre — dans la lecture d'existence. Le rogner pour l'en-tête ferait diverger
  /// les deux, et le service chercherait au catalogue une base que personne n'a nommée.
  /// </summary>
  [Fact]
  public void RefusesANameNoDatabaseCouldCarry()
  {
    var immense = new string('n', Screening.MaxDatabaseNameLength + 1);

    MySqlConnectionSettings.TryPrepare(
      $"Server=base;User ID=lecteur;Database={immense}",
      out _).ShouldBeFalse();
  }

  /// <summary>
  /// Le reste de la chaîne est laissé tel quel : le scanner ne décide ni du port, ni du délai, ni
  /// du chiffrement du transport. Ce sont des choix de l'<c>Operator</c>, et aucun n'ouvre la porte
  /// que les deux options forcées ferment.
  /// </summary>
  [Fact]
  public void LeavesEverythingElseAsTheOperatorWroteIt()
  {
    MySqlConnectionSettings.TryPrepare(
      Supplied + ";Port=3307;ConnectionTimeout=7",
      out var settings).ShouldBeTrue();

    var effective = new MySqlConnectionStringBuilder(settings.ConnectionString);

    effective.Port.ShouldBe(3307u);
    effective.ConnectionTimeout.ShouldBe(7u);
    effective.Server.ShouldBe("base.client.exemple");
  }
}
