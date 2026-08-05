namespace MicroserviceRgpd.Core.Casework.Adapters;

/// <summary>
/// Ce qu'un appel d'<c>Adapter</c> rapporte : un <see cref="AdapterOutcome"/>, et ce que cette
/// réponse porte avec lui — le corps servi, ou l'<b>échéance déclarée</b> d'un différé.
/// </summary>
/// <typeparam name="TServed">
/// Ce que la <see cref="Capability"/> appelée rend quand elle sert. Le contrat de transport est le
/// même pour les quatre ; ce qu'elles rendent ne l'est pas, et ce paramètre est exactement cette
/// différence — laissée à l'appelant plutôt que devinée ici.
/// </typeparam>
/// <remarks>
/// <para>
/// <b>Les deux refus sont des réponses, pas des exceptions.</b> Un <c>Adapter</c> qui refuse a
/// répondu : le service consigne la tentative et signale le désaccord, ce qu'il ne saurait pas
/// faire d'une pile d'appels dépliée par une exception. Ce qui n'est <b>ni</b> réponse ni refus
/// reste une <see cref="AdapterFailure"/>.
/// </para>
/// <para>
/// <b>Rien n'est complété.</b> Un différé sans échéance lisible n'est pas un différé par défaut :
/// l'<c>Adapter</c> a rompu le contrat, et cela remonte en panne plutôt qu'en une date inventée par
/// le service — laquelle deviendrait, une heure plus tard, une preuve que personne n'a déclarée.
/// </para>
/// </remarks>
public sealed record AdapterAnswer<TServed>
  where TServed : class
{
  private AdapterAnswer(AdapterOutcome outcome, TServed? served, DateTimeOffset? declaredDeadline)
  {
    Outcome = outcome;
    Served = served;
    DeclaredDeadline = declaredDeadline;
  }

  /// <summary>Ce que l'<c>Adapter</c> a répondu.</summary>
  public AdapterOutcome Outcome { get; }

  /// <summary>
  /// Ce qui a été servi, ou <c>null</c> dès que la réponse n'est pas
  /// <see cref="AdapterOutcome.Served"/>. Le <c>null</c> ne remplace ici aucun corps : il accompagne
  /// une réponse qui dit déjà qu'aucun n'a été rendu.
  /// </summary>
  public TServed? Served { get; }

  /// <summary>
  /// L'échéance que l'<c>Adapter</c> a <b>déclarée</b> en différant, ou <c>null</c> pour toute autre
  /// réponse. Elle est déclarée, jamais négociée : le service la relit et repassera après elle,
  /// sans tenir aucune connexion d'ici là.
  /// </summary>
  public DateTimeOffset? DeclaredDeadline { get; }

  /// <summary>L'<c>Adapter</c> a servi, et voici ce qu'il rend.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="served"/> est absent.</exception>
  public static AdapterAnswer<TServed> Serving(TServed served)
  {
    ArgumentNullException.ThrowIfNull(served);

    return new AdapterAnswer<TServed>(AdapterOutcome.Served, served, declaredDeadline: null);
  }

  /// <summary>
  /// L'<c>Adapter</c> a différé, et déclare quand il aura fini.
  /// </summary>
  /// <remarks>
  /// L'instant est ramené en UTC, comme celui du <c>Ledger</c> : une échéance relue dans le fuseau
  /// de la machine qui l'a reçue serait une échéance différente d'un serveur à l'autre.
  /// </remarks>
  public static AdapterAnswer<TServed> Deferring(DateTimeOffset declaredDeadline)
  {
    return new AdapterAnswer<TServed>(
      AdapterOutcome.Deferred,
      served: null,
      declaredDeadline.ToUniversalTime());
  }

  /// <summary>
  /// L'<c>Adapter</c> a refusé, et l'on sait lequel des deux refus c'est.
  /// </summary>
  /// <remarks>
  /// Servir et différer ont chacun leur fabrique, parce que chacun porte quelque chose que
  /// celle-ci n'a pas : le garde est donc le même que partout ailleurs, et il vit une seule fois.
  /// </remarks>
  /// <exception cref="ArgumentNullException"><paramref name="refusal"/> est absent.</exception>
  /// <exception cref="ArgumentException">La réponse donnée n'est pas un refus.</exception>
  public static AdapterAnswer<TServed> Refusing(AdapterOutcome refusal)
  {
    return new AdapterAnswer<TServed>(
      AdapterOutcome.RefusalOrThrow(refusal, nameof(refusal)),
      served: null,
      declaredDeadline: null);
  }
}
