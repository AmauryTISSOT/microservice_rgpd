namespace MicroserviceRgpd.Core.Casework.Ledger;

/// <summary>
/// Ce qu'une ligne du <c>Ledger</c> consigne. Vocabulaire <b>fermé</b>, pour que le contrôle
/// dénombre des faits plutôt qu'il ne lise de la prose.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le <c>Ledger</c> consigne les faits qui changent quelque chose, jamais leur répétition.</b>
/// Un appel s'y inscrira s'il rend un verdict différent du précédent, et pas autrement : trente-cinq
/// relances rendant le même <c>202</c> n'ont aucun signataire — c'est un affichage qui les a
/// déclenchées, non un humain — et noieraient sous du bruit de mécanique ce que le contrôle vient
/// lire.
/// </para>
/// <para>
/// <b>La liste s'allongera, et c'est un geste délibéré à chaque fois.</b> Un fait qu'aucune valeur
/// ne nomme n'entre pas au <c>Ledger</c> par une colonne de prose libre : il y entre par une valeur
/// que quelqu'un a décidé d'ajouter.
/// </para>
/// </remarks>
public sealed class LedgerFact : SmartEnum<LedgerFact>
{
  /// <summary>
  /// Un dossier s'est ouvert. C'est la <b>première ligne</b> de la matière de preuve d'un
  /// <c>Case</c>, et elle est déjà anonyme côté personne concernée.
  /// </summary>
  public static readonly LedgerFact CaseOpened = new(nameof(CaseOpened), 0, "dossier ouvert");

  /// <summary>
  /// Un <c>Adapter</c> a refusé un appel parce que le <b>secret</b> ne lui convenait pas. La
  /// tentative est datée dans le dossier au titre duquel elle est partie ; le désaccord, lui, se
  /// signale <b>une fois, au grain du déploiement</b> — un secret périmé vaut pour tous les
  /// dossiers à la fois, et rien n'a bougé dans celui-ci.
  /// </summary>
  public static readonly LedgerFact AdapterRefusedTheSecret =
    new(nameof(AdapterRefusedTheSecret), 1, "appel refusé : secret");

  /// <summary>
  /// Un <c>Adapter</c> a refusé un appel parce qu'il <b>ne sert pas ce système</b>. C'est un
  /// désaccord entre le <c>Manifest</c> et l'<c>Adapter</c>, et il ne se corrige jamais en silence :
  /// la preuve garde la tentative, un humain tranche lequel des deux avait tort.
  /// </summary>
  public static readonly LedgerFact AdapterDidNotServeTheSystem =
    new(nameof(AdapterDidNotServeTheSystem), 2, "appel refusé : système non servi");

  /// <summary>
  /// Un <c>Operator</c> a <b>déclaré</b> où en est le travail dû sur un système. C'est un constat
  /// signé, jamais un fait vérifié : <c>Done</c> prouve qu'on a déclaré l'avoir fait, et
  /// <c>Untreated</c> — l'aveu que personne ne l'a fait — s'inscrit aussi volontiers, le service
  /// n'ayant jamais le droit de bloquer la trace la plus précieuse du dispositif.
  /// </summary>
  public static readonly LedgerFact StepDeclared = new(nameof(StepDeclared), 3, "travail dû déclaré");

  /// <summary>
  /// Un <c>Operator</c> a <b>repris à son compte</b> un droit qu'une <c>Qualification</c> avait
  /// seulement proposé. C'est le geste par lequel une proposition de machine devient une
  /// reconnaissance d'humain : sans cette ligne, un droit qu'aucune personne n'a jamais pesé se
  /// relirait dans dix ans comme s'il avait été reconnu par quelqu'un.
  /// </summary>
  public static readonly LedgerFact ClaimConfirmed = new(nameof(ClaimConfirmed), 4, "droit confirmé");

  /// <summary>
  /// Un <c>Operator</c> a écrit <b>après coup</b> ce qu'il avait pesé de l'identité du demandeur.
  /// La ligne porte la méthode et sa date : elle dit qu'on a fini par peser, et <b>quand</b> — ce
  /// qui n'est pas la même chose que d'avoir pesé avant d'ouvrir le droit. Les deux lignes se lisent
  /// donc ensemble, et l'écart entre elles est un fait que le contrôle voit.
  /// </summary>
  public static readonly LedgerFact MotivationDeclared =
    new(nameof(MotivationDeclared), 5, "motivation d'identité déclarée");

