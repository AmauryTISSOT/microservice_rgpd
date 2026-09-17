using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.FunctionalTests.Layout;
using MicroserviceRgpd.Web.Pages.Requests;
using NSwag.Generation;

namespace MicroserviceRgpd.FunctionalTests.Requests;

/// <summary>
/// <b>Modifier une demande</b>, exercé par la <b>seule frontière HTTP</b> : le handler
/// <c>POST /demandes?handler=Modify</c> que le script de la modale appellera, frappé ici directement,
/// jeton anti-rejeu compris — exactement ce que fera le navigateur.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La persistance se vérifie ici, et nulle part ailleurs</b> : cette frontière écrit dans un
/// vrai PostgreSQL, et la ligne relue <b>en SQL brut</b> — sans repasser par le modèle qui l'a
/// écrite — est ce que le ticket promet. Il n'y a pas de test d'intégration dédié à cette table.
/// </para>
/// <para>
/// Chaque demande porte un <b>message unique</b>, par lequel elle se retrouve en base : la base est
/// partagée par toute la collection, et ne se vide pas entre deux tests. ⚠️ <b>Les corrections
/// gardent donc le message intact</b> : c'est la clé de retrouvage.
/// </para>
/// </remarks>
[Collection(RequestsWebCollection.Name)]
public class RequestModification(CustomWebApplicationFactory<Program> factory)
{
  private readonly RequestSurface _surface = new(factory);

  /// <summary>
  /// <b>Une correction valide est enregistrée</b> : le serveur répond 200, et la demande relue en base
  /// porte les nouvelles valeurs, la date limite recalculée depuis la nouvelle date de réception, et
  /// l'empreinte de modification signée <c>operator</c> et datée par l'horloge du service.
  /// </summary>
  /// <remarks>
  /// ⚠️ L'horloge est <b>avancée</b> avant l'envoi : un instant de modification lu sur la machine
  /// plutôt que sur l'horloge du service tomberait hors de la fenêtre attendue.
  /// </remarks>
  [Fact]
  public async Task AnswersOkAndStoresTheCorrectionStampedByTheServiceClock()
  {
    var ahead = TimeSpan.FromDays(40);
    var (id, message) = await _surface.RecordAsync();

    factory.Clock.Advance(ahead);

    try
    {
      var before = DateTimeOffset.UtcNow + ahead;

      var response = await _surface.ModifyAsync(id, Correction(message, new Dictionary<string, string>
      {
        ["origin"] = "Letter",
        ["receivedOn"] = "2026-01-31",
        ["lastName"] = "  Durand  ",
        ["firstName"] = "\tPaul ",
        ["email"] = "",
        ["identityVerified"] = "true",
        ["right"] = "Erasure",
      }));

      var after = DateTimeOffset.UtcNow + ahead;

      response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

      var row = await _surface.RowOfAsync(message);

      row["origin"].ShouldBe("Letter");
      row["received_on"].ShouldBe(new DateOnly(2026, 1, 31));
      row["last_name"].ShouldBe("Durand");
      row["first_name"].ShouldBe("Paul");
      row["email"].ShouldBeNull();
      row["identity_verified"].ShouldBe(true);
      row["data_subject_right"].ShouldBe("Erasure");

      // La date limite se rejoue depuis la nouvelle date de réception : un 31 janvier donne le 28
      // février, dernier jour du mois suivant (ADR-0021).
      row["response_deadline"].ShouldBe(new DateOnly(2026, 2, 28));

      // ⚠️ Une demande qui n'a pas été prolongée n'a qu'une date limite : la correction n'en invente
      // pas une seconde.
      row["initial_response_deadline"].ShouldBeNull();

      row["modified_by"].ShouldBe("operator");

      var modifiedAt = row["modified_at"].ShouldBeOfType<DateTime>();
      modifiedAt.Kind.ShouldBe(DateTimeKind.Utc);
      new DateTimeOffset(modifiedAt).ShouldBeInRange(before, after);
    }
    finally
    {
      factory.Clock.Reset();
    }
  }

