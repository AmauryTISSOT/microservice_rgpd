using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.ReadCase;

/// <summary>
/// Compose l'écran du dossier : le dossier lui-même, et ce que le catalogue dit <b>aujourd'hui</b> des
/// systèmes dont il porte le travail dû.
/// </summary>
/// <remarks>
/// <para>
/// <b>Le catalogue est relu à chaque affichage, et le <c>Step</c> ne le recopie jamais.</b> Le
/// <c>Manifest</c> vieillit exprès : un système révisé hier montre sa date d'hier, et c'est la
/// fraîcheur de l'affirmation qu'on veut rapporter à qui va signer, non l'ancienneté d'une ligne.
/// </para>
/// <para>
/// <b>Un système que le catalogue ne porte plus ne fait pas disparaître son travail dû.</b> La ligne
/// reste, sans nom et sans date, et l'écran le dit : une ligne qui s'évaporerait serait exactement
/// l'<c>Omission silencieuse</c> fabriquée par l'écran.
/// </para>
/// </remarks>
/// <param name="cases">Les dossiers, en lecture seule.</param>
/// <param name="manifest">Le catalogue, en lecture seule.</param>
/// <param name="clock">L'horloge, injectée pour que l'instant du regard se dicte en test.</param>
public sealed class ReadCaseHandler(
  IReadRepository<Case> cases,
  IReadRepository<DeclaredSystem> manifest,
  TimeProvider clock)
  : IQueryHandler<ReadCaseQuery, CaseOnScreen?>
{
  /// <inheritdoc />
  public async ValueTask<CaseOnScreen?> Handle(ReadCaseQuery query, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(query);

    var opened = await cases.FirstOrDefaultAsync(new CaseByIdSpec(query.Case), cancellationToken);

    if (opened is null)
    {
      return null;
    }

    var observedAt = clock.GetUtcNow();
    var catalogue = (await manifest.ListAsync(cancellationToken)).ToDictionary(system => system.Id);
    var deadline = StatutoryDeadline.Of(opened.Reception);

    var claims = opened.Claims
      .Select(claim => new ClaimedRight(
        claim.Right,
        claim.State,
        // L'origine et l'identité d'origine descendent telles quelles : ce sont des copies figées à
        // la naissance du droit, et les recalculer sur le dossier d'aujourd'hui serait exactement la
        // réécriture qu'elles existent pour empêcher.
        claim.Origin,
        claim.IdentityAtOrigin,
        claim.AwaitsConfirmation,
        claim.MotivationIsDemanded,
        [.. claim.Steps.Select(step => Projected(step, catalogue))]))
      .ToArray();

    return new CaseOnScreen(
      opened.Id,
      opened.IdentityDeclaration,
      opened.Motivation,
      // La règle vit sur la racine, qui la calcule sur ses Claim : une seconde rédaction ici
      // finirait par ne plus dire la même chose que le dossier.
      opened.AwaitsAMotivation,
      opened.Reception,
      deadline,
      deadline.IsOverrunAt(observedAt),
      OldestDeclarationAmong(claims),
      claims,
      observedAt);
  }

  private static StepOnScreen Projected(Step step, IReadOnlyDictionary<DeclaredSystemId, DeclaredSystem> catalogue)
  {
    var declared = catalogue.GetValueOrDefault(step.DeclaredSystem);

    return new StepOnScreen(
      step.DeclaredSystem,
      declared?.Label,
      declared?.DeclaredOn,
      step.State,
      // La règle vit sur l'état, et la ligne de preuve s'en sert pour refuser ce que l'écran réclame
      // ici : une seule règle, aux deux endroits.
      step.State.RequiresAFinding);
  }

  private static DateTimeOffset? OldestDeclarationAmong(IEnumerable<ClaimedRight> claims)
  {
    // La plus ancienne parmi les systèmes de ce dossier, et non celle du catalogue entier : le
    // bandeau parle du travail que l'Operator a sous les yeux, et un système déclaré après
    // l'ouverture n'a jamais eu de Step ici.
    var declarations = claims
      .SelectMany(claim => claim.Steps)
      .Select(step => step.DeclaredOn)
      .OfType<DateTimeOffset>()
      .ToArray();

    return declarations.Length == 0 ? null : declarations.Min();
  }
}
