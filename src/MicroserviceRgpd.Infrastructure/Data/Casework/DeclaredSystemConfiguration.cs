using MicroserviceRgpd.Core.Casework;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MicroserviceRgpd.Infrastructure.Data.Casework;

/// <summary>
/// La table du <c>Manifest</c> : une ligne par <see cref="DeclaredSystem"/>, et rien de plus.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aucune colonne ne nomme une table, une colonne ni un champ du client</b>, et il n'existe
/// <b>aucun emplacement pour un secret</b> d'<c>Adapter</c> — pas de colonne à laisser vide, pas de
/// colonne à chiffrer, pas de colonne à oublier de masquer dans un journal.
/// </para>
/// <para>
/// <b>Le <c>snake_case</c> est déclaré ici, explicitement</b>, comme sur la seule autre table du
/// dépôt : un paquet de conventions déciderait en silence du nom de toutes les tables à venir.
/// </para>
/// <para>
/// <b>Aucun index secondaire.</b> Le catalogue est un paysage de quelques systèmes qu'on lit d'un
/// bloc ; la clé primaire couvre le seul accès ciblé qui existe — la page de révision.
/// </para>
/// </remarks>
public sealed class DeclaredSystemConfiguration : IEntityTypeConfiguration<DeclaredSystem>
{
  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<DeclaredSystem> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("declared_systems");

    // La clé est l'identifiant choisi par l'humain, et jamais un substitut engendré : c'est lui que
    // l'Adapter recevra, et c'est donc lui dont l'unicité doit être tenue par la base plutôt que
    // par la seule lecture préalable du gestionnaire.
    builder.HasKey(system => system.Id).HasName("pk_declared_systems");
    builder.Property(system => system.Id)
      .HasColumnName("id")
      .HasMaxLength(DeclaredSystemId.MaxLength)
      .HasConversion(id => id.Value, value => DeclaredSystemId.From(value))
      .ValueGeneratedNever();

    builder.Property(system => system.Label)
      .HasColumnName("label")
      .HasMaxLength(SystemLabel.MaxLength)
      .HasConversion(label => label.Value, value => SystemLabel.From(value))
      .IsRequired();

    // La prose est obligatoire jusque dans la base : un système sans elle serait un système que la
    // DeliveryLetter ne saurait pas nommer à la personne concernée.
    builder.Property(system => system.Contents)
      .HasColumnName("contents")
      .HasMaxLength(SystemContents.MaxLength)
      .HasConversion(contents => contents.Value, value => SystemContents.From(value))
      .IsRequired();

    // Les capacités descendent sous leurs noms canoniques anglais, comme les droits sur l'autre
    // table : la base n'introduit aucun second vocabulaire, et aucun ordinal d'énumération ne
    // devient un élément du schéma. Un tableau vide est une valeur — c'est le niveau 0 — là où un
    // `null` laisserait croire à une déclaration inachevée.
    builder.Property<string[]>("_capabilities")
      .HasColumnName("capabilities")
      .HasField("_capabilities")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .IsRequired();

    builder.Ignore(system => system.Capabilities);

    // Absente vaut `null`, jamais chaîne vide : une adresse vide ferait croire à une adresse mal
    // saisie là où l'absence d'Adapter est le régime majoritaire et parfaitement normal.
    builder.Property(system => system.AdapterAddress)
      .HasColumnName("adapter_address")
      .HasMaxLength(AdapterAddress.MaxLength)
      .HasConversion(new ValueConverter<AdapterAddress?, string?>(
        address => address == null ? null : address.Value.Value,
        value => value == null ? null : AdapterAddress.From(value)));

    builder.Property(system => system.DeclaredOn).HasColumnName("declared_on").IsRequired();
  }
}
