namespace MicroserviceRgpd.Infrastructure.Casework.Adapters;

/// <summary>
/// Le secret partagé que le service présente à chaque <c>Adapter</c>. <b>Un seul, et de la
/// topologie</b> : un par <c>Adapter</c>, jamais par <c>system_id</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il vit dans l'infrastructure, jamais dans le domaine.</b> Le <c>Manifest</c> décrit le
/// catalogue déclaré du client et se relit à l'écran ; un secret n'y a aucun emplacement, et n'en
/// aura pas — <c>AdapterAddress</c> le dit déjà en toutes lettres.
/// </para>
/// <para>
/// Il porte un type plutôt qu'un <c>string</c> : une chaîne se glisse dans une signature à la place
/// d'une autre sans que rien ne l'arrête, et celle-ci est la seule du dispositif qu'on ne veut voir
/// nulle part ailleurs.
/// </para>
/// </remarks>
/// <param name="Value">Le secret tel que la configuration de déploiement le donne.</param>
public sealed record AdapterSecret(string Value);