  /// <summary>
  /// <b>Le 200 porte la ligne mise à jour</b>, en HTML, que le script remet dans le tableau : ses
  /// libellés — dont la date limite recalculée et son signalement —, et l'identifiant de la demande.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est la ligne même que le tableau rend</b>, au caractère près : une seule vue partielle
  /// rend les deux, et deux gabarits finiraient par diverger.
  /// </remarks>
  [Fact]
  public async Task AnswersOkWithTheRowTheTableRenders()
  {
    var email = $"{Guid.NewGuid():N}@example.org";
    var (id, message) = await _surface.RecordAsync();

    var response = await _surface.ModifyAsync(id, Correction(message, new Dictionary<string, string>
    {
      ["receivedOn"] = "2026-01-31",
      ["lastName"] = "Durand",
      ["firstName"] = "Paul",
      ["email"] = email,
      ["right"] = "Erasure",
    }));

    var body = await response.Content.ReadAsStringAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
    response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("text/html");

    var row = body.Trim();
    Regex.Matches(row, @"<tr\b").Count.ShouldBe(1, "Le 200 ne porte pas une ligne, une seule.");

    var cells = Regex.Matches(row, @"<td\b[^>]*>(.*?)</td>", RegexOptions.Singleline)
      .Select(cell => LayoutSurface.TextIn(cell.Groups[1].Value))
      .ToArray();

    cells[..7].ShouldBe([email, "Durand", "Paul", "31/01/2026", "28/02/2026 En retard", "Non", "Droit à l'effacement"]);
    cells[8..10].ShouldBe(["Opérateur", "En cours"]);
    Regex.Match(row, @"data-request-id=""([^""]*)""").Groups[1].Value.ShouldBe(id.ToString());
    row.ShouldContain(@"data-received-on=""2026-01-31""", customMessage: "La ligne ne porte pas sa date de réception ISO, qui la place.");

    row.ShouldBe(await _surface.BoardRowWithAsync(email), "La ligne du 200 n'est pas celle que le tableau rend.");
  }

  /// <summary>
  /// ⚠️ <b>Un renvoi à l'identique réussit comme un autre</b> — 200 et la ligne — <b>et ne laisse
  /// aucune empreinte nouvelle</b> : une modification qui ne change rien n'a pas eu lieu. Le succès
  /// n'a qu'une forme, et la ligne rendue reste correcte, puisqu'elle n'a pas changé.
  /// </summary>
  [Fact]
  public async Task AnswersOkWithTheRowAndLeavesTheStampUntouchedWhenNothingChanges()
  {
    var (id, message) = await _surface.RecordAsync();
    var correction = Correction(message, new Dictionary<string, string> { ["lastName"] = "Durand" });

    (await _surface.ModifyAsync(id, correction)).StatusCode.ShouldBe(HttpStatusCode.OK);

    var stamped = await _surface.RowOfAsync(message);
    stamped["modified_at"].ShouldNotBeNull();

    var response = await _surface.ModifyAsync(id, correction);
    var body = await response.Content.ReadAsStringAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.OK, body);
    Regex.Match(body, @"data-request-id=""([^""]*)""").Groups[1].Value.ShouldBe(id.ToString());

    var row = await _surface.RowOfAsync(message);

