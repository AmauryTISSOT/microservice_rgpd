using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.UseCases.Casework.DeclareStep;

/// <summary>
/// Déclarer où en est le travail dû sur un système, sous un nom saisi et un constat écrit.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ce geste n'existe que dans la surface de l'<c>Operator</c>.</b> Aucune route publique ne le
/// porte : une API qui instruirait un dossier permettrait à quelqu'un de refaire chez lui l'écran
/// « ✅ demande traitée », et l'incomplétude cesserait d'être visible par construction.
/// </para>
/// <para>
/// <b>Le nom et le régime voyagent ensemble.</b> La surface n'authentifie personne — choix de PoC
/// assumé —, et le régime dit ce que valait ce nom : sans lui, le <c>EvidenceLog</c> d'aujourd'hui serait
/// indiscernable de celui de demain.
/// </para>
/// <para>
/// <b>Le constat est du texte qui reste, et il est la seule prose que cette commande porte.</b> Le
/// texte qui meurt — celui qui nomme des tiers — n'a aucun champ ici : il vit sur le <c>Case</c> et
/// meurt à la clôture. La règle tient par le placement.
/// </para>
/// </remarks>
/// <param name="Case">Le dossier où le travail était dû.</param>
/// <param name="Right">Le droit au titre duquel il l'était.</param>
/// <param name="DeclaredSystem">Le système sur lequel il l'était.</param>
/// <param name="State">
/// L'état déclaré. <c>Untreated</c> — l'aveu que personne ne l'a fait — s'enregistre aussi volontiers
/// que <c>Done</c> : le service n'a jamais le droit de bloquer la trace la plus précieuse du
/// dispositif.
/// </param>
/// <param name="Finding">Le constat de l'<c>Operator</c>, en prose libre. Exigé.</param>
/// <param name="SignedBy">Le nom que l'<c>Operator</c> a saisi. Exigé.</param>
public sealed record DeclareStepCommand(
  CaseId Case,
  DataSubjectRight Right,
  DeclaredSystemId DeclaredSystem,
  StepState State,
  string? Finding,
  string? SignedBy)
  : ICommand<Result>;
