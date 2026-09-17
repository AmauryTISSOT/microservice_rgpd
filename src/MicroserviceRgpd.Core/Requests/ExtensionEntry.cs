namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Les valeurs <b>brutes</b> d'une prolongation, telles que l'<c>Operator</c> les a saisies : rien
/// n'y est encore trimé, validé ni interprété. C'est ce que <see cref="DataSubjectRequest.Extend"/>
/// transforme en prolongation, ou en la liste de ce qui l'en empêche.
/// </summary>
/// <remarks>
/// ⚠️ <b>La durée n'y est pas</b> : deux mois est une constante du domaine, pas un champ. Le
/// règlement dit « deux mois », et non « jusqu'à deux mois » (ADR-0029).
/// </remarks>
/// <param name="Ground">Le nom canonique du motif de prolongation — <c>Complexity</c>, <c>NumberOfRequests</c>.</param>
/// <param name="Justification">Le texte qui dit le fait concret, obligatoire.</param>
public sealed record ExtensionEntry(string? Ground, string? Justification);
