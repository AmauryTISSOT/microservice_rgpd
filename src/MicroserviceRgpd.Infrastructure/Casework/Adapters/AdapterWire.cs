using MicroserviceRgpd.Core.Casework;

namespace MicroserviceRgpd.Infrastructure.Casework.Adapters;

/// <summary>
/// La forme du contrat d'<c>Adapter</c> sur le fil, écrite <b>une fois</b> : l'en-tête qui porte le
/// secret, le paramètre qui porte le système, et l'adresse d'une opération.
/// </summary>
/// <remarks>
/// Elle vit ici plutôt qu'en deux exemplaires chez l'appel et chez la sonde : ce que le développeur
/// du client implémente est <b>une</b> forme, et deux copies finiraient par ne plus décrire la même
/// — la seconde dérivant sans que rien, du côté du service, ne s'en aperçoive.
/// </remarks>
public static class AdapterWire
{
  /// <summary>
  /// L'en-tête qui porte le secret. Un en-tête propre plutôt qu'<c>Authorization</c> : le contrat
  /// n'a ni schéma, ni jeton, ni porteur à présenter, et emprunter le mot ferait croire à un
  /// <c>Bearer</c> que personne n'émet ni ne valide.
  /// </summary>
  public const string SecretHeader = "X-RGPD-Secret";

  /// <summary>Le paramètre qui porte le système — <b>en paramètre, jamais en corps</b>.</summary>
  public const string SystemParameter = "system_id";

  /// <summary>
  /// L'adresse d'une opération : l'adresse déclarée, <b>une opération par <see cref="Capability"/></b>,
  /// et le <c>system_id</c> en paramètre — jamais en corps, pour qu'un seul <c>Adapter</c> puisse
  /// servir plusieurs systèmes sans les démêler lui-même.
  /// </summary>
  public static Uri AddressOf(AdapterAddress address, DeclaredSystemId declaredSystem, Capability capability)
  {
    ArgumentNullException.ThrowIfNull(capability);

    // L'identifiant du système est déjà d'un jeu de caractères sûr en URL ; il est échappé quand
    // même, l'inverse étant une exception à retenir de tête à chaque nouvelle traversée.
    return new Uri(
      $"{address.Value.TrimEnd('/')}/{capability.Token}"
      + $"?{SystemParameter}={Uri.EscapeDataString(declaredSystem.Value)}",
      UriKind.Absolute);
  }
}
