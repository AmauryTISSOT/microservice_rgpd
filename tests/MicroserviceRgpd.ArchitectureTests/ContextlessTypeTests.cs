using MicroserviceRgpd.ArchitectureTests.Fixtures.Reporting;

namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// <b>Le second garde, et il pose une autre question que la matrice</b> : <i>aucun type
/// n'appartenant à aucun contexte n'en atteint plus d'un, sauf ceux écrits ici.</i>
/// <para>
/// <see cref="ContextIsolationTests"/> lit l'appartenance à un contexte au segment d'espace de
/// noms, et son angle mort est exactement là : un type qui n'en porte aucun n'est <b>jamais un
/// <c>from</c> ni un <c>to</c></b>, n'apparaît dans aucune paire ordonnée de la matrice, et la
/// matrice reste donc verte sur lui quoi qu'il fasse. Le cas qui fait mal, écrit avec les noms du
/// dépôt : un <c>Infrastructure/Data/Reporting/ScreeningExportService.cs</c> qui lit les colonnes
/// <c>Retained</c>, les rapproche des <c>DeclaredSystem</c> et pré-remplit le <c>Manifest</c> — le
/// geste que la liste <i>Avoid</i> de <c>Suggéré, jamais déclaré</c> bannit nommément.
/// </para>
/// <para>
/// ⚠️ <b>Le garde était vert par vacuité jusqu'ici, et il cesse de l'être avec cette persistance.</b>
/// Tant que l'<c>AppDbContext</c> ne connaissait qu'un contexte, la règle ne trouvait rien à
/// examiner ; le <c>DbSet</c> du <c>Screening</c> en fait le premier fichier sans contexte à en
/// atteindre deux. C'est le moment choisi pour le poser : la dérogation devient <b>nommée et
/// mesurée</b> au lieu d'être découverte le jour où quelqu'un s'en sert.
/// </para>
/// <para>
/// ⚠️ <b>Ce que la mesure a trouvé au-delà de ce que #136 annonçait, écrit ici plutôt que découvert
/// un jour de panne.</b> #136 prévoyait une dérogation d'un seul fichier ; l'IL en montre trois, et
/// les deux autres n'ont rien de fautif — un <b>point de montage</b> et un <b>adaptateur qui
/// atteint le noyau partagé de son propre contexte</b>. Chacun est inscrit avec la liste
/// <b>exacte</b> des contextes qu'il a le droit de toucher : une dérogation par fichier aurait
/// laissé l'<c>AppDbContext</c> apprendre <c>Qualification</c> en silence, ce qui est très
/// exactement ce que ce garde existe pour voir.
/// </para>
/// </summary>
public class ContextlessTypeTests
{
  private static readonly string ThisAssembly =
    Path.Combine(AppContext.BaseDirectory, "MicroserviceRgpd.ArchitectureTests.dll");

  /// <summary>
  /// Les <b>seules</b> dérogations, et chacune est bornée aux contextes qu'elle touche aujourd'hui.
  /// <para>
  /// La liste est écrite en dur, comme celle de <see cref="ContextIsolationTests.PermitsTwoCrossingsAndNoOthers"/>
  /// et pour le même motif : y ajouter une ligne doit demander un geste délibéré, et ce geste est de
  /// niveau ADR.
  /// </para>
  /// </summary>
  private static readonly (string Type, string[] Contexts)[] Permitted =
  [
    // La seule base du service, partagée par les trois contextes — c'est ADR-0003 qui l'a voulue
    // unique, et un schéma par contexte aurait été une seconde base à administrer pour une
    // frontière que l'IL tient déjà. Elle nomme les racines qu'elle stocke, et rien de plus : elle
    // n'a aucun corps de méthode qui rapproche deux contextes l'un de l'autre.
    ("MicroserviceRgpd.Infrastructure.Data.AppDbContext", [ContextInspector.Casework, ContextInspector.Screening]),

    // Le point de montage de la couche : nommer les ports de tous les contextes EST son travail, et
    // l'y interdire n'aurait laissé qu'un point de montage par contexte, c'est-à-dire le même
    // fichier découpé en trois. ⚠️ Il ne fait que déclarer des correspondances : il ne lit rien et
    // n'écrit rien.
    (
      "MicroserviceRgpd.Infrastructure.InfrastructureServiceExtensions",
      [ContextInspector.Qualification, ContextInspector.Casework, ContextInspector.Screening]),

    // L'adaptateur de la trace d'audit : il sert `Qualification` seul, et le noyau partagé qu'il
    // touche est celui que `Qualification` a le droit de toucher — voir la liste blanche de la
    // matrice. Il ne rapproche donc aucun contexte d'un autre. ⚠️ Sa ligne disparaîtrait le jour où
    // il descendrait dans un dossier de contexte, comme les configurations d'entités de `Casework`,
    // qui sont déjà gardées par la matrice pour cette seule raison.
    (
      "MicroserviceRgpd.Infrastructure.Data.Audit.QualificationAuditTrail",
      [ContextInspector.Qualification, ContextInspector.SharedKernel]),
  ];

  public static TheoryData<string> ProductionAssemblies => [.. ProductionAssembly.All];

