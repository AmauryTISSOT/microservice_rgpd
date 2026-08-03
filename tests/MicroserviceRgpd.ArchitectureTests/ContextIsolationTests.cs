namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// L'optionnalité de <c>Qualification</c>, rendue vérifiable : <b>aucune dépendance de compilation
/// <c>Casework</c> → <c>Qualification</c></b>. Sans cette règle, la promesse qu'un <c>Case</c>
/// s'ouvre, s'instruise et se close sans qu'aucune qualification n'ait eu lieu ne serait qu'une
/// intention — voir <c>CONTEXT-MAP.md</c>.
/// <para>
/// Le contrôle se fait au niveau de l'<b>IL</b>, et non des signatures : un gestionnaire de
/// <c>Casework</c> qui appelle le moteur depuis un corps de méthode n'expose rien dans sa surface,
/// et c'est précisément la fuite que l'on craint. Ce que l'inspecteur voit vraiment est établi par
/// <see cref="ContextInspectorTests"/>, sur des témoins écrits pour ça.
/// </para>
/// <para>
/// ⚠️ Ce que l'IL ne peut pas montrer, écrit ici plutôt que découvert un jour de panne : une
/// <c>const</c> lue d'un contexte à l'autre <b>disparaît</b> du compilé — le compilateur en recopie
/// la valeur sur place. Le garde ne la voit pas, et n'a rien à voir : il ne reste aucune dépendance,
/// ni à la compilation ni à l'exécution. C'est une duplication de source, pas un couplage.
/// </para>
/// <para>
/// ⚠️ Tant que <c>Casework</c> n'a pas de code, cette règle ne trouve rien à examiner : elle est
/// verte par vacuité. C'est voulu — on pose le garde <b>avant</b> le contexte qu'il garde, pour
/// que la première ligne de <c>Casework</c> naisse déjà sous surveillance.
/// </para>
/// </summary>
public class ContextIsolationTests
{
  public static TheoryData<string> ProductionAssemblies => [.. ProductionAssembly.All];

  [Theory]
  [MemberData(nameof(ProductionAssemblies))]
  public void NoCaseworkTypeReachesQualification(string assembly)
  {
    var crossings = ContextInspector.Inspect(
      ProductionAssembly.PathOf(assembly),
      from: ContextInspector.Casework,
      to: ContextInspector.Qualification);

    crossings.ShouldBeEmpty(
      $"{assembly} porte une dépendance {ContextInspector.Casework} → {ContextInspector.Qualification} :" +
      Environment.NewLine + string.Join(Environment.NewLine, crossings) + Environment.NewLine +
      "Une demande peut arriver déjà qualifiée : un Case s'instruit sans qu'aucune qualification " +
      "n'ait eu lieu. Passez par le noyau partagé, ou par un qualificationId opaque.");
  }
}
