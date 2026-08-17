using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.FunctionalTests.Layout;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// L'écran du <c>Manifest</c>, exercé par sa <b>seule frontière HTTP</b> : on y déclare un système,
/// on le relit, on le reprend. Rien d'autre ne le pilote — il n'existe aucune API du catalogue.
/// </summary>
/// <remarks>
/// Ce que ces tests gardent n'est pas la mise en page : c'est ce que l'écran <b>refuse d'offrir</b>
/// — aucune case pour un secret d'<c>Adapter</c>, aucune case pour un nom de table, aucun nombre
/// agrégé — et le fait qu'un système de niveau 0 y entre comme un état parfaitement normal.
/// </remarks>
[Collection(WebCollection.Name)]
public class ManifestScreen(CustomWebApplicationFactory<Program> factory)
{
  private const string Manifest = "/manifest";

  /// <summary>
  /// Le nom que l'écran porte, recopié à dessein : un test qui lirait la constante qu'il vérifie ne
  /// vérifierait plus rien.
  /// </summary>
  private const string ScreenName = "Configuration du microservice RGPD";

  /// <summary>
  /// Le nom <b>que la barre</b> donne au même écran, recopié à dessein lui aussi. L'écran en porte
  /// deux depuis ADR-0008 : le wordmark « Microservice RGPD » précède l'entrée de barre, où la forme
  /// pleine répétait le nom du service à quinze centimètres de lui-même.
  /// </summary>
  private const string ShortScreenName = "Configuration";

  /// <summary>
  /// Les redirections ne sont pas suivies : c'est la redirection elle-même qu'on vérifie. Une
  /// écriture qui rend directement sa page ferait d'un rechargement une seconde déclaration.
  /// </summary>
  private readonly HttpClient _client = factory.CreateClient(
    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  /// <summary>
  /// Le critère du ticket : déclarer un système par la frontière HTTP, et le relire. Le système
  /// choisi est celui du <b>niveau 0</b> — ni capacité, ni <c>Adapter</c> —, parce que c'est le
  /// régime majoritaire et non une tolérance.
  /// </summary>
  [Fact]
  public async Task DeclaresASystemWithoutCapabilityOrAdapterAndReadsItBackAsANormalState()
  {
    const string Prose = "L'export commercial transmis chaque mois à notre agence de publicité.";

    var declaring = await DeclareAsync(new Declaration("export-agence", "L'export commercial mensuel", Prose));

    declaring.StatusCode.ShouldBe(HttpStatusCode.Found);

    var screen = await ReadAsync(Manifest);

    screen.ShouldContain("export-agence");
    screen.ShouldContain("export commercial mensuel");
    screen.ShouldContain("aucune — ce système est traité à la main");
    screen.ShouldContain("aucun");
  }

  /// <summary>
  /// La prose « contient » se relit <b>telle quelle</b> : c'est avec ces mots-là que la
  /// <c>DeliveryLetter</c> nommera plus tard ce système à la personne concernée.
  /// </summary>
  [Fact]
  public async Task GivesBackTheProseWordForWordBecauseItIsWhatWillNameTheSystemToThePerson()
  {
    const string Prose = "Les photos des annonces, sur le serveur de fichiers du grenier.";

    await DeclareAsync(new Declaration("photos-annonces", "Les photos des annonces", Prose));

    (await ReadAsync(Manifest)).ShouldContain(Prose);
  }

  /// <summary>Chaque système porte sa date de déclaration, et elle se consulte à l'écran.</summary>
  [Fact]
  public async Task ShowsTheDayAHumanDeclaredEachSystem()
  {
    await DeclareAsync(new Declaration("date-visible", "Un système daté", "Ce qu'il contient."));

    var screen = await SectionAsync("date-visible");

    screen.ShouldContain("Déclaré le");
    Regex.IsMatch(screen, @"\d{2}/\d{2}/\d{4}").ShouldBeTrue();
  }

  /// <summary>
  /// Les quatre capacités sont <b>déclarables</b>, <c>Erase</c> et <c>Rectify</c> comprises, alors
  /// même qu'aucun code ne les exercera dans ce lot : une capacité indéclarable serait un mensonge
  /// du catalogue.
  /// </summary>
  [Fact]
  public async Task OffersTheFourCapabilitiesOfTheCatalogueIncludingTheTwoNoCodeWillExerciseYet()
  {
    var screen = await ReadAsync(Manifest);

    foreach (var capability in new[] { "Locate", "Read", "Erase", "Rectify" })
    {
      screen.ShouldContain($"value=\"{capability}\"");
    }
  }

  [Fact]
  public async Task DeclaresASystemThatCanLocateAndReadButWillNeverErase()
  {
    await DeclareAsync(new Declaration(
      "compta-scellee",
      "La comptabilité scellée",
      "Les écritures comptables, conservées dix ans.",
      Capabilities: ["Locate", "Read"],
      AdapterAddress: "https://brocanto.example/rgpd/compta"));

    var section = await SectionAsync("compta-scellee");

    section.ShouldContain("localiser, lire");
    section.ShouldNotContain("effacer");
    section.ShouldContain("https://brocanto.example/rgpd/compta");
  }

  /// <summary>
  /// Le plancher, dit à l'humain plutôt que levé sur lui : cocher « effacer » sans « localiser »
  /// est une case cochée de travers, pas une panne.
  /// </summary>
  [Fact]
  public async Task NamesTheLocateFloorToWhoeverTickedEraseWithoutIt()
  {
    var refusal = await DeclareAsync(new Declaration(
      "plancher-viole",
      "Un système sans plancher",
      "Ce qu'il contient.",
      Capabilities: ["Erase"]));

    refusal.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await refusal.Content.ReadAsStringAsync()).ShouldContain("Locate est le plancher");

    (await ReadAsync(Manifest)).ShouldNotContain("plancher-viole");
  }

