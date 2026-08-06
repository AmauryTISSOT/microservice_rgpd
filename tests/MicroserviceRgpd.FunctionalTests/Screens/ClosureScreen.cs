using System.Net;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Ledger;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Data.Casework;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// La <b>clôture</b>, exercée par la seule frontière HTTP : le geste par lequel un humain met fin au
/// dossier, et détruit tout son nominatif à l'instant même.
/// </summary>
/// <remarks>
/// <para>
/// Ce que ces tests gardent est l'équilibre exact du dispositif : <b>la clôture détruit tout ce qui
/// nomme, et ne recouvre rien de ce qui manque</b>. Un <c>Step</c> laissé <c>ToDo</c> le reste, le
/// <c>Ledger</c> n'a pas bougé, et il n'y a plus une désignation nulle part.
/// </para>
/// <para>
/// C'est aussi le seul geste irréversible du dispositif, et sa parade est ici : une case à cocher
/// dans l'écran, jamais de la donnée gardée en réserve.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ClosureScreen(CustomWebApplicationFactory<Program> factory)
{
  private static readonly DateTimeOffset Declared = new(2026, 7, 30, 9, 0, 0, TimeSpan.Zero);

  private readonly OperatorSurface _surface = new(factory);

  /// <summary>
  /// <b>Le test d'acceptation du lot.</b> Un dossier se clôt avec un <c>Step</c> resté <c>ToDo</c> ;
  /// tout le nominatif est détruit à l'instant ; le <c>Ledger</c>, lui, est intact et continue de
  /// nommer l'<c>Operator</c>.
  /// </summary>
  [Fact]
  public async Task ClosesACaseWithWorkLeftUndoneDestroysItsNamesAndLeavesTheLedgerIntact()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      ClaimOrigin.Named,
      IdentityMotivation.Of(
        IdentityVerificationMethod.CallbackOnKnownContact,
        "Rappel au numéro connu ; Jean Dupont a confirmé sa date de naissance."),
      [
        Designation.Of(DesignationKind.Email, "jean.dupont@example.fr"),
        Designation.Of(DesignationKind.PersonName, "Jean Dupont"),
      ],
      [DataSubjectRight.Access],
      OperatorSurface.ASystem("boutique-cloture", "La base de la boutique", Declared),
      OperatorSurface.ASystem("journal-cloture", "Le journal applicatif", Declared));

    // Un seul des deux travaux dus est déclaré. L'autre restera « à faire », et c'est le propos.
    var declaring = await _surface.DeclareAsync(opened, new Declaration(
      nameof(DataSubjectRight.Access),
      "boutique-cloture",
      nameof(StepState.Untreated),
      "Personne n'a lancé la requête avant l'échéance.",
      "Claire Berger"));

    declaring.StatusCode.ShouldBe(HttpStatusCode.Found);

    // L'écran réclame AVANT de laisser signer — et il ne barre rien.
    var before = await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    before.ShouldContain("Avant de clore");
    before.ShouldContain("Rien de tout cela ne vous barre la route");

    var closing = await _surface.CloseAsync(
      opened,
      nameof(ClosingCause.Answered),
      "Camille Roy");

    closing.StatusCode.ShouldBe(HttpStatusCode.Found);

    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var closed = await dbContext.Cases.AsNoTracking().SingleAsync(one => one.Id == opened);

    closed.State.ShouldBe(CaseState.Closed);
    closed.ClosingCause.ShouldBe(ClosingCause.Answered);
    closed.ClosedOn.ShouldNotBeNull();

    // ⚠️ PLUS UNE SEULE DÉSIGNATION, et pas dans une minute : à l'instant même.
    closed.Designations.ShouldBeEmpty();

    // La méthode survit — elle se compte, son lecteur est le contrôle. Le détail, qui nomme, est mort.
    closed.Motivation.ShouldNotBeNull();
    closed.Motivation!.Method.ShouldBe(IdentityVerificationMethod.CallbackOnKnownContact);
    closed.Motivation.Detail.ShouldBeNull();

    // ⚠️ LA CLÔTURE NE PROPAGE RIEN ET NE GÈLE RIEN. Le travail resté « à faire » l'est encore, et
    // le droit n'est pas passé « répondu » de lui-même : l'incomplétude reste lisible là où elle est
    // vraie, plutôt que masquée par une cause de clôture rassurante.
    var claim = closed.Claims.Single();

    claim.State.ShouldBe(ClaimState.Open);
    claim.Steps.Single(step => step.DeclaredSystem == DeclaredSystemId.From("journal-cloture"))
      .State.ShouldBe(StepState.ToDo);
    claim.Steps.Single(step => step.DeclaredSystem == DeclaredSystemId.From("boutique-cloture"))
      .State.ShouldBe(StepState.Untreated);

    // LE LEDGER EST INTACT. Sa ligne d'hier est là, mot pour mot, et la clôture n'a fait qu'en
    // ajouter une : la preuve d'une procédure ne dépend pas du sort du dossier qu'elle documente.
    var lines = await dbContext.Set<LedgerRow>()
      .AsNoTracking()
      .Where(row => row.CaseId == opened.Value)
      .ToListAsync();

    lines.Single(row => row.Fact == nameof(LedgerFact.StepDeclared))
      .EvidenceProse.ShouldBe("Personne n'a lancé la requête avant l'échéance.");

    var closure = lines.Single(row => row.Fact == nameof(LedgerFact.CaseClosed));

    closure.ClosingCause.ShouldBe(nameof(ClosingCause.Answered));

    // ⚠️ ELLE CONTINUE DE NOMMER L'OPERATOR, définitivement : son effacement se refuse légitimement.
    closure.SignatoryName.ShouldBe("Camille Roy");
    closure.SignatoryKind.ShouldBe(nameof(SignatoryKind.Operator));

    // Et elle reste anonyme côté personne concernée, comme depuis la première ligne.
    lines.ShouldAllBe(row => row.DesignationCount == null || row.DesignationCount >= 0);

    // L'écran d'après dit ce qui s'est passé, sans prétendre que tout a été fait.
    var after = await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    after.ShouldContain("Dossier clos");
    after.ShouldContain("Tout le nominatif a été détruit à cet instant");

    // L'aveu et l'oubli sont toujours lisibles, un par un.
    after.ShouldContain("non traité");
    after.ShouldContain("à faire");

    // Et plus aucune trace de la personne.
    after.ShouldNotContain("jean.dupont@example.fr");
    after.ShouldNotContain("Jean Dupont");
  }

  /// <summary>
  /// <b>La parade du geste irréversible est la case à cocher, et elle tient.</b> Sans elle, rien
  /// n'est détruit — et l'écran dit pourquoi plutôt que d'échouer en silence.
  /// </summary>
  [Fact]
  public async Task DestroysNothingWhenNoOneDeliberatelyConfirmed()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      [DataSubjectRight.Access]);

    var closing = await _surface.CloseAsync(
      opened,
      nameof(ClosingCause.Answered),
      "Camille Roy",
      confirmed: false);

    // Pas de redirection : l'écran revient avec son refus, sous le nom de la case.
    closing.StatusCode.ShouldBe(HttpStatusCode.OK);

    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var untouched = await dbContext.Cases.AsNoTracking().SingleAsync(one => one.Id == opened);

    untouched.State.ShouldBe(CaseState.Open);
    untouched.Designations.ShouldNotBeEmpty();

    (await dbContext.Set<LedgerRow>().AsNoTracking()
      .AnyAsync(row => row.CaseId == opened.Value && row.Fact == nameof(LedgerFact.CaseClosed)))
      .ShouldBeFalse();
  }

  /// <summary>
  /// <b><c>Abandoned</c> exige un motif, et le refus se dépose sous le nom de SON champ.</b> Sans un
  /// mot, la preuve dirait qu'on a cessé d'instruire sans dire pourquoi, le jour même où tout le
  /// nominatif disparaît.
  /// </summary>
  [Fact]
  public async Task RefusesToAbandonACaseWithoutSayingWhy()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      [DataSubjectRight.Access]);

    var closing = await _surface.CloseAsync(opened, nameof(ClosingCause.Abandoned), "Camille Roy");

    closing.StatusCode.ShouldBe(HttpStatusCode.OK);

    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    (await dbContext.Cases.AsNoTracking().SingleAsync(one => one.Id == opened))
      .State.ShouldBe(CaseState.Open);

    // Le même geste, expliqué, passe — et c'est ainsi qu'une personne demandant l'effacement de son
    // dossier encore ouvert est servie : il n'existe aucune autre fonction pour cela.
    var explained = await _surface.CloseAsync(
      opened,
      nameof(ClosingCause.Abandoned),
      "Camille Roy",
      "La personne s'est ravisée et a demandé l'effacement de son dossier.");

    explained.StatusCode.ShouldBe(HttpStatusCode.Found);

    using var second = factory.Services.CreateScope();
    var reread = second.ServiceProvider.GetRequiredService<AppDbContext>();

    var abandoned = await reread.Cases.AsNoTracking().SingleAsync(one => one.Id == opened);

    abandoned.State.ShouldBe(CaseState.Closed);
    abandoned.ClosingCause.ShouldBe(ClosingCause.Abandoned);
    abandoned.Designations.ShouldBeEmpty();

    // Le motif survit dans la preuve, quand tout le reste du dossier tombe.
    var closure = await reread.Set<LedgerRow>()
      .AsNoTracking()
      .SingleAsync(row => row.CaseId == opened.Value && row.Fact == nameof(LedgerFact.CaseClosed));

    closure.EvidenceProse.ShouldBe("La personne s'est ravisée et a demandé l'effacement de son dossier.");
  }

  /// <summary>
  /// <b>Répondre est un geste par droit, et la clôture ne le fait à personne.</b> C'est ce que
  /// l'écran réclame, et ce que le dossier clos garde tel que l'humain l'a laissé.
  /// </summary>
  [Fact]
  public async Task AnswersOneRightWithoutAnsweringTheOther()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      [DataSubjectRight.Access, DataSubjectRight.Portability]);

    var answering = await _surface.AnswerClaimAsync(
      opened,
      nameof(DataSubjectRight.Access),
      "Camille Roy");

    answering.StatusCode.ShouldBe(HttpStatusCode.Found);

    var closing = await _surface.CloseAsync(opened, nameof(ClosingCause.Answered), "Camille Roy");

    closing.StatusCode.ShouldBe(HttpStatusCode.Found);

    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var closed = await dbContext.Cases.AsNoTracking().SingleAsync(one => one.Id == opened);

    closed.Claims.Single(claim => claim.Right == DataSubjectRight.Access)
      .State.ShouldBe(ClaimState.Answered);

    // ⚠️ La clôture n'a pas répondu pour l'autre droit : elle ne propage rien, et le droit resté
    // ouvert dans un dossier clos est un fait que la relecture voit.
    closed.Claims.Single(claim => claim.Right == DataSubjectRight.Portability)
      .State.ShouldBe(ClaimState.Open);

    var answered = await dbContext.Set<LedgerRow>()
      .AsNoTracking()
      .SingleAsync(row => row.CaseId == opened.Value && row.Fact == nameof(LedgerFact.ClaimAnswered));

    answered.DataSubjectRight.ShouldBe(nameof(DataSubjectRight.Access));
    answered.SignatoryName.ShouldBe("Camille Roy");
  }

  /// <summary>
  /// <b>Un dossier clos ne se travaille plus, et l'écran n'en offre plus le moyen.</b> Il ne reste
  /// pas un formulaire : les désignations sous lesquelles on cherchait n'existent plus, et
  /// déclarer un travail après coup daterait un geste que plus aucune donnée ne soutient.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le refus du domaine n'est pas éprouvé ici, et il ne peut pas l'être.</b> Il ne reste
  /// aucun formulaire d'où tirer un jeton anti-rejeu — ce qui <em>est</em> la démonstration que
  /// l'écran n'offre plus rien. Que la racine rende faux sur un dossier clos est gardé par
  /// <c>CaseClosureTests.RefusesEveryGestureOnceClosed</c>.
  /// </remarks>
  [Fact]
  public async Task OffersNoFurtherGestureOnceTheCaseIsClosed()
  {
    var opened = await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)),
      [DataSubjectRight.Access],
      OperatorSurface.ASystem("boutique-close", "La base de la boutique", Declared));

    (await _surface.CloseAsync(opened, nameof(ClosingCause.NotApplicable), "Camille Roy"))
      .StatusCode.ShouldBe(HttpStatusCode.Found);

    var screen = await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    // Ni le formulaire de clôture — le geste ne se rejoue pas — ni celui d'une déclaration.
    screen.ShouldNotContain("Clore, détruire et signer");
    screen.ShouldNotContain("Déclarer et signer");
    screen.ShouldNotContain("Déclarer répondu et signer");
    screen.ShouldNotContain("Télécharger la remise");

    // Pas un seul formulaire ne subsiste : l'écran clos se lit, il ne s'agit plus.
    screen.ShouldNotContain("__RequestVerificationToken");

    // ⚠️ ET LE TRAVAIL RESTÉ « À FAIRE » EST TOUJOURS LÀ, lisible. C'est ce que la clôture ne
    // recouvre pas, et c'est la seule trace que l'Omission silencieuse laisse jamais.
    screen.ShouldContain("La base de la boutique");
    screen.ShouldContain("à faire");
  }
}
