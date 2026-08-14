using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Data.Casework;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// L'écran du dossier, exercé par sa <b>seule frontière HTTP</b> : c'est le seul endroit où
/// l'<c>Operator</c> agit, et le seul chemin qui existe vers l'instruction.
/// </summary>
/// <remarks>
/// Ce que ces tests gardent est ce que l'écran <b>réclame</b> — un constat sur un « fait » sans
/// rattachement, un nom à chaque signature — et ce qu'il <b>refuse de taire</b> : l'âge du
/// recensement, et une date de réception que personne n'a déclarée.
/// </remarks>
[Collection(WebCollection.Name)]
public class CaseScreen(CustomWebApplicationFactory<Program> factory)
{
  private static readonly DateTimeOffset DeclaredLongAgo = new(2025, 3, 14, 9, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset DeclaredRecently = new(2026, 7, 30, 9, 0, 0, TimeSpan.Zero);

  private readonly OperatorSurface _surface = new(factory);

  /// <summary>
  /// <b>Legs 1/3 — la plus ancienne date de déclaration du <c>Manifest</c> est nommée dans le
  /// bandeau</b>, et chaque <c>Step</c> porte celle de son système. C'est au moment où quelqu'un signe
  /// qu'une déclaration vieille doit lui être rappelée : le catalogue vieillit exprès.
  /// </summary>
  [Fact]
  public async Task NamesTheOldestManifestDeclarationInTheBanner()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      [DataSubjectRight.Access],
      OperatorSurface.ASystem("vieux-recensement", "La comptabilité scellée", DeclaredLongAgo),
      OperatorSurface.ASystem("recensement-frais", "La base de la boutique", DeclaredRecently));

    var screen = await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    screen.ShouldContain("La plus ancienne déclaration des systèmes de ce dossier date du 14/03/2025");

    // Et la date descend jusque sur le Step, là où l'on signe.
    screen.ShouldContain("déclaré le 14/03/2025");
    screen.ShouldContain("déclaré le 30/07/2026");

