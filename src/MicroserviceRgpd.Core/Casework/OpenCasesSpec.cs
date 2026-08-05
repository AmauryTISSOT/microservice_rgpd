namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Les dossiers <b>ouverts</b>, avec leurs <see cref="Claim"/> et leurs <see cref="Step"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aucune échéance n'est calculée ici.</b> Le tri de la file se fait sur un calcul fait à
/// l'instant de l'affichage, et une base ne connaît pas cet instant : lui demander de trier
/// obligerait à y persister une échéance, c'est-à-dire exactement le drapeau que ce contexte refuse.
/// La requête rend les lignes présentes ; l'échéance les range.
/// </para>
/// <para>
/// <b>Aucune pagination.</b> Quelques demandes par an, et une page suivante qu'on n'ouvre pas est
/// exactement la forme d'<c>Omission silencieuse</c> qu'un écran peut fabriquer.
/// </para>
/// </remarks>
public sealed class OpenCasesSpec : Specification<Case>
{
  public OpenCasesSpec()
  {
    Query.Where(opened => opened.State == CaseState.Open);
  }
}
