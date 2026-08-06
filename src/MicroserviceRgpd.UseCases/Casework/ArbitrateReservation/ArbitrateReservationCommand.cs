using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.UseCases.Casework.ArbitrateReservation;

/// <summary>
/// Un <c>Operator</c> <b>tranche une réserve</b> de <c>Locate</c> : il dit que cette ligne est celle
/// de la personne, ou qu'elle ne l'est pas.
/// </summary>
/// <remarks>
/// <b>C'est le seul chemin par lequel un rattachement se décide</b>, et il passe par un humain nommé.
/// Ni la fusion à tort — irréversible, et portant sur la donnée d'un tiers — ni l'exclusion par
/// prudence, qui est l'<c>Omission silencieuse</c>, n'appartiennent au service.
/// </remarks>
/// <param name="Case">Le dossier dans lequel la réserve a été levée.</param>
/// <param name="DeclaredSystem">Le système qui l'a rendue.</param>
/// <param name="Reference">La référence opaque qui l'identifie dans ce système.</param>
/// <param name="Ruling">L'issue : rattachée, ou écartée. Jamais <c>Awaiting</c>.</param>
/// <param name="SignedBy">Le nom de l'humain qui tranche, saisi et non authentifié.</param>
public sealed record ArbitrateReservationCommand(
  CaseId Case,
  DeclaredSystemId DeclaredSystem,
  OpaqueReference Reference,
  ReservationState Ruling,
  string? SignedBy) : ICommand<Result>;
