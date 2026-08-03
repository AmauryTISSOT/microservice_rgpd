namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// Ce qu'un <c>Adapter</c> a répondu à un appel du service. Quatre valeurs, dont <b>deux refus qui
/// ne se confondent pas</b>.
/// </summary>
/// <remarks>
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
/// c'est un verdict, qu'on consigne et qu'on signale. Ce qui n'est ni une réponse ni un refus — un
/// serveur muet, un statut inattendu, un <c>202</c> sans échéance lisible — arrive par
/// <see cref="AdapterFailure"/>, et n'a pas de valeur ici.
/// </para>
/// </remarks>
public sealed class AdapterVerdict : SmartEnum<AdapterVerdict>
{
  /// <summary>L'<c>Adapter</c> a servi l'appel, et rend ce qu'il a trouvé.</summary>
  public static readonly AdapterVerdict Served = new(nameof(Served), 0, "servi", isRefusal: false);

  /// <summary>
  /// L'<c>Adapter</c> a pris l'appel et déclare une <b>échéance</b> : le travail est long, et il ne
  /// tiendra pas la connexion. Le service repassera de lui-même, à l'ouverture du dossier.
  /// </summary>
  public static readonly AdapterVerdict Deferred = new(nameof(Deferred), 1, "différé", isRefusal: false);

  /// <summary>
  /// Le secret partagé n'est pas celui que l'<c>Adapter</c> attend — ou il manquait. <b>Un refus de
  /// topologie</b> : il ne dit rien du système appelé, et se répare dans la configuration de
  /// déploiement des deux côtés.
  /// </summary>
  public static readonly AdapterVerdict SecretRefused = new(nameof(SecretRefused), 2, "secret refusé", isRefusal: true);

  /// <summary>
  /// L'<c>Adapter</c> ne sert pas ce <c>system_id</c>. <b>Un désaccord entre le <c>Manifest</c> et
  /// l'<c>Adapter</c></b> : le paysage déclaré désigne une adresse qui ne connaît pas ce système,
  /// et personne ne corrige l'un par l'autre en silence.
  /// </summary>
  public static readonly AdapterVerdict SystemNotServed = new(nameof(SystemNotServed), 3, "système non servi", isRefusal: true);

  private AdapterVerdict(string name, int value, string frenchLabel, bool isRefusal)
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
}
