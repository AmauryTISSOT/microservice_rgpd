namespace MicroserviceRgpd.Web.Casework;

/// <summary>
/// Ce que l'application du client envoie pour faire <b>entrer</b> une demande.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucun emplacement pour une pièce jointe, et c'est une propriété du type.</b> Ni fichier,
/// ni base 64, ni URL à récupérer : aucune pièce d'identité n'entre dans le service, tous canaux
/// confondus (CEPD § 79). Un champ qui pourrait en porter une serait un champ dans lequel une pièce
/// finirait par entrer, et le service aurait alors à prouver qu'il ne l'a pas gardée.
/// </para>
/// <para>
/// <b>Aucun champ de date de réception non plus.</b> Une demande postée par l'application arrive à
/// l'instant où elle est postée ; c'est le dépôt manuel, où l'<c>Operator</c> transcrit un courriel
/// reçu il y a un nombre de jours inconnu, qui aura besoin de la déclarer.
/// </para>
/// <para>
/// <b>Aucun champ d'identité déclarée non plus.</b> Ce canal en porte une seule et le service la
/// pose lui-même : une application qui pourrait déclarer <c>ChannelControl</c> ou
/// <c>OperatorAttested</c> choisirait la valeur propre à la place du fait, et le service
/// enregistrerait un faux sur la foi d'un appelant qu'il ne vérifie pas.
/// </para>
/// </remarks>
public sealed record OpenCaseRequest
{
  /// <summary>
  /// Le sac sous lequel on cherchera la personne — courriels, nom, téléphone, références. Sa forme
  /// est <b>identique à celle du dépôt manuel</b> : deux formes voudraient dire deux chemins de
  /// recherche à maintenir, et l'un des deux finirait par ne plus être celui qu'on croit.
  /// <para>
  /// <b>L'identifiant natif de l'application y entre comme une désignation de nature
  /// <c>reference</c></b>, parmi les autres et sans champ à lui : il n'a pas l'autorité d'un
  /// identifiant de personne, et le lui donner ferait passer pour un recensement ce qui n'est
  /// qu'une clé de plus.
  /// </para>
  /// </summary>
  public IReadOnlyList<DeclaredDesignation>? Designations { get; init; }

  /// <summary>
  /// Les droits que cette demande exerce, sous leurs noms canoniques anglais.
  /// <para>
  /// <b>Une demande portant plusieurs droits donne un seul dossier.</b> Éventuellement vide — une
  /// demande dont on ne reconnaît encore aucun droit entre quand même — et jamais
  /// <c>OutOfScope</c>, qui est le verdict qu'aucun droit n'a été reconnu et non un droit qu'on
  /// réclame.
  /// </para>
  /// </summary>
  public IReadOnlyList<string>? Rights { get; init; }
}

/// <summary>
/// Une désignation sur le fil : sa nature et sa valeur, et rien d'autre.
/// </summary>
/// <param name="Kind">
/// La nature, dans le vocabulaire fermé du contrat d'<c>Adapter</c> : <c>email</c>, <c>name</c>,
/// <c>phone</c>, <c>reference</c>.
/// </param>
/// <param name="Value">
/// La valeur déclarée. Le service <b>ne l'interprète jamais</b> : ni unicité, ni format, ni
/// exactitude — la personne n'a pas d'identifiant, et prétendre valider une désignation reviendrait
/// à prétendre savoir qui elle est.
/// </param>
public sealed record DeclaredDesignation(string? Kind, string? Value);
