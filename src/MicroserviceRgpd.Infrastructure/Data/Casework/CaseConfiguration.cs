using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Infrastructure.Data.Casework;

/// <summary>
/// L'agrégat sur trois tables : le dossier, ses <c>Claim</c>, leurs <c>Step</c> — plus le sac de
/// <c>Designation</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Les trois tables filles sont <em>possédées</em>, et c'est la frontière de l'agrégat rendue
/// structurelle.</b> Un type possédé n'a ni <c>DbSet</c>, ni requête à lui, ni dépôt possible : il
/// ne s'atteint que par sa racine. « Aucun accès en écriture direct à un <c>Claim</c> ou à un
/// <c>Step</c> » cesse ainsi d'être une discipline pour devenir une impossibilité — et l'invariant
/// « lire avant d'effacer », qui traverse deux <c>Claim</c>, garde la seule frontière qui puisse le
/// tenir.
/// </para>
/// <para>
/// <b>Le droit est la clé d'un <c>Claim</c>, et aucun identifiant de substitution n'est engendré.</b>
/// Deux <c>Claim</c> portant le même droit dans un même dossier seraient deux réponses dues à la
/// personne sur la même question : c'est la base qui le rend impossible, et non la seule
/// déduplication du domaine.
/// </para>
/// <para>
/// <b>Le <c>snake_case</c> est déclaré ici, explicitement</b>, comme sur les autres tables du
/// dépôt : un paquet de conventions déciderait en silence du nom de toutes les tables à venir.
/// </para>
/// </remarks>
public sealed class CaseConfiguration : IEntityTypeConfiguration<Case>
{
  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<Case> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("cases");

    builder.HasKey(opened => opened.Id).HasName("pk_cases");
    builder.Property(opened => opened.Id)
      .HasColumnName("id")
      .HasConversion(id => id.Value, value => CaseId.From(value))
      .ValueGeneratedNever();

    // Par son nom, jamais par un entier : un entier lierait le schéma à l'ordre de déclaration d'un
    // vocabulaire fermé que personne ne pense à tenir stable, et rendrait la table illisible.
    builder.Property(opened => opened.IdentityDeclaration)
      .HasColumnName("identity_declaration")
      .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
      .HasConversion(
        declaration => declaration.Name,
        name => IdentityDeclaration.FromName(name))
      .IsRequired();

    builder.Property(opened => opened.State)
      .HasColumnName("state")
      .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
      .HasConversion(state => state.Name, name => CaseState.FromName(name))
      .IsRequired();

