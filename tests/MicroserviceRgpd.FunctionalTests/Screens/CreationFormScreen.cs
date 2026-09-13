using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.FunctionalTests.Layout;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// Le <b>formulaire de la modale de création</b>, tel que le serveur le rend avec le tableau des
/// demandes : ses champs, dans l'ordre où l'<c>Operator</c> lit une demande reçue, et leurs valeurs
/// par défaut.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Ce qui est gardé ici est ce que le serveur rend, pas ce que le script en fait.</b> Le script
/// réinitialise le formulaire et recalcule « aujourd'hui » à chaque ouverture ; cela se vérifie
/// dans un vrai navigateur (<c>MicroserviceRgpd.BrowserTests</c>). Le rendu du serveur reste la
/// valeur par défaut du formulaire, celle que le script retrouve.
/// </para>
/// <para>
/// Chaque champ se lit par son <b>nom accessible</b> — son <c>label</c>, ou le texte du bouton —,
/// et chaque libellé est recopié à dessein.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class CreationFormScreen(CustomWebApplicationFactory<Program> factory)
{
  private const string AiQualification = "Qualification du droit par IA";

  private readonly LayoutSurface _layout = new(factory);

  /// <summary>
  /// <b>Les champs apparaissent dans l'ordre de lecture d'une demande reçue</b> — d'où elle vient,
  /// quand, de qui, ce qu'elle dit, quel droit —, entre la croix de l'en-tête et les deux actions
  /// du pied.
  /// </summary>
  [Fact]
  public async Task RendersTheFieldsInTheReadingOrderOfAReceivedRequest()
  {
    var dialog = await DialogAsync();

    ControlsIn(dialog).Select(control => control.Name).ShouldBe(
      [
        "Fermer",
        "Origine",
        "Date de réception",
        "Nom",
        "Prénom",
        "Email",
        "Identité vérifiée",
        "Message",
        AiQualification,
        "Droits RGPD",
        "Annuler",
        "Créer",
      ],
      "La modale ne porte pas ses champs dans l'ordre de lecture d'une demande reçue.");
  }

  /// <summary>
  /// <b>L'origine est « Email » par défaut</b>, et « Courrier » en est la seule autre : l'<c>Operator</c>
  /// ne clique que pour un courrier.
  /// </summary>
  [Fact]
  public async Task OffersEmailByDefaultAndLetterAsTheOnlyOtherOrigin()
  {
    var origin = ControlNamed(await DialogAsync(), "Origine");

    origin.Tag.ShouldBe("select");
    OptionsIn(origin.Contents).ShouldBe([("Email", "Email", true), ("Letter", "Courrier", false)]);
  }

  /// <summary>
  /// <b>La date de réception est aujourd'hui à Paris, et ne le dépasse pas</b> : c'est à la fois sa
  /// valeur par défaut et sa borne haute. Il n'y a pas de borne basse — un courrier peut être resté
  /// longtemps en attente.
  /// </summary>
  /// <remarks>
  /// ⚠️ L'horloge du service est <b>avancée de trois jours</b> : un écran qui lirait l'horloge de la
  /// machine plutôt que celle du service rendrait la date réelle, et le test le verrait.
  /// </remarks>
  [Fact]
  public async Task DatesTheReceptionTodayInParisAndCapsItThere()
  {
    try
    {
      factory.Clock.Advance(TimeSpan.FromDays(3));

      var today = ParisCalendar.Today(factory.Clock).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
      var date = ControlNamed(await DialogAsync(), "Date de réception");

      date.Tag.ShouldBe("input");
      AttributeOf(date, "type").ShouldBe("date");
      AttributeOf(date, "lang").ShouldBe("fr");
      AttributeOf(date, "value").ShouldBe(today, "La date de réception n'est pas aujourd'hui à Paris par défaut.");
      AttributeOf(date, "max").ShouldBe(today, "La date de réception n'est pas bornée à aujourd'hui à Paris.");
      AttributeOf(date, "min").ShouldBeNull("La date de réception porte une borne basse.");
    }
    finally
    {
      factory.Clock.Reset();
    }
  }

  /// <summary>
  /// <b>L'identification et le message partent vides</b>, et l'identité n'est pas déclarée vérifiée :
  /// le formulaire est neutre.
  /// </summary>
  [Fact]
  public async Task LeavesTheIdentificationAndTheMessageBlank()
  {
    var dialog = await DialogAsync();

    foreach (var name in new[] { "Nom", "Prénom", "Email" })
    {
      var field = ControlNamed(dialog, name);

      field.Tag.ShouldBe("input");
      AttributeOf(field, "type").ShouldBe("text", $"Le champ {name} n'est pas un champ texte.");
      (AttributeOf(field, "value") ?? string.Empty).ShouldBeEmpty($"Le champ {name} n'est pas vide par défaut.");
    }

    var verified = ControlNamed(dialog, "Identité vérifiée");
    AttributeOf(verified, "type").ShouldBe("checkbox");
    Regex.IsMatch(verified.Attributes, @"\bchecked\b").ShouldBeFalse("L'identité est déclarée vérifiée par défaut.");

    var message = ControlNamed(dialog, "Message");
    message.Tag.ShouldBe("textarea");
    message.Contents.Trim().ShouldBeEmpty("Le message n'est pas vide par défaut.");
  }

  /// <summary>
  /// <b>La liste des droits affiche « Sélectionner un droit », puis exactement les six droits dans
  /// l'ordre des articles.</b> ⚠️ « Hors périmètre » n'y est jamais : c'est un verdict de
  /// qualification, pas un droit qu'une personne invoque.
  /// </summary>
  [Fact]
  public async Task OffersTheSixRightsInArticleOrderAfterAPrompt()
  {
    var rights = ControlNamed(await DialogAsync(), "Droits RGPD");

    rights.Tag.ShouldBe("select");
    OptionsIn(rights.Contents).ShouldBe(
      [
        (string.Empty, "Sélectionner un droit", false),
        ("Access", "droit d'accès", false),
        ("Rectification", "droit de rectification", false),
        ("Erasure", "droit à l'effacement", false),
        ("Restriction", "droit à la limitation du traitement", false),
        ("Portability", "droit à la portabilité", false),
        ("Objection", "droit d'opposition", false),
      ],
      "La liste des droits ne propose pas l'invite puis les six droits, dans l'ordre des articles.");

    rights.Contents.ShouldNotContain("OutOfScope", Case.Sensitive);
    rights.Contents.ShouldNotContain("hors périmètre", Case.Insensitive);
  }

  /// <summary>
  /// <b>La qualification du droit par IA est offerte</b> : le bouton est actif, sans infobulle, et
  /// porte l'adresse du handler <c>Propose</c> de l'écran de qualification — que la vue rend, et
  /// dont le page model du tableau ne sait rien.
  /// </summary>
  [Fact]
  public async Task OffersTheAiQualificationWithTheAddressOfTheProposal()
  {
    var button = ControlNamed(await DialogAsync(), AiQualification);

    button.Tag.ShouldBe("button");
    AttributeOf(button, "type").ShouldBe("button");
    Regex.IsMatch(button.Attributes, @"\bdisabled\b").ShouldBeFalse("Le bouton de qualification par IA est désactivé.");
    AttributeOf(button, "title").ShouldBeNull("Le bouton de qualification par IA porte une infobulle.");
    AttributeOf(button, "data-propose").ShouldBe("/qualification?handler=Propose");
  }

  /// <summary>
  /// ⚠️ <b>Le navigateur ne juge rien à la place des règles du service.</b> Le formulaire porte
  /// <c>novalidate</c> ; l'email est un champ texte, pas un <c>type=email</c> ; et aucun champ ne
  /// porte de <c>maxlength</c>, qui tronquerait en silence un texte collé au lieu d'en dire le refus.
  /// </summary>
  [Fact]
  public async Task LeavesNoRuleToTheBrowser()
  {
    var dialog = await DialogAsync();

    var form = Regex.Match(dialog, @"<form\b([^>]*)>");
    form.Success.ShouldBeTrue("La modale ne porte pas de formulaire.");
    Regex.IsMatch(form.Groups[1].Value, @"\bnovalidate\b").ShouldBeTrue("Le formulaire laisse le navigateur valider.");

    dialog.ShouldNotContain("maxlength", Case.Insensitive, "Un champ tronquerait une saisie trop longue.");
    dialog.ShouldNotContain("type=\"email\"", Case.Insensitive, "L'email est laissé au jugement du navigateur.");
    dialog.ShouldNotContain(" required", Case.Insensitive, "Un champ est laissé au jugement du navigateur.");
  }

  /// <summary>
  /// <b>La page fournit au script les dix messages d'erreur</b>, par un îlot JSON : le script porte la
  /// logique des règles, le serveur en écrit les mots, une seule fois.
  /// </summary>
  [Fact]
  public async Task HandsTheTenMessagesToTheScript()
  {
    var rendered = await _layout.ReadAsync(LayoutSurface.Board);

    var island = Regex.Match(
      rendered,
      @"<script\b(?=[^>]*\btype=""application/json"")(?=[^>]*\bid=""request-messages"")[^>]*>(.*?)</script>",
      RegexOptions.Singleline);
    island.Success.ShouldBeTrue("La page ne fournit pas au script l'îlot de ses messages.");

    JsonSerializer.Deserialize<Dictionary<string, string>>(island.Groups[1].Value).ShouldBe(
      new Dictionary<string, string>
      {
        ["identificationMissing"] = "Renseignez un email, ou un nom et un prénom.",
        ["receivedOnMissing"] = "La date de réception est obligatoire.",
        ["receivedOnMalformed"] = "La date de réception doit être au format jj/mm/aaaa.",
        ["receivedOnInTheFuture"] = "La date de réception ne peut pas être dans le futur.",
        ["emailInvalid"] = "L'adresse email n'est pas valide.",
        ["lastNameTooLong"] = "Le nom ne peut pas dépasser 100 caractères.",
        ["firstNameTooLong"] = "Le prénom ne peut pas dépasser 100 caractères.",
        ["messageMissing"] = "Le message est obligatoire.",
        ["messageTooLong"] = "Le message ne peut pas dépasser 10 000 caractères.",
        ["rightMissing"] = "Sélectionnez un droit RGPD.",
      },
      ignoreOrder: true);
  }

  private async Task<string> DialogAsync()
  {
    var main = LayoutSurface.MainOf(await _layout.ReadAsync(LayoutSurface.Board));
    var dialog = Regex.Match(main, @"<dialog\b.*?</dialog>", RegexOptions.Singleline);

    dialog.Success.ShouldBeTrue("Le tableau ne porte pas de modale de création.");

    return dialog.Value;
  }

  /// <summary>
  /// Les <b>contrôles</b> d'un fragment, dans l'ordre du document, chacun sous son nom accessible :
  /// son <c>aria-label</c>, le <c>label</c> qui le désigne, ou le texte du bouton. Les champs cachés,
  /// que personne ne lit, sont écartés.
  /// </summary>
  private static IReadOnlyList<Control> ControlsIn(string fragment)
  {
    var labels = Regex.Matches(fragment, @"<label\b[^>]*\bfor=""([^""]+)""[^>]*>(.*?)</label>", RegexOptions.Singleline)
      .ToDictionary(label => label.Groups[1].Value, label => LayoutSurface.TextIn(label.Groups[2].Value));

    return
    [
      .. Regex.Matches(
          fragment,
          @"<(?<tag>button|select|textarea)\b(?<attributes>[^>]*)>(?<contents>.*?)</\k<tag>>|<(?<tag>input)\b(?<attributes>[^>]*)>",
          RegexOptions.Singleline)
        .Select(control => new Control(
          control.Groups["tag"].Value,
          control.Groups["attributes"].Value,
          control.Groups["contents"].Value,
          string.Empty))
        .Where(control => AttributeOf(control, "type") != "hidden")
        .Select(control => control with
        {
          Name = AttributeOf(control, "aria-label")
            ?? (AttributeOf(control, "id") is { } id && labels.TryGetValue(id, out var label) ? label : null)
            ?? LayoutSurface.TextIn(control.Contents),
        }),
    ];
  }

  private static Control ControlNamed(string dialog, string name)
  {
    return ControlsIn(dialog).Where(control => control.Name == name)
      .ShouldHaveSingleItem($"La modale ne porte pas un et un seul champ « {name} ».");
  }

  private static string? AttributeOf(Control control, string attribute)
  {
    var match = Regex.Match(control.Attributes, $@"(?:^|\s){Regex.Escape(attribute)}=""([^""]*)""");

    return match.Success ? match.Groups[1].Value : null;
  }

  /// <summary>Les options d'une liste : leur valeur, leur texte, et si elles sont choisies par défaut.</summary>
  private static IReadOnlyList<(string Value, string Text, bool Selected)> OptionsIn(string select)
  {
    return
    [
      .. Regex.Matches(select, @"<option\b([^>]*)>(.*?)</option>", RegexOptions.Singleline)
        .Select(option => (
          Regex.Match(option.Groups[1].Value, @"\bvalue=""([^""]*)""").Groups[1].Value,
          LayoutSurface.TextIn(option.Groups[2].Value),
          Regex.IsMatch(option.Groups[1].Value, @"\bselected\b"))),
    ];
  }

  private sealed record Control(string Tag, string Attributes, string Contents, string Name);
}
