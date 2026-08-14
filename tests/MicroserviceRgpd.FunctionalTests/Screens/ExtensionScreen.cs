using System.Globalization;
using System.Net;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.EvidenceLog;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Data.Casework;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// La <b>prolongation de l'art. 12.3</b>, exercée par la seule frontière HTTP.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ Le test le plus important de ce fichier est
/// <see cref="RecordsALateExtensionWithoutMovingTheDeadlineItCameTooLateFor"/>. Un service qui
/// déplacerait l'échéance sur un clic tardif blanchirait un dépassement déjà acquis ; un service qui
/// refuserait la déclaration perdrait le fait. Il fait ni l'un ni l'autre : il enregistre un fait
/// laid.
/// </para>
/// <para>
/// <b>Le service n'écrit jamais à la personne concernée</b>, et ces tests le gardent : ce qui entre
/// est une <b>déclaration</b> — un motif et une date d'information — et la charge probatoire de
/// l'art. 12.3 reste à celui qui l'a faite.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ExtensionScreen(CustomWebApplicationFactory<Program> factory)
{
  private readonly OperatorSurface _surface = new(factory);

  /// <summary>
  /// <b>Le test d'acceptation.</b> Déclarée dans le mois, la prolongation porte le dénominateur à
  /// trois mois — à l'écran du dossier comme à celui de la file — et la preuve garde le motif, la
  /// date d'information et le nom du signataire.
  /// </summary>
  [Fact]
  public async Task CarriesTheDeadlineToThreeMonthsWhenDeclaredWithinTheMonth()
  {
    var received = DateTimeOffset.UtcNow.AddDays(-3);
    var opened = await _surface.OpenAsync(ReceptionDate.Declared(received));

    var declaring = await _surface.DeclareExtensionAsync(
      opened,
      "Le prestataire de paie ne rend la main qu'au trimestre.",
      DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
      "Camille Roy");

    declaring.StatusCode.ShouldBe(HttpStatusCode.Found);

    var screen = await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    // L'échéance affichée est celle de trois mois, calculée sur la réception — jamais sur le jour
    // du clic, qui ferait dépendre le délai dû à la personne de l'agenda de l'Operator.
    screen.ShouldContain(received.AddMonths(3).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
    screen.ShouldContain("Prolongée de deux mois");
    screen.ShouldContain("le service n'a écrit à personne");

    // Et la file range la ligne sur la nouvelle échéance, en le disant.
    var queue = await _surface.ReadTextAsync(OperatorSurface.Queue);

    queue.ShouldContain("prolongée (art. 12.3)");

    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var line = await dbContext.Set<EvidenceLogRow>()
      .AsNoTracking()
      .SingleAsync(row => row.CaseId == opened.Value && row.Fact == nameof(EvidenceLogFact.ExtensionDeclared));

    line.Prose.ShouldBe("Le prestataire de paie ne rend la main qu'au trimestre.");
    line.SignatoryName.ShouldBe("Camille Roy");
    line.SignatoryKind.ShouldBe(nameof(SignatoryKind.Operator));

    // ⚠️ DEUX DATES, ET ELLES NE SE CONFONDENT PAS : celle du geste, et celle où l'Operator
    // déclare avoir informé la personne. L'écart entre elles est ce que le contrôle vient lire.
    line.InformedOn.ShouldNotBeNull();
    line.InformedOn!.Value.ShouldBeLessThan(line.OccurredAt);

    // Et aucune colonne ne dit que l'échéance a bougé : c'est un calcul, refait à chaque affichage.
    line.DeclaredDeadline.ShouldBeNull();
  }

  /// <summary>
  /// ⚠️ <b>Déclarée après le mois, la prolongation s'inscrit — et ne déplace rien.</b> Le fait est
  /// gardé au dossier et au <c>EvidenceLog</c> ; le dépassement acquis reste dépassé. Un clic ne blanchit
  /// pas ce qui est déjà advenu, et refuser la déclaration aurait perdu le fait.
  /// </summary>
  [Fact]
  public async Task RecordsALateExtensionWithoutMovingTheDeadlineItCameTooLateFor()
  {
    var received = DateTimeOffset.UtcNow.AddMonths(-2);
    var opened = await _surface.OpenAsync(ReceptionDate.Declared(received));

    var declaring = await _surface.DeclareExtensionAsync(
      opened,
      "Personne n'a vu passer le dossier avant la fin du mois.",
      DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
      "Camille Roy");

    declaring.StatusCode.ShouldBe(HttpStatusCode.Found);

    var screen = await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    // L'échéance n'a pas bougé d'un jour, et l'écran dit pourquoi la prolongation est là sans avoir
    // rien déplacé.
    screen.ShouldContain(received.AddMonths(1).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
    screen.ShouldNotContain(received.AddMonths(3).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
    screen.ShouldContain("Prolongation déclarée hors délai");
    screen.ShouldContain("délai dépassé");

    var queue = await _surface.ReadTextAsync(OperatorSurface.Queue);

    queue.ShouldNotContain("prolongée (art. 12.3)");

    // ⚠️ LE FAIT EST TOUT DE MÊME AU LEDGER, sans mention particulière : la ligne dit ce qui a été
    // déclaré et quand, et c'est au contrôle de refaire le calcul.
    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    (await dbContext.Set<EvidenceLogRow>()
      .AsNoTracking()
      .SingleAsync(row => row.CaseId == opened.Value && row.Fact == nameof(EvidenceLogFact.ExtensionDeclared)))
      .Prose.ShouldBe("Personne n'a vu passer le dossier avant la fin du mois.");
  }

  /// <summary>
  /// <b>Le motif et la date d'information sont tous deux exigés</b>, et chaque refus se dépose sous
  /// le nom de SON champ : l'art. 12.3 met les deux à la charge de qui prolonge, et une prolongation
  /// à demi prouvée ne prouve rien.
  /// </summary>
  [Fact]
  public async Task RefusesToProlongWithoutAMotiveOrWithoutTheDayThePersonWasInformed()
  {
    var opened = await _surface.OpenAsync(ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)));

    var without = await _surface.DeclareExtensionAsync(
      opened,
      string.Empty,
      DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
      "Camille Roy");

    // Pas de redirection : l'écran revient avec son refus.
    without.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await without.Content.ReadAsStringAsync()).ShouldContain("Le motif de la prolongation est absent");

    var undated = await _surface.DeclareExtensionAsync(
      opened,
      "Le prestataire de paie ne rend la main qu'au trimestre.",
      string.Empty,
      "Camille Roy");

    undated.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await undated.Content.ReadAsStringAsync()).ShouldContain("est exigé");

    // Et rien ne s'est posé sur le dossier ni sur la preuve.
    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    (await dbContext.Cases.AsNoTracking().SingleAsync(one => one.Id == opened)).ExtensionDeclaration.ShouldBeNull();

    (await dbContext.Set<EvidenceLogRow>()
      .AsNoTracking()
      .AnyAsync(row => row.CaseId == opened.Value && row.Fact == nameof(EvidenceLogFact.ExtensionDeclared)))
      .ShouldBeFalse();
  }

  /// <summary>
  /// <b>L'art. 12.3 n'ouvre qu'une prolongation, et l'écran cesse de l'offrir une fois déclarée.</b>
  /// Une seconde réécrirait le motif et les dates qu'un humain a signés, et ferait du délai une
  /// chose qu'on repousse à volonté.
  /// </summary>
  [Fact]
  public async Task OffersTheGestureOnceAndKeepsWhatWasDeclared()
  {
    var opened = await _surface.OpenAsync(ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)));

    await _surface.DeclareExtensionAsync(
      opened,
      "Le prestataire de paie ne rend la main qu'au trimestre.",
      DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
      "Camille Roy");

    var screen = await _surface.ReadTextAsync(OperatorSurface.AddressOf(opened));

    screen.ShouldNotContain("Déclarer la prolongation et signer");
    screen.ShouldContain("Le prestataire de paie ne rend la main qu'au trimestre.");
  }
}
