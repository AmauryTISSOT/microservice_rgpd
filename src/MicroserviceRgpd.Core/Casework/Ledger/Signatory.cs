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
/// enregistre ce qu'un humain a saisi <b>et</b> le <see cref="SignatureRegime"/> sous lequel il l'a
/// saisi — dans le même objet, par le même geste, pour que le <c>Ledger</c> d'aujourd'hui ne soit pas
/// indiscernable de celui de demain.
/// </para>
/// </remarks>
public sealed record Signatory
{
  /// <summary>Le plafond, en unités UTF-16. Un nom saisi à la main, pas un paragraphe.</summary>
  public const int MaxNameLength = 200;

  private Signatory(SignatoryKind kind, string? name, SignatureRegime? regime)
  {
    Kind = kind;
    Name = name;
    Regime = regime;
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
  /// Ce que valait le nom au moment où il a été saisi, ou <c>null</c> quand c'est l'application qui a
  /// appelé — elle n'a saisi aucun nom, et un régime de signature ne dirait rien d'elle.
  /// <para>
  /// <b>Il s'écrit en même temps que le nom, et par le même geste.</b> Un nom enregistré sans son
  /// régime serait relu dans dix ans comme si quelqu'un s'était identifié.
  /// </para>
  /// </summary>
  public SignatureRegime? Regime { get; private set; }

  /// <summary>
  /// L'application du client, appelant depuis une session qu'elle a elle-même authentifiée.
  /// <b>Aucun humain n'a signé</b>, et la ligne le dit plutôt que de le taire.
  /// </summary>
  public static Signatory Application { get; } =
    new(SignatoryKind.Application, name: null, regime: null);

  /// <summary>
  /// L'<c>Operator</c> qui signe, sous le nom qu'il a saisi et sous le régime qui dit ce que ce nom
  /// valait.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Un opérateur anonyme n'existe pas : le geste qui produit une issue est toujours celui d'un
  /// humain nommé, et une signature vide serait une preuve qui ne prouve rien. « Personne n'a
  /// signé » s'écrit par <see cref="Application"/>, jamais par un nom laissé vide.
  /// </para>
  /// <para>
  /// <b>Le régime est un paramètre, jamais une valeur par défaut posée ici.</b> Un défaut ferait
  /// écrire « non authentifié » par oubli le jour où l'authentification existera, et l'appelant est
  /// le seul à savoir sous quel régime il a recueilli ce nom.
  /// </para>
  /// </remarks>
  /// <param name="name">Le nom saisi par l'humain qui signe.</param>
  /// <param name="regime">Ce que valait ce nom au moment où il a été saisi.</param>
  /// <exception cref="ArgumentNullException"><paramref name="regime"/> est absent.</exception>
  /// <exception cref="ArgumentException">Le nom est absent, vide, démesuré, ou porte un caractère de contrôle.</exception>
  public static Signatory Operator(string? name, SignatureRegime regime)
  {
    ArgumentNullException.ThrowIfNull(regime);

    return new Signatory(
      SignatoryKind.Operator,
      DeclaredText.OrThrow(name, "Le nom du signataire", MaxNameLength, nameof(name)),
      regime);
  }
}
