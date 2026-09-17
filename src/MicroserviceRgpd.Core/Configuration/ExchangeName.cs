using System.Text;
using Vogen;

namespace MicroserviceRgpd.Core.Configuration;

/// <summary>
/// Le nom de l'<b>exchange</b> RabbitMQ sur lequel le service publiera pour faire exercer un droit,
/// dans le contexte <c>Configuration</c>. <b>Impossible à construire dans un état invalide</b> :
/// rogné, non vide, et plafonné à <see cref="MaxLengthInBytes"/> octets UTF-8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aucun effet de bord réseau à la construction.</b> Nommer un exchange, ce n'est ni s'y
/// connecter, ni le déclarer, ni vérifier qu'il existe : la topologie du bus reste la propriété de
/// l'exploitant (ADR-0027). Rien ici n'ouvre de connexion ni ne résout de nom.
/// </para>
/// <para>
/// ⚠️ <b>Le plafond compte des octets, pas des caractères.</b> C'est la limite qu'AMQP pose sur un
/// <i>shortstr</i>, et un nom d'exchange accentué ou non latin y consomme plus d'un octet par
/// caractère. Compter des caractères laisserait passer un nom que le broker refuserait.
/// </para>
/// <para>
/// <b>Aucun autre contrôle de forme.</b> Le jeu de caractères qu'un broker accepte, et ce qu'il
/// fait d'un nom réservé, lui appartiennent : ce value object refuse ce qui est certainement faux,
/// et n'invente pas une grammaire que RabbitMQ n'impose pas.
/// </para>
/// </remarks>
[ValueObject<string>]
public readonly partial struct ExchangeName
{
  /// <summary>Le plafond, en <b>octets UTF-8</b> — la limite d'un <i>shortstr</i> AMQP.</summary>
  public const int MaxLengthInBytes = 255;

  /// <summary>Les bordures sont retirées avant validation <b>et</b> avant stockage.</summary>
  private static string NormalizeInput(string? input)
  {
    return input?.Trim() ?? string.Empty;
  }

  private static Validation Validate(string value)
  {
    if (value.Length == 0)
    {
      return Validation.Invalid("Le nom de l'exchange est absent ou vide.");
    }

    if (Encoding.UTF8.GetByteCount(value) > MaxLengthInBytes)
    {
      return Validation.Invalid($"Le nom de l'exchange dépasse {MaxLengthInBytes} octets UTF-8.");
    }

    return Validation.Ok;
  }
}
