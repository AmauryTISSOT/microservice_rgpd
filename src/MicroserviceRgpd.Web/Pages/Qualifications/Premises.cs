using System.Globalization;
using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.UseCases.Qualifications.Qualify;

namespace MicroserviceRgpd.Web.Pages.Qualifications;

/// <summary>
/// Ce dont le verdict est <b>tiré</b>, mis en français pour le dépliant que l'écran range sous lui :
/// les deux avis, la confiance déclarée, l'identité de chaque moteur, et les trois latences.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deux raisons distinctes justifient ce que le dépliant montre, et elles restent séparées</b> —
/// c'est la clause de l'ADR-0011 qu'un lecteur pressé fondrait. Les <b>deux avis</b> et la
/// <b>confiance</b> sont là pour <b>expliquer le signal de relecture</b> : un écran qui annonce
/// « Contestée » sans dire sur quoi les moteurs divergent demande à un humain de croire une machine
/// qui refuse de s'expliquer. Les <b>identités de moteurs</b> et les <b>trois latences</b> ne sont
/// là que pour le <b>diagnostic</b> : elles n'expliquent rien du verdict, et servent à dire plus
/// tard quel moteur a rendu quel verdict, et ce que la qualification coûte réellement.
/// </para>
/// <para>
/// ⚠️ <b>Rien de tout cela ne franchit le contrat HTTP public.</b> <c>QualifyResponse</c> ne projette
/// aucune de ces valeurs, et le garde de l'endpoint le tient. Ce qui est élargi ici est ce que le
/// service montre à l'<c>Operator</c> qui est <b>devant lui</b>, jamais ce qu'il consent à dire à
/// une application tierce.
/// </para>
/// <para>
/// <b>La mise en français vit ici, pas dans le gabarit</b>, pour la même raison que dans
/// <see cref="Verdict"/> : une vue qui aurait tenu un <c>DeclaredConfidence</c> aurait mis du
/// domaine dans un fichier que rien ne relit.
/// </para>
/// </remarks>
/// <param name="VerdictOpinion">
/// L'avis dont le verdict est tiré, ou <c>null</c> quand ce moteur n'a rien rendu — et c'est alors
/// le lexique qui a tenu lieu de verdict.
/// </param>
/// <param name="LexiconOpinion">
/// L'avis qui contrôle le verdict, ou <c>null</c> quand ce moteur n'a rien rendu — et c'est alors un
/// verdict resté sans contrôle.
/// </param>
/// <param name="TotalLatency">
/// Ce que la qualification entière a pris. <b>Elle est toujours présente</b>, à la différence de
/// celles des moteurs : le service a bien duré, même quand l'un des deux s'est tu.
/// </param>
public sealed record Premises(
  RenderedOpinion? VerdictOpinion,
  RenderedOpinion? LexiconOpinion,
  string TotalLatency)
{
  /// <summary>
  /// Ce que le résumé du dépliant <b>dit</b>, et il nomme ce qu'il cache : un dépliant intitulé
  /// « Détails » ou « Avancé » ne dit pas s'il vaut la peine d'être ouvert.
  /// </summary>
  public const string Summary = "Les deux avis dont ce verdict est tiré";

  /// <summary>
  /// Les deux avis <b>dans l'ordre où ils se lisent</b> : celui dont le verdict est tiré, puis celui
  /// qui le contrôle. Ils paraissent <b>côte à côte</b> — c'est ce qui rend une divergence lisible
  /// d'un coup d'œil, là où deux blocs empilés se lisent l'un après l'autre.
  /// </summary>
  public IReadOnlyList<OpinionColumn> Opinions =>
  [
    new OpinionColumn(
      "L'avis dont le verdict est tiré",
      "Aucun avis : ce moteur n'a rien rendu, et c'est l'autre qui a tenu lieu de verdict.",
      VerdictOpinion),
    new OpinionColumn(
      "L'avis qui contrôle le verdict",
      "Aucun avis : ce moteur n'a rien rendu, et le verdict est resté sans contrôle.",
      LexiconOpinion),
  ];

  /// <summary>Ce qu'une qualification rendue donne à lire de ses prémisses.</summary>
  public static Premises Of(QualificationOutcome outcome)
  {
    ArgumentNullException.ThrowIfNull(outcome);

    return new Premises(
      RenderedOpinion.Of(outcome.VerdictOpinion, outcome.VerdictLatency),
      RenderedOpinion.Of(outcome.LexiconOpinion, outcome.LexiconLatency),
      LatencyOf(outcome.TotalLatency));
  }

  /// <summary>
  /// Une durée dite en millisecondes entières. <b>Aucune décimale</b> : un chiffre après la virgule
  /// inviterait à comparer deux exécutions d'une machine qui n'a rien promis de tel.
  /// </summary>
  internal static string LatencyOf(TimeSpan latency)
  {
    return string.Create(
      CultureInfo.InvariantCulture,
      $"{Math.Round(latency.TotalMilliseconds):F0} ms");
  }

  /// <summary>
  /// L'un des deux avis, avec ce qui se lit à sa place <b>quand le moteur n'a rien rendu</b> : une
  /// colonne laissée blanche se lirait comme un rendu tronqué, alors que c'est le service qui
  /// n'était pas entier.
  /// </summary>
  /// <param name="Heading">Ce que cet avis est dans la qualification — sa fonction, jamais le nom du moteur.</param>
  /// <param name="Absence">La phrase qui dit le silence de ce moteur-là, et ce qu'il a coûté au verdict.</param>
  /// <param name="Opinion">L'avis lui-même, ou <c>null</c> quand il n'y en a pas eu.</param>
  public sealed record OpinionColumn(string Heading, string Absence, RenderedOpinion? Opinion);
}
