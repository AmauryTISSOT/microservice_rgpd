namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Le degré de doute d'une <see cref="ScreenedColumn"/>, <b>dérivé de la règle qui a déclenché</b>.
/// Externe et déterministe : il ne doit rien à l'auto-évaluation d'un moteur.
/// </summary>
/// <remarks>
/// <para>
/// <b>La dérivation est structurelle, et c'est tout l'objet du type.</b> Les cinq membres <i>sont</i>
/// les cinq familles de règles — il n'existe aucun chemin qui construise un degré autrement qu'en
/// nommant la règle qui l'a produit. Pas de constructeur public, pas de fabrique prenant un nombre,
/// aucune valeur flottante nulle part : un moteur ne peut littéralement pas s'auto-évaluer ici, il
/// ne peut que dire ce qui a déclenché.
/// </para>
/// <para>
/// ⚠️ <b>Les deux membres de forme se séparent sur la <see cref="Threshold"/>, jamais sur le
/// nombre</b>, et c'est un résultat mesuré. Un test de <b>forme</b> ne se multiplie pas sur cinq
/// valeurs : la forme est une propriété de la colonne, et « cinq sur cinq » n'y est qu'<b>une seule
/// observation affichée cinq fois</b> — d'où 81 % de faux positifs sur un code postal, qu'on en lise
/// cinq ou cinquante. Un test de <b>clé de contrôle</b>, lui, se multiplie réellement : deux IBAN
/// distincts qui se vérifient valident à une chance sur dix milliards.
/// </para>
/// <para>
/// ⚠️ <b><see cref="TypeHeuristic"/> parle du type déclaré au schéma, jamais de la forme des
/// valeurs</b>, et la confusion est facile parce que le mot « forme » va aux deux. Le type est ce que
/// le SGBD annonce (<c>jsonb</c>) ; la forme est ce que les valeurs montrent. Les fondre rendrait
/// indistinguables une colonne qu'on n'a pas su lire et une colonne dont on a lu le contenu.
/// </para>
/// <para>
/// ⚠️ <b>Aucun membre ne varie avec le nombre de valeurs conformes, et il n'existe pas de membre
/// « corroboré ».</b> Un degré qui monterait de « deux sur cinq » à « cinq sur cinq », ou qui dirait
/// qu'une colonne a été reconnue deux fois, serait une <b>quantité de preuve</b> — c'est-à-dire un
/// score par un autre chemin. Une colonne <c>iban</c> dont les valeurs sont des IBAN porte
/// <see cref="ExactName"/>, et son motif porte les deux phrases.
/// </para>
/// <para>
/// ⚠️ <b>L'ordinal est renuméroté sans migration, et c'est ce qui l'a rendu gratuit</b> : la
/// persistance passe par le <c>Name</c>. Deux membres se sont insérés au milieu de l'ordre sans que
/// rien d'écrit en base ne se relise autrement.
/// </para>
/// <para>
/// ⚠️ <b>Il ne se confond pas avec la <c>DeclaredConfidence</c> de <c>Qualification</c></b>, et c'est
/// l'homonyme le plus dangereux du dépôt : celle-là est une auto-évaluation qu'un moteur produit sur
/// lui-même, et elle a été mesurée <b>dégénérée</b> — 118 <c>Low</c>, 2 <c>High</c>, aucun
/// <c>Medium</c> sur 120. La leçon est payée ; le vocabulaire la garde.
/// </para>
/// <para>
/// ⚠️ <b>Deux degrés ne se comparent pas pour désigner un gagnant.</b> Quand plusieurs règles
/// déclenchent sur une colonne, c'est l'ordre interne du moteur qui les a produites qui tranche —
/// jamais le degré, qui serait alors un score produisant une issue.
/// </para>
/// </remarks>
public sealed class RuleStrength : SmartEnum<RuleStrength>
{
  /// <summary>Le nom entier de la colonne figure au lexique. Il n'y a rien à interpréter.</summary>
  public static readonly RuleStrength ExactName =
    new(nameof(ExactName), 0, "correspondance exacte", "le nom entier de la colonne figure au lexique");

  /// <summary>
  /// Les valeurs lues portent une <b>clé de contrôle</b> qui se vérifie — IBAN, NIR, SIREN. Le seuil
  /// est de deux valeurs comptées, parce qu'une clé se multiplie réellement là où une forme ne le
  /// fait pas.
  /// </summary>
  public static readonly RuleStrength CheckedValueForm =
    new(
      nameof(CheckedValueForm),
      1,
      "clé de contrôle des valeurs",
      "les valeurs lues portent une clé de contrôle qui se vérifie",
      ValueFormThreshold.AtLeastTwoCountedValues);

  /// <summary>Une racine, un préfixe, un suffixe rapproche le nom d'une entrée du lexique — <c>adr_l1</c> de <c>adresse</c>.</summary>
  public static readonly RuleStrength Morphological =
    new(nameof(Morphological), 2, "rapprochement morphologique", "une racine ou un affixe rapproche le nom d'une entrée du lexique");

