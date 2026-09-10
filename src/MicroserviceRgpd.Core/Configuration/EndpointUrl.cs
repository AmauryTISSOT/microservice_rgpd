using Vogen;

namespace MicroserviceRgpd.Core.Configuration;

/// <summary>
/// L'adresse à laquelle le service joint le système d'un intégrateur, dans le contexte
/// <c>Configuration</c>. <b>Impossible à construire dans un état invalide</b> : toute la validation
/// d'adresse est enfermée ici, et l'intégrateur ne pourra jamais enregistrer une adresse que le
/// service ne saurait pas appeler.
/// </summary>
/// <remarks>
/// <para>
/// <b>Aucun effet de bord réseau à la construction.</b> Valider une adresse, ce n'est pas la
/// joindre : le type se contente de la <i>lire</i>. Rien ici n'ouvre de connexion, ne résout de nom
/// ni n'émet de requête — une adresse peut être parfaitement formée et pointer vers un hôte
/// injoignable, et ce n'est pas à la construction que cela se découvre.
/// </para>
/// <para>
/// <b>Une adresse absolue est exigée.</b> Une adresse relative — <c>/callback</c> — n'a pas d'hôte,
/// et le service ne saurait pas où l'appeler. Seuls <c>http</c> et <c>https</c> sont admis : le HTTP
/// est accepté au même titre que le HTTPS, car l'exigence de chiffrement du transport relève du
/// déploiement, non de ce value object.
/// </para>
/// <para>
/// ⚠️ <b>Un userinfo est refusé.</b> Les identifiants portés par une URL — <c>https://jean:mot@…</c>
/// — sont exactement le secret d'appel que la <c>Configuration</c> n'a pas à détenir : une adresse
/// est ce que l'écran a le droit de connaître, et un secret y serait un secret affiché.
/// </para>
/// </remarks>
[ValueObject<string>]
public readonly partial struct EndpointUrl
{
  /// <summary>Le plafond, en unités UTF-16. Chiffre arbitraire et assumé.</summary>
  public const int MaxLength = 2_048;

  /// <summary>Les bordures sont retirées avant validation <b>et</b> avant stockage.</summary>
  private static string NormalizeInput(string? input)
  {
    return input?.Trim() ?? string.Empty;
  }

  private static Validation Validate(string value)
  {
    if (value.Length == 0)
    {
      return Validation.Invalid("L'adresse du endpoint est absente ou vide.");
    }

    if (value.Length > MaxLength)
    {
      return Validation.Invalid($"L'adresse du endpoint dépasse {MaxLength} caractères.");
    }

    if (!Uri.TryCreate(value, UriKind.Absolute, out var address)
        || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
    {
      return Validation.Invalid("L'adresse du endpoint n'est pas une URL http ou https absolue.");
    }

    if (address.UserInfo.Length > 0)
    {
      return Validation.Invalid("L'adresse du endpoint ne porte pas d'identifiants : la Configuration ne détient aucun secret.");
    }

    return Validation.Ok;
  }
}
