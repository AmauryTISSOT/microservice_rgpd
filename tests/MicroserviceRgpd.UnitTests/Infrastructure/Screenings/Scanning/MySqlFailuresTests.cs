using System.Net.Sockets;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Infrastructure.Screenings.Scanning.MySql;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings.Scanning;

/// <summary>
/// Ce qu'une panne du pilote devient en franchissant le port : deux mots du service, et pas un
/// caractère venu du pilote.
/// </summary>
/// <remarks>
/// ⚠️ <b>Le rangement s'éprouve sur le numéro parce que c'est sur le numéro qu'il est écrit.</b>
/// <c>MySqlException</c> n'a aucun constructeur public : présenter au service un « droits refusés
/// sur la table » demanderait un serveur. Ce qui décide, lui, est un entier — et le protocole l'a
/// figé.
/// </remarks>
public class MySqlFailuresTests
{
  /// <summary>
  /// ⚠️ <b>Ce qui vient du réseau se range ensemble.</b> « Hôte injoignable », « délai dépassé » et
  /// « connexion tombée » n'offrent pas à l'<c>Operator</c> des gestes différents.
  /// </summary>
  [Theory]
  [InlineData(MySqlFailures.UnableToConnectToHost)]
  [InlineData(MySqlFailures.HandshakeError)]
  public void ReadsTheNetworkInWhatNeverReachedTheServer(int errorCode)
  {
    MySqlFailures.FamilyOf(errorCode, inner: null).ShouldBe(ScanFailureFamily.Network);
  }

  /// <summary>
  /// Un socket, un flux coupé, un délai : le pilote n'a rien reçu du serveur, et le numéro qu'il
  /// porte alors ne dit rien de plus que son enveloppe.
  /// </summary>
  [Theory]
  [ClassData(typeof(TransportFailures))]
  public void ReadsTheNetworkInWhatTheTransportThrew(Exception inner)
  {
    MySqlFailures.FamilyOf(errorCode: 0, inner).ShouldBe(ScanFailureFamily.Network);
  }

  /// <summary>
  /// ⚠️ <b>Ce que l'<c>Operator</c> a fourni est la seule famille qu'il corrige seul.</b> Un compte
  /// refusé, une base inconnue, une base sur laquelle il n'a aucun droit : dans les trois cas, le
  /// geste est le même — reprendre la chaîne, ou demander un accès.
  /// </summary>
  [Theory]
  [InlineData(MySqlFailures.AccessDenied)]
  [InlineData(MySqlFailures.DatabaseAccessDenied)]
  [InlineData(MySqlFailures.UnknownDatabase)]
  public void ReadsWhatWasSuppliedInARefusedAccount(int errorCode)
  {
    MySqlFailures.FamilyOf(errorCode, inner: null).ShouldBe(ScanFailureFamily.Supplied);
  }

  /// <summary>
  /// ⚠️ <b>Ce qui n'est pas reconnu tombe du côté de la base.</b> C'est le défaut prudent : elle a
  /// répondu, et ce qu'elle a répondu est un échec. Le rangement inverse enverrait l'<c>Operator</c>
  /// corriger une chaîne qui n'a rien.
  /// </summary>
  [Theory]
  [InlineData(1146)]
  [InlineData(1064)]
  [InlineData(9999)]
  public void FallsBackToTheDatabaseForWhatItDoesNotKnow(int errorCode)
  {
    MySqlFailures.FamilyOf(errorCode, inner: null).ShouldBe(ScanFailureFamily.Database);
  }

  /// <summary>
  /// ⚠️ <b>« Droits refusés » vit à part.</b> C'est la seule des quatre raisons que l'<c>Operator</c>
  /// puisse corriger — elle l'envoie demander un accès, là où les trois autres ne lui font rien
  /// faire.
  /// </summary>
  [Theory]
  [InlineData(MySqlFailures.TableAccessDenied)]
  [InlineData(MySqlFailures.ColumnAccessDenied)]
  [InlineData(MySqlFailures.AccessDenied)]
  [InlineData(MySqlFailures.DatabaseAccessDenied)]
  public void TellsARefusedRightFromAFailedRead(int errorCode)
  {
    MySqlFailures.ReasonFor(errorCode).ShouldBe(PreviewAbsenceReason.AccessDenied);
  }

  /// <summary>
  /// Le reste est « lecture échouée » : délai dépassé, connexion tombée, table disparue entre le
  /// relevé et le prélèvement. Aucune n'offre un geste que les autres n'offrent pas.
  /// </summary>
  [Theory]
  [InlineData(1146)]
  [InlineData(2013)]
  public void SaysTheReadFailedForEverythingElse(int errorCode)
  {
    MySqlFailures.ReasonFor(errorCode).ShouldBe(PreviewAbsenceReason.ReadFailed);
  }

  /// <summary>
  /// ⚠️ <b>Une requête coupée n'est pas un scan tombé.</b> Le serveur rend <c>1317</c> quand
  /// l'annulation l'a interrompu ; le rendre tel quel ferait de l'<c>Operator</c> qui quitte
  /// l'écran un échec, avec un écran d'échec à la clé.
  /// </summary>
  [Fact]
  public void ReadsAnInterruptedQueryAsTheCallerTakingBackControl()
  {
    MySqlFailures.IsInterrupt(MySqlFailures.QueryInterrupted, CancellationToken.None)
      .ShouldBeTrue();

    MySqlFailures.IsInterrupt(MySqlFailures.AccessDenied, CancellationToken.None)
      .ShouldBeFalse();
  }

  /// <summary>
  /// ⚠️ <b>Le jeton fait foi même quand le serveur a dit autre chose.</b> Une connexion coupée sous
  /// une requête annulée remonte parfois en « connexion perdue » plutôt qu'en « requête
  /// interrompue » : c'est la même annulation, et elle ne doit pas se déguiser en panne.
  /// </summary>
  [Fact]
  public void TrustsTheTokenOverWhateverTheServerManagedToSay()
  {
    using var abandon = new CancellationTokenSource();

    abandon.Cancel();

    MySqlFailures.IsInterrupt(2013, abandon.Token).ShouldBeTrue();
  }

  /// <summary>Les pannes de transport que le pilote enveloppe.</summary>
  private sealed class TransportFailures : TheoryData<Exception>
  {
    public TransportFailures()
    {
      Add(new SocketException(10061));
      Add(new IOException("flux coupé"));
      Add(new TimeoutException("délai dépassé"));
    }
  }
}