    ConfigureTheMotivation(builder);
    ConfigureTheReception(builder);
    ConfigureTheBag(builder);
    ConfigureTheClaims(builder);
  }

  /// <summary>
  /// Ce que l'humain a pesé avant d'ouvrir un droit sous cette identité : la <b>méthode</b> qui se
  /// compte, et le <b>détail</b> en prose qui meurt avec le dossier.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Les deux colonnes sont nulles ensemble, et c'est le propos.</b> Toutes deux nulles se lit
  /// « personne ne l'a pesé » ; la méthode <c>None</c> se lit « quelqu'un a pesé et n'a rien fait ».
  /// Les confondre aurait fait signer par défaut un aveu que personne n'a écrit.
  /// </para>
  /// <para>
  /// ⚠️ <b>Le détail est du nominatif, et il est là où la clôture ira le détruire</b> : sur la ligne
  /// du dossier. Il n'existe aucune colonne pour lui dans le <c>Ledger</c>, qui survit cinq ans.
  /// </para>
  /// </remarks>
  private static void ConfigureTheMotivation(EntityTypeBuilder<Case> builder)
  {
    builder.OwnsOne(opened => opened.Motivation, motivation =>
    {
      // Par son nom, jamais par un entier, comme partout ailleurs sur les vocabulaires fermés.
      motivation.Property(one => one.Method)
        .HasColumnName("identity_verification_method")
        .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
        .HasConversion(
          method => method.Name,
          name => IdentityVerificationMethod.FromName(name))
        .IsRequired();

      motivation.Property(one => one.Detail)
        .HasColumnName("identity_motivation_detail")
        .HasMaxLength(IdentityMotivation.MaxDetailLength);
    });
  }

  /// <summary>
  /// La date de réception et <b>le régime sous lequel le service la sait</b> : deux colonnes de la
  /// même table, jamais l'une sans l'autre.
  /// </summary>
  /// <remarks>
  /// <b>Deux colonnes plutôt qu'une.</b> Une date nue serait indiscernable d'une date affirmée par un
  /// humain — le drapeau est ce qui rend le défaut visible <em>comme un défaut</em>, à l'écran comme
  /// en base. Le nom <c>received_on</c> ne bouge pas : c'est la même date, mieux qualifiée.
  /// </remarks>
  private static void ConfigureTheReception(EntityTypeBuilder<Case> builder)
  {
    builder.OwnsOne(opened => opened.Reception, reception =>
    {
      reception.Property(date => date.On).HasColumnName("received_on").IsRequired();
      reception.Property(date => date.IsDefault).HasColumnName("reception_is_default").IsRequired();
    });

    builder.Navigation(opened => opened.Reception).IsRequired();
  }

  /// <summary>
  /// Le sac de <c>Designation</c> — <b>la seule identité qui circule</b>, et la première chose que
  /// la clôture détruira.
  /// </summary>
  /// <remarks>
  /// <b>Une table plutôt qu'une colonne.</b> Chaque désignation porte sa nature, et une colonne
  /// unique aurait exigé de coder la paire dans une chaîne — que toute requête aurait dû décoder,
  /// et qu'aucune contrainte n'aurait pu tenir.
  /// </remarks>
  private static void ConfigureTheBag(EntityTypeBuilder<Case> builder)
  {
    builder.OwnsMany(opened => opened.Designations, bag =>
    {
      bag.ToTable("case_designations");
      bag.WithOwner().HasForeignKey("case_id").HasConstraintName("fk_case_designations_cases");

      // Un rang plutôt qu'une clé sur la valeur : la clé d'une désignation serait la donnée
      // nominative elle-même, montée dans un index que la clôture devrait aller défaire.
      bag.Property<int>("ordinal").HasColumnName("ordinal");
      bag.HasKey("case_id", "ordinal").HasName("pk_case_designations");

      bag.Property(designation => designation.Kind)
        .HasColumnName("kind")
        .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
        .HasConversion(
          // Le mot du contrat d'Adapter, et non le nom du membre C# : la base n'introduit aucun
          // second vocabulaire, et le fil et la colonne disent le même mot.
          kind => kind.Token,
          token => DesignationKind.FromToken(token)!)
        .IsRequired();

      bag.Property(designation => designation.Value)
        .HasColumnName("value")
        .HasMaxLength(Designation.MaxValueLength)
        .IsRequired();
    });

    builder.Navigation(opened => opened.Designations).UsePropertyAccessMode(PropertyAccessMode.Field);
  }

  /// <summary>
  /// Les <c>Claim</c> et, sous chacun, ses <c>Step</c> — un par <c>DeclaredSystem</c> du catalogue
  /// au moment où le dossier s'est ouvert.
  /// </summary>
  private static void ConfigureTheClaims(EntityTypeBuilder<Case> builder)
  {
    builder.OwnsMany(opened => opened.Claims, claim =>
    {
      claim.ToTable("case_claims");
      claim.WithOwner().HasForeignKey("case_id").HasConstraintName("fk_case_claims_cases");

      claim.Property(one => one.Right)
        .HasColumnName("data_subject_right")
        .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
        .HasConversion(right => right.Name, name => DataSubjectRight.FromName(name));

      // Un droit au plus par dossier : la clé le dit, et la base le tient.
      claim.HasKey("case_id", "Right").HasName("pk_case_claims");

      claim.Property(one => one.State)
        .HasColumnName("state")
        .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
        .HasConversion(state => state.Name, name => ClaimState.FromName(name))
        .IsRequired();

      // L'origine et l'identité d'origine sont des COPIES figées à la naissance du droit, et elles
      // vivent donc sur la ligne du Claim plutôt que d'être relues sur celle du dossier : une
      // jointure vers `cases.identity_declaration` aurait rendu rétroactivement propre un accès
      // ouvert sur rien, le jour où quelqu'un reprend la déclaration du dossier.
      claim.Property(one => one.Origin)
        .HasColumnName("origin")
        .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
        .HasConversion(origin => origin.Name, name => ClaimOrigin.FromName(name))
        .IsRequired();

      claim.Property(one => one.IdentityAtOrigin)
        .HasColumnName("identity_at_origin")
        .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
        .HasConversion(
          declaration => declaration.Name,
          name => IdentityDeclaration.FromName(name))
        .IsRequired();

      claim.Property(one => one.Confirmed)
        .HasColumnName("confirmed")
        .IsRequired();

      claim.OwnsMany(one => one.Steps, step =>
      {
        step.ToTable("case_steps");
        step.WithOwner().HasForeignKey("case_id", "data_subject_right").HasConstraintName("fk_case_steps_case_claims");

        step.Property(one => one.DeclaredSystem)
          .HasColumnName("declared_system_id")
          .HasMaxLength(DeclaredSystemId.MaxLength)
          .HasConversion(id => id.Value, value => DeclaredSystemId.From(value));

        // Un Step naît par (Claim, DeclaredSystem) : la clé est exactement cette paire, et le même
        // système ne peut pas porter deux fois le même travail dû.
        step.HasKey("case_id", "data_subject_right", "DeclaredSystem").HasName("pk_case_steps");

        step.Property(one => one.State)
          .HasColumnName("state")
          .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
          .HasConversion(state => state.Name, name => StepState.FromName(name))
          .IsRequired();
      });

      claim.Navigation(one => one.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
    });

    builder.Navigation(opened => opened.Claims).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}
