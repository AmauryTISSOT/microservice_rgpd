using System.Globalization;
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

  /// <summary>L'instant d'une correction, postérieur à l'enregistrement : l'empreinte ne se confond pas avec lui.</summary>
  private static readonly DateTimeOffset Later = new(2026, 9, 11, 17, 40, 0, TimeSpan.Zero);

  /// <summary>L'instant d'une seconde correction, pour distinguer une empreinte gardée d'une empreinte refaite.</summary>
  private static readonly DateTimeOffset MuchLater = new(2026, 9, 12, 9, 5, 0, TimeSpan.Zero);

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

  /// <summary>
  /// <b>Une demande reçue ne porte aucune empreinte de modification</b> : <c>ModifiedBy</c> et
  /// <c>ModifiedAt</c> sont nuls tant qu'aucune modification effective n'a eu lieu. ⚠️ Le nul est la
  /// vérité, pas un trou à combler — une empreinte posée à la réception dirait qu'on a corrigé une
  /// demande qui vient de naître.
  /// </summary>
  [Fact]
  public void CarriesNoModificationStampWhenReceived()
  {
    var request = Receive(AValidEntry()).Value;

    request.ModifiedBy.ShouldBeNull();
    request.ModifiedAt.ShouldBeNull();
  }

  // ─── Date limite de réponse ────────────────────────────────────────────────────────────────

  /// <summary>
  /// <b>La date limite est la date de réception plus un mois</b>, ramenée au dernier jour du mois
  /// suivant quand ce jour n'y existe pas (RG1, ADR-0021). Chaque demande est enregistrée le jour
  /// même de sa réception, pour que les dates encore à venir restent admises.
  /// </summary>
  [Theory]
  [InlineData("2026-03-31", "2026-04-30")]
  [InlineData("2027-01-31", "2027-02-28")]
  [InlineData("2028-01-31", "2028-02-29")]
  [InlineData("2026-09-10", "2026-10-10")]
  public void SetsTheResponseDeadlineOneMonthAfterReception(string receivedOn, string responseDeadline)
  {
    var today = DateOnly.Parse(receivedOn, CultureInfo.InvariantCulture);

    var request = DataSubjectRequest.Receive(AValidEntry() with { ReceivedOn = receivedOn }, today, Now).Value;

    request.ResponseDeadline.ShouldBe(DateOnly.Parse(responseDeadline, CultureInfo.InvariantCulture));
  }

  /// <summary>
  /// ⚠️ <b>La date limite part de la date de réception, jamais de l'instant d'enregistrement</b> :
  /// une demande reçue le 31 mars et transcrite en septembre devait sa réponse au 30 avril.
  /// </summary>
  [Fact]
  public void CountsTheResponseDeadlineFromReceptionNotFromRecording()
  {
    var request = Receive(AValidEntry() with { ReceivedOn = "2026-03-31" }).Value;

    request.ResponseDeadline.ShouldBe(new DateOnly(2026, 4, 30));
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

  // ─── Modifier une demande ───────────────────────────────────────────────────────────────────

  /// <summary>
  /// <b>Les huit champs prennent les nouvelles valeurs, et l'empreinte est posée.</b> Modifier est un
  /// <c>Gesture</c> : il laisse une trace datée et signée, distincte de celle de l'enregistrement,
  /// qui ne bouge pas.
  /// </summary>
  [Fact]
  public void TakesTheEightNewValuesAndStampsTheModification()
  {
    var request = Receive(AValidEntry()).Value;

    var result = request.Modify(
      new DataSubjectRequestEntry(
        Origin: Origin.Letter,
        ReceivedOn: "2026-09-11",
        LastName: "Durand",
        FirstName: "Paul",
        Email: "paul.durand@exemple.fr",
        IdentityVerified: true,
        Message: "Je souhaite faire effacer mes données.",
        Right: "Erasure"),
      Today,
      Later);

    result.IsSuccess.ShouldBeTrue();
    request.Origin.ShouldBe(Origin.Letter);
    request.ReceivedOn.ShouldBe(new DateOnly(2026, 9, 11));
    request.LastName!.Value.Value.ShouldBe("Durand");
    request.FirstName!.Value.Value.ShouldBe("Paul");
    request.Email!.Value.Value.ShouldBe("paul.durand@exemple.fr");
    request.IdentityVerified.ShouldBeTrue();
    request.Message.Value.ShouldBe("Je souhaite faire effacer mes données.");
    request.Right.ShouldBe(DataSubjectRight.Erasure);

    request.ModifiedBy.ShouldBe("operator");
    request.ModifiedAt.ShouldBe(Later);
    request.ModifiedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
    request.CreatedBy.ShouldBe("operator");
    request.CreatedAt.ShouldBe(Now);
  }

  /// <summary>
  /// <b>La date limite suit la date de réception corrigée</b> : la même règle qu'à la réception, et
  /// ses trois bascules de fin de mois (ADR-0021). L'ADR-0021 avait annoncé cette conséquence sans la
  /// traiter ; c'est ici qu'elle est honorée.
  /// </summary>
  [Theory]
  [InlineData("2026-03-31", "2026-04-30")]
  [InlineData("2027-01-31", "2027-02-28")]
  [InlineData("2028-01-31", "2028-02-29")]
  public void RecomputesTheResponseDeadlineFromTheCorrectedReceptionDate(string receivedOn, string responseDeadline)
  {
    var request = Receive(AValidEntry() with { ReceivedOn = "2026-03-10" }).Value;

    request.ResponseDeadline.ShouldBe(new DateOnly(2026, 4, 10));

    // Chaque correction se pose le jour même de la réception corrigée, pour que les dates encore à
    // venir en 2026 restent admises.
    var todayInParis = DateOnly.Parse(receivedOn, CultureInfo.InvariantCulture);

    request.Modify(AValidEntry() with { ReceivedOn = receivedOn }, todayInParis, Later).IsSuccess.ShouldBeTrue();

    request.ResponseDeadline.ShouldBe(DateOnly.Parse(responseDeadline, CultureInfo.InvariantCulture));
  }

  /// <summary>
  /// <b>Une saisie fautive revient en refus groupé</b>, chacune rattachée à son champ et avec les mots
  /// mêmes de la réception — l'<c>Operator</c> n'a pas deux vocabulaires de refus à apprendre — et la
  /// demande n'est pas touchée.
  /// </summary>
  [Fact]
  public void RefusesAnInvalidEntryWithTheWordsOfTheReceptionAndLeavesTheRequestUntouched()
  {
    var request = Receive(AValidEntry()).Value;

    var result = request.Modify(
      AValidEntry() with
      {
        ReceivedOn = "2026-09-12",
        LastName = new string('a', 101),
        Email = null,
        FirstName = null,
        Message = null,
        Right = "OutOfScope",
      },
      Today,
      Later);

    result.Status.ShouldBe(ResultStatus.Invalid);
    result.ValidationErrors.Select(error => (error.Identifier, error.ErrorMessage)).ShouldBe(
      [
        ("receivedOn", "La date de réception ne peut pas être dans le futur."),
        ("lastName", "Le nom ne peut pas dépasser 100 caractères."),
        ("email", "Renseignez un email, ou un nom et un prénom."),
        ("firstName", "Renseignez un email, ou un nom et un prénom."),
        ("message", "Le message est obligatoire."),
        ("right", "Sélectionnez un droit RGPD."),
      ],
      ignoreOrder: true);

    ShouldStillHoldTheValidEntry(request);
    request.ModifiedBy.ShouldBeNull();
    request.ModifiedAt.ShouldBeNull();
  }

  /// <summary>
  /// <b>Une demande close est inaltérable</b>, Terminée comme Annulée : le refus est un conflit, et
  /// non un refus de saisie.
  /// </summary>
  [Theory]
  [InlineData("Completed")]
  [InlineData("Cancelled")]
  public void RefusesToModifyAClosedRequest(string status)
  {
    var request = AClosedRequest(status);

    var result = request.Modify(AValidEntry() with { LastName = "Durand" }, Today, Later);

    result.Status.ShouldBe(ResultStatus.Conflict);
    ShouldStillHoldTheValidEntry(request);
    request.ModifiedBy.ShouldBeNull();
    request.ModifiedAt.ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Le conflit passe avant la validation.</b> Lister des erreurs de saisie sous les champs d'un
  /// formulaire qui ne pourra jamais enregistrer n'apprend rien à l'<c>Operator</c> : une saisie
  /// fautive sur une demande close rend un conflit, jamais un refus de saisie.
  /// </summary>
  [Fact]
  public void RefusesAClosedRequestBeforeEvenLookingAtTheEntry()
  {
    var request = AClosedRequest("Completed");

    var result = request.Modify(AValidEntry() with { Message = null, Right = null }, Today, Later);

    result.Status.ShouldBe(ResultStatus.Conflict);
    result.ValidationErrors.ShouldBeEmpty();
  }

  /// <summary>
  /// <b>Une modification qui ne change aucune valeur n'a pas eu lieu</b> : elle réussit et ne laisse
  /// <b>aucune</b> empreinte. Les valeurs se comparent une fois rognées — deux espaces en fin de nom
  /// ne sont pas un changement.
  /// </summary>
  [Fact]
  public void LeavesNoStampWhenNothingChangesOnceTrimmed()
  {
    var request = Receive(AValidEntry()).Value;

    var result = request.Modify(
      AValidEntry() with
      {
        LastName = "  Dupont ",
        FirstName = "\tJeanne\n",
        Email = " jeanne.dupont@exemple.fr ",
        Message = "  Je souhaite accéder à mes données.  ",
        Right = " Access ",
        ReceivedOn = " 2026-09-10 ",
      },
      Today,
      Later);

    result.IsSuccess.ShouldBeTrue();
    request.ModifiedBy.ShouldBeNull();
    request.ModifiedAt.ShouldBeNull();
    request.ResponseDeadline.ShouldBe(new DateOnly(2026, 10, 10));
  }

  /// <summary>
  /// ⚠️ <b>L'empreinte d'un non-événement reste celle du dernier vrai changement</b> : elle ne se
  /// rafraîchit pas, et ne s'efface pas non plus.
  /// </summary>
  [Fact]
  public void KeepsThePreviousStampWhenAModificationChangesNothing()
  {
    var request = Receive(AValidEntry()).Value;
    request.Modify(AValidEntry() with { LastName = "Durand" }, Today, Later).IsSuccess.ShouldBeTrue();

    request.Modify(AValidEntry() with { LastName = "Durand" }, Today, MuchLater).IsSuccess.ShouldBeTrue();

    request.ModifiedAt.ShouldBe(Later);
  }

  /// <summary>
  /// <b>Une demande En cours passe à Terminée</b> : le système hôte a appliqué le droit invoqué
  /// (ADR-0026). Le passage est un état, pas une trace : aucune empreinte n'est posée.
  /// </summary>
  [Fact]
  public void CompletesARequestInProgress()
  {
    var request = Receive(AValidEntry()).Value;

    var result = request.Complete();

    result.IsSuccess.ShouldBeTrue();
    request.Status.ShouldBe(RequestStatus.Completed);
    ShouldStillHoldTheValidEntry(request);
    request.ModifiedBy.ShouldBeNull();
    request.ModifiedAt.ShouldBeNull();
  }

  /// <summary>
  /// <b>Une demande close ne se termine pas</b>, Terminée comme Annulée : le refus est un conflit, et
  /// le statut ne bouge pas — une Annulée ne devient pas Terminée.
  /// </summary>
  [Theory]
  [InlineData("Completed")]
  [InlineData("Cancelled")]
  public void RefusesToCompleteAClosedRequest(string status)
  {
    var request = AClosedRequest(status);

    var result = request.Complete();

    result.Status.ShouldBe(ResultStatus.Conflict);
    request.Status.ShouldBe(RequestStatus.FromName(status));
  }

  /// <summary>
  /// <b>Une demande Terminée ne s'exécute plus</b> : le motif est « Demande close », quoi qu'en dise
  /// le Paramétrage.
  /// </summary>
  [Fact]
  public void IsNoLongerExecutableOnceCompleted()
  {
    var request = Receive(AValidEntry() with { IdentityVerified = true }).Value;
    var endpoint = MicroserviceRgpd.Core.Configuration.EndpointUrl.From("https://brocanto.example.fr/rgpd");

    request.ExecutionBlockFacing(endpoint).ShouldBeNull();

    request.Complete();

    request.ExecutionBlockFacing(endpoint).ShouldBe(ExecutionBlock.Closed);
  }

  /// <summary>La saisie valide de départ, telle que la demande la tient encore après un refus.</summary>
  private static void ShouldStillHoldTheValidEntry(DataSubjectRequest request)
  {
    request.Origin.ShouldBe(Origin.Email);
    request.ReceivedOn.ShouldBe(new DateOnly(2026, 9, 10));
    request.ResponseDeadline.ShouldBe(new DateOnly(2026, 10, 10));
    request.LastName!.Value.Value.ShouldBe("Dupont");
    request.FirstName!.Value.Value.ShouldBe("Jeanne");
    request.Email!.Value.Value.ShouldBe("jeanne.dupont@exemple.fr");
    request.IdentityVerified.ShouldBeFalse();
    request.Message.Value.ShouldBe("Je souhaite accéder à mes données.");
    request.Right.ShouldBe(DataSubjectRight.Access);
  }

  /// <summary>
  /// Une demande close. ⚠️ <b>Le statut se pose par réflexion</b> parce qu'aucun <c>Gesture</c> ne sait
  /// encore clore une demande : les tests qui ont besoin d'une demande close la fabriquent, ici comme
  /// en base, par-dessus le domaine plutôt que par un chemin que le service n'offre pas.
  /// </summary>
  private static DataSubjectRequest AClosedRequest(string status)
  {
    var request = Receive(AValidEntry()).Value;

    typeof(DataSubjectRequest)
      .GetProperty(nameof(DataSubjectRequest.Status))!
      .SetValue(request, RequestStatus.FromName(status));

    return request;
  }
}
