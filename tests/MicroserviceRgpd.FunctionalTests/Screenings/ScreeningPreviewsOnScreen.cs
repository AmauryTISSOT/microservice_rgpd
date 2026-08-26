using System.Net;
using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.FunctionalTests.Screenings;

/// <summary>
/// Les <c>ColumnPreview</c> à l'écran, et leur <b>expiration</b> : ce que l'<c>Operator</c> lit à
/// côté d'un motif de forme, et ce qu'il lit quand les valeurs ne sont plus là.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucun test de ce fichier n'instancie le cache d'aperçus.</b> Ce n'est pas une couture de
/// test : tout passe par la frontière HTTP, et le seul levier sur sa durée de vie est l'horloge
/// injectée — c'est-à-dire très exactement le levier dont l'exploitation dispose. Un cache doublé
/// n'aurait prouvé que le comportement de la doublure, sur la seule promesse de ce contexte qui se
/// mesure en heures.
/// </para>
/// <para>
/// ⚠️ <b>L'horloge est remise à l'heure réelle par chaque test qui l'avance, quoi qu'il arrive.</b>
/// Le déploiement est partagé par toute la collection, et « le rapport courant » est le plus grand
/// <c>LaunchedOn</c> : un test qui repartirait avec douze heures d'avance daterait dans le futur le
/// rapport du test suivant, et le courant cesserait d'être celui qu'on vient de déposer.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class ScreeningPreviewsOnScreen(CustomWebApplicationFactory<Program> factory)
{
  /// <summary>La colonne signalée dont on lit l'aperçu — son nom suffit au lexique à la signaler.</summary>
  private const string Flagged = "courriel";

  /// <summary>Une valeur coupée par le SGBD, et la longueur qu'elle avait en base.</summary>
  private const int RealLengthOfTheTruncatedValue = 900;

  private readonly ScreeningSurface _surface = new(factory);

  /// <summary>
  /// Sur un rapport <c>Scanné</c> frais, la fiche d'une colonne signalée rend ses valeurs sur la
  /// ligne du motif — <c>⟨null⟩</c> et <c>⟨vide⟩</c> compris —, la longueur réelle de ce qui a été
  /// coupé, la phrase du premier venu et le décompte.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est ce test qui garde « pouvoir dire non à la machine ».</b> Un motif de forme —
  /// « toutes les valeurs lues portent une clé de contrôle d'IBAN » — n'est vérifiable que si les
  /// valeurs sont là, à côté de lui.
  /// </remarks>
  [Fact]
  public async Task ShowsTheReadValuesOnTheLineOfTheReasonTheyCarry()
  {
    var table = await ScanAndOpenAsync(AScanShowing(Flagged, AnAssortedPreview()));

    var card = ScreeningSurface.BlockOf(table, Flagged);

    card.ShouldNotBeNull($"L'écran ne porte aucun bloc pour « {Flagged} ».");

    card.ShouldContain("jean@exemple.fr");

    // ⚠️ DEUX MARQUEURS PLUTÔT QU'UN : le NULL lu et la chaîne vide lue sont deux choses
    // différentes en base, et ce sont deux VALEURS lues — le prélèvement a parfaitement réussi.
    // Rendues en cellules blanches, elles se liraient comme l'absence que la raison nommée chasse.
    card.ShouldContain(PreviewedValue.NullMarker);
    card.ShouldContain(PreviewedValue.EmptyMarker);

    // ⚠️ LA LONGUEUR RÉELLE ACCOMPAGNE LA VALEUR COUPÉE. Sans elle, un courriel amputé se lit comme
    // un courriel — et la clause qui écarte du compte une valeur tronquée serait écrite sans jamais
    // pouvoir s'appliquer.
    card.ShouldContain(RealLengthOfTheTruncatedValue.ToString(System.Globalization.CultureInfo.InvariantCulture));

    // Le biais du premier venu est dit, une fois, là où les valeurs se lisent — et c'est la phrase
    // du domaine, jamais une prose de l'écran.
    table.ShouldContain(ColumnPreview.FirstComeStatement);

    // Et le décompte : l'avant se dit par écran.
    table.ShouldContain("Les aperçus de ce rapport disparaîtront dans");
  }

  /// <summary>
  /// Une colonne sans valeur rend <b>sa raison nommée</b>, jamais une cellule vide — et la phrase
  /// est celle du membre de <see cref="PreviewAbsenceReason"/>, jamais une prose de l'écran.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>« Aucune valeur retournée », et jamais « cette table est vide ».</b> Deux situations
  /// produisent cette famille, indiscernables du dehors : la table est réellement vide, ou elle est
  /// pleine et filtrée par une politique de sécurité au niveau ligne. Affirmer le vide serait
  /// l'<c>Omission silencieuse</c> reconstituée dans le champ créé pour l'empêcher, sur la table
  /// <c>patients</c> d'un hôpital.
  /// </remarks>
  [Fact]
  public async Task NamesOneOfTheFourReasonsRatherThanLeavingTheCellEmpty()
  {
    foreach (var reason in PreviewAbsenceReason.List)
    {
      var table = await ScanAndOpenAsync(AScanWhereNothingWasRead(reason));

      var card = ScreeningSurface.BlockOf(table, Flagged);

      card.ShouldNotBeNull($"L'écran ne porte aucun bloc pour « {Flagged} ».");
      card.ShouldContain(
        reason.FrenchLabel,
        Case.Sensitive,
        $"La fiche ne dit pas le libellé de « {reason.Name} » : une case vide se lirait comme un "
        + "silence, ce qui est l'Omission silencieuse réintroduite par la mise en page.");
      card.ShouldContain(reason.Statement);

      // ⚠️ Le service ne dit jamais ce que la colonne CONTIENT : il dit ce qu'il a OBSERVÉ.
      table.ShouldNotContain("cette table est vide");
      table.ShouldNotContain("ne contient aucune ligne");

      // ⚠️ ET LA PHRASE DU BIAIS SE TAIT : aucune des trois colonnes de ce scan n'a de valeur à
      // montrer. Mettre en garde contre le biais de valeurs que personne n'a sous les yeux aurait
      // décrit un travers qui n'a rien travesti — et le décompte, lui, reste dû : les aperçus
      // existent, ils portent des raisons.
      table.ShouldNotContain(
        ColumnPreview.FirstComeStatement,
        Case.Sensitive,
        "L'écran met en garde contre le biais du premier venu sur un scan qui n'a rendu aucune "
        + "valeur : la phrase compte des entrées d'aperçu au lieu de compter des valeurs lues.");
    }
  }

  /// <summary>
  /// Les deux heures <b>glissent</b> : une heure cinquante-neuf après une visite de la table, les
  /// aperçus sont là ; deux heures une sans visite, ils ont expiré et le bloc a quitté l'écran.
  /// </summary>
  [Fact]
  public async Task SlidesTwoHoursForwardOnEveryVisitOfTheTableAndExpiresWithoutOne()
  {
    try
    {
      var table = await ScanAndOpenAsync(AScanShowing(Flagged, AnAssortedPreview()));

      table.ShouldContain("jean@exemple.fr");

      // Une heure cinquante-neuf après la visite qui vient de réarmer : encore là.
      factory.Clock.Advance(TimeSpan.FromMinutes(119));

      var stillThere = await ReadTheTableAsync();

      stillThere.ShouldContain(
        "jean@exemple.fr",
        Case.Sensitive,
        "Les deux heures glissantes n'ont pas été réarmées par la visite de la table.");

      // Deux heures une APRÈS cette visite-là : la fenêtre est passée.
      factory.Clock.Advance(TimeSpan.FromMinutes(121));

      var expired = await ReadTheTableAsync();

      ShouldHaveExpired(expired);
    }
    finally
    {
      factory.Clock.Reset();
    }
  }

  /// <summary>
  /// ⚠️ <b>Ni le rapport, ni l'historique, ni l'archive ne réarment la fenêtre.</b> Ce sont les
  /// écrans qui <b>montrent</b> des aperçus qui les prolongent, et eux seuls : un onglet laissé sur
  /// l'accueil ferait sinon d'un cache une rétention.
  /// </summary>
  [Fact]
  public async Task IsRearmedByTheTableAndByNoOtherScreen()
  {
    try
    {
      // Un premier rapport, qui partira à l'archive — c'est lui qui donne une adresse d'archive
      // réelle à visiter.
      await new ScanSurface(factory).ScanAsync(DatabaseScannerDouble.AListing(("adherents", Flagged)));

      var archived = await _surface.CurrentScreeningAsync();

      var table = await ScanAndOpenAsync(AScanShowing(Flagged, AnAssortedPreview()));

      table.ShouldContain("jean@exemple.fr");

      factory.Clock.Advance(TimeSpan.FromMinutes(90));

      // Les quatre écrans que la spec nomme sont visités, et pas un ne montre d'aperçu : pas un ne
      // prolonge. ⚠️ L'accueil en fait partie — c'est l'écran devant lequel un onglet reste ouvert
      // toute une journée, et donc celui par qui un cache deviendrait une rétention.
      await _surface.ReadAsync("/");
      await _surface.ReadAsync(ScreeningSurface.Report);
      await _surface.ReadAsync(ScreeningSurface.History);
      await _surface.ReadAsync(ScreeningSurface.ArchiveOf(archived));

      factory.Clock.Advance(TimeSpan.FromMinutes(40));

      var expired = await ReadTheTableAsync();

      ShouldHaveExpired(
        expired,
        "Un écran qui ne montre aucun aperçu a prolongé la vie des aperçus.");
    }
    finally
    {
      factory.Clock.Reset();
    }
  }

  /// <summary>
  /// ⚠️ <b>Le plafond absolu mord malgré des visites régulières.</b> Douze heures après le scan, les
  /// aperçus ne sont plus là — sans quoi un onglet rouvert toutes les heures ferait vivre des
  /// valeurs réelles du client aussi longtemps que le processus.
  /// </summary>
  [Fact]
  public async Task DiesTwelveHoursAfterTheScanEvenWhenTheTableIsVisitedRegularly()
  {
    try
    {
      var table = await ScanAndOpenAsync(AScanShowing(Flagged, AnAssortedPreview()));

      table.ShouldContain("jean@exemple.fr");

      // Sept visites espacées d'une heure trente : la fenêtre glissante ne se ferme jamais, et
      // l'horloge arrive à dix heures trente après le scan.
      for (var visit = 0; visit < 7; visit++)
      {
        factory.Clock.Advance(TimeSpan.FromMinutes(90));

        var alive = await ReadTheTableAsync();

        alive.ShouldContain(
          "jean@exemple.fr",
          Case.Sensitive,
          $"Les aperçus ont disparu à la visite n° {visit + 1}, sous le plafond absolu.");
      }

      // ⚠️ Et le décompte a cessé d'annoncer deux heures : il est borné par le plafond, sans quoi
      // il promettrait un délai que le cache n'a pas l'intention de tenir.
      var lastVisit = await ReadTheTableAsync();

      lastVisit.ShouldNotContain("disparaîtront dans 2 h");

      // Douze heures une après le scan.
      factory.Clock.Advance(TimeSpan.FromMinutes(91));

      ShouldHaveExpired(
        await ReadTheTableAsync(),
        "Le plafond absolu de douze heures n'a pas mordu.");
    }
    finally
    {
      factory.Clock.Reset();
    }
  }

  /// <summary>
  /// ⚠️ <b>Une expiration n'est pas une péremption.</b> Le motif de forme reste arbitrable, l'écran
  /// le dit, et les <b>raisons</b> d'absence — enregistrées sur la ligne — restent affichées.
  /// Refuser l'arbitrage aurait laissé pour seule issue de relancer un scan, c'est-à-dire de
  /// détruire le travail déjà tranché.
  /// </summary>
  [Fact]
  public async Task StillAcceptsAnArbitrationOnceThePreviewsHaveExpired()
  {
    try
    {
      await ScanAndOpenAsync(
        AScanShowing(Flagged, ColumnPreview.Absent(PreviewAbsenceReason.AccessDenied)));

      factory.Clock.Advance(TimeSpan.FromMinutes(121));

      var expired = await ReadTheTableAsync();

      ShouldHaveExpired(expired);

      // ⚠️ LA RAISON RESTE : c'est une propriété enregistrée de la colonne, pas un aperçu. La
      // retirer avec les valeurs rendrait un rapport d'une heure MOINS renseigné que son archive.
      ScreeningSurface.BlockOf(expired, Flagged)
        .ShouldNotBeNull()
        .ShouldContain(PreviewAbsenceReason.AccessDenied.FrenchLabel);

      var arbitrated = await _surface.ArbitrateAsync(
        Flagged, ScreenedColumnState.Retained.Name);

      arbitrated.StatusCode.ShouldBe(
        HttpStatusCode.Redirect,
        "Un arbitrage posé après l'expiration des aperçus a été refusé : une expiration technique "
        + "vient de périmer un rapport.");

      var afterwards = await ReadTheTableAsync();

      ScreeningSurface.BlockOf(afterwards, Flagged)
        .ShouldNotBeNull()
        .ShouldContain(ScreenedColumnState.Retained.FrenchLabel);
    }
    finally
    {
      factory.Clock.Reset();
    }
  }

  /// <summary>
  /// ⚠️ <b>Un nouveau scan évince les aperçus du rapport qu'il fait reculer</b>, et l'écran
  /// d'archive n'en rend jamais. Les garder tiendrait en mémoire ce que la base a le droit de
  /// refuser, derrière un rapport que plus aucun écran d'arbitrage ne montre.
  /// </summary>
  [Fact]
  public async Task EvictsThePreviewsOfTheReportANewScanSendsToTheArchive()
  {
    var table = await ScanAndOpenAsync(AScanShowing(Flagged, AnAssortedPreview()));

    table.ShouldContain("jean@exemple.fr");

    var archived = await _surface.CurrentScreeningAsync();

    await new ScanSurface(factory).ScanAsync(
      DatabaseScannerDouble.AListing(("adherents", Flagged)));

    var archivedTable = WebUtility.HtmlDecode(
      await _surface.ReadAsync(ScreeningSurface.ArchivedTableOf(archived)));

    archivedTable.ShouldNotContain(
      "jean@exemple.fr",
      Case.Sensitive,
      "L'écran d'archive rend une valeur lue : une archive ne porte aucun aperçu.");
    archivedTable.ShouldNotContain("disparaîtront dans");
  }

  /// <summary>
  /// ⚠️ <b>Un rapport qui remonte après une suppression n'entend pas parler de redémarrage.</b> Ses
  /// aperçus ont bien disparu — un relevé plus récent a pris leur place —, mais aucune panne n'a eu
  /// lieu, et lui annoncer une panne serait annoncer une perte à qui n'en a pas subi celle-là.
  /// </summary>
  /// <remarks>
  /// <para>
  /// C'est le seul chemin par lequel l'éviction du cache s'observe depuis la frontière HTTP : un
  /// rapport évincé est normalement archivé, et un écran d'archive ne rend aucun aperçu. Supprimer
  /// celui qui l'a évincé le ramène au rang de courant, et son écran de table redevient lisible.
  /// </para>
  /// <para>
  /// ⚠️ <b>Il éprouve du même geste les deux gestes du cache</b> : le dépôt du jeu suivant, qui
  /// évince — le collage appelle <c>Forget</c>, un second scan appelle <c>Keep</c> — et la mémoire
  /// qu'il garde d'avoir tenu quelque chose, sans laquelle la phrase du redémarrage se mettrait à
  /// mentir.
  /// </para>
  /// </remarks>
  [Theory]
  [InlineData(true)]
  [InlineData(false)]
  public async Task SaysAnotherListingTookTheirPlaceRatherThanBlamingARestart(bool byPasting)
  {
    var table = await ScanAndOpenAsync(AScanShowing(Flagged, AnAssortedPreview()));

    table.ShouldContain("jean@exemple.fr");

    var scanned = await _surface.CurrentScreeningAsync();

    if (byPasting)
    {
      await _surface.DepositAsync(
        ScreeningSurface.Paste(ScreeningSurface.Column(Flagged, position: 1)));
    }
    else
    {
      await new ScanSurface(factory).ScanAsync(
        DatabaseScannerDouble.AListing(("adherents", Flagged)));
    }

    var evicting = await _surface.CurrentScreeningAsync();

    // Le relevé qui vient d'évincer part, et le rapport scanné redevient le courant.
    var deleted = await _surface.DeleteAsync(evicting, "galette_prod");

    deleted.StatusCode.ShouldBe(
      HttpStatusCode.Redirect,
      "La suppression du relevé qui a évincé les aperçus a été refusée : le rapport scanné ne "
      + "redevient pas le courant, et ce test n'éprouve plus rien.");

    (await _surface.CurrentScreeningAsync()).ShouldBe(scanned);

    var afterwards = await ReadTheTableAsync();

    afterwards.ShouldContain(
      PreviewAvailability.Evicted.Statement!,
      Case.Sensitive,
      "L'écran ne dit pas qu'un autre relevé a pris la place des aperçus.");

    // ⚠️ ET SURTOUT PAS CELLE-CI : aucun redémarrage n'a eu lieu.
    afterwards.ShouldNotContain(PreviewAvailability.ClearedByRestart.Statement!);
    afterwards.ShouldNotContain("jean@exemple.fr");
  }

  /// <summary>
  /// ⚠️ <b>Un redémarrage efface les aperçus, et c'est DIT plutôt que réparé.</b> Les faire survivre
  /// demanderait de les écrire, c'est-à-dire de faire du service un détenteur durable de données
  /// personnelles du client.
  /// </summary>
  /// <remarks>
  /// Le redémarrage est un <b>second hôte sur la même base</b> : le rapport est toujours là, le
  /// cache mémoire du processus, non.
  /// </remarks>
  [Fact]
  public async Task SaysThePreviewsWereClearedByARestart()
  {
    var table = await ScanAndOpenAsync(AScanShowing(Flagged, AnAssortedPreview()));

    table.ShouldContain("jean@exemple.fr");

    using var restarted = factory.WithWebHostBuilder(_ => { });
    using var client = restarted.CreateClient(
      new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    var afterTheRestart = WebUtility.HtmlDecode(
      await client.GetStringAsync(ScreeningSurface.TableOf()));

    afterTheRestart.ShouldContain(PreviewAvailability.ClearedByRestart.Statement!);
    afterTheRestart.ShouldNotContain("jean@exemple.fr");

    // ⚠️ Et la phrase du redémarrage n'est pas celle de l'expiration : l'une dit une panne, l'autre
    // une durée écoulée, et confondre les deux annoncerait une perte à qui n'en a pas subi.
    afterTheRestart.ShouldNotContain(PreviewAvailability.Expired.Statement!);
  }

  /// <summary>
  /// ⚠️ <b>Un relevé COLLÉ ne dit rien du tout de ses aperçus.</b> Annoncer une expiration là où
  /// aucun prélèvement n'a eu lieu affirmerait qu'il y en a eu un — exactement comme quatre comptes
  /// à zéro y inventeraient une incomplétude.
  /// </summary>
  [Fact]
  public async Task SaysNothingAboutPreviewsOnAPastedListing()
  {
    await _surface.DepositAsync(
      ScreeningSurface.Paste(
        ScreeningSurface.Column("id_adh", position: 1),
        ScreeningSurface.Column(Flagged, position: 2)));

    var table = await ReadTheTableAsync();

    table.ShouldNotContain("disparaîtront dans");
    table.ShouldNotContain(PreviewAvailability.Expired.Statement!);
    table.ShouldNotContain(PreviewAvailability.ClearedByRestart.Statement!);
  }

  /// <summary>
  /// Ce que l'écran doit dire quand les aperçus ne sont plus là : la phrase du <b>rapport</b> en
  /// tête, et <b>aucune</b> valeur nulle part.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le bloc quitte l'écran entièrement.</b> Écrire « valeurs expirées » dans la case aurait
  /// fabriqué la troisième forme que <c>ColumnPreview</c> interdit : un aperçu est soit des valeurs,
  /// soit une raison nommée.
  /// </remarks>
  private static void ShouldHaveExpired(string table, string? because = null)
  {
    table.ShouldContain(
      PreviewAvailability.Expired.Statement!,
      Case.Sensitive,
      because ?? "L'écran ne dit pas que les aperçus de ce rapport ont expiré.");

    table.ShouldNotContain("jean@exemple.fr");
    table.ShouldNotContain("disparaîtront dans");

    // ⚠️ La troisième forme, nommément : elle ne doit apparaître dans aucune case.
    table.ShouldNotContain("valeurs expirées");
  }

  /// <summary>
  /// Un relevé scanné de trois colonnes, dont l'une porte l'aperçu qu'on lui dicte.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Il se compose à partir de la fixture partagée plutôt que de s'écrire à la main.</b> Le
  /// pivot doit passer les neuf refus de l'ingestion — dont le saut de rang —, et un relevé écrit
  /// ici les aurait tous redécouverts un par un.
  /// </remarks>
  private static ScanOutcome AScanShowing(string column, ColumnPreview preview)
  {
    var listed = DatabaseScannerDouble.AListing(
      ("adherents", "id_adh"), ("adherents", column), ("adherents", "montant"));

    var previews = listed.Previews.ToDictionary(entry => entry.Key, entry => entry.Value);

    previews[ColumnIdentity.Of("public", "adherents", column)] = preview;

    return ScanOutcome.Listed(listed.Pivot!, previews);
  }

  /// <summary>
  /// Un relevé scanné dont <b>aucune</b> colonne n'a rendu de valeur : toutes portent la même raison
  /// nommée.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est le seul relevé qui distingue « compter des entrées » de « compter des valeurs ».</b>
  /// Un relevé où une seule colonne est refusée porte encore des valeurs ailleurs, et une phrase qui
  /// compte les entrées d'aperçu y resterait verte.
  /// </remarks>
  private static ScanOutcome AScanWhereNothingWasRead(PreviewAbsenceReason reason)
  {
    var listed = DatabaseScannerDouble.AListing(
      ("adherents", "id_adh"), ("adherents", Flagged), ("adherents", "montant"));

    return ScanOutcome.Listed(
      listed.Pivot!,
      listed.Previews.ToDictionary(entry => entry.Key, _ => ColumnPreview.Absent(reason)));
  }

  /// <summary>
  /// Un aperçu qui porte les quatre formes qu'une valeur lue peut prendre : du texte, le
  /// <c>NULL</c> lu, la chaîne vide lue, et une valeur coupée par le SGBD.
  /// </summary>
  private static ColumnPreview AnAssortedPreview()
  {
    return ColumnPreview.Read(
    [
      PreviewedValue.Of("jean@exemple.fr", "jean@exemple.fr".Length),
      PreviewedValue.NullValue,
      PreviewedValue.EmptyText,
      PreviewedValue.Of(
        new string('a', ColumnPreview.MaxValueLength), RealLengthOfTheTruncatedValue),
    ]);
  }

  /// <summary>Fait courir un scan jusqu'au bout, puis ouvre la table qu'il vient de produire.</summary>
  private async Task<string> ScanAndOpenAsync(ScanOutcome outcome)
  {
    await new ScanSurface(factory).ScanAsync(outcome);

    return await ReadTheTableAsync();
  }

  /// <summary>L'écran de la table, tel qu'un <c>Operator</c> le lit — entités HTML résolues.</summary>
  private async Task<string> ReadTheTableAsync()
  {
    return WebUtility.HtmlDecode(await _surface.ReadAsync(ScreeningSurface.TableOf()));
  }
}
