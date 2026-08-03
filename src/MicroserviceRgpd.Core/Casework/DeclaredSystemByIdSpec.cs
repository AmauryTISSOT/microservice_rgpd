namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Le système que l'humain désigne par son identifiant, ou rien. C'est une <b>spécification</b> et
/// non un <c>GetByIdAsync</c> : la clé est un type du domaine que le dépôt ne sait pas rapprocher
/// de la colonne sans passer par la conversion déclarée sur le modèle.
/// </summary>
public sealed class DeclaredSystemByIdSpec : SingleResultSpecification<DeclaredSystem>
{
  public DeclaredSystemByIdSpec(DeclaredSystemId id)
  {
    Query.Where(system => system.Id == id);
  }
}
