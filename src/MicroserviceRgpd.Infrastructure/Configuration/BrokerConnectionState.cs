using MicroserviceRgpd.Core.Configuration;

namespace MicroserviceRgpd.Infrastructure.Configuration;

/// <summary>
/// L'état de la connexion au broker, <b>lu sur ce que le déploiement déclare</b> — et sur rien
/// d'autre.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucun test réseau.</b> La décision se prend sur la <b>seule présence</b> de l'hôte, jamais
/// sur un broker joignable : ni le rendu d'un écran ni le calcul d'un motif de blocage ne dépendent
/// ainsi de la disponibilité du bus (ADR-0027). Le prix en est assumé — un hôte déclaré devant un
/// broker éteint n'avertit de rien, et c'est l'exécution qui échouera.
/// </para>
/// <para>
/// ⚠️ <b>Cet adaptateur ne juge rien lui-même</b> : ce qu'une clé vide ou faite d'espaces signifie
/// est écrit une seule fois, sur <see cref="BrokerConnection.Declaring"/>, dans le noyau
/// (ADR-0028). Il ne fait que lui porter ce que le déploiement a écrit. Tout lecteur passe par cet
/// état plutôt que de relire la clé, faute de quoi deux publics liraient deux vérités.
/// </para>
/// </remarks>
/// <param name="options">La connexion telle que le déploiement la déclare.</param>
public sealed class BrokerConnectionState(RabbitMqOptions options) : IBrokerConnectionState
{
  /// <inheritdoc />
  public BrokerConnection Current => BrokerConnection.Declaring(options.HostName);
}
