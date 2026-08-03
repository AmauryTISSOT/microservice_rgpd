using Vogen;

namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// L'adresse à laquelle le service joint l'<c>Adapter</c> qui sert un <see cref="DeclaredSystem"/>.
/// <b>Facultative</b> : la plupart des systèmes déclarés n'en ont aucune, et c'est le régime normal
/// — le travail s'y fait à la main.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle ne porte aucun secret, et il n'existe aucun emplacement pour en porter un.</b> Ni ici,
/// ni ailleurs dans le <c>Manifest</c> : le secret d'appel relève du déploiement, non du paysage
/// déclaré, et une adresse est ce que le catalogue a le droit de connaître. Un champ « secret »
/// dans un catalogue relu à l'écran serait un secret affiché.
/// ⚠️ Conséquence assumée : les identifiants d'usager d'une URL — <c>https://jean:mot@…</c> — sont
/// refusés, parce qu'ils sont exactement le secret que ce type ne doit pas savoir transporter.
/// </para>
/// <para>
/// <b>Le sens est unique</b> : le service appelle toujours, l'application ne rappelle jamais. Il
/// n'existe donc aucune adresse symétrique à déclarer, et l'<c>Adapter</c> n'a besoin d'aucune
/// adresse du service.
/// </para>
/// </remarks>
[ValueObject<string>]
public readonly partial struct AdapterAddress
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
      // Une adresse vide n'existe pas : l'absence d'adresse s'écrit par l'absence du champ, jamais
      // par une chaîne vide qui ferait croire à une adresse qu'on aurait mal saisie.
      return Validation.Invalid("L'adresse de l'Adapter est absente ou vide.");
    }

    if (value.Length > MaxLength)
    {
      return Validation.Invalid($"L'adresse de l'Adapter dépasse {MaxLength} caractères.");
    }

    if (!Uri.TryCreate(value, UriKind.Absolute, out var address)
        || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
    {
      return Validation.Invalid("L'adresse de l'Adapter n'est pas une URL http ou https absolue.");
    }

    if (address.UserInfo.Length > 0)
    {
      return Validation.Invalid("L'adresse de l'Adapter ne porte pas d'identifiants : le Manifest ne détient aucun secret.");
    }

    return Validation.Ok;
  }
}
