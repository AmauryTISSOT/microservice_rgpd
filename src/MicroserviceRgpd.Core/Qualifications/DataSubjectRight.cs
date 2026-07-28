namespace MicroserviceRgpd.Core.Qualifications;

/// <summary>
/// La taxonomie fermée de sept valeurs dans laquelle une qualification puise : six droits ouverts
/// par le RGPD, et <see cref="OutOfScope"/> qui dit qu'aucun d'eux n'a été reconnu.
/// <para>
/// Chaque membre porte son article et son libellé français <b>comme données attachées</b> : une
/// seule source de vérité, aucune table de correspondance parallèle à maintenir ailleurs.
/// </para>
/// <para>
/// <b>Règle d'évolution</b> — ajouter une valeur est une <b>rupture</b> du contrat public, pas une
/// extension : un appelant ayant écrit un <c>switch</c> exhaustif sur sept droits casse à la
/// huitième. La taxonomie est fermée par décision ; son extension est un événement de niveau ADR.
/// </para>
/// </summary>
public sealed class DataSubjectRight : SmartEnum<DataSubjectRight>
{
  /// <summary>Le droit d'obtenir communication des données traitées et des informations sur leur traitement.</summary>
  public static readonly DataSubjectRight Access = new(nameof(Access), 0, 15, "droit d'accès");

  /// <summary>Le droit de faire corriger des données inexactes ou compléter des données incomplètes.</summary>
  public static readonly DataSubjectRight Rectification = new(nameof(Rectification), 1, 16, "droit de rectification");

  /// <summary>Le droit de faire supprimer des données, dit aussi droit à l'oubli.</summary>
  public static readonly DataSubjectRight Erasure = new(nameof(Erasure), 2, 17, "droit à l'effacement");

  /// <summary>Le droit de faire geler le traitement de données sans les faire supprimer.</summary>
  public static readonly DataSubjectRight Restriction = new(nameof(Restriction), 3, 18, "droit à la limitation du traitement");

  /// <summary>Le droit de recevoir ses données dans un format réutilisable, ou de les faire transmettre.</summary>
  public static readonly DataSubjectRight Portability = new(nameof(Portability), 4, 20, "droit à la portabilité");

  /// <summary>Le droit de s'opposer à un traitement, notamment à la prospection.</summary>
  public static readonly DataSubjectRight Objection = new(nameof(Objection), 5, 21, "droit d'opposition");

  /// <summary>
  /// Le verdict rendu quand aucun des six droits n'est reconnu. Exclusif : il ne se combine jamais
  /// avec un droit. Il couvre aussi bien un texte étranger au RGPD qu'une demande relevant d'un
  /// droit resté hors de la taxonomie — déréférencement, droit à l'information, art. 22.
  /// Ce n'est ni « inconnu », ni « non classé » : c'est un verdict.
  /// </summary>
  public static readonly DataSubjectRight OutOfScope = new(nameof(OutOfScope), 6, null, "hors périmètre");

  private DataSubjectRight(string name, int value, int? article, string frenchLabel)
    : base(name, value)
  {
    Article = article;
    FrenchLabel = frenchLabel;
  }

  /// <summary>
  /// L'article du RGPD ouvrant ce droit, absent pour <see cref="OutOfScope"/> qui n'en a aucun.
  /// </summary>
  /// <remarks>
  /// La valeur entière du membre — <see cref="SmartEnum{TEnum,TValue}.Value"/> — est un ordinal
  /// stable et <b>jamais</b> le numéro d'article : sans quoi <see cref="OutOfScope"/> deviendrait
  /// un cas particulier faute d'article à porter.
  /// </remarks>
  public int? Article { get; }

  /// <summary>Le libellé destiné à l'opérateur humain. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
