using System.Globalization;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MicroserviceRgpd.Web.Pages.Casework;

/// <summary>
/// Ce qu'un <c>Operator</c> saisit pour <b>déposer à la main</b> une demande arrivée par courriel —
/// <b>des chaînes, et rien que des chaînes</b>, jusqu'à ce qu'elles franchissent la frontière du
/// domaine.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Aucun emplacement pour une pièce jointe, et c'est une propriété du type.</b> Ni fichier, ni
/// base 64, ni URL à récupérer — pas plus ici que sur le canal API. Aucune pièce d'identité n'entre
/// dans le service, tous canaux confondus (CEPD § 79) : un champ qui pourrait en porter une serait un
/// champ dans lequel une pièce finirait par entrer, et le service aurait alors à prouver qu'il ne l'a
/// pas gardée. Le <c>EvidenceLog</c> ne garde qu'un <b>fait</b> de vérification, jamais la pièce.
/// </para>
/// <para>
/// <b>Le sac de désignations a la forme du canal API, exactement.</b> Une paire nature/valeur, dans
/// le vocabulaire fermé du contrat d'<c>Adapter</c> : deux formes voudraient dire deux chemins de
/// recherche à maintenir, et l'un des deux finirait par ne plus être celui qu'on croit.
/// </para>
/// <para>
/// <b>Rien n'est obligatoire ici sauf le nom du signataire.</b> Ni désignation, ni droit, ni date, ni
/// motivation : le service ne barre jamais la route, et un dossier faible doit rester enregistrable
/// et <b>visible comme tel</b>. Le nom fait exception parce qu'il n'est pas une donnée du dossier
/// mais la <b>signature du geste</b> : sans lui, la preuve dirait que personne n'a déposé.
/// </para>
/// </remarks>
public sealed class DepositForm
{
  /// <summary>
  /// Les natures des désignations, dans l'ordre des lignes du formulaire, par leur mot du contrat
  /// d'<c>Adapter</c>.
  /// </summary>
  public string[] DesignationKinds { get; set; } = [];

  /// <summary>Les valeurs des désignations, dans le même ordre que <see cref="DesignationKinds"/>.</summary>
  public string[] DesignationValues { get; set; } = [];

  /// <summary>
  /// Les droits cochés, par leur nom canonique anglais. <b>Aucune case cochée est une réponse
  /// valide</b> : une demande n'exerçant aucun droit entre quand même, et se clora
  /// <c>NotApplicable</c> sous la signature d'un humain.
  /// </summary>
  public string[] Rights { get; set; } = [];

  /// <summary>Ce que l'<c>Operator</c> déclare de l'identité du demandeur, par son nom canonique anglais.</summary>
  public string? IdentityDeclaration { get; set; }

  /// <summary>D'où vient la reconnaissance de ces droits, par son nom canonique anglais.</summary>
  public string? Origin { get; set; }

  /// <summary>
  /// La date de réception <b>réelle</b> — celle du jour où le client a reçu la demande, jamais celle
  /// du geste de dépôt. Laissée vide, le service retombe sur son défaut et le consigne <b>comme</b>
  /// un défaut.
  /// </summary>
  public string? ReceivedOn { get; set; }

  /// <summary>
  /// La méthode de vérification d'identité, par son nom canonique anglais, ou vide si personne n'a
  /// pesé. ⚠️ Vide et <c>None</c> ne se confondent pas : <c>None</c> est une réponse.
  /// </summary>
  public string? VerificationMethod { get; set; }

  /// <summary>
  /// Le détail de la motivation, en prose libre. <b>Texte qui meurt</b> : nominatif par nature, il
  /// vit sur le dossier et meurt à sa clôture. Il n'a aucun chemin vers le <c>EvidenceLog</c>.
  /// </summary>
  public string? MotivationDetail { get; set; }

  /// <summary>Le nom que l'<c>Operator</c> saisit pour signer le dépôt. Sans authentification, et sans mémoire.</summary>
  public string? SignedBy { get; set; }

