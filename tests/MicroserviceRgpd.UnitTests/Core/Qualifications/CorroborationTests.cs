using MicroserviceRgpd.Core.Qualifications;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UnitTests.Core.Qualifications;

/// <summary>
/// La règle d'entrecontrôle des deux moteurs, exercée <b>sans réseau, sans base et sans container</b>.
/// C'est ce qui justifie qu'elle vive dans le domaine : la seule règle du service qui décide de ce
/// qu'un opérateur humain lira ne doit dépendre d'aucune infrastructure pour être vérifiée.
/// </summary>
public class CorroborationTests
{
  private static readonly QualificationOpinion Erasure =
    Reasoned([DataSubjectRight.Erasure], DeclaredConfidence.High);

  private static readonly QualificationOpinion LexiconErasure = Lexicon(DataSubjectRight.Erasure);

  /// <summary>
  /// Première ligne du tableau, et <b>première évaluée</b> : la divergence l'emporte sur la
  /// confiance, parce qu'elle est le seul signal à ne rien devoir à l'auto-évaluation du moteur.
  /// </summary>
  [Fact]
  public void CallsTwoOpinionsThatDisagreeContested()
  {
    var corroboration = Corroboration.Between(Erasure, Lexicon(DataSubjectRight.Objection));

    corroboration.ReviewSignal.ShouldBe(ReviewSignal.Contested);
  }

  [Fact]
  public void CallsTwoAgreeingOpinionsCorroboratedWhenTheConfidenceIsHigh()
  {
    var corroboration = Corroboration.Between(Erasure, LexiconErasure);

    corroboration.ReviewSignal.ShouldBe(ReviewSignal.Corroborated);
  }

  /// <summary>
  /// L'accord ne suffit pas : « corroboré » veut dire contrôle indépendant favorable <b>et</b>
  /// confiance haute. Un moteur qui doute de lui-même n'achète pas le signal le plus rassurant.
  /// </summary>
  [Theory]
  [InlineData(DeclaredConfidence.Medium)]
  [InlineData(DeclaredConfidence.Low)]
  [InlineData(null)]
  public void CallsTwoAgreeingOpinionsNeedsReviewWhenTheConfidenceIsNotHigh(DeclaredConfidence? confidence)
  {
    var corroboration = Corroboration.Between(
      Reasoned([DataSubjectRight.Erasure], confidence),
      LexiconErasure);

    corroboration.ReviewSignal.ShouldBe(ReviewSignal.NeedsReview);
  }

  /// <summary>
  /// Quatrième ligne : un seul avis, donc aucun contrôle. « À relire » le dit exactement, et reste
  /// le seul signal atteignable — quel que soit celui des deux moteurs qui manque.
  /// </summary>
  [Fact]
  public void CallsALoneOpinionNeedsReviewWhicheverEngineIsMissing()
  {
    Corroboration.Between(Erasure, null).ReviewSignal.ShouldBe(ReviewSignal.NeedsReview);
    Corroboration.Between(null, LexiconErasure).ReviewSignal.ShouldBe(ReviewSignal.NeedsReview);
  }

  /// <summary>
  /// La comparaison est une égalité d'ensembles : deux moteurs qui reconnaissent les mêmes droits
  /// dans un ordre différent s'accordent. L'inverse ferait dépendre le signal de l'ordre de sortie
  /// d'un modèle, qui n'en a aucun.
  /// </summary>
  [Fact]
  public void ComparesTheRightsAsSetsRatherThanAsOrderedLists()
  {
    var corroboration = Corroboration.Between(
      Reasoned([DataSubjectRight.Access, DataSubjectRight.Erasure], DeclaredConfidence.High),
      Lexicon(DataSubjectRight.Erasure, DataSubjectRight.Access));

    corroboration.ReviewSignal.ShouldBe(ReviewSignal.Corroborated);
  }

