using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.Infrastructure.Data.Screenings;

/// <summary>
/// La table du rapport : une ligne par <see cref="Screening"/>, et rien de plus. Ses colonnes
/// vivent à côté, dans leur propre table — voir <see cref="ScreenedColumnConfiguration"/>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucune colonne d'état, et c'est le point qu'on relit trois fois avant d'y toucher.</b> Ni
/// drapeau <c>archived</c>, ni date d'archivage, ni table à part : « courant » est le rapport de
/// <c>launched_on</c> maximal, et tous les autres sont archivés par le seul fait qu'un plus récent
/// existe. Une colonne aurait rendu possibles deux courants après une transition ratée, et
/// l'<c>Operator</c> aurait arbitré le mauvais rapport.
/// </para>
/// <para>
/// <b>Aucune colonne d'avancement non plus</b> — ni compte de colonnes arbitrées, ni verrou
/// « inachevé ». Ce sont des comptes sur <c>screened_columns</c>, recalculés à chaque rendu ; un
/// compte persisté aurait fait dépendre le verrou de ce que quelqu'un ait pensé à le remettre à
/// jour.
/// </para>
/// <para>
/// <b>Le <c>snake_case</c> est déclaré ici, explicitement</b>, comme sur toutes les autres tables du
/// dépôt : un paquet de conventions déciderait en silence du nom de toutes les tables à venir.
/// </para>
/// <para>
/// <b>Aucun index secondaire, pas même sur <c>launched_on</c></b> — qui est pourtant la seule
/// lecture de cette table, « le plus récemment lancé ». Un déploiement en porte quelques dizaines,
/// jamais plus : dix détections de Dolibarr font dix lignes ici. Un index se paierait à chaque
/// dépôt pour éviter un balayage de dix lignes, et la clé primaire couvre le seul accès ciblé qui
/// existe.
/// </para>
/// </remarks>
public sealed class ScreeningConfiguration : IEntityTypeConfiguration<Screening>
{
  /// <inheritdoc />
  public void Configure(EntityTypeBuilder<Screening> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable(ScreeningSchema.Screenings);

    // La clé est forgée par le service en GUID v7 — ordonnée dans le temps, donc sans fragmentation
    // d'index à l'insertion. La base ne la génère jamais.
    builder.HasKey(screening => screening.Id).HasName("pk_screenings");
    builder.Property(screening => screening.Id)
      .HasColumnName("id")
      .HasConversion(id => id.Value, value => ScreeningId.From(value))
      .ValueGeneratedNever();

    // C'est ce seul instant qui décide quel rapport est le courant : il est donc obligatoire, et
    // c'est la seule chose dont « courant » a besoin en base.
    builder.Property(screening => screening.LaunchedOn).HasColumnName("launched_on").IsRequired();

    builder.Property(screening => screening.Database)
      .HasColumnName("database_name")
      .HasMaxLength(Screening.MaxDatabaseNameLength)
      .IsRequired();

    builder.Property(screening => screening.Dialect)
      .HasColumnName("dialect")
      .HasMaxLength(Screening.MaxDialectLength)
      .IsRequired();

    ConfigureTheOrigin(builder);

    // Le compte DÉCLARÉ par le relevé, conservé à côté du compte réel des lignes filles : c'est
    // leur égalité qui rend une troncature au collage détectable, et un rapport bâti sur 99 % d'un
    // relevé se lirait sinon comme complet.
    builder.Property(screening => screening.DeclaredColumnCount)
      .HasColumnName("declared_column_count")
      .IsRequired();

    ConfigureTheEngine(builder);
    ConfigureTheColumns(builder);
    IgnoreWhatIsCounted(builder);
  }

  /// <summary>
  /// Par quel chemin le relevé est entré : <b>une colonne, non nullable</b>, portant le nom du
  /// membre.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Elle est enregistrée plutôt que déduite</b>, parce que la clause d'incomplétude est
  /// rendue jusque sur l'archive, des mois après que la chaîne de connexion a cessé d'exister. Rien
  /// d'autre de la ligne ne dit d'où venait le relevé.
  /// </para>
  /// <para>
  /// ⚠️ <b>La lecture passe par le champ, et c'est nécessaire</b> : <c>Screening.Origin</c>
  /// <b>lève</b> quand l'origine est absente ou nulle, et EF Core lit la propriété au moment
  /// d'écrire. Sans ce mode d'accès, une ligne héritée ferait tomber le contexte de suivi plutôt que
  /// le rendu — c'est-à-dire au mauvais endroit, et sur une phrase qui ne parlerait plus d'origine.
  /// </para>
  /// <para>
  /// <b>Par son nom, jamais par un entier</b>, comme les trois autres vocabulaires fermés de ce
  /// contexte : un entier lierait le schéma à l'ordre de déclaration et rendrait la table illisible.
  /// ⚠️ Et <b>aucune contrainte de contrôle n'énumère les valeurs</b> — pas plus que pour
  /// <c>category</c> : le cas nul est refusé par le domaine à l'écriture et par le rendu à la
  /// lecture, et une liste de mots recopiée en SQL aurait exigé une migration à chaque membre neuf
  /// pour ce que deux gardes tiennent déjà.
  /// </para>
  /// </remarks>
  private static void ConfigureTheOrigin(EntityTypeBuilder<Screening> builder)
  {
    builder.Property(screening => screening.Origin)
      .HasColumnName("listing_origin")
      .HasMaxLength(ScreeningSchema.ClosedVocabularyLength)
      .HasConversion(origin => origin.Name, name => ListingOrigin.FromName(name))
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .IsRequired();
  }

