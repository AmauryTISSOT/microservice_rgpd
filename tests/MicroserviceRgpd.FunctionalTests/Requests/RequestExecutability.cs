using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.UseCases.Configuration.SetRightEndpoint;
using MicroserviceRgpd.UseCases.Configuration.SetRightRabbitMqRouting;
using MicroserviceRgpd.Web.Pages.Requests;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Requests;

/// <summary>
/// <b>L'exécution d'une demande, telle que le tableau l'offre</b> : la quatrième action de chaque
/// ligne, « Exécuter la demande », active quand la demande s'exécute, éteinte sinon — et son
/// infobulle dit alors le premier motif de blocage (ADR-0026). Exercé par la seule frontière HTTP.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Chaque test part d'un Paramétrage vierge</b>, et le rend vierge en sortant : c'est un
/// singleton, et la base est partagée par toute la collection.
/// </para>
/// <para>
/// Les motifs attendus se lisent sur <see cref="ExecutionBlock"/> : c'est le serveur qui les écrit, et
/// ce qui se garde ici est que la ligne rend celui de sa demande.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class RequestExecutability(CustomWebApplicationFactory<Program> factory) : IAsyncLifetime
{
  private readonly RequestSurface _surface = new(factory);

  public Task InitializeAsync() => ForgetEveryEndpointAsync();

  public Task DisposeAsync() => ForgetEveryEndpointAsync();

  /// <summary>
  /// <b>En cours, identité vérifiée, avec un email, au droit configuré</b> : le bouton est actif, et
  /// son infobulle dit son nom.
  /// </summary>
  [Fact]
  public async Task OffersTheExecutionOfAnExecutableRequest()
  {
    await ConfigureAsync(DataSubjectRight.Access);
    var email = $"{Guid.NewGuid():N}@example.org";

    await _surface.RecordAsync(new Dictionary<string, string> { ["email"] = email, ["identityVerified"] = "true" });

    var (attributes, tooltip) = ExecutionButtonIn(await _surface.BoardRowWithAsync(email));

    attributes.ShouldNotContain("aria-disabled", Case.Sensitive, "L'exécution d'une demande exécutable est éteinte.");
    tooltip.ShouldBe(RequestRow.ExecutionOffered);
  }

  /// <summary>
  /// <b>Chaque motif éteint le bouton et se lit dans son infobulle</b>, sous le libellé du serveur ;
  /// le bouton garde son nom accessible.
  /// </summary>
  [Theory]
  [InlineData(nameof(ExecutionBlock.Closed))]
  [InlineData(nameof(ExecutionBlock.IdentityNotVerified))]
  [InlineData(nameof(ExecutionBlock.EmailMissing))]
  [InlineData(nameof(ExecutionBlock.RightNotConfigured))]
  [InlineData(nameof(ExecutionBlock.RabbitMqNotYetSupported))]
  public async Task DimsTheExecutionAndSaysTheBlock(string blockName)
  {
    var block = ExecutionBlock.FromName(blockName);
    var marker = Guid.NewGuid().ToString("N");

    if (block == ExecutionBlock.RabbitMqNotYetSupported)
    {
      await RouteAsync(DataSubjectRight.Erasure);
    }
    else if (block != ExecutionBlock.RightNotConfigured)
    {
      await ConfigureAsync(DataSubjectRight.Erasure);
    }

    var (id, _) = await _surface.RecordAsync(new Dictionary<string, string>
    {
      ["lastName"] = $"Martin-{marker}",
      ["firstName"] = "Jeanne",
      ["email"] = block == ExecutionBlock.EmailMissing ? "" : $"jeanne.{marker}@example.org",
      ["identityVerified"] = block == ExecutionBlock.IdentityNotVerified ? "false" : "true",
      ["right"] = nameof(DataSubjectRight.Erasure),
    });

    if (block == ExecutionBlock.Closed)
    {
      await _surface.SetStatusAsync(id, nameof(RequestStatus.Completed));
    }

    var (attributes, tooltip) = ExecutionButtonIn(await _surface.BoardRowWithAsync(marker));

    attributes.ShouldContain(@"aria-disabled=""true""", Case.Sensitive, "L'exécution n'est pas éteinte.");
    attributes.ShouldNotMatch(@"\sdisabled\b", "L'exécution est désactivée : son infobulle ne se montrerait jamais.");
    tooltip.ShouldBe(block.FrenchLabelFor(DataSubjectRight.Erasure));
  }

  /// <summary>
  /// ⚠️ <b>Quand plusieurs conditions manquent, seule la première se dit</b> : une demande close, non
  /// vérifiée, sans email, à un droit sans adresse, dit « Demande close ».
  /// </summary>
  [Fact]
  public async Task SaysOnlyTheFirstBlockWhenSeveralAreMissing()
  {
    var marker = $"Martin-{Guid.NewGuid():N}";
    var (id, _) = await _surface.RecordAsync(new Dictionary<string, string>
    {
      ["lastName"] = marker,
      ["firstName"] = "Jeanne",
      ["email"] = "",
      ["identityVerified"] = "false",
    });

    await _surface.SetStatusAsync(id, nameof(RequestStatus.Cancelled));

    ExecutionButtonIn(await _surface.BoardRowWithAsync(marker)).Tooltip
      .ShouldBe(ExecutionBlock.Closed.FrenchLabelFor(DataSubjectRight.Access));
  }

  /// <summary>
  /// <b>La ligne rendue par la création et par la modification dit l'exécution comme le tableau</b> :
  /// une correction qui atteste l'identité rallume le bouton, sans recharger la page.
  /// </summary>
  [Fact]
  public async Task RendersTheExecutionOnTheRowsOfTheCreationAndOfTheModification()
  {
    await ConfigureAsync(DataSubjectRight.Access);
    var request = RequestSurface.AValidRequest();
    request["email"] = $"{Guid.NewGuid():N}@example.org";

    var created = await _surface.CreateAsync(request);
    created.StatusCode.ShouldBe(HttpStatusCode.Created);
    ExecutionButtonIn(await created.Content.ReadAsStringAsync()).Tooltip
      .ShouldBe(ExecutionBlock.IdentityNotVerified.FrenchLabelFor(DataSubjectRight.Access));

    var id = (await _surface.RowOfAsync(request["message"]))["id"].ShouldBeOfType<Guid>();
    request["identityVerified"] = "true";

    var modified = await _surface.ModifyAsync(id, request);
    modified.StatusCode.ShouldBe(HttpStatusCode.OK);

    var (attributes, tooltip) = ExecutionButtonIn(await modified.Content.ReadAsStringAsync());
    attributes.ShouldNotContain("aria-disabled", Case.Sensitive, "La correction n'a pas rallumé l'exécution.");
    tooltip.ShouldBe(RequestRow.ExecutionOffered);
  }

  /// <summary>
  /// Le bouton d'exécution de la ligne, une fois : les attributs de sa balise, et le texte de son
  /// infobulle.
  /// </summary>
  private static (string Attributes, string Tooltip) ExecutionButtonIn(string row)
  {
    var button = Regex.Matches(row, @"<button\b(?<attributes>[^>]*)>(?<content>.*?)</button>", RegexOptions.Singleline)
      .Where(match => match.Groups["attributes"].Value.Contains(@"data-action=""execute""", StringComparison.Ordinal))
      .ShouldHaveSingleItem("La ligne ne porte pas son bouton d'exécution, une fois.");

    var tooltip = Regex.Match(button.Groups["content"].Value, @"<span class=""tooltip""[^>]*>(.*?)</span>", RegexOptions.Singleline);
    tooltip.Success.ShouldBeTrue("Le bouton d'exécution ne porte pas d'infobulle.");

    return (button.Groups["attributes"].Value, WebUtility.HtmlDecode(tooltip.Groups[1].Value).Trim());
  }

  /// <summary>Pose une adresse au droit, par le use case du Paramétrage.</summary>
  private async Task ConfigureAsync(DataSubjectRight right)
  {
    using var scope = factory.Services.CreateScope();

    var set = await scope.ServiceProvider.GetRequiredService<Mediator.IMediator>().Send(
      new SetRightEndpointCommand(right, EndpointUrl.From("https://brocanto.example.fr/rgpd")));

    set.IsSuccess.ShouldBeTrue();
  }

  /// <summary>Route le droit sur RabbitMQ, par le use case du Paramétrage.</summary>
  private async Task RouteAsync(DataSubjectRight right)
  {
    using var scope = factory.Services.CreateScope();

    var set = await scope.ServiceProvider.GetRequiredService<Mediator.IMediator>().Send(
      new SetRightRabbitMqRoutingCommand(
        right,
        new RabbitMqRouting(ExchangeName.From("rgpd.exercice"), RoutingKey.From("droit.effacement"))));

    set.IsSuccess.ShouldBeTrue();
  }

  /// <summary>Ramène le service à son état d'installation : aucune ligne de Paramétrage.</summary>
  private async Task ForgetEveryEndpointAsync()
  {
    using var scope = factory.Services.CreateScope();

    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Settings>().ExecuteDeleteAsync();
  }
}
