namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// <b>Rien ne traverse d'un contexte à l'autre, sauf ce qui est écrit ici.</b> La règle s'énonce par
/// ce qu'elle <b>permet</b> — deux traversées, toutes deux vers le noyau partagé — et tout le reste
/// est interdit par construction : voir <c>docs/adr/0003</c>.
/// <para>
/// L'énoncé à l'envers n'est pas une coquetterie. Une liste d'interdits ne protège que ce qu'on a
/// pensé à y écrire : elle aurait laissé ouvertes en silence la traversée par laquelle l'instant du
/// verdict se mettrait à connaître la durée de l'instruction, et les trois par lesquelles le noyau
/// partagé — censé n'appartenir à personne — se mettrait à dépendre d'un contexte. Et un quatrième
/// contexte serait né <b>non gardé</b>, jusqu'à ce que quelqu'un pense à allonger la liste. Ici il
/// naît interdit partout, et c'est à lui d'écrire sa dérogation.
/// </para>
/// <para>
/// Le contrôle se fait au niveau de l'<b>IL</b>, et non des signatures : un gestionnaire qui appelle
/// le moteur d'en face depuis un corps de méthode n'expose rien dans sa surface, et c'est précisément
/// la fuite que l'on craint. Ce que l'inspecteur voit vraiment est établi par
/// <see cref="ContextInspectorTests"/>, sur des témoins écrits pour ça.
/// </para>
/// <para>
/// ⚠️ Ce que l'IL ne peut pas montrer, écrit ici plutôt que découvert un jour de panne : une
/// <c>const</c> lue d'un contexte à l'autre <b>disparaît</b> du compilé — le compilateur en recopie
/// la valeur sur place. Le garde ne la voit pas, et n'a rien à voir : il ne reste aucune dépendance,
/// ni à la compilation ni à l'exécution. C'est une duplication de source, pas un couplage.
/// </para>
/// <para>
/// ⚠️ Tant qu'un contexte n'a pas de code, ses règles ne trouvent rien à examiner : elles sont vertes
/// par vacuité. C'est voulu — on pose le garde <b>avant</b> le contexte qu'il garde, pour que sa
/// première ligne naisse déjà sous surveillance. C'était vrai de <c>Casework</c> en son temps ; ça
/// l'est de <c>Screening</c> aujourd'hui, dont aucune des quatre couches ne porte encore un dossier.
/// </para>
/// </summary>
public class ContextIsolationTests
{
  /// <summary>
  /// Les <b>seules</b> traversées permises, et elles vont toutes deux vers le noyau partagé : la
  /// taxonomie des droits n'est le modèle d'aucun contexte — son auteur est le RGPD, articles 15
  /// à 21 — et les deux qui parlent de droits s'y <i>conforment</i> sans la <i>posséder</i>.
  /// <para>
  /// La liste est écrite en dur, comme celle de <see cref="SharedKernelTests"/> et pour le même
  /// motif : y ajouter une ligne doit demander un geste délibéré, et ce geste est de niveau ADR.
  /// </para>
  /// </summary>
  private static readonly (string From, string To)[] Permitted =
  [
    (ContextInspector.Qualification, ContextInspector.SharedKernel),
    (ContextInspector.Casework, ContextInspector.SharedKernel),
  ];

  /// <summary>
  /// La matrice, dépliée : chaque paire ordonnée qui n'est pas permise, contre chaque assemblage de
  /// production. Un assemblage oublié se lirait comme une règle respectée — d'où
  /// <see cref="ProductionAssembly.All"/>, écrite elle aussi en toutes lettres.
  /// </summary>
  public static TheoryData<string, string, string> ForbiddenCrossings
  {
    get
    {
      var forbidden = new TheoryData<string, string, string>();

      foreach (var assembly in ProductionAssembly.All)
      {
        foreach (var from in ContextInspector.All)
        {
          foreach (var to in ContextInspector.All.Where(target => target != from && !Permitted.Contains((from, target))))
          {
            forbidden.Add(assembly, from, to);
          }
        }
      }

      return forbidden;
    }
  }