  /// <summary>
  /// La règle elle-même, contre chaque assemblage de production. Un assemblage oublié se lirait
  /// comme une règle respectée — d'où <see cref="ProductionAssembly.All"/>, écrite en toutes lettres.
  /// </summary>
  [Theory]
  [MemberData(nameof(ProductionAssemblies))]
  public void NoContextlessTypeReachesMoreThanOneContextExceptWhatIsWrittenDown(string assembly)
  {
    var reaches = ContextInspector
      .ContextlessTypesReachingSeveralContexts(ProductionAssembly.PathOf(assembly))
      .Where(reach => !Permitted.Any(
        exempt => exempt.Type == reach.Type && exempt.Contexts.Order().SequenceEqual(reach.Contexts.Order())))
      .ToList();

    reaches.ShouldBeEmpty(
      $"{assembly} porte un type sans contexte qui en rapproche plusieurs :" + Environment.NewLine +
      string.Join(Environment.NewLine, reaches) + Environment.NewLine +
      "Un fichier qui n'habite aucun contexte échappe entièrement à la matrice d'ADR-0003 : il " +
      "n'est jamais un `from` ni un `to`. Rangez-le dans le dossier du contexte qu'il sert — la " +
      "matrice le gardera alors comme elle garde les configurations d'entités de Casework — ou " +
      "coupez ce qu'il rapproche. Élargir la dérogation est un geste de niveau ADR, jamais une " +
      "ligne ajoutée en passant pour faire compiler.");
  }

  /// <summary>
  /// La liste des dérogations ne grossit pas toute seule, et <b>aucune ne grossit non plus de son
  /// côté</b> : c'est le couple (fichier, contextes) qui est inscrit. Un <c>AppDbContext</c> qui
  /// apprendrait <c>Qualification</c> ferait rougir la règle sans qu'aucune ligne n'ait été ajoutée
  /// ici.
  /// </summary>
  [Fact]
  public void PermitsThreeFilesWithTheirExactReachAndNoOthers()
  {
    Permitted.Select(exempt => exempt.Type).ShouldBe(
      [
        "MicroserviceRgpd.Infrastructure.Data.AppDbContext",
        "MicroserviceRgpd.Infrastructure.InfrastructureServiceExtensions",
        "MicroserviceRgpd.Infrastructure.Data.Audit.QualificationAuditTrail",
      ],
      "La liste des dérogations s'est élargie. Un fichier sans contexte qui en rapproche deux est " +
      "l'angle mort d'ADR-0003 : l'y inscrire est un geste de niveau ADR.");

    Permitted.Sum(exempt => exempt.Contexts.Length).ShouldBe(7);
  }

  /// <summary>
  /// <b>Le garde rougit vraiment</b>, et c'est ce que la vacuité d'hier ne permettait pas de dire :
  /// le vert d'une règle qui ne trouve rien et le vert d'une frontière tenue sont exactement le même
  /// vert. L'inspecteur est donc retourné contre son propre assemblage, où vit un témoin écrit pour
  /// lui.
  /// </summary>
  [Fact]
  public void TurnsRedOnAContextlessTypeThatReachesTwoContexts()
  {
    var reaches = ContextInspector.ContextlessTypesReachingSeveralContexts(ThisAssembly);

    var caught = reaches.SingleOrDefault(
      reach => reach.Type == typeof(AnExportServiceWithoutAContext).FullName);

    caught.ShouldNotBeNull(
      "Le service d'export sans contexte passe : le garde ne voit pas l'angle mort qu'il existe " +
      "pour refermer, et un ScreeningExportService naîtrait sous un vert complet.");

    caught.Contexts.ShouldBe([ContextInspector.Casework, ContextInspector.Screening], ignoreOrder: true);
  }

  /// <summary>
  /// L'exact pendant du précédent. La règle porte sur le <b>rapprochement</b> de deux contextes,
  /// jamais sur l'absence de contexte : un garde qui dénoncerait tout fichier sans contexte
  /// obligerait à ranger la moitié de la couche d'infrastructure dans une liste blanche, ce qui
  /// reviendrait à n'avoir aucun garde.
  /// </summary>
  [Fact]
  public void SaysNothingOfAContextlessTypeThatStaysWithinOneContext()
  {
    var reaches = ContextInspector.ContextlessTypesReachingSeveralContexts(ThisAssembly);

    reaches.ShouldNotContain(reach => reach.Type == typeof(AReaderThatStaysWithinOneContext).FullName);
  }

  /// <summary>
  /// <b>La règle a vraiment quelque chose à examiner en production.</b> Elle était verte par
  /// vacuité tant qu'aucun fichier sans contexte n'en atteignait deux ; ce test sépare ce vert-là
  /// de celui d'une règle qui trouve et laisse passer ce qui est écrit.
  /// </summary>
  [Fact]
  public void FindsInProductionTheDerogationsItClaimsToMeasure()
  {
    var reaches = ProductionAssembly.All
      .SelectMany(assembly => ContextInspector
        .ContextlessTypesReachingSeveralContexts(ProductionAssembly.PathOf(assembly)))
      .Select(reach => reach.Type)
      .ToList();

    reaches.ShouldBe(Permitted.Select(exempt => exempt.Type), ignoreOrder: true,
      "Ce que la règle trouve en production a bougé sans que la liste des dérogations ne bouge. " +
      "Une dérogation devenue inutile se retire : la laisser rendrait la règle verte sur un " +
      "fichier que plus personne ne regarde.");
  }
}
