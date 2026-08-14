using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Casework.EvidenceLog;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Data.Casework;
using Microsoft.EntityFrameworkCore;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// La <b>section propre</b> des <c>EvidenceLog</c> échus, sur l'écran de la file, et le geste
/// irréversible qu'elle porte.
/// </summary>
/// <remarks>
/// <para>
/// Ce que ces tests gardent est la <b>séparation</b> : un <c>EvidenceLog</c> échu n'a ni personne, ni
/// droit, ni délai — son dossier est clos depuis cinq ans —, et son bouton, définitif et sans trace,
/// ne doit jamais voisiner les lignes de dossiers. C'est aussi la seule échéance du dispositif qui
/// fasse <b>naître</b> une ligne.
/// </para>
/// <para>
/// ⚠️ <b>Rien ne purge derrière cet écran.</b> La destruction n'a lieu que sous le clic d'un humain :
/// un service qui purgerait tout seul afficherait la même section vide qu'un service arrêté.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ExpiredEvidenceLogSection(CustomWebApplicationFactory<Program> factory)
{
  private readonly OperatorSurface _surface = new(factory);

  /// <summary>
  /// <b>La section reste affichée, et vide</b>, les années où rien n'est échu. Une section qui
  /// disparaîtrait sortirait du regard, et le jour où quelque chose y tomberait, personne ne
  /// l'attendrait plus. Elle se dit en mots, jamais par un zéro.
  /// </summary>
  [Fact]
  public async Task StaysOnTheScreenAndEmptyInTheYearsWhenNothingHasExpired()
  {
    var screen = await _surface.ReadTextAsync(OperatorSurface.Queue);

    screen.ShouldContain("À détruire — conservation échue");
    screen.ShouldContain("cinq ans à compter de la clôture");

    // Rien n'y purge, et l'écran le dit à qui vient l'y chercher.
    screen.ShouldContain("Aucun processus ne purge");
  }

  /// <summary>
  /// <b>Un <c>EvidenceLog</c> échu fait NAÎTRE une ligne</b> — la seule échéance du dispositif à le
  /// faire —, et elle ne porte ni personne, ni droit, ni délai : deux dates et un dossier clos.
  /// </summary>
  [Fact]
  public async Task MakesARowAppearForAProofThatIsNoLongerDue()
  {
    var expired = await _surface.CloseLongAgoAsync(DateTimeOffset.UtcNow.AddYears(-6));

    var screen = await _surface.ReadTextAsync(OperatorSurface.Queue);

    screen.ShouldContain(expired.Value.ToString());
    screen.ShouldContain("échue depuis le");

    // La ligne ne nomme personne : le dossier qu'elle désigne n'a plus une désignation depuis sa
    // clôture, et rien de ce qui nommait n'a survécu pour reparaître ici.
    screen.ShouldNotContain("jean.dupont@example.fr");
  }

  /// <summary>
  /// <b>Le bouton est loin de ceux des dossiers.</b> Le tableau des <c>Case</c> n'offre aucun geste
  /// — pas un formulaire, pas un bouton, pas une case — et la seule action de l'écran vit dans la
  /// section d'après, sous son propre titre : un mauvais clic définitif et inconstatable ne doit pas
  /// être à portée de main.
  /// </summary>
  [Fact]
  public async Task KeepsTheIrreversibleButtonOutOfReachOfTheCaseRows()
  {
    var opened = await _surface.OpenAsync(
      Core.Casework.ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)));

    await _surface.CloseLongAgoAsync(DateTimeOffset.UtcNow.AddYears(-6));

    var screen = await _surface.ReadAsync(OperatorSurface.Queue);

    var frontier = screen.IndexOf("À détruire", StringComparison.Ordinal);

    frontier.ShouldBeGreaterThan(-1, "La section des EvidenceLog échus a disparu de l'écran.");

    var cases = screen[..frontier];

    // ⚠️ AUCUN GESTE SUR UN DOSSIER, comme depuis toujours : chaque ligne mène au dossier, et c'est
    // là que l'Operator agit. Une action depuis la liste ferait signer quelqu'un sans qu'il ait
    // ouvert ce qu'il signe.
    cases.ShouldNotContain("<form");
    cases.ShouldNotContain("<button");
    cases.ShouldNotContain("type=\"checkbox\"");
    cases.ShouldContain($"/dossiers/{opened.Value}");

    // Et tous les formulaires de l'écran sont, sans exception, ceux de la section d'après.
    Regex.Matches(screen, "<form").Count
      .ShouldBe(Regex.Matches(screen[frontier..], "<form").Count);
  }

  /// <summary>
  /// <b>Le geste détruit toute la preuve, ne laisse aucune trace de lui-même, et la ligne s'en
  /// va.</b> C'est ainsi — faute de pouvoir se consigner — que la destruction se voit avoir été
  /// faite.
  /// </summary>
  [Fact]
  public async Task DestroysTheWholeProofAndLeavesNoTraceOfHavingDoneIt()
  {
    var expired = await _surface.CloseLongAgoAsync(DateTimeOffset.UtcNow.AddYears(-6));

    var destroying = await _surface.DestroyEvidenceLogAsync(expired);

    destroying.StatusCode.ShouldBe(HttpStatusCode.Found);

    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // ⚠️ PLUS UNE LIGNE, ET AUCUNE ÉCRITE À LA PLACE. On ne prouvera jamais avoir purgé.
    (await dbContext.Set<EvidenceLogRow>().AsNoTracking().AnyAsync(row => row.CaseId == expired.Value))
      .ShouldBeFalse();

    // La ligne de l'écran est partie avec la preuve, et le dossier clos est resté : il ne nomme
    // plus personne depuis cinq ans, et ce n'est pas lui que la conservation visait.
    (await _surface.ReadTextAsync(OperatorSurface.Queue)).ShouldNotContain(expired.Value.ToString());

    (await dbContext.Cases.AsNoTracking().AnyAsync(one => one.Id == expired)).ShouldBeTrue();
  }

  /// <summary>
  /// <b>La parade du geste irréversible est la case à cocher, et elle tient.</b> Sans elle, rien
  /// n'est détruit — et l'écran dit pourquoi plutôt que d'échouer en silence.
  /// </summary>
  [Fact]
  public async Task DestroysNothingWhenNoOneDeliberatelyConfirmed()
  {
    var expired = await _surface.CloseLongAgoAsync(DateTimeOffset.UtcNow.AddYears(-6));

    var destroying = await _surface.DestroyEvidenceLogAsync(expired, confirmed: false);

    // Pas de redirection : l'écran revient avec son refus, sous le nom de la case.
    destroying.StatusCode.ShouldBe(HttpStatusCode.OK);
    (await destroying.Content.ReadAsStringAsync()).ShouldContain("Cochez la case");

    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    (await dbContext.Set<EvidenceLogRow>().AsNoTracking().AnyAsync(row => row.CaseId == expired.Value))
      .ShouldBeTrue();
  }

  /// <summary>
  /// ⚠️ <b>Une preuve encore due ne se détruit pas depuis un écran vieux d'une heure.</b> L'échéance
  /// est éprouvée au moment du geste : ce qui est irréversible ne se décide pas sur une page
  /// affichée il y a longtemps, ni sur un envoi forgé.
  /// </summary>
  [Fact]
  public async Task RefusesToDestroyAProofThatIsStillDueHoweverTheClickArrived()
  {
    // Une preuve échue est là, et c'est son formulaire que l'écran offre. Le clic, lui, en désigne
    // une autre — celle d'un dossier clos l'an dernier : c'est très exactement la forme d'un envoi
    // forgé, ou d'une page restée ouverte pendant que la première était détruite.
    await _surface.CloseLongAgoAsync(DateTimeOffset.UtcNow.AddYears(-6));

    var recent = await _surface.CloseLongAgoAsync(DateTimeOffset.UtcNow.AddYears(-1));

    var destroying = await _surface.DestroyEvidenceLogAsync(recent);

    // La file rechargée dit d'elle-même ce qui reste : c'est la seule chose vraie qu'on puisse
    // afficher d'un geste qui ne se consigne pas.
    destroying.StatusCode.ShouldBe(HttpStatusCode.Found);

    using var scope = factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    (await dbContext.Set<EvidenceLogRow>()
      .AsNoTracking()
      .AnyAsync(row => row.CaseId == recent.Value && row.Fact == nameof(EvidenceLogFact.CaseClosed)))
      .ShouldBeTrue();
  }

  /// <summary>
  /// <b>Aucun nom n'est demandé pour détruire</b>, seul geste du dispositif dans ce cas : il
  /// n'existe plus une ligne où l'écrire. Réclamer une signature pour ne l'écrire nulle part aurait
  /// été la façade d'une preuve, et l'écran dit franchement qu'il n'en gardera rien.
  /// </summary>
  [Fact]
  public async Task AsksForNoSignatureItCouldNeverKeep()
  {
    await _surface.CloseLongAgoAsync(DateTimeOffset.UtcNow.AddYears(-6));

    var screen = await _surface.ReadAsync(OperatorSurface.Queue);

    screen.ShouldNotContain("Destruction.SignedBy");

    (await _surface.ReadTextAsync(OperatorSurface.Queue))
      .ShouldContain("votre nom ne vous est pas demandé");
  }
}
