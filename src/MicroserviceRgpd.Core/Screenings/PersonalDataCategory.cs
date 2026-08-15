namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// La taxonomie fermée de treize valeurs dans laquelle une <see cref="ScreenedColumn"/> puise. Elle
/// reconnaît <b>toujours</b> une valeur : l'absence de signalement est elle-même une valeur nommée,
/// <see cref="Unflagged"/>, jamais une ligne absente.
/// <para>
/// Chaque membre porte son <b>nom canonique anglais</b> et son <b>libellé français attaché</b> —
/// une seule source de vérité, aucune table de correspondance parallèle à maintenir ailleurs.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Elle n'est pas <c>DataSubjectRight</c> et ne s'y raccroche jamais.</b> Une colonne
/// « courriel » ne relève pas d'un droit plutôt qu'un autre, elle relève de tous. Elle est propre à
/// ce contexte, et son auteur n'est pas le RGPD.
/// </para>
/// <para>
/// ⚠️ <b>La sensibilité est une valeur, pas une seconde dimension.</b> La CNIL coche ses items
/// sensibles dans un bloc parallèle à ses catégories ordinaires, mais c'est un artefact de son
/// <b>grain</b> : une fiche de registre décrit un traitement entier, où « identité <b>et</b> santé »
/// coexistent forcément. Notre grain est la colonne, et à ce grain la coexistence s'effondre —
/// <c>confession</c> est une conviction religieuse, elle n'est pas <i>aussi</i> de l'état civil. Un
/// drapeau qui vaudrait vrai exactement quand la catégorie est déjà l'une des trois valeurs de droit
/// serait un champ redondant, et deux champs qu'un chemin d'écriture peut dissocier finissent par se
/// dissocier. « Montre-moi les colonnes sensibles » est donc un <b>calcul</b> :
/// <see cref="IsClosedByStatute"/>.
/// </para>
/// <para>
/// ⚠️ <b>L'art. 9 tient en deux valeurs, et l'énumération n'y porte pas l'item : le motif le
/// porte.</b> La santé se détache parce qu'elle cumule trois raisons — seul des huit items fréquent
/// dans un schéma réel, seul dont le texte singularise le régime, et première question d'un DPO. Les
/// sept autres tiennent dans <see cref="SpecialCategoryData"/>, et le motif nomme lequel. Le cas qui
/// tranche est la biométrie : le considérant 51 ne la range à l'art. 9 qu'« aux fins d'identifier
/// une personne de manière unique », une <b>finalité</b> qu'aucun lecteur de schéma ne connaît — une
/// valeur <c>Biometrics</c> affirmerait toujours plus que le service ne peut savoir.
/// </para>
/// <para>
/// <b>La gouvernance a deux étages, parce que les valeurs n'ont pas toutes le même auteur.</b> Les
/// trois valeurs marquées <see cref="IsClosedByStatute"/> sont fermées par le texte et ne bougent
/// que s'il bouge. Les valeurs ordinaires sont un découpage de travail : en ajouter une est une
/// <b>PR</b> dont le corps dit quelles colonnes réelles ne trouvaient pas de valeur, pourquoi
/// <see cref="PersonalDataUncategorised"/> ne suffisait pas, et où la valeur entre dans l'ordre
/// d'arbitrage. ⚠️ <b>En retirer ou en renommer une reste un ADR</b> : il n'y a pas d'appelant à
/// casser, mais il y a des arbitrages humains signés et datés qu'un rapport de détection neuf ne
/// reprend pas — ce
/// geste-là ne périme pas un contrat, il périme du travail humain.
/// </para>
/// </remarks>
public sealed class PersonalDataCategory : SmartEnum<PersonalDataCategory>
{
  /// <summary>Ce que l'art. 10 réserve aux autorités publiques. Rare, et sa rareté n'est pas un argument contre son existence.</summary>
  public static readonly PersonalDataCategory CriminalOffenceData =
    new(nameof(CriminalOffenceData), 0, "données relatives aux infractions", "RGPD art. 10", closedByStatute: true);

  /// <summary>La santé, détachée des autres items de l'art. 9 : le seul qui soit fréquent dans un schéma réel.</summary>
  public static readonly PersonalDataCategory HealthData =
    new(nameof(HealthData), 1, "données concernant la santé", "RGPD art. 9", closedByStatute: true);

  /// <summary>Les sept autres items de l'art. 9, dont le <b>motif</b> nomme lequel a déclenché.</summary>
  public static readonly PersonalDataCategory SpecialCategoryData =
    new(nameof(SpecialCategoryData), 2, "autre catégorie particulière", "RGPD art. 9", closedByStatute: true);

