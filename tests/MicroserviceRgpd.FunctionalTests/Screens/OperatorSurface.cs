using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas. Le mot du glossaire
// l'emporte, et l'alias dit lequel des deux on lit.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// De quoi poser des dossiers en base et lire les deux écrans par leur <b>seule frontière HTTP</b> —
/// exactement ce que fait un navigateur, et le seul chemin qui existe vers l'instruction.
/// </summary>
/// <remarks>
/// <para>
/// <b>Les dossiers sont posés par le dépôt, et non par la route d'entrée.</b> Celle-ci date la
/// réception de l'instant de l'appel : deux dossiers postés à la suite auraient la même échéance, et
/// aucun ordre de file ne serait démontrable. Ce que ces tests exercent est la <b>surface</b>, et la
/// route d'entrée a ses propres tests.
/// </para>
/// <para>
/// La collection est partagée : d'autres tests ouvrent des dossiers dans la même base. Les
/// assertions portent donc sur <b>l'ordre relatif</b> de dossiers nommés, jamais sur un total —
/// ce qui est aussi la seule chose que la règle des chiffres autoriserait à lire.
/// </para>
/// </remarks>
internal sealed class OperatorSurface(CustomWebApplicationFactory<Program> factory)
{
  internal const string Queue = "/dossiers";

  /// <summary>L'écran du dépôt manuel — le seul canal par lequel un humain fait entrer une demande.</summary>
  internal const string Deposit = "/dossiers/depot";