  /// <summary>
  /// Les valeurs lues ont une <b>forme</b> reconnue, <b>sans</b> clé de contrôle — courriel,
  /// téléphone, adresse IP. Le seuil est alors <b>toutes</b> les valeurs comptées : sans clé, une
  /// forme ne gagne rien à se répéter, et une seule valeur non conforme dit que la colonne n'est pas
  /// celle-là.
  /// </summary>
  public static readonly RuleStrength ValueForm =
    new(
      nameof(ValueForm),
      3,
      "forme des valeurs",
      "les valeurs lues ont une forme reconnue, sans clé de contrôle",
      ValueFormThreshold.EveryCountedValue);

  /// <summary>Ni le nom, ni sa morphologie, ni les valeurs n'ont parlé : c'est le <b>type déclaré</b> de la colonne qui a déclenché.</summary>
  public static readonly RuleStrength TypeHeuristic =
    new(nameof(TypeHeuristic), 4, "heuristique de type", "ni le nom ni ses valeurs n'ont parlé : le type déclaré de la colonne a déclenché");

  private RuleStrength(
    string name,
    int value,
    string frenchLabel,
    string rule,
    ValueFormThreshold? threshold = null)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
    Rule = rule;
    Threshold = threshold;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>
  /// Ce qu'a fait la règle qui a produit ce degré, en une phrase. Elle est attachée au membre parce
  /// que le degré <b>est</b> la règle : les séparer rouvrirait la porte à un degré qui ne vient de
  /// nulle part.
  /// </summary>
  public string Rule { get; }

  /// <summary>
  /// Combien de valeurs comptées il faut pour que ce degré soit atteint, ou <c>null</c> pour les
  /// trois degrés qui ne lisent aucune valeur.
  /// <para>
  /// ⚠️ <b>Attaché au membre, comme <see cref="Rule"/> l'est déjà, et jamais un paramètre de
  /// configuration.</b> Un seuil réglable ferait du degré une chose qu'on accorde, alors qu'il
  /// <b>est</b> la règle — seuil compris.
  /// </para>
  /// </summary>
  public ValueFormThreshold? Threshold { get; }
}

/// <summary>
/// Ce qu'il faut de valeurs comptées pour qu'une règle de forme déclenche. <b>Deux seuils, et pas un
/// de plus</b> : ils sont les deux membres de forme de <see cref="RuleStrength"/>, vus depuis leur
/// condition de déclenchement.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il ne se règle pas, et c'est le sens du type.</b> Un entier lu dans une configuration aurait
/// fait du seuil une chose qu'on accorde à un déploiement pressé ; ici, chaque seuil est attaché au
/// membre qui le porte, et changer l'un est changer une règle.
/// </para>
/// <para>
/// ⚠️ <b>Le compte dont il parle n'est pas le nombre de valeurs lues.</b> Trois valeurs ne comptent
/// nulle part — le <c>NULL</c>, la valeur tronquée par le SGBD, le doublon d'une valeur déjà
/// comptée —, ni au numérateur ni au dénominateur. Voir <see cref="ColumnPreview"/>.
/// </para>
/// </remarks>
public sealed class ValueFormThreshold
{
  /// <summary>
  /// <b>Deux valeurs comptées</b> reconnues suffisent, parce que la règle vérifie une clé de
  /// contrôle : deux clés distinctes qui se vérifient sont deux observations indépendantes, et non
  /// la même affichée deux fois. Les autres valeurs peuvent être quelconques — le motif dira alors
  /// « certaines ».
  /// </summary>
  public static readonly ValueFormThreshold AtLeastTwoCountedValues =
    new("au moins deux des valeurs comptées", minimum: 2, everyValue: false);

  /// <summary>
  /// <b>Toutes les valeurs comptées</b>, et au moins une. Sans clé de contrôle, une forme ne gagne
  /// rien à se répéter : ce qui la porte, c'est qu'aucune valeur lue ne la contredise.
  /// </summary>
  public static readonly ValueFormThreshold EveryCountedValue =
    new("toutes les valeurs comptées", minimum: 1, everyValue: true);

  private ValueFormThreshold(string frenchLabel, int minimum, bool everyValue)
  {
    FrenchLabel = frenchLabel;
    Minimum = minimum;
    EveryValue = everyValue;
  }

  /// <summary>Le libellé destiné à l'humain qui relit le seuil. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>Combien de valeurs comptées reconnues il faut, au moins.</summary>
  public int Minimum { get; }

  /// <summary>Faut-il en plus qu'<b>aucune</b> valeur comptée ne contredise la forme ?</summary>
  public bool EveryValue { get; }

  /// <summary>
  /// Ce seuil est-il atteint ? <paramref name="recognised"/> et <paramref name="counted"/> sont
  /// <b>tous deux</b> nets des trois valeurs qui ne comptent nulle part.
  /// </summary>
  /// <param name="recognised">Combien de valeurs comptées la règle a reconnues.</param>
  /// <param name="counted">Combien de valeurs ont été comptées en tout.</param>
  public bool IsReachedBy(int recognised, int counted)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(recognised);
    ArgumentOutOfRangeException.ThrowIfLessThan(counted, recognised);

    return recognised >= Minimum && (!EveryValue || recognised == counted);
  }
}
