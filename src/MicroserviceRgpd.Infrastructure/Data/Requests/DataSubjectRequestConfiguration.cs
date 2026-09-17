using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Infrastructure.Data.Requests;

/// <summary>
/// La table <c>data_subject_requests</c> : <b>une ligne par demande enregistrée</b>, et rien de plus.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>La colonne <c>status</c> tient un état, pas la trace d'un <c>Gesture</c></b> (ADR-0021) :
/// elle est ce que le tableau lira, et non la promesse d'une instruction qui n'existe pas.
/// </para>
/// <para>
/// <b>Les vocabulaires fermés sont stockés par leur nom, jamais par un entier</b> — l'origine comme
/// <c>ListingOrigin</c>, le statut de même, le droit sous son nom canonique, celui du fil JSON. Un
/// entier lierait le schéma à l'ordre de déclaration et rendrait la table illisible.
/// </para>
/// <para>
/// <b>Les textes sont en <c>text</c>, sans plafond déclaré</b> : les plafonds sont ceux des objets
/// valeurs, qui refusent la saisie avant qu'elle n'atteigne la base. Les recopier en SQL aurait
/// exigé une migration pour chaque plafond qui bouge.
/// </para>
/// <para>
/// <b>Le <c>snake_case</c> est déclaré ici, explicitement</b>, comme sur toutes les autres tables du
/// dépôt, colonne par colonne.
/// </para>
/// </remarks>
public sealed class DataSubjectRequestConfiguration : IEntityTypeConfiguration<DataSubjectRequest>
{
  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<DataSubjectRequest> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("data_subject_requests");

    // La clé est forgée par le service en GUID v7 ; la base ne la génère jamais.
    builder.HasKey(request => request.Id).HasName("pk_data_subject_requests");
    builder.Property(request => request.Id)
      .HasColumnName("id")
      .HasConversion(id => id.Value, value => DataSubjectRequestId.From(value))
      .ValueGeneratedNever();

    builder.Property(request => request.Origin)
      .HasColumnName("origin")
      .HasConversion(origin => origin.Name, name => Origin.FromName(name))
      .IsRequired();

    builder.Property(request => request.ReceivedOn).HasColumnName("received_on").IsRequired();

    builder.Property(request => request.ResponseDeadline).HasColumnName("response_deadline").IsRequired();

    builder.Property(request => request.LastName)
      .HasColumnName("last_name")
      .HasConversion(
        lastName => lastName == null ? null : lastName.Value.Value,
        value => value == null ? null : LastName.From(value));

    builder.Property(request => request.FirstName)
      .HasColumnName("first_name")
      .HasConversion(
        firstName => firstName == null ? null : firstName.Value.Value,
        value => value == null ? null : FirstName.From(value));

    builder.Property(request => request.Email)
      .HasColumnName("email")
      .HasConversion(
        email => email == null ? null : email.Value.Value,
        value => value == null ? null : EmailAddress.From(value));

    builder.Property(request => request.IdentityVerified).HasColumnName("identity_verified").IsRequired();

    builder.Property(request => request.Message)
      .HasColumnName("message")
      .HasConversion(message => message.Value, value => RequestMessage.From(value))
      .IsRequired();

    builder.Property(request => request.Right)
      .HasColumnName("data_subject_right")
      .HasConversion(right => right.Name, name => DataSubjectRight.FromName(name))
      .IsRequired();

    builder.Property(request => request.Status)
      .HasColumnName("status")
      .HasConversion(status => status.Name, name => RequestStatus.FromName(name))
      .IsRequired();

    builder.Property(request => request.CreatedBy).HasColumnName("created_by").IsRequired();

    builder.Property(request => request.CreatedAt).HasColumnName("created_at").IsRequired();

    // ⚠️ L'empreinte de modification est nullable des deux côtés : le nul dit « jamais modifiée »,
    // et c'est la vérité de toute demande qui n'a connu que sa réception.
    builder.Property(request => request.ModifiedBy).HasColumnName("modified_by");

    builder.Property(request => request.ModifiedAt).HasColumnName("modified_at");

    // ⚠️ LES QUATRE COLONNES DE LA PROLONGATION SONT NULLABLES, ET LE NUL DIT « JAMAIS PROLONGÉE ».
    // Elles sont écrites ensemble, ou pas du tout (ADR-0029).
    //
    // ⚠️ `initial_response_deadline` NE PREND PAS LE PRÉFIXE `extension_` : c'est une date limite de
    // réponse — celle qui valait avant —, pas un attribut de l'acte. `extended_at` suit `created_at`
    // et `modified_at`.
    builder.Property(request => request.InitialResponseDeadline).HasColumnName("initial_response_deadline");

    builder.Property(request => request.ExtensionGround)
      .HasColumnName("extension_ground")
      .HasConversion(
        ground => ground == null ? null : ground.Name,
        name => name == null ? null : ExtensionGround.FromName(name));

    builder.Property(request => request.ExtensionJustification)
      .HasColumnName("extension_justification")
      .HasConversion(
        justification => justification == null ? null : justification.Value.Value,
        value => value == null ? null : ExtensionJustification.From(value));

    builder.Property(request => request.ExtendedAt).HasColumnName("extended_at");
  }
}
