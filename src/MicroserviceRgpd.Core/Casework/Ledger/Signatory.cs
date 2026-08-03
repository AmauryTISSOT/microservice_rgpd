namespace MicroserviceRgpd.Core.Casework.Ledger;

/// <summary>
/// Qui a déclaré ce qu'une ligne du <c>Ledger</c> consigne. Le <c>Ledger</c> est <b>daté et
/// signé</b> — « par qui » est un tiers de ce que le service prouve.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est le seul endroit du <c>Ledger</c> où un nom de personne est permis, et ce n'est
/// jamais celui de la personne concernée.</b> Le <c>Ledger</c> nomme l'<c>Operator</c>,
/// définitivement : c'est un fichier de données personnelles sur les salariés du client, et son
/// effacement leur est légitimement refusé.
/// </para>
/// <para>
/// <b>Un nom n'est pas une authentification</b>, et le service ne prétend pas le contraire. Il
/// enregistre ce qu'un humain a saisi ; le régime sous lequel il l'a saisi s'écrit ailleurs, pour
/// que le <c>Ledger</c> d'aujourd'hui ne soit pas indiscernable de celui de demain.
/// </para>
/// </remarks>
public sealed record Signatory
{
  /// <summary>Le plafond, en unités UTF-16. Un nom saisi à la main, pas un paragraphe.</summary>
  public const int MaxNameLength = 200;

  private Signatory(SignatoryKind kind, string? name)
  {
    Kind = kind;
    Name = name;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private Signatory()
  {
    Kind = SignatoryKind.Application;
  }

  /// <summary>Un humain nommé, ou le canal applicatif — c'est-à-dire personne.</summary>
  public SignatoryKind Kind { get; private set; }

  /// <summary>
  /// Le nom de l'<c>Operator</c>, ou <c>null</c> quand c'est l'application qui a appelé. Le
  /// <c>null</c> ne remplace ici aucun nom : il accompagne un <see cref="Kind"/> qui dit déjà
  /// qu'aucun humain n'a signé.
  /// </summary>
  public string? Name { get; private set; }

  /// <summary>
  /// L'application du client, appelant depuis une session qu'elle a elle-même authentifiée.
  /// <b>Aucun humain n'a signé</b>, et la ligne le dit plutôt que de le taire.
  /// </summary>
  public static Signatory Application { get; } = new(SignatoryKind.Application, name: null);

  /// <summary>
  /// L'<c>Operator</c> qui signe, sous le nom qu'il a saisi.
  /// </summary>
  /// <exception cref="ArgumentException">Le nom est absent, vide, démesuré, ou porte un caractère de contrôle.</exception>
  public static Signatory Operator(string? name)
  {
    var trimmed = name?.Trim() ?? string.Empty;

    if (trimmed.Length == 0)
    {
      // Un opérateur anonyme n'existe pas : le geste qui produit une issue est toujours celui d'un
      // humain nommé, et une signature vide serait une preuve qui ne prouve rien.
      throw new ArgumentException("Le nom du signataire est absent ou vide.", nameof(name));
    }

    if (trimmed.Length > MaxNameLength)
    {
      throw new ArgumentException($"Le nom du signataire dépasse {MaxNameLength} caractères.", nameof(name));
    }

    if (trimmed.Any(char.IsControl))
    {
      throw new ArgumentException("Le nom du signataire porte un caractère de contrôle.", nameof(name));
    }

    return new Signatory(SignatoryKind.Operator, trimmed);
  }
}
