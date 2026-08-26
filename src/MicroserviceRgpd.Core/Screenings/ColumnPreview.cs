namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// L'aperçu d'une colonne : les quelques valeurs que le <c>Scan</c> y a lues, et que l'écran montre
/// à l'<c>Operator</c> <b>pendant qu'il arbitre</b>. Elles ne prouvent rien, elles n'entrent dans
/// aucun chiffre rendu, et elles meurent — voir <c>Rien de réel ne reste</c>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Un aperçu n'est jamais vide.</b> Il est <b>soit</b> des valeurs lues, <b>soit</b> une
/// <see cref="PreviewAbsenceReason"/> — jamais rien, et <b>jamais les deux à la fois</b>. Les deux
/// fabriques sont le seul chemin qui y mène, et il n'en existe aucun qui produise la troisième
/// forme.
/// </para>
/// <para>
/// ⚠️ <b>Les deux membres n'ont plus la même durée de vie, et l'interdiction de fond n'a pas
/// bougé.</b> Les valeurs vivent en mémoire du processus et meurent avec la session d'arbitrage ; la
/// raison, elle, <b>descend en base</b> sur la <see cref="ScreenedColumn"/> — sans quoi les quatre
/// comptes de la <see cref="IncompletenessClause"/> seraient incalculables une heure après le scan.
/// Ce qui reste vrai partout : jamais des valeurs <b>et</b> une raison.
/// </para>
/// <para>
/// ⚠️ <b>Le type descend dans <c>Core</c> pour que le port sortant le nomme, et rien ne le fait
/// ressortir.</b> Aucun champ de <see cref="ScreenedColumn"/>, de <see cref="ScreenedListing"/> ni
/// de <see cref="ColumnListing"/> ne sait porter une valeur lue : un aperçu n'entre <b>jamais</b>
/// dans un objet persisté, et c'est un garde de <c>ArchitectureTests</c> qui le tient plutôt qu'une
/// intention.
/// </para>
/// <para>
/// ⚠️ <b>Les deux bornes vivent ici, et elles n'ont pas d'autre source.</b> La requête de
/// prélèvement lit <see cref="MaxValues"/> ; la clause qui écrit « cinq valeurs au plus » le lit au
/// même endroit. Écrit à la main des deux côtés, le chiffre divergerait au premier changement de
/// borne — et c'est le texte rendu à l'<c>Operator</c> qui se mettrait à mentir, sans que rien ne
/// rougisse.
/// </para>
/// </remarks>
public sealed class ColumnPreview
{
  /// <summary>
  /// Combien de valeurs, au plus, un aperçu porte. <b>Ordre de grandeur, jamais paramètre de
  /// doctrine</b> : cinq, trois ou huit ne changent rien ; cinq cents change tout — ce n'est plus un
  /// aperçu qu'un humain lit de ses yeux, c'est de l'analyse de contenu, et c'est ce qui rouvre
  /// l'<c>ADR-0012</c>.
  /// </summary>
  public const int MaxValues = 5;

  /// <summary>
  /// La borne de coupure d'une valeur, en unités UTF-16. <b>254 : la plus longue valeur à motif
  /// utile</b> — 34 pour un IBAN, 254 pour un courriel (RFC 5321). Deux cents, chiffre qui a traîné
  /// dans un calcul de volume, coupait sous la borne du courriel : il ne coûtait rien de moins et
  /// coûtait des détections.
  /// </summary>
  public const int MaxValueLength = 254;

  private ColumnPreview(IReadOnlyList<PreviewedValue> values, PreviewAbsenceReason? absence)
  {
    Values = values;
    Absence = absence;
  }

  /// <summary>
  /// Les valeurs lues, dans l'ordre où le prélèvement les a rendues — <b>vide</b> quand l'aperçu
  /// porte une raison.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Ce sont les premières venues, et l'écran le dit.</b> Rien ne les trie : elles arrivent
  /// dans l'ordre physique du stockage, c'est-à-dire très souvent les <b>plus anciennes</b> lignes
  /// de la table — jeux d'essai, comptes de démonstration, données d'amorçage. C'est un biais à
  /// <b>dire</b>, pas à corriger : trier coûterait un balayage complet, prix que la base de
  /// production d'un tiers n'a pas à payer pour cinq valeurs qui ne prouvent rien.
  /// </remarks>
  public IReadOnlyList<PreviewedValue> Values { get; }

