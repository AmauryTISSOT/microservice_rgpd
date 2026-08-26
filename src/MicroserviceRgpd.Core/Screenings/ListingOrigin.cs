namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// D'où vient le <c>ColumnListing</c> qu'un <see cref="Screening"/> a lu : l'<c>Operator</c> l'a
/// <see cref="Pasted"/>, ou le service l'a <see cref="Scanned"/>. Trois cas, et le troisième est le
/// <b>cas nul</b> : il existe pour être refusé.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le cas nul est en position 0, et c'est tout l'objet du type.</b> Sans lui,
/// <see cref="Pasted"/> serait la valeur qu'on obtient par oubli — un <c>default</c>, une colonne
/// remplie par défaut, un champ qu'un chemin d'écriture neuf ne pense pas à poser —, et la panne
/// rentrerait par la porte de derrière : un rapport <b>scanné</b> se lirait comme collé, et la
/// <see cref="IncompletenessClause"/> qu'il rend affirmerait que le service n'a jamais vu une seule
/// valeur. <see cref="Unspecified"/> est donc refusé <b>bruyamment</b> — à l'enregistrement par
/// <see cref="KnownOrThrow"/>, à l'affichage par <c>Screening.Origin</c>, qui lève plutôt que de
/// rendre un rapport dont on ne sait pas ce qu'il a lu.
/// </para>
/// <para>
/// ⚠️ <b>Un booléen est refusé nommément.</b> <c>IsPasted</c> ferait du collage le cas normal et de
/// la connexion l'exception, quand les deux chemins coexistent et qu'<b>aucun ne remplace
/// l'autre</b> — et il n'aurait pas de cas nul, donc pas de refus possible.
/// </para>
/// <para>
/// <b>Elle est une propriété du <see cref="Screening"/>, enregistrée avec lui</b>, et non un fait de
/// la session qui l'a produit : la clause est rendue jusque sur l'archive, des mois après que la
/// chaîne de connexion a cessé d'exister. ⚠️ <b>Elle ne voyage pas dans <see cref="ScreeningCounts"/></b>
/// — la clause reçoit <b>deux</b> choses, les comptes et l'origine, et ce qu'elle a besoin de savoir
/// reste lisible dans sa signature.
/// </para>
/// <para>
/// ⚠️ <b>Elle ne dit pas la provenance, et c'est la confusion à ne pas faire.</b> Elle nomme le
/// <b>chemin</b> par lequel le relevé est entré, jamais la base dont il vient : un relevé scanné sur
/// l'adresse de la recette est aussi sincère, entier et faux qu'un relevé d'hier collé à la main.
/// <c>Enregistré, jamais vérifié</c> vaut des deux côtés.
/// </para>
/// </remarks>
public sealed class ListingOrigin : SmartEnum<ListingOrigin>
{
  /// <summary>
  /// <b>Le cas nul</b> : personne n'a dit d'où venait ce relevé. Il n'est jamais une réponse — il
  /// est ce qui reste quand la question n'a pas été posée, et il se refuse partout.
  /// </summary>
  public static readonly ListingOrigin Unspecified =
    new(nameof(Unspecified), 0, "origine non renseignée", isKnown: false);

  /// <summary>L'<c>Operator</c> a produit le relevé lui-même, avec la requête que le service lui fournit, et l'a collé.</summary>
  public static readonly ListingOrigin Pasted = new(nameof(Pasted), 1, "collé");

  /// <summary>Le service s'est connecté à la base et a relevé le schéma lui-même — c'est le <c>Scan</c>.</summary>
  public static readonly ListingOrigin Scanned = new(nameof(Scanned), 2, "scanné");

  private ListingOrigin(string name, int value, string frenchLabel, bool isKnown = true)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
    IsKnown = isKnown;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }

  /// <summary>
  /// Sait-on d'où vient ce relevé ? Faux du seul <see cref="Unspecified"/>, et c'est ce qui distingue
  /// une origine d'une case vide.
  /// </summary>
  public bool IsKnown { get; }

  /// <summary>
  /// Cette origine, si c'en est une — sinon une programmation fautive nommée. Le garde vit ici
  /// plutôt que chez chaque appelant : ce qu'un développeur lirait à trois endroits doit être écrit
  /// à un seul.
  /// </summary>
  /// <param name="origin">L'origine posée par le chemin d'écriture.</param>
  /// <param name="parameterName">Le paramètre qui la portait, pour que le refus se lise.</param>
  /// <exception cref="ArgumentNullException"><paramref name="origin"/> est absent.</exception>
  /// <exception cref="ArgumentException">L'origine est le cas nul : personne n'a dit d'où venait ce relevé.</exception>
  public static ListingOrigin KnownOrThrow(ListingOrigin origin, string parameterName)
  {
    ArgumentNullException.ThrowIfNull(origin);

    if (!origin.IsKnown)
    {
      throw new ArgumentException(
        $"« {origin.Name} » n'est pas une origine de relevé : c'est le cas nul, et il existe pour "
        + "être refusé. Un rapport dont on ne sait pas s'il a été collé ou scanné rend une clause "
        + "d'incomplétude qui ment sur ce que le service a lu.",
        parameterName);
    }

    return origin;
  }
}
