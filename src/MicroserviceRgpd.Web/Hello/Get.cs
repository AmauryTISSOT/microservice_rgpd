namespace MicroserviceRgpd.Web.Hello;

public class Get : EndpointWithoutRequest<HelloResponse>
{
  public override void Configure()
  {
    Get("/hello");
    AllowAnonymous();

    Summary(s =>
    {
      s.Summary = "Renvoie un message de bienvenue";
      s.Description = "Endpoint de fumee : confirme que l'API repond, sans toucher a la base.";
      s.Responses[200] = "Message renvoye";
    });

    Tags("Hello");
  }

  public override Task HandleAsync(CancellationToken cancellationToken) =>
    Send.OkAsync(new HelloResponse("Hello world"), cancellationToken);
}

public record HelloResponse(string Message);