  /// <summary>
  /// Pourquoi il n'y a aucune valeur à montrer, ou <c>null</c> quand il y en a. C'est le second
  /// membre de « soit des valeurs, soit une raison », et le seul des deux qui soit enregistré.
  /// </summary>
  public PreviewAbsenceReason? Absence { get; }

  /// <summary>Cet aperçu porte-t-il des valeurs ? La négation exacte de « il porte une raison ».</summary>
  public bool CarriesValues => Absence is null;

  /// <summary>
  /// Un aperçu <b>de valeurs</b> : celles que le prélèvement a lues, dans son ordre.
  /// </summary>
  /// <param name="values">Les valeurs lues. Au moins une, au plus <see cref="MaxValues"/>.</param>
  /// <exception cref="ArgumentNullException"><paramref name="values"/> est absent, ou l'une des valeurs l'est.</exception>
  /// <exception cref="ArgumentException">
  /// Aucune valeur — un aperçu vide se dit par une raison nommée, jamais par le silence — ou plus
  /// que la borne de prélèvement.
  /// </exception>
  public static ColumnPreview Read(IEnumerable<PreviewedValue> values)
  {
    ArgumentNullException.ThrowIfNull(values);

    var read = values.ToList();

    if (read.Count == 0)
    {
      throw new ArgumentException(
        "Un aperçu sans valeur est une absence, et une absence se dit par une raison nommée : "
        + "employez Absent. Rendu vide, il se lirait comme « on n'a pas regardé cette colonne », "
        + "ce qui est l'Omission silencieuse réintroduite par une cellule vide.",
        nameof(values));
    }

    if (read.Count > MaxValues)
    {
      throw new ArgumentException(
        $"Un aperçu porte au plus {MaxValues} valeurs et on lui en donne {read.Count}. Au-delà, ce "
        + "n'est plus un aperçu qu'un humain lit de ses yeux : c'est de l'analyse de contenu, et la "
        + "frontière de l'ADR-0012 est franchie.",
        nameof(values));
    }

    if (read.Exists(value => value is null))
    {
      throw new ArgumentNullException(
        nameof(values),
        "Une valeur absente de la liste n'est pas un NULL lu : le NULL lu est PreviewedValue.NullValue, "
        + "qui porte son marqueur. Un trou dans la liste serait une valeur qu'on ne sait pas afficher.");
    }

    return new ColumnPreview(read, absence: null);
  }

  /// <summary>
  /// Un aperçu <b>sans valeur</b>, et la raison nommée de n'en porter aucune.
  /// </summary>
  /// <param name="reason">Laquelle des quatre familles s'applique.</param>
  /// <exception cref="ArgumentNullException"><paramref name="reason"/> est absent — c'est-à-dire l'absence muette que le type existe pour empêcher.</exception>
  public static ColumnPreview Absent(PreviewAbsenceReason reason)
  {
    ArgumentNullException.ThrowIfNull(reason);

    return new ColumnPreview([], reason);
  }
}

/// <summary>
/// <b>Une</b> valeur lue dans une colonne : le texte tel que le SGBD l'a coupé, et la
/// <b>longueur réelle</b> de ce qu'il portait.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b><c>NULL</c> et la chaîne vide sont des valeurs lues</b>, et non des absences. Cinq
/// <c>NULL</c> sont cinq valeurs lues : le prélèvement a parfaitement réussi et aucune raison n'est
/// due. Ils portent chacun un marqueur visible — <see cref="NullMarker"/>,
/// <see cref="EmptyMarker"/> — et <b>deux marqueurs plutôt qu'un</b> parce que ce sont deux choses
/// différentes en base. Rendus en cellules blanches, ils se liraient comme l'absence que la raison
/// nommée vient de chasser, un cran plus bas.
/// </para>
/// <para>
/// ⚠️ <b>La longueur réelle accompagne toute valeur, et ce n'est pas un ornement.</b> La coupure est
/// faite par le SGBD, avant la traversée du réseau ; une règle de forme écarte de son compte une
/// valeur <b>tronquée</b> — un courriel coupé n'est plus un courriel —, et cette clause serait
/// écrite sans jamais pouvoir s'appliquer si rien ne disait qu'une valeur l'a été.
/// </para>
/// </remarks>
public sealed class PreviewedValue
{
  /// <summary>Ce que l'écran montre à la place d'un <c>NULL</c> lu.</summary>
  public const string NullMarker = "⟨null⟩";

