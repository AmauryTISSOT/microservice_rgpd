using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Infrastructure.Data.Casework;

/// <summary>
/// La table des pièces lues : le triplet qui les identifie, l'<b>enveloppe de transport</b> en deux
/// colonnes, et les octets.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aucune clé étrangère vers <c>cases</c>, et ce n'est pas un oubli.</b> Ces pièces sont
/// <b>hors de l'agrégat</b> et ont leur durée de vie propre : le geste « déclarer remis » les
/// détruira sans réécrire le dossier, et une contrainte référentielle aurait fait dépendre leur
/// effacement de l'état d'une racine qui, elle, survit à la remise. Le <c>case_id</c> est une
/// référence <b>libre</b>.
/// </para>
/// <para>
/// <b>Le triplet est la clé, et aucun identifiant de substitution n'est engendré.</b> Deux pièces
/// pour le même (dossier, droit, système) seraient deux réponses dues à la personne sur la même
/// question : c'est la base qui le rend impossible, et non la seule discipline de l'appelant.
/// </para>
/// <para>
/// ⚠️ <b>Les deux colonnes d'enveloppe sont recopiées, jamais indexées ni contraintes autrement que
/// par leur longueur.</b> Le service ne juge pas un <c>Content-Type</c> et ne connaît aucune liste
/// de types admis : en tenir une ici ferait refuser en base une pièce que l'application a servie.
/// </para>
/// <para>
/// ⚠️ <b>Aucune colonne ne dénombre quoi que ce soit du corps.</b> Ni taille, ni compte de lignes, ni
/// somme de contrôle : le service n'ouvre jamais la pièce, et une colonne qui en dirait quelque
/// chose serait une mesure des données de quelqu'un que rien n'aurait demandée.
/// </para>
/// </remarks>
public sealed class RetrievedDataConfiguration : IEntityTypeConfiguration<RetrievedData>
{
  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<RetrievedData> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("case_retrieved_data");

    builder.Property(piece => piece.Case)
      .HasColumnName("case_id")
      .HasConversion(id => id.Value, value => CaseId.From(value));

    builder.Property(piece => piece.Right)
      .HasColumnName("data_subject_right")
      .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
      .HasConversion(right => right.Name, name => DataSubjectRight.FromName(name));

    builder.Property(piece => piece.DeclaredSystem)
      .HasColumnName("declared_system_id")
      .HasMaxLength(DeclaredSystemId.MaxLength)
      .HasConversion(id => id.Value, value => DeclaredSystemId.From(value));

    builder.HasKey("Case", "Right", "DeclaredSystem").HasName("pk_case_retrieved_data");

    // L'enveloppe est possédée : elle n'a de sens que portée par la pièce, et lui donner une table
    // aurait fait exister un Content-Type sans octets à décrire.
    builder.OwnsOne(piece => piece.Envelope, envelope =>
    {
      envelope.Property(one => one.ContentType)
        .HasColumnName("content_type")
        // La borne est celle que l'enveloppe applique elle-même : une colonne plus étroite que le
        // domaine ferait perdre la pièce à l'écriture, pour un en-tête que personne n'a lu.
        .HasMaxLength(TransportEnvelope.MaxContentTypeLength)
        .IsRequired();

      envelope.Property(one => one.FileName)
        .HasColumnName("file_name")
        .HasMaxLength(TransportEnvelope.MaxFileNameLength)
        .IsRequired();
    });

    builder.Navigation(piece => piece.Envelope).IsRequired();

    // Les octets, tels qu'ils sont arrivés. La colonne est requise et éventuellement vide : la pièce
    // vide est une réponse — « interrogé, rien » — et un NULL l'aurait rendue indiscernable d'une
    // pièce absente, que cette table ne porte justement pas.
    builder.Property(piece => piece.Content).HasColumnName("content").IsRequired();

    builder.Property(piece => piece.RetrievedAt).HasColumnName("retrieved_at").IsRequired();
  }
}
