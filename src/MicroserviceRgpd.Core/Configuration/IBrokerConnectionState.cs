namespace MicroserviceRgpd.Core.Configuration;

/// <summary>
/// <b>Ce déploiement sait-il publier ?</b> — la seule règle qui réponde, et le seul endroit où la
/// question se pose (ADR-0028).
/// </summary>
/// <remarks>
/// <para>
/// <b>Une propriété, et rien d'autre.</b> L'état de la connexion se lit ; il ne s'ouvre pas, ne se
/// teste pas et ne se ferme pas. Aucune implémentation de ce port n'a le droit de joindre un
/// broker pour répondre : la disponibilité d'un écran ne dépend jamais de celle du bus (ADR-0027).
/// </para>
/// <para>
/// ⚠️ <b>Ce que le déploiement déclare se juge sur <see cref="BrokerConnection.Declaring"/></b>, et
/// nulle part ailleurs : une implémentation de ce port lit la configuration, elle ne décide pas de
/// ce qu'une clé vide signifie.
/// </para>
/// </remarks>
public interface IBrokerConnectionState
{
  /// <summary>La connexion que ce déploiement déclare, à cet instant.</summary>
  BrokerConnection Current { get; }
}
