using System.Net;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// Le tableau des demandes RGPD, exercé par sa <b>seule frontière HTTP</b>.
/// </summary>
/// <remarks>
/// <para>
/// Ce que ces tests gardent n'est pas la mise en page : c'est ce que le tableau des demandes RGPD
/// <b>refuse d'offrir</b> — aucun total, aucun taux, aucun « 0 dossier en retard », aucun geste
/// depuis la liste — et le fait qu'une échéance <b>trie</b> des lignes déjà présentes sans jamais
/// en faire naître.
/// </para>
/// <para>
/// ⚠️ Le test le plus important de ce fichier n'est pas celui du chemin heureux : c'est
/// <see cref="ShowsNoCountNoRateAndNoCountOfAbsencesAnywhere"/>. Un tableau de bord offrirait un
/// chiffre rassurant à regarder à la place du travail, et « 0 dossier en retard » est exactement la
/// phrase qu'un service qui a cessé de tourner afficherait.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class QueueScreen(CustomWebApplicationFactory<Program> factory)
{
  private readonly OperatorSurface _surface = new(factory);

  /// <summary>
  /// <b>Le tableau des demandes RGPD liste les dossiers ouverts, rangés par échéance</b> — et
  /// l'ordre est celui des échéances, jamais celui de l'arrivée en base.
  /// </summary>
  [Fact]
  public async Task ListsTheOpenCasesInTheOrderOfTheirDeadlinesRatherThanOfTheirArrival()
  {
    var recent = await _surface.OpenAsync(ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-2)));
    var older = await _surface.OpenAsync(ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-20)));

    var screen = await _surface.ReadAsync(OperatorSurface.Queue);

    var first = screen.IndexOf(older.Value.ToString(), StringComparison.Ordinal);
    var second = screen.IndexOf(recent.Value.ToString(), StringComparison.Ordinal);

    first.ShouldBeGreaterThan(
      -1, "Le dossier le plus ancien n'apparaît pas dans le tableau des demandes RGPD.");
    second.ShouldBeGreaterThan(
      -1, "Le dossier le plus récent n'apparaît pas dans le tableau des demandes RGPD.");
    first.ShouldBeLessThan(second, "L'échéance la plus proche doit passer devant.");
  }

  /// <summary>
  /// <b>Le dépassement est un calcul fait à l'instant de l'affichage</b>, et il se dit par un mot :
  /// aucun nombre de jours, aucun seuil. Un dossier reçu il y a deux mois est dépassé ; un dossier
  /// reçu hier ne l'est pas — et rien n'a eu besoin de tourner entre les deux.
  /// </summary>
  [Fact]
  public async Task ComputesTheOverrunWhenSomebodyLooksRatherThanReadingAPersistedFlag()
  {
    var overrun = await _surface.OpenAsync(ReceptionDate.Declared(DateTimeOffset.UtcNow.AddMonths(-2)));
    var running = await _surface.OpenAsync(ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-1)));

    var screen = await _surface.ReadAsync(OperatorSurface.Queue);

    RowOf(screen, overrun).ShouldContain("dépassé");
    RowOf(screen, running).ShouldNotContain("dépassé");
    RowOf(screen, running).ShouldContain("en cours");
  }

  /// <summary>
  /// <b>Les échéances trient les lignes déjà présentes ; aucune ne fait naître une ligne.</b> Le
  /// dossier est ouvert, il est dans le tableau des demandes RGPD — dépassé ou non, le nombre de
  /// lignes est le même.
  /// </summary>
  [Fact]
  public async Task NeverMakesARowAppearBecauseADeadlinePassed()
  {
    var before = RowsOf(await _surface.ReadAsync(OperatorSurface.Queue));

    var overrun = await _surface.OpenAsync(ReceptionDate.Declared(DateTimeOffset.UtcNow.AddMonths(-3)));

    var after = RowsOf(await _surface.ReadAsync(OperatorSurface.Queue));

    // Un dossier ouvert, une ligne de plus — et pas deux : l'échéance dépassée n'a rien ajouté à
    // côté de la ligne du dossier, elle en a seulement changé une colonne.
    after.ShouldBe(before + 1);

    RowOf(await _surface.ReadAsync(OperatorSurface.Queue), overrun).ShouldContain("dépassé");
  }

  /// <summary>
  /// <b>Aucun nombre affiché ne viole la règle des chiffres.</b> Ni total, ni taux, ni pourcentage,
  /// ni moyenne, ni dénombrement d'absences : <c>0 dossier en retard</c> est impossible à produire ici,
  /// parce qu'aucun compte n'existe.
  /// </summary>
  [Fact]
  public async Task ShowsNoCountNoRateAndNoCountOfAbsencesAnywhere()
  {
    await _surface.OpenAsync(
      ReceptionDate.Declared(DateTimeOffset.UtcNow.AddMonths(-2)),
      [DataSubjectRight.Access, DataSubjectRight.Erasure]);

    var screen = await _surface.ReadWithoutStyleAsync(OperatorSurface.Queue);

    screen.ShouldNotContain("%");

    // « 4 dossiers », « 2 sur 6 », « 0 dossier en retard » : aucune de ces formes ne doit exister.
    Regex.IsMatch(screen, @"\d+\s*(dossiers?|en retard|sur\s+\d+)").ShouldBeFalse(
      "Un nombre ne s'affiche que s'il dénombre une chose présente que le service détient lui-même.");

    // Les droits sont énumérés, jamais comptés.
    screen.ShouldContain("Access, Erasure");
  }

  /// <summary>
  /// <b>Le tableau des demandes RGPD n'offre aucun geste SUR UN DOSSIER.</b> Chaque ligne mène au
  /// dossier, et c'est là que l'<c>Operator</c> agit : une action depuis la liste ferait signer
  /// quelqu'un sans qu'il ait ouvert ce qu'il signe. Aucun formulaire, donc, et pas même une case à
  /// cocher.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>L'écran porte une exception, et une seule</b> : la destruction d'un <c>EvidenceLog</c> échu,
  /// qui n'a aucun dossier où vivre — le sien est clos depuis cinq ans. Elle est <b>tenue à part</b>,
  /// dans sa propre section, et <see cref="ExpiredEvidenceLogSection"/> garde qu'elle ne redescend jamais
  /// parmi les lignes de dossiers. C'est pourquoi ce test-ci lit la liste des dossiers plutôt que
  /// la page entière.
  /// </remarks>
  [Fact]
  public async Task OffersNoGestureFromTheListAndLeadsToTheCaseInstead()
  {
    var opened = await _surface.OpenAsync(ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-3)));

    var screen = await _surface.ReadAsync(OperatorSurface.Queue);

    // La liste des dossiers s'arrête là où commence la section des EvidenceLog échus, et tout ce qui
    // précède ce titre est ce qu'un Operator lit en cherchant par quel dossier commencer.
    var cases = screen[..screen.IndexOf("À détruire", StringComparison.Ordinal)];

    cases.ShouldNotContain("<form");
    cases.ShouldNotContain("<button");
    cases.ShouldNotContain("type=\"checkbox\"");

    cases.ShouldContain($"/dossiers/{opened.Value}");
  }

  /// <summary>
  /// Le tableau des demandes RGPD ne montre <b>que</b> ce qui sert à décider par quoi commencer, et
  /// jamais la personne : ni désignation, ni nom. Le seul nom qu'un écran de liste porterait serait
  /// celui de la personne concernée, et il n'a rien à faire sous les yeux de qui passe.
  /// </summary>
  [Fact]
  public async Task NamesNobodyInTheList()
  {
    await _surface.OpenAsync(ReceptionDate.Declared(DateTimeOffset.UtcNow.AddDays(-4)));

    var screen = await _surface.ReadAsync(OperatorSurface.Queue);

    screen.ShouldNotContain("jean.dupont@example.fr");
  }

  /// <summary>
  /// Le tableau des demandes RGPD dit sur quoi il a calculé : <b>il est recalculé à chaque
  /// affichage</b>. Une liste qui ne dirait pas de quand elle date se lirait comme une vérité
  /// intemporelle.
  /// </summary>
  [Fact]
  public async Task SaysThatItIsRecomputedAtEveryDisplay()
  {
    (await _surface.ReadAsync(OperatorSurface.Queue))
      .ShouldContain("recalculée à chaque");
  }

  /// <summary>Une adresse de dossier qui ne désigne rien n'est pas un écran vide.</summary>
  [Theory]
  [InlineData("/dossiers/00000000-0000-0000-0000-000000000000")]
  [InlineData("/dossiers/pas-un-identifiant")]
  public async Task OffersNoScreenForACaseNobodyOpened(string address)
  {
    (await _surface.Client.GetAsync(address)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
  }

  private static string RowOf(string screen, CaseId opened)
  {
    var rows = screen.Split("<tr>");

    return rows.Single(row => row.Contains(opened.Value.ToString(), StringComparison.Ordinal));
  }

  private static int RowsOf(string screen)
  {
    return Regex.Matches(screen, "<tr>").Count;
  }
}
