namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Ce qu'un <c>Adapter</c> sait faire sur un <see cref="DeclaredSystem"/> donné. Quatre valeurs
/// dont les droits se composent, parce que le catalogue exige un grain <b>plus fin</b> que le
/// droit : la comptabilité scellée dix ans peut localiser et lire, elle ne pourra jamais effacer —
/// ce qui est indicible en « effacement : oui/non ».
/// <para>
/// <b>Elle n'entre pas au noyau partagé.</b> La <c>Qualification</c> ne connaît que l'instant d'un
/// verdict et n'a aucun système à toucher : <c>Capability</c> est du <c>Casework</c> pur, et le
/// noyau partagé vaut par sa petitesse — voir <c>docs/adr/0002</c>.
/// </para>
/// <para>
/// <b>Les quatre valeurs sont déclarables dès maintenant ; deux seulement seront exercées.</b>
/// <see cref="Erase"/> et <see cref="Rectify"/> entrent au catalogue sans qu'aucun code ne les
/// appelle, parce qu'un client dont l'<c>Adapter</c> sait effacer doit pouvoir le déclarer le jour
/// où il le construit, et non le jour où le service saura s'en servir. Une capacité déclarée que
/// personne n'exerce est une déclaration exacte ; une capacité indéclarable serait un mensonge du
/// catalogue.
/// </para>
/// </summary>
/// <remarks>
/// Le nom retenu est <c>Capability</c> et non <c>Operation</c> : l'art. 4.2 du RGPD définit le
/// traitement comme « toute <b>opération</b> appliquée à des données », le mot y est pris dans un
/// sens plus large et non compositionnel.
/// </remarks>
public sealed class Capability : SmartEnum<Capability>
{
  /// <summary>
  /// Dire si des données rattachables aux <c>Designations</c> existent dans ce système, et combien.
  /// <b>C'est le plancher</b> : sans lui, une affirmation n'a pas de dénominateur — « le client
  /// déclare avoir effacé » est du vide, « le client déclarait 12 enregistrements, puis 0 » est une
  /// trace. C'est aussi la première marche, une requête et rien de destructeur.
  /// </summary>
  public static readonly Capability Locate = new(nameof(Locate), 0, "localiser");

  /// <summary>Rendre les données rattachées, dans le vocabulaire de l'application et sous aucune forme imposée.</summary>
  public static readonly Capability Read = new(nameof(Read), 1, "lire");

  /// <summary>Supprimer les données rattachées. Déclarable dès ce lot, exercée à partir du lot 3.</summary>
  public static readonly Capability Erase = new(nameof(Erase), 2, "effacer");

  /// <summary>Corriger les données rattachées. Déclarable dès ce lot, exercée plus tard.</summary>
  public static readonly Capability Rectify = new(nameof(Rectify), 3, "rectifier");

  private Capability(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
