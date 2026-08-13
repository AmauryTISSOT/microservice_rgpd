using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.DepositListing;

/// <summary>
/// Ingère le collage, le fait dépister, assemble le rapport et l'écrit — <b>dans cet ordre et sans
/// rien différer</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est le geste qui assemble, jamais le moteur.</b> Ce que rend un <c>IScreeningEngine</c>
/// est une <c>ScreenedListing</c> : une ligne par colonne, et l'identité du moteur qui les a
/// produites. Il y manque ce que le moteur n'a pas à décider — l'identité du rapport, l'instant du
/// lancement, le nom de base et le dialecte —, et c'est ici que les quatre se posent.
/// </para>
/// <para>
/// ⚠️ <b>L'identité du moteur est celle qui a répondu</b>, prise sur ce qu'il rend et jamais lue à
/// côté de l'appel : un moteur servi apprend la version qu'on lui sert au moment où il répond, et
/// une propriété posée à côté aurait dit la version configurée.
/// </para>
/// <para>
/// <b>Le nom de base et le dialecte sont recopiés du relevé, jamais vérifiés.</b>
/// <c>Greffier, pas témoin</c> : le service ne sait pas d'où vient ce relevé, et un relevé sincère
/// mais tiré de la base de recette est indiscernable du bon. Aucun mécanisme n'attrape ce cas ici,
/// et aucun ne doit prétendre l'attraper.
/// </para>
/// </remarks>
/// <param name="screenings">Le dépôt des rapports. Un dépôt neuf à chaque collage : rien ne fusionne.</param>
/// <param name="engine">
/// Le port du dépistage. ⚠️ Le geste ignore s'il parle à des règles locales ou à un moteur servi, et
/// c'est la couture de réversibilité d'ADR-0004 — pas une couture de test.
/// </param>
/// <param name="clock">L'horloge. C'est elle, et elle seule, qui décide quel rapport sera le courant.</param>
public sealed class DepositListingHandler(
  IRepository<Screening> screenings,
  IScreeningEngine engine,
  TimeProvider clock)
  : ICommandHandler<DepositListingCommand, Result<ScreeningId>>
{
  /// <inheritdoc />
  public async ValueTask<Result<ScreeningId>> Handle(
    DepositListingCommand command,
    CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var ingested = ColumnListingIngestion.Ingest(command.Paste);

    if (ingested.Refusal is { } refusal)
    {
      // ⚠️ Refusé EN BLOC : aucune colonne n'est rendue, pas même les lignes lisibles. Un rapport
      // bâti sur 99,9 % d'un relevé se lirait comme complet, et les lignes perdues seraient
      // précisément celles que personne ne relirait jamais. Le coût du refus est faible et
      // réversible — l'Operator relance sa requête, il ne perd aucun arbitrage.
      return Result<ScreeningId>.Invalid(RefusalOf(refusal));
    }

    var listing = ingested.Listing!;

    var screened = await engine.ScreenAsync(listing, cancellationToken);

    var screening = Screening.Of(
      ScreeningId.Next(),
      listing.Database,
      listing.Dialect,
      screened.Engine,
      listing.DeclaredColumnCount,
      screened.Columns,
      clock.GetUtcNow());

    await screenings.AddAsync(screening, cancellationToken);

    return screening.Id;
  }

  /// <summary>
  /// Le refus, redit à l'humain qui a collé : le cas, la ligne qu'il peut retrouver dans son propre
  /// collage, ce qui s'y trouvait, et <b>ce que le format attendait</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>L'attente est dite, et ce n'est pas une politesse.</b> Un refus qui nomme le cas sans
  /// dire ce qui était attendu fait recommencer au hasard, sur un collage d'un mégaoctet que
  /// l'<c>Operator</c> ne relira pas à l'œil.
  /// </remarks>
  private static ValidationError RefusalOf(ColumnListingRefusal refusal)
  {
    return new ValidationError
    {
      Identifier = nameof(DepositListingCommand.Paste),
      ErrorCode = refusal.Cause.Name,
      ErrorMessage =
        $"Relevé refusé en bloc — {refusal.Cause.FrenchLabel} (cas {refusal.Cause.CaseNumber}), "
        + $"ligne {refusal.LineNumber} : « {refusal.Observed} ». Attendu : {refusal.Cause.Expectation}. "
        + "Aucune colonne n'a été ingérée : relancez la requête de relevé et recollez-la entière.",
      Severity = ValidationSeverity.Error,
    };
  }
}
