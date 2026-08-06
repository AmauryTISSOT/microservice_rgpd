namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// La forme d'une réponse à <c>locate</c> <b>telle qu'elle arrive du fil</b>, avant qu'on en croie
/// quoi que ce soit : tout y est facultatif, et rien n'y est garanti.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle est distincte de <see cref="LocateFindings"/>, et la distinction est tout son propos.</b>
/// Ce type-ci décrit ce qu'un <c>Adapter</c> a le droit d'écrire ; l'autre décrit ce que le service
/// consent à tenir pour vrai. Un seul type aurait obligé le domaine à porter des <c>null</c>
/// partout, et l'écran aurait fini par afficher une réserve sans motif.
/// </para>
/// <para>
/// <b>Un corps vide — ou <c>{}</c> — est le zéro unique</b>, et non une panne : l'<c>Adapter</c> n'a
/// pas à distinguer « cherché, rien trouvé » de « désignation insuffisante », distinction qu'il ne
/// peut pas faire.
/// </para>
/// </remarks>
/// <param name="Certain">
/// Le noyau certain : ce que l'application rattache à la personne <b>sans hésiter</b>, en références
/// opaques de son propre vocabulaire.
/// </param>
/// <param name="Reserved">Les lignes trouvées <b>sans trancher</b>, chacune avec sa prose de motif.</param>
public sealed record LocateOnTheWire(
  IReadOnlyList<string?>? Certain,
  IReadOnlyList<ReservedOnTheWire?>? Reserved);

/// <summary>Une réserve telle qu'elle arrive du fil.</summary>
/// <param name="Reference">La référence opaque de la ligne, dans le vocabulaire de l'application.</param>
/// <param name="Reason">
/// Pourquoi l'application hésite, <b>en prose française</b>. C'est ce que l'<c>Operator</c> lira tel
/// quel pour arbitrer ; le contrat l'exige, et le service ne l'écrit pas à la place de personne.
/// </param>
/// <param name="Designations">
/// Ce que cette réserve propose de verser au sac — <b>le seul champ que le service interprète</b>.
/// Absent, la réserve reste locale et opaque.
/// </param>
public sealed record ReservedOnTheWire(
  string? Reference,
  string? Reason,
  IReadOnlyList<DesignationOnTheWire?>? Designations);

/// <summary>
/// Une désignation sur le fil : sa <b>nature par son mot canonique</b> et sa valeur, telles que le
/// contrat les fixe.
/// </summary>
/// <remarks>
/// <b>Elle sert les deux sens</b> — le sac qui part, les désignations qu'une réserve propose — et
/// elle est écrite une fois : deux copies de la même forme finiraient par ne plus décrire la même,
/// la seconde dérivant sans que rien ne s'en aperçoive.
/// </remarks>
public sealed record DesignationOnTheWire(string? Kind, string? Value);