  /// <summary>
  /// Un <c>Adapter</c> a <b>servi</b> un <c>Locate</c>. La ligne dit le système et <b>sous combien
  /// de désignations</b> on a cherché — jamais lesquelles, et jamais ce qui a été trouvé : le
  /// <c>Ledger</c> mesure l'ampleur d'une recherche, il ne dénombre pas les données de la personne.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Elle ne s'inscrit que si le verdict change.</b> Rouvrir un dossier relance les appels, et
  /// trente-cinq passages rendant le même « servi » n'ont aucun signataire — c'est un affichage qui
  /// les a déclenchés, non un humain. La règle est tenue par l'<b>appelant</b> : le <c>Ledger</c> ne
  /// se relit jamais.
  /// </remarks>
  public static readonly LedgerFact LocateServed = new(nameof(LocateServed), 6, "localisation servie");

  /// <summary>
  /// Un <c>Adapter</c> a <b>différé</b> un <c>Locate</c> en déclarant son échéance. Trois dates
  /// disent tout : appelé, échéance déclarée, résultat — et l'on ne saura jamais combien de fois on
  /// est repassé, faute de compteur que personne n'aurait signé.
  /// </summary>
  public static readonly LedgerFact LocateDeferred = new(nameof(LocateDeferred), 7, "localisation différée");

  /// <summary>
  /// Un <c>Operator</c> a <b>rattaché</b> une réserve : il a dit que cette ligne était bien celle de
  /// la personne. La ligne porte le compte du sac <b>après</b> l'arbitrage — c'est ainsi que la
  /// preuve dit « recherché sous 2 désignations, dont 1 ajoutée par arbitrage le 12/04 » sans jamais
  /// écrire une valeur.
  /// </summary>
  public static readonly LedgerFact ReservationAttached =
    new(nameof(ReservationAttached), 8, "réserve rattachée");

  /// <summary>
  /// Un <c>Operator</c> a <b>écarté</b> une réserve. Le fait est gardé au même titre que le
  /// rattachement : une exclusion par prudence dont personne ne répondrait serait l'<c>Omission
  /// silencieuse</c> sous sa forme la plus commode.
  /// </summary>
  public static readonly LedgerFact ReservationSetAside =
    new(nameof(ReservationSetAside), 9, "réserve écartée");

  /// <summary>
  /// Une <c>OpenQuestion</c> est née sur le dossier : <b>tous</b> les <c>Locate</c> ont rendu zéro, et
  /// la désignation ne suffit donc pas. Elle est datée, elle <b>n'arrête pas</b> le délai de
  /// l'art. 12.3, et elle est signée par l'application — c'est un constat du service, non l'issue
  /// d'un humain.
  /// </summary>
  public static readonly LedgerFact QuestionRaised = new(nameof(QuestionRaised), 10, "question posée");

  /// <summary>
  /// Un <c>Adapter</c> a <b>servi</b> un <c>Read</c>. La ligne dit le système, le <b>droit au titre
  /// duquel</b> on a lu, et sous combien de désignations — <b>jamais ce qu'il y avait dans la
  /// pièce</b>, ni son type, ni son nom, ni sa taille. Le <c>Ledger</c> ne porte aucun contenu, et
  /// il n'existe aucune colonne où il pourrait atterrir.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Elle ne s'inscrit que si le verdict change</b>, comme celle d'un <c>Locate</c> : rouvrir
  /// un dossier relance les appels, et trente-cinq passages rendant le même « servi » n'ont aucun
  /// signataire.
  /// </remarks>
  public static readonly LedgerFact ReadServed = new(nameof(ReadServed), 11, "lecture servie");

  /// <summary>
  /// Un <c>Adapter</c> a <b>différé</b> un <c>Read</c> en déclarant son échéance. Trois dates disent
  /// tout : appelé, échéance déclarée, résultat — et l'on ne saura jamais combien de fois on est
  /// repassé, faute de compteur que personne n'aurait signé.
  /// </summary>
  public static readonly LedgerFact ReadDeferred = new(nameof(ReadDeferred), 12, "lecture différée");

