namespace MicroserviceRgpd.Core.Qualifications.Audit;

/// <summary>
/// L'écrit d'un acte de qualification : le verdict rendu, <b>et les prémisses dont il est tiré</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>La finalité est la redevabilité seule.</b> Le débogage n'en est qu'un effet de bord borné, et
/// l'amélioration du moteur est explicitement écartée : ce serait une finalité propre, qui ferait
/// basculer le service de sous-traitant à responsable de traitement.
/// </para>
/// <para>
/// <b>Les deux avis bruts sont conservés</b>, alors même que le contrat public les refuse à
/// l'appelant. La logique s'inverse parce que la trace est interne : les avis sont ce qui explique
/// le signal de relecture, et enregistrer une conclusion sans ses prémisses ne permettrait de
/// répondre de rien.
/// </para>
/// <para>
/// <b>Aucun champ ne nomme le mode dégradé</b> : la nullité des deux avis l'enregistre, et elle dit
/// davantage que le booléen public — un avis de verdict absent signe le repli sur le lexique, un avis
/// du lexique absent signe le contrôle manquant. Le booléen recouvre les deux ; la trace les distingue.
/// </para>
/// <para>
/// <b>Les deux avis ne sont jamais absents ensemble</b> : sans avis il n'y a pas de verdict, donc
/// rien à tracer. Une double panne, un dépassement d'échéance ou une annulation ne laissent aucune
/// ligne — la trace enregistre les verdicts, jamais les tentatives.
/// </para>
/// </remarks>
/// <param name="QualificationId">
/// L'identité que le service a donnée à cette qualification, et la <b>clé</b> de la trace. C'est par
/// elle, et par aucun identifiant de télémétrie, qu'on répondra plus tard d'un verdict.
/// </param>
/// <param name="OccurredAt">L'instant où l'acte a eu lieu, en UTC.</param>
/// <param name="Text">Le texte qualifié, conservé intégral et en clair.</param>
/// <param name="Qualification">Le verdict rendu à l'appelant.</param>
/// <param name="ReviewSignal">L'urgence à relire, telle qu'elle a été rendue.</param>
/// <param name="VerdictOpinion">
/// L'avis du moteur qui fait verdict, tel qu'il est arrivé — <c>null</c> quand ce moteur n'a rien
/// rendu, et c'est alors le repli sur le lexique que la ligne enregistre.
/// </param>
/// <param name="LexiconOpinion">
/// L'avis du moteur lexical, tel qu'il est arrivé — <c>null</c> quand ce moteur n'a rien rendu, et
/// c'est alors un verdict resté sans contrôle que la ligne enregistre.
/// </param>
/// <param name="Justification">La phrase rendue à l'opérateur, quand il y en a eu une.</param>
/// <param name="CallerReference">La référence de l'appelant, conservée verbatim, ou absente.</param>
/// <param name="TraceId">
/// La trace de télémétrie de l'échange, quand la propagation en a fourni une. <b>Facultative par
/// nature</b> : son absence dit que la propagation est cassée en amont, ce qu'un identifiant de
/// repli masquerait. Elle ne remplace jamais <paramref name="QualificationId"/>, dont la rétention
/// n'échappe pas au service.
/// </param>
/// <param name="TotalLatency">Le temps qu'a pris la qualification entière, hors écriture de la trace.</param>
/// <param name="VerdictLatency">Le temps qu'a pris le moteur de verdict, ou rien s'il n'a pas rendu d'avis.</param>
/// <param name="LexiconLatency">Le temps qu'a pris le moteur lexical, ou rien s'il n'a pas rendu d'avis.</param>
public sealed record QualificationAuditEntry(
  Guid QualificationId,
  DateTimeOffset OccurredAt,
  RightsRequestText Text,
  Qualification Qualification,
  ReviewSignal ReviewSignal,
  QualificationOpinion? VerdictOpinion,
  QualificationOpinion? LexiconOpinion,
  string? Justification,
  string? CallerReference,
  string? TraceId,
  TimeSpan TotalLatency,
  TimeSpan? VerdictLatency,
  TimeSpan? LexiconLatency);
