using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// <b>Le nom de base ne porte jamais un chemin.</b> Ce champ quitte le service dans la
/// <c>Cartographie</c>, et jusque dans le nom du fichier CSV.
/// </summary>
public class DatabaseNameTests
{
  private static Screening Named(string database)
  {
    return Screening.Of(
      ScreeningId.Next(),
      database,
      "sqlite",
      ListingOrigin.Pasted,
      AScreening.Engine,
      declaredColumnCount: 1,
      [ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh"))],
      AScreening.LaunchedOn);
  }

  /// <summary>
  /// ⚠️ <b>Le cas qui a motivé la règle</b> : côté SQLite, le SGBD ne connaît sa base que par son
  /// chemin, et le dossier parent est très exactement l'endroit où l'on écrit le nom du client.
  /// </summary>
  [Fact]
  public void KeepsOnlyTheFileNameOfASqlitePath()
  {
    Named("/srv/clients/mutuelle-des-cheminots/galette.sqlite").Database.ShouldBe("galette.sqlite");
  }

  /// <summary>
  /// ⚠️ <b>Les deux séparateurs sont traités, quel que soit l'hôte.</b> Le service tourne sous Linux
  /// et le relevé peut venir d'un poste Windows.
  /// </summary>
  [Fact]
  public void KeepsOnlyTheFileNameOfAWindowsPath()
  {
    Named(@"C:\clients\acme\galette.db").Database.ShouldBe("galette.db");
  }

  /// <summary>
  /// ⚠️ <b>La règle ne se branche pas sur le dialecte déclaré</b> : elle est « ce champ ne porte
  /// jamais un chemin », et non « ce dialecte-ci se nettoie ». La brancher sur le dialecte aurait
  /// fait dépendre une garantie de confidentialité d'une chaîne que le relevé recopie sans jamais la
  /// vérifier.
  /// </summary>
  [Fact]
  public void AppliesWhateverTheDeclaredDialectSays()
  {
    var screening = Screening.Of(
      ScreeningId.Next(),
      "/var/lib/clients/acme/galette_prod",
      "postgresql",
      ListingOrigin.Pasted,
      AScreening.Engine,
      declaredColumnCount: 1,
      [ScreenedColumn.NothingSeen(AScreening.AListedColumn("id_adh"))],
      AScreening.LaunchedOn);

    screening.Database.ShouldBe("galette_prod");
  }

  /// <summary>Un nom ordinaire traverse sans être touché.</summary>
  [Fact]
  public void LeavesAnOrdinaryNameAlone()
  {
    Named("galette_prod").Database.ShouldBe("galette_prod");
  }

  /// <summary>
  /// ⚠️ <b>La réduction <i>referme</i> la borne de cent caractères au lieu de la rouvrir</b> : un
  /// chemin trop long ne demande plus qu'on élargisse le plafond, il demande qu'on n'en garde que le
  /// dernier segment. Elle a donc lieu <b>avant</b> le contrôle de longueur.
  /// </summary>
  [Fact]
  public void ShortensBeforeTheLengthIsChecked()
  {
    var deepPath = "/srv/" + string.Join('/', Enumerable.Repeat("un-dossier-au-nom-long", 8))
      + "/galette.sqlite";

    deepPath.Length.ShouldBeGreaterThan(100);

    Named(deepPath).Database.ShouldBe("galette.sqlite");
  }

  /// <summary>
  /// ⚠️ <b>Un chemin terminé par un séparateur n'a pas de dernier segment</b>, et c'est le cas qui
  /// aurait fait échouer la règle là où elle sert : sans couper les séparateurs de fin, elle aurait
  /// rendu le chemin <b>entier</b> faute de trouver quoi garder.
  /// </summary>
  [Fact]
  public void KeepsTheLastSegmentOfAPathThatEndsWithASeparator()
  {
    Named("/srv/clients/mutuelle-des-cheminots/").Database.ShouldBe("mutuelle-des-cheminots");
  }

  /// <summary>
  /// <b>Un nom qui n'est <i>que</i> des séparateurs traverse tel quel</b> : il est absurde, mais il
  /// ne publie aucune arborescence, et c'est tout ce qui se joue ici. Le rendre vide aurait
  /// transformé un nom absurde en « nom absent », c'est-à-dire fait dire au refus autre chose que le
  /// problème.
  /// </summary>
  [Fact]
  public void LeavesANameMadeOnlyOfSeparatorsAsItIs()
  {
    Named("///").Database.ShouldBe("///");
  }
}
