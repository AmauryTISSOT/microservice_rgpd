using Ardalis.Result;
using MicroserviceRgpd.Core.Configuration;
using MicroserviceRgpd.Core.SharedKernel;
using MicroserviceRgpd.Infrastructure.Data;
using MicroserviceRgpd.UseCases.Configuration.ClearRightChannel;
using MicroserviceRgpd.UseCases.Configuration.ReadSettings;
using MicroserviceRgpd.UseCases.Configuration.SetRightRabbitMqRouting;

namespace MicroserviceRgpd.IntegrationTests.UseCases.Configuration;

/// <summary>
/// Ce que les use cases d'écriture d'un canal font <b>à la ligne</b>, et que rien d'autre ne
/// prouve : la naissance de la ligne unique, et ce qu'une écriture laisse debout du travail d'une
/// autre.
/// </summary>
/// <remarks>
/// <b>Deux contextes distincts font les deux écritures concurrentes.</b> Un contexte unique rendrait
/// le scénario de mémoire : les deux gestionnaires liraient le même objet suivi, et la question —
/// quelles colonnes partent en base ? — ne se poserait jamais. Le reste du comportement de ces
/// gestionnaires — refus d'un droit forgé, effacement des deux canaux, service vierge — se tient à
/// l'unité, sans container.
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public class ChannelWritesTests(PostgreSqlFixture postgres)
{
  private static readonly RabbitMqRouting Routing =
    new(ExchangeName.From("rgpd.exercice"), RoutingKey.From("droit.acces"));

  /// <summary>
  /// <b>Première écriture sur un service vierge</b> : la ligne naît, et le routage se relit par la
  /// lecture du Paramétrage.
  /// </summary>
  [Fact]
  public async Task CreatesTheRowOnAVirginServiceAndTheRoutingReadsBack()
  {
    await ClearAsync();

    await using (var writing = postgres.NewDbContext())
    {
      (await SetRoutingAsync(writing, DataSubjectRight.Access)).IsSuccess.ShouldBeTrue();
    }

    await using var reading = postgres.NewDbContext();

    (await reading.Settings.CountAsync()).ShouldBe(1);
    (await ReadAsync(reading)).ChannelFor(DataSubjectRight.Access)
      .ShouldBe(new ExerciseChannel.RabbitMq(Routing));
  }

  /// <summary>
  /// ⚠️ <b>Un enregistrement concurrent sur un autre droit n'est pas écrasé.</b> Deux contextes
  /// lisent la même ligne ; l'un pose un routage, l'autre efface un droit voisin et enregistre en
  /// dernier. Le suivi des modifications n'envoie que les colonnes du droit touché : les deux gestes
  /// survivent. Un <c>Update</c> global aurait réécrit les cinq autres droits avec la valeur lue un
  /// instant plus tôt, et le premier enregistrement aurait disparu en silence.
  /// </summary>
  [Fact]
  public async Task DoesNotOverwriteAConcurrentWriteOnAnotherRight()
  {
    await ClearAsync();
    await using (var seeding = postgres.NewDbContext())
    {
      (await SetRoutingAsync(seeding, DataSubjectRight.Objection)).IsSuccess.ShouldBeTrue();
    }

    await using var posing = postgres.NewDbContext();
    await using var clearing = postgres.NewDbContext();

    var posingRepository = new EfRepository<Settings>(posing);
    var clearingRepository = new EfRepository<Settings>(clearing);

    // Les deux lisent avant que l'un ou l'autre n'écrive : c'est la fenêtre où une écriture
    // aveugle écraserait le travail de l'autre.
    await posingRepository.GetByIdAsync(Settings.SingletonId);
    await clearingRepository.GetByIdAsync(Settings.SingletonId);

    (await new SetRightRabbitMqRoutingHandler(posingRepository).Handle(
      new SetRightRabbitMqRoutingCommand(DataSubjectRight.Access, Routing),
      CancellationToken.None)).IsSuccess.ShouldBeTrue();

    (await new ClearRightChannelHandler(clearingRepository).Handle(
      new ClearRightChannelCommand(DataSubjectRight.Objection),
      CancellationToken.None)).IsSuccess.ShouldBeTrue();

    await using var reading = postgres.NewDbContext();
    var reread = await ReadAsync(reading);

    reread.ChannelFor(DataSubjectRight.Access).ShouldBe(new ExerciseChannel.RabbitMq(Routing));
    reread.ChannelFor(DataSubjectRight.Objection).ShouldBe(ExerciseChannel.NotConfigured.Instance);
  }

  private static ValueTask<Result> SetRoutingAsync(AppDbContext dbContext, DataSubjectRight right) =>
    new SetRightRabbitMqRoutingHandler(new EfRepository<Settings>(dbContext)).Handle(
      new SetRightRabbitMqRoutingCommand(right, Routing),
      CancellationToken.None);

  private static ValueTask<Settings> ReadAsync(AppDbContext dbContext) =>
    new ReadSettingsHandler(new EfRepository<Settings>(dbContext)).Handle(
      new ReadSettingsQuery(),
      CancellationToken.None);

  private async Task ClearAsync()
  {
    await using var dbContext = postgres.NewDbContext();
    await dbContext.Database.ExecuteSqlRawAsync("delete from settings");
  }
}
