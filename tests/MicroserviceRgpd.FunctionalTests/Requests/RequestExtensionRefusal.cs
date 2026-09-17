using System.Net;
using System.Text.Json;
using MicroserviceRgpd.Core.Requests;

namespace MicroserviceRgpd.FunctionalTests.Requests;

/// <summary>
/// <b>Le serveur fait foi</b> : une prolongation que <see cref="DataSubjectRequest.Extend"/> refuse,
/// envoyée directement au handler <c>POST /demandes?handler=Extend</c>, reçoit <b>400
/// <c>ValidationProblem</c></b>, ses refus <b>groupés par champ</b>, et ne prolonge rien (ADR-0029).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>400, et non 422.</b> Le partage de l'écran : <b>400</b> dit « ce que tu as écrit ne va
/// pas », <b>409</b> et <b>422</b> disent « la demande n'est pas dans l'état qu'il faut ». Un champ
/// mal saisi est un 400, comme à la création et à la correction.
/// </para>
/// <para>
/// Chaque refus s'écrit <c>clé : message</c> — la clé du corps sous laquelle l'écran placera le
/// message, et le message tel que le serveur l'a écrit une seule fois, dans
/// <see cref="DataSubjectRequestMessages"/>. Les tests les <b>lisent</b>, ils ne les recopient pas.
/// </para>
/// <para>
/// ⚠️ <b>« Rien n'a été prolongé » se relit en base</b>, sur les colonnes de la prolongation : une
/// saisie refusée laisse la demande non prolongée, date limite comprise.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class RequestExtensionRefusal(CustomWebApplicationFactory<Program> factory)
{
  private readonly RequestSurface _surface = new(factory);

  /// <summary>
  /// <b>Un motif absent, blanc ou forgé est refusé sous le champ motif</b> : le choix fermé de
  /// l'écran ne suffit pas — un envoi forgé n'en vient pas, et le domaine revérifie.
  /// </summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("Autre")]
  [InlineData("complexity")]
  [InlineData("Complexité de la demande")]
  public async Task RefusesAGroundThatIsNotOneOfTheTwo(string? ground)
  {
    var refusals = await RefusalsOfAsync(
      RequestSurface.With(RequestSurface.AValidExtension(), DataSubjectRequestField.ExtensionGround, ground));

    refusals.ShouldBe(
      [$"{DataSubjectRequestField.ExtensionGround} : {DataSubjectRequestMessages.ExtensionGroundMissing}"]);
  }

  /// <summary>
  /// <b>Une justification absente, vide ou blanche après élagage est refusée sous son champ</b> :
  /// c'est le texte qui dit le fait concret, et il est obligatoire.
  /// </summary>
  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData(" \t\n ")]
  public async Task RefusesAJustificationThatSaysNothing(string? justification)
  {
    var refusals = await RefusalsOfAsync(RequestSurface.With(
      RequestSurface.AValidExtension(),
      DataSubjectRequestField.ExtensionJustification,
      justification));

    refusals.ShouldBe(
      [$"{DataSubjectRequestField.ExtensionJustification} : {DataSubjectRequestMessages.ExtensionJustificationMissing}"]);
  }

  /// <summary>
  /// <b>Un caractère au-delà du plafond suffit</b> : la justification à deux mille un est refusée, et
  /// le message dit la borne en toutes lettres. Le serveur ne tronque rien.
  /// </summary>
  [Fact]
  public async Task RefusesAJustificationOverItsCeiling()
  {
    var refusals = await RefusalsOfAsync(RequestSurface.With(
      RequestSurface.AValidExtension(),
      DataSubjectRequestField.ExtensionJustification,
      new string('j', ExtensionJustification.MaxLength + 1)));

    refusals.ShouldBe(
      [$"{DataSubjectRequestField.ExtensionJustification} : {DataSubjectRequestMessages.ExtensionJustificationTooLong}"]);
  }

  /// <summary>
  /// <b>Les refus arrivent ensemble, chacun sous son champ</b> : l'<c>Operator</c> corrige tout d'un
  /// coup, sans deviner ce que le second envoi lui opposera.
  /// </summary>
  [Fact]
  public async Task GroupsEveryRefusalUnderItsOwnField()
  {
    var refusals = await RefusalsOfAsync(new Dictionary<string, string>
    {
      [DataSubjectRequestField.ExtensionGround] = "Autre",
      [DataSubjectRequestField.ExtensionJustification] = "   ",
    });

    refusals.ShouldBe(
      [
        $"{DataSubjectRequestField.ExtensionGround} : {DataSubjectRequestMessages.ExtensionGroundMissing}",
        $"{DataSubjectRequestField.ExtensionJustification} : {DataSubjectRequestMessages.ExtensionJustificationMissing}",
      ],
      ignoreOrder: true);
  }

  /// <summary>
  /// <b>Une justification au plafond exact passe</b>, bordures élaguées comprises : la borne est
  /// « au-delà de deux mille », pas « à deux mille ».
  /// </summary>
  [Fact]
  public async Task AcceptsAJustificationAtItsCeiling()
  {
    var (id, message) = await _surface.RecordAsync();

    var response = await _surface.ExtendAsync(id, new Dictionary<string, string>
    {
      [DataSubjectRequestField.ExtensionJustification] = $"  {new string('j', ExtensionJustification.MaxLength)}  ",
    });

    response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    ((string?)(await _surface.RowOfAsync(message))["extension_justification"])
      .ShouldNotBeNull().Length.ShouldBe(ExtensionJustification.MaxLength);
  }

  /// <summary>
  /// Envoie la prolongation sur une demande fraîche, vérifie qu'elle est refusée en <c>400
  /// ValidationProblem</c> <b>sans que rien n'ait été prolongé</b>, et rend ses refus sous la forme
  /// <c>clé : message</c>.
  /// </summary>
  private async Task<string[]> RefusalsOfAsync(Dictionary<string, string> extension)
  {
    var (id, message) = await _surface.RecordAsync(new Dictionary<string, string> { ["receivedOn"] = "2026-01-15" });

    var response = await _surface.ExtendExactlyAsync(id, extension);
    var body = await response.Content.ReadAsStringAsync();

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, body);
    response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

    var row = await _surface.RowOfAsync(message);

    row["extended_at"].ShouldBeNull("Une prolongation refusée a été enregistrée.");
    row["initial_response_deadline"].ShouldBeNull();
    row["extension_ground"].ShouldBeNull();
    row["extension_justification"].ShouldBeNull();
    row["response_deadline"].ShouldBe(new DateOnly(2026, 2, 15), "La date limite a bougé sur une prolongation refusée.");

    using var document = JsonDocument.Parse(body);

    document.RootElement.GetProperty("status").GetInt32().ShouldBe((int)HttpStatusCode.BadRequest);

    return
    [
      .. document.RootElement.GetProperty("errors").EnumerateObject().SelectMany(field =>
        field.Value.EnumerateArray().Select(refusal => $"{field.Name} : {refusal.GetString()}")),
    ];
  }
}
