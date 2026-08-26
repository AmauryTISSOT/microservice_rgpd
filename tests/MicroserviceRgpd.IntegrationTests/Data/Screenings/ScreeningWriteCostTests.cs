using System.Diagnostics;
using System.Globalization;
using MicroserviceRgpd.Core.Screenings;
using Xunit.Abstractions;

namespace MicroserviceRgpd.IntegrationTests.Data.Screenings;

/// <summary>
/// La <b>part écriture du budget</b> : les 3 s que le geste de dépôt réserve à EF Core pour poser un
/// rapport de 20 000 colonnes.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Elle était une estimation, et jusqu'ici rien ne l'avait mesurée.</b> Le budget du geste se
/// scinde <c>10 s = 3 s d'écriture + 7 s de moteur</c>, et le chiffre de 7 s a été transmis au banc
/// <b>en s'appuyant sur une part d'écriture que personne n'avait chiffrée</b>. Si l'écriture
/// dépassait 3 s, ce serait le budget du moteur qui se réduirait — jamais le geste qui s'allongerait.
/// </para>
/// <para>
/// <b>Elle se mesure ici, contre le vrai PostgreSQL et par le vrai chemin d'écriture</b> : EF Core,
/// homogène avec tout le reste du dépôt, et non un <c>COPY</c> binaire Npgsql qui aurait tenu
/// 20 000 lignes sous 100 ms au prix d'un chemin contournant EF et spécifique au fournisseur.
/// </para>
/// <para>
/// ⚠️ <b>Le seuil gardé est large, et il ne dit pas « l'écriture est rapide ».</b> Comme pour la
/// part moteur, une machine d'intégration partagée ne reproduit pas un chiffre de banc et n'a pas à
/// essayer : ce qui est gardé est que l'écriture reste <b>dans son enveloppe</b>, c'est-à-dire
/// qu'aucune régression n'a fait passer la pose d'un rapport d'un lot à un aller-retour par ligne —
/// le seul mode de panne qui ferait sortir le geste entier de ses 10 s.
/// </para>
/// <para>
/// <b>Ce que la mesure a rendu, le 13/08/2026</b>, sur PostgreSQL 18 en conteneur, machine de
/// développement, modèle et pool déjà chauds : <b>≈ 2,7 s pour 20 000 colonnes</b> (0,133 ms/colonne)
/// et <b>≈ 1,3 s pour les 5 382 colonnes de Dolibarr</b> (0,238 ms/colonne). L'écriture est bien un
/// lot ; le coût par colonne baisse quand le relevé grandit, ce qui est la signature d'un coût fixe
/// de transaction amorti — l'inverse exact d'un aller-retour par ligne.
/// </para>
/// <para>
/// ⚠️ <b>Et l'estimation de 3 s n'avait aucune marge : au pire cas autorisé, l'écriture la consomme
/// presque entièrement.</b> Elle tient, mais de justesse, et sur une machine plus lente elle
/// mordrait sur la part moteur. Ce n'est pas un problème du geste réel — l'<c>Operator</c> dépose un
/// schéma de la taille de Dolibarr, où l'écriture coûte 1,3 s et laisse le budget entier respirer —
/// mais c'est un fait à connaître avant de resserrer le budget ou de promettre les 10 s sur un
/// relevé de 20 000 colonnes.
/// </para>
/// </remarks>
[Collection(PostgreSqlCollection.Name)]
public class ScreeningWriteCostTests(PostgreSqlFixture postgres, ITestOutputHelper output)
{
  /// <summary>Le pire cas autorisé par le contrat de format : au-delà, le relevé est refusé plutôt que tronqué.</summary>
  private const int WorstCaseAllowed = ColumnListing.MaxColumns;

  /// <summary>La taille du plus gros schéma du corpus — celle que l'<c>Operator</c> déposera vraiment.</summary>
  private const int DolibarrSized = 5_382;

  /// <summary>
  /// Le plafond gardé, en millisecondes par colonne. ⚠️ <b>Ce n'est pas la part d'écriture du
  /// budget</b>, qui vaudrait 0,150 ms/colonne au pire cas et qui se mesure, ici, à un cheveu de sa
  /// borne : garder la ligne du budget elle-même sur une machine partagée rendrait ce test rouge un
  /// jour de charge sans qu'aucun code n'ait bougé, et un test qui crie faux finit ignoré.
  /// <b>Ce qui est gardé est la <i>forme</i> de l'écriture</b> — un lot, et non un aller-retour par
  /// ligne.
  /// <para>
  /// ⚠️ <b>Le plafond est le même que celui de la part moteur, et il est volontairement lâche</b> :
  /// 1,0 ms/colonne, contre 0,13 mesuré au pire cas et 0,24 sur le corpus — le coût fixe d'une
  /// transaction pèse davantage sur un petit relevé, et un seuil serré sur cette taille-là rougirait
  /// le premier. Un aller-retour par ligne sur 20 000 lignes coûte plusieurs secondes de plus et
  /// franchit cette borne ; c'est ce qu'elle attrape, et rien d'autre.
  /// </para>
  /// </summary>
  private const double MeanCeilingInMillisecondsPerColumn = 1.0;

