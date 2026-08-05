using MicroserviceRgpd.Core.Casework;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MicroserviceRgpd.Web.Pages.Casework;

/// <summary>
/// Ce qu'un humain saisit à l'écran pour déclarer ou réviser un système — <b>des chaînes, et rien
/// que des chaînes</b>, jusqu'à ce qu'elles franchissent la frontière du domaine.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il n'y a aucun champ pour le secret de l'<c>Adapter</c>.</b> Pas de champ masqué, pas de
/// champ optionnel replié : l'absence est structurelle, et c'est ce qui la rend tenable.
/// </para>
/// <para>
/// <b>Aucun champ ne demande un nom de table, de colonne ou de champ.</b> Le formulaire est le seul
/// endroit où quelqu'un aurait l'occasion d'en écrire un ; ne pas lui offrir la case est ce qui
/// ferme la porte.
/// </para>
/// </remarks>
public sealed class DeclaredSystemForm
{
  /// <summary>L'identifiant choisi par l'humain, et celui que l'<c>Adapter</c> recevra.</summary>
  public string? Id { get; set; }

  /// <summary>Le nom sous lequel l'<c>Operator</c> reconnaît ce système.</summary>
  public string? Label { get; set; }

  /// <summary>Ce que ce système contient, en prose française libre. Obligatoire.</summary>
  public string? Contents { get; set; }

  /// <summary>
  /// Les capacités cochées, par leur nom canonique anglais. <b>Aucune case cochée est une réponse
  /// valide</b> — c'est le niveau 0, et le régime majoritaire.
  /// </summary>
  public string[] Capabilities { get; set; } = [];

  /// <summary>L'adresse de l'<c>Adapter</c>, laissée vide s'il n'y en a pas.</summary>
  public string? AdapterAddress { get; set; }

  /// <summary>Reprend le formulaire tel qu'un système déjà déclaré le remplirait.</summary>
  public static DeclaredSystemForm Of(DeclaredSystem system)
  {
    ArgumentNullException.ThrowIfNull(system);

    return new DeclaredSystemForm
    {
      Id = system.Id.Value,
      Label = system.Label.Value,
      Contents = system.Contents.Value,
      Capabilities = [.. system.Capabilities.Select(capability => capability.Name)],
      AdapterAddress = system.AdapterAddress?.Value,
    };
  }

  /// <summary>
  /// Fait franchir au formulaire la frontière du domaine, ou <b>nomme à l'humain</b> ce qu'il a mal
  /// rempli, champ par champ.
  /// </summary>
  /// <remarks>
  /// Les messages viennent des types du domaine eux-mêmes, jamais d'une seconde rédaction : deux
  /// libellés pour une même règle finiraient par ne plus dire la même chose, et l'écran mentirait
  /// sur ce que la base accepte.
  /// </remarks>
  /// <param name="modelState">L'endroit où les refus se déposent, sous le nom du champ fautif.</param>
  /// <param name="prefix">Le préfixe de liaison du formulaire, tel que la page l'a déclaré.</param>
  /// <returns>Les valeurs du domaine, ou <c>null</c> si au moins un champ a été refusé.</returns>
  public DeclaredSystemFields? Read(ModelStateDictionary modelState, string prefix)
  {
    ArgumentNullException.ThrowIfNull(modelState);

    // Un champ laissé vide arrive `null` de la liaison de modèle, et le refus de Vogen sur le nul
    // est un message anglais et générique. Le vide entre donc comme vide : c'est la règle du champ
    // — écrite en français, dans le type qui la porte — qui doit parler à qui a saisi.
    var id = FormBoundary.Read(modelState, prefix, nameof(Id), () => DeclaredSystemId.From(Id ?? string.Empty));
    var label = FormBoundary.Read(modelState, prefix, nameof(Label), () => SystemLabel.From(Label ?? string.Empty));
    var contents = FormBoundary.Read(modelState, prefix, nameof(Contents), () => SystemContents.From(Contents ?? string.Empty));

    // Le champ vide est l'absence d'Adapter, et non une adresse mal saisie : c'est le régime
    // majoritaire, et le seul endroit du formulaire où le vide soit une réponse à part entière
    // avec la liste de capacités.
    Core.Casework.AdapterAddress? adapterAddress = string.IsNullOrWhiteSpace(AdapterAddress)
      ? null
      : FormBoundary.Read(modelState, prefix, nameof(AdapterAddress), () => Core.Casework.AdapterAddress.From(AdapterAddress!));

    var capabilities = ReadCapabilities(modelState, prefix);

    if (!modelState.IsValid || id is null || label is null || contents is null || capabilities is null)
    {
      return null;
    }

    return new DeclaredSystemFields(id.Value, label.Value, contents.Value, capabilities, adapterAddress);
  }

  /// <summary>
  /// Les capacités cochées. Une valeur inconnue n'est pas une saisie humaine — les cases sont
  /// closes — mais un formulaire forgé : elle est refusée plutôt qu'ignorée, faute de quoi le
  /// service enregistrerait moins que ce qu'on croit lui avoir dit.
  /// </summary>
  private IReadOnlyCollection<Capability>? ReadCapabilities(ModelStateDictionary modelState, string prefix)
  {
    var capabilities = new List<Capability>();

    foreach (var name in Capabilities)
    {
      if (!Capability.TryFromName(name, out var capability))
      {
        modelState.AddModelError($"{prefix}.{nameof(Capabilities)}", $"« {name} » n'est pas une capacité du catalogue.");

        return null;
      }

      capabilities.Add(capability);
    }

    return capabilities;
  }
}
