using System.Data.Common;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.Casework.EvidenceLog;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.Infrastructure.Data.Casework;

// Shouldly porte un type du même nom, réservé à ses propres tables de cas.
using Case = MicroserviceRgpd.Core.Casework.Case;
using EvidenceLog = MicroserviceRgpd.Infrastructure.Data.Casework.EvidenceLog;

namespace MicroserviceRgpd.IntegrationTests.Data.Casework;

/// <summary>
/// La forme que la migration a réellement donnée au <c>EvidenceLog</c>, et ce qu'elle rend
/// <b>impossible</b>.
/// </summary>
/// <remarks>
/// <para>
/// « Anonyme par construction, jamais par expurgation » est une promesse qu'on ne peut pas tenir en
/// vérifiant ce qui a été écrit — il faudrait avoir tout écrit pour le savoir. Ces tests vérifient
/// donc qu'<b>aucune colonne n'existe</b> où une <c>Designation</c> ou un nom de personne concernée
/// pourrait atterrir, et ils portent sur le <b>schéma</b> plutôt que sur le modèle : c'est la
/// migration qui sera appliquée en production, et un modèle correct dont la migration diverge ne
/// protège de rien.
/// </para>
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public class EvidenceLogSchemaTests(PostgreSqlFixture postgres)
{
  private static readonly DateTimeOffset Opened = new(2026, 8, 3, 14, 30, 0, TimeSpan.Zero);

  /// <summary>
  /// Vingt et une colonnes, nommées une par une : ce qui n'a pas de colonne ne s'écrira pas. La liste est
  /// écrite en toutes lettres <b>pour que l'ajout d'une colonne soit un geste délibéré</b> — une
  /// colonne de prose libre glissée ici serait la porte par laquelle un nom finirait par passer.
  /// <para>
  /// <c>declared_system</c> est arrivée avec les tentatives d'appel d'<c>Adapter</c> : elle porte un
  /// nom du <b>paysage déclaré du client</b>, choisi par l'humain qui l'a recensé, et jamais un nom de
  /// personne concernée.
  /// </para>
  /// <para>
  /// Cinq sont arrivées avec la surface de l'<c>Operator</c>, et chacune pour une raison écrite :
  /// <c>signer_verification</c>, pour que le nom saisi sans authentification ne soit pas relu comme une
  /// identification ; <c>reception_was_defaulted</c>, pour qu'une date tenue pour défaut ne se lise pas
  /// comme un fait déclaré ; <c>data_subject_right</c> et <c>step_state</c>, qui disent de quel travail
  /// dû un constat parle ; et <c>evidence_prose</c>, <b>seule colonne de prose de la table</b>.
  /// </para>
  /// <para>
  /// ⚠️ <c>evidence_prose</c> ne porte que le <b>texte qui reste</b> — écrit à un point de décision,
  /// non nominatif par nature, et qui survit. Le <b>texte qui meurt</b>, qui nomme des tiers, n'a
  /// aucune colonne ici : il vit sur le <c>Case</c> et meurt à la clôture. La règle tient par ce
  /// <b>placement</b>, et l'écran offre les deux champs à deux endroits distincts.
  /// </para>
  /// <para>
  /// Deux sont arrivées avec le <b>dépôt manuel</b>, et chacune pour une raison écrite :
  /// <c>received_on</c>, parce qu'une demande transcrite d'une boîte aux lettres a été reçue avant
  /// d'être déposée — une seule date aurait fait choisir entre dater le geste et dater le délai ; et
  /// <c>identity_verification_method</c>, la <b>moitié qui se compte</b> de la motivation d'identité,
  /// pour que le contrôle dénombre une pratique.
  /// </para>
  /// <para>
  /// ⚠️ Le <b>détail</b> de la motivation n'a, lui, aucune colonne ici, et c'est le même placement
  /// que pour le texte qui meurt : il dit qui a été rappelé et sur quoi, il nomme donc par nature,
  /// il vit sur le <c>Case</c> et meurt à la clôture. Le contrôle juge la pratique sans qu'un seul
  /// nom lui survive.
  /// </para>
  /// <para>
  /// Une est arrivée avec <c>Locate</c> : <c>declared_deadline</c>, l'échéance qu'un <c>Adapter</c>
  /// <b>déclare</b> en différant. Trois dates disent alors tout de ce travail — appelé, échéance
  /// déclarée, résultat. ⚠️ <b>Aucune colonne ne compte les passages</b> : une relance n'a aucun
  /// signataire, c'est un affichage qui l'a déclenchée, et son compte serait du bruit de mécanique
  /// dans ce que le contrôle vient lire.
  /// </para>
  /// <para>
  /// ⚠️ <b>Ni la référence opaque, ni le motif d'une réserve n'ont de colonne ici</b>, et c'est encore
  /// le même placement : l'une désigne les données de quelqu'un chez le client, l'autre dit quelle
  /// ligne appartient à qui. Les deux vivent sur le <c>Case</c> et meurent à sa clôture ; ce qui
  /// survit d'un arbitrage est le <b>fait daté</b> et le <b>compte du sac</b>.
  /// </para>
  /// <para>
  /// Deux sont arrivées avec la remise : <c>covered_system_count</c> et
  /// <c>declared_system_count</c>, les deux moitiés du « 2 sur 6 ». ⚠️ <b>Elles s'arrêtent ici</b> :
  /// leur lecteur est le contrôle, qui juge une pratique, et le même rapport écrit à la personne
  /// donnerait à une déclaration qui vieillit exprès l'autorité d'un recensement.
  /// </para>
  /// <para>
  /// Une est arrivée avec la clôture : <c>closing_cause</c>, un vocabulaire fermé — donc une colonne
  /// à lui, parce qu'il <b>se compte</b>. ⚠️ <b>Le motif n'en a pas</b> : c'est de la prose de
  /// preuve, il partage <c>evidence_prose</c> avec les constats, et une seconde colonne de prose
  /// aurait fait chercher un motif à deux endroits.
  /// </para>
  /// <para>
  /// Une est arrivée avec la <b>prolongation de l'art. 12.3</b> : <c>informed_on</c>, le jour où
  /// l'<c>Operator</c> déclare avoir informé la personne. ⚠️ Elle ne se confond pas avec
  /// <c>occurred_at</c>, qui date la déclaration au service : l'article exige que la personne soit
  /// informée <b>dans le mois</b>, et une seule date aurait fait choisir entre dater le clic et
  /// dater l'obligation. ⚠️ <b>Aucune colonne ne dit si l'échéance a bougé</b> : c'est un calcul
  /// refait à chaque affichage, et le persister l'aurait fait relire comme un fait signé.
  /// </para>
  /// </summary>
  [Fact]
  public async Task NamesTwentyOneColumnsAndNotOneMoreWhereANameCouldLand()
  {
    var columns = await ColumnsAsync();

    columns.Keys.Order().ShouldBe(
    [
      "case_id",
      "closing_cause",
      "covered_system_count",
      "data_subject_right",
      "declared_deadline",
      "declared_system",
      "declared_system_count",
      "designation_count",
      "entry_id",
      "evidence_prose",
      "fact",
      "identity_declaration",
      "identity_verification_method",
      "informed_on",
      "occurred_at",
      "received_on",
      "reception_was_defaulted",
      "signatory_kind",
      "signatory_name",
      "signer_verification",
      "step_state",
    ]);
  }

  /// <summary>
  /// Une tentative d'appel refusée fait l'aller-retour : le fait qui dit lequel des deux refus
  /// c'était, le système appelé, et <b>personne</b> comme signataire.
  /// </summary>
  [Fact]
  public async Task AppendsTheDatedAttemptOfARefusedAdapterCall()
  {
    await using var dbContext = postgres.NewDbContext();

    var refused = CaseId.Next();

    await new EvidenceLog(dbContext).AppendAsync(
      EvidenceLogEntry.AdapterRefused(
        refused,
        Opened,
        DeclaredSystemId.From("boutique"),
        AdapterOutcome.SecretRefused));

    await using var reread = postgres.NewDbContext();

    var line = await reread.Set<EvidenceLogRow>()
      .AsNoTracking()
      .SingleAsync(row => row.CaseId == refused.Value);

    line.Fact.ShouldBe("AdapterRefusedTheSecret");
    line.DeclaredSystem.ShouldBe("boutique");
    line.SignatoryKind.ShouldBe("Application");
    line.SignatoryName.ShouldBeNull();
    line.DesignationCount.ShouldBeNull();
  }

  /// <summary>
  /// La clôture fait l'aller-retour : la cause dans sa colonne, le motif dans le texte qui reste,
  /// et le nom de l'humain qui a signé. <b>C'est ce qui survit au dossier détruit.</b>
  /// </summary>
  [Fact]
  public async Task AppendsTheClosureThatOutlivesTheCaseItEmptied()
  {
    await using var dbContext = postgres.NewDbContext();

    var closed = CaseId.Next();

    await new EvidenceLog(dbContext).AppendAsync(
      EvidenceLogEntry.CaseClosed(
        closed,
        Opened,
        ClosingCause.Abandoned,
        "La personne s'est ravisée et a demandé l'effacement de son dossier.",
        Signatory.Operator("Camille Roy", SignerVerification.Unauthenticated)));

    await using var reread = postgres.NewDbContext();

    var line = await reread.Set<EvidenceLogRow>()
      .AsNoTracking()
      .SingleAsync(row => row.CaseId == closed.Value);

    line.Fact.ShouldBe(nameof(EvidenceLogFact.CaseClosed));
    line.ClosingCause.ShouldBe(nameof(ClosingCause.Abandoned));
    line.Prose!.ShouldContain("demandé l'effacement");

    // Elle nomme l'Operator, définitivement : la preuve d'une procédure ne peut pas dépendre du
    // consentement de qui l'a instruite.
    line.SignatoryName.ShouldBe("Camille Roy");
    line.SignerVerification.ShouldBe(nameof(SignerVerification.Unauthenticated));

    // Et rien du dossier qu'elle vient de vider.
    line.DesignationCount.ShouldBeNull();
    line.DataSubjectRight.ShouldBeNull();
    line.DeclaredSystem.ShouldBeNull();
  }

  /// <summary>
  /// <b>Aucune <c>Designation</c> n'est stockable, dès la première ligne.</b> Ni valeur, ni nature,
  /// ni sac : la seule chose que le <c>EvidenceLog</c> sait de la recherche est son <b>nombre</b>, et il
  /// est typé <c>integer</c> — un texte ne peut pas s'y ranger.
  /// </summary>
  [Fact]
  public async Task OffersNoColumnWhereADesignationCouldEverLand()
  {
    var columns = await ColumnsAsync();

    string[] forbidden = ["designation_value", "email", "phone", "subject", "designations"];

    // ⚠️ « subject » reste interdit partout sauf dans `data_subject_right`, et l'exception est nommée
    // plutôt que le mot retiré de la liste : celui-là vient de la taxonomie du RGPD — « data subject
    // right » — et désigne un droit, jamais la personne qui l'exerce. Toute autre colonne portant ce
    // mot serait la porte par laquelle un attribut de la personne entrerait.
    columns.Keys
      .Where(name => name != "data_subject_right")
      .ShouldNotContain(
        name => forbidden.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase)));

    columns["designation_count"].DataType.ShouldBe("integer");
  }

  /// <summary>
  /// <b>Le seul nom de personne que la table porte est celui de l'<c>Operator</c></b>, et il est
  /// facultatif — l'application appelant depuis une session authentifiée ne signe sous aucun nom,
  /// et la ligne le dit par son <c>signatory_kind</c> plutôt que par un nom vide.
  /// </summary>
  [Fact]
  public async Task CarriesTheOperatorsNameAndNoOtherPersonsName()
  {
    var columns = await ColumnsAsync();

    columns.Keys.Where(name => name.Contains("name", StringComparison.Ordinal))
      .ShouldBe(["signatory_name"]);

    columns["signatory_name"].Nullable.ShouldBeTrue();
    columns["signatory_kind"].Nullable.ShouldBeFalse();
  }

  /// <summary>
  /// Ce qui fait une preuve n'est jamais nul : le dossier, l'instant, le fait, le signataire.
  /// </summary>
  [Fact]
  public async Task LeavesNothingOptionalThatAProofIsMadeOf()
  {
    var columns = await ColumnsAsync();

    columns["entry_id"].Nullable.ShouldBeFalse();
    columns["case_id"].Nullable.ShouldBeFalse();
    columns["occurred_at"].Nullable.ShouldBeFalse();
    columns["fact"].Nullable.ShouldBeFalse();

    columns["occurred_at"].DataType.ShouldBe("timestamp with time zone");
  }

  /// <summary>
  /// <b>Aucune clé étrangère vers <c>cases</c>.</b> Ce n'est pas un oubli : le <c>EvidenceLog</c> survit
  /// au dossier de cinq ans, et une contrainte référentielle rendrait la clôture impossible — ou,
  /// pire, emporterait la preuve avec le dossier qu'elle sert à défendre.
  /// </summary>
  [Fact]
  public async Task TiesTheProofToNoForeignKeyThatTheClosureWouldHaveToBreak()
  {
    var constraints = await ScalarListAsync(
      """
      select constraint_type
      from information_schema.table_constraints
      where table_name = 'evidence_log_entries' and constraint_type = 'FOREIGN KEY'
      """);

    constraints.ShouldBeEmpty();
  }

  /// <summary>
  /// La ligne fait l'aller-retour, et <b>elle n'a lu aucune ligne antérieure pour s'écrire</b> :
  /// deux ouvertures successives écrivent deux lignes qui ne se connaissent pas.
  /// </summary>
  [Fact]
  public async Task AppendsALineWithoutEverReadingTheOneBefore()
  {
    await using var dbContext = postgres.NewDbContext();

    var ledger = new EvidenceLog(dbContext);
    var first = CaseId.Next();
    var second = CaseId.Next();

    await ledger.AppendAsync(EvidenceLogEntry.CaseOpened(
      first,
      Opened,
      Signatory.Application,
      IdentityDeclaration.ApplicationSession,
      designationCount: 2,
      reception: ReceptionDate.Declared(Opened)));

    await ledger.AppendAsync(EvidenceLogEntry.CaseOpened(
      second,
      Opened,
      Signatory.Operator("Claire Berger", SignerVerification.Unauthenticated),
      IdentityDeclaration.Unverified,
      designationCount: 0,
      reception: ReceptionDate.Defaulted(Opened)));

    await using var reread = postgres.NewDbContext();

    var lines = await reread.Set<EvidenceLogRow>()
      .AsNoTracking()
      .Where(row => row.CaseId == first.Value || row.CaseId == second.Value)
      .ToListAsync();

    lines.Count.ShouldBe(2);

    var byApplication = lines.Single(row => row.CaseId == first.Value);
    byApplication.SignatoryKind.ShouldBe("Application");
    byApplication.SignatoryName.ShouldBeNull();
    byApplication.DesignationCount.ShouldBe(2);
    byApplication.Fact.ShouldBe("CaseOpened");

    var byOperator = lines.Single(row => row.CaseId == second.Value);
    byOperator.SignatoryKind.ShouldBe("Operator");
    byOperator.SignatoryName.ShouldBe("Claire Berger");
    byOperator.IdentityDeclaration.ShouldBe("Unverified");

    // Le nom et son régime descendent ensemble : un nom sans régime serait relu comme une
    // identification, et la date tenue pour défaut serait relue comme un fait déclaré.
    byOperator.SignerVerification.ShouldBe("Unauthenticated");
    byOperator.ReceptionWasDefaulted.ShouldBe(true);
    byApplication.SignerVerification.ShouldBeNull();
    byApplication.ReceptionWasDefaulted.ShouldBe(false);
  }

  /// <summary>
  /// <b>L'adaptateur n'expose qu'un ajout</b> — ni mise à jour, ni suppression ligne à ligne, ni
  /// relecture, pas même privée. Ce que le type ne sait pas faire, personne n'aura à jurer qu'il ne
  /// l'a pas fait.
  /// </summary>
  [Fact]
  public void OffersNoUpdateAndNoLineByLineDeletion()
  {
    typeof(EvidenceLog)
      .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                  | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static)
      .Where(method => method.DeclaringType == typeof(EvidenceLog))
      .Select(method => method.Name)
      .ShouldBe(["AppendAsync", "RowOf"], ignoreOrder: true);

    // La ligne n'est jamais déclarée agrégat racine : le dépôt générique lui aurait rendu la mise à
    // jour et la suppression que la définition du EvidenceLog ferme.
    typeof(EvidenceLogRow).IsAssignableTo(typeof(IAggregateRoot)).ShouldBeFalse();

    // Et le contexte n'expose aucun `DbSet` du EvidenceLog : il en existe un pour la trace d'audit, qui
    // n'a qu'un invariant d'écriture seule, mais un `DbSet` public rendrait ici `Remove` et
    // `Update` à quiconque tient le contexte — c'est-à-dire à tout le service.
    typeof(AppDbContext).GetProperties()
      .Select(property => property.PropertyType)
      .ShouldNotContain(typeof(DbSet<EvidenceLogRow>));
  }

  /// <summary>
  /// <b>Il n'existe aucun dépôt de <c>Claim</c> ni de <c>Step</c>, ni aucun chemin d'écriture vers
  /// eux hors de la racine.</b> Ils sont <em>possédés</em> par le <c>Case</c> : EF Core refuse de
  /// les tenir pour des types interrogeables à part entière, et la règle cesse d'être une
  /// discipline pour devenir une impossibilité.
  /// </summary>
  [Fact]
  public void ReachesAClaimAndAStepThroughTheRootOrNotAtAll()
  {
    using var dbContext = postgres.NewDbContext();

    dbContext.Model.FindEntityType(typeof(Claim))!.IsOwned().ShouldBeTrue();
    dbContext.Model.FindEntityType(typeof(Step))!.IsOwned().ShouldBeTrue();
    dbContext.Model.FindEntityType(typeof(Locating))!.IsOwned().ShouldBeTrue();
    dbContext.Model.FindEntityType(typeof(Reservation))!.IsOwned().ShouldBeTrue();
    dbContext.Model.FindEntityType(typeof(OpenQuestion))!.IsOwned().ShouldBeTrue();
    dbContext.Model.FindEntityType(typeof(OpaqueReference))!.IsOwned().ShouldBeTrue();

    // `Designation` est possédée à DEUX endroits — le sac du dossier, et ce qu'une réserve propose —
    // et EF en tient donc deux types, un par propriétaire. Aucun des deux n'est interrogeable seul.
    var designations = dbContext.Model.GetEntityTypes()
      .Where(owned => owned.ClrType == typeof(Designation))
      .ToArray();

    designations.Length.ShouldBe(2);
    designations.ShouldAllBe(owned => owned.IsOwned());

    dbContext.Model.FindEntityType(typeof(Case))!.IsOwned().ShouldBeFalse();

    // Et aucun DbSet ne les expose : le seul dépôt de ce contexte est celui des Case.
    typeof(AppDbContext).GetProperties()
      .Select(property => property.PropertyType)
      .ShouldNotContain(type =>
        type == typeof(DbSet<Claim>)
        || type == typeof(DbSet<Step>)
        || type == typeof(DbSet<Locating>)
        || type == typeof(DbSet<Reservation>));
  }

  private async Task<Dictionary<string, ColumnShape>> ColumnsAsync()
  {
    await using var dbContext = postgres.NewDbContext();
    await using var command = await CommandAsync(
      dbContext,
      """
      select column_name, is_nullable, data_type, character_maximum_length
      from information_schema.columns
      where table_name = 'evidence_log_entries'
      """);

    var columns = new Dictionary<string, ColumnShape>(StringComparer.Ordinal);

    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
      columns[reader.GetString(0)] = new ColumnShape(
        Nullable: reader.GetString(1) == "YES",
        DataType: reader.GetString(2),
        MaxLength: reader.IsDBNull(3) ? null : reader.GetInt32(3));
    }

    return columns;
  }

  private async Task<List<string>> ScalarListAsync(string sql)
  {
    await using var dbContext = postgres.NewDbContext();
    await using var command = await CommandAsync(dbContext, sql);

    var values = new List<string>();

    await using var reader = await command.ExecuteReaderAsync();

    while (await reader.ReadAsync())
    {
      values.Add(reader.GetString(0));
    }

    return values;
  }

  private static async Task<DbCommand> CommandAsync(AppDbContext dbContext, string sql)
  {
    var connection = dbContext.Database.GetDbConnection();
    await connection.OpenAsync();

    var command = connection.CreateCommand();
    command.CommandText = sql;

    return command;
  }

  /// <summary>Ce qu'on demande à une colonne : sa nullité, son type, et sa borne s'il y en a une.</summary>
  private sealed record ColumnShape(bool Nullable, string DataType, int? MaxLength);
}
