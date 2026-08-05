namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// Ce que vaut le <c>Manifest</c> confronté aux <c>Adapter</c> qui le servent, à l'instant où
/// quelqu'un l'a demandé.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le <c>Manifest</c> est déclaratif, et il vieillit exprès.</b> Il n'existe aucun autre moyen de
/// savoir qu'il ment : rien ne l'engendre, rien ne le complète, et ce qu'il déclare de travers ne
/// fait aucun bruit. Cette confrontation est le seul bruit qu'on sache produire — et elle
/// <b>rapporte</b>, elle ne corrige pas.
/// </para>
/// <para>
/// <b>Elle ne se présente jamais comme un recensement.</b> Elle ne parle que des systèmes dotés
/// d'une adresse d'<c>Adapter</c>, c'est-à-dire de la minorité que le service touche ; les autres —
/// le régime majoritaire — ne sont vérifiables par personne, et leur silence ici ne dit rien de leur
/// exactitude.
/// </para>
/// <para>
/// <b>Rien ne tourne pour la produire.</b> Elle se recalcule quand on la demande, comme la file de
/// l'<c>Operator</c> : un processus de fond interrompu rendrait un rapport <b>vide et
/// rassurant</b>, soit l'<c>Omission silencieuse</c> sous sa forme la plus dangereuse.
/// </para>
/// </remarks>
/// <param name="VerifiedOn">
/// L'instant où la confrontation a eu lieu. Il se relit à côté de la date de déclaration de chaque
/// système : un écart n'a pas le même sens selon qu'il sépare deux affirmations du même jour ou une
/// déclaration de l'an dernier d'une sonde de ce matin.
/// </param>
/// <param name="Adapters">
/// Les systèmes confrontés, dans l'ordre du <c>Manifest</c>. Vide est un résultat — un catalogue où
/// aucun système n'a d'adresse — et jamais une absence.
/// </param>
public sealed record ManifestVerification(
  DateTimeOffset VerifiedOn,
  IReadOnlyList<AdapterVerification> Adapters)
{
  /// <summary>Les <c>Adapter</c> que la sonde a trouvés nus. Ils se lisent avant tout le reste.</summary>
  public IReadOnlyList<AdapterVerification> Naked =>
    [.. Adapters.Where(adapter => adapter.Exposure == AdapterExposure.Naked)];

  /// <summary>Les systèmes dont le catalogue et l'<c>Adapter</c> se contredisent.</summary>
  public IReadOnlyList<AdapterVerification> Disagreeing =>
    [.. Adapters.Where(adapter => adapter.Disagrees)];
}
