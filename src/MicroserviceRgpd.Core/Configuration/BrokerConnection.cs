namespace MicroserviceRgpd.Core.Configuration;

/// <summary>
/// La <b>connexion du déploiement au broker</b> : ce qui dit « <b>ce déploiement sait publier</b> ».
/// Type <b>fermé à deux cas</b> — <see cref="Configured"/> et <see cref="Absent"/> (ADR-0028).
/// </summary>
/// <remarks>
/// <para>
/// <b>Une chose nommée plutôt qu'un booléen nu.</b> La question « ce déploiement sait-il publier ? »
/// se pose à plusieurs endroits — le bandeau de « Routage RabbitMQ », et demain l'exécution
/// d'une demande routée —, et elle doit s'y lire sous le même nom, avec la même réponse. Un
/// <c>bool</c> se serait relu « vrai de quoi ? » à chaque appel.
/// </para>
/// <para>
/// ⚠️ <b>« Absente » est un état légal</b>, jamais une erreur de configuration : un service sans bus
/// démarre normalement, et un routage reste enregistrable avant que le broker existe (ADR-0027).
/// </para>
/// <para>
/// ⚠️ <b>Elle ne promet aucun broker joignable.</b> Elle dit ce que le déploiement <b>déclare</b>,
/// jamais ce qu'il atteint : aucun test réseau ne se cache derrière ce type, et une connexion
/// <see cref="Configured"/> devant un broker éteint est un cas prévu, dont l'exécution — et elle
/// seule — découvrira l'échec.
/// </para>
/// <para>
/// <b>La hiérarchie est fermée</b> par un constructeur privé : les deux cas sont déclarés ici, et un
/// <c>switch</c> sur les deux est donc complet pour toujours, sans branche nulle.
/// </para>
/// </remarks>
public abstract record BrokerConnection
{
  private BrokerConnection()
  {
  }

  /// <summary>
  /// La connexion que déclare un déploiement dont l'hôte de broker est <paramref name="hostName"/>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est ici — et nulle part ailleurs — qu'une clé vide ou faite d'espaces vaut une clé
  /// absente</b> (ADR-0028) : un hôte de broker qui ne nomme aucune machine ne connecte rien.
  /// Recopiée chez chaque lecteur, la règle aurait divergé, et deux publics auraient lu deux vérités
  /// de la même configuration.
  /// <para>
  /// ⚠️ <b>Elle ne joint rien</b> : ni résolution de nom, ni socket. Elle lit ce qui est écrit.
  /// </para>
  /// </remarks>
  /// <param name="hostName">L'hôte tel que le déploiement l'écrit — ou <c>null</c> s'il n'écrit rien.</param>
  public static BrokerConnection Declaring(string? hostName) =>
    string.IsNullOrWhiteSpace(hostName) ? Absent.Instance : Configured.Instance;

  /// <summary>
  /// Le déploiement <b>déclare une connexion</b> : il sait où publier. ⚠️ Toutes les instances sont
  /// égales — c'est un record sans champ —, et <see cref="Instance"/> évite d'en allouer une par
  /// lecture. Hôte, port, vhost et identifiants restent au déploiement : ce type dit qu'il y en a
  /// un, pas lequel.
  /// </summary>
  public sealed record Configured : BrokerConnection
  {
    /// <summary>L'unique « connexion configurée » dont une lecture a besoin.</summary>
    public static readonly Configured Instance = new();
  }

  /// <summary>
  /// Le déploiement <b>n'a aucune connexion</b> : rien ne partira sur le bus, quels que soient les
  /// routages posés. État <b>valide et normal</b>, jamais un manque à combler.
  /// </summary>
  public sealed record Absent : BrokerConnection
  {
    /// <summary>L'unique « connexion absente » dont une lecture a besoin.</summary>
    public static readonly Absent Instance = new();
  }
}