  [Theory]
  [MemberData(nameof(ForbiddenCrossings))]
  public void NothingCrossesExceptWhatIsWrittenDown(string assembly, string from, string to)
  {
    var crossings = ContextInspector.Inspect(ProductionAssembly.PathOf(assembly), from, to);

    crossings.ShouldBeEmpty(
      $"{assembly} porte une dépendance {from} → {to} :" + Environment.NewLine +
      string.Join(Environment.NewLine, crossings) + Environment.NewLine +
      Motive(from, to));
  }

  /// <summary>
  /// La liste blanche ne grossit pas toute seule. C'est elle, et elle seule, qui dit ce que la
  /// matrice permet : un ajout discret y rouvrirait une frontière sans qu'aucun autre test ne
  /// change de couleur.
  /// </summary>
  [Fact]
  public void PermitsTwoCrossingsAndNoOthers()
  {
    Permitted.ShouldBe(
      [
        (ContextInspector.Qualification, ContextInspector.SharedKernel),
        (ContextInspector.Casework, ContextInspector.SharedKernel),
      ],
      "La liste blanche s'est élargie. Élargir une frontière est un geste de niveau ADR — " +
      "voir docs/adr/0003 — et non une ligne ajoutée en passant pour faire compiler.");
  }

  /// <summary>
  /// Le motif de chaque interdit, servi <b>avec l'échec</b>. Un garde qui dit « interdit » sans dire
  /// pourquoi se fait contourner par la première personne pressée : elle ne sait pas ce qu'elle
  /// casse.
  /// </summary>
  private static string Motive(string from, string to)
  {
    return (from, to) switch
    {
      (ContextInspector.Casework, ContextInspector.Qualification) =>
        "Une demande peut arriver déjà qualifiée : un Case s'instruit sans qu'aucune qualification " +
        "n'ait eu lieu, et le second contexte se démontre sans GPU. Passez par le noyau partagé, ou " +
        "par un qualificationId opaque.",

      (ContextInspector.Qualification, ContextInspector.Casework) =>
        "Qualification ne connaît que l'instant d'un verdict : elle ignore qu'un dossier existe, " +
        "s'ouvre et se clôt. Un fournisseur amont qui connaît son aval n'est plus optionnel.",

      (ContextInspector.Screening, ContextInspector.Casework) =>
        "Screening ne confronte jamais le Manifest — c'est la borne « Suggéré, jamais déclaré ». " +
        "Un dépistage qui sait ce qui est déjà déclaré est un pré-remplissage, et un Manifest " +
        "pré-rempli par une machine se lit comme complet.",

      (ContextInspector.Casework, ContextInspector.Screening) =>
        "Casework n'affiche pas de suggestions à côté d'une déclaration : c'est le pré-remplissage " +
        "par l'autre bout, et c'est la corrosion par voisinage que le troisième contexte existe " +
        "pour empêcher. Le grain du Manifest est le système, jamais la colonne.",

      (ContextInspector.SharedKernel, _) =>
        "Le noyau partagé n'appartient à personne : son auteur est le RGPD. Un noyau qui dépend " +
        "d'un contexte recouple les autres en silence, et c'est le pire montage DDD. Sortez ce " +
        "type vers le contexte qui le possède.",

      (ContextInspector.Screening, _) or (_, ContextInspector.Screening) =>
        "Screening vit avant toute demande et n'a aucune intersection avec le reste du dépôt — pas " +
        "même le noyau partagé. Il ne rattache jamais une colonne à un DataSubjectRight : une " +
        "colonne « courriel » ne relève pas d'un droit plutôt qu'un autre, elle relève de tous.",

      _ =>
        "Separate Ways : rien ne traverse cette frontière. Il n'y a pas de couche anticorruption, " +
        "parce qu'il n'y a rien à traduire.",
    };
  }
}
