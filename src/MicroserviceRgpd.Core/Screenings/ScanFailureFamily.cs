namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// De quel côté vient ce qui a fait tomber un <c>Scan</c> : de l'<c>Operator</c>, du réseau, ou de
/// la base. Trois familles, au grain du <b>geste que l'<c>Operator</c> peut poser</b> — exactement
/// comme <see cref="PreviewAbsenceReason"/> un cran plus bas.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Elle ne porte jamais le message du pilote, l'hôte ni l'utilisateur.</b> C'est par là que la
/// chaîne de connexion se reconstituerait par morceaux, et <c>Rien de réel ne reste</c> l'interdit.
/// Une famille est un mot du service, pas un texte du pilote recopié : le type ne sait pas porter de
/// prose venue d'ailleurs, et c'est ce qui tient la règle plutôt qu'une relecture.
/// </para>
/// <para>
/// ⚠️ <b>Trois familles, et non une par panne rencontrée.</b> « Hôte injoignable », « délai
/// dépassé » et « connexion tombée en cours de relevé » n'offrent pas à l'<c>Operator</c> des gestes
/// différents : elles se rangent ensemble sous <see cref="Network"/>. Une énumération qui grandirait
/// au fil des pannes de production serait, à chaque panne neuve, une valeur que du code déjà écrit
/// ne sait pas afficher.
/// </para>
/// </remarks>
public sealed class ScanFailureFamily : SmartEnum<ScanFailureFamily>
{
  /// <summary>
  /// Ce que l'<c>Operator</c> a fourni : chaîne malformée, fichier absent, identifiants refusés.
  /// C'est la seule des trois qu'il corrige seul, et l'écran le dit.
  /// </summary>
  public static readonly ScanFailureFamily Supplied = new(
    nameof(Supplied),
    1,
    "ce qui a été fourni au service",
    "La chaîne de connexion, le fichier ou le compte fourni n'a pas permis d'atteindre la base.");

  /// <summary>Le réseau : hôte injoignable, délai dépassé, connexion tombée en cours de route.</summary>
  public static readonly ScanFailureFamily Network = new(
    nameof(Network),
    2,
    "le réseau",
    "Le service n'a pas pu joindre la base, ou a perdu la connexion en cours de lecture.");

  /// <summary>La base elle-même : elle a répondu, et ce qu'elle a répondu est un échec.</summary>
  public static readonly ScanFailureFamily Database = new(
    nameof(Database),
    3,
    "la base",
    "La base a refusé la lecture ou n'a pas su y répondre.");

  private ScanFailureFamily(string name, int value, string frenchLabel, string statement)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
    Statement = statement;
  }

  /// <summary>Ce que l'écran nomme.</summary>
  public string FrenchLabel { get; }

  /// <summary>La phrase rendue à l'<c>Operator</c>, écrite ici et nulle part ailleurs.</summary>
  public string Statement { get; }
}