    // Le recensement ne se présente jamais comme complet, sur cet écran non plus.
    screen.ShouldContain("ne garantit pas qu'il n'en existe pas d'autres");
  }

  /// <summary>
  /// <b>Legs 2/3 — un <c>Step</c> <c>Done</c> à zéro rattachement fait réclamer un constat</b>, sans
  /// qu'aucun état nouveau n'ait eu à naître : six zéros ne doivent pas se lire « cette personne n'est
  /// pas chez nous ».
  /// </summary>
  [Fact]
  public async Task AwaitsAFindingOnAWorkDeclaredDoneWithoutASingleAttachment()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      [DataSubjectRight.Access],
      OperatorSurface.ASystem("boutique-constat", "La base de la boutique", DeclaredRecently));

    // À faire : rien à constater encore.
    (await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened))).ShouldNotContain("Constat réclamé");

    var declaring = await _surface.DeclareAsync(opened, new Declaration(
      nameof(DataSubjectRight.Access),
      "boutique-constat",
      nameof(StepState.Done),
      "Requête lancée le 3 ; aucun compte trouvé sous l'adresse dont nous disposons.",
      "Claire Berger"));

    declaring.StatusCode.ShouldBe(HttpStatusCode.Found);

    var screen = await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    screen.ShouldContain("Constat réclamé");
    screen.ShouldContain("ne se lise pas « cette personne n'est pas chez nous »");

    // ⚠️ Aucun état nouveau : le vocabulaire des Step reste à cinq valeurs, et « fait, mais à
    // constater » n'en est pas une.
    StepState.List.Count.ShouldBe(5);
  }

  /// <summary>
  /// <b>Legs 3/3 — une date de réception non déclarée s'affiche <c>J+9 (défaut)</c></b>, sur les deux
  /// écrans. Le défaut doit être visible <b>comme un défaut</b>, jamais confondu avec un fait déclaré :
  /// on nomme la règle appliquée plutôt que la date qu'elle a produite.
  /// </summary>
  [Fact]
  public async Task ShowsADefaultedReceptionDateAsADefaultRatherThanAsADate()
  {
    var opened = await _surface.OpenAsync(ReceptionDate.Defaulted(DateTimeOffset.UtcNow));

    var onScreen = await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    onScreen.ShouldContain("J+9 (défaut)");
    onScreen.ShouldContain("Personne n'a déclaré cette date");

    // La date produite par le défaut ne s'affiche pas à la place d'un fait.
    var defaulted = ReceptionDate.Defaulted(DateTimeOffset.UtcNow).On.ToString("dd/MM/yyyy", null);
    Regex.Matches(onScreen, Regex.Escape(defaulted)).Count.ShouldBe(0);

    // Et la file le dit de la même façon : c'est la même règle, aux deux endroits.
    var queue = await _surface.ReadTextAsync(OperatorSurface.Queue);
    queue.ShouldContain("J+9 (défaut)");
  }

  /// <summary>
  /// <b>La signature se fait en saisissant un nom, et l'<c>EvidenceLog</c> enregistre le nom <em>et</em> le
  /// régime « non authentifié ».</b> Sans le régime, la preuve d'aujourd'hui serait indiscernable de
  /// celle du jour où la GUI authentifiera son <c>Operator</c>.
  /// </summary>
  [Fact]
  public async Task WritesTheTypedNameAndTheUnauthenticatedRegimeToTheEvidenceLog()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      [DataSubjectRight.Access],
      OperatorSurface.ASystem("journal-signature", "Le journal applicatif", DeclaredRecently));

    const string Finding = "Journal relu à la main sur les trois derniers mois : aucune ligne.";

    var declaring = await _surface.DeclareAsync(opened, new Declaration(
      nameof(DataSubjectRight.Access),
      "journal-signature",
      nameof(StepState.Untreated),
      Finding,
      "  Claire Berger  "));

    declaring.StatusCode.ShouldBe(HttpStatusCode.Found);

    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var line = await dbContext.Set<EvidenceLogRow>()
      .AsNoTracking()
      .SingleAsync(row => row.CaseId == opened.Value && row.Fact == "StepDeclared");

    line.SignatoryKind.ShouldBe("Operator");
    line.SignatoryName.ShouldBe("Claire Berger");
    line.SignerVerification.ShouldBe("Unauthenticated");

    // L'aveu que personne ne l'a fait s'enregistre comme le reste : le service ne bloque jamais la
    // trace la plus précieuse du dispositif.
    line.StepState.ShouldBe("Untreated");
    line.DataSubjectRight.ShouldBe("Access");

    // Le texte qui reste survit dans l'EvidenceLog, mot pour mot.
    line.Prose.ShouldBe(Finding);

    // Anonyme côté personne concernée, dès cette ligne comme dès la première.
    line.DesignationCount.ShouldBeNull();
  }

  /// <summary>
  /// <b>Le constat est réclamé, et le nom aussi</b> — mais la route n'est pas barrée pour autant : le
  /// refus nomme ce qui manque, et le dossier n'a pas bougé. Une signature vide serait une preuve qui
  /// ne prouve rien.
  /// </summary>
  [Theory]
  [InlineData("", "Claire Berger", "Le constat est absent ou vide.")]
  [InlineData("Un constat sans personne pour l'avoir fait.", "   ", "Le nom du signataire est absent ou vide.")]
  public async Task RefusesToWriteADeclarationThatNobodySignedOrMotivated(
    string finding,
    string signedBy,
    string expected)
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      [DataSubjectRight.Access],
      OperatorSurface.ASystem($"refus-{finding.Length}-{signedBy.Length}", "Un système", DeclaredRecently));

    var refusal = await _surface.DeclareAsync(opened, new Declaration(
      nameof(DataSubjectRight.Access),
      $"refus-{finding.Length}-{signedBy.Length}",
      nameof(StepState.Done),
      finding,
      signedBy));

    refusal.StatusCode.ShouldBe(HttpStatusCode.OK);

    var refused = Regex.Replace(
      System.Net.WebUtility.HtmlDecode(Regex.Replace(await refusal.Content.ReadAsStringAsync(), "<[^>]+>", " ")),
      @"\s+",
      " ");

    refused.ShouldContain(expected);

    // Rien n'a été écrit : ni le dossier, ni la preuve.
    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    (await dbContext.Set<EvidenceLogRow>().AsNoTracking()
      .AnyAsync(row => row.CaseId == opened.Value && row.Fact == "StepDeclared"))
      .ShouldBeFalse();

    (await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened))).ShouldContain("à faire");
  }

  /// <summary>
  /// <b>Le constat n'est réclamé que là où l'écran le réclame.</b> Passer un travail à « en attente »
  /// sans un mot est accueilli : exiger une prose sur chaque état ferait écrire une ligne de rien à
  /// chaque clic, et le constat qui compte — celui d'un « fait » sans rattachement — se noierait dans
  /// les autres.
  /// </summary>
  [Fact]
  public async Task AsksForNoFindingOnAStateTheScreenDoesNotClaimOneFor()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      [DataSubjectRight.Access],
      OperatorSurface.ASystem("constat-facultatif", "Un système", DeclaredRecently));

    var declaring = await _surface.DeclareAsync(opened, new Declaration(
      nameof(DataSubjectRight.Access),
      "constat-facultatif",
      nameof(StepState.Awaiting),
      string.Empty,
      "Claire Berger"));

    declaring.StatusCode.ShouldBe(HttpStatusCode.Found);

    (await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened))).ShouldContain("en attente");

    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var line = await dbContext.Set<EvidenceLogRow>()
      .AsNoTracking()
      .SingleAsync(row => row.CaseId == opened.Value && row.Fact == "StepDeclared");

    // La colonne reste vide plutôt que de porter une chaîne vide, qui se lirait comme un constat
    // qu'on aurait effacé.
    line.Prose.ShouldBeNull();
  }

  /// <summary>
  /// <b>Un refus rend sa prose à l'<c>Operator</c></b>, et dit du même coup <b>tout</b> ce qui manque.
  /// Un constat perdu parce que le nom était vide serait du travail jeté, et un second aller-retour
  /// pour découvrir le second refus ferait corriger à l'aveugle.
  /// </summary>
  [Fact]
  public async Task GivesTheFindingBackOnARefusalAndNamesEverythingThatIsMissingAtOnce()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      [DataSubjectRight.Access],
      OperatorSurface.ASystem("prose-rendue", "Un système", DeclaredRecently));

    const string Written = "Requête lancée le 3 sur les deux bases, rien sous cette adresse.";

    var refusal = await _surface.DeclareAsync(opened, new Declaration(
      nameof(DataSubjectRight.Access),
      "prose-rendue",
      nameof(StepState.Done),
      Written,
      "   "));

    refusal.StatusCode.ShouldBe(HttpStatusCode.OK);

    var screen = await refusal.Content.ReadAsStringAsync();

    // La prose revient dans le formulaire d'où elle vient, et le nom manquant est nommé.
    screen.ShouldContain(Written);
    screen.ShouldContain("Le nom du signataire est absent ou vide.");
  }

  /// <summary>
  /// Les deux cases vides sont nommées <b>en un seul aller-retour</b> : découvrir le second refus après
  /// avoir corrigé le premier ferait travailler à l'aveugle.
  /// </summary>
  [Fact]
  public async Task NamesTheMissingNameAndTheMissingFindingInOneRoundTrip()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      [DataSubjectRight.Access],
      OperatorSurface.ASystem("deux-refus", "Un système", DeclaredRecently));

    var refusal = await _surface.DeclareAsync(opened, new Declaration(
      nameof(DataSubjectRight.Access),
      "deux-refus",
      nameof(StepState.Done),
      string.Empty,
      string.Empty));

    var refused = Regex.Replace(
      System.Net.WebUtility.HtmlDecode(Regex.Replace(await refusal.Content.ReadAsStringAsync(), "<[^>]+>", " ")),
      @"\s+",
      " ");

    refused.ShouldContain("Le nom du signataire est absent ou vide.");
    refused.ShouldContain("Le constat est absent ou vide.");
  }

  /// <summary>
  /// <b><c>Untreated</c> est offert comme les autres.</b> Sans la valeur laide, l'<c>Operator</c> pressé
  /// coche la valeur propre et le service fabrique un faux au lieu d'enregistrer un vide.
  /// </summary>
  [Fact]
  public async Task OffersTheFiveStatesIncludingTheOneThatAdmitsNobodyDidTheWork()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      [DataSubjectRight.Access],
      OperatorSurface.ASystem("cinq-etats", "Un système", DeclaredRecently));

    var markup = await _surface.ReadAsync(OperatorSurface.AddressOf(opened));

    foreach (var state in StepState.List)
    {
      markup.ShouldContain($"value=\"{state.Name}\"");
    }

    // Les libellés français se lisent dans le texte, apostrophes comprises : c'est cette liste-là que
    // l'Operator déroule, et « non traité » doit y être aussi visible que « fait ».
    var screen = await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    screen.ShouldContain("non traité");
    screen.ShouldContain("hors d'atteinte");
  }

  /// <summary>
  /// <b>Texte qui meurt et texte qui reste sont deux champs à deux endroits distincts</b> : la règle
  /// tient par le <b>placement</b>, et non par la discipline de l'<c>Operator</c>. Le seul champ de
  /// prose que le formulaire de signature porte est le constat, et l'écran dit de chacun où il va.
  /// </summary>
  [Fact]
  public async Task KeepsTheTextThatDiesAndTheTextThatRemainsInTwoDistinctPlaces()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      [DataSubjectRight.Access],
      OperatorSurface.ASystem("deux-proses", "Un système", DeclaredRecently));

    var screen = await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    // Le point de décision : le texte qui reste, avec le nom, et l'écran dit qu'il survit.
    screen.ShouldContain("Signer ce constat");
    screen.ShouldContain("entre dans la matière de preuve et survit au dossier");

    // L'autre endroit : le texte qui meurt, dont l'écran dit qu'il meurt à la clôture.
    screen.ShouldContain("Le texte qui meurt");
    screen.ShouldContain("disparaît à sa clôture");
    screen.ShouldContain("Rien de cette section n'entre dans la matière de preuve");

    var markup = await _surface.ReadAsync(OperatorSurface.AddressOf(opened));

    // Les champs saisissables sont énumérés en toutes lettres : en ajouter un doit être un geste
    // délibéré, parce que c'est par un champ de prose non qualifié qu'un nom de tiers entrerait dans
    // la preuve.
    Regex.Matches(markup, @"name=""(Form\.[A-Za-z]+)""")
      .Select(match => match.Groups[1].Value)
      .Distinct()
      .Order(StringComparer.Ordinal)
      .ShouldBe(["Form.DeclaredSystem", "Form.Finding", "Form.Right", "Form.SignedBy", "Form.State"]);
  }

  /// <summary>
  /// <b>Le nom n'est jamais pré-rempli ni mémorisé d'une signature à la suivante.</b> Une case
  /// pré-remplie ferait signer quelqu'un sans qu'il l'ait voulu.
  /// </summary>
  [Fact]
  public async Task NeverRemembersTheNameOfTheLastPersonWhoSigned()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      [DataSubjectRight.Access],
      OperatorSurface.ASystem("sans-memoire", "Un système", DeclaredRecently));

    await _surface.DeclareAsync(opened, new Declaration(
      nameof(DataSubjectRight.Access),
      "sans-memoire",
      nameof(StepState.Done),
      "Un constat quelconque, mais écrit.",
      "Claire Berger"));

    (await _surface.ReadAsync(OperatorSurface.AddressOf(opened))).ShouldNotContain("value=\"Claire Berger\"");
  }

  /// <summary>
  /// <b>Aucun nombre affiché ne viole la règle des chiffres</b>, sur cet écran non plus : ni « 2
  /// systèmes sur 3 », ni taux de couverture. L'écran <b>énumère</b>.
  /// </summary>
  [Fact]
  public async Task ShowsNoCountNoRatioAndNoDenominatorOfTheClientsLandscape()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      [DataSubjectRight.Access, DataSubjectRight.Erasure],
      OperatorSurface.ASystem("chiffres-1", "Un premier système", DeclaredRecently),
      OperatorSurface.ASystem("chiffres-2", "Un second système", DeclaredRecently));

    var screen = await _surface.ReadWithoutStyleAsync(OperatorSurface.AddressOf(opened));

    screen.ShouldNotContain("%");
    Regex.IsMatch(screen, @"\d+\s*(systèmes?|dossiers?|sur\s+\d+)").ShouldBeFalse(
      "Un dénominateur qui décrit le paysage du client donnerait à une déclaration faussable en "
      + "silence l'autorité d'un recensement.");
  }

  /// <summary>
  /// Un dossier ouvert sur un catalogue vide se lit, et <b>le dit</b> : aucun travail dû n'est un fait,
  /// jamais un travail achevé. C'est l'état d'un service qu'on vient d'installer.
  /// </summary>
  [Fact]
  public async Task SaysThatAnEmptyManifestLeftTheCaseWithoutAnyDueWork()
  {
    var opened = await _surface.OpenAsync(ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)));

    (await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened)))
      .ShouldContain("Aucun système n'était déclaré quand ce dossier s'est ouvert");
  }

  /// <summary>
  /// <b>Un <c>Claim</c> <c>Proposed</c> se confirme <em>dans le dossier</em>, pendant que le délai
  /// court.</b> Il n'existe aucun état d'attente hors du <c>Case</c> : le dossier est ouvert, il est
  /// dans la file, et la confirmation qui manque est une ligne <b>visible</b> plutôt qu'un vestibule
  /// que personne ne regarde.
  /// </summary>
  [Fact]
  public async Task ConfirmsAProposedRightInsideTheOpenCaseRatherThanInAnyWaitingRoom()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-4)),
      ClaimOrigin.Proposed,
      motivation: null,
      [DataSubjectRight.Erasure]);

    var address = OperatorSurface.AddressOf(opened);

    // Le dossier est LISIBLE et l'attente y est écrite : elle n'a pas retenu la demande à l'entrée.
    var before = await _surface.ReadTextAsync(address);

    before.ShouldContain("Confirmation réclamée");
    before.ShouldContain(ClaimOrigin.Proposed.FrenchLabel);

    // Et le dossier est bien dans la file pendant qu'il attend : le compteur ne s'arrête pour
    // personne, et une demande non confirmée ne doit pas disparaître de la vue.
    (await _surface.ReadAsync(OperatorSurface.Queue)).ShouldContain(opened.Value.ToString());

    var confirmed = await _surface.ConfirmAsync(opened, nameof(DataSubjectRight.Erasure), "Claire Martin");

    // Une redirection après l'écriture : recharger la page ne reconfirme rien.
    confirmed.StatusCode.ShouldBe(HttpStatusCode.Found);

    (await _surface.ReadTextAsync(address)).ShouldNotContain("Confirmation réclamée");
  }

  /// <summary>
  /// <b>Une confirmation que personne n'a signée n'a pas lieu.</b> Confirmer est précisément le geste
  /// qui fait passer une proposition de machine au compte de quelqu'un : sans nom, la preuve dirait
  /// qu'un droit a été reconnu par personne.
  /// </summary>
  [Fact]
  public async Task RefusesToConfirmARightUnderNobodysName()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-4)),
      ClaimOrigin.Proposed,
      motivation: null,
      [DataSubjectRight.Objection]);

    var refused = await _surface.ConfirmAsync(opened, nameof(DataSubjectRight.Objection), string.Empty);

    refused.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await refused.Content.ReadAsStringAsync()).ShouldContain("Le nom du signataire");

    // Et rien n'a bougé : la réclamation est toujours là.
    (await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened)))
      .ShouldContain("Confirmation réclamée");
  }

  /// <summary>
  /// <b>Un droit garde la porte sous laquelle il est né.</b> L'écran nomme l'identité <em>d'origine</em>
  /// à côté du droit, et non celle que le dossier déclare aujourd'hui : une déclaration relevée en
  /// fin de dossier ne réécrit pas la preuve d'hier.
  /// </summary>
  [Fact]
  public async Task NamesTheIdentityUnderWhichEachRightWasOpenedRatherThanTodays()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-5)),
      ClaimOrigin.Named,
      motivation: null,
      [DataSubjectRight.Access]);

    var screen = await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    screen.ShouldContain($"ouvert sous l'identité « {IdentityDeclaration.Unverified.FrenchLabel} »");
  }
}
