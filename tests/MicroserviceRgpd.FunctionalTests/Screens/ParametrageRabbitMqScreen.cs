using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Configuration;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.UseCases.Configuration.SetRightEndpoint;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// La <b>seconde face du Paramétrage</b> — « Configuration RabbitMQ » —, exercée par sa seule
/// frontière HTTP. L'intégrateur y lit les routages en vigueur, et les y <b>déclare</b> droit par
/// droit : un exchange, une routing key, et le bouton qui ramène le droit à « non configuré ». Les
/// deux faces se rejoignent par des <b>onglets rendus par le serveur</b>, sans une ligne de
/// JavaScript.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Les deux pages portent le même <c>h1</c></b>, et c'est ce qui les fait lire comme un seul
/// écran à deux faces plutôt que comme deux écrans voisins. Le panneau latéral ne bouge pas : on
/// n'a pas quitté le Paramétrage, et « Paramétrage » y reste marqué courant — ce que le harnais de
/// layout garde pour toute la surface, cette page comprise.
/// </para>
/// <para>
/// ⚠️ <b>Il configure, il n'appelle pas.</b> Enregistrer un routage est une écriture locale : rien
/// n'est publié, aucun exchange n'est déclaré ni vérifié, et un routage reste enregistrable alors
/// même qu'aucun broker n'existe — ce que ces tests exercent sur un service qui n'en a aucun.
/// </para>
/// <para>
/// ⚠️ <b>Chaque test part d'un service vierge</b>, pour la même raison que sur la page HTTP : le
/// Paramétrage est un singleton, et la base est partagée par toute la collection.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ParametrageRabbitMqScreen(CustomWebApplicationFactory<Program> factory) : IAsyncLifetime
{
  private const string Http = "/parametrage";
  private const string RabbitMq = "/parametrage/rabbitmq";

  /// <summary>L'adresse du formulaire d'un droit : la page, et le gestionnaire d'enregistrement.</summary>
  private const string Save = "/parametrage/rabbitmq?handler=Set";

  /// <summary>L'adresse du bouton Effacer d'un droit : la page, et le gestionnaire d'effacement.</summary>
  private const string Clear = "/parametrage/rabbitmq?handler=Clear";

  /// <summary>Le nom que les <b>deux</b> faces portent en tête, recopié à dessein.</summary>
  private const string ScreenName = "Paramétrage du microservice RGPD";

  private const string HttpTab = "Configuration HTTP";
  private const string RabbitMqTab = "Configuration RabbitMQ";

  private const string Erasure = "droit à l'effacement";

  /// <summary>
  /// <b>L'avertissement de remplacement</b>, recopié à dessein : il <b>nomme le réglage qui sera
  /// remplacé</b> — l'adresse —, et sans ce mot l'intégrateur ne saurait pas ce qu'il perd.
  /// </summary>
  private const string ReplacementWarning =
    "Ce droit porte déjà une adresse d'exercice, sur la face « Configuration HTTP ». " +
    "Enregistrer un routage ici remplacera cette adresse : un droit ne porte qu'un seul canal.";

  /// <summary>Le bouton d'enregistrement d'une mini-form, ce que l'avertissement doit précéder.</summary>
  private const string SaveButton = ">Enregistrer</button>";

  /// <summary>
  /// <b>Les six droits, leur libellé français et leur article</b>, recopiés à dessein : un test qui
  /// lirait le SmartEnum qu'il vérifie ne vérifierait plus rien. C'est l'ordre du règlement — 15,
  /// 16, 17, 18, 20, 21 — et l'énumération est <b>non contiguë</b>. Chaque droit porte aussi son
  /// <b>nom canonique</b>, celui que sa mini-form envoie.
  /// </summary>
  private static readonly (string Name, string Label, int Article)[] TheSixRights =
  [
    ("Access", "droit d'accès", 15),
    ("Rectification", "droit de rectification", 16),
    ("Erasure", "droit à l'effacement", 17),
    ("Restriction", "droit à la limitation du traitement", 18),
    ("Portability", "droit à la portabilité", 20),
    ("Objection", "droit d'opposition", 21),
  ];

  private readonly HttpClient _client = factory.CreateClient(
    new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

  public Task InitializeAsync() => ForgetEveryChannelAsync();

  public Task DisposeAsync() => ForgetEveryChannelAsync();

  /// <summary>La page répond, et elle porte <b>le même titre que la page HTTP</b>.</summary>
  [Fact]
  public async Task CarriesTheSameHeadingAsTheHttpPage()
  {
    var rabbitMq = WebUtility.HtmlDecode(await ReadAsync(RabbitMq));

    rabbitMq.ShouldContain($"<h1>{ScreenName}</h1>");
    WebUtility.HtmlDecode(await ReadAsync(Http)).ShouldContain($"<h1>{ScreenName}</h1>");
  }

  /// <summary>
  /// <b>Les deux faces portent les mêmes deux onglets</b>, dans le même ordre, et chacune marque le
  /// sien — et lui seul — <c>aria-current="page"</c>.
  /// </summary>
  [Theory]
  [InlineData(Http, HttpTab)]
  [InlineData(RabbitMq, RabbitMqTab)]
  public async Task MarksItsOwnTabAndOnlyItOnBothFaces(string screen, string expected)
  {
    var tabs = TabsIn(WebUtility.HtmlDecode(await ReadAsync(screen)));

    tabs.Select(tab => (tab.Address, tab.Label))
      .ShouldBe([(Http, HttpTab), (RabbitMq, RabbitMqTab)]);

    tabs.Where(tab => tab.IsCurrent).Select(tab => tab.Label).ShouldBe([expected]);
  }

  /// <summary>
  /// <b>Les onglets sont des liens rendus par le serveur</b> : la navigation d'une face à l'autre
  /// tient sans une ligne de JavaScript, et l'adresse de chaque onglet répond d'elle-même.
  /// </summary>
  [Fact]
  public async Task NavigatesFromOneFaceToTheOtherWithoutAnyScript()
  {
    foreach (var screen in new[] { Http, RabbitMq })
    {
      var rendered = WebUtility.HtmlDecode(await ReadAsync(screen));

      // ⚠️ C'EST LA RÉGION D'ONGLETS QU'ON LIT, PAS LA PAGE ENTIÈRE. Ce que ce test garde est que LES
      // ONGLETS sont des liens ; un module qu'un écran poserait un jour pour tout autre chose n'a pas
      // à faire échouer cette phrase-là.
      TabsRegionIn(rendered).ShouldNotContain("<script", Case.Insensitive);

      foreach (var tab in TabsIn(rendered))
      {
        tab.Address.ShouldNotBeEmpty("Un onglet qui ne mène nulle part n'est pas un lien.");
        (await _client.GetAsync(tab.Address)).StatusCode.ShouldBe(HttpStatusCode.OK);
      }
    }
  }

  /// <summary>
  /// La page liste <b>les six droits dans l'ordre des articles</b>, chacun avec son libellé français
  /// et son article — lus sur le noyau partagé, jamais recopiés par le contexte.
  /// </summary>
  [Fact]
  public async Task ListsTheSixRightsInArticleOrderEachWithItsLabelAndArticle()
  {
    var screen = WebUtility.HtmlDecode(await ReadAsync(RabbitMq));

    var sections = screen.Split("<div class=\"right\">").Skip(1).ToList();

    sections.Count.ShouldBe(TheSixRights.Length);

    foreach (var (section, right) in sections.Zip(TheSixRights))
    {
      section.ShouldContain($"<h2>{right.Label}</h2>");
      section.ShouldContain($"Article {right.Article}");
    }
  }

  /// <summary>
  /// <b>Sur un service vierge, les six droits s'affichent « non configuré »</b> : rien n'est semé au
  /// démarrage, et la page le dit en toutes lettres plutôt que de laisser un blanc.
  /// </summary>
  [Fact]
  public async Task ShowsEveryRightAsUnconfiguredOnAVirginService()
  {
    foreach (var section in SectionsIn(WebUtility.HtmlDecode(await ReadAsync(RabbitMq))).Values)
    {
      section.ShouldContain("non configuré");
    }
  }

  /// <summary>
  /// Le critère du ticket : <b>enregistrer un exchange et une routing key pour un droit</b>, et les
  /// relire sur la page. Les cinq autres droits sont inchangés.
  /// </summary>
  [Fact]
  public async Task SavesTheExchangeAndTheRoutingKeyOfARightAndLeavesTheFiveOthersUntouched()
  {
    const string Exchange = "rgpd.exercices";
    const string Key = "droit.effacement";

    (await SaveAsync("Erasure", Exchange, Key)).StatusCode.ShouldBe(HttpStatusCode.Found);

    var sections = await SectionsAsync();

    sections[Erasure].ShouldContain(Exchange);
    sections[Erasure].ShouldContain(Key);
    sections[Erasure].ShouldNotContain("non configuré");

    foreach (var (_, label, _) in TheSixRights.Where(right => right.Label != Erasure))
    {
      sections[label].ShouldContain("non configuré", customMessage: $"Le {label} ne devait pas être touché.");
    }
  }

  /// <summary>
  /// <b>L'enregistrement suit le Post-Redirect-Get</b> : l'écriture répond par une redirection vers
  /// la face RabbitMQ, et recharger la page relit l'état au lieu de renvoyer la saisie.
  /// </summary>
  [Fact]
  public async Task RedirectsBackToTheScreenAfterSaving()
  {
    var response = await SaveAsync("Erasure", "rgpd.exercices", "droit.effacement");

    response.StatusCode.ShouldBe(HttpStatusCode.Found);
    response.Headers.Location!.OriginalString.ShouldBe(RabbitMq);
  }

  /// <summary>
  /// <b>Corriger sans repartir de zéro</b> : les champs d'un droit routé portent déjà son routage, et
  /// l'enregistrer de nouveau le <b>remplace</b> — l'ancien disparaît, l'autre droit routé ne bouge
  /// pas.
  /// </summary>
  [Fact]
  public async Task RevisesAnAlreadyRoutedRightWithoutTouchingTheOtherRights()
  {
    await SaveAsync("Rectification", "rgpd.v1", "droit.rectification");
    await SaveAsync("Erasure", "rgpd.exercices", "droit.effacement");

    (await SectionAsync("droit de rectification")).ShouldContain("value=\"rgpd.v1\"");

    (await SaveAsync("Rectification", "rgpd.v2", "droit.rectification.v2")).StatusCode
      .ShouldBe(HttpStatusCode.Found);

    var rectification = await SectionAsync("droit de rectification");

    rectification.ShouldContain("rgpd.v2");
    rectification.ShouldNotContain("rgpd.v1");
    (await SectionAsync(Erasure)).ShouldContain("rgpd.exercices");
  }

  /// <summary>
  /// <b>Les espaces de début et de fin sont rognés avant enregistrement</b> : un copier-coller
  /// malheureux ne crée pas un exchange fantôme.
  /// </summary>
  [Fact]
  public async Task TrimsTheSurroundingSpacesBeforeSaving()
  {
    (await SaveAsync("Access", "  rgpd.exercices  ", "\tdroit.acces\n")).StatusCode
      .ShouldBe(HttpStatusCode.Found);

    var access = await SectionAsync("droit d'accès");

    access.ShouldContain("value=\"rgpd.exercices\"");
    access.ShouldContain("value=\"droit.acces\"");
  }

  /// <summary>
  /// ⚠️ <b>C'est le serveur qui refuse, pas le navigateur</b> : un exchange vide, une routing key
  /// vide ou une valeur de plus de 255 octets sont rendus à l'écran — un statut 200, aucune
  /// redirection —, et rien n'est enregistré.
  /// </summary>
  [Theory]
  [InlineData("", "droit.acces", "Le nom de l'exchange est absent ou vide.")]
  [InlineData("   ", "droit.acces", "Le nom de l'exchange est absent ou vide.")]
  [InlineData("rgpd.exercices", "", "La routing key est absente ou vide.")]
  [InlineData("rgpd.exercices", "  ", "La routing key est absente ou vide.")]
  public async Task RefusesAnEmptyFieldOnTheServerAndSaysSoWithoutRedirecting(
    string exchange, string key, string refusal)
  {
    var response = await SaveAsync("Access", exchange, key);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);
    response.Headers.Location.ShouldBeNull();
    WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()).ShouldContain(refusal);

    (await SectionAsync("droit d'accès")).ShouldContain("non configuré");
  }

  /// <summary>
  /// ⚠️ <b>Le plafond se compte en octets UTF-8, pas en caractères</b> : 256 caractères latins sont
  /// refusés, et 128 idéogrammes — 384 octets pour 128 caractères — le sont aussi, là où un compte
  /// de caractères les aurait laissés passer.
  /// </summary>
  [Theory]
  [InlineData("Exchange", 256, 'a')]
  [InlineData("Exchange", 128, '銀')]
  [InlineData("RoutingKey", 256, 'a')]
  [InlineData("RoutingKey", 128, '銀')]
  public async Task RefusesAValueLongerThanTwoHundredAndFiftyFiveBytes(string field, int length, char letter)
  {
    var tooLong = new string(letter, length);

    Encoding.UTF8.GetByteCount(tooLong).ShouldBeGreaterThan(255);

    var response = field == "Exchange"
      ? await SaveAsync("Access", tooLong, "droit.acces")
      : await SaveAsync("Access", "rgpd.exercices", tooLong);

    response.StatusCode.ShouldBe(HttpStatusCode.OK);

    // Le refus se range SOUS LE CHAMP FAUTIF, ici comme pour un champ vide : c'est ce qui dit lequel
    // des deux corriger, et le lire sur la page entière ne l'aurait pas dit.
    var access = SectionIn(WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()), "droit d'accès");
    var sound = field == "Exchange" ? "RoutingKey" : "Exchange";

    FieldIn(access, field).ShouldContain("dépasse 255 octets UTF-8");
    FieldIn(access, sound).ShouldNotContain("dépasse 255 octets UTF-8");

    (await SectionAsync("droit d'accès")).ShouldContain("non configuré");
  }

  /// <summary>
  /// Le refus se dit <b>dans la section du bon droit et sous le bon champ</b> — exchange ou routing
  /// key —, et la saisie refusée <b>reste dans les champs</b> : on corrige, on ne retape pas.
  /// </summary>
  [Theory]
  [InlineData("Exchange", "", "droit.opposition", "Le nom de l'exchange est absent ou vide.")]
  [InlineData("RoutingKey", "rgpd.exercices", "", "La routing key est absente ou vide.")]
  public async Task SaysTheRefusalUnderItsOwnFieldInTheSectionOfTheRightBeingEdited(
    string faulty, string exchange, string key, string refusal)
  {
    var response = await SaveAsync("Objection", exchange, key);
    var screen = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    var objection = SectionIn(screen, "droit d'opposition");
    var sound = faulty == "Exchange" ? "RoutingKey" : "Exchange";

    FieldIn(objection, faulty).ShouldContain(refusal);
    FieldIn(objection, sound).ShouldNotContain(refusal);

    // Les cinq autres droits n'ont rien à dire d'un refus qui n'est pas le leur.
    SectionIn(screen, "droit d'accès").ShouldNotContain(refusal);

    // La saisie refusée reste affichée — celle des deux champs, pas seulement celle du fautif.
    FieldIn(objection, "Exchange").ShouldContain($"value=\"{exchange}\"");
    FieldIn(objection, "RoutingKey").ShouldContain($"value=\"{key}\"");
  }

  /// <summary>
  /// <b>Les deux champs sont jugés tous les deux</b> : une saisie où l'exchange et la routing key
  /// sont l'un et l'autre fautifs se lit d'un coup, chaque refus sous son champ — et non en deux
  /// allers-retours.
  /// </summary>
  [Fact]
  public async Task JudgesBothFieldsAndSaysBothRefusalsAtOnce()
  {
    var response = await SaveAsync("Access", "", "");
    var access = SectionIn(WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()), "droit d'accès");

    FieldIn(access, "Exchange").ShouldContain("Le nom de l'exchange est absent ou vide.");
    FieldIn(access, "RoutingKey").ShouldContain("La routing key est absente ou vide.");
  }

  /// <summary>
  /// ⚠️ <b>Les champs ne portent pas d'attribut <c>maxlength</c></b> : l'attribut compte des unités
  /// UTF-16 là où la contrainte compte des octets, et un navigateur qui tronquerait à 255 unités
  /// laisserait passer une valeur que le broker refuserait. <b>Le serveur fait foi.</b> Chaque champ
  /// porte en revanche un <b>texte d'aide</b>, qui dit ce que le service attend.
  /// </summary>
  [Fact]
  public async Task CarriesNoMaxlengthOnItsFieldsAndAccompaniesEachWithAHint()
  {
    var access = await SectionAsync("droit d'accès");

    foreach (var field in new[] { "Exchange", "RoutingKey" })
    {
      var block = FieldIn(access, field);

      block.ShouldNotContain("maxlength", Case.Insensitive);
      block.ShouldContain("class=\"hint\"");

      // ⚠️ L'AIDE NE RENVOIE PAS À LA DOCUMENTATION DE RABBITMQ : elle dit ce que CE service attend.
      block.ShouldNotContain("rabbitmq.com");
    }
  }

  /// <summary>
  /// <b>Les deux champs d'un droit lui sont rattachés par un <c>fieldset</c> et sa <c>legend</c></b>,
  /// et non par une périphrase recopiée dans chaque libellé : les libellés se lisent « Exchange » et
  /// « Routing key », et c'est la légende qui nomme le droit.
  /// </summary>
  [Fact]
  public async Task TiesBothFieldsToTheirRightByAFieldsetAndItsLegend()
  {
    var screen = WebUtility.HtmlDecode(await ReadAsync(RabbitMq));

    foreach (var (_, label, _) in TheSixRights)
    {
      var section = SectionIn(screen, label);

      section.ShouldContain("<fieldset>");
      section.ShouldContain($"</legend>");
      LegendIn(section).ShouldContain(label);

      FieldIn(section, "Exchange").ShouldContain(">Exchange</label>");
      FieldIn(section, "RoutingKey").ShouldContain(">Routing key</label>");
    }
  }

  /// <summary>
  /// ⚠️ <b>La page HTTP n'est pas modifiée sur ce point</b> : son champ unique n'a pas de second
  /// champ dont le distinguer, et l'entourer d'un <c>fieldset</c> pour ressembler à sa voisine aurait
  /// été de la symétrie sans objet.
  /// </summary>
  [Fact]
  public async Task LeavesTheHttpFaceWithoutAFieldset()
  {
    (await ReadAsync(Http)).ShouldNotContain("<fieldset", Case.Insensitive);
  }

  /// <summary>
  /// Le critère du ticket : <b>effacer le routage d'un droit le ramène à « non configuré »</b>, et
  /// l'effacement suit le Post-Redirect-Get.
  /// </summary>
  [Fact]
  public async Task ClearsTheRoutingOfARightBackToUnconfigured()
  {
    await SaveAsync("Erasure", "rgpd.exercices", "droit.effacement");

    var response = await ClearAsync("Erasure");

    response.StatusCode.ShouldBe(HttpStatusCode.Found);
    response.Headers.Location!.OriginalString.ShouldBe(RabbitMq);

    var erasure = await SectionAsync(Erasure);

    erasure.ShouldContain("non configuré");
    erasure.ShouldNotContain("rgpd.exercices");
  }

  /// <summary>
  /// <b>Effacer n'est offert qu'à un droit portant un routage</b> : un droit « non configuré » n'a
  /// rien à oublier, et un bouton qui ne ferait rien serait une promesse vide.
  /// </summary>
  [Fact]
  public async Task OffersToClearOnlyARightThatCarriesARouting()
  {
    await SaveAsync("Objection", "rgpd.exercices", "droit.opposition");

    var sections = await SectionsAsync();

    sections["droit d'opposition"].ShouldContain(">Effacer</button>");

    foreach (var (_, label, _) in TheSixRights.Where(right => right.Label != "droit d'opposition"))
    {
      sections[label].ShouldNotContain(">Effacer</button>");
    }
  }

  /// <summary>
  /// ⚠️ <b>Un droit réglé en HTTP ne se voit pas offrir « Effacer » sur cette face</b> : cette page
  /// ne montre pas son canal, et un bouton qui l'effacerait sans le dire serait une destruction
  /// aveugle.
  /// </summary>
  [Fact]
  public async Task DoesNotOfferToClearARightConfiguredOverHttp()
  {
    await AddressAsync(DataSubjectRight.Access, "https://brocanto.example.fr/rgpd/acces");

    (await SectionAsync("droit d'accès")).ShouldNotContain(">Effacer</button>");
  }

  /// <summary>
  /// <b>Un envoi sans jeton anti-rejeu est refusé</b>, et rien n'est enregistré : le formulaire est
  /// le seul chemin vers le Paramétrage, et un site tiers n'a pas à y écrire à la place de
  /// l'intégrateur.
  /// </summary>
  [Fact]
  public async Task RefusesASaveThatCarriesNoAntiforgeryToken()
  {
    var response = await _client.PostAsync(Save, new FormUrlEncodedContent(
    [
      new("Form.Right", "Access"),
      new("Form.Exchange", "tiers.exercices"),
      new("Form.RoutingKey", "droit.acces"),
    ]));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await SectionAsync("droit d'accès")).ShouldContain("non configuré");
  }

  /// <summary>
  /// <b>Un effacement sans jeton anti-rejeu est refusé</b>, et le routage reste : un site tiers n'a
  /// pas à déconfigurer un droit à la place de l'intégrateur.
  /// </summary>
  [Fact]
  public async Task RefusesAClearThatCarriesNoAntiforgeryToken()
  {
    await SaveAsync("Access", "rgpd.exercices", "droit.acces");

    var response = await _client.PostAsync(Clear, new FormUrlEncodedContent([new("Form.Right", "Access")]));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await SectionAsync("droit d'accès")).ShouldContain("rgpd.exercices");
  }

  /// <summary>
  /// ⚠️ <b>Un droit forgé est refusé en le nommant</b> — <c>OutOfScope</c> compris, qui est un verdict
  /// et non un droit qu'on exerce —, à l'enregistrement comme à l'effacement, et rien n'est écrit.
  /// </summary>
  [Theory]
  [InlineData("OutOfScope")]
  [InlineData("Profiling")]
  public async Task RefusesARightTheScreenDoesNotConfigure(string right)
  {
    var saved = await SaveAsync(right, "rgpd.exercices", "droit.forge");

    saved.StatusCode.ShouldBe(HttpStatusCode.OK);
    WebUtility.HtmlDecode(await saved.Content.ReadAsStringAsync())
      .ShouldContain($"« {right} » n'est pas un droit du Paramétrage");

    var cleared = await ClearAsync(right);

    cleared.StatusCode.ShouldBe(HttpStatusCode.OK);
    cleared.Headers.Location.ShouldBeNull();

    (await ReadAsync(RabbitMq)).ShouldNotContain("rgpd.exercices");
  }

  /// <summary>
  /// ⚠️ <b>Aucun appel au broker à l'enregistrement.</b> L'exchange enregistré désigne un port
  /// d'écoute ouvert par le test : si le service tentait de joindre ce qu'il enregistre — pour
  /// « vérifier » l'exchange, le déclarer, s'y connecter —, une connexion y attendrait. Déclarer un
  /// routage reste une écriture locale, et c'est ce qui le rend enregistrable avant que le broker
  /// existe.
  /// </summary>
  [Fact]
  public async Task EmitsNoNetworkCallWhenSaving()
  {
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();

    try
    {
      var exchange = $"127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";

      (await SaveAsync("Portability", exchange, "droit.portabilite")).StatusCode
        .ShouldBe(HttpStatusCode.Found);

      (await SectionAsync("droit à la portabilité")).ShouldContain(exchange);

      listener.Pending().ShouldBeFalse("Le service a ouvert une connexion vers le routage qu'il enregistrait.");
    }
    finally
    {
      listener.Stop();
    }
  }

  /// <summary>
  /// <b>Un droit portant un routage affiche son exchange et sa routing key</b>, et lui seul : les
  /// cinq autres restent « non configuré ».
  /// </summary>
  [Fact]
  public async Task ShowsTheExchangeAndTheRoutingKeyOfARightThatCarriesARouting()
  {
    const string Exchange = "rgpd.exercices";
    const string Key = "droit.effacement";

    await SaveAsync("Erasure", Exchange, Key);

    var sections = SectionsIn(WebUtility.HtmlDecode(await ReadAsync(RabbitMq)));

    sections[Erasure].ShouldContain(Exchange);
    sections[Erasure].ShouldContain(Key);
    sections[Erasure].ShouldNotContain("non configuré");

    foreach (var untouched in sections.Where(section => section.Key != Erasure))
    {
      untouched.Value.ShouldContain(
        "non configuré", customMessage: $"Le {untouched.Key} ne devait pas être touché.");
    }
  }

  /// <summary>
  /// ⚠️ <b>Un droit réglé sur l'autre canal n'est pas « non configuré »</b> sur cette page : le mot
  /// garde son sens — ni adresse, ni routage —, et « non configuré » ne se dit donc que d'un droit
  /// qui ne porte ni l'un ni l'autre.
  /// </summary>
  [Fact]
  public async Task DoesNotCallARightConfiguredOverHttpUnconfigured()
  {
    await AddressAsync(DataSubjectRight.Access, "https://brocanto.example.fr/rgpd/acces");

    SectionsIn(WebUtility.HtmlDecode(await ReadAsync(RabbitMq)))["droit d'accès"]
      .ShouldNotContain("non configuré");
  }

  /// <summary>
  /// <b>Un droit réglé sur l'autre canal montre son adresse ici</b>, et l'avertissement qui
  /// prévient qu'enregistrer un routage la remplacera. ⚠️ <b>L'avertissement précède le
  /// bouton</b> : lu après, il aurait prévenu d'un geste déjà fait.
  /// </summary>
  [Fact]
  public async Task ShowsTheAddressOfARightConfiguredOverHttpAndWarnsBeforeTheSaveButton()
  {
    const string Endpoint = "https://brocanto.example.fr/rgpd/acces";

    await AddressAsync(DataSubjectRight.Access, Endpoint);

    var access = Flattened(await SectionAsync("droit d'accès"));

    access.ShouldContain(Endpoint);
    access.ShouldContain(ReplacementWarning);
    access.IndexOf(ReplacementWarning, StringComparison.Ordinal)
      .ShouldBeLessThan(access.IndexOf(SaveButton, StringComparison.Ordinal));
  }

  /// <summary>
  /// <b>Après un remplacement, aucune des deux faces ne revendique plus l'ancien canal</b> :
  /// enregistrer un routage sur un droit qui portait une adresse efface cette adresse — cette face
  /// montre le routage et n'avertit plus, la face HTTP ne montre plus l'adresse.
  /// </summary>
  [Fact]
  public async Task ReplacesTheAddressOfARightByARoutingAndNeitherFaceClaimsTheOldAddress()
  {
    const string Endpoint = "https://brocanto.example.fr/rgpd/effacement";
    const string Exchange = "rgpd.exercices";
    const string Key = "droit.effacement";

    await AddressAsync(DataSubjectRight.Erasure, Endpoint);

    (await SaveAsync("Erasure", Exchange, Key)).StatusCode.ShouldBe(HttpStatusCode.Found);

    var erasure = Flattened(await SectionAsync(Erasure));

    erasure.ShouldContain(Exchange);
    erasure.ShouldContain(Key);
    erasure.ShouldNotContain(Endpoint);
    erasure.ShouldNotContain(ReplacementWarning);
    erasure.ShouldNotContain("non configuré");

    var onTheOtherFace = SectionIn(WebUtility.HtmlDecode(await ReadAsync(Http)), Erasure);

    onTheOtherFace.ShouldNotContain(Endpoint);
    onTheOtherFace.ShouldContain(Exchange);
    onTheOtherFace.ShouldContain(Key);
  }

  /// <summary>
  /// Le critère du ticket : <b>clé de connexion absente et au moins un routage posé</b>, le bandeau
  /// paraît sur la face RabbitMQ — et il <b>nomme la clé</b>, sans quoi l'intégrateur saurait qu'il
  /// manque quelque chose sans savoir où le poser.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le routage s'enregistre quand même</b>, et le test le vérifie dans le même souffle :
  /// avertir n'est pas refuser, et le déploiement dont le bus n'existe pas encore doit pouvoir se
  /// préparer (ADR-0027).
  /// </remarks>
  [Fact]
  public async Task WarnsThatNoBrokerConnectionIsConfiguredOnceARoutingIsPosed()
  {
    const string Exchange = "rgpd.exercices";

    (await SaveAsync("Erasure", Exchange, "droit.effacement")).StatusCode.ShouldBe(HttpStatusCode.Found);

    var screen = WebUtility.HtmlDecode(await ReadAsync(RabbitMq));

    var advisory = TheBrokerConnectionAdvisory.In(screen);

    advisory.ShouldNotBeNull("Aucun bandeau ne prévient que rien ne partira sur le bus.");
    advisory.ShouldContain(RabbitMqOptions.HostNameKey);

    // Le routage est bien là : le bandeau avertit, il n'a rien refusé.
    (await SectionAsync(Erasure)).ShouldContain(Exchange);
  }

  /// <summary>
  /// <b>Pas de bandeau tant qu'aucun routage n'a été posé</b> — pas même sur un service vierge, et
  /// pas davantage pour un droit réglé sur l'autre canal : le manque de connexion ne concerne pas
  /// encore celui qui n'a rien déclaré, et un avertissement permanent devient un meuble.
  /// </summary>
  [Fact]
  public async Task SaysNothingAboutTheBrokerConnectionUntilARoutingIsPosed()
  {
    TheBrokerConnectionAdvisory.In(WebUtility.HtmlDecode(await ReadAsync(RabbitMq)))
      .ShouldBeNull("Un service vierge n'a aucun routage dont il faudrait avertir.");

    await AddressAsync(DataSubjectRight.Access, "https://brocanto.example.fr/rgpd/acces");

    TheBrokerConnectionAdvisory.In(WebUtility.HtmlDecode(await ReadAsync(RabbitMq)))
      .ShouldBeNull("Un droit réglé en HTTP n'est pas un routage qu'on croirait opérationnel.");
  }

  /// <summary>
  /// <b>Effacer le dernier routage retire le bandeau</b> : l'avertissement suit ce qui est déclaré,
  /// et ne survit pas à ce qui l'a fait paraître.
  /// </summary>
  [Fact]
  public async Task TakesTheWarningBackDownWhenTheLastRoutingIsCleared()
  {
    await SaveAsync("Erasure", "rgpd.exercices", "droit.effacement");

    (await ClearAsync("Erasure")).StatusCode.ShouldBe(HttpStatusCode.Found);

    TheBrokerConnectionAdvisory.In(WebUtility.HtmlDecode(await ReadAsync(RabbitMq))).ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Le bandeau ne paraît jamais sur la face HTTP</b>, routage posé ou non : un avertissement
  /// RabbitMQ n'a rien à dire d'un écran qui ne parle pas de RabbitMQ, et la clé n'y est pas même
  /// nommée.
  /// </summary>
  [Fact]
  public async Task NeverWarnsAboutTheBrokerConnectionOnTheHttpFace()
  {
    await SaveAsync("Erasure", "rgpd.exercices", "droit.effacement");

    var http = WebUtility.HtmlDecode(await ReadAsync(Http));

    TheBrokerConnectionAdvisory.In(http).ShouldBeNull("La face HTTP porte un avertissement qui ne la regarde pas.");
    http.ShouldNotContain(RabbitMqOptions.HostNameKey);
  }

  private async Task<string> ReadAsync(string screen)
  {
    var response = await _client.GetAsync(screen);

    response.StatusCode.ShouldBe(HttpStatusCode.OK, $"L'adresse {screen} doit répondre.");

    return await response.Content.ReadAsStringAsync();
  }

  /// <summary>La section d'un droit, retrouvée par son libellé — entités décodées.</summary>
  private async Task<string> SectionAsync(string label) =>
    SectionIn(WebUtility.HtmlDecode(await ReadAsync(RabbitMq)), label);

  /// <summary>Les six sections de la face RabbitMQ, chacune sous son libellé.</summary>
  private async Task<IReadOnlyDictionary<string, string>> SectionsAsync() =>
    SectionsIn(WebUtility.HtmlDecode(await ReadAsync(RabbitMq)));

  /// <summary>Les sections des six droits, chacune sous son libellé.</summary>
  private static IReadOnlyDictionary<string, string> SectionsIn(string screen)
  {
    return TheSixRights.ToDictionary(right => right.Label, right => SectionIn(screen, right.Label));
  }

  /// <summary>
  /// ⚠️ <b>L'avertissement ne paraît que pour un droit à remplacer</b> : un droit « non configuré »
  /// n'a rien à perdre, et un droit déjà routé ne remplace que son propre réglage. Avertir partout
  /// aurait fait lire l'avertissement comme un ornement de la page.
  /// </summary>
  [Fact]
  public async Task WarnsOfNoReplacementWhenThereIsNothingToReplace()
  {
    (await SectionAsync("droit d'accès")).ShouldNotContain(ReplacementWarning);

    (await SaveAsync("Access", "rgpd.exercices", "droit.acces"))
      .StatusCode.ShouldBe(HttpStatusCode.Found);

    Flattened(await SectionAsync("droit d'accès")).ShouldNotContain(ReplacementWarning);
  }

  /// <summary>
  /// La section, ses blancs de gabarit réduits à une espace : une phrase que le gabarit coupe en
  /// deux lignes reste <b>une</b> phrase, et c'est elle qu'on lit — pas sa mise en page.
  /// </summary>
  private static string Flattened(string section) => Regex.Replace(section, @"\s+", " ");

  private static string SectionIn(string screen, string label) =>
    screen.Split("<div class=\"right\">")
      .Single(section => section.Contains($"<h2>{label}</h2>", StringComparison.Ordinal));

  /// <summary>
  /// Le bloc d'un champ dans la section d'un droit — son libellé, son champ, son aide et le refus
  /// qui s'y range. ⚠️ C'est LUI qu'on lit pour dire qu'un refus est <b>sous le bon champ</b> : lu
  /// sur la section entière, le refus de l'exchange se serait confondu avec celui de la routing key.
  /// </summary>
  private static string FieldIn(string section, string field)
  {
    var blocks = Regex.Matches(section, @"<p\b[^>]*>(?:(?!</p>).)*</p>", RegexOptions.Singleline)
      .Select(block => block.Value)
      .Where(block => block.Contains($"name=\"Form.{field}\"", StringComparison.Ordinal))
      .ToList();

    blocks.Count.ShouldBe(1, $"La section ne porte pas un bloc unique pour le champ {field}.");

    return blocks[0];
  }

  /// <summary>La légende du <c>fieldset</c> d'une section — ce qui rattache ses champs à son droit.</summary>
  private static string LegendIn(string section)
  {
    var legend = Regex.Match(section, @"<legend\b[^>]*>(.*?)</legend>", RegexOptions.Singleline);

    legend.Success.ShouldBeTrue("La section ne porte aucune légende.");

    return legend.Groups[1].Value;
  }

  /// <summary>
  /// Les onglets du Paramétrage, dans l'ordre où la page les pose : leur adresse, ce qu'ils donnent
  /// à lire, et le marquage de la face courante. ⚠️ Ils se lisent dans la <b>région d'onglets</b>,
  /// jamais dans la page entière : le panneau latéral porte lui aussi un lien vers
  /// <c>/parametrage</c>, et une lecture large l'aurait pris pour un onglet.
  /// </summary>
  private static IReadOnlyList<(string Address, string Label, bool IsCurrent)> TabsIn(string screen)
  {
    return
    [
      .. Regex.Matches(TabsRegionIn(screen), @"<a\b([^>]*)>(.*?)</a>", RegexOptions.Singleline).Select(link =>
      (
        Address: Regex.Match(link.Groups[1].Value, @"\bhref=""([^""]*)""").Groups[1].Value,
        Label: link.Groups[2].Value.Trim(),
        IsCurrent: link.Groups[1].Value.Contains(@"aria-current=""page""", StringComparison.Ordinal)
      )),
    ];
  }

  /// <summary>La région d'onglets d'une page rendue, reconnue à sa classe.</summary>
  private static string TabsRegionIn(string screen)
  {
    var tabs = Regex.Match(screen, @"<nav\b[^>]*\bclass=""tabs""[^>]*>(.*?)</nav>", RegexOptions.Singleline);

    tabs.Success.ShouldBeTrue("L'écran ne porte aucune région d'onglets.");

    return tabs.Groups[1].Value;
  }

  /// <summary>
  /// Remplit la mini-form d'un droit et la renvoie, jeton anti-rejeu compris — exactement ce que
  /// fait un navigateur, et le seul chemin qui existe vers la face RabbitMQ.
  /// </summary>
  private async Task<HttpResponseMessage> SaveAsync(string right, string exchange, string key)
  {
    return await _client.PostAsync(Save, new FormUrlEncodedContent(
    [
      new("__RequestVerificationToken", await AntiforgeryTokenAsync()),
      new("Form.Right", right),
      new("Form.Exchange", exchange),
      new("Form.RoutingKey", key),
    ]));
  }

  /// <summary>
  /// Envoie le bouton <b>Effacer</b> de la mini-form d'un droit, jeton anti-rejeu compris : l'envoi
  /// ne porte que le droit, jamais un routage.
  /// </summary>
  private async Task<HttpResponseMessage> ClearAsync(string right)
  {
    return await _client.PostAsync(Clear, new FormUrlEncodedContent(
    [
      new("__RequestVerificationToken", await AntiforgeryTokenAsync()),
      new("Form.Right", right),
    ]));
  }

  private async Task<string> AntiforgeryTokenAsync()
  {
    var token = Regex.Match(
      await ReadAsync(RabbitMq),
      @"<input name=""__RequestVerificationToken""[^>]*value=""([^""]+)""");

    token.Success.ShouldBeTrue("La face RabbitMQ ne porte aucun jeton anti-rejeu.");

    return token.Groups[1].Value;
  }

  /// <summary>
  /// Pose l'adresse HTTP d'un droit <b>par le use case</b> : c'est le réglage de l'autre face, et
  /// cette face n'a pas à l'écrire.
  /// </summary>
  private async Task AddressAsync(DataSubjectRight right, string url)
  {
    using var scope = factory.Services.CreateScope();

    var written = await scope.ServiceProvider.GetRequiredService<Mediator.IMediator>().Send(
      new SetRightEndpointCommand(right, EndpointUrl.From(url)));

    written.IsSuccess.ShouldBeTrue();
  }

  /// <summary>Ramène le service à son état d'installation : aucune ligne de Paramétrage.</summary>
  private async Task ForgetEveryChannelAsync()
  {
    using var scope = factory.Services.CreateScope();

    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Settings>().ExecuteDeleteAsync();
  }
}
