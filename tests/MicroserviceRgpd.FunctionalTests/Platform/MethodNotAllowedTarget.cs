using FastEndpoints;

namespace MicroserviceRgpd.FunctionalTests.Platform;

/// <summary>
/// Une cible en <c>GET</c> seul, et rien d'autre. Prouver qu'un 405 sort au format
/// <c>application/problem+json</c> demande une route que la plateforme refuse en <c>POST</c> ; ce
/// besoin est celui de la suite de tests, pas du service, et l'API n'a donc pas a porter une route
/// publique pour le satisfaire.
/// <para>
/// Elle est declaree en <c>FastEndpoints</c>, et non en route minimale : le format d'erreur eprouve
/// ici est celui que produit ce pipeline. Une route posee a cote sortirait du pipeline, et le test
/// passerait au vert en affirmant autre chose que ce qu'il annonce.
/// </para>
/// </summary>
public class MethodNotAllowedTarget : EndpointWithoutRequest
{
  public const string Route = "/tests/methode-refusee";

  public override void Configure()
  {
    Get(Route);
    AllowAnonymous();
  }

  public override Task HandleAsync(CancellationToken cancellationToken) =>
    Send.OkAsync(cancellationToken);
}
