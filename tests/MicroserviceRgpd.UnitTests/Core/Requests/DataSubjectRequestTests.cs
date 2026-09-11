using Ardalis.Result;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UnitTests.Core.Requests;

/// <summary>
/// Ce que <see cref="DataSubjectRequest.Receive"/> accepte et refuse. La fabrique ne lève pas : elle
/// rend la demande, ou <b>toutes</b> les raisons de la refuser, chacune rattachée au champ qu'elle
/// concerne — l'<c>Operator</c> corrige tout d'un coup, pas une erreur après l'autre.
/// </summary>
public class DataSubjectRequestTests
{
  private static readonly DateOnly Today = new(2026, 9, 11);

  private static readonly DateTimeOffset Now = new(2026, 9, 11, 8, 15, 0, TimeSpan.Zero);

  /// <summary>Une saisie complète et valide, que chaque test déforme sur un seul point.</summary>
  private static DataSubjectRequestEntry AValidEntry() => new(
    Origin: Origin.Email,
    ReceivedOn: "2026-09-10",
    LastName: "Dupont",
    FirstName: "Jeanne",
    Email: "jeanne.dupont@exemple.fr",
    IdentityVerified: false,
    Message: "Je souhaite accéder à mes données.",
    Right: "Access");

  private static Result<DataSubjectRequest> Receive(DataSubjectRequestEntry entry) =>
    DataSubjectRequest.Receive(entry, Today, Now);

  /// <summary>Les erreurs d'un refus, champ par champ.</summary>
  private static (string Field, string Message)[] ErrorsOf(Result<DataSubjectRequest> result)
  {
    result.Status.ShouldBe(ResultStatus.Invalid);
    return [.. result.ValidationErrors.Select(error => (error.Identifier, error.ErrorMessage))];
  }

  [Fact]
  public void ReceivesAValidEntryWithEveryValueItCarries()
  {
    var result = DataSubjectRequest.Receive(
      AValidEntry() with { Origin = Origin.Letter, IdentityVerified = true, Right = "Erasure" },
      Today,
      Now);

    result.IsSuccess.ShouldBeTrue();
    var request = result.Value;
    request.Origin.ShouldBe(Origin.Letter);
    request.ReceivedOn.ShouldBe(new DateOnly(2026, 9, 10));
    request.LastName!.Value.Value.ShouldBe("Dupont");
    request.FirstName!.Value.Value.ShouldBe("Jeanne");
    request.Email!.Value.Value.ShouldBe("jeanne.dupont@exemple.fr");
    request.IdentityVerified.ShouldBeTrue();
    request.Message.Value.ShouldBe("Je souhaite accéder à mes données.");
    request.Right.ShouldBe(DataSubjectRight.Erasure);
  }

