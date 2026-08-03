namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Le catalogue des <see cref="DeclaredSystem"/>, déclaré par un humain et détenu par le service.
/// À <b>gros grain</b> : des systèmes, jamais des champs.
/// <para>
/// <b>Il ne se présente jamais comme complet.</b> C'est le seul endroit du dispositif où l'on parle
/// des systèmes que le service ne touche pas — l'export mensuel parti chez l'agence n'a pas
/// d'<c>Adapter</c>, pas même de serveur, et c'est pourtant lui qu'il faut voir écrit dans le
/// dossier. Rien ne l'engendre, rien ne le complète, et ce qu'il ignore ne fait aucun bruit.
/// </para>
/// </summary>
/// <remarks>
/// <b>Il n'est pas un agrégat.</b> Chaque <see cref="DeclaredSystem"/> se déclare, se relit et se
/// révise seul, et porte sa propre date : faire du catalogue entier la racine sérialiserait la
/// saisie de six systèmes indépendants et daterait ensemble des affirmations faites séparément. Ce
/// type est la vue qu'on en prend à un instant, pas la chose qu'on écrit.
/// </remarks>
public sealed class Manifest
{
  private Manifest(IReadOnlyList<DeclaredSystem> systems)
  {
    Systems = systems;
  }

  /// <summary>Un catalogue sans aucun système déclaré — l'état d'un service qu'on vient d'installer.</summary>
  public static Manifest Empty { get; } = new([]);

  /// <summary>
  /// Les systèmes recensés, rangés par libellé. L'ordre est celui sous lequel l'<c>Operator</c>
  /// cherche, et il est figé pour qu'un même catalogue ne se relise pas dans deux ordres.
  /// </summary>
  public IReadOnlyList<DeclaredSystem> Systems { get; }

  /// <summary>
  /// La plus ancienne date de déclaration du catalogue, ou <c>null</c> s'il est vide. C'est elle
  /// qu'on nomme lorsqu'on dit de quand date ce recensement : la fraîcheur d'un catalogue est celle
  /// de sa ligne la plus vieille, jamais celle de la dernière touchée.
  /// </summary>
  public DateTimeOffset? OldestDeclaration =>
    Systems.Count == 0 ? null : Systems.Min(system => system.DeclaredOn);

  /// <summary>Prend la vue du catalogue que forment ces systèmes.</summary>
  public static Manifest Of(IEnumerable<DeclaredSystem> systems)
  {
    ArgumentNullException.ThrowIfNull(systems);

    return new Manifest(
      [.. systems.OrderBy(system => system.Label.Value, StringComparer.CurrentCulture)]);
  }
}
