using System.Net.Sockets;
using RabbitMQ.Client.Exceptions;

namespace MicroserviceRgpd.Infrastructure.Requests;

/// <summary>
/// <b>Ce qui, du côté du bus, est une panne de réseau</b> : la seule définition du dépôt, et le seul
/// endroit où elle s'écrit (ADR-0028).
/// </summary>
/// <remarks>
/// <para>
/// Elle sert à trois endroits — l'ouverture d'un channel, la publication elle-même, et la fermeture
/// de ce qu'on jette —, et le client AMQP n'a pas de type unique pour la dire : une connexion perdue
/// arrive tantôt en <see cref="RabbitMQClientException"/>, tantôt en <see cref="IOException"/>,
/// tantôt en <see cref="SocketException"/>. Recopiée à chaque <c>catch</c>, la liste aurait divergé.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne dit rien de l'annulation</b> : un délai dépassé n'est pas une panne, et se lit sur
/// le jeton qui l'a provoqué.
/// </para>
/// </remarks>
internal static class BrokerFailure
{
  /// <summary>L'exception dit-elle que le broker n'a pas pu être joint, ou ne l'est plus ?</summary>
  public static bool IsNetwork(Exception failure) =>
    failure is RabbitMQClientException or IOException or SocketException;

  /// <summary>
  /// La même chose, <b>l'ouverture d'une connexion en plus</b> : la fabrique du client rend un
  /// <see cref="TimeoutException"/> quand la poignée de main n'aboutit pas.
  /// </summary>
  public static bool IsUnreachable(Exception failure) => IsNetwork(failure) || failure is TimeoutException;
}