  /// <summary>
  /// Fait franchir au formulaire la frontière du domaine, ou <b>nomme à l'humain</b> ce qu'il a mal
  /// rempli, champ par champ.
  /// </summary>
  /// <remarks>
  /// <para>
  /// <b>Les refus se déposent tous par <see cref="FormBoundary"/></b>, qui tient les deux gestes une
  /// seule fois pour tous les écrans de ce contexte : franchir, et nommer à l'humain sous le nom de
  /// son champ ce que le type a refusé.
  /// </para>
  /// <para>
  /// <b>Le nom du signataire n'est pas relu ici</b> : sa règle vit dans le type de la preuve, et la
  /// redire ferait deux rédactions d'une même exigence — qui finiraient par différer.
  /// </para>
  /// </remarks>
  /// <param name="modelState">L'endroit où les refus se déposent, sous le nom du champ fautif.</param>
  /// <param name="prefix">Le préfixe de liaison du formulaire, tel que la page l'a déclaré.</param>
  /// <param name="depositedAt">L'instant du dépôt, dont le défaut de date de réception se déduit.</param>
  /// <returns>Les valeurs du domaine, ou <c>null</c> si au moins un champ a été refusé.</returns>
  public DepositedRequest? Read(ModelStateDictionary modelState, string prefix, DateTimeOffset depositedAt)
  {
    ArgumentNullException.ThrowIfNull(modelState);

    var designations = ReadDesignations(modelState, prefix);
    var rights = ReadRights(modelState, prefix);
    var declaration = FormBoundary.ReadVocabulary<IdentityDeclaration>(
      modelState,
      prefix,
      nameof(IdentityDeclaration),
      IdentityDeclaration,
      Core.Casework.IdentityDeclaration.TryFromName,
      "n'est pas une déclaration d'identité du vocabulaire");

    var origin = FormBoundary.ReadVocabulary<ClaimOrigin>(
      modelState,
      prefix,
      nameof(Origin),
      Origin,
      ClaimOrigin.TryFromName,
      "n'est pas une origine du vocabulaire");

    var reception = ReadReception(modelState, prefix, depositedAt);
    var motivation = ReadMotivation(modelState, prefix);

    if (!modelState.IsValid || designations is null || rights is null || declaration is null || origin is null
      || reception is null)
    {
      return null;
    }

    return new DepositedRequest(declaration, motivation, designations, rights, origin, reception, SignedBy);
  }

  /// <summary>
  /// Le sac, ligne par ligne. Les lignes <b>entièrement vides sont ignorées</b> — le formulaire en
  /// offre plusieurs d'avance, et se plaindre de celles qu'on n'a pas remplies ferait d'une commodité
  /// un piège.
  /// </summary>
  private IReadOnlyCollection<Designation>? ReadDesignations(ModelStateDictionary modelState, string prefix)
  {
    var bag = new List<Designation>();
    var field = $"{prefix}.{nameof(DesignationValues)}";

    for (var line = 0; line < DesignationValues.Length; line++)
    {
      var value = DesignationValues[line];

      if (string.IsNullOrWhiteSpace(value))
      {
        continue;
      }

      // La nature manque quand la ligne a été forgée : le formulaire pose toujours les deux ensemble.
      var token = line < DesignationKinds.Length ? DesignationKinds[line] : null;
      var kind = DesignationKind.FromToken(token);

      if (kind is null)
      {
        modelState.AddModelError(field, $"« {token} » n'est pas une nature de désignation du vocabulaire.");

        return null;
      }

      var designation = FormBoundary.Declared(
        modelState,
        prefix,
        nameof(DesignationValues),
        () => Designation.Of(kind, value));

      if (designation is null)
      {
        return null;
      }

      bag.Add(designation);
    }

    return bag;
  }

  /// <summary>
  /// Les droits cochés. <b>Aucun est une réponse</b> : une demande n'exerçant aucun droit entre
  /// quand même, et le dossier se clora <c>NotApplicable</c> sous la signature d'un humain.
  /// </summary>
  private IReadOnlyCollection<DataSubjectRight>? ReadRights(ModelStateDictionary modelState, string prefix)
  {
    var claimed = new List<DataSubjectRight>();

    foreach (var name in Rights)
    {
      var right = FormBoundary.ReadVocabulary<DataSubjectRight>(
        modelState,
        prefix,
        nameof(Rights),
        name,
        DataSubjectRight.TryFromName,
        "n'est pas un droit de la taxonomie");

      if (right is null)
      {
        return null;
      }

      claimed.Add(right);
    }

    return claimed;
  }