    row["modified_at"].ShouldBe(stamped["modified_at"], "Un renvoi à l'identique a redaté l'empreinte.");
    row["modified_by"].ShouldBe(stamped["modified_by"]);
  }

  /// <summary>
  /// <b>Corriger la date de réception d'une demande prolongée refait les deux dates limites</b> —
  /// <c>initial_response_deadline</c> et <c>response_deadline</c> — depuis la date de réception
  /// corrigée, l'écart de deux mois conservé, et <b>n'efface pas la prolongation</b> : motif,
  /// justification et <c>extended_at</c> sont relus intacts.
  /// </summary>
  [Fact]
  public async Task RecomputesBothDeadlinesOfAnExtendedRequestWithoutErasingTheExtension()
  {
    var (id, message) = await AnExtendedRequestAsync();
    var extended = await _surface.RowOfAsync(message);

    var correctedReceivedOn = ParisCalendar.Today(factory.Clock).AddMonths(-3);

    var response = await _surface.ModifyAsync(id, Correction(message, new Dictionary<string, string>
    {
      ["receivedOn"] = correctedReceivedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    }));

    response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

    var row = await _surface.RowOfAsync(message);

    row["received_on"].ShouldBe(correctedReceivedOn);
    row["initial_response_deadline"].ShouldBe(correctedReceivedOn.AddMonths(1));
    row["response_deadline"].ShouldBe(correctedReceivedOn.AddMonths(1).AddMonths(2));

    row["extension_ground"].ShouldBe(extended["extension_ground"]);
    row["extension_justification"].ShouldBe(extended["extension_justification"]);
    row["extended_at"].ShouldBe(extended["extended_at"], "Une correction a redaté la prolongation.");
  }

  /// <summary>
  /// ⚠️ <b>Un renvoi à l'identique ne touche aucune des deux dates limites</b> : la règle ne vaut que
  /// sur le chemin d'écriture. La ligne entière est relue avant et après, et ne bouge pas d'une
  /// colonne.
  /// </summary>
  [Fact]
  public async Task TouchesNeitherDeadlineOfAnExtendedRequestWhenNothingChanges()
  {
    var (id, message) = await AnExtendedRequestAsync();
    var before = await _surface.RowOfAsync(message);

    var response = await _surface.ModifyAsync(id, Correction(message));

    response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    (await _surface.RowOfAsync(message)).ShouldBe(before, "Un renvoi à l'identique a rejoué les dates limites.");
  }

  /// <summary>
  /// <b>Une correction qui place rétroactivement la demande en retard est acceptée</b> — la
  /// prolongation fût-elle ainsi reportée hors de sa fenêtre : c'est une vérité à afficher, pas un
  /// état à empêcher. La ligne rendue le dit — « Prolongée » et « En retard » —, et la demande ne se
  /// prolonge toujours pas une seconde fois.
  /// </summary>
  [Fact]
  public async Task AcceptsACorrectionThatPutsAnExtendedRequestRetroactivelyLate()
  {
    var (id, message) = await AnExtendedRequestAsync();

    var correctedReceivedOn = ParisCalendar.Today(factory.Clock).AddYears(-1);

    var response = await _surface.ModifyAsync(id, Correction(message, new Dictionary<string, string>
    {
      ["receivedOn"] = correctedReceivedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    }));

    var body = await response.Content.ReadAsStringAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.OK, body);

    var row = await _surface.RowOfAsync(message);
    var inForce = row["response_deadline"].ShouldBeOfType<DateOnly>();

    inForce.ShouldBe(correctedReceivedOn.AddMonths(1).AddMonths(2));
    inForce.ShouldBeLessThan(ParisCalendar.Today(factory.Clock), "La correction n'a pas mis la demande en retard.");
    row["extended_at"].ShouldNotBeNull();

    var deadlineCell = Regex.Match(
      body,
      @"<td\b[^>]*data-field=""responseDeadline""[^>]*>(?<cell>.*?)</td>",
      RegexOptions.Singleline).Groups["cell"].Value;

    LayoutSurface.TextIn(deadlineCell).ShouldBe(
      $"{inForce.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} {DeadlineSignal.Overdue.FrenchLabel} {RequestRow.Extended}");

    using var summary = JsonDocument.Parse(
      await (await _surface.ExtensionOfAsync(id)).Content.ReadAsStringAsync());

    summary.RootElement.GetProperty("block").GetString().ShouldBe(ExtensionBlock.AlreadyExtended.FrenchLabel);

    // ⚠️ L'écran le dit, et le serveur le tient : une seconde prolongation est refusée en 409.
    (await _surface.ExtendAsync(id)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
  }

  /// <summary>
  /// <b>Le serveur fait foi</b> : une saisie fautive est refusée en <b>400 <c>ValidationProblem</c></b>,
  /// indexée par les clés du corps, avec les mots mêmes de la création — et rien n'est enregistré.
  /// </summary>
  [Theory]
  [InlineData("receivedOn", "15/01/2026", DataSubjectRequestMessages.ReceivedOnMalformed)]
  [InlineData("email", "jeanne.martin@", DataSubjectRequestMessages.EmailInvalid)]
  [InlineData("message", "   ", DataSubjectRequestMessages.MessageMissing)]
  [InlineData("right", "OutOfScope", DataSubjectRequestMessages.RightMissing)]
  public async Task RefusesEachFieldThatBreaksItsRuleWithoutStoringAnything(string key, string value, string refusal)
  {
    var (id, message) = await _surface.RecordAsync();
    var before = await _surface.RowOfAsync(message);

    var response = await _surface.ModifyAsync(id, RequestSurface.With(Correction(message), key, value));
    var body = await response.Content.ReadAsStringAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
    response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

    using var document = JsonDocument.Parse(body);

    document.RootElement.GetProperty("status").GetInt32().ShouldBe((int)HttpStatusCode.BadRequest);
    document.RootElement.GetProperty("errors").EnumerateObject()
      .SelectMany(field => field.Value.EnumerateArray().Select(text => $"{field.Name} : {text.GetString()}"))
      .ShouldBe([$"{key} : {refusal}"]);

    (await _surface.RowOfAsync(message)).ShouldBe(before, "Une correction refusée a été enregistrée.");
  }

  /// <summary>
  /// <b>Une saisie qui cumule les fautes les reçoit toutes, en une seule réponse</b> — chacune sous sa
  /// clé : le serveur ne s'arrête pas à la première.
  /// </summary>
  [Fact]
  public async Task ReturnsEveryRefusalOfACorrectionThatBreaksSeveralRules()
  {
    var (id, message) = await _surface.RecordAsync();

    var response = await _surface.ModifyAsync(id, Correction(message, new Dictionary<string, string>
    {
      ["receivedOn"] = "31/12/2026",
      ["lastName"] = new string('a', 101),
      ["email"] = "",
      ["right"] = "",
    }));

    var body = await response.Content.ReadAsStringAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);

    using var document = JsonDocument.Parse(body);

    document.RootElement.GetProperty("errors").EnumerateObject()
      .SelectMany(field => field.Value.EnumerateArray().Select(text => $"{field.Name} : {text.GetString()}"))
      .ShouldBe(
        [
          $"receivedOn : {DataSubjectRequestMessages.ReceivedOnMalformed}",
          $"lastName : {DataSubjectRequestMessages.LastNameTooLong}",
          $"firstName : {DataSubjectRequestMessages.IdentificationMissing}",
          $"email : {DataSubjectRequestMessages.IdentificationMissing}",
          $"right : {DataSubjectRequestMessages.RightMissing}",
        ],
        ignoreOrder: true);
  }

  /// <summary>
  /// <b>Une demande qui n'existe plus rend 404</b> — un second onglet l'a supprimée pendant que la
  /// modale était ouverte.
  /// </summary>
  [Fact]
  public async Task AnswersNotFoundForARequestThatDoesNotExist()
  {
    var (id, message) = await _surface.RecordAsync();
    (await _surface.DeleteAsync(id)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

    var response = await _surface.ModifyAsync(id, Correction(message));

    response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    (await _surface.CountOfAsync(message)).ShouldBe(0, "Une correction a ressuscité une demande supprimée.");
  }

  /// <summary>
  /// <b>Une demande close rend 409</b>, Terminée comme Annulée — le statut posé en base, puisqu'aucun
  /// geste ne le fait encore changer —, et rien n'est enregistré.
  /// </summary>
  [Theory]
  [InlineData("Completed")]
  [InlineData("Cancelled")]
  public async Task AnswersConflictForAClosedRequestWithoutStoringAnything(string status)
  {
    var (id, message) = await _surface.RecordAsync();
    await _surface.SetStatusAsync(id, status);

    var before = await _surface.RowOfAsync(message);

    var response = await _surface.ModifyAsync(id, Correction(message, new Dictionary<string, string>
    {
      ["lastName"] = "Durand",
    }));

    response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
    (await _surface.RowOfAsync(message)).ShouldBe(before, "Une demande close a été modifiée.");
  }

  /// <summary>
  /// ⚠️ <b>Le conflit passe avant la validation</b> : une saisie fautive sur une demande close rend
  /// 409, jamais 400. Lister des erreurs sous les champs d'un formulaire qui ne pourra jamais
  /// enregistrer n'apprend rien à l'<c>Operator</c>.
  /// </summary>
  [Fact]
  public async Task AnswersConflictForAClosedRequestEvenOnAFaultyEntry()
  {
    var (id, message) = await _surface.RecordAsync();
    await _surface.SetStatusAsync(id, "Completed");

    var response = await _surface.ModifyAsync(
      id,
      RequestSurface.With(Correction(message), "message", "   "));

    response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
  }

  /// <summary>
  /// <b>Un identifiant qui n'en est pas un est refusé</b> — 400, et non 404 : l'écran ne poste que
  /// ceux qu'il a rendus.
  /// </summary>
  [Theory]
  [InlineData("")]
  [InlineData("pas-un-guid")]
  [InlineData("00000000-0000-0000-0000-000000000000")]
  public async Task RefusesAnIdThatIsNotOne(string id)
  {
    var before = await _surface.CountAllAsync();

    var response = await _surface.ModifyAsync(id, RequestSurface.AValidRequest());

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

    // ⚠️ La saisie est valide : sans l'identifiant, rien ne l'empêcherait d'être enregistrée comme
    // une demande neuve.
    (await _surface.CountAllAsync()).ShouldBe(before, "Une correction sous un identifiant forgé a été enregistrée.");
  }

  /// <summary>
  /// ⚠️ <b>Sans jeton anti-rejeu, rien n'est modifié</b> : une page tierce qui ferait poster le
  /// navigateur de l'<c>Operator</c> n'atteint pas le handler.
  /// </summary>
  [Fact]
  public async Task ModifiesNothingWithoutTheAntiforgeryToken()
  {
    var (id, message) = await _surface.RecordAsync();
    var before = await _surface.RowOfAsync(message);

    var response = await _surface.ModifyWithoutTokenAsync(id, Correction(message, new Dictionary<string, string>
    {
      ["lastName"] = "Durand",
    }));

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    (await _surface.RowOfAsync(message)).ShouldBe(before, "Une correction sans jeton anti-rejeu a été enregistrée.");
  }

  /// <summary>
  /// ⚠️ <b>La modification n'est pas une API</b> : le document Swagger ne publie ni sa route, ni son
  /// contrat. Le handler est celui d'une page, appelé par son propre script.
  /// </summary>
  [Fact]
  public async Task TheApiDocumentDoesNotPublishTheModification()
  {
    using var scope = factory.Services.CreateScope();
    var document = await scope.ServiceProvider.GetRequiredService<IOpenApiDocumentGenerator>().GenerateAsync("v1");
    var published = document.ToJson();

    document.Paths.Keys.ShouldContain("/qualifications", "Le document ne publie plus rien : les assertions suivantes seraient vides.");
    published.ShouldNotContain("handler=Modify", Case.Insensitive, "Le document Swagger publie la modification d'une demande.");
    published.ShouldNotContain("ModifyDataSubjectRequest", Case.Insensitive, "Le document Swagger publie la modification d'une demande.");
  }

  /// <summary>
  /// Une correction complète et valide, <b>le message de la demande gardé intact</b> — c'est par lui
  /// qu'elle se retrouve en base —, les valeurs de <paramref name="fields"/> posées par-dessus.
  /// </summary>
  private static Dictionary<string, string> Correction(
    string message,
    IReadOnlyDictionary<string, string>? fields = null)
  {
    var correction = RequestSurface.AValidRequest();
    correction["message"] = message;

    foreach (var (key, value) in fields ?? new Dictionary<string, string>())
    {
      correction[key] = value;
    }

    return correction;
  }

  /// <summary>
  /// Une demande <b>prolongée</b> par la frontière HTTP : sa date limite est d'abord posée dans la
  /// fenêtre — un mois après aujourd'hui —, faute de quoi la prolongation serait refusée.
  /// </summary>
  private async Task<(Guid Id, string Message)> AnExtendedRequestAsync()
  {
    var (id, message) = await _surface.RecordAsync();

    await _surface.SetResponseDeadlineAsync(id, ParisCalendar.Today(factory.Clock).AddMonths(1));

    (await _surface.ExtendAsync(id)).StatusCode.ShouldBe(HttpStatusCode.OK);

    return (id, message);
  }
}
