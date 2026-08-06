using System.Data.Common;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;
using Npgsql;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;

namespace MicroserviceRgpd.IntegrationTests.Data.Casework;

/// <summary>
/// L'agrégat à travers la vraie base : ce qui s'écrit, ce qui se relit, et ce que la racine emporte
/// avec elle.
/// </summary>
/// <remarks>
/// La base est réelle parce que rien d'autre ne prouve ce qui est en jeu : la clé composite d'un
/// <c>Claim</c> — un droit au plus par dossier —, celle d'un <c>Step</c> — un par (<c>Claim</c>,
/// <c>DeclaredSystem</c>) —, et le fait qu'un dossier relu porte son travail dû sans qu'aucun
/// <c>Include</c> ne l'ait demandé.
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public class CasePersistenceTests(PostgreSqlFixture postgres)
{
  private static readonly DateTimeOffset Received = new(2026, 8, 1, 8, 0, 0, TimeSpan.Zero);
  private static readonly DateTimeOffset Declared = new(2026, 7, 30, 9, 0, 0, TimeSpan.Zero);

  /// <summary>
  /// Le dossier fait l'aller-retour <b>entier</b> : ses <c>Claim</c>, leurs <c>Step</c> et son sac
  /// arrivent avec la racine — un dossier relu sans son travail dû serait un dossier dont
  /// l'incomplétude ne se voit pas.
  /// </summary>
  [Fact]
  public async Task RoundTripsTheWholeCaseThroughItsRootAlone()
  {
    var opened = Open(
      [DataSubjectRight.Access, DataSubjectRight.Erasure],
      [
        Designation.Of(DesignationKind.Email, "jean.dupont@example.fr"),
        Designation.Of(DesignationKind.Reference, "1203"),
      ],
      ASystem("boutique"),
      ASystem("journal"));

    await SaveAsync(opened);

    var reread = await RereadAsync(opened.Id);

    reread.IdentityDeclaration.ShouldBe(IdentityDeclaration.ApplicationSession);
    reread.Reception.ShouldBe(ReceptionDate.Declared(Received));

    reread.Claims.Select(claim => claim.Right).ShouldBe(
      [DataSubjectRight.Access, DataSubjectRight.Erasure], ignoreOrder: true);

    reread.Claims.ShouldAllBe(claim => claim.State == ClaimState.Open);

    foreach (var claim in reread.Claims)
    {
      claim.Steps.Select(step => step.DeclaredSystem).ShouldBe(
        [DeclaredSystemId.From("boutique"), DeclaredSystemId.From("journal")], ignoreOrder: true);

      claim.Steps.ShouldAllBe(step => step.State == StepState.ToDo);
    }

    reread.Designations.Select(designation => (designation.Kind.Token, designation.Value)).ShouldBe(
      [("email", "jean.dupont@example.fr"), ("reference", "1203")], ignoreOrder: true);
  }

  /// <summary>
  /// <b>Une demande portant plusieurs droits donne un seul dossier</b>, et la base ne connaît qu'une
  /// ligne dans <c>cases</c> pour les deux réclamations.
  /// </summary>
  [Fact]
  public async Task WritesASingleRowInCasesWhateverTheNumberOfRights()
  {
    var opened = Open(
      [DataSubjectRight.Access, DataSubjectRight.Erasure, DataSubjectRight.Portability],
      [Designation.Of(DesignationKind.Email, "trois.droits@example.fr")]);

    await SaveAsync(opened);

    var rows = await ScalarListAsync(
      $"select id::text from cases where id = '{opened.Id.Value}'");

    rows.Count.ShouldBe(1);

    var claims = await ScalarListAsync(
      $"select data_subject_right from case_claims where case_id = '{opened.Id.Value}' order by data_subject_right");

    claims.ShouldBe(["Access", "Erasure", "Portability"]);
  }

  /// <summary>
  /// Un <c>Step</c> naît par (<c>Claim</c>, <c>DeclaredSystem</c>), et <b>la base tient la paire</b> :
  /// deux fois le même travail dû sur le même système est impossible, pas seulement improbable.
  /// </summary>
  [Fact]
  public async Task LetsTheDatabaseHoldOneStepPerClaimAndSystem()
  {
    var opened = Open(
      [DataSubjectRight.Access],
      [Designation.Of(DesignationKind.Email, "une.paire@example.fr")],
      ASystem("boutique"));

    await SaveAsync(opened);

    await using var dbContext = postgres.NewDbContext();

    // L'insertion se fait en SQL nu, et non par le modèle : ce qu'on veut prouver est que la
    // *base* refuse la paire en double, y compris à qui contournerait EF Core.
    var refusal = await Should.ThrowAsync<PostgresException>(async () =>
    {
      await dbContext.Database.ExecuteSqlAsync(
        $"""
         insert into case_steps (case_id, data_subject_right, declared_system_id, state)
         values ({opened.Id.Value}, 'Access', 'boutique', 'ToDo')
         """);
    });

    // 23505 : violation d'unicité.
    refusal.SqlState.ShouldBe("23505");
    refusal.ConstraintName.ShouldBe("pk_case_steps");
  }

  /// <summary>
  /// <b>La racine emporte tout ce qui est à elle.</b> C'est ce sur quoi la clôture s'appuiera :
  /// détruire le nominatif d'un dossier ne demandera pas d'aller chercher trois tables à la main.
  /// </summary>
  [Fact]
  public async Task TakesItsClaimsStepsAndBagWithItWhenTheRootGoes()
  {
    var opened = Open(
      [DataSubjectRight.Access],
      [Designation.Of(DesignationKind.PersonName, "Jean Dupont")],
      ASystem("boutique"));

    await SaveAsync(opened);

    await using var dbContext = postgres.NewDbContext();

    dbContext.Cases.Remove(await dbContext.Cases.SingleAsync(one => one.Id == opened.Id));

    await dbContext.SaveChangesAsync();

    (await ScalarListAsync($"select state from case_claims where case_id = '{opened.Id.Value}'")).ShouldBeEmpty();
    (await ScalarListAsync($"select state from case_steps where case_id = '{opened.Id.Value}'")).ShouldBeEmpty();
    (await ScalarListAsync($"select value from case_designations where case_id = '{opened.Id.Value}'")).ShouldBeEmpty();
  }

  /// <summary>
  /// Le dossier se relit par le <b>seul dépôt de ce contexte</b> — celui des <c>Case</c>. Il n'en
  /// existe aucun pour un <c>Claim</c> ni pour un <c>Step</c>, et il ne peut pas en exister : le
  /// dépôt générique est contraint aux agrégats racines, et eux n'en sont pas.
  /// </summary>
  [Fact]
  public async Task ReadsTheCaseThroughTheOnlyRepositoryThisContextHas()
  {
    var opened = Open(
      [DataSubjectRight.Access],
      [Designation.Of(DesignationKind.Email, "par.le.depot@example.fr")],
      ASystem("boutique"));

    await SaveAsync(opened);

    await using var dbContext = postgres.NewDbContext();

    var cases = new EfRepository<Case>(dbContext);

    var reread = await cases.FirstOrDefaultAsync(new CaseByIdSpec(opened.Id));

    reread.ShouldNotBeNull();
    reread.Claims[0].Steps[0].DeclaredSystem.ShouldBe(DeclaredSystemId.From("boutique"));
  }

  /// <summary>
  /// <b>La date de réception et son régime tiennent en deux colonnes, jamais une.</b> Une date nue
  /// serait indiscernable d'une date affirmée par un humain, et le défaut cesserait d'être visible
  /// <em>comme</em> un défaut là où il compte le plus : en base, où il survit à l'écran qui l'a montré.
  /// </summary>
  [Fact]
  public async Task WritesTheReceptionDateAndItsRegimeInTwoDistinctColumns()
  {
    var defaulted = Case.Open(
      CaseId.Next(),
      IdentityDeclaration.Unverified,
      motivation: null,
      [Designation.Of(DesignationKind.Email, "sans.date@example.fr")],
      [DataSubjectRight.Access],
      ClaimOrigin.Named,
      Manifest.Empty,
      ReceptionDate.Defaulted(Received));

    await SaveAsync(defaulted);

    var regimes = await ScalarListAsync(
      $"select reception_is_default::text from cases where id = '{defaulted.Id.Value}'");

    regimes.ShouldBe(["true"]);

    var reread = await RereadAsync(defaulted.Id);

    reread.Reception.IsDefault.ShouldBeTrue();
    reread.Reception.On.ShouldBe(Received.AddDays(-ReceptionDate.DaysHeldAlreadyRunByDefault));
  }

  /// <summary>
  /// <b>Un dossier naît <c>Open</c> en base</b>, et l'état y est écrit par son nom : la file s'appuie
  /// sur lui plutôt que de lister « tous les dossiers », pour qu'un dossier clos n'y réapparaisse jamais
  /// le jour où la clôture existera.
  /// </summary>
  [Fact]
  public async Task WritesTheStateByItsNameAndBornsItOpen()
  {
    var opened = Open(
      [DataSubjectRight.Access],
      [Designation.Of(DesignationKind.Email, "etat.ouvert@example.fr")]);

    await SaveAsync(opened);

    var states = await ScalarListAsync($"select state from cases where id = '{opened.Id.Value}'");

    states.ShouldBe([nameof(CaseState.Open)]);
  }

  /// <summary>
  /// <b>Ce qu'un <c>Claim</c> a figé à sa naissance descend sur sa ligne</b>, et non par une jointure
  /// vers celle du dossier : le jour où quelqu'un reprend la déclaration d'identité du <c>Case</c>,
  /// une jointure rendrait rétroactivement propre un accès ouvert sur rien.
  /// </summary>
  [Fact]
  public async Task WritesOnEachClaimTheOriginAndTheIdentityItFrozeAtItsBirth()
  {
    var proposed = Case.Open(
      CaseId.Next(),
      IdentityDeclaration.Unverified,
      motivation: null,
      [Designation.Of(DesignationKind.Email, "propose@example.fr")],
      [DataSubjectRight.Access],
      ClaimOrigin.Proposed,
      Manifest.Empty,
      ReceptionDate.Declared(Received));

    await SaveAsync(proposed);

    var frozen = await ScalarListAsync(
      $"select origin || '/' || identity_at_origin || '/' || confirmed::text "
      + $"from case_claims where case_id = '{proposed.Id.Value}'");

    frozen.ShouldBe([$"{nameof(ClaimOrigin.Proposed)}/{nameof(IdentityDeclaration.Unverified)}/false"]);

    var reread = await RereadAsync(proposed.Id);

    reread.Claims[0].Origin.ShouldBe(ClaimOrigin.Proposed);
    reread.Claims[0].IdentityAtOrigin.ShouldBe(IdentityDeclaration.Unverified);
    reread.Claims[0].AwaitsConfirmation.ShouldBeTrue();
  }

  /// <summary>
  /// <b>Les deux moitiés de la motivation tiennent en deux colonnes du dossier</b> — celle qui se
  /// compte et celle qui nomme — et toutes deux sont là où la clôture ira les détruire. La méthode
  /// survit ailleurs, dans le <c>Ledger</c>, et jamais ici.
  /// </summary>
  [Fact]
  public async Task WritesTheMotivationInTwoColumnsThatBothDieWithTheCase()
  {
    var motivated = Case.Open(
      CaseId.Next(),
      IdentityDeclaration.Unverified,
      IdentityMotivation.Of(
        IdentityVerificationMethod.AttributeCrosscheck,
        "A su citer le montant de sa dernière facture, que nous ne lui avons jamais écrit."),
      [Designation.Of(DesignationKind.Email, "motive@example.fr")],
      [DataSubjectRight.Access],
      ClaimOrigin.Named,
      Manifest.Empty,
      ReceptionDate.Declared(Received));

    await SaveAsync(motivated);

    var written = await ScalarListAsync(
      $"select identity_verification_method from cases where id = '{motivated.Id.Value}'");

    written.ShouldBe([nameof(IdentityVerificationMethod.AttributeCrosscheck)]);

    var reread = await RereadAsync(motivated.Id);

    reread.Motivation!.Method.ShouldBe(IdentityVerificationMethod.AttributeCrosscheck);
    reread.Motivation.Detail!.ShouldContain("dernière facture");
    reread.AwaitsAMotivation.ShouldBeFalse();
  }

  /// <summary>
  /// <b>« Personne ne l'a pesé » se relit comme tel</b>, et jamais comme <c>None</c> : les deux
  /// colonnes sont nulles ensemble, et le dossier réclame toujours ce qu'aucun humain n'a écrit.
  /// </summary>
  [Fact]
  public async Task RereadsTheAbsenceOfAMotivationAsAnAbsenceRatherThanAsTheAdmission()
  {
    var unweighed = Case.Open(
      CaseId.Next(),
      IdentityDeclaration.Unverified,
      motivation: null,
      [Designation.Of(DesignationKind.Email, "non.pese@example.fr")],
      [DataSubjectRight.Access],
      ClaimOrigin.Named,
      Manifest.Empty,
      ReceptionDate.Declared(Received));

    await SaveAsync(unweighed);

    var reread = await RereadAsync(unweighed.Id);

    reread.Motivation.ShouldBeNull();
    reread.AwaitsAMotivation.ShouldBeTrue();
  }

  /// <summary>
  /// <b>Après la clôture, il ne reste pas une <c>Designation</c> en base.</b> C'est l'exigence que
  /// seule la vraie base peut prouver : le domaine peut vider une liste en mémoire et laisser des
  /// lignes derrière lui si la configuration ne les emporte pas.
  /// </summary>
  /// <remarks>
  /// <b>On compte sur la table, pas sur l'objet relu.</b> Un <c>Case</c> rematérialisé dont le sac
  /// serait vide ne dirait rien des lignes restées dans <c>case_designations</c> : c'est très
  /// exactement le genre d'écart qu'un délai « au cas où » aurait produit sans que personne ne le
  /// voie.
  /// <para>
  /// ⚠️ <b>Le sac du dossier n'est pas la seule table qui nomme.</b> Une réserve porte ses propres
  /// désignations, deux niveaux sous la racine, dans <c>case_reservation_designations</c> — celles
  /// que le système a proposées et que personne n'a encore tranchées. Le dossier en pose donc une
  /// ici : sans elle, la table la plus profonde du nominatif ne serait jamais écrite, et le test
  /// passerait au vert sans l'avoir regardée une seule fois.
  /// </para>
  /// </remarks>
  [Fact]
  public async Task LeavesNotASingleDesignationInTheDatabaseOnceTheCaseIsClosed()
  {
    var opened = Open(
      [DataSubjectRight.Access],
      [
        Designation.Of(DesignationKind.Email, "a.fermer@example.fr"),
        Designation.Of(DesignationKind.Reference, "4471"),
      ],
      ASystem("boutique"));

    opened.Ask(OpenQuestionSubject.Designation, Received.AddDays(2));

    // Une réserve non tranchée, et ses désignations propres : le nominatif le plus enfoui du
    // dispositif, deux niveaux sous la racine.
    opened.LocateServed(
      DeclaredSystemId.From("boutique"),
      LocateFindings.ReadFrom(
        new LocateOnTheWire(
          null,
          [
            new ReservedOnTheWire(
              "clients#4417",
              "Deux comptes portent ce nom : celui-ci n'a jamais commandé.",
              [new DesignationOnTheWire("email", "Autre.Jean@example.fr")]),
          ]),
        DeclaredSystemId.From("boutique")),
      Received.AddDays(3));

    await SaveAsync(opened);

    // Les deux sacs sont bien là AVANT : sans cette moitié, un test vert ne prouverait qu'une
    // écriture manquante.
    (await ScalarListAsync($"select value from case_designations where case_id = '{opened.Id.Value}'"))
      .Count.ShouldBe(2);

    (await ScalarListAsync("select value from case_reservation_designations"))
      .ShouldContain("Autre.Jean@example.fr");

    await CloseAsync(opened.Id, ClosingCause.Answered, Received.AddDays(9));

    (await ScalarListAsync($"select value from case_designations where case_id = '{opened.Id.Value}'"))
      .ShouldBeEmpty();

    (await ScalarListAsync($"select subject from case_questions where case_id = '{opened.Id.Value}'"))
      .ShouldBeEmpty();

    // La réserve tombe avec sa localisation, et ses désignations avec elle.
    (await ScalarListAsync("select value from case_reservation_designations"))
      .ShouldNotContain("Autre.Jean@example.fr");

    // La cause et l'instant, eux, sont écrits : c'est ce que le dossier vidé garde de sa propre fin.
    (await ScalarListAsync($"select state || '/' || closing_cause from cases where id = '{opened.Id.Value}'"))
      .ShouldBe([$"{nameof(CaseState.Closed)}/{nameof(ClosingCause.Answered)}"]);

    var reread = await RereadAsync(opened.Id);

    reread.Designations.ShouldBeEmpty();
    reread.ClosedOn.ShouldBe(Received.AddDays(9));

    // ⚠️ Le travail dû n'a pas bougé : la clôture ne propage rien et ne gèle rien, et un « à faire »
    // resté tel quel se lit comme l'oubli qu'il est.
    reread.Claims.Single().Steps.Single().State.ShouldBe(StepState.ToDo);
  }

  /// <summary>
  /// <b>La méthode de vérification survit à la clôture, le détail meurt.</b> La première se compte
  /// et son lecteur est le contrôle ; le second nomme, et rien ne justifie qu'il survive à la
  /// personne dont il parle.
  /// </summary>
  [Fact]
  public async Task KeepsTheVerificationMethodInTheRowAndWipesItsProse()
  {
    var motivated = Case.Open(
      CaseId.Next(),
      IdentityDeclaration.Unverified,
      IdentityMotivation.Of(
        IdentityVerificationMethod.CallbackOnKnownContact,
        "Rappel au numéro connu ; Jean Dupont a confirmé sa date de naissance."),
      [Designation.Of(DesignationKind.Email, "a.clore@example.fr")],
      [DataSubjectRight.Access],
      ClaimOrigin.Named,
      Manifest.Empty,
      ReceptionDate.Declared(Received));

    await SaveAsync(motivated);

    await CloseAsync(motivated.Id, ClosingCause.Abandoned, Received.AddDays(4));

    var written = await ScalarListAsync(
      "select identity_verification_method || '/' || coalesce(identity_motivation_detail, '∅') "
      + $"from cases where id = '{motivated.Id.Value}'");

    written.ShouldBe([$"{nameof(IdentityVerificationMethod.CallbackOnKnownContact)}/∅"]);
  }

  private static Case Open(
    DataSubjectRight[] rights,
    Designation[] designations,
    params DeclaredSystem[] systems)
  {
    return Case.Open(
      CaseId.Next(),
      IdentityDeclaration.ApplicationSession,
      motivation: null,
      designations,
      rights,
      ClaimOrigin.Named,
      Manifest.Of(systems),
      ReceptionDate.Declared(Received));
  }

  private static DeclaredSystem ASystem(string id)
  {
    return DeclaredSystem.Declare(
      DeclaredSystemId.From(id),
      SystemLabel.From(id),
      SystemContents.From("Ce qu'il contient, dans les mots de qui l'a déclaré."),
      [],
      adapterAddress: null,
      Declared);
  }

  private async Task SaveAsync(Case opened)
  {
    await using var dbContext = postgres.NewDbContext();

    dbContext.Cases.Add(opened);

    await dbContext.SaveChangesAsync();
  }

  /// <summary>
  /// Relit le dossier, le clôt, et écrit — <b>dans un seul contexte</b>, comme le fait le
  /// gestionnaire.
  /// </summary>
  /// <remarks>
  /// C'est ce qui rend l'exigence vérifiable : la destruction du nominatif est un <b>effet de
  /// l'écriture de la racine</b>, et non un effacement que le test aurait fait à la main.
  /// </remarks>
  private async Task<Case> CloseAsync(CaseId id, ClosingCause cause, DateTimeOffset closedOn)
  {
    await using var dbContext = postgres.NewDbContext();

    var toClose = await dbContext.Cases.SingleOrDefaultAsync(one => one.Id == id);

    toClose.ShouldNotBeNull();
    toClose.Close(cause, closedOn).ShouldBeTrue();

    await dbContext.SaveChangesAsync();

    return toClose;
  }

  private async Task<Case> RereadAsync(CaseId id)
  {
    // Un contexte neuf : relire depuis celui qui a écrit ne prouverait que le suivi des
    // modifications, jamais l'aller-retour à travers les colonnes.
    await using var dbContext = postgres.NewDbContext();

    var reread = await dbContext.Cases.SingleOrDefaultAsync(one => one.Id == id);

    return reread.ShouldNotBeNull();
  }

  private async Task<List<string>> ScalarListAsync(string sql)
  {
    await using var dbContext = postgres.NewDbContext();

    var connection = dbContext.Database.GetDbConnection();
    await connection.OpenAsync();

    await using DbCommand command = connection.CreateCommand();
    command.CommandText = sql;

    var values = new List<string>();

    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
      values.Add(reader.GetString(0));
    }

    return values;
  }
}
