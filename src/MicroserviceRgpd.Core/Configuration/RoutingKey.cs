using System.Text;
using Vogen;

namespace MicroserviceRgpd.Core.Configuration;

/// <summary>
/// La <b>routing key</b> avec laquelle le service publiera sur l'exchange pour faire exercer un
/// droit, dans le contexte <c>Configuration</c>. <b>Impossible à construire dans un état
/// invalide</b> : rognée, non vide, et plafonnée à <see cref="MaxLengthInBytes"/> octets UTF-8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aucun effet de bord réseau à la construction</b>, pour le motif de l'<see cref="ExchangeName"/> :
/// déclarer un routage ne touche pas le broker (ADR-0027).
/// </para>
/// <para>
/// ⚠️ <b>Le plafond compte des octets, pas des caractères</b> — la limite d'un <i>shortstr</i> AMQP.
/// </para>
/// <para>
/// <b>Une routing key vide est refusée</b> ici, alors qu'un broker l'accepte sur un exchange
/// <c>fanout</c> : ce que l'intégrateur déclare est ce que le service publiera, et une clé laissée
/// vide se lit comme une saisie inachevée, non comme un choix.
/// </para>
/// </remarks>
[ValueObject<string>]
public readonly partial struct RoutingKey
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
      return Validation.Invalid("La routing key est absente ou vide.");
    }

    if (Encoding.UTF8.GetByteCount(value) > MaxLengthInBytes)
    {
      return Validation.Invalid($"La routing key dépasse {MaxLengthInBytes} octets UTF-8.");
    }

    return Validation.Ok;
  }
}
