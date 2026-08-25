using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Data.Screenings;

/// <summary>
/// La table des colonnes détectées : une ligne par colonne du relevé, <b>sans exception</b> — y
/// compris là où le moteur n'a rien vu.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Elle a son propre <c>DbSet</c>, et le précédent de <c>Claim</c> et <c>Step</c> est rompu
/// délibérément.</b> Ceux-là sont <em>possédés</em> par le dossier et ne s'atteignent que par lui ;
/// mais un <c>Case</c> a quelques dizaines d'enfants là où un <c>Screening</c> en a cinq mille, et
/// l'écran d'arbitrage n'ouvre qu'une table à la fois. C'est un <b>ordre de grandeur</b> qui rompt
/// le précédent, jamais un goût — voir l'index par table, qui est la lecture que cette table sert.
/// </para>
/// <para>
/// ⚠️ <b>Aucune colonne d'état d'arbitrage séparée de sa date.</b> Les deux colonnes de
/// l'arbitrage sont nulles ensemble — la colonne attend — ou pleines ensemble, et une contrainte de
/// contrôle le tient jusque dans la base : « deux champs qu'un chemin d'écriture peut dissocier
/// finissent par se dissocier », et un <c>Retained</c> sans date serait très exactement un
/// arbitrage que personne n'a rendu.
/// </para>
/// <para>
/// <b>Le <c>snake_case</c> est déclaré ici, explicitement</b>, comme sur toutes les autres tables du
/// dépôt.
/// </para>
/// </remarks>
public sealed class ScreenedColumnConfiguration : IEntityTypeConfiguration<ScreenedColumn>
{
  /// <summary>
  /// La clé étrangère vers le rapport, <b>en propriété d'ombre</b> : une colonne ne se nomme pas
  /// elle-même par son rapport dans le domaine — elle naît du moteur avant qu'un rapport n'existe —
  /// et lui poser une propriété n'aurait servi qu'à la base.
  /// </summary>
  internal const string ScreeningForeignKey = "ScreeningId";

  /// <summary>
  /// La clé de substitution, en propriété d'ombre elle aussi. Ce qui identifie une colonne <b>dans
  /// un rapport</b> est le triplet — il porte l'index unique plus bas — et le domaine n'a aucun
  /// usage d'un identifiant de ligne.
  /// </summary>
  private const string RowIdentity = "Id";

  /// <summary>
  /// Les deux colonnes de l'arbitrage entrent ensemble ou pas du tout, et c'est la base qui le
  /// dit. Sans elle, la promesse « il n'existe ni <c>Retained</c> ni <c>SetAside</c> sans date »
  /// ne tiendrait que tant que tout le monde passe par le domaine.
  /// </summary>
  private const string ArbitrationIsWholeOrAbsent =
    "(state is null and rendered_on is null) or "
    + "(state is not null and rendered_on is not null)";

  /// <summary>
  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<ScreenedColumn> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(
      ScreeningSchema.ScreenedColumns,
      table => table.HasCheckConstraint("ck_screened_columns_arbitration", ArbitrationIsWholeOrAbsent));

    builder.Property<Guid>(RowIdentity)
      .HasColumnName("id")
      .ValueGeneratedOnAdd()
      .HasValueGenerator<Version7GuidValueGenerator>();

    builder.HasKey(RowIdentity).HasName("pk_screened_columns");

    builder.Property<ScreeningId>(ScreeningForeignKey)
      .HasColumnName("screening_id")
      .HasConversion(id => id.Value, value => ScreeningId.From(value))
      .IsRequired();

    // ⚠️ L'index que TOUTE clé étrangère reçoit de la convention d'EF Core. Il est couvert par les
    // colonnes de tête de `ux_screened_columns_identity`, l'index unique du triplet — écrit à la
    // main dans la migration, parce que ses colonnes sont à cheval sur deux types du modèle et
    // qu'EF Core ne sait déclarer un index que sur les propriétés d'un seul. On le NOMME donc
    // plutôt que de le combattre : la convention le reposerait à la migration suivante, et le
    // nommage par défaut aurait introduit dans la base un second style d'écriture. Il n'y a en
    // revanche pas de TROISIÈME index sur (screening_id, schema_name, table_name) : ce sont
    // exactement les colonnes de tête de l'index unique, et il se serait payé sur chaque dépôt,
    // cinq mille lignes à la fois, pour ce que celui-là rend déjà.
    builder.HasIndex(ScreeningForeignKey).HasDatabaseName("ix_screened_columns_screening_id");

    ConfigureTheListedLine(builder);
    ConfigureTheScreeningVerdict(builder);
    ConfigureTheArbitration(builder);

