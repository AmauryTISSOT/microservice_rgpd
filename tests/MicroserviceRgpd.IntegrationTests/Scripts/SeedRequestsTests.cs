using System.Globalization;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace MicroserviceRgpd.IntegrationTests.Scripts;

/// <summary>
/// <b><c>scripts/seed-requests.sql</c> plante cent demandes fictives dans le Tableau des demandes</b>,
/// et seulement là où il a le droit de le faire. Le script est joué par <c>psql</c>, comme le lance
/// <c>scripts/seed-requests.sh</c>, contre une base posée par les migrations du dépôt.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est ce test qui tient le script en phase avec le schéma.</b> Le SQL écrit directement
/// dans la table, sans passer par le domaine : une colonne renommée ou un nom de vocabulaire qui
/// bouge le casserait sans qu'aucune compilation ne proteste. Chaque demande plantée est donc
/// relue <b>par EF Core</b>, objets valeurs et vocabulaires fermés compris.
/// </para>
/// <para>
/// <b>Les dates sont relatives au jour du lancement.</b> Le script accepte un <c>aujourdhui</c> et un
/// <c>maintenant</c> imposés : c'est ce qui permet d'éprouver les fins de mois, où « un mois avant
/// l'échéance » n'a pas toujours de réponse exacte.
/// </para>
/// </remarks>
public class SeedRequestsTests(SeedRequestsTests.SeededDatabase database)
  : IClassFixture<SeedRequestsTests.SeededDatabase>
{
  private const string TheReservedBlockStart = "5eed0000-0000-7000-8000-000000000000";
  private const string TheReservedBlockEnd = "5eed0000-0000-7000-8000-ffffffffffff";

  private static readonly Guid AHandwrittenRequest = new("019b0000-0000-7000-8000-000000000001");

  /// <summary>Relancé, le script ramène au même état : cent demandes, pas deux cents.</summary>
  [Fact]
  public async Task PlantsAHundredRequestsInTheReservedBlockHoweverOftenItRuns()
  {
    await database.SeedAsync();
    await database.SeedAsync();

    (await database.PlantedIdsAsync()).Count.ShouldBe(100);
  }

  /// <summary>
  /// ⚠️ <b>Une demande saisie à la main survit au seed comme au reset</b> : le script ne connaît que
  /// sa plage réservée.
  /// </summary>
  [Fact]
  public async Task NeverTouchesARequestOutsideTheReservedBlock()
  {
    await database.WriteAHandwrittenRequestAsync(AHandwrittenRequest);

    await database.SeedAsync();
    await database.ResetAsync();

    await using var dbContext = database.NewDbContext();
    var survivor = await dbContext.DataSubjectRequests
      .SingleOrDefaultAsync(request => request.Id == DataSubjectRequestId.From(AHandwrittenRequest));

    survivor.ShouldNotBeNull();
  }

  /// <summary>Le reset retire toutes les demandes plantées, sans en replanter.</summary>
  [Fact]
  public async Task ResetRemovesEveryPlantedRequest()
  {
    await database.SeedAsync();

    await database.ResetAsync();

    (await database.PlantedIdsAsync()).ShouldBeEmpty();
  }

  /// <summary>
  /// Chaque demande plantée se relit <b>par le domaine</b>, telle que le service l'aurait enregistrée :
  /// par l'opérateur, jamais modifiée, et avec un droit du périmètre.
  /// </summary>
  [Fact]
  public async Task EveryPlantedRequestReadsBackThroughTheDomain()
  {
    var requests = await database.SeedAndReadAsync();

    requests.Count.ShouldBe(100);
    requests.ShouldAllBe(request => request.CreatedBy == "operator");
    requests.ShouldAllBe(request => request.ModifiedBy == null && request.ModifiedAt == null);
    requests.ShouldNotContain(request => request.Right == DataSubjectRight.OutOfScope);
  }

  /// <summary>70 en cours, 20 terminées, 10 annulées.</summary>
  [Fact]
  public async Task SpreadsTheStatusesLikeADeskAtWork()
  {
    var requests = await database.SeedAndReadAsync();

    requests.Count(request => request.Status == RequestStatus.InProgress).ShouldBe(70);
    requests.Count(request => request.Status == RequestStatus.Completed).ShouldBe(20);
    requests.Count(request => request.Status == RequestStatus.Cancelled).ShouldBe(10);
  }

  /// <summary>Les six droits du périmètre, l'accès et l'effacement en tête, comme au guichet.</summary>
  [Fact]
  public async Task InvokesEveryRightWithAccessAndErasureAhead()
  {
    var requests = await database.SeedAndReadAsync();

    var byRight = requests.GroupBy(request => request.Right).ToDictionary(g => g.Key, g => g.Count());

    byRight.Keys.ShouldBe(
      [
        DataSubjectRight.Access, DataSubjectRight.Rectification, DataSubjectRight.Erasure,
        DataSubjectRight.Restriction, DataSubjectRight.Portability, DataSubjectRight.Objection,
      ],
      ignoreOrder: true);

    var others = byRight
      .Where(pair => pair.Key != DataSubjectRight.Access && pair.Key != DataSubjectRight.Erasure)
      .Max(pair => pair.Value);
    byRight[DataSubjectRight.Access].ShouldBeGreaterThan(others);
    byRight[DataSubjectRight.Erasure].ShouldBeGreaterThan(others);
  }

  /// <summary>
  /// 70 % d'emails, 30 % de courriers ; 60 personnes identifiées par tout, 25 par leur seul email,
  /// 15 par leur seul nom ; l'identité vérifiée pour environ la moitié.
  /// </summary>
  [Fact]
  public async Task CoversEveryOriginAndEveryWayOfBeingIdentified()
  {
    var requests = await database.SeedAndReadAsync();

    requests.Count(request => request.Origin == Origin.Email).ShouldBe(70);
    requests.Count(request => request.Origin == Origin.Letter).ShouldBe(30);

    requests.Count(request => request.Email != null && request.LastName != null && request.FirstName != null)
      .ShouldBe(60);
    requests.Count(request => request.Email != null && request.LastName == null && request.FirstName == null)
      .ShouldBe(25);
    requests.Count(request => request.Email == null && request.LastName != null && request.FirstName != null)
      .ShouldBe(15);

    requests.Count(request => request.IdentityVerified).ShouldBeInRange(40, 60);
  }

  /// <summary>
  /// ⚠️ <b>Une demande qui arrive par email porte un email</b> : l'opérateur l'a forcément reçu
  /// quelque part.
  /// </summary>
  [Fact]
  public async Task GivesAnEmailToEveryRequestReceivedByEmail()
  {
    var requests = await database.SeedAndReadAsync();

    requests.Where(request => request.Origin == Origin.Email).ShouldAllBe(request => request.Email != null);
  }

  /// <summary>
  /// Aucune adresse réelle : les emails ne vivent que sur <c>example.com</c>, réservé à l'exemple
  /// (RFC 2606).
  /// </summary>
  [Fact]
  public async Task WritesOnlyEmailsOnAReservedDomain()
  {
    var requests = await database.SeedAndReadAsync();

    requests.Where(request => request.Email != null)
      .ShouldAllBe(request => request.Email!.Value.Value.EndsWith("@example.com"));
  }

  /// <summary>
  /// La recherche du tableau ignore accents et casse : des noms accentués sont là pour l'éprouver,
  /// et un message très long pour éprouver la fiche.
  /// </summary>
  [Fact]
  public async Task CarriesAccentedNamesAndAVeryLongMessage()
  {
    var requests = await database.SeedAndReadAsync();

    string[] accents = ["é", "è", "ê", "ë", "ï", "ô", "ç", "É"];
    requests.Count(request =>
        accents.Any(accent =>
          (request.LastName?.Value ?? "").Contains(accent) || (request.FirstName?.Value ?? "").Contains(accent)))
      .ShouldBeGreaterThanOrEqualTo(10);

    requests.Max(request => request.Message.Value.Length).ShouldBeGreaterThanOrEqualTo(4_000);
  }

  /// <summary>
  /// Les dates tiennent les invariants du domaine, quel que soit le jour : reçue au plus tard
  /// aujourd'hui, date limite à un mois de la réception, créée après la réception et jamais dans
  /// le futur.
  /// </summary>
  [Theory]
  [MemberData(nameof(Days))]
  public async Task KeepsTheDatesOfTheDomainWhateverTheDay(string today)
  {
    var day = DateOnly.Parse(today, CultureInfo.InvariantCulture);
    var now = SeededDatabase.AfternoonInParis(day);

    var requests = await database.SeedAndReadAsync(day);

    requests.ShouldAllBe(request => request.ReceivedOn <= day);
    requests.ShouldAllBe(request => request.ReceivedOn >= day.AddMonths(-6));
    requests.ShouldAllBe(request => request.ResponseDeadline == request.ReceivedOn.AddMonths(1));
    requests.ShouldAllBe(request => ParisCalendar.DateOf(request.CreatedAt) >= request.ReceivedOn);
    requests.ShouldAllBe(request => request.CreatedAt <= now);
  }

  /// <summary>
  /// Parmi les 70 demandes en cours, 10 en retard et 10 à échéance proche, quel que soit le jour —
  /// fins de mois et 29 février compris.
  /// </summary>
  [Theory]
  [MemberData(nameof(Days))]
  public async Task RaisesTenOverdueAndTenDueSoonWhateverTheDay(string today)
  {
    var day = DateOnly.Parse(today, CultureInfo.InvariantCulture);

    var requests = await database.SeedAndReadAsync(day);

    var signals = requests
      .Where(request => request.Status == RequestStatus.InProgress)
      .Select(request => DeadlineSignal.Of(request.Status, request.ResponseDeadline, day))
      .ToList();

    signals.Count(signal => signal == DeadlineSignal.Overdue).ShouldBe(10);
    signals.Count(signal => signal == DeadlineSignal.DueSoon).ShouldBe(10);
  }

  /// <summary>
  /// Un jour ordinaire, les deux bornes de l'échéance proche sont plantées pile : une date limite
  /// aujourd'hui, une autre dans sept jours.
  /// </summary>
  [Fact]
  public async Task PlantsBothEdgesOfTheDueSoonWindowOnAnOrdinaryDay()
  {
    var day = new DateOnly(2027, 3, 15);

    var requests = await database.SeedAndReadAsync(day);

    var deadlines = requests
      .Where(request => request.Status == RequestStatus.InProgress)
      .Select(request => request.ResponseDeadline)
      .ToList();

    deadlines.ShouldContain(day);
    deadlines.ShouldContain(day.AddDays(DeadlineSignal.DueSoonWindowInDays));
  }

  /// <summary>Sans jour imposé, le script prend le jour de Paris et l'instant de la base.</summary>
  [Fact]
  public async Task DatesFromTodayInParisWhenNoDayIsImposed()
  {
    var before = DateTimeOffset.UtcNow.AddSeconds(-5);

    var requests = await database.SeedAndReadAsync();

    var today = ParisCalendar.DateOf(DateTimeOffset.UtcNow);
    requests.Max(request => request.ReceivedOn).ShouldBeInRange(today.AddDays(-1), today);
    requests.ShouldAllBe(request => request.CreatedAt <= DateTimeOffset.UtcNow.AddSeconds(5));
    requests.Max(request => request.CreatedAt).ShouldBeGreaterThan(before.AddDays(-2));
  }

  public static TheoryData<string> Days() =>
    ["2027-03-15", "2027-03-30", "2027-03-31", "2028-02-29", "2026-12-31", "2027-01-01", "2027-05-31"];

  /// <summary>
  /// Un PostgreSQL à lui seul : les autres classes comptent les lignes de
  /// <c>data_subject_requests</c>, et cent demandes plantées leur mentiraient.
  /// </summary>
  public sealed class SeededDatabase : IAsyncLifetime
  {
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();

    private readonly string _script = File.ReadAllText(
      Path.Combine(AppContext.BaseDirectory, "Scripts", "seed-requests.sql"));

    public async Task InitializeAsync()
    {
      await _container.StartAsync();

      await using var dbContext = NewDbContext();
      await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public static DateTimeOffset AfternoonInParis(DateOnly day)
    {
      var local = day.ToDateTime(new TimeOnly(15, 0));
      return new DateTimeOffset(local, ParisCalendar.TimeZone.GetUtcOffset(local));
    }

    /// <summary>
    /// Joue le script comme le wrapper. Un jour imposé l'est à 15 h, heure de Paris ; sans jour, le
    /// script prend les siens.
    /// </summary>
    public Task SeedAsync(DateOnly? today = null)
    {
      if (today is not { } day)
      {
        return RunAsync([]);
      }

      return RunAsync(
      [
        $"\\set aujourdhui '{day:yyyy-MM-dd}'",
        $"\\set maintenant '{AfternoonInParis(day):yyyy-MM-dd HH:mm:sszzz}'",
      ]);
    }

    public Task ResetAsync() => RunAsync(["\\set reset"]);

    public async Task<List<DataSubjectRequest>> SeedAndReadAsync(DateOnly? today = null)
    {
      await SeedAsync(today);

      await using var dbContext = NewDbContext();
      var planted = (await PlantedIdsAsync()).Select(DataSubjectRequestId.From).ToList();

      return await dbContext.DataSubjectRequests
        .Where(request => planted.Contains(request.Id))
        .ToListAsync();
    }

    public async Task<List<Guid>> PlantedIdsAsync()
    {
      await using var dbContext = NewDbContext();

      return await dbContext.Database.SqlQueryRaw<Guid>(
        $"""
        SELECT id AS "Value" FROM data_subject_requests
        WHERE id BETWEEN '{TheReservedBlockStart}' AND '{TheReservedBlockEnd}'
        """).ToListAsync();
    }

    public async Task WriteAHandwrittenRequestAsync(Guid id)
    {
      await using var dbContext = NewDbContext();

      await dbContext.Database.ExecuteSqlAsync(
        $"""
        INSERT INTO data_subject_requests
          (id, origin, received_on, response_deadline, last_name, first_name, email, identity_verified,
           message, data_subject_right, status, created_by, created_at)
        VALUES
          ({id}, 'Email', DATE '2026-09-01', DATE '2026-10-01', NULL, NULL, 'jeanne@example.com', false,
           'Je souhaite accéder à mes données.', 'Access', 'InProgress', 'operator',
           TIMESTAMPTZ '2026-09-01 08:00:00+00')
        ON CONFLICT (id) DO NOTHING;
        """);
    }

    public AppDbContext NewDbContext() =>
      new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_container.GetConnectionString()).Options);

    private async Task RunAsync(IEnumerable<string> variables)
    {
      var result = await _container.ExecScriptAsync(string.Join('\n', [.. variables, _script]));

      result.ExitCode.ShouldBe(0, result.Stderr);
      result.Stderr.ShouldNotContain("ERROR", Case.Insensitive);
    }
  }
}
