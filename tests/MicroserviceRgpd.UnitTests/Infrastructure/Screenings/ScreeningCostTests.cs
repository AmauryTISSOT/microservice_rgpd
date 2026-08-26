using System.Diagnostics;
using System.Globalization;
using MicroserviceRgpd.Core.Screenings;
using MicroserviceRgpd.UnitTests.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Infrastructure.Screenings;

/// <summary>
/// La <b>part moteur du budget</b> (#156), tenue sur un relevé de la taille du plus gros schéma du
/// corpus — 5 382 colonnes chez Dolibarr.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce n'est pas une re-mesure du banc, et ce n'est surtout pas une mesure de coût.</b> Le banc
/// a mesuré le coût sous contention, sur une machine nommée, avec un protocole écrit — p95 ≤ 0,103
/// ms/colonne pour les montages à dictionnaire, contre 29,1 ms/colonne pour le modèle CPU qui a été
/// éliminé par là. Une machine d'intégration partagée ne reproduit pas ce chiffre et n'a pas à
/// essayer.
/// </para>
/// <para>
/// <b>Ce qui est gardé ici est l'<i>ordre de grandeur</i></b>, avec une marge délibérément large :
/// le seuil est deux ordres de grandeur au-dessus de la mesure du banc. Il ne dit pas « le moteur
/// est rapide » — il dit qu'aucune régression n'a fait passer la détection d'un balayage linéaire à
/// autre chose, ce qui est le seul mode de panne qui ferait franchir la borne éliminatoire du banc
/// à un dictionnaire. Un seuil serré ferait rougir la construction un jour de machine chargée, et
/// un test qui crie faux finit ignoré.
/// </para>
/// </remarks>
public class ScreeningCostTests
{
  /// <summary>La taille du plus gros schéma du corpus, sur lequel le banc a chiffré le geste entier.</summary>
  private const int DolibarrSized = 5_382;

  /// <summary>
  /// Le plafond gardé, en millisecondes par colonne et <b>en moyenne</b> — jamais en p95, qui est
  /// une mesure du banc et le reste. Le banc a mesuré p95 ≤ 0,103 ; le portage rend environ 0,015
  /// sur une machine de développement ; on garde 1, soit dix fois la borne du banc et deux ordres
  /// de grandeur au-dessus du portage. La marge est la contrepartie assumée de mesurer sur une
  /// machine que personne n'a décrite.
  /// </summary>
  private const double MeanCeilingInMillisecondsPerColumn = 1.0;

  /// <summary>Le moteur reste linéaire dans la taille du relevé, et très loin de la borne du banc.</summary>
  [Fact]
  public async Task StaysFarUnderTheBudgetOnAListingTheSizeOfTheLargestSchema()
  {
    var engine = AScreeningEngine.Wired();
    var listing = ABigListing();

    // Un premier passage à part : le chargement des lexiques et la compilation à la volée ne sont
    // pas le geste qu'on mesure, et ils n'ont lieu qu'une fois pour la vie du service.
    await engine.ScreenAsync(listing, IScreeningEngine.NoPreviews);

    var clock = Stopwatch.StartNew();
    var screened = await engine.ScreenAsync(listing, IScreeningEngine.NoPreviews);
    clock.Stop();

    screened.Columns.Count.ShouldBe(DolibarrSized);
    (clock.Elapsed.TotalMilliseconds / DolibarrSized).ShouldBeLessThan(MeanCeilingInMillisecondsPerColumn);
  }

  /// <summary>
  /// Un relevé de la taille de Dolibarr, fait de noms réalistes : des colonnes qui déclenchent, des
  /// colonnes qui ne déclenchent pas, et des noms longs qui font travailler la découpe.
  /// </summary>
  private static ColumnListing ABigListing()
  {
    var names = new[]
    {
      "id", "email", "nom_complet", "adresseLivraison", "prix_centimes", "date_naissance",
      "commentaire_interne", "reference_psp", "mot_de_passe_hash", "libelle", "actif",
    };

    var columns = new string[DolibarrSized];

    for (var index = 0; index < DolibarrSized; index++)
    {
      var table = string.Create(CultureInfo.InvariantCulture, $"llx_table_{index / 20}");

      columns[index] = APivot.Column(
        string.Create(CultureInfo.InvariantCulture, $"{names[index % names.Length]}_{index % 7}"),
        table: table,
        position: (index % 20) + 1,
        tableComment: "table applicative");
    }

    var outcome = ColumnListingIngestion.Ingest(APivot.Paste(columns));

    outcome.Refusal.ShouldBeNull();

    return outcome.Listing!;
  }
}