  /// <summary>
  /// <b>L'auteur est <c>operator</c></b>, tant que le service n'authentifie personne, et l'instant
  /// d'enregistrement est celui de l'horloge, en UTC — distinct de la date de réception déclarée.
  /// </summary>
  [Fact]
  public void IsSignedByTheOperatorAndDatedByTheClock()
  {
    var request = Receive(AValidEntry()).Value;

    request.CreatedBy.ShouldBe("operator");
    request.CreatedAt.ShouldBe(Now);
    request.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);
    request.Id.Value.ShouldNotBe(Guid.Empty);
  }

  /// <summary>
  /// <b>Une demande naît <c>InProgress</c></b> : c'est <see cref="DataSubjectRequest.Receive"/> qui
  /// le fixe, et aucun <c>Gesture</c> ne le fait encore changer.
  /// </summary>
  [Fact]
  public void IsBornInProgress()
  {
    Receive(AValidEntry()).Value.Status.ShouldBe(RequestStatus.InProgress);
  }

  [Fact]
  public void GivesEachReceivedRequestItsOwnIdentity()
  {
    Receive(AValidEntry()).Value.Id.ShouldNotBe(Receive(AValidEntry()).Value.Id);
  }

  // ─── Identification ─────────────────────────────────────────────────────────────────────────

  /// <summary>
  /// <b>Un email, ou un nom et un prénom.</b> Chaque ligne est une saisie acceptée ; les espaces
  /// seuls valent absence.
  /// </summary>
  [Theory]
  [InlineData("jeanne@exemple.fr", null, null)]
  [InlineData("jeanne@exemple.fr", "   ", "  ")]
  [InlineData(null, "Dupont", "Jeanne")]
  [InlineData("  ", "Dupont", "Jeanne")]
  [InlineData("jeanne@exemple.fr", "Dupont", null)]
  [InlineData("jeanne@exemple.fr", null, "Jeanne")]
  [InlineData("jeanne@exemple.fr", "Dupont", "Jeanne")]
  public void AcceptsAnEmailOrALastNameWithAFirstName(string? email, string? lastName, string? firstName)
  {
    Receive(AValidEntry() with { Email = email, LastName = lastName, FirstName = firstName })
      .IsSuccess.ShouldBeTrue();
  }

  /// <summary>
  /// Sans identification, l'erreur est rattachée à <c>email</c> <b>et</b> à chacun des champs nom
  /// et prénom qui manquent — pas à celui qui est renseigné.
  /// </summary>
  [Theory]
  [InlineData(null, null, null, new[] { "email", "lastName", "firstName" })]
  [InlineData("", "  ", "\t", new[] { "email", "lastName", "firstName" })]
  [InlineData(null, "Dupont", null, new[] { "email", "firstName" })]
  [InlineData("   ", "Dupont", "  ", new[] { "email", "firstName" })]
  [InlineData(null, null, "Jeanne", new[] { "email", "lastName" })]
  public void PlacesTheIdentificationErrorOnEveryMissingField(
    string? email, string? lastName, string? firstName, string[] fields)
  {
    var errors = ErrorsOf(Receive(AValidEntry() with { Email = email, LastName = lastName, FirstName = firstName }));

    errors.ShouldBe(
      [.. fields.Select(field => (field, "Renseignez un email, ou un nom et un prénom."))],
      ignoreOrder: true);
  }

  /// <summary>Un email renseigné mais mal formé identifie quand même : seule sa forme est refusée.</summary>
  [Fact]
  public void RefusesOnlyTheFormOfAMalformedEmailThatIdentifies()
  {
    ErrorsOf(Receive(AValidEntry() with { Email = "pas-un-email", LastName = null, FirstName = null }))
      .ShouldBe([("email", "L'adresse email n'est pas valide.")]);
  }

  // ─── Date de réception ──────────────────────────────────────────────────────────────────────

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  public void RequiresTheReceptionDate(string? receivedOn)
  {
    ErrorsOf(Receive(AValidEntry() with { ReceivedOn = receivedOn }))
      .ShouldBe([("receivedOn", "La date de réception est obligatoire.")]);
  }

  /// <summary>La date arrive au format ISO du fil (<c>yyyy-MM-dd</c>) ; tout le reste est mal formé.</summary>
  [Theory]
  [InlineData("11/09/2026")]
  [InlineData("2026-9-1")]
  [InlineData("2026-02-30")]
  [InlineData("demain")]
  public void RefusesAMalformedReceptionDate(string receivedOn)
  {
    ErrorsOf(Receive(AValidEntry() with { ReceivedOn = receivedOn }))
      .ShouldBe([("receivedOn", "La date de réception doit être au format jj/mm/aaaa.")]);
  }

  [Fact]
  public void AcceptsTodayAndHasNoLowerBound()
  {
    Receive(AValidEntry() with { ReceivedOn = "2026-09-11" }).IsSuccess.ShouldBeTrue();
    Receive(AValidEntry() with { ReceivedOn = "1900-01-01" }).IsSuccess.ShouldBeTrue();
    Receive(AValidEntry() with { ReceivedOn = " 2026-09-11 " }).Value.ReceivedOn.ShouldBe(Today);
  }

  [Fact]
  public void RefusesAReceptionDateInTheFuture()
  {
    ErrorsOf(Receive(AValidEntry() with { ReceivedOn = "2026-09-12" }))
      .ShouldBe([("receivedOn", "La date de réception ne peut pas être dans le futur.")]);
  }

  /// <summary>
  /// <b>Le futur se juge à Paris.</b> À 23 h 30 UTC la veille, Paris est déjà le lendemain — l'hiver
  /// comme l'été : ce lendemain est « aujourd'hui », et le jour d'après est le futur.
  /// </summary>
  [Theory]
  [InlineData("2026-01-14T23:30:00Z", "2026-01-15", "2026-01-16")]
  [InlineData("2026-07-14T23:30:00Z", "2026-07-15", "2026-07-16")]
  [InlineData("2026-07-14T22:00:00Z", "2026-07-15", "2026-07-16")]
  [InlineData("2026-01-14T22:30:00Z", "2026-01-14", "2026-01-15")]
  public void JudgesTheFutureAtParisAroundMidnight(string instant, string todayInParis, string tomorrowInParis)
  {
    var clock = new AClockStuckAt(DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture));
    var today = ParisCalendar.Today(clock);

    DataSubjectRequest.Receive(AValidEntry() with { ReceivedOn = todayInParis }, today, clock.GetUtcNow())
      .IsSuccess.ShouldBeTrue();
    ErrorsOf(DataSubjectRequest.Receive(AValidEntry() with { ReceivedOn = tomorrowInParis }, today, clock.GetUtcNow()))
      .ShouldBe([("receivedOn", "La date de réception ne peut pas être dans le futur.")]);
  }

  // ─── Trim, casse, longueurs ─────────────────────────────────────────────────────────────────

  /// <summary>Les bordures sont retirées ; la casse de l'email est gardée telle qu'elle a été saisie.</summary>
  [Fact]
  public void TrimsEveryFieldAndKeepsTheCaseOfTheEmail()
  {
    var request = Receive(AValidEntry() with
    {
      LastName = "  Dupont ",
      FirstName = "\tJeanne\n",
      Email = "  Jeanne.Dupont@Exemple.FR  ",
      Message = "  Bonjour.  ",
      Right = " Access ",
    }).Value;

    request.LastName!.Value.Value.ShouldBe("Dupont");
    request.FirstName!.Value.Value.ShouldBe("Jeanne");
    request.Email!.Value.Value.ShouldBe("Jeanne.Dupont@Exemple.FR");
    request.Message.Value.ShouldBe("Bonjour.");
    request.Right.ShouldBe(DataSubjectRight.Access);
  }

  /// <summary>Un nom, un prénom ou un email fait d'espaces seuls est absent, pas une valeur vide.</summary>
  [Fact]
  public void ReadsAWhitespaceOnlyFieldAsAbsent()
  {
    var request = Receive(AValidEntry() with { LastName = "   ", FirstName = " " }).Value;

    request.LastName.ShouldBeNull();
    request.FirstName.ShouldBeNull();
  }

  /// <summary>Les limites se comptent en unités UTF-16 — <c>.Length</c> —, au caractère près.</summary>
  [Fact]
  public void BoundsTheLastNameAtOneHundredCharacters()
  {
    Receive(AValidEntry() with { LastName = new string('a', 100) }).IsSuccess.ShouldBeTrue();
    Receive(AValidEntry() with { LastName = " " + new string('a', 100) + " " }).IsSuccess.ShouldBeTrue();
    ErrorsOf(Receive(AValidEntry() with { LastName = new string('a', 101) }))
      .ShouldBe([("lastName", "Le nom ne peut pas dépasser 100 caractères.")]);
  }

  [Fact]
  public void BoundsTheFirstNameAtOneHundredCharacters()
  {
    Receive(AValidEntry() with { FirstName = new string('a', 100) }).IsSuccess.ShouldBeTrue();
    ErrorsOf(Receive(AValidEntry() with { FirstName = new string('a', 101) }))
      .ShouldBe([("firstName", "Le prénom ne peut pas dépasser 100 caractères.")]);
  }

  /// <summary>Un caractère hors du plan de base compte pour deux unités UTF-16, comme dans le navigateur.</summary>
  [Fact]
  public void CountsInUtf16CodeUnits()
  {
    var ninetyNineAndAnEmoji = new string('a', 99) + "😀";

    ninetyNineAndAnEmoji.Length.ShouldBe(101);
    ErrorsOf(Receive(AValidEntry() with { LastName = ninetyNineAndAnEmoji }))
      .ShouldBe([("lastName", "Le nom ne peut pas dépasser 100 caractères.")]);
  }

  [Fact]
  public void BoundsTheEmailAtTwoHundredFiftyFourCharacters()
  {
    var domain = "@exemple.fr";
    var atTheLimit = new string('a', 64) + "." + new string('b', 254 - 64 - 1 - domain.Length) + domain;
    var beyond = "c" + atTheLimit;

    atTheLimit.Length.ShouldBe(254);
    Receive(AValidEntry() with { Email = atTheLimit }).IsSuccess.ShouldBeTrue();
    ErrorsOf(Receive(AValidEntry() with { Email = beyond }))
      .ShouldBe([("email", "L'adresse email n'est pas valide.")]);
  }

  [Fact]
  public void BoundsTheMessageAtTenThousandCharacters()
  {
    Receive(AValidEntry() with { Message = new string('m', 10_000) }).IsSuccess.ShouldBeTrue();
    ErrorsOf(Receive(AValidEntry() with { Message = new string('m', 10_001) }))
      .ShouldBe([("message", "Le message ne peut pas dépasser 10 000 caractères.")]);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(" \n\t ")]
  public void RequiresTheMessage(string? message)
  {
    ErrorsOf(Receive(AValidEntry() with { Message = message }))
      .ShouldBe([("message", "Le message est obligatoire.")]);
  }

  // ─── Email ──────────────────────────────────────────────────────────────────────────────────

  /// <summary>La forme est celle de <c>&lt;input type=email&gt;</c> : la regex WHATWG, et elle seule.</summary>
  [Theory]
  [InlineData("jeanne@exemple.fr")]
  [InlineData("jeanne@localhost")]
  [InlineData("j.e+rgpd!#$%&'*/=?^_`{|}~-@exemple-fr.co.uk")]
  [InlineData("JEANNE@EXEMPLE.FR")]
  public void AcceptsWhatTheBrowserAccepts(string email)
  {
    Receive(AValidEntry() with { Email = email }).IsSuccess.ShouldBeTrue();
  }

  [Theory]
  [InlineData("jeanne")]
  [InlineData("jeanne@")]
  [InlineData("@exemple.fr")]
  [InlineData("jeanne@@exemple.fr")]
  [InlineData("jeanne dupont@exemple.fr")]
  [InlineData("jeanne@-exemple.fr")]
  [InlineData("jeanne@exemple-.fr")]
  [InlineData("jeanne@exemple..fr")]
  [InlineData("jeanne@exemple.fr.")]
  [InlineData("jéanne@exemple.fr")]
  [InlineData("jeanne@exemple.fr\nx")]
  public void RefusesWhatTheBrowserRefuses(string email)
  {
    ErrorsOf(Receive(AValidEntry() with { Email = email }))
      .ShouldBe([("email", "L'adresse email n'est pas valide.")]);
  }

  // ─── Droit invoqué ──────────────────────────────────────────────────────────────────────────

  [Theory]
  [InlineData("Access")]
  [InlineData("Rectification")]
  [InlineData("Erasure")]
  [InlineData("Restriction")]
  [InlineData("Portability")]
  [InlineData("Objection")]
  public void AcceptsEachOfTheSixRights(string right)
  {
    Receive(AValidEntry() with { Right = right }).Value.Right.Name.ShouldBe(right);
  }

  /// <summary>
  /// ⚠️ <b><see cref="DataSubjectRight.OutOfScope"/> n'est pas un droit qu'on invoque</b> : c'est un
  /// verdict de <c>Qualification</c>. Il est refusé avec le même message que l'absence de droit.
  /// </summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("  ")]
  [InlineData("OutOfScope")]
  [InlineData("access")]
  [InlineData("0")]
  [InlineData("Dereferencing")]
  public void RefusesAMissingOrUninvocableRight(string? right)
  {
    ErrorsOf(Receive(AValidEntry() with { Right = right }))
      .ShouldBe([("right", "Sélectionnez un droit RGPD.")]);
  }

  // ─── Toutes les erreurs à la fois ───────────────────────────────────────────────────────────

  /// <summary><b>La fabrique ne s'arrête pas à la première erreur</b> : une saisie vide les reçoit toutes.</summary>
  [Fact]
  public void CollectsEveryErrorAtOnce()
  {
    var entry = new DataSubjectRequestEntry(
      Origin: Origin.Email,
      ReceivedOn: "2027-01-01",
      LastName: new string('a', 101),
      FirstName: null,
      Email: null,
      IdentityVerified: false,
      Message: new string('m', 10_001),
      Right: "OutOfScope");

    ErrorsOf(Receive(entry)).ShouldBe(
      [
        ("receivedOn", "La date de réception ne peut pas être dans le futur."),
        ("lastName", "Le nom ne peut pas dépasser 100 caractères."),
        ("email", "Renseignez un email, ou un nom et un prénom."),
        ("firstName", "Renseignez un email, ou un nom et un prénom."),
        ("message", "Le message ne peut pas dépasser 10 000 caractères."),
        ("right", "Sélectionnez un droit RGPD."),
      ],
      ignoreOrder: true);
  }

  /// <summary>Les dix messages, écrits une seule fois, sont exposés pour que la page les fournisse au navigateur.</summary>
  [Fact]
  public void ExposesTheTenMessagesOnce()
  {
    DataSubjectRequestMessages.All.Count.ShouldBe(10);
    DataSubjectRequestMessages.All.Values.Distinct().Count().ShouldBe(10);
  }
}