  /// <summary>Un mot de passe, une empreinte, un jeton — ce dont la fuite ouvre la porte au reste.</summary>
  public static readonly PersonalDataCategory AuthenticationSecret =
    new(nameof(AuthenticationSecret), 3, "secret d'authentification", "doctrinal");

  /// <summary>Un identifiant attribué par l'État : NIR, numéro fiscal, numéro d'allocataire.</summary>
  public static readonly PersonalDataCategory NationalIdentifier =
    new(nameof(NationalIdentifier), 4, "identifiant national", "CNIL, registre simplifié");

  /// <summary>Un IBAN, un montant, un encours — la situation économique de la personne.</summary>
  public static readonly PersonalDataCategory FinancialData =
    new(nameof(FinancialData), 5, "données économiques et financières", "CNIL, registre simplifié");

  /// <summary>Une position, un trajet, une géolocalisation.</summary>
  public static readonly PersonalDataCategory LocationData =
    new(nameof(LocationData), 6, "données de localisation", "CNIL, registre simplifié");

  /// <summary>Une adresse IP, un horodatage de session, un identifiant de traceur.</summary>
  public static readonly PersonalDataCategory ConnectionData =
    new(nameof(ConnectionData), 7, "données de connexion", "CNIL, registre simplifié");

  /// <summary>Le nom, le prénom, la date de naissance, la photographie.</summary>
  public static readonly PersonalDataCategory Identity =
    new(nameof(Identity), 8, "état civil et identité", "CNIL, registre simplifié");

  /// <summary>Une adresse postale, un courriel, un numéro de téléphone.</summary>
  public static readonly PersonalDataCategory ContactDetails =
    new(nameof(ContactDetails), 9, "coordonnées", "CNIL, registre simplifié");

  /// <summary>L'emploi, l'employeur, le service, la carrière.</summary>
  public static readonly PersonalDataCategory ProfessionalLife =
    new(nameof(ProfessionalLife), 10, "vie professionnelle", "CNIL, ancien modèle");

  /// <summary>
  /// Le repli : <b>vu, personnel, mais aucune autre valeur ne va</b>. C'est un verdict, pas un aveu
  /// d'ignorance — même geste que <c>OutOfScope</c>, dont le glossaire dit « ce n'est ni "inconnu",
  /// ni "non classé" : c'est un verdict ».
  /// <para>
  /// ⚠️ <b>Sans elle, le moteur n'a que deux issues et les deux mentent</b> : déguiser un doute en
  /// catégorie, ou retomber sur <see cref="Unflagged"/> et affirmer « rien vu » alors que quelque
  /// chose a été vu. Le premier mensonge est bruyant, le second est silencieux, et c'est le second
  /// qui coûte ici.
  /// </para>
  /// <para>
  /// <b>Son taux est l'instrument de mesure de la taxonomie</b>, et il n'est pas un défaut à
  /// minimiser : un taux qui monte est le signal qu'il manque une valeur. C'est aussi le logement
  /// d'une colonne <c>json</c>/<c>jsonb</c> — « conteneur libre : le contenu n'est pas lisible depuis
  /// le schéma » est un motif parfaitement rédigeable, donc ce n'est pas <see cref="Unflagged"/>.
  /// </para>
  /// </summary>
  public static readonly PersonalDataCategory PersonalDataUncategorised =
    new(nameof(PersonalDataUncategorised), 11, "donnée personnelle sans catégorie", "repli");

  /// <summary>
  /// La valeur rendue quand la détection <b>n'a rien signalé</b> sur une colonne.
  /// <para>
  /// ⚠️ <b>Elle dit ce que le service n'a pas fait, jamais ce que la colonne est.</b> Une colonne
  /// <see cref="Unflagged"/> n'est pas une colonne sans données personnelles — c'est une colonne où
  /// <b>rien n'a été vu</b>, ce qui est un constat sur la détection et non sur la donnée. Le service
  /// n'a jamais vu la donnée. C'est <c>Enregistré, jamais vérifié</c> appliqué au seul endroit de ce
  /// contexte où il serait tentant de l'oublier : une machine qui déclare une colonne inoffensive
  /// porte très exactement le témoignage qu'elle n'a pas les moyens de porter.
  /// </para>
  /// <para>
  /// ⚠️ <b>Il n'y a pas de second repli.</b> Une valeur « non personnelle » a été explicitement
  /// écartée : elle porterait un verdict d'innocuité sur une donnée que le service n'a jamais vue.
  /// </para>
  /// <para>
  /// <b>Elle n'est pas <see cref="PersonalDataUncategorised"/></b>, et la règle qui les départage est
  /// mécanique et se teste : <b>motif présent ⇔ ce n'est pas <see cref="Unflagged"/></b>.
  /// </para>
  /// </summary>
  public static readonly PersonalDataCategory Unflagged =
    new(nameof(Unflagged), 12, "rien signalé", "repli");