  /// <summary>Ce que l'écran montre à la place d'une chaîne vide lue.</summary>
  public const string EmptyMarker = "⟨vide⟩";

  private PreviewedValue(string? text, int actualLength)
  {
    Text = text;
    ActualLength = actualLength;
  }

  /// <summary>Le <c>NULL</c> lu. Une valeur, jamais une absence — et l'écran le dit par son marqueur.</summary>
  public static PreviewedValue NullValue { get; } = new(text: null, actualLength: 0);

  /// <summary>La chaîne vide lue. Elle n'est pas le <c>NULL</c>, et son marqueur non plus.</summary>
  public static PreviewedValue EmptyText { get; } = new(string.Empty, actualLength: 0);

  /// <summary>
  /// Le texte lu, <b>tel que le SGBD l'a coupé</b> — <c>null</c> pour le <c>NULL</c> lu, la chaîne
  /// vide pour la chaîne vide lue.
  /// </summary>
  public string? Text { get; }

  /// <summary>
  /// La longueur que la valeur avait <b>en base</b>, avant coupure. Égale à celle du texte quand
  /// rien n'a été coupé.
  /// </summary>
  public int ActualLength { get; }

  /// <summary>Le <c>NULL</c> lu se distingue de la chaîne vide, et les deux se distinguent d'un texte.</summary>
  public bool IsNull => Text is null;

  /// <summary>La chaîne vide lue — lue, et vide. Ce n'est ni un <c>NULL</c> ni une absence.</summary>
  public bool IsEmpty => Text?.Length == 0;

  /// <summary>
  /// Le SGBD a-t-il coupé cette valeur ? ⚠️ <b>C'est ce qui l'écarte du compte d'une règle de
  /// forme</b> : une valeur qui n'a pas été lue en entier n'a pas été lue.
  /// </summary>
  public bool IsTruncated => ActualLength > (Text?.Length ?? 0);

  /// <summary>
  /// Ce que l'<c>Operator</c> lit : le texte, ou le marqueur de ce qui n'a pas de texte à montrer.
  /// <b>Jamais une cellule blanche.</b>
  /// </summary>
  public string Display => Text switch
  {
    null => NullMarker,
    "" => EmptyMarker,
    var text => text,
  };

  /// <summary>
  /// Une valeur lue portant du texte, et la longueur qu'elle avait avant coupure.
  /// </summary>
  /// <param name="text">Le texte tel que le SGBD l'a rendu, déjà coupé par lui.</param>
  /// <param name="actualLength">La longueur réelle en base, jamais inférieure à celle du texte.</param>
  /// <exception cref="ArgumentNullException"><paramref name="text"/> est absent — le <c>NULL</c> lu est <see cref="NullValue"/>, qui porte son marqueur.</exception>
  /// <exception cref="ArgumentException">Le texte est vide — la chaîne vide lue est <see cref="EmptyText"/>, qui porte le sien — ou il dépasse la borne de coupure.</exception>
  /// <exception cref="ArgumentOutOfRangeException">La longueur réelle est inférieure à celle du texte : une coupure ne rallonge pas.</exception>
  public static PreviewedValue Of(string text, int actualLength)
  {
    ArgumentNullException.ThrowIfNull(text);

    if (text.Length == 0)
    {
      throw new ArgumentException(
        "La chaîne vide lue est PreviewedValue.EmptyText, qui porte son propre marqueur : elle n'est "
        + "ni un NULL ni une absence, et deux marqueurs plutôt qu'un parce que ce sont deux choses "
        + "différentes en base.",
        nameof(text));
    }

    if (text.Length > ColumnPreview.MaxValueLength)
    {
      throw new ArgumentException(
        $"La valeur porte {text.Length} caractères et la coupure est à {ColumnPreview.MaxValueLength}. "
        + "Elle est faite par le SGBD, avant la traversée du réseau : une valeur plus longue arrivée "
        + "jusqu'ici est une requête de prélèvement qui a cessé de couper.",
        nameof(text));
    }

    if (actualLength < text.Length)
    {
      throw new ArgumentOutOfRangeException(
        nameof(actualLength),
        actualLength,
        $"La longueur réelle est plus courte que le texte lu ({text.Length}) : une coupure ne "
        + "rallonge pas une valeur, et ce couple-là rendrait « tronquée » faux dans les deux sens.");
    }

    return new PreviewedValue(text, actualLength);
  }
}
