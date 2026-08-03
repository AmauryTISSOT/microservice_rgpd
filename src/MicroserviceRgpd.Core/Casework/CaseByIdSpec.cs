namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Le dossier que désigne cet identifiant, ou rien. C'est une <b>spécification</b> et non un
/// <c>GetByIdAsync</c> : la clé est un type du domaine que le dépôt ne sait pas rapprocher de la
/// colonne sans passer par la conversion déclarée sur le modèle.
/// </summary>
/// <remarks>
/// Les <see cref="Claim"/> et leurs <see cref="Step"/> arrivent avec, sans qu'aucun
/// <c>Include</c> ne le demande : ils appartiennent à la racine, et un dossier relu sans son
/// travail dû serait un dossier dont l'incomplétude ne se voit pas.
/// </remarks>
public sealed class CaseByIdSpec : SingleResultSpecification<Case>
{
  public CaseByIdSpec(CaseId id)
  {
    Query.Where(opened => opened.Id == id);
  }
}