  /// <summary>
  /// Un <c>Operator</c> a <b>déclaré la remise</b> d'un droit. La ligne dit le droit, le jour, le
  /// signataire, et <b>combien de systèmes recensés la réponse couvrait</b> — « un fichier a été
  /// remis le 12/04 couvrant 2 systèmes sur 6 ».
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Ce dénombrement s'arrête ici et ne descend jamais dans la <c>CoverSheet</c>.</b> Son
  /// lecteur est le contrôle, qui juge une pratique et pour qui « 2 sur 6 » est une mesure. Écrit à
  /// la personne, le même chiffre lui affirmerait que le client a exactement six systèmes — donnant
  /// à une déclaration qui vieillit exprès l'autorité d'un recensement.
  /// </para>
  /// <para>
  /// ⚠️ <b>Elle ne dit rien de ce qui a été remis.</b> Ni type, ni nom de fichier, ni taille : la
  /// preuve dit qu'un fichier a été remis, jamais ce qu'il y avait dedans. Elle ne prouve pas non
  /// plus que la personne l'ait reçu — le service est greffier, pas témoin.
  /// </para>
  /// </remarks>
  public static readonly LedgerFact DeliveryDeclared =
    new(nameof(DeliveryDeclared), 13, "remise déclarée");

  /// <summary>
  /// Un <c>Operator</c> a déclaré que le service avait <b>répondu</b> sur un droit. La ligne dit le
  /// droit, le jour et le signataire — et rien de plus, parce qu'elle n'atteste rien de plus que
  /// l'acte de répondre.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Elle ne promet pas que le droit ait été honoré.</b> Le dossier peut porter des
  /// <c>Step</c> restés inatteints, et la preuve les garde ligne à ligne : c'est là, et pas dans
  /// cette ligne-ci, que le contrôle lit ce qui a réellement été fait.
  /// </remarks>
  public static readonly LedgerFact ClaimAnswered = new(nameof(ClaimAnswered), 14, "droit répondu");

  /// <summary>
  /// Un <c>Operator</c> a <b>clos le dossier</b>, et tout le nominatif a été détruit à cet instant.
  /// La ligne dit la cause, le jour et le signataire ; c'est d'elle que courent les cinq ans du
  /// <c>Ledger</c>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>C'est la dernière ligne d'un dossier, et souvent la seule qui restera longtemps.</b> Le
  /// <c>Case</c> qu'elle clôt n'a plus une désignation ; la preuve, elle, continue de nommer
  /// l'<c>Operator</c> — et son effacement se refuse légitimement, la preuve d'une procédure ne
  /// pouvant pas dépendre du consentement de qui l'a instruite.
  /// </para>
  /// <para>
  /// ⚠️ <b>Elle ne dit rien de l'état des <c>Step</c> ni des <c>Claim</c>.</b> La clôture ne propage
  /// rien : un travail resté <c>ToDo</c> a sa propre absence de ligne, et l'écart entre cette
  /// ligne-ci et ce que la preuve porte par ailleurs est très exactement ce que le contrôle vient
  /// lire.
  /// </para>
  /// </remarks>
  public static readonly LedgerFact CaseClosed = new(nameof(CaseClosed), 15, "dossier clos");

  /// <summary>
  /// Un <c>Operator</c> a <b>déclaré prolonger</b> de deux mois au titre de l'art. 12.3. La ligne dit
  /// le motif, le jour où il déclare avoir informé la personne, le jour de la déclaration et le
  /// signataire — c'est-à-dire la charge probatoire que l'article met sur lui, et que le service lui
  /// fait tenir.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Elle ne dit pas si le délai a bougé.</b> Le déplacement est un calcul sur la date de la
  /// déclaration, refait à chaque affichage ; l'écrire ici aurait persisté un dénominateur, et
  /// l'aurait fait relire dans cinq ans comme un fait signé plutôt que comme le calcul qu'il est. Le
  /// contrôle le refait depuis cette date et celle de l'ouverture, qui sont toutes deux au
  /// <c>Ledger</c>.
  /// </para>
  /// <para>
  /// ⚠️ <b>Une prolongation déclarée hors délai s'inscrit aussi</b>, et sans mention particulière :
  /// le fait est gardé tel quel, et l'écart entre les deux dates est exactement ce que le contrôle
  /// vient lire.
  /// </para>
  /// </remarks>
  public static readonly LedgerFact ExtensionDeclared =
    new(nameof(ExtensionDeclared), 16, "prolongation déclarée");

  private LedgerFact(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'humain qui relit la preuve. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