  private static readonly DateTimeOffset LaunchedOn = new(2026, 8, 6, 9, 30, 0, TimeSpan.Zero);

  private static readonly ScreeningEngineIdentity Engine = new("lexique-fr-en", "1.0.0");

  /// <summary>
  /// <b>Poser un rapport reste un lot, aux deux tailles qui comptent</b> : celle que
  /// l'<c>Operator</c> déposera, et celle que le contrat de format autorise au pire.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Les deux tailles sont mesurées, et pas seulement le pire cas.</b> Le budget se raisonne
  /// sur le pire cas ; ce que vit l'<c>Operator</c> se raisonne sur le corpus. Ne chiffrer que
  /// 20 000 colonnes aurait laissé croire que le geste réel coûte trois secondes.
  /// </remarks>
  [Theory]
  [InlineData(DolibarrSized)]
  [InlineData(WorstCaseAllowed)]
  public async Task WritesAListingAsOneBatchRatherThanOneRoundTripPerRow(int columns)
  {
    // ⚠️ Une écriture minuscule d'abord, et elle n'est PAS de la complaisance. EF Core compile son
    // modèle et Npgsql ouvre son pool au premier accès : ce coût-là, l'application le paie au
    // démarrage, pas à chaque geste de dépôt. Le mesurer avec l'écriture rendrait un chiffre que
    // l'Operator ne verra jamais — et il le rendrait en gonflant le pire cas de 0,5 s.
    await using (var warmup = postgres.NewDbContext())
    {
      warmup.Screenings.Add(AListingOf(1));
      await warmup.SaveChangesAsync();
    }

    var screening = AListingOf(columns);

    await using var dbContext = postgres.NewDbContext();

    dbContext.Screenings.Add(screening);

    var clock = Stopwatch.StartNew();
    await dbContext.SaveChangesAsync();
    clock.Stop();

    // Le chiffre est porté au rapport de test : ce ticket doit consigner une mesure, pas un vert.
    var elapsed = clock.Elapsed.TotalMilliseconds;

    output.WriteLine(string.Create(
      CultureInfo.InvariantCulture,
      $"Écriture EF Core de {columns} colonnes : {elapsed:F0} ms "
      + $"({elapsed / columns:F3} ms/colonne)."));

    (elapsed / columns).ShouldBeLessThan(
      MeanCeilingInMillisecondsPerColumn,
      $"L'écriture de {columns} colonnes a pris {elapsed:F0} ms, soit {elapsed / columns:F3} "
      + "ms/colonne : le chemin d'écriture a probablement régressé d'un lot vers un aller-retour par "
      + "ligne, le seul mode de panne qui ferait sortir le geste entier de ses 10 s.");
  }

  /// <summary>
  /// Un rapport de la taille qu'on lui demande, avec une ligne sur treize signalée — la densité que
  /// le corpus rend, et non un rapport tout signalé qui n'existe nulle part.
  /// </summary>
  private static Screening AListingOf(int columns)
  {
    var screened = new List<ScreenedColumn>(columns);

    for (var position = 0; position < columns; position++)
    {
      // Treize colonnes par table : la taille moyenne d'une table du plus gros schéma du corpus.
      var listed = ListedColumn.Of(
        ColumnIdentity.Of("public", $"llx_table_{position / 13}", $"colonne_{position}"),
        (position % 13) + 1,
        "varchar(255)",
        isNullable: true,
        columnComment: null,
        tableComment: null,
        referencedTable: null);

      screened.Add(position % 13 == 0
        ? ScreenedColumn.Flagged(
          listed,
          PersonalDataCategory.ContactDetails,
          RuleStrength.Morphological,
          $"préfixe « adr » reconnu dans « colonne_{position} »")
        : ScreenedColumn.NothingSeen(listed));
    }

    return Screening.Of(
      ScreeningId.Next(),
      "dolibarr_prod",
      "postgresql",
      ListingOrigin.Pasted,
      Engine,
      columns,
      screened,
      LaunchedOn);
  }
}