  /// <summary>
  /// Qui a détecté, et dans quelle version — deux colonnes de la ligne du rapport de détection,
  /// jamais une table.
  /// </summary>
  /// <remarks>
  /// <b>Sans plafond déclaré</b>, comme les colonnes de moteur de la trace d'audit : c'est une
  /// chaîne opaque qu'un moteur se donne à lui-même, et que le service enregistre sans jamais la
  /// comparer. Borner ce qu'on n'interprète pas ferait refuser demain un moteur qui se nomme plus
  /// longuement, pour aucun gain.
  /// </remarks>
  private static void ConfigureTheEngine(EntityTypeBuilder<Screening> builder)
  {
    builder.OwnsOne(screening => screening.Engine, engine =>
    {
      engine.Property(identity => identity.Name).HasColumnName("engine_name").IsRequired();
      engine.Property(identity => identity.Version).HasColumnName("engine_version").IsRequired();
    });

    builder.Navigation(screening => screening.Engine).IsRequired();
  }

  /// <summary>
  /// Les colonnes du relevé, dans leur <b>propre table</b> — et le rapport les emporte quand il s'en
  /// va.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Elles ne sont pas <em>possédées</em></b> — le motif est écrit là où leur table se
  /// décide, voir <see cref="ScreenedColumnConfiguration"/>. Conséquence à retenir ici : un rapport
  /// relu <b>n'apporte pas ses colonnes</b> sans qu'on les demande, là où une collection possédée
  /// arriverait avec lui. C'est voulu — cinq mille lignes ne se chargent pas pour afficher un
  /// en-tête.
  /// </para>
  /// <para>
  /// <b>La suppression du rapport emporte ses colonnes, et c'est la base qui le tient.</b> Supprimer
  /// est un geste de l'<c>Operator</c>, irréversible et sans trace : laisser cinq mille lignes
  /// orphelines derrière lui aurait fait survivre les arbitrages d'un rapport qui n'existe plus.
  /// </para>
  /// </remarks>
  private static void ConfigureTheColumns(EntityTypeBuilder<Screening> builder)
  {
    builder.HasMany(screening => screening.Columns)
      .WithOne()
      .HasForeignKey(ScreenedColumnConfiguration.ScreeningForeignKey)
      .HasConstraintName("fk_screened_columns_screenings")
      .OnDelete(DeleteBehavior.Cascade);

    builder.Navigation(screening => screening.Columns).UsePropertyAccessMode(PropertyAccessMode.Field);
  }

  /// <summary>
  /// Tout ce que le rapport <b>compte</b> plutôt que de le porter. EF Core prendrait ces lectures
  /// pour des colonnes ou des navigations et leur chercherait une place : elles n'en ont pas, et
  /// n'en auront jamais.
  /// </summary>
  private static void IgnoreWhatIsCounted(EntityTypeBuilder<Screening> builder)
  {
    builder.Ignore(screening => screening.AwaitingCount);
    builder.Ignore(screening => screening.RetainedCount);
    builder.Ignore(screening => screening.SetAsideCount);
    builder.Ignore(screening => screening.FlaggedCount);
    builder.Ignore(screening => screening.UncategorisedCount);
    builder.Ignore(screening => screening.ColumnCount);
    builder.Ignore(screening => screening.ColumnsWithoutACommentCount);
    builder.Ignore(screening => screening.TableCount);
    builder.Ignore(screening => screening.RetainedOnUnflaggedCount);
    builder.Ignore(screening => screening.UnreadUnflaggedCount);

    // ⚠️ Les tables RETRIÉES sont un calcul sur les colonnes, et non une seconde table. EF Core y
    // voit par convention une navigation vers un type qu'il faudrait persister — il a réclamé une
    // clé primaire pour `TableIdentity` — alors qu'il n'existe aucune ligne à écrire : le retri se
    // refait à chaque lecture, et une table mémorisée aurait eu besoin de quelque chose pour la
    // mettre à jour.
    builder.Ignore(screening => screening.Tables);
  }
}
