using MicroserviceRgpd.Core.Configuration;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MicroserviceRgpd.Infrastructure.Data.Configuration;

/// <summary>
/// La table <c>settings</c> : <b>une seule ligne</b> pour le Paramétrage, et <b>trois colonnes par
/// droit</b> du périmètre — l'adresse, l'exchange et la routing key —, toutes <b>nullables</b>. Un
/// droit dont les trois sont à <c>NULL</c> est « non configuré », état parfaitement normal.
/// </summary>
/// <remarks>
/// <para>
/// <b>La clé primaire est figée</b> (<see cref="Settings.SingletonId"/>) et déclarée <c>ValueGeneratedNever</c> :
/// la configuration est un singleton, sa clé ne s'engendre pas. Une seule ligne existe, matérialisée
/// paresseusement au premier enregistrement.
/// </para>
/// <para>
/// <b>Le <c>snake_case</c> est déclaré ici, explicitement</b>, comme sur les autres tables du dépôt :
/// un paquet de conventions déciderait en silence du nom de toutes les colonnes à venir.
/// </para>
/// <para>
/// ⚠️ <b>Aucune colonne discriminante de canal</b> (ADR-0027) : l'exclusivité « un droit, un seul
/// canal » est tenue par l'agrégat, au seul endroit qui écrit, et la présence d'un exchange <i>est</i>
/// le canal RabbitMQ. Une colonne qui redirait le canal créerait un second état illégal représentable.
/// </para>
/// <para>
/// ⚠️ <b>Les colonnes du routage n'ont aucune longueur déclarée</b>, là où celles d'adresse en portent
/// une : la contrainte d'un <see cref="ExchangeName"/> compte des <b>octets UTF-8</b>, quand une
/// longueur en base compterait des caractères. Déclarer 255 en base donnerait à lire une limite qui
/// n'est pas celle qu'on applique.
/// </para>
/// <para>
/// <b>Aucune colonne pour <see cref="DataSubjectRight.OutOfScope"/></b> : ce n'est pas un droit qu'on
/// exerce, et le domaine refuse déjà de lui porter un canal. La table n'en garde donc aucune trace
/// muette.
/// </para>
/// </remarks>
public sealed class SettingsConfiguration : IEntityTypeConfiguration<Settings>
{
  /// <summary>
  /// Convertit l'adresse optionnelle vers la colonne, et retour — <c>null</c> vaut « non configuré ».
  /// </summary>
  private static readonly ValueConverter<EndpointUrl?, string?> EndpointConversion = new(
    endpoint => endpoint == null ? null : endpoint.Value.Value,
    value => value == null ? null : EndpointUrl.From(value));

  /// <summary>Convertit l'exchange optionnel vers la colonne, et retour.</summary>
  private static readonly ValueConverter<ExchangeName?, string?> ExchangeConversion = new(
    exchange => exchange == null ? null : exchange.Value.Value,
    value => value == null ? null : ExchangeName.From(value));

  /// <summary>Convertit la routing key optionnelle vers la colonne, et retour.</summary>
  private static readonly ValueConverter<RoutingKey?, string?> RoutingKeyConversion = new(
    routingKey => routingKey == null ? null : routingKey.Value.Value,
    value => value == null ? null : RoutingKey.From(value));

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<Settings> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("settings");

    builder.HasKey(settings => settings.Id).HasName("pk_settings");
    builder.Property(settings => settings.Id)
      .HasColumnName("id")
      .ValueGeneratedNever();

    // La projection des six droits se recalcule à la lecture depuis les colonnes — ce n'est ni une
    // colonne ni une navigation, et EF n'a pas à la matérialiser.
    builder.Ignore(settings => settings.Rights);

    Endpoint(builder, settings => settings.AccessUrl, "access_url");
    Exchange(builder, settings => settings.AccessExchange, "access_exchange");
    RoutingKeyColumn(builder, settings => settings.AccessRoutingKey, "access_routing_key");

    Endpoint(builder, settings => settings.RectificationUrl, "rectification_url");
    Exchange(builder, settings => settings.RectificationExchange, "rectification_exchange");
    RoutingKeyColumn(builder, settings => settings.RectificationRoutingKey, "rectification_routing_key");

    Endpoint(builder, settings => settings.ErasureUrl, "erasure_url");
    Exchange(builder, settings => settings.ErasureExchange, "erasure_exchange");
    RoutingKeyColumn(builder, settings => settings.ErasureRoutingKey, "erasure_routing_key");

    Endpoint(builder, settings => settings.RestrictionUrl, "restriction_url");
    Exchange(builder, settings => settings.RestrictionExchange, "restriction_exchange");
    RoutingKeyColumn(builder, settings => settings.RestrictionRoutingKey, "restriction_routing_key");

    Endpoint(builder, settings => settings.PortabilityUrl, "portability_url");
    Exchange(builder, settings => settings.PortabilityExchange, "portability_exchange");
    RoutingKeyColumn(builder, settings => settings.PortabilityRoutingKey, "portability_routing_key");

    Endpoint(builder, settings => settings.ObjectionUrl, "objection_url");
    Exchange(builder, settings => settings.ObjectionExchange, "objection_exchange");
    RoutingKeyColumn(builder, settings => settings.ObjectionRoutingKey, "objection_routing_key");
  }

  /// <summary>
  /// Une colonne d'adresse : nullable, plafonnée comme l'<see cref="EndpointUrl"/> elle-même.
  /// </summary>
  private static void Endpoint(
    EntityTypeBuilder<Settings> builder,
    System.Linq.Expressions.Expression<Func<Settings, EndpointUrl?>> property,
    string column)
  {
    builder.Property(property)
      .HasColumnName(column)
      .HasMaxLength(EndpointUrl.MaxLength)
      .HasConversion(EndpointConversion);
  }

  /// <summary>
  /// Une colonne d'exchange : nullable et <b>sans longueur déclarée</b> — la contrainte de l'<see
  /// cref="ExchangeName"/> compte des octets UTF-8, pas des caractères.
  /// </summary>
  private static void Exchange(
    EntityTypeBuilder<Settings> builder,
    System.Linq.Expressions.Expression<Func<Settings, ExchangeName?>> property,
    string column)
  {
    builder.Property(property)
      .HasColumnName(column)
      .HasConversion(ExchangeConversion);
  }

  /// <summary>
  /// Une colonne de routing key : nullable et <b>sans longueur déclarée</b>, pour le motif de
  /// <see cref="Exchange"/>.
  /// </summary>
  private static void RoutingKeyColumn(
    EntityTypeBuilder<Settings> builder,
    System.Linq.Expressions.Expression<Func<Settings, RoutingKey?>> property,
    string column)
  {
    builder.Property(property)
      .HasColumnName(column)
      .HasConversion(RoutingKeyConversion);
  }
}
