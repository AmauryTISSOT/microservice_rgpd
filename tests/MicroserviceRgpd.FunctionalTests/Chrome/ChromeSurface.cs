using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.FunctionalTests.Chrome;

/// <summary>
/// Le <b>chrome partagé</b> de la surface de l'<c>Operator</c> — la feuille de style et la police
/// que le service sert lui-même —, exercé par sa <b>seule frontière HTTP</b>, exactement ce que fait
/// un navigateur.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le chrome n'appartient ni au <c>Casework</c> ni au <c>Screening</c> : il est du layout.</b>
/// Il reçoit donc son harnais à lui plutôt que d'entrer dans les harnais de contexte existants —
/// écrire deux fois la même assertion, une par contexte, l'aurait dupliquée sans rien prouver de
/// plus, et aurait fait se croiser deux contextes que la carte tient pour disjoints. Ce harnais pose
/// l'état dont il a besoin par les mêmes formulaires qu'un humain remplirait.
/// </para>
/// <para>
/// ⚠️ <b>Aucune valeur de design n'est lue ici.</b> Pas une couleur, pas un rayon, pas une taille,
/// pas une graisse : une telle assertion lirait le contenu de la feuille, décrirait l'implémentation
/// et casserait à chaque retouche sans jamais rien attraper. Ce que ce harnais garde est que le
/// chrome <b>arrive</b>, et qu'il arrive <b>du service</b>. Le rendu, lui, se vérifie à l'œil.
/// </para>
/// <para>
/// <b>La collection est partagée</b> : d'autres tests déposent dans la même base. Les assertions ne
/// portent donc sur aucun compte et sur aucun contenu de rapport — seulement sur des adresses qui
/// répondent.
/// </para>
/// </remarks>
internal sealed class ChromeSurface(CustomWebApplicationFactory<Program> factory)
{
  /// <summary>La feuille unique, servie par le service et référencée par tous les écrans.</summary>
  internal const string StyleSheet = "/css/operator.css";

  /// <summary>
  /// Le fichier de police, variable et en sous-ensemble latin — embarqué, jamais chargé chez un
  /// tiers : un service qui outille le RGPD ne fait pas fuiter l'adresse IP de ses utilisateurs vers
  /// un hébergeur de polices.
  /// </summary>
  internal const string Font = "/fonts/inter-latin-variable.woff2";

  private const string Queue = "/dossiers";
  private const string CaseDeposit = "/dossiers/depot";
  private const string Manifest = "/manifest";
  private const string ScreeningDeposit = "/depistage/depot";
  private const string Report = "/depistage";
  private const string ScreeningTable = "/depistage/table";
  private const string History = "/depistage/historique";
  private const string Archive = "/depistage/archive";
  private const string ArchivedTable = "/depistage/archive/table";

  private const string Schema = "public";
  private const string Table = "adherents";

  private static readonly DateTimeOffset GeneratedOn = new(2026, 8, 10, 9, 30, 0, TimeSpan.Zero);

