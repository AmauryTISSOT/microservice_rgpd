namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// Ce qu'un <c>Adapter</c> a répondu à un appel du service. Quatre valeurs, dont <b>deux refus qui
/// ne se confondent pas</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le mot « verdict » est écarté.</b> Il appartient à la <c>Qualification</c>, qui ne connaît que
/// l'instant d'un verdict, et la posture du service interdit d'en faire rendre un par une machine.
/// Ce que ce type nomme n'est l'issue de rien : c'est ce que l'autre bout a répondu.
/// </para>
/// <para>
/// <b>Les deux refus sont distincts parce qu'on ne les répare pas au même endroit.</b> Un secret
/// invalide se répare dans la configuration de déploiement — des deux côtés, et d'un seul geste
/// coordonné ; un <c>system_id</c> non servi se répare dans le <c>Manifest</c> ou dans
/// l'<c>Adapter</c>, et il dit que le paysage déclaré et le programme qui le sert ne parlent plus
/// du même système. Les fondre en un « refusé » ferait chercher l'exploitant au mauvais endroit une
/// fois sur deux.
/// </para>
/// <para>
/// <b>Un refus n'est pas une panne.</b> L'<c>Adapter</c> a répondu, et il a répondu clairement :
/// c'est une réponse, qu'on consigne et qu'on signale. Ce qui n'est ni réponse ni refus — un
/// serveur muet, un statut inattendu, un <c>202</c> sans échéance lisible — arrive par
/// <see cref="AdapterFailure"/>, et n'a pas de valeur ici.
/// </para>
/// </remarks>
public sealed class AdapterOutcome : SmartEnum<AdapterOutcome>
{
  /// <summary>L'<c>Adapter</c> a servi l'appel, et rend ce qu'il a trouvé.</summary>
  public static readonly AdapterOutcome Served = new(nameof(Served), 0, "servi", isRefusal: false);

  /// <summary>
  /// L'<c>Adapter</c> a pris l'appel et déclare une <b>échéance</b> : le travail est long, et il ne
  /// tiendra pas la connexion. Le service repassera de lui-même, à l'ouverture du dossier.
  /// </summary>
  public static readonly AdapterOutcome Deferred = new(nameof(Deferred), 1, "différé", isRefusal: false);

  /// <summary>
  /// Le secret partagé n'est pas celui que l'<c>Adapter</c> attend — ou il manquait. <b>Un refus de
  /// topologie</b> : il ne dit rien du système appelé, et se répare dans la configuration de
  /// déploiement des deux côtés.
  /// </summary>
  public static readonly AdapterOutcome SecretRefused = new(nameof(SecretRefused), 2, "secret refusé", isRefusal: true);

  /// <summary>
  /// L'<c>Adapter</c> ne sert pas ce <c>system_id</c>. <b>Un désaccord entre le <c>Manifest</c> et
  /// l'<c>Adapter</c></b> : le paysage déclaré désigne une adresse qui ne connaît pas ce système,
  /// et personne ne corrige l'un par l'autre en silence.
  /// </summary>
  public static readonly AdapterOutcome SystemNotServed = new(nameof(SystemNotServed), 3, "système non servi", isRefusal: true);

  private AdapterOutcome(string name, int value, string frenchLabel, bool isRefusal)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
    IsRefusal = isRefusal;
  }

  /// <summary>Le libellé destiné à l'humain qui relit. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>
  /// L'appel a-t-il été refusé ? C'est ce qui décide qu'une tentative datée entre au <c>Ledger</c>
  /// et qu'un désaccord est signalé, jamais qu'un <c>Step</c> bouge : <b>un appel refusé n'est pas
  /// une affaire de <c>Case</c></b>.
  /// </summary>
  public bool IsRefusal { get; }

  /// <summary>
  /// Cette réponse, si c'est un refus — sinon une programmation fautive nommée.
  /// </summary>
  /// <remarks>
  /// Le garde vit ici plutôt qu'en trois exemplaires chez ceux qui ne traitent que des refus : trois
  /// gardes valant chacun la même chose finiraient par ne plus dire la même chose, et ce qu'un
  /// développeur lirait à trois endroits doit être écrit à un seul.
  /// </remarks>
  /// <param name="outcome">Ce que l'<c>Adapter</c> a répondu.</param>
  /// <param name="parameterName">Le paramètre à nommer si ce n'en est pas un.</param>
  /// <exception cref="ArgumentNullException"><paramref name="outcome"/> est absent.</exception>
  /// <exception cref="ArgumentException">La réponse n'est pas un refus.</exception>
  public static AdapterOutcome RefusalOrThrow(AdapterOutcome outcome, string parameterName)
  {
    ArgumentNullException.ThrowIfNull(outcome);

    if (!outcome.IsRefusal)
    {
      throw new ArgumentException(
        $"« {outcome.Name} » n'est pas un refus : un Adapter qui sert ou qui diffère a répondu, et "
        + "ce qu'il devient appartient au dossier, non à la preuve du transport.",
        parameterName);
    }

    return outcome;
  }
}
