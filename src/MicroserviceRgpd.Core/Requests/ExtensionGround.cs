namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Le <b>motif de prolongation</b> : la raison pour laquelle la date limite de réponse d'une
/// <see cref="DataSubjectRequest"/> est reportée de deux mois — <see cref="Complexity"/> ou
/// <see cref="NumberOfRequests"/>, et rien d'autre (ADR-0029).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Deux valeurs, jamais une troisième</b> : l'article 12 §3 n'en ouvre pas d'autre, et il n'y a
/// pas de motif « Autre ». Une justification en toutes lettres accompagne le motif — c'est elle qui
/// dit le fait concret.
/// </para>
/// <para>
/// ⚠️ <b>Ce n'est pas un <see cref="ExecutionBlock"/></b> : celui-là dit pourquoi une demande ne
/// s'exécute pas, et c'est le serveur qui le calcule. Le motif de prolongation, l'<c>Operator</c> le
/// choisit.
/// </para>
/// </remarks>
public sealed class ExtensionGround : SmartEnum<ExtensionGround>
{
  /// <summary>La demande est complexe à traiter.</summary>
  public static readonly ExtensionGround Complexity = new(nameof(Complexity), 0, "Complexité de la demande");

  /// <summary>Le responsable fait face à un nombre de demandes qui l'empêche de répondre à temps.</summary>
  public static readonly ExtensionGround NumberOfRequests = new(nameof(NumberOfRequests), 1, "Nombre de demandes");

  private ExtensionGround(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