  /// <summary>
  /// Les redirections ne sont pas suivies : ce sont les écrans eux-mêmes qu'on lit, et une écriture
  /// qui rendrait sa page directement ferait d'un rechargement une seconde déclaration.
  /// </summary>
  private readonly HttpClient _client = factory.CreateClient(
    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  /// <summary>
  /// <b>Les onze adresses de la surface de l'<c>Operator</c></b>, l'état de chacune posé par le
  /// chemin que le domaine autorise — un dépôt manuel pour le dossier, une déclaration pour la
  /// reprise, deux dépôts de relevé pour qu'il existe un rapport courant et un rapport archivé.
  /// </summary>
  internal async Task<IReadOnlyList<string>> ScreensAsync()
  {
    var opened = await OpenACaseAsync();
    var declared = await DeclareASystemAsync();
    var archived = await ScreenTwiceAsync();

    return
    [
      Queue,
      CaseDeposit,
      $"{Queue}/{opened}",
      Manifest,
      $"{Manifest}/{declared}",
      ScreeningDeposit,
      Report,
      TableOf(ScreeningTable),
      History,
      $"{Archive}?screening={Uri.EscapeDataString(archived)}",
      TableOf(ArchivedTable, $"screening={Uri.EscapeDataString(archived)}&"),
    ];
  }

  /// <summary>Le corps d'une adresse qui doit répondre, et le refus d'une adresse qui ne répond pas.</summary>
  internal async Task<string> ReadAsync(string address)
  {
    var response = await _client.GetAsync(address);

    response.StatusCode.ShouldBe(HttpStatusCode.OK, $"L'adresse {address} doit répondre.");

    return await response.Content.ReadAsStringAsync();
  }

  /// <summary>La réponse brute — de quoi lire un en-tête, ou peser un fichier binaire.</summary>
  internal async Task<HttpResponseMessage> FetchAsync(string address)
  {
    return await _client.GetAsync(address);
  }

  /// <summary>
  /// Les adresses qu'une page rendue fait <b>chercher chez un tiers</b>. Ce qui est retenu est ce
  /// qui déclenche une requête — feuilles, scripts, images —, jamais une URL qui n'est que du texte
  /// affiché : l'adresse d'un <c>Adapter</c> se lit à l'écran sans que le navigateur n'aille nulle
  /// part.
  /// </summary>
  internal static IReadOnlyList<string> ThirdPartyResourcesIn(string rendered)
  {
    IReadOnlyList<string> fetched =
    [
      .. Regex.Matches(rendered, @"<link\b[^>]*\bhref=""([^""]+)""").Select(m => m.Groups[1].Value),
      .. Regex.Matches(rendered, @"<script\b[^>]*\bsrc=""([^""]+)""").Select(m => m.Groups[1].Value),
      .. Regex.Matches(rendered, @"<img\b[^>]*\bsrc=""([^""]+)""").Select(m => m.Groups[1].Value),
    ];

    return [.. fetched.Where(IsThirdParty)];
  }

  private static bool IsThirdParty(string address)
  {
    return address.StartsWith("//", StringComparison.Ordinal)
      || address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
      || address.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
  }

  private static string TableOf(string screen, string leading = "")
  {
    return $"{screen}?{leading}schema={Uri.EscapeDataString(Schema)}&table={Uri.EscapeDataString(Table)}";
  }

  /// <summary>Ouvre un dossier par le formulaire du dépôt manuel, et rend son identifiant.</summary>
  private async Task<string> OpenACaseAsync()
  {
    var fields = new List<KeyValuePair<string, string>>
    {
      new("__RequestVerificationToken", await TokenOfAsync(CaseDeposit)),
      new("Form.IdentityDeclaration", nameof(IdentityDeclaration.OperatorAttested)),
      new("Form.Origin", nameof(ClaimOrigin.Named)),
      new("Form.ReceivedOn", string.Empty),
      new("Form.VerificationMethod", nameof(IdentityVerificationMethod.PersonalRecognition)),
      new("Form.MotivationDetail", string.Empty),
      new("Form.SignedBy", "Claire Martin"),
      new("Form.DesignationKinds", DesignationKind.Email.Token),
      new("Form.DesignationValues", "chrome@example.fr"),
      new("Form.Rights", nameof(DataSubjectRight.Access)),
    };

    var deposited = await _client.PostAsync(CaseDeposit, new FormUrlEncodedContent(fields));

    deposited.StatusCode.ShouldBe(
      HttpStatusCode.Found, "Le dépôt manuel doit mener au dossier qu'il vient d'ouvrir.");

    return deposited.Headers.Location!.ToString().Split('/')[^1];
  }

  /// <summary>
  /// Déclare un système au <c>Manifest</c> et rend son identifiant — celui que porte l'écran de
  /// reprise. L'identifiant est unique à l'appel : la base est partagée, et une seconde déclaration
  /// du même système ne serait plus une déclaration.
  /// </summary>
  private async Task<string> DeclareASystemAsync()
  {
    var id = $"chrome-{Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture)}";

    var fields = new List<KeyValuePair<string, string>>
    {
      new("__RequestVerificationToken", await TokenOfAsync(Manifest)),
      new("Form.Id", id),
      new("Form.Label", "Le système du chrome"),
      new("Form.Contents", "Les adhésions et leurs coordonnées."),
      new("Form.AdapterAddress", string.Empty),
    };

    var declared = await _client.PostAsync(Manifest, new FormUrlEncodedContent(fields));

    declared.StatusCode.ShouldBe(
      HttpStatusCode.Found, "Une déclaration acceptée doit rediriger vers le Manifest rechargé.");

    return id;
  }

  /// <summary>
  /// Dépose deux relevés et rend le rapport que le second a archivé : c'est le seul chemin vers un
  /// dépistage archivé, puisque « archivé » n'est écrit nulle part et se recalcule à chaque rendu.
  /// </summary>
  private async Task<string> ScreenTwiceAsync()
  {
    await DepositAListingAsync();

    var archived = await CurrentScreeningAsync();

    await DepositAListingAsync();

    return archived;
  }

  private async Task DepositAListingAsync()
  {
    var fields = new List<KeyValuePair<string, string>>
    {
      new("__RequestVerificationToken", await TokenOfAsync(ScreeningDeposit)),
      new("Paste", Paste()),
    };

    var deposited = await _client.PostAsync(ScreeningDeposit, new FormUrlEncodedContent(fields));

    deposited.StatusCode.ShouldBe(
      HttpStatusCode.Redirect, "Le dépôt d'un relevé sincère doit mener au rapport qu'il produit.");
  }

  /// <summary>
  /// Le rapport que l'écran de la table rendait, lu <b>sur son formulaire</b> comme le fait un
  /// navigateur — le seul moyen de retenir l'identité d'un rapport avant qu'un second dépôt ne
  /// l'archive.
  /// </summary>
  private async Task<string> CurrentScreeningAsync()
  {
    var address = TableOf(ScreeningTable);
    var screening = Regex.Match(await ReadAsync(address), @"name=""Screening"" value=""([^""]+)""");

    screening.Success.ShouldBeTrue($"Le formulaire de {address} ne dit pas quel rapport il rendait.");

    return screening.Groups[1].Value;
  }

  /// <summary>Un collage sincère d'une seule colonne, dans la forme que la requête de relevé émet.</summary>
  private static string Paste()
  {
    var header = $$"""
      {"format":"{{ColumnListing.FormatVersion}}","dialecte":"postgresql","base":"galette_prod","genere_le":"{{GeneratedOn.ToString("O", CultureInfo.InvariantCulture)}}"}
      """;

    var column = $$"""
      {"schema":"{{Schema}}","table":"{{Table}}","colonne":"email","position":1,"type":"varchar(255)","nullable":true,"commentaire_colonne":null,"commentaire_table":null,"table_referencee":null}
      """;

    return string.Join('\n', header, column, """{"fin":true,"colonnes":1}""");
  }

  private async Task<string> TokenOfAsync(string address)
  {
    var token = Regex.Match(
      await ReadAsync(address),
      @"<input name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");

    token.Success.ShouldBeTrue($"Le formulaire de {address} ne porte aucun jeton anti-rejeu.");

    return token.Groups[1].Value;
  }
}
