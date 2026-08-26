using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UnitTests.Core.Screenings;

/// <summary>
/// Le cache mémoire des aperçus : <b>un seul jeu vivant</b>, et l'ancien évincé quand le suivant se
/// dépose.
/// </summary>
public class ScanPreviewsTests
{
  /// <summary>Les aperçus déposés se relisent, pour le rapport qu'ils accompagnent.</summary>
  [Fact]
  public void GivesBackThePreviewsOfTheReportTheyBelongTo()
  {
    var previews = new ScanPreviews();
    var report = ScreeningId.Next();
    var kept = AJeu();

    previews.Keep(report, kept);

    previews.Of(report).ShouldBe(kept);
  }

  /// <summary>
  /// ⚠️ <b>Un rapport qui part à l'archive n'a plus d'aperçus.</b> Les garder ferait du cache une
  /// rétention, alors que le geste qui archive est très exactement celui qui dépose le jeu suivant.
  /// </summary>
  [Fact]
  public void EvictsThePreviousSetWhenTheNextOneIsKept()
  {
    var previews = new ScanPreviews();
    var archived = ScreeningId.Next();

    previews.Keep(archived, AJeu());
    previews.Keep(ScreeningId.Next(), AJeu());

    previews.Of(archived).ShouldBeEmpty();
  }

  /// <summary>
  /// Un rapport que le cache ne connaît pas ne rend <b>rien</b> — ce qui est le cas d'un service qui
  /// vient de redémarrer, et le cas normal d'un rapport archivé.
  /// </summary>
  [Fact]
  public void GivesBackNothingForAReportItDoesNotHold()
  {
    new ScanPreviews().Of(ScreeningId.Next()).ShouldBeEmpty();
  }

  /// <summary>
  /// ⚠️ <b>L'éviction ne dépend pas de la voie par laquelle le rapport suivant est arrivé.</b> Un
  /// collage archive le rapport courant sans rien lire : sans cet oubli, les aperçus d'un rapport
  /// scanné — des valeurs réelles du client — resteraient en mémoire du processus indéfiniment.
  /// </summary>
  [Fact]
  public void ForgetsTheLivingSetWithoutKeepingAnother()
  {
    var previews = new ScanPreviews();
    var archived = ScreeningId.Next();

    previews.Keep(archived, AJeu());
    previews.Forget();

    previews.Of(archived).ShouldBeEmpty();
  }

  private static Dictionary<ColumnIdentity, ColumnPreview> AJeu()
  {
    return new Dictionary<ColumnIdentity, ColumnPreview>
    {
      [ColumnIdentity.Of("public", "adherents", "nom")] =
        ColumnPreview.Read([PreviewedValue.Of("Durand", 6)]),
    };
  }
}
