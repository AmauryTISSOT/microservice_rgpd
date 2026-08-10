namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Le degré de doute d'une <see cref="ScreenedColumn"/>, <b>dérivé de la règle qui a déclenché</b>.
/// Externe et déterministe : il ne doit rien à l'auto-évaluation d'un moteur.
/// </summary>
/// <remarks>
/// <para>
/// <b>La dérivation est structurelle, et c'est tout l'objet du type.</b> Les trois membres <i>sont</i>
/// les trois familles de règles — il n'existe aucun chemin qui construise un degré autrement qu'en
/// nommant la règle qui l'a produit. Pas de constructeur public, pas de fabrique prenant un nombre,
/// aucune valeur flottante nulle part : un moteur ne peut littéralement pas s'auto-évaluer ici, il
/// ne peut que dire ce qui a déclenché.
/// </para>
/// <para>
/// ⚠️ <b>Il ne se confond pas avec la <c>DeclaredConfidence</c> de <c>Qualification</c></b>, et c'est
/// l'homonyme le plus dangereux du dépôt : celle-là est une auto-évaluation qu'un moteur produit sur
/// lui-même, et elle a été mesurée <b>dégénérée</b> — 118 <c>Low</c>, 2 <c>High</c>, aucun
/// <c>Medium</c> sur 120. La leçon est payée ; le vocabulaire la garde.
/// </para>
/// <para>
/// ⚠️ <b>Deux degrés ne se comparent pas pour désigner un gagnant.</b> Quand plusieurs règles
/// déclenchent sur une colonne, c'est l'ordre d'arbitrage de <see cref="PersonalDataCategory"/> qui
/// tranche — jamais le degré, qui serait alors un score produisant une issue.
/// </para>
/// </remarks>
public sealed class RuleStrength : SmartEnum<RuleStrength>
{
  /// <summary>Le nom entier de la colonne figure au lexique. Il n'y a rien à interpréter.</summary>
  public static readonly RuleStrength ExactName =
    new(nameof(ExactName), 0, "correspondance exacte", "le nom entier de la colonne figure au lexique");

  /// <summary>Une racine, un préfixe, un suffixe rapproche le nom d'une entrée du lexique — <c>adr_l1</c> de <c>adresse</c>.</summary>
  public static readonly RuleStrength Morphological =
    new(nameof(Morphological), 1, "rapprochement morphologique", "une racine ou un affixe rapproche le nom d'une entrée du lexique");

  /// <summary>Ni le nom entier ni sa morphologie n'ont parlé : c'est la forme de la colonne qui a déclenché.</summary>
  public static readonly RuleStrength TypeHeuristic =
    new(nameof(TypeHeuristic), 2, "heuristique de type", "ni le nom ni sa morphologie n'ont parlé : la forme de la colonne a déclenché");

  private RuleStrength(string name, int value, string frenchLabel, string rule)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
    Rule = rule;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>
  /// Ce qu'a fait la règle qui a produit ce degré, en une phrase. Elle est attachée au membre parce
  /// que le degré <b>est</b> la règle : les séparer rouvrirait la porte à un degré qui ne vient de
  /// nulle part.
  /// </summary>
  public string Rule { get; }
}
