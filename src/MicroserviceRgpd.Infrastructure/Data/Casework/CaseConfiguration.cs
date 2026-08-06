using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.Casework.Adapters;
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
    ConfigureTheLocatings(builder);
    ConfigureTheReadings(builder);
    ConfigureTheQuestions(builder);
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

      // Les deux gestes de la remise sont deux colonnes, et non un état : la première est un
      // téléchargement, qui ne date rien ; la seconde est l'affirmation qu'on a rendu la réponse.
      // Les garder distinctes laisse voir la `Delivery` prise et jamais déclarée — celle qui doit
      // remonter dans la file plutôt que de disparaître entre deux états.
      claim.Property(one => one.DeliveryTakenOn).HasColumnName("delivery_taken_on");

      claim.Property(one => one.DeliveryDeclaredOn).HasColumnName("delivery_declared_on");

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

  /// <summary>
  /// Ce que les <c>Locate</c> ont rapporté : une ligne par système appelé, son noyau certain, et ses
  /// réserves avec les désignations qu'elles proposent.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Le grain est le système, jamais le droit</b> — à la différence de <c>case_steps</c>. Un
  /// <c>Locate</c> cherche la personne : le rattacher à un <c>Claim</c> aurait fait partir deux fois
  /// la même requête chez le client pour la même réponse, et aurait fait diverger deux réponses qui
  /// n'en sont qu'une.
  /// </para>
  /// <para>
  /// ⚠️ <b>Tout ce qui descend ici est nominatif ou le devient</b> : une référence opaque désigne les
  /// données de quelqu'un, un motif de réserve nomme des tiers non demandeurs. Ces tables sont donc du
  /// <b>dossier</b>, possédées par lui, et la clôture les emportera. Rien de leur contenu n'a de
  /// colonne dans <c>ledger_entries</c>, qui survit cinq ans.
  /// </para>
  /// </remarks>
  private static void ConfigureTheLocatings(EntityTypeBuilder<Case> builder)
  {
    builder.OwnsMany(opened => opened.Locatings, locating =>
    {
      locating.ToTable("case_locatings");
      locating.WithOwner().HasForeignKey("case_id").HasConstraintName("fk_case_locatings_cases");

      locating.Property(one => one.DeclaredSystem)
        .HasColumnName("declared_system_id")
        .HasMaxLength(DeclaredSystemId.MaxLength)
        .HasConversion(id => id.Value, value => DeclaredSystemId.From(value));

      // Un système au plus par dossier : on ne cherche pas deux fois la même personne au même
      // endroit, et la clé le dit plutôt que la seule discipline du domaine.
      locating.HasKey("case_id", "DeclaredSystem").HasName("pk_case_locatings");

      // Par son nom, jamais par un entier, comme partout ailleurs sur les vocabulaires fermés.
      locating.Property(one => one.LastOutcome)
        .HasColumnName("last_outcome")
        .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
        .HasConversion(outcome => outcome.Name, name => AdapterOutcome.FromName(name))
        .IsRequired();

      locating.Property(one => one.AskedAt).HasColumnName("asked_at").IsRequired();

      locating.Property(one => one.DeclaredDeadline).HasColumnName("declared_deadline");

      // Le COMPTE des désignations portées par le dernier appel, jamais lesquelles : c'est lui qui
      // dit qu'une réponse a répondu à une question plus étroite que celle qu'on pose aujourd'hui,
      // et donc qu'il faut repasser.
      locating.Property(one => one.DesignationsAtCall).HasColumnName("designations_at_call").IsRequired();

      ConfigureTheCertainCore(locating);
      ConfigureTheReservations(locating);
    });

    builder.Navigation(opened => opened.Locatings).UsePropertyAccessMode(PropertyAccessMode.Field);
  }

  /// <summary>
  /// Le noyau certain : des références <b>opaques</b>, recopiées et jamais ouvertes.
  /// </summary>
  private static void ConfigureTheCertainCore(OwnedNavigationBuilder<Case, Locating> locating)
  {
    locating.OwnsMany(one => one.Certain, certain =>
    {
      certain.ToTable("case_locating_references");
      certain.WithOwner()
        .HasForeignKey("case_id", "declared_system_id")
        .HasConstraintName("fk_case_locating_references_case_locatings");

      // Un rang plutôt qu'une clé sur la valeur, comme pour le sac de désignations : la clé serait la
      // donnée elle-même, montée dans un index que la clôture devrait aller défaire.
      certain.Property<int>("ordinal").HasColumnName("ordinal");
      certain.HasKey("case_id", "declared_system_id", "ordinal").HasName("pk_case_locating_references");

      certain.Property(reference => reference.Value)
        .HasColumnName("value")
        .HasMaxLength(OpaqueReference.MaxValueLength)
        .IsRequired();
    });

    locating.Navigation(one => one.Certain).UsePropertyAccessMode(PropertyAccessMode.Field);
  }

  /// <summary>
  /// Les réserves d'un <c>Locate</c>, leur <b>prose de motif</b>, leur arbitrage, et les
  /// <c>Designation</c> qu'elles proposent de verser au sac.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le motif est de la prose de TRAVAIL, et sa colonne est ici — jamais dans le
  /// <c>Ledger</c>.</b> Il dit quelle ligne appartient à qui, il nomme donc par nature des tiers non
  /// demandeurs, et il meurt avec le dossier. La règle tient par ce <b>placement</b> : il n'existe
  /// aucune colonne où il pourrait atterrir dans la preuve.
  /// </remarks>
  private static void ConfigureTheReservations(OwnedNavigationBuilder<Case, Locating> locating)
  {
    locating.OwnsMany(one => one.Reserved, reservation =>
    {
      reservation.ToTable("case_reservations");
      reservation.WithOwner()
        .HasForeignKey("case_id", "declared_system_id")
        .HasConstraintName("fk_case_reservations_case_locatings");

      reservation.Property(one => one.Reference)
        .HasColumnName("reference")
        .HasMaxLength(OpaqueReference.MaxValueLength)
        .HasConversion(reference => reference.Value, value => OpaqueReference.Of(value));

      // La référence identifie la réserve dans son système : deux fois la même serait deux fois la
      // même ligne, et ferait arbitrer deux fois le même doute.
      reservation.HasKey("case_id", "declared_system_id", "Reference").HasName("pk_case_reservations");

      reservation.Property(one => one.Reason)
        .HasColumnName("reason")
        .HasMaxLength(Reservation.MaxReasonLength)
        .IsRequired();

      reservation.Property(one => one.State)
        .HasColumnName("state")
        .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
        .HasConversion(state => state.Name, name => ReservationState.FromName(name))
        .IsRequired();

      reservation.OwnsMany(one => one.Designations, proposed =>
      {
        proposed.ToTable("case_reservation_designations");
        proposed.WithOwner()
          .HasForeignKey("case_id", "declared_system_id", "reference")
          .HasConstraintName("fk_case_reservation_designations_case_reservations");

        proposed.Property<int>("ordinal").HasColumnName("ordinal");
        proposed.HasKey("case_id", "declared_system_id", "reference", "ordinal")
          .HasName("pk_case_reservation_designations");

        proposed.Property(designation => designation.Kind)
          .HasColumnName("kind")
          .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
          .HasConversion(kind => kind.Token, token => DesignationKind.FromToken(token)!)
          .IsRequired();

        proposed.Property(designation => designation.Value)
          .HasColumnName("value")
          .HasMaxLength(Designation.MaxValueLength)
          .IsRequired();
      });

      reservation.Navigation(one => one.Designations).UsePropertyAccessMode(PropertyAccessMode.Field);
    });

    locating.Navigation(one => one.Reserved).UsePropertyAccessMode(PropertyAccessMode.Field);
  }

  /// <summary>
  /// Ce que les <c>Read</c> ont <b>tenté</b> : une ligne par (droit, système) appelé, ce qu'on nous a
  /// répondu, et l'échéance d'un différé.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Le grain est (droit, système)</b> — à la différence de <c>case_locatings</c>, dont le grain
  /// est le système seul : un <c>Locate</c> cherche la personne, un <c>Read</c> lit au titre d'un
  /// droit, et l'application peut légitimement rendre d'un même système deux pièces différentes selon
  /// qu'on lit sous l'art. 15 ou sous l'art. 20.
  /// </para>
  /// <para>
  /// ⚠️ <b>Aucune colonne ne porte d'octets, ni rien de la pièce.</b> Celle-ci vit dans
  /// <c>case_retrieved_data</c>, <b>hors de l'agrégat</b>, parce que la remise l'effacera sans
  /// réécrire le dossier. Ce qui reste ici est ce qui meurt avec lui.
  /// </para>
  /// <para>
  /// <b>Aucune clé étrangère vers <c>case_claims</c>.</b> Le droit est écrit tel quel : une lecture
  /// menée hier au titre d'un droit reste un fait daté, et une contrainte l'aurait fait disparaître
  /// le jour où le dossier cesse de porter ce <c>Claim</c>.
  /// </para>
  /// </remarks>
  private static void ConfigureTheReadings(EntityTypeBuilder<Case> builder)
  {
    builder.OwnsMany(opened => opened.Readings, reading =>
    {
      reading.ToTable("case_readings");
      reading.WithOwner().HasForeignKey("case_id").HasConstraintName("fk_case_readings_cases");

      reading.Property(one => one.Right)
        .HasColumnName("data_subject_right")
        .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
        .HasConversion(right => right.Name, name => DataSubjectRight.FromName(name));

      reading.Property(one => one.DeclaredSystem)
        .HasColumnName("declared_system_id")
        .HasMaxLength(DeclaredSystemId.MaxLength)
        .HasConversion(id => id.Value, value => DeclaredSystemId.From(value));

      // Une lecture au plus par (droit, système) : on ne lit pas deux fois le même endroit au titre
      // du même droit, et la clé le dit plutôt que la seule discipline du domaine.
      reading.HasKey("case_id", "Right", "DeclaredSystem").HasName("pk_case_readings");

      // Par son nom, jamais par un entier, comme partout ailleurs sur les vocabulaires fermés.
      reading.Property(one => one.LastOutcome)
        .HasColumnName("last_outcome")
        .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
        .HasConversion(outcome => outcome.Name, name => AdapterOutcome.FromName(name))
        .IsRequired();

      reading.Property(one => one.AskedAt).HasColumnName("asked_at").IsRequired();

      reading.Property(one => one.DeclaredDeadline).HasColumnName("declared_deadline");

      reading.Property(one => one.DesignationsAtCall).HasColumnName("designations_at_call").IsRequired();
    });

    builder.Navigation(opened => opened.Readings).UsePropertyAccessMode(PropertyAccessMode.Field);
  }

  /// <summary>
  /// Les questions ouvertes du dossier : leur sujet, et la date à laquelle elles ont été posées.
  /// </summary>
  /// <remarks>
  /// <b>Le sujet est la clé, et c'est ce qui fait qu'une question ne se pose qu'une fois.</b> La
  /// reposer à chaque passage en ferait un bruit quotidien, et réinitialiserait la seule chose que
  /// l'écran en dise : sa date. ⚠️ <b>Aucune colonne « depuis N jours »</b>, ici ni ailleurs — aucun
  /// nombre du droit ne fonderait N.
  /// </remarks>
  private static void ConfigureTheQuestions(EntityTypeBuilder<Case> builder)
  {
    builder.OwnsMany(opened => opened.Questions, question =>
    {
      question.ToTable("case_questions");
      question.WithOwner().HasForeignKey("case_id").HasConstraintName("fk_case_questions_cases");

      question.Property(one => one.Subject)
        .HasColumnName("subject")
        .HasMaxLength(CaseworkSchema.ClosedVocabularyLength)
        .HasConversion(subject => subject.Name, name => OpenQuestionSubject.FromName(name));

      question.HasKey("case_id", "Subject").HasName("pk_case_questions");

      question.Property(one => one.AskedOn).HasColumnName("asked_on").IsRequired();
    });

    builder.Navigation(opened => opened.Questions).UsePropertyAccessMode(PropertyAccessMode.Field);
  }
}
