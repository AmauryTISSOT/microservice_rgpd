namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Le jour où le responsable de traitement a reçu la demande, et <b>la façon dont le service le
/// sait</b> : déclaré par quelqu'un, ou tenu pour défaut faute de déclaration.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le mois de l'art. 12.3 court avant nous et rien ne l'arrête.</b> Le service hérite d'un
/// compteur lancé depuis un nombre de jours qu'il ignore ; c'est de cette date que tout se
/// recalcule, et elle lui est <b>déclarée</b>, jamais constatée.
/// </para>
/// <para>
/// <b>Les deux régimes ne se confondent pas, et c'est tout le propos de ce type.</b> Une date nue
/// serait indiscernable d'une date affirmée par un humain : l'écran nommerait un fait là où il n'y a
/// qu'une hypothèse du service, et le <c>Ledger</c> garderait la même trace des deux. Le drapeau
/// vit donc à côté de la date, dans le même objet, où aucun chemin d'écriture ne peut poser l'une
/// sans l'autre.
/// </para>
/// </remarks>
public sealed record ReceptionDate
{
  /// <summary>
  /// Ce que le service tient pour <b>déjà couru</b> quand personne n'a déclaré la date. Neuf jours :
  /// une demande transcrite d'une boîte aux lettres y a séjourné, et supposer le mois parti à
  /// l'instant du dépôt rendrait un délai rassurant à un dossier dont personne ne sait depuis quand
  /// il attend. Le nombre n'est <b>pas réglable</b> — une case à régler par déploiement serait une
  /// case à laisser pourrir, et sa valeur basse serait celle que tout le monde garderait.
  /// </summary>
  public const int DaysHeldAlreadyRunByDefault = 9;

  private ReceptionDate(DateTimeOffset on, bool isDefault)
  {
    On = on;
    IsDefault = isDefault;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private ReceptionDate()
  {
  }

  /// <summary>
  /// La date depuis laquelle le délai se compte, en UTC. <c>timestamptz</c> ne conserve pas le
  /// décalage, et laisser passer une heure locale ferait varier une échéance juridique d'un serveur
  /// à l'autre.
  /// </summary>
  public DateTimeOffset On { get; private set; }

  /// <summary>
  /// Le service l'a-t-il <b>tenue pour défaut</b> ? Ce drapeau est ce que l'écran nomme
  /// « J+9 (défaut) » et ce que le <c>Ledger</c> consigne : le défaut doit être visible <b>comme un
  /// défaut</b>, jamais confondu avec un fait déclaré.
  /// </summary>
  public bool IsDefault { get; private set; }

  /// <summary>La date qu'un humain a affirmée, telle qu'il l'a affirmée.</summary>
  public static ReceptionDate Declared(DateTimeOffset on) => new(on.ToUniversalTime(), isDefault: false);

  /// <summary>
  /// Faute de déclaration : <see cref="DaysHeldAlreadyRunByDefault"/> jours tenus pour déjà courus
  /// avant le dépôt.
  /// </summary>
  /// <param name="depositedAt">L'instant où la demande est entrée dans le service.</param>
  public static ReceptionDate Defaulted(DateTimeOffset depositedAt) =>
    new(depositedAt.ToUniversalTime().AddDays(-DaysHeldAlreadyRunByDefault), isDefault: true);
}