  /// <summary>
  /// Le champ « contient » est obligatoire : un système sans prose serait un système que la
  /// <c>DeliveryLetter</c> ne saurait pas nommer.
  /// </summary>
  [Fact]
  public async Task RefusesASystemDeclaredWithoutTheProseThatWouldNameIt()
  {
    var refusal = await DeclareAsync(new Declaration("sans-prose", "Un système muet", string.Empty));

    refusal.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await refusal.Content.ReadAsStringAsync()).ShouldContain("Le champ « contient » est absent ou vide.");
  }

  /// <summary>
  /// Deux lignes partageant l'identifiant feraient de tout appel sortant vers un <c>Adapter</c>
  /// une loterie : le second est refusé, et l'humain lit pourquoi.
  /// </summary>
  [Fact]
  public async Task RefusesASecondSystemUnderAnIdentifierTheAdapterAlreadyKnows()
  {
    await DeclareAsync(new Declaration("deja-pris", "Le premier", "Ce qu'il contient."));

    var refusal = await DeclareAsync(new Declaration("deja-pris", "Le second", "Autre chose."));

    refusal.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await refusal.Content.ReadAsStringAsync()).ShouldContain("désigne déjà un système déclaré");
  }

  /// <summary>
  /// On reprend une déclaration, et elle est <b>redatée</b> : un humain vient de regarder ce
  /// système et de dire ce qu'il en sait.
  /// </summary>
  [Fact]
  public async Task RevisesADeclarationAndKeepsTheIdentifierTheAdapterKnows()
  {
    await DeclareAsync(new Declaration("boutique", "La base", "Les comptes clients."));

    var revision = await ReviseAsync(new Declaration(
      "boutique",
      "La base de la boutique",
      "Les comptes clients, leurs commandes et les messages du support.",
      Capabilities: ["Locate"]));

    revision.StatusCode.ShouldBe(HttpStatusCode.Found);

    var section = await SectionAsync("boutique");

    section.ShouldContain("La base de la boutique");
    section.ShouldContain("localiser");
    section.ShouldContain("les messages du support");
  }

  /// <summary>Une adresse qui ne désigne aucun système déclaré n'est pas un formulaire vide.</summary>
  [Theory]
  [InlineData("/manifest/jamais-declare")]
  [InlineData("/manifest/PAS UN IDENTIFIANT")]
  public async Task OffersNoRevisionScreenForASystemNobodyDeclared(string address)
  {
    var response = await _client.GetAsync(address);

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  /// <summary>
  /// <b>Aucune case pour le secret de l'<c>Adapter</c>, aucune case pour le schéma du client.</b>
  /// Le formulaire est le seul endroit où quelqu'un aurait l'occasion d'en écrire un ; ne pas lui
  /// offrir la case est ce qui ferme la porte.
  /// </summary>
  [Fact]
  public async Task OffersNoFieldForAnAdapterSecretNorForAnyNameOfTheClientSchema()
  {
    var screen = await ReadAsync(Manifest);

    // Aucun champ masqué du domaine : un secret ne se saisit pas plus discrètement qu'il ne se
    // saisit ouvertement.
    screen.ShouldNotContain("type=\"password\"");

    // Les noms de champs saisissables sont énumérés en toutes lettres : en ajouter un doit être un
    // geste délibéré, parce que c'est là que le service cesserait de pouvoir répondre de ce qu'il
    // détient. Le jeton anti-rejeu de la plateforme n'est pas un champ du domaine.
    var fields = Regex.Matches(screen, @"name=""(Form\.[A-Za-z]+)""")
      .Select(match => match.Groups[1].Value)
      .Distinct()
      .Order(StringComparer.Ordinal);

    fields.ShouldBe(["Form.AdapterAddress", "Form.Capabilities", "Form.Contents", "Form.Id", "Form.Label"]);
  }

  /// <summary>
  /// <b>Aucun nombre agrégé, aucun taux, aucun ratio.</b> Un dénominateur qui décrit le paysage du
  /// client est une déclaration faussable en silence ; lui donner l'autorité d'un chiffre serait
  /// présenter un recensement comme complet. L'écran énumère, il ne compte pas.
  /// </summary>
  [Fact]
  public async Task ShowsNoCountNoRatioAndNoRateAnywhereOnTheScreen()
  {
    await DeclareAsync(new Declaration("compte-interdit", "Un système de plus", "Ce qu'il contient."));

    // La feuille de style est retirée : ses `100%` sont de la mise en page, pas un taux affiché.
    var screen = Regex.Replace(await ReadAsync(Manifest), "(?s)<style>.*?</style>", string.Empty);

    screen.ShouldNotContain("%");
    Regex.IsMatch(screen, @"\d+\s*(systèmes?|sur\s+\d+|/\s*\d+\s*systèmes?)").ShouldBeFalse();
  }

  /// <summary>
  /// L'écran dit lui-même qu'il ne prouve pas l'exhaustivité. C'est la seule phrase du dispositif
  /// qui tienne l'<c>Omission silencieuse</c> devant les yeux de celui qui déclare.
  /// </summary>
  [Fact]
  public async Task NeverPresentsTheManifestAsComplete()
  {
    (await ReadAsync(Manifest)).ShouldContain("ne garantit pas qu'il n'en existe pas d'autres");
  }

  /// <summary>
  /// <b>L'écran porte DEUX noms, et chacun à sa place</b> : la <b>forme pleine</b> — « Configuration
  /// du microservice RGPD » — de l'onglet au titre, et la <b>forme courte</b> — « Configuration » —
  /// dans la barre, où le wordmark « Microservice RGPD » la précède et rendait la forme pleine
  /// redondante.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>CE TEST RENVERSE CELUI QU'IL REMPLACE, ET LA RAISON EST ÉCRITE DANS ADR-0008.</b> La
  /// règle d'avant était « un seul nom, aucune forme courte : un mot dont la lisibilité dépend de
  /// l'écran où on le lit se retrouvera un jour hors de cet écran ». Elle n'est pas abandonnée
  /// parce qu'elle avait tort — elle a été <b>pesée contre</b> la répétition du nom du service à
  /// quinze centimètres de lui-même, et c'est cette répétition qui a été jugée le plus coûteux des
  /// deux. Son coût, lui, est <b>borné et vérifié</b> : le seul texte de domaine qui cite la forme
  /// courte — la clause d'incomplétude — n'est rendu que sur des écrans qui portent la barre, où le
  /// mot cité est écrit sous les yeux du lecteur. Voir ADR-0008.
  /// </remarks>
  [Fact]
  public async Task CarriesTheFullNameInTheTabAndTitleAndTheShortOneInTheBar()
  {
    var screen = await ReadAsync(Manifest);

    screen.ShouldContain($"<title>{ScreenName} —");
    screen.ShouldContain($"<h1>{ScreenName}</h1>");
    screen.ShouldContain($">{ShortScreenName}</a>");

    // Le nom retiré ne survit nulle part sur l'écran…
    screen.ShouldNotContain("paysage déclaré");

    // …et les DEUX formes se comptent : toute occurrence de « Configuration » est soit la forme
    // pleine, soit le lien de LA BARRE. Une forme courte glissée ailleurs — dans une phrase de
    // l'écran, ou dans un lien posé hors de la barre — incrémente la gauche sans la droite, et
    // échoue ici.
    //
    // ⚠️ LE RETOUR DE LA FORME PLEINE DANS LA BARRE N'EST PAS ATTRAPÉ PAR CE COMPTE, qui resterait
    // équilibré : c'est le `ShouldContain` ci-dessus qui le tient, et c'est pourquoi les deux
    // assertions ne font pas double emploi.
    Regex.Matches(screen, "Configuration").Count.ShouldBe(
      Regex.Matches(screen, Regex.Escape(ScreenName)).Count
      + Regex.Matches(
        LayoutSurface.NavigationBarIn(screen), $">{Regex.Escape(ShortScreenName)}</a>").Count,
      "L'écran écrit « Configuration » ailleurs que dans sa forme pleine ou dans son lien de barre.");
  }

  /// <summary>
  /// Le lien de retour de la reprise d'une déclaration porte la <b>forme courte</b> : c'est un
  /// <b>geste de navigation</b> — il renvoie l'<c>Operator</c> vers la barre —, et le nom qu'il
  /// doit y reconnaître est celui qui y est écrit. La forme pleine ne paraît nulle part sur cet
  /// écran, qui porte son propre titre.
  /// </summary>
  [Fact]
  public async Task NamesTheScreenShortInTheProseLinkThatLeadsBackToIt()
  {
    await DeclareAsync(new Declaration("retour-en-toutes-lettres", "Un système de plus", "Ce qu'il contient."));

    var revising = await ReadAsync($"{Manifest}/retour-en-toutes-lettres");

    revising.ShouldContain($"Revenir à « {ShortScreenName} »");
    revising.ShouldNotContain(ScreenName);
    revising.ShouldNotContain("paysage déclaré");
  }

  private async Task<string> SectionAsync(string id)
  {
    var screen = await ReadAsync(Manifest);
    var sections = screen.Split("<div class=\"system\">");

    return sections.Single(section => section.Contains($"<dd>{id}</dd>", StringComparison.Ordinal));
  }

  private async Task<string> ReadAsync(string address)
  {
    var response = await _client.GetAsync(address);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    return await response.Content.ReadAsStringAsync();
  }

  private Task<HttpResponseMessage> DeclareAsync(Declaration declaration) =>
    SubmitAsync(Manifest, declaration, carriesTheIdentifier: true);

  private Task<HttpResponseMessage> ReviseAsync(Declaration declaration) =>
    SubmitAsync($"{Manifest}/{declaration.Id}", declaration, carriesTheIdentifier: false);

  /// <summary>
  /// Remplit le formulaire de l'écran et le renvoie, jeton anti-rejeu compris — c'est-à-dire
  /// exactement ce que fait un navigateur, et le seul chemin qui existe vers ce catalogue.
  /// </summary>
  private async Task<HttpResponseMessage> SubmitAsync(
    string address,
    Declaration declaration,
    bool carriesTheIdentifier)
  {
    var fields = new List<KeyValuePair<string, string>>
    {
      new("__RequestVerificationToken", await AntiforgeryTokenOfAsync(address)),
      new("Form.Label", declaration.Label),
      new("Form.Contents", declaration.Contents),
      new("Form.AdapterAddress", declaration.AdapterAddress ?? string.Empty),
    };

    if (carriesTheIdentifier)
    {
      fields.Add(new KeyValuePair<string, string>("Form.Id", declaration.Id));
    }

    fields.AddRange(declaration.Capabilities.Select(
      capability => new KeyValuePair<string, string>("Form.Capabilities", capability)));

    return await _client.PostAsync(address, new FormUrlEncodedContent(fields));
  }

  private async Task<string> AntiforgeryTokenOfAsync(string address)
  {
    var token = Regex.Match(
      await ReadAsync(address),
      @"<input name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");

    token.Success.ShouldBeTrue($"Le formulaire de {address} ne porte aucun jeton anti-rejeu.");

    return token.Groups[1].Value;
  }

  /// <summary>Ce qu'un humain saisit à l'écran pour déclarer ou reprendre un système.</summary>
  private sealed record Declaration(
    string Id,
    string Label,
    string Contents,
    string[]? Capabilities = null,
    string? AdapterAddress = null)
  {
    public string[] Capabilities { get; } = Capabilities ?? [];
  }
}