  /// <summary>
  /// Les redirections ne sont pas suivies : c'est la redirection elle-même qu'on vérifie. Une écriture
  /// qui rendrait directement sa page ferait d'un rechargement une seconde déclaration.
  /// </summary>
  private readonly HttpClient _client = factory.CreateClient(
    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  internal HttpClient Client => _client;

  internal static string AddressOf(CaseId opened) => $"{Queue}/{opened.Value}";

  /// <summary>Pose un dossier en base, avec la date de réception qu'on veut lui donner.</summary>
  internal async Task<CaseId> OpenAsync(
    ReceptionDate reception,
    DataSubjectRight[]? rights = null,
    params DeclaredSystem[] systems)
  {
    return await OpenAsync(reception, ClaimOrigin.Named, motivation: null, rights, systems);
  }

  /// <summary>
  /// Pose un dossier en base, avec l'origine et la motivation qu'on veut lui donner — de quoi
  /// éprouver ce que l'écran <b>réclame</b> sans passer par le dépôt, qui a ses propres tests.
  /// </summary>
  internal async Task<CaseId> OpenAsync(
    ReceptionDate reception,
    ClaimOrigin origin,
    IdentityMotivation? motivation,
    DataSubjectRight[]? rights = null,
    params DeclaredSystem[] systems)
  {
    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    foreach (var system in systems)
    {
      // Le catalogue est relu à chaque affichage : les systèmes doivent y être pour que l'écran
      // sache les nommer et dire de quand date leur déclaration.
      if (!await dbContext.Set<DeclaredSystem>().AnyAsync(one => one.Id == system.Id))
      {
        dbContext.Add(system);
      }
    }

    var opened = Case.Open(
      CaseId.Next(),
      IdentityDeclaration.Unverified,
      motivation,
      [Designation.Of(DesignationKind.Email, "jean.dupont@example.fr")],
      rights ?? [DataSubjectRight.Access],
      origin,
      Manifest.Of(systems),
      reception);

    dbContext.Add(opened);

    await dbContext.SaveChangesAsync();

    return opened.Id;
  }

  /// <summary>Un système déclaré au jour qu'on choisit — c'est cette date qui descend sur le <c>Step</c>.</summary>
  internal static DeclaredSystem ASystem(string id, string label, DateTimeOffset declaredOn)
  {
    return DeclaredSystem.Declare(
      DeclaredSystemId.From(id),
      SystemLabel.From(label),
      SystemContents.From("Ce qu'il contient, dans les mots de qui l'a déclaré."),
      [],
      adapterAddress: null,
      declaredOn);
  }

  internal async Task<string> ReadAsync(string address)
  {
    var response = await _client.GetAsync(address);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    return await response.Content.ReadAsStringAsync();
  }

  /// <summary>
  /// La page sans sa feuille de style : les <c>100%</c> et les tailles en <c>rem</c> sont de la mise
  /// en page, jamais des nombres affichés à l'<c>Operator</c>.
  /// </summary>
  internal async Task<string> ReadWithoutStyleAsync(string address)
  {
    return Regex.Replace(await ReadAsync(address), "(?s)<style>.*?</style>", string.Empty);
  }

  /// <summary>
  /// <b>Le texte tel que l'<c>Operator</c> le lit</b> : entités décodées, blancs réduits à un espace.
  /// </summary>
  /// <remarks>
  /// Une phrase française porte des apostrophes, que l'encodeur écrit <c>&amp;#x27;</c>, et le HTML
  /// coupe ses lignes où bon lui semble. Affirmer sur la source brute reviendrait à faire dépendre le
  /// test de la mise en page : ce qu'on garde ici est <b>ce qui est dit</b>, et non comment c'est
  /// balisé.
  /// </remarks>
  internal async Task<string> ReadTextAsync(string address)
  {
    var page = Regex.Replace(await ReadAsync(address), "(?s)<style>.*?</style>", string.Empty);

    return Regex.Replace(System.Net.WebUtility.HtmlDecode(Regex.Replace(page, "<[^>]+>", " ")), @"\s+", " ");
  }

  /// <summary>
  /// Remplit le formulaire de déclaration et le renvoie, jeton anti-rejeu compris — c'est-à-dire
  /// exactement ce que fait un navigateur.
  /// </summary>
  internal async Task<HttpResponseMessage> DeclareAsync(CaseId opened, Declaration declaration)
  {
    var address = AddressOf(opened);

    var fields = new List<KeyValuePair<string, string>>
    {
      new("__RequestVerificationToken", await AntiforgeryTokenOfAsync(address)),
      new("Form.Right", declaration.Right),
      new("Form.DeclaredSystem", declaration.DeclaredSystem),
      new("Form.State", declaration.State),
      new("Form.Finding", declaration.Finding),
      new("Form.SignedBy", declaration.SignedBy),
    };

    return await _client.PostAsync(address, new FormUrlEncodedContent(fields));
  }

  /// <summary>
  /// Remplit le formulaire du dépôt manuel et l'envoie, jeton anti-rejeu compris — c'est-à-dire
  /// exactement ce que fait un navigateur.
  /// </summary>
  internal async Task<HttpResponseMessage> DepositAsync(ADeposit deposited)
  {
    ArgumentNullException.ThrowIfNull(deposited);

    var fields = new List<KeyValuePair<string, string>>
    {
      new("__RequestVerificationToken", await AntiforgeryTokenOfAsync(Deposit)),
      new("Form.IdentityDeclaration", deposited.IdentityDeclaration),
      new("Form.Origin", deposited.Origin),
      new("Form.ReceivedOn", deposited.ReceivedOn),
      new("Form.VerificationMethod", deposited.VerificationMethod),
      new("Form.MotivationDetail", deposited.MotivationDetail),
      new("Form.SignedBy", deposited.SignedBy),
    };

    foreach (var designation in deposited.Designations)
    {
      // Les deux listes sont posées en parallèle, ligne par ligne : c'est la forme que le navigateur
      // envoie, et c'est sur elle que la frontière de saisie s'appuie pour reformer les paires.
      fields.Add(new KeyValuePair<string, string>("Form.DesignationKinds", designation.Kind));
      fields.Add(new KeyValuePair<string, string>("Form.DesignationValues", designation.Value));
    }

    foreach (var right in deposited.Rights)
    {
      fields.Add(new KeyValuePair<string, string>("Form.Rights", right));
    }

    return await _client.PostAsync(Deposit, new FormUrlEncodedContent(fields));
  }

  /// <summary>
  /// Remplit le formulaire de confirmation d'un droit proposé en amont et l'envoie.
  /// </summary>
  internal async Task<HttpResponseMessage> ConfirmAsync(CaseId opened, string right, string signedBy)
  {
    var address = AddressOf(opened);

    var fields = new List<KeyValuePair<string, string>>
    {
      new("__RequestVerificationToken", await AntiforgeryTokenOfAsync(address)),
      new("Confirmation.Right", right),
      new("Confirmation.SignedBy", signedBy),
    };

    return await _client.PostAsync($"{address}?handler=Confirm", new FormUrlEncodedContent(fields));
  }

  /// <summary>
  /// Remplit le formulaire par lequel un <c>Operator</c> pèse l'identité <b>après coup</b>, et
  /// l'envoie.
  /// </summary>
  internal async Task<HttpResponseMessage> MotivateAsync(
    CaseId opened,
    string method,
    string detail,
    string signedBy)
  {
    var address = AddressOf(opened);

    var fields = new List<KeyValuePair<string, string>>
    {
      new("__RequestVerificationToken", await AntiforgeryTokenOfAsync(address)),
      new("Motivation.VerificationMethod", method),
      new("Motivation.Detail", detail),
      new("Motivation.SignedBy", signedBy),
    };

    return await _client.PostAsync($"{address}?handler=Motivate", new FormUrlEncodedContent(fields));
  }

  private async Task<string> AntiforgeryTokenOfAsync(string address)
  {
    var token = Regex.Match(
      await ReadAsync(address),
      @"<input name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");

    token.Success.ShouldBeTrue($"Le formulaire de {address} ne porte aucun jeton anti-rejeu.");

    return token.Groups[1].Value;
  }
}

/// <summary>
/// Ce qu'un <c>Operator</c> transcrit à l'écran du dépôt manuel. Tout est facultatif hors le nom —
/// le service ne barre jamais la route.
/// </summary>
internal sealed record ADeposit
{
  internal string IdentityDeclaration { get; init; } = nameof(Core.Casework.IdentityDeclaration.Unverified);

  internal string Origin { get; init; } = nameof(ClaimOrigin.Named);

  /// <summary>Vide vaut « je l'ignore » : le service retombe sur son défaut et le nomme comme tel.</summary>
  internal string ReceivedOn { get; init; } = string.Empty;

  /// <summary>Vide vaut « personne ne l'a pesé », qui n'est jamais <c>None</c>.</summary>
  internal string VerificationMethod { get; init; } = string.Empty;

  internal string MotivationDetail { get; init; } = string.Empty;

  internal string SignedBy { get; init; } = "Claire Martin";

  internal IReadOnlyList<ADesignation> Designations { get; init; } = [];

  internal IReadOnlyList<string> Rights { get; init; } = [];
}

/// <summary>Une ligne du sac, telle que le formulaire l'envoie : une nature et une valeur.</summary>
internal sealed record ADesignation(string Kind, string Value);

/// <summary>Ce qu'un <c>Operator</c> saisit à l'écran pour déclarer où en est un travail dû.</summary>
internal sealed record Declaration(
  string Right,
  string DeclaredSystem,
  string State,
  string Finding,
  string SignedBy);