  private PersonalDataCategory(string name, int value, string frenchLabel, string origin, bool closedByStatute = false)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
    Origin = origin;
    IsClosedByStatute = closedByStatute;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>
  /// D'où la valeur vient — un article du RGPD, le registre simplifié de la CNIL, ou notre propre
  /// doctrine. Il est écrit parce que la gouvernance en dépend : ce qui a un auteur légal ne se
  /// retire pas comme ce qui n'en a pas.
  /// </summary>
  public string Origin { get; }

  /// <summary>
  /// Cette valeur est-elle fermée par le texte de loi ? Vrai pour les trois seules valeurs dont
  /// l'auteur n'est pas nous. C'est aussi le <b>calcul</b> qui répond à « montre-moi les colonnes
  /// sensibles », et la raison pour laquelle aucun drapeau parallèle n'existe.
  /// </summary>
  public bool IsClosedByStatute { get; }

  /// <summary>
  /// Cette valeur signale-t-elle quelque chose ? Vrai partout sauf <see cref="Unflagged"/>. C'est la
  /// moitié gauche de l'invariant du contexte — <b>motif présent ⇔ ce n'est pas
  /// <see cref="Unflagged"/></b> — et la seule lecture autorisée de « la ligne dit quelque chose ».
  /// </summary>
  public bool IsFlagged => this != Unflagged;

  /// <summary>
  /// Le rang d'arbitrage : plus il est bas, plus la valeur coûte cher à omettre. C'est
  /// <see cref="SmartEnum{TEnum,TValue}.Value"/> lui-même, et l'ordre de la déclaration est l'ordre
  /// de la table du glossaire, <b>figé</b>.
  /// </summary>
  public int ArbitrationRank => Value;

  /// <summary>
  /// Parmi plusieurs valeurs déclenchées, celle qui <b>coûte le plus cher à omettre</b>.
  /// <c>arret_maladie</c> est santé <i>et</i> vie professionnelle ; <c>email_pro</c> est coordonnées
  /// <i>et</i> vie professionnelle — et c'est l'ordre de la table qui départage, du plus au moins
  /// coûteux à omettre.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Le nom dit un ordre, jamais une issue, et ce n'est pas une coquetterie.</b> Cette méthode
  /// est appelée par un <b>moteur</b>, et la liste <c>_Avoid_</c> de l'<c>Aide à la décision</c>
  /// interdit de nommer une issue que la <i>machine</i> produirait — un <c>ArbitrationEngine</c> y
  /// tombe nommément. <c>Arbitrate</c> est réservé au geste de l'<c>Operator</c>, qui est seul à
  /// produire une issue : voir <see cref="Screening.Arbitrate"/>. Choisir le même verbe ici aurait
  /// donné un mot pour deux gestes dont tout le contexte s'emploie à dire qu'ils ne sont pas de même
  /// nature. Le <b>nom</b> de l'ordre, lui, reste « ordre d'arbitrage » — c'est le glossaire qui
  /// l'écrit — d'où <see cref="ArbitrationRank"/>.
  /// </para>
  /// <para>
  /// ⚠️ <b>Jamais la <see cref="RuleStrength"/>.</b> Comparer deux degrés pour désigner un gagnant
  /// serait un score qui produit une issue, ce que l'<c>Aide à la décision</c> interdit tout autant.
  /// Le motif, lui, peut dire ce qui a été écarté — « la règle <i>vie professionnelle</i> a aussi
  /// déclenché ».
  /// </para>
  /// <para>
  /// <b>Les moteurs héritent cet ordre ; aucun ne le redécide.</b> C'est la raison pour laquelle il
  /// vit ici et non dans le moteur : un second moteur qui recopierait l'ordre finirait par en avoir
  /// un autre.
  /// </para>
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="triggered"/> est absent.</exception>
  /// <exception cref="ArgumentException">Aucune valeur n'a déclenché — la détection rend alors <see cref="Unflagged"/>, jamais rien.</exception>
  public static PersonalDataCategory MostCostlyToOmit(IEnumerable<PersonalDataCategory> triggered)
  {
    ArgumentNullException.ThrowIfNull(triggered);

    var candidates = triggered.ToList();

    if (candidates.Count == 0)
    {
      throw new ArgumentException(
        "Aucune valeur n'a déclenché. L'absence de signalement s'écrit Unflagged, qui est une "
        + "valeur nommée : elle ne se départage pas, elle se pose.",
        nameof(triggered));
    }

    return candidates.MinBy(category => category.ArbitrationRank)!;
  }
}
