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

  private LedgerFact(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'humain qui relit la preuve. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
