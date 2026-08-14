using MicroserviceRgpd.Core.Casework.EvidenceLog;

namespace MicroserviceRgpd.UseCases.Casework.DestroyEvidenceLog;

/// <summary>
/// Détruit un <c>EvidenceLog</c> échu, en entier — et n'écrit rien à la place.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aucune ligne de preuve n'est ajoutée, ici ou ailleurs.</b> C'est le seul gestionnaire du
/// dispositif qui ne consigne rien, et c'est assumé : la seule ligne possible serait dans le
/// <c>EvidenceLog</c> qu'on détruit. On ne prouvera donc jamais avoir purgé — l'alternative aurait été un
/// second étage d'anonymisation, qui rouvrirait l'expurgation que la définition du <c>EvidenceLog</c>
/// ferme.
/// </para>
/// <para>
/// <b>Le <c>Case</c> clos, lui, n'est pas touché.</b> Il n'a plus une désignation depuis cinq ans et
/// ne nomme personne ; ce qui disparaît est la preuve, et elle disparaît seule. La ligne de l'écran
/// s'en va avec elle, faute d'une preuve à détruire.
/// </para>
/// <para>
/// ⚠️ <b>Un <c>EvidenceLog</c> non échu n'est pas détruit</b>, quoi que demande l'appel : l'échéance est
/// éprouvée sur l'horloge du service, et le refus se lit comme un dossier qui n'était pas là.
/// </para>
/// </remarks>
/// <param name="expired">Les <c>EvidenceLog</c> échus, et la seule suppression du dispositif.</param>
/// <param name="clock">L'horloge, injectée pour que l'instant du geste se dicte en test.</param>
public sealed class DestroyEvidenceLogHandler(IExpiredEvidenceLogs expired, TimeProvider clock)
  : ICommandHandler<DestroyEvidenceLogCommand, Result>
{
  /// <inheritdoc />
  public async ValueTask<Result> Handle(DestroyEvidenceLogCommand command, CancellationToken cancellationToken)
  {
    ArgumentNullException.ThrowIfNull(command);

    var destroyed = await expired.DestroyAsync(command.Case, clock.GetUtcNow(), cancellationToken);

    // Rien à détruire : une preuve encore due, un dossier qui n'a jamais existé, ou un EvidenceLog qu'un
    // autre écran vient d'emporter. Aucun de ces cas n'est une panne, et aucun ne se distingue pour
    // l'humain qui regarde — sa file, à l'affichage suivant, dit ce qui reste.
    return destroyed ? Result.Success() : Result.NotFound();
  }
}