  /// <summary>
  /// Le lexique est <b>détecteur, jamais contributeur</b> en marche nominale : il conteste le verdict,
  /// il ne le corrige pas, et les droits rendus restent ceux du moteur principal.
  /// </summary>
  [Fact]
  public void RendersTheVerdictOfThePrincipalEngineEvenWhenTheLexiconDisagrees()
  {
    var corroboration = Corroboration.Between(Erasure, Lexicon(DataSubjectRight.Objection));

    corroboration.Qualification.ShouldBe(Qualification.Of([DataSubjectRight.Erasure]));
  }

  /// <summary>
  /// L'autre rôle du lexique, et il n'arrive que seul : contributeur de dernier recours, quand le
  /// verdict manque. Les deux rôles ne coexistent jamais dans une même réponse.
  /// </summary>
  [Fact]
  public void FallsBackOnTheLexiconVerdictWhenNoPrincipalOpinionCame()
  {
    var corroboration = Corroboration.Between(null, Lexicon(DataSubjectRight.Objection));

    corroboration.Qualification.ShouldBe(Qualification.Of([DataSubjectRight.Objection]));
  }

  [Fact]
  public void SaysTheServiceWasWholeOnlyWhenBothOpinionsCame()
  {
    Corroboration.Between(Erasure, LexiconErasure).Degraded.ShouldBeFalse();
    Corroboration.Between(Erasure, null).Degraded.ShouldBeTrue();
    Corroboration.Between(null, LexiconErasure).Degraded.ShouldBeTrue();
  }

  /// <summary>
  /// Contrainte de contrat, non négociable et vérifiée <b>quel que soit le chemin</b> : aucun mode
  /// dégradé ne peut se présenter comme corroboré. Une confiance haute survivant à l'absence du
  /// lexique suffirait à l'obtenir si la règle lisait la confiance avant de compter les avis.
  /// </summary>
  [Fact]
  public void NeverPresentsADegradedQualificationAsCorroborated()
  {
    Corroboration.Between(Erasure, null).ReviewSignal.ShouldNotBe(ReviewSignal.Corroborated);
    Corroboration.Between(null, LexiconErasure).ReviewSignal.ShouldNotBe(ReviewSignal.Corroborated);
  }

  [Fact]
  public void CarriesTheJustificationOfThePrincipalEngineWhenItRenderedOne()
  {
    var corroboration = Corroboration.Between(Erasure, LexiconErasure);

    corroboration.Justification.ShouldBe("Le texte demande la suppression des données.");
  }

  /// <summary>
  /// Le repli lexical est <b>muet</b> : le lexique ne justifie rien, et lui fabriquer une phrase
  /// mentirait à l'opérateur au moment précis où le service se trompe le plus.
  /// </summary>
  [Fact]
  public void StaysSilentWhenTheLexiconAloneRenderedTheVerdict()
  {
    var corroboration = Corroboration.Between(null, LexiconErasure);

    corroboration.Justification.ShouldBeNull();
  }

  /// <summary>
  /// Deux avis absents ne font pas une qualification faible : il n'y a rien à rendre, et le domaine
  /// refuse plutôt que de forger un verdict que personne n'a prononcé.
  /// </summary>
  [Fact]
  public void RefusesToCorroborateNothingAtAll()
  {
    Should.Throw<ArgumentException>(() => Corroboration.Between(null, null));
  }

  private static QualificationOpinion Reasoned(
    DataSubjectRight[] rights,
    DeclaredConfidence? confidence)
  {
    return new QualificationOpinion(
      Qualification.Of(rights),
      AnEngine.HoldingTheVerdict,
      confidence,
      "Le texte demande la suppression des données.");
  }

  /// <summary>Le lexique tel qu'il est réellement : des droits, et pas un mot de plus.</summary>
  private static QualificationOpinion Lexicon(params DataSubjectRight[] rights)
  {
    return new QualificationOpinion(Qualification.Of(rights), AnEngine.HoldingTheLexicon);
  }
}
