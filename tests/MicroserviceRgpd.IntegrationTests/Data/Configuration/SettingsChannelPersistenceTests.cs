using System.Data.Common;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;

namespace MicroserviceRgpd.IntegrationTests.Data.Configuration;

/// <summary>
/// Le <b>canal d'exercice</b> tel que la base le porte : douze colonnes de routage en plus des six
/// d'adresse, et l'exclusivité « un droit, un seul canal » vérifiée <b>en base</b> et non seulement
/// dans l'agrégat (ADR-0027).
/// </summary>
/// <remarks>
/// <para>
/// <b>Un vrai PostgreSQL parce que rien d'autre ne prouve ce qui est en jeu</b> : le type <c>text</c>
/// sans longueur déclarée, la nullité colonne par colonne, et surtout ce qui <i>reste</i> écrit après
/// un remplacement de canal. Une valeur dormante ne se voit pas dans l'objet qui vient de l'effacer ;
/// elle se voit dans la ligne.
/// </para>
/// <para>
/// Le schéma vient des migrations du dépôt, comme pour toute cette suite : c'est la migration qui
/// sera appliquée en production que ces vérifications portent.
/// </para>
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public class SettingsChannelPersistenceTests(PostgreSqlFixture postgres)
{
  private static readonly EndpointUrl Address = EndpointUrl.From("https://brocanto.example.fr/rgpd/acces");

  private static readonly ExerciseChannel Routing = new ExerciseChannel.RabbitMq(
    new RabbitMqRouting(ExchangeName.From("rgpd.exercice"), RoutingKey.From("droit.accès")));

  /// <summary>
  /// <b>Trois colonnes par droit, et rien de plus</b> : l'adresse, l'exchange et la routing key,
  /// toutes nullables. ⚠️ <b>Aucune colonne discriminante</b> ne nomme le canal en vigueur, et aucun
  /// nom de colonne ne préfixe le broker.
  /// </summary>
  [Fact]
  public async Task CarriesThreeNullableColumnsPerRightAndNoDiscriminator()
  {
    var columns = await ColumnsAsync();

    columns.Keys.Order(StringComparer.Ordinal).ShouldBe(
      [
        "access_exchange", "access_routing_key", "access_url",
        "erasure_exchange", "erasure_routing_key", "erasure_url",
        "id",
        "objection_exchange", "objection_routing_key", "objection_url",
        "portability_exchange", "portability_routing_key", "portability_url",
        "rectification_exchange", "rectification_routing_key", "rectification_url",
        "restriction_exchange", "restriction_routing_key", "restriction_url",
      ]);

    columns
      .Where(column => column.Key != "id")
      .ShouldAllBe(column => column.Value.Nullable);

    columns.Keys.ShouldNotContain(name => name.Contains("rabbit", StringComparison.OrdinalIgnoreCase));
    columns.Keys.ShouldNotContain(name => name.Contains("channel", StringComparison.OrdinalIgnoreCase));
    columns.Keys.ShouldNotContain(name => name.Contains("canal", StringComparison.OrdinalIgnoreCase));
  }

  /// <summary>
  /// ⚠️ <b>Les colonnes du routage sont en <c>text</c> sans longueur déclarée</b>, là où l'adresse
  /// porte la sienne : la contrainte du domaine compte des <b>octets UTF-8</b>, quand une longueur en
  /// base compterait des caractères.
  /// </summary>
  [Fact]
  public async Task DeclaresTheRoutingColumnsAsTextWithoutALength()
  {
    var columns = await ColumnsAsync();

    foreach (var name in new[] { "access_exchange", "access_routing_key" })
    {
      columns[name].DataType.ShouldBe("text");
      columns[name].MaxLength.ShouldBeNull();
    }

    columns["access_url"].MaxLength.ShouldBe(EndpointUrl.MaxLength);
  }

  /// <summary>
  /// <b>Un routage enregistré se relit</b> après un aller-retour en base, exchange et routing key
  /// compris — et les cinq autres droits restent « non configuré ».
  /// </summary>
  [Fact]
  public async Task ReadsBackARoutingAfterARoundTrip()
  {
    await ClearAsync();

    var written = Settings.Unconfigured();
    written.SetChannel(DataSubjectRight.Erasure, Routing);
    await SaveAsync(written);

    var relu = await ReadAsync();

    relu.ChannelFor(DataSubjectRight.Erasure).ShouldBe(Routing);
    relu.Rights
      .Where(entry => entry.Right != DataSubjectRight.Erasure)
      .ShouldAllBe(entry => !entry.IsConfigured);
  }

  /// <summary>
  /// ⚠️ <b>Remplacer un canal efface l'autre en base</b> : après une adresse posée sur un droit qui
  /// portait un routage, les deux colonnes du routage sont à <c>NULL</c> dans la ligne. C'est ce que
  /// l'agrégat seul ne peut pas prouver.
  /// </summary>
  [Fact]
  public async Task ErasesTheOtherChannelInTheRowWhenAChannelReplacesIt()
  {
    await ClearAsync();

    var written = Settings.Unconfigured();
    written.SetChannel(DataSubjectRight.Access, Routing);
    await SaveAsync(written);

    (await CellAsync("access_exchange")).ShouldBe("rgpd.exercice");
    (await CellAsync("access_routing_key")).ShouldBe("droit.accès");

    var replacing = await ReadAsync();
    replacing.SetChannel(DataSubjectRight.Access, new ExerciseChannel.HttpEndpoint(Address));
    await SaveChangesAsync(replacing);

    (await CellAsync("access_url")).ShouldBe(Address.Value);
    (await CellAsync("access_exchange")).ShouldBeNull();
    (await CellAsync("access_routing_key")).ShouldBeNull();

    var back = await ReadAsync();
    back.SetChannel(DataSubjectRight.Access, Routing);
    await SaveChangesAsync(back);

    (await CellAsync("access_url")).ShouldBeNull();
    (await CellAsync("access_exchange")).ShouldBe("rgpd.exercice");
  }

  /// <summary>
  /// <b>Effacer un droit rend ses trois colonnes à <c>NULL</c></b>, et ne touche aucune colonne d'un
  /// autre droit : un routage ne survit pas en réserve à l'effacement du droit voisin.
  /// </summary>
  [Fact]
  public async Task ClearingOneRightLeavesTheOtherRightsRowUntouched()
  {
    await ClearAsync();

    var written = Settings.Unconfigured();
    written.SetChannel(DataSubjectRight.Access, Routing);
    written.SetChannel(DataSubjectRight.Objection, new ExerciseChannel.HttpEndpoint(Address));
    await SaveAsync(written);

    var clearing = await ReadAsync();
    clearing.ClearChannel(DataSubjectRight.Access);
    await SaveChangesAsync(clearing);

    (await CellAsync("access_exchange")).ShouldBeNull();
    (await CellAsync("access_routing_key")).ShouldBeNull();
    (await CellAsync("access_url")).ShouldBeNull();
    (await CellAsync("objection_url")).ShouldBe(Address.Value);
  }

  /// <summary>
  /// <b>Naissance paresseuse, jusqu'en base</b> : un Paramétrage vierge rend les six droits « non
  /// configuré » <b>sans qu'aucune ligne n'existe</b>.
  /// </summary>
  [Fact]
  public async Task PersistsNoRowForAVirginParametrage()
  {
    await ClearAsync();

    await using var dbContext = postgres.NewDbContext();

    (await dbContext.Settings.CountAsync()).ShouldBe(0);
    Settings.Unconfigured().Rights.ShouldAllBe(entry => !entry.IsConfigured);
  }

  /// <summary>
  /// ⚠️ <b>Un routage à demi écrit lève à la lecture</b>, au lieu de se lire comme « non configuré ».
  /// C'est l'état illégal que les propriétés plates laissent représentable, et cette ligne — plantée
  /// à la main, comme le ferait un correctif en base — est le seul endroit d'où il puisse se voir.
  /// Le lire en silence ferait disparaître un réglage au prochain enregistrement.
  /// </summary>
  [Fact]
  public async Task RefusesToReadARowCarryingHalfARouting()
  {
    await ClearAsync();
    await ExecuteAsync(
      $"insert into settings (id, access_exchange) values ({Settings.SingletonId}, 'rgpd.exercice')");

    var relu = await ReadAsync();

    Should.Throw<InvalidOperationException>(() => relu.ChannelFor(DataSubjectRight.Access));

    await ClearAsync();
    await ExecuteAsync(
      $"insert into settings (id, access_routing_key) values ({Settings.SingletonId}, 'droit.acces')");

    var half = await ReadAsync();

    Should.Throw<InvalidOperationException>(() => half.ChannelFor(DataSubjectRight.Access));
  }

  private async Task ExecuteAsync(string sql)
  {
    await using var dbContext = postgres.NewDbContext();
    await dbContext.Database.ExecuteSqlRawAsync(sql);
  }

  private async Task ClearAsync()
  {
    await using var dbContext = postgres.NewDbContext();
    await dbContext.Database.ExecuteSqlRawAsync("delete from settings");
  }

  private async Task SaveAsync(Settings settings)
  {
    await using var dbContext = postgres.NewDbContext();
    dbContext.Settings.Add(settings);
    await dbContext.SaveChangesAsync();
  }

  /// <summary>
  /// Le Paramétrage relu par un contexte neuf : c'est l'aller-retour qui est en jeu, et un contexte
  /// qui suit déjà l'objet écrit le rendrait de mémoire.
  /// </summary>
  private async Task<Settings> ReadAsync()
  {
    await using var dbContext = postgres.NewDbContext();

    var persisted = await dbContext.Settings.SingleOrDefaultAsync();

    return persisted.ShouldNotBeNull();
  }

  /// <summary>
  /// Enregistre un Paramétrage relu par <see cref="ReadAsync"/> — dont le contexte est déjà disposé :
  /// on le rattache pour que seules ses colonnes modifiées partent en base.
  /// </summary>
  private async Task SaveChangesAsync(Settings settings)
  {
    await using var dbContext = postgres.NewDbContext();
    dbContext.Settings.Update(settings);
    await dbContext.SaveChangesAsync();
  }

  private async Task<string?> CellAsync(string column)
  {
    await using var dbContext = postgres.NewDbContext();
    await using var command = await CommandAsync(dbContext, $"select {column} from settings");

    var value = await command.ExecuteScalarAsync();

    return value is DBNull or null ? null : (string)value;
  }

  private async Task<Dictionary<string, ColumnShape>> ColumnsAsync()
  {
    await using var dbContext = postgres.NewDbContext();
    await using var command = await CommandAsync(
      dbContext,
      """
      select column_name, is_nullable, data_type, character_maximum_length
      from information_schema.columns
      where table_name = 'settings'
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
