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

  private LedgerFact(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'humain qui relit la preuve. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