  /// <summary>
  /// La date de réception telle que l'<c>Operator</c> l'a déclarée, ou <b>le défaut nommé comme un
  /// défaut</b> quand il l'ignore.
  /// </summary>
  /// <remarks>
  /// <b>Une date dans l'avenir est refusée, et c'est le seul refus de ce champ.</b> Le mois de
  /// l'art. 12.3 court <em>avant</em> nous : une réception postérieure au dépôt n'est pas une saisie
  /// faible qu'il faudrait garder visible, c'est une frappe qui rendrait une échéance fausse et un
  /// dépassement invisible. Une date ancienne, elle, passe telle quelle — même très ancienne : un
  /// dossier oublié six mois est exactement ce que le service doit pouvoir dire.
  /// </remarks>
  private ReceptionDate? ReadReception(ModelStateDictionary modelState, string prefix, DateTimeOffset depositedAt)
  {
    if (string.IsNullOrWhiteSpace(ReceivedOn))
    {
      // Personne ne l'a déclarée : le service tient neuf jours pour déjà courus, et le drapeau part
      // avec la date pour que l'écran et le EvidenceLog la nomment comme une hypothèse, jamais un fait.
      return ReceptionDate.Defaulted(depositedAt);
    }

    var field = $"{prefix}.{nameof(ReceivedOn)}";

    if (!DateOnly.TryParse(ReceivedOn, CultureInfo.InvariantCulture, out var declared))
    {
      modelState.AddModelError(field, $"« {ReceivedOn} » n'est pas une date.");

      return null;
    }

    var on = new DateTimeOffset(declared.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    if (on > depositedAt)
    {
      modelState.AddModelError(
        field,
        "La date de réception est dans l'avenir : le délai de l'art. 12.3 court avant le dépôt, "
        + "jamais après. Laissez la case vide si vous ignorez cette date.");

      return null;
    }

    return ReceptionDate.Declared(on);
  }

  /// <summary>
  /// La motivation, ou <c>null</c> si <b>personne ne l'a pesée</b> — ce qui n'est jamais refusé : le
  /// service ne barre pas la route, et le dossier restera visible comme faible.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Un détail écrit sans méthode ne fabrique pas une motivation.</b> La méthode est ce qui se
  /// compte et ce qui survit ; une motivation qui n'aurait que de la prose serait invisible au
  /// contrôle et mourrait à la clôture, c'est-à-dire n'aurait jamais existé. Le détail est alors
  /// refusé en le nommant, plutôt que gardé sous une méthode que personne n'a choisie.
  /// </para>
  /// <para>
  /// <b>Ce n'est pas une route barrée, et la distinction se tient.</b> « Le service ne bloque jamais »
  /// porte sur les <b>états du monde</b> : « je n'ai rien vérifié », « j'ignore la date », « la
  /// personne n'est pas identifiée » sont tous enregistrables, et c'est la raison d'être de
  /// <see cref="IdentityVerificationMethod.None"/>, de la date laissée vide et d'<c>Unverified</c>.
  /// Une prose sans méthode n'est l'état d'aucun monde : c'est un champ composé à moitié rempli, dont
  /// l'autre moitié est un menu où <em>aucune</em> se trouve juste là. Le refus rend sa prose à
  /// l'humain plutôt que de la jeter.
  /// </para>
  /// </remarks>
  private IdentityMotivation? ReadMotivation(ModelStateDictionary modelState, string prefix)
  {
    if (string.IsNullOrWhiteSpace(VerificationMethod))
    {
      if (!string.IsNullOrWhiteSpace(MotivationDetail))
      {
        modelState.AddModelError(
          $"{prefix}.{nameof(VerificationMethod)}",
          "Un détail a été écrit sans méthode : c'est la méthode qui se compte et qui survit à la "
          + "clôture. Choisissez-en une — « aucune » en est une.");
      }

      return null;
    }

    var method = FormBoundary.ReadVocabulary<IdentityVerificationMethod>(
      modelState,
      prefix,
      nameof(VerificationMethod),
      VerificationMethod,
      IdentityVerificationMethod.TryFromName,
      "n'est pas une méthode du vocabulaire");

    return method is null
      ? null
      : FormBoundary.Declared(
        modelState,
        prefix,
        nameof(MotivationDetail),
        () => IdentityMotivation.Of(method, MotivationDetail));
  }

}

/// <summary>
/// Une demande déposée à la main, une fois la frontière du domaine franchie.
/// </summary>
/// <param name="IdentityDeclaration">Ce que l'<c>Operator</c> déclare de l'identité du demandeur.</param>
/// <param name="Motivation">Ce qu'il a pesé, ou <c>null</c> si personne ne l'a pesé.</param>
/// <param name="Designations">Le sac sous lequel on cherchera la personne, éventuellement vide.</param>
/// <param name="Rights">Les droits reconnus, éventuellement aucun.</param>
/// <param name="Origin">D'où vient la reconnaissance de ces droits.</param>
/// <param name="Reception">La date de réception, et le régime sous lequel le service la sait.</param>
/// <param name="SignedBy">Le nom saisi, exigé par le type de la signature.</param>
public sealed record DepositedRequest(
  IdentityDeclaration IdentityDeclaration,
  IdentityMotivation? Motivation,
  IReadOnlyCollection<Designation> Designations,
  IReadOnlyCollection<DataSubjectRight> Rights,
  ClaimOrigin Origin,
  ReceptionDate Reception,
  string? SignedBy);
