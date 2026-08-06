namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// Tout ce que le service sait d'une pièce que <c>Read</c> a rendue : son <b>type de contenu</b> et
/// son <b>nom de fichier</b>, tels que le transport les lui a mis dans la main.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle est recopiée, jamais interprétée.</b> Le service ne vérifie pas qu'un <c>Content-Type</c>
/// dit vrai, ne devine pas celui qui manque à partir des octets, et n'ouvre pas le corps pour en
/// juger. C'est le prix — et le propos — d'une réponse écrite dans le vocabulaire de l'application :
/// il n'existe aucun vocabulaire commun sur le terrain, et en inventer un le ferait payer à chaque
/// <c>Adapter</c>.
/// </para>
/// <para>
/// <b>Elle est ce qui rend <c>send_file()</c> suffisant côté client.</b> Un intégrateur qui rend son
/// export par la fonction de sa bibliothèque a déjà écrit les deux en-têtes sans y penser ; celui
/// qui n'en écrit aucun n'a rien fait de mal, et le nom <b>dégrade sur le <c>system_id</c></b> plutôt
/// que sur un « fichier sans nom » qui n'apprendrait rien à qui le reçoit.
/// </para>
/// <para>
/// ⚠️ <b>Du nom, seul le dernier segment est gardé.</b> Un <c>Content-Disposition</c> portant
/// <c>../../etc/passwd</c> ou <c>C:\exports\jean.csv</c> nomme un chemin de la machine du client :
/// le recopier ferait écrire, un jour, un fichier là où personne ne l'a demandé — et il dit de la
/// topologie du client ce que le service n'a pas à savoir. Le découpage n'est donc pas une
/// interprétation du nom mais le refus d'en tenir un qui n'en est pas un.
/// </para>
/// </remarks>
/// <param name="ContentType">Le type de contenu, recopié tel quel.</param>
/// <param name="FileName">Le nom de la pièce — dernier segment, ou le <c>system_id</c> à défaut.</param>
public sealed record TransportEnvelope(string ContentType, string FileName)
{
  /// <summary>
  /// Ce que vaut un <c>Content-Type</c> absent : <b>des octets, et rien de dit d'eux</b>. C'est la
  /// valeur que la norme réserve exactement à cela, et non un type deviné à la place du client.
  /// </summary>
  public const string UnnamedContentType = "application/octet-stream";

  /// <summary>
  /// Le plafond du nom, en unités UTF-16. Il borne une colonne, jamais un jugement sur le nom : ce
  /// qui dépasse est <b>coupé</b> plutôt que refusé, une pièce n'ayant pas à être perdue parce que
  /// son nom était long.
  /// </summary>
  public const int MaxFileNameLength = 255;

  /// <summary>
  /// Le plafond du type de contenu, en unités UTF-16. Même raison, et même conséquence : un
  /// <c>Content-Type</c> à rallonge est <b>coupé</b>, jamais refusé — une pièce perdue parce que son
  /// en-tête dépassait d'un caractère serait la donnée d'une personne perdue pour une colonne.
  /// </summary>
  public const int MaxContentTypeLength = 256;

  /// <summary>
  /// L'enveloppe telle que le transport l'a portée, ramenée à ce que le service consent à en garder.
  /// </summary>
  /// <remarks>
  /// <b>Rien n'est refusé ici, et c'est délibéré.</b> Une enveloppe manquante ou biscornue n'est pas
  /// une panne : c'est un <c>Adapter</c> qui a servi une pièce sans se soucier de la nommer, et lui
  /// rendre une erreur ferait perdre la pièce pour un en-tête. La <b>panne</b> est ailleurs — dans un
  /// statut hors contrat, ou dans un différé sans échéance.
  /// </remarks>
  /// <param name="contentType">Le <c>Content-Type</c> reçu, ou <c>null</c> s'il n'y en avait pas.</param>
  /// <param name="fileName">
  /// Le <c>filename=</c> du <c>Content-Disposition</c> reçu, ou <c>null</c> s'il n'y en avait pas.
  /// </param>
  /// <param name="declaredSystem">Le système appelé, sur lequel le nom dégrade quand il manque.</param>
  public static TransportEnvelope Of(string? contentType, string? fileName, DeclaredSystemId declaredSystem)
  {
    return new TransportEnvelope(
      TypeOf(contentType) ?? UnnamedContentType,
      NameOf(fileName) ?? declaredSystem.Value);
  }

  /// <summary>
  /// Le type de contenu tel qu'il est arrivé, ou <c>null</c> quand rien n'a été dit des octets.
  /// </summary>
  private static string? TypeOf(string? contentType)
  {
    var written = contentType?.Trim();

    return string.IsNullOrEmpty(written)
      ? null
      : written[..Math.Min(written.Length, MaxContentTypeLength)];
  }

  /// <summary>
  /// Le dernier segment du nom, ou <c>null</c> quand il n'en reste rien à garder.
  /// </summary>
  /// <remarks>
  /// Les guillemets tombent avec les blancs : un <c>filename="rapport.csv"</c> est le nom
  /// <c>rapport.csv</c>, et garder ses guillemets ferait afficher à l'écran une syntaxe d'en-tête là
  /// où l'<c>Operator</c> attend un nom de fichier.
  /// </remarks>
  private static string? NameOf(string? fileName)
  {
    var written = fileName?.Trim().Trim('"').Trim();

    if (string.IsNullOrEmpty(written))
    {
      return null;
    }

    // Les deux séparateurs, et non celui de la machine qui exécute : le chemin vient de celle du
    // client, dont le service ne connaît ni le système ni les usages.
    var segment = written[(written.LastIndexOfAny(['/', '\\']) + 1)..].Trim();

    return string.IsNullOrEmpty(segment)
      ? null
      : segment[..Math.Min(segment.Length, MaxFileNameLength)];
  }
}
