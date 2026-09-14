using System.Globalization;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Le <b>motif de blocage</b> : la raison pour laquelle une <see cref="DataSubjectRequest"/> ne peut
/// pas être exécutée — <see cref="Closed"/>, <see cref="IdentityNotVerified"/>,
/// <see cref="EmailMissing"/> ou <see cref="NoEndpoint"/> (ADR-0026).
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>L'ordre des valeurs est celui des motifs</b> : quand plusieurs conditions manquent, seule la
/// première se dit. Ce qui se corrige sur la demande passe avant ce qui se règle dans le Paramétrage.
/// </para>
/// <para>
/// Il n'y a pas de motif « droit non renseigné » : l'invariant de l'ADR-0019 garantit qu'une demande
/// en porte toujours un.
/// </para>
/// </remarks>
public sealed class ExecutionBlock : SmartEnum<ExecutionBlock>
{
  /// <summary>La demande est Terminée ou Annulée.</summary>
  public static readonly ExecutionBlock Closed = new(nameof(Closed), 0, "Demande close");

  /// <summary>L'<c>Operator</c> n'a pas attesté avoir vérifié l'identité de la personne.</summary>
  public static readonly ExecutionBlock IdentityNotVerified = new(nameof(IdentityNotVerified), 1, "Identité non vérifiée");

  /// <summary>La demande ne porte pas d'email : le système hôte ne saurait pas qui elle concerne.</summary>
  public static readonly ExecutionBlock EmailMissing = new(nameof(EmailMissing), 2, "Email manquant");

  /// <summary>Le Paramétrage n'associe aucune adresse au droit invoqué.</summary>
  public static readonly ExecutionBlock NoEndpoint = new(nameof(NoEndpoint), 3, "Aucune adresse configurée pour le {0}");

  private readonly string _frenchLabel;

  private ExecutionBlock(string name, int value, string frenchLabel)
    : base(name, value)
  {
    _frenchLabel = frenchLabel;
  }

  /// <summary>
  /// Le libellé destiné à l'<c>Operator</c>, pour une demande qui invoque <paramref name="right"/>.
  /// Seul <see cref="NoEndpoint"/> nomme le droit, sous son libellé du noyau partagé.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="right"/> est absent.</exception>
  public string FrenchLabelFor(DataSubjectRight right)
  {
    ArgumentNullException.ThrowIfNull(right);

    return string.Format(CultureInfo.InvariantCulture, _frenchLabel, right.FrenchLabel);
  }
}
