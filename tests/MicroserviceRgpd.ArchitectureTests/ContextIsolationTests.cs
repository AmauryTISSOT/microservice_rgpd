namespace MicroserviceRgpd.ArchitectureTests;

/// <summary>
/// <b>Rien ne traverse d'un contexte à l'autre, sauf ce qui est écrit ici.</b> La règle s'énonce par
/// ce qu'elle <b>permet</b> — trois traversées, toutes vers le noyau partagé — et tout le reste
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
/// première ligne naisse déjà sous surveillance. C'était vrai de <c>Casework</c> en son temps, et de
/// <c>Screening</c> jusqu'à <c>Core/Screenings/</c>, qui est le premier dossier qu'il porte. C'est
/// vrai de <c>Requests</c> aujourd'hui : il n'a encore aucun type de production, aucune traversée
/// ne lui est permise, et il n'entrera dans
/// <see cref="FindsTheContextItClaimsToGuardSomewhereInProduction"/> qu'avec son premier dossier.
/// </para>
/// <para>
/// ⚠️ <b>Le premier code de <c>Screening</c> a immédiatement montré une chose que la vacuité
/// cachait</b> : l'inspecteur lisait <c>Ardalis.SharedKernel</c> — le paquet d'où vient le marqueur
/// <c>IAggregateRoot</c> — comme le noyau partagé du dépôt. Les deux contextes qui portaient du code
/// ont tous deux la traversée vers le noyau <b>permise</b>, si bien que le faux positif y restait
/// couvert ; <c>Screening</c>, à qui elle est refusée, se dénonçait sur son premier agrégat. La borne
/// vit dans <see cref="ContextInspector"/>, et deux témoins la tiennent des deux côtés.
/// </para>
/// </summary>
public class ContextIsolationTests
{
  /// <summary>
  /// Les <b>seules</b> traversées permises, et elles vont toutes vers le noyau partagé : la
  /// taxonomie des droits n'est le modèle d'aucun contexte — son auteur est le RGPD, articles 15
  /// à 21 — et les trois qui parlent de droits s'y <i>conforment</i> sans la <i>posséder</i>.
  /// <c>Configuration</c> est le troisième, depuis l'ADR-0016 : il associe à chacun des six droits
  /// l'adresse à laquelle le service l'exercera, et lit leur libellé et leur article sur le type.
  /// <para>
  /// La liste est écrite en dur, comme celle de <see cref="SharedKernelTests"/> et pour le même
  /// motif : y ajouter une ligne doit demander un geste délibéré, et ce geste est de niveau ADR.
  /// </para>
  /// </summary>
  private static readonly (string From, string To)[] Permitted =
  [
    (ContextInspector.Qualification, ContextInspector.SharedKernel),
    (ContextInspector.Casework, ContextInspector.SharedKernel),
    (ContextInspector.Configuration, ContextInspector.SharedKernel),
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
  public void PermitsThreeCrossingsAndNoOthers()
  {
    Permitted.ShouldBe(
      [
        (ContextInspector.Qualification, ContextInspector.SharedKernel),
        (ContextInspector.Casework, ContextInspector.SharedKernel),
        (ContextInspector.Configuration, ContextInspector.SharedKernel),
      ],
      "La liste blanche s'est élargie. Élargir une frontière est un geste de niveau ADR — " +
      "voir docs/adr/0003 — et non une ligne ajoutée en passant pour faire compiler.");
  }

  /// <summary>
  /// <b>La matrice a vraiment quelque chose à examiner.</b> Un contexte sans code rend ses règles
  /// vertes par vacuité, et <c>ContextRosterTests</c> n'attrape pas ce cas : il ancre le <b>nom</b>
  /// sur un glossaire, il ne promet pas qu'un dossier de code le porte.
  /// <para>
  /// Le vert d'un contexte pas encore écrit et le vert d'un garde qui ne trouve rien sont exactement
  /// le même vert. Ce test les sépare, et il le fait <b>par le nom que l'inspecteur emploie</b> : un
  /// dossier <c>Screenings/</c> reconnu par préfixe compte, un <c>Depistage/</c> qui ne le serait pas
  /// ferait rougir ici plutôt que de laisser un contexte entier hors surveillance.
  /// </para>
  /// </summary>
  [Theory]
  [InlineData(ContextInspector.Qualification)]
  [InlineData(ContextInspector.Casework)]
  [InlineData(ContextInspector.Screening)]
  [InlineData(ContextInspector.Configuration)]
  [InlineData(ContextInspector.SharedKernel)]
  public void FindsTheContextItClaimsToGuardSomewhereInProduction(string context)
  {
    var inhabitants = ProductionAssembly.All
      .SelectMany(assembly => ContextInspector.TypesIn(ProductionAssembly.PathOf(assembly), context))
      .ToList();

    inhabitants.ShouldNotBeEmpty(
      $"Aucun type de production n'habite « {context} » : ses règles sont vertes parce qu'elles ne " +
      "trouvent rien, et non parce que la frontière est tenue. Si le contexte a du code, c'est que " +
      "l'inspecteur ne reconnaît pas son dossier — voir la lecture par préfixe d'espace de noms.");
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
        "Screening ne confronte jamais le Manifest — c'est la clause " +
        "« Aucune modification vers le Manifest ». " +
        "Une détection qui sait ce qui est déjà déclaré est un pré-remplissage, et un Manifest " +
        "pré-rempli par une machine se lit comme complet.",

      (ContextInspector.Casework, ContextInspector.Screening) =>
        "Casework n'affiche pas de suggestions à côté d'une déclaration : c'est le pré-remplissage " +
        "par l'autre bout, et c'est la corrosion par voisinage que le troisième contexte existe " +
        "pour empêcher. Le grain du Manifest est le système, jamais la colonne.",

      (ContextInspector.Casework, ContextInspector.Configuration) =>
        "Le Case n'appelle pas encore par droit : il suit les DeclaredSystem, et le Paramétrage " +
        "n'est lu par aucune instruction. Le recâblage est une décision à venir — voir " +
        "docs/adr/0016 — et la ligne qui l'ouvrira sera écrite dans la liste blanche, pas ici.",

      (ContextInspector.Requests, ContextInspector.Qualification) =>
        "Le droit invoqué d'une demande est choisi par l'Operator, jamais lu d'un verdict : une " +
        "demande s'enregistre sans qu'aucune qualification n'ait eu lieu. Passez par le noyau " +
        "partagé, ou par un identifiant opaque.",

      (ContextInspector.Qualification, ContextInspector.Requests) =>
        "Qualification ne connaît que l'instant d'un verdict : elle ignore qu'une demande est " +
        "enregistrée. Un fournisseur amont qui connaît son aval n'est plus optionnel.",

      (ContextInspector.Requests, ContextInspector.SharedKernel) =>
        "La liste blanche n'ouvre rien à Requests. Ouvrir sa traversée vers le noyau partagé est " +
        "un geste de niveau ADR — voir docs/adr/0003 —, pas une ligne ajoutée pour faire compiler.",

      (ContextInspector.SharedKernel, _) =>
        "Le noyau partagé n'appartient à personne : son auteur est le RGPD. Un noyau qui dépend " +
        "d'un contexte recouple les autres en silence, et c'est le pire montage DDD. Sortez ce " +
        "type vers le contexte qui le possède.",

      (ContextInspector.Screening, _) or (_, ContextInspector.Screening) =>
        "Screening vit avant toute demande et n'a aucune intersection avec le reste du dépôt — pas " +
        "même le noyau partagé. Il ne rattache jamais une colonne à un DataSubjectRight : une " +
        "colonne « courriel » ne relève pas d'un droit plutôt qu'un autre, elle relève de tous.",

      (ContextInspector.Configuration, _) or (_, ContextInspector.Configuration) =>
        "Configuration ne connaît que le Paramétrage : un droit, une adresse. Il ne sait rien d'un " +
        "dossier, d'un verdict ni d'un relevé de colonnes, et aucun d'eux ne le lit encore — voir " +
        "docs/adr/0016.",

      _ =>
        "Separate Ways : rien ne traverse cette frontière. Il n'y a pas de couche anticorruption, " +
        "parce qu'il n'y a rien à traduire.",
    };
  }
}
