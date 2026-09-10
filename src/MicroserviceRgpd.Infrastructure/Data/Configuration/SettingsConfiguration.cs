using MicroserviceRgpd.Core.Configuration;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MicroserviceRgpd.Infrastructure.Data.Configuration;

/// <summary>
/// La table <c>settings</c> : <b>une seule ligne</b> pour le Paramétrage, et six colonnes — une par
/// droit du périmètre —, toutes <b>nullables</b>. Une colonne à <c>NULL</c> est un droit « non
/// configuré », état parfaitement normal.
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
/// <b>Aucune colonne pour <see cref="DataSubjectRight.OutOfScope"/></b> : ce n'est pas un droit qu'on
/// exerce, et le domaine refuse déjà de lui porter une adresse. La table n'en garde donc aucune
/// trace muette.
/// </para>
/// </remarks>
public sealed class SettingsConfiguration : IEntityTypeConfiguration<Settings>
{
  /// <summary>Convertit l'adresse optionnelle vers la colonne, et retour — <c>null</c> vaut « non configuré ».</summary>
  private static readonly ValueConverter<EndpointUrl?, string?> EndpointConversion = new(
    endpoint => endpoint == null ? null : endpoint.Value.Value,
    value => value == null ? null : EndpointUrl.From(value));

  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<Settings> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("settings");

    builder.HasKey(settings => settings.Id).HasName("pk_settings");
    builder.Property(settings => settings.Id)
      .HasColumnName("id")
      .ValueGeneratedNever();

    // La projection des six droits se recalcule à la lecture depuis les six colonnes — ce n'est ni
    // une colonne ni une navigation, et EF n'a pas à la matérialiser.
    builder.Ignore(settings => settings.Rights);

    Endpoint(builder, settings => settings.Access, "access_url");
    Endpoint(builder, settings => settings.Rectification, "rectification_url");
    Endpoint(builder, settings => settings.Erasure, "erasure_url");
    Endpoint(builder, settings => settings.Restriction, "restriction_url");
    Endpoint(builder, settings => settings.Portability, "portability_url");
    Endpoint(builder, settings => settings.Objection, "objection_url");
  }

  /// <summary>
  /// Une colonne d'adresse : nullable, plafonnée comme l'<see cref="EndpointUrl"/> elle-même, et
  /// nommée en <c>snake_case</c>. Les six se déclarent d'un seul geste pour que la table reste lisible
  /// d'un bloc.
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
}
