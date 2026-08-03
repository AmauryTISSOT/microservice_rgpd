namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Un attribut déclaré qui <b>pointe vers</b> la personne — une adresse électronique, un nom, un
/// téléphone, une référence interne.
/// <para>
/// <b>Elle ne prétend ni à l'unicité ni à l'exactitude.</b> La personne n'a pas d'identifiant, et
/// l'identifiant natif d'une application est souvent la clé qui ouvre le moins de portes. Deux
/// désignations peuvent viser la même personne, une seule peut en viser deux : c'est ce doute que
/// l'<c>Operator</c> arbitre, et que ce type n'a jamais prétendu lever.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>La forme est unique, et indépendante du canal.</b> La même paire arrive de l'API de
/// l'application et du dépôt manuel : deux formes voudraient dire deux chemins de recherche à
/// maintenir, et l'un des deux finirait par ne plus être celui qu'on croit.
/// </para>
/// <para>
/// <b>C'est du nominatif, et il meurt à la clôture.</b> Aucune valeur de ce type n'entre au
/// <c>Ledger</c> — qui n'en garde jamais que le <b>compte</b> et la provenance.
/// </para>
/// </remarks>
public sealed record Designation
{
  /// <summary>Le plafond, en unités UTF-16. Large : une adresse électronique complète y tient.</summary>
  public const int MaxValueLength = 400;

  private Designation(DesignationKind kind, string value)
  {
    Kind = kind;
    Value = value;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private Designation()
  {
    Kind = DesignationKind.Reference;
    Value = string.Empty;
  }

  /// <summary>Ce qu'on tient — jamais ce que ça vaut.</summary>
  public DesignationKind Kind { get; private set; }

  /// <summary>La valeur déclarée, relue telle quelle, bordures nettoyées et rien d'autre.</summary>
  public string Value { get; private set; }

  /// <summary>
  /// Prend une désignation, ou refuse. Le refus est une <b>programmation fautive</b> : la frontière
  /// d'entrée a déjà nommé à l'appelant ce qu'il avait mal rempli.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="kind"/> est absent.</exception>
  /// <exception cref="ArgumentException">La valeur est vide, démesurée, ou porte un caractère de contrôle.</exception>
  public static Designation Of(DesignationKind kind, string? value)
  {
    ArgumentNullException.ThrowIfNull(kind);

    var trimmed = value?.Trim() ?? string.Empty;

    if (trimmed.Length == 0)
    {
      // Une désignation vide ne pointe vers personne, et une recherche lancée sous elle rendrait
      // un zéro qu'on lirait « cette personne n'est pas chez nous ».
      throw new ArgumentException("La valeur de la désignation est absente ou vide.", nameof(value));
    }

    if (trimmed.Length > MaxValueLength)
    {
      throw new ArgumentException(
        $"La valeur de la désignation dépasse {MaxValueLength} caractères.", nameof(value));
    }

    // Une désignation part sur le fil vers un Adapter : un caractère de contrôle n'y a rien à
    // faire, et le service ne l'échappera pas pour lui.
    if (trimmed.Any(char.IsControl))
    {
      throw new ArgumentException("La valeur de la désignation porte un caractère de contrôle.", nameof(value));
    }

    return new Designation(kind, trimmed);
  }
}
