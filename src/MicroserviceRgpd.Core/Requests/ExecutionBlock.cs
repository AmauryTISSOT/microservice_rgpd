using System.Globalization;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// Le <b>motif de blocage</b> : la raison pour laquelle une <see cref="DataSubjectRequest"/> ne peut
/// pas être exécutée — <see cref="Closed"/>, <see cref="IdentityNotVerified"/>,
/// <see cref="EmailMissing"/>, <see cref="RightNotConfigured"/> ou
/// <see cref="RabbitMqNotYetSupported"/> (ADR-0026).
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

  /// <summary>Le Paramétrage n'associe aucun canal d'exercice au droit invoqué (ADR-0027).</summary>
  public static readonly ExecutionBlock RightNotConfigured = new(nameof(RightNotConfigured), 3, "Le {0} n'est pas configuré");

  /// <summary>
  /// Le droit invoqué s'exerce par une publication RabbitMQ, <b>que le service ne sait pas encore
  /// faire</b> : le Paramétrage est bon, c'est l'exercice qui manque.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Motif provisoire, à supprimer avec l'US qui publiera sur RabbitMQ</b> : le jour où le
  /// service publie, ce motif n'a plus rien à dire, et un droit routé s'exécute comme un droit adressé.
  /// </remarks>
  public static readonly ExecutionBlock RabbitMqNotYetSupported = new(
    nameof(RabbitMqNotYetSupported),
    4,
    "Le {0} s'exerce par RabbitMQ, que le service ne sait pas encore publier");

  private readonly string _frenchLabel;

  private ExecutionBlock(string name, int value, string frenchLabel)
    : base(name, value)
  {
    _frenchLabel = frenchLabel;
  }

  /// <summary>
  /// Le libellé destiné à l'<c>Operator</c>, pour une demande qui invoque <paramref name="right"/>.
  /// Seuls <see cref="RightNotConfigured"/> et <see cref="RabbitMqNotYetSupported"/> — les motifs qui
  /// regardent le canal — nomment le droit, sous son libellé du noyau partagé.
  /// </summary>
  /// <exception cref="ArgumentNullException"><paramref name="right"/> est absent.</exception>
  public string FrenchLabelFor(DataSubjectRight right)
  {
    ArgumentNullException.ThrowIfNull(right);

    return string.Format(CultureInfo.InvariantCulture, _frenchLabel, right.FrenchLabel);
  }
}
