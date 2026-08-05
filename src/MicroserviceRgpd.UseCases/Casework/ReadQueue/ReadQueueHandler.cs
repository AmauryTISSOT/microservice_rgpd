using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.ReadQueue;

/// <summary>
/// Évalue la file. <b>À chaque affichage, entièrement</b> : rien n'est mémorisé d'un affichage à
/// l'autre, et aucun drapeau d'échéance n'est écrit nulle part.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le tri se fait ici, en mémoire, et non dans la base.</b> L'échéance est un calcul sur l'instant
/// où l'<c>Operator</c> regarde ; demander à la base de trier aurait exigé d'y persister l'échéance,
/// c'est-à-dire le drapeau même que ce contexte refuse. Le troc est assumé : quelques demandes par an.
/// </para>
/// <para>
/// <b>Une file vide est un résultat, jamais une absence.</b> C'est l'état d'un service qui n'a rien à
/// instruire, et l'écran doit pouvoir le dire — mais il ne le dira <b>jamais par un zéro</b>.
/// </para>
/// </remarks>
/// <param name="cases">Les dossiers, en lecture seule.</param>
/// <param name="clock">
/// L'horloge, injectée pour que l'instant du regard se dicte en test plutôt que d'être lu sur la
/// machine qui sert la page.
/// </param>
public sealed class ReadQueueHandler(IReadRepository<Case> cases, TimeProvider clock)
  : IQueryHandler<ReadQueueQuery, OperatorQueue>
{
  /// <inheritdoc />
  public async ValueTask<OperatorQueue> Handle(ReadQueueQuery query, CancellationToken cancellationToken)
  {
    var observedAt = clock.GetUtcNow();

    var open = await cases.ListAsync(new OpenCasesSpec(), cancellationToken);

    var queued = open
      .Select(opened => Line(opened, observedAt))
      // L'échéance trie. À échéance égale, l'identité du dossier : deux affichages de suite doivent
      // ranger les mêmes lignes dans le même ordre, sans quoi l'écran bougerait sous les yeux.
      .OrderBy(line => line.Deadline.On)
      .ThenBy(line => line.Case.Value)
      .ToArray();

    return new OperatorQueue(queued, observedAt);
  }

  private static QueuedCase Line(Case opened, DateTimeOffset observedAt)
  {
    var deadline = StatutoryDeadline.Of(opened.Reception);

    return new QueuedCase(
      opened.Id,
      opened.IdentityDeclaration,
      opened.Reception,
      deadline,
      deadline.IsOverrunAt(observedAt),
      [.. opened.Claims.Select(claim => claim.Right)]);
  }
}
