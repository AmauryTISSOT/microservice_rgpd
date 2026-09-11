using System.Text.RegularExpressions;
using Vogen;

namespace MicroserviceRgpd.Core.Requests;

/// <summary>
/// L'adresse email de la personne qui exerce son droit. <b>Facultative sur la demande</b> —
/// l'absence s'y dit par <c>null</c> —, mais toujours bien formée ici.
/// </summary>
/// <remarks>
/// <para>
/// <b>La forme est celle de <c>&lt;input type=email&gt;</c></b> : la regex de la spécification
/// WHATWG, recopiée telle quelle, pour que le serveur et le navigateur refusent exactement les mêmes
/// saisies. Elle est volontairement plus stricte que la RFC 5322 — pas de guillemets, pas de
/// caractères hors ASCII — et plus lâche sur le domaine, qui peut n'avoir qu'un label.
/// </para>
/// <para>
/// ⚠️ <b>La casse est conservée.</b> La partie locale d'une adresse est sensible à la casse selon la
/// RFC, et l'<c>Operator</c> recopie ce que la personne a écrit : rien ne la met en minuscules.
/// </para>
/// </remarks>
[ValueObject<string>]
public readonly partial struct EmailAddress
{
  /// <summary>Le plafond, en unités UTF-16 : la longueur maximale d'un chemin SMTP, moins les chevrons.</summary>
  public const int MaxLength = 254;

  /// <summary>Les bordures sont retirées avant validation <b>et</b> avant stockage.</summary>
  private static string NormalizeInput(string? input) => input?.Trim() ?? string.Empty;

  private static Validation Validate(string value)
  {
    return value.Length is > 0 and <= MaxLength && WhatwgEmail().IsMatch(value)
      ? Validation.Ok
      : Validation.Invalid(DataSubjectRequestMessages.EmailInvalid);
  }

  /// <summary>
  /// La regex WHATWG de <c>&lt;input type=email&gt;</c>, ancrée par <c>\z</c> plutôt que <c>$</c> :
  /// en .NET, <c>$</c> admettrait un saut de ligne final.
  /// </summary>
  [GeneratedRegex(
    @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*\z",
    RegexOptions.CultureInvariant)]
  private static partial Regex WhatwgEmail();
}