    // Ce que la ligne calcule plutôt que de le porter. `State` en particulier n'a pas de colonne à
    // lui : il EST « aucun arbitrage » ou l'état de l'arbitrage, et une colonne à côté aurait pu
    // s'en dissocier. `Identity` est une lecture de la ligne du relevé, jamais une seconde copie du
    // triplet — EF Core y verrait sinon une navigation et lui chercherait une table.
    builder.Ignore(column => column.State);
    builder.Ignore(column => column.AwaitsAnArbitration);
    builder.Ignore(column => column.IsFlagged);
    builder.Ignore(column => column.IsWithinReachOfABatchGesture);
    builder.Ignore(column => column.Identity);
  }

  /// <summary>
  /// La ligne du relevé, <b>recopiée telle quelle</b> : le triplet qui la nomme, son rang, et les
  /// cinq champs que le relevé porte à côté.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Les cinq champs descendent en base alors même qu'ils entrent « comme <i>filtre</i>,
  /// jamais comme <i>signal</i> »</b> : le commentaire de table éclaire toutes les colonnes de
  /// l'écran, un humain juge sur le type autant que sur le nom, et les comptes de la clause
  /// d'incomplétude restent calculables à tout moment plutôt que figés à l'ingestion.
  /// </para>
  /// <para>
  /// <b>Aucun plafond sur les commentaires ni sur le type</b> : ce sont des textes que le SGBD du
  /// client rend, que le service ne borne nulle part dans le domaine, et qu'il recopie sans les
  /// juger. Un plafond en base seule ferait refuser au collage un relevé sincère.
  /// </para>
  /// </remarks>
  private static void ConfigureTheListedLine(EntityTypeBuilder<ScreenedColumn> builder)
  {
    builder.OwnsOne(column => column.Listed, listed =>
    {
      listed.OwnsOne(one => one.Identity, identity =>
      {
        // `schema`, `table` et `column` sont des mots du SQL : les colonnes portent le suffixe qui
        // les en distingue, plutôt que de dépendre d'un guillemet que toute requête écrite à la
        // main devrait penser à poser.
        identity.Property(triplet => triplet.Schema)
          .HasColumnName("schema_name")
          .HasMaxLength(ColumnIdentity.MaxNameLength)
          .IsRequired();

        identity.Property(triplet => triplet.Table)
          .HasColumnName("table_name")
          .HasMaxLength(ColumnIdentity.MaxNameLength)
          .IsRequired();

        identity.Property(triplet => triplet.Column)
          .HasColumnName("column_name")
          .HasMaxLength(ColumnIdentity.MaxNameLength)
          .IsRequired();

        // La table seule est une lecture du triplet, jamais une colonne de plus.
        identity.Ignore(triplet => triplet.TableIdentity);
      });

      listed.Navigation(one => one.Identity).IsRequired();

      // Le rang tel que le relevé le rend : c'est lui qui ordonne l'écran, et l'ordre du schéma est
      // le seul qui garde à `adr_l1` le voisinage de `adr_l2`, `cp` et `ville`.
      listed.Property(one => one.Position).HasColumnName("position").IsRequired();

      listed.Property(one => one.DataType).HasColumnName("data_type");
      listed.Property(one => one.IsNullable).HasColumnName("is_nullable");
      listed.Property(one => one.ColumnComment).HasColumnName("column_comment");
      listed.Property(one => one.TableComment).HasColumnName("table_comment");
      listed.Property(one => one.ReferencedTable).HasColumnName("referenced_table");

      listed.Ignore(one => one.CarriesAComment);
    });

    builder.Navigation(column => column.Listed).IsRequired();
  }

  /// <summary>
  /// Ce que la détection a dit de cette colonne : une catégorie <b>toujours</b> présente, le degré
  /// de la règle qui a déclenché, et le motif en prose qui les justifie.
  /// </summary>
  /// <remarks>
  /// <b>Motif présent ⇔ ce n'est pas <c>Unflagged</c></b> — l'invariant du contexte, tenu par les
  /// deux fabriques du domaine. La base porte donc deux colonnes nullables ensemble : une colonne
  /// où rien n'a été vu n'a ni degré ni motif, parce qu'elle n'a pas de règle et rien à motiver.
  /// </remarks>
  private static void ConfigureTheScreeningVerdict(EntityTypeBuilder<ScreenedColumn> builder)
  {
    // Par leur nom, jamais par un entier : un entier lierait le schéma à l'ordre de déclaration
    // d'un vocabulaire fermé que personne ne pense à tenir stable, et rendrait la table illisible.
    builder.Property(column => column.Category)
      .HasColumnName("category")
      .HasMaxLength(ScreeningSchema.ClosedVocabularyLength)
      .HasConversion(category => category.Name, name => PersonalDataCategory.FromName(name))
      .IsRequired();

    builder.Property(column => column.Strength)
      .HasColumnName("strength")
      .HasMaxLength(ScreeningSchema.ClosedVocabularyLength)
      .HasConversion(strength => strength!.Name, name => RuleStrength.FromName(name));

    builder.Property(column => column.Reason)
      .HasColumnName("reason")
      .HasMaxLength(ScreenedColumn.MaxReasonLength);
  }

  /// <summary>
  /// L'issue rendue par un humain, <b>inline sur la ligne</b> : l'état et la date.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Ce n'est pas une troisième table</b>, et ce n'est pas non plus une histoire. La trace est
  /// l'état courant seul : un second arbitrage écrase le premier, et le coût est déclaré — la date
  /// de l'arbitrage remplacé est effacée.
  /// </para>
  /// <para>
  /// ⚠️ <b>Aucune colonne ne porte qui a arbitré</b>, contre le <c>string?</c> de <c>Casework</c> :
  /// ce contexte ne l'enregistre pas du tout — voir l'<c>ADR-0014</c>. La date est obligatoire dès
  /// qu'un arbitrage existe : une date manquante n'est pas un champ vide, c'est un arbitrage qui
  /// n'a pas eu lieu. Les deux colonnes sont nullables <b>ensemble</b> parce qu'<c>Awaiting</c> est
  /// l'absence des deux ; c'est la contrainte de contrôle de la table, et non la nullité d'une
  /// colonne prise seule, qui interdit le duo dépareillé.
  /// </para>
  /// </remarks>
  private static void ConfigureTheArbitration(EntityTypeBuilder<ScreenedColumn> builder)
  {
    builder.OwnsOne(column => column.Arbitration, arbitration =>
    {
      arbitration.Property(one => one.State)
        .HasColumnName("state")
        .HasMaxLength(ScreeningSchema.ClosedVocabularyLength)
        .HasConversion(state => state.Name, name => ScreenedColumnState.FromName(name))
        .IsRequired();

      arbitration.Property(one => one.RenderedOn).HasColumnName("rendered_on").IsRequired();
    });
  }
}
