using System.Net;
using MicroserviceRgpd.Core.Casework;
using MicroserviceRgpd.Core.SharedKernel;

namespace MicroserviceRgpd.FunctionalTests.Screens;

/// <summary>
/// L'écran du <b>dépôt manuel</b>, exercé par sa seule frontière HTTP — c'est-à-dire exactement ce
/// que fait un navigateur.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ Le test le plus important de ce fichier n'est pas celui du chemin heureux : c'est
/// <see cref="ClaimsTheMotivationWithoutEverBarringTheRoad"/>. Un service qui refuserait le dépôt
/// faute de motivation laisserait la demande dans une boîte aux lettres pendant que le mois de
/// l'art. 12.3 court — et la faiblesse, au lieu d'être visible, deviendrait invisible.
/// </para>
/// <para>
/// Le second est <see cref="OffersNoPlaceForAnIdentityDocumentOnThisChannelEither"/> : l'absence de
/// pièce jointe est une propriété de l'écran, et un champ ajouté par mégarde le ferait échouer.
/// </para>
/// </remarks>
[Collection(WebCollection.Name)]
public class DepositScreen(CustomWebApplicationFactory<Program> factory)
{
  private readonly OperatorSurface _surface = new(factory);

  /// <summary>
  /// <b>Un dépôt manuel ouvre un dossier lisible dans la GUI</b>, avec le sac de désignations que
  /// l'<c>Operator</c> a transcrit — de forme identique à celui du canal API.
  /// </summary>
  [Fact]
  public async Task OpensACaseTheOperatorCanThenReadOnTheScreen()
  {
    var deposited = await _surface.DepositAsync(new ADeposit
    {
      Designations =
      [
        new ADesignation(DesignationKind.Email.Token, "jean.dupont@example.fr"),
        new ADesignation(DesignationKind.PersonName.Token, "Jean Dupont"),
      ],
      Rights = [nameof(DataSubjectRight.Access)],
      IdentityDeclaration = nameof(IdentityDeclaration.OperatorAttested),
      VerificationMethod = nameof(IdentityVerificationMethod.PersonalRecognition),
      SignedBy = "Claire Martin",
    });

    // Le dépôt mène au dossier qu'il vient d'ouvrir, et non à la file : c'est là que ce qui manque
    // se lit. La redirection fait aussi qu'un rechargement ne dépose pas deux fois.
    deposited.StatusCode.ShouldBe(HttpStatusCode.Found);

    var screen = await _surface.ReadTextAsync(deposited.Headers.Location!.ToString());

    screen.ShouldContain("Le dossier");
    screen.ShouldContain(DataSubjectRight.Access.Name);
    screen.ShouldContain(IdentityDeclaration.OperatorAttested.FrenchLabel);
    screen.ShouldContain(IdentityVerificationMethod.PersonalRecognition.FrenchLabel);
  }

  /// <summary>
  /// <b>La motivation est réclamée, et la route n'est jamais barrée.</b> Un <c>Access</c> ouvert sous
  /// <c>Unverified</c> sans que personne l'ait pesé <b>entre quand même</b> — et l'écran du dossier le
  /// dit, en toutes lettres, tant que la motivation manque.
  /// </summary>
  [Fact]
  public async Task ClaimsTheMotivationWithoutEverBarringTheRoad()
  {
    var deposited = await _surface.DepositAsync(new ADeposit
    {
      Designations = [new ADesignation(DesignationKind.Email.Token, "sans.motivation@example.fr")],
      Rights = [nameof(DataSubjectRight.Access)],
      IdentityDeclaration = nameof(IdentityDeclaration.Unverified),
      // Personne ne l'a pesé : ni méthode, ni détail.
      VerificationMethod = string.Empty,
      SignedBy = "Claire Martin",
    });

    // LA ROUTE N'EST PAS BARRÉE : le dossier est ouvert, et le mois de l'art. 12.3 court dans le
    // service plutôt que dans une boîte aux lettres.
    deposited.StatusCode.ShouldBe(HttpStatusCode.Found);

    // ET LA MOTIVATION EST RÉCLAMÉE : la faiblesse reste visible, au bandeau comme sous le droit qui
    // l'exige.
    var screen = await _surface.ReadTextAsync(deposited.Headers.Location!.ToString());

    screen.ShouldContain("Motivation réclamée");
    screen.ShouldContain("Motivation réclamée pour ce droit");
  }

  /// <summary>
  /// <b>Ce que la demande réclame peut être écrit ensuite, depuis le dossier lui-même.</b> Une
  /// exigence qu'on ne peut pas satisfaire cesse d'être lue : le bandeau permanent qu'on apprend à
  /// ne plus voir aurait rendu la faiblesse invisible — l'inverse exact de ce qu'il est là pour
  /// faire.
  /// </summary>
  [Fact]
  public async Task LetsTheOperatorSatisfyTheClaimedMotivationFromTheCaseItself()
  {
    var deposited = await _surface.DepositAsync(new ADeposit
    {
      Designations = [new ADesignation(DesignationKind.Email.Token, "pese.plus.tard@example.fr")],
      Rights = [nameof(DataSubjectRight.Access)],
      IdentityDeclaration = nameof(IdentityDeclaration.Unverified),
      VerificationMethod = string.Empty,
    });

    var address = deposited.Headers.Location!.ToString();

    (await _surface.ReadTextAsync(address)).ShouldContain("Motivation réclamée");

    var opened = CaseId.From(Guid.Parse(address.Split('/')[^1]));

    var weighed = await _surface.MotivateAsync(
      opened,
      nameof(IdentityVerificationMethod.CallbackOnKnownContact),
      "Rappelée sur le numéro déjà enregistré au contrat.",
      "Claire Martin");

    weighed.StatusCode.ShouldBe(HttpStatusCode.Found);

    var after = await _surface.ReadTextAsync(address);

    after.ShouldNotContain("Motivation réclamée");
    after.ShouldContain(IdentityVerificationMethod.CallbackOnKnownContact.FrenchLabel);

    // ET LE DROIT N'A PAS BOUGÉ : l'accès reste ouvert sous l'identité de sa naissance. Ce qu'on
    // pèse aujourd'hui ne rend pas rétroactivement propre ce qui a été fait sur la foi de rien.
    after.ShouldContain($"ouvert sous l'identité « {IdentityDeclaration.Unverified.FrenchLabel} »");
  }

  /// <summary>
  /// <b><c>None</c> est une réponse, et elle éteint la demande</b> — là où l'absence ne l'éteint pas.
  /// Sans la valeur laide, l'opérateur pressé cocherait la valeur propre, et le service enregistrerait
  /// un faux au lieu d'un aveu.
  /// </summary>
  [Fact]
  public async Task TakesTheAdmissionThatNothingWasDoneAsAnAnswer()
  {
    var deposited = await _surface.DepositAsync(new ADeposit
    {
      Designations = [new ADesignation(DesignationKind.Email.Token, "aucune.methode@example.fr")],
      Rights = [nameof(DataSubjectRight.Access)],
      IdentityDeclaration = nameof(IdentityDeclaration.Unverified),
      VerificationMethod = nameof(IdentityVerificationMethod.None),
      SignedBy = "Claire Martin",
    });

    var screen = await _surface.ReadTextAsync(deposited.Headers.Location!.ToString());

    screen.ShouldContain(IdentityVerificationMethod.None.FrenchLabel);
    screen.ShouldNotContain("Motivation réclamée");
  }

  /// <summary>
  /// <b>La date de réception réelle est déclarable</b> ; à défaut, <c>J+9 (défaut)</c> s'affiche et
  /// se consigne <b>comme un défaut</b>. Le service nomme la règle appliquée, jamais la date qu'elle
  /// a produite — qui se lirait comme un fait que personne n'a déclaré.
  /// </summary>
  [Fact]
  public async Task DeclaresTheRealReceptionDateOrNamesTheDefaultAsADefault()
  {
    var declared = DateTimeOffset.UtcNow.AddDays(-12);

    var withDate = await _surface.DepositAsync(new ADeposit
    {
      Designations = [new ADesignation(DesignationKind.Email.Token, "date.declaree@example.fr")],
      Rights = [nameof(DataSubjectRight.Access)],
      ReceivedOn = declared.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
      VerificationMethod = nameof(IdentityVerificationMethod.None),
    });

    var declaredScreen = await _surface.ReadTextAsync(withDate.Headers.Location!.ToString());

    declaredScreen.ShouldContain(
      declared.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture));
    declaredScreen.ShouldNotContain("(défaut)");

    var withoutDate = await _surface.DepositAsync(new ADeposit
    {
      Designations = [new ADesignation(DesignationKind.Email.Token, "date.inconnue@example.fr")],
      Rights = [nameof(DataSubjectRight.Access)],
      ReceivedOn = string.Empty,
      VerificationMethod = nameof(IdentityVerificationMethod.None),
    });

    var defaultedScreen = await _surface.ReadTextAsync(withoutDate.Headers.Location!.ToString());

    defaultedScreen.ShouldContain($"J+{ReceptionDate.DaysHeldAlreadyRunByDefault} (défaut)");
  }

  /// <summary>
  /// <b>Une demande n'exerçant aucun droit entre quand même</b>, et le dossier le dit comme un fait
  /// plutôt que comme une saisie inachevée : aucun verdict <c>OutOfScope</c> ne reste orphelin, et
  /// aucune machine ne prononce seule une fin de non-recevoir.
  /// </summary>
  [Fact]
  public async Task LetsInARequestThatExercisesNoRightAtAll()
  {
    var deposited = await _surface.DepositAsync(new ADeposit
    {
      Designations = [new ADesignation(DesignationKind.Email.Token, "aucun.droit@example.fr")],
      Rights = [],
      VerificationMethod = nameof(IdentityVerificationMethod.None),
    });

    deposited.StatusCode.ShouldBe(HttpStatusCode.Found);

    var screen = await _surface.ReadTextAsync(deposited.Headers.Location!.ToString());

    screen.ShouldContain("Aucun droit n'est reconnu à ce dossier à ce jour");
  }

  /// <summary>
  /// <c>OutOfScope</c> <b>n'est pas offert à cocher</b>, et son absence est structurelle : ce n'est
  /// pas un droit qu'on réclame, c'est le constat qu'aucun ne l'a été. Il se prononce à la clôture,
  /// sous la signature d'un humain.
  /// </summary>
  [Fact]
  public async Task OffersNoBoxForTheVerdictThatNoRightWasRecognised()
  {
    var screen = await _surface.ReadWithoutStyleAsync(OperatorSurface.Deposit);

    screen.ShouldNotContain($"value=\"{DataSubjectRight.OutOfScope.Name}\"");
  }

  /// <summary>
  /// <b>Aucun emplacement pour une pièce jointe sur ce canal non plus</b> (CEPD § 79). Ni champ de
  /// fichier, ni case pour un numéro de pièce : le <c>EvidenceLog</c> ne garde jamais qu'un <b>fait</b> de
  /// vérification, jamais la pièce.
  /// </summary>
  [Fact]
  public async Task OffersNoPlaceForAnIdentityDocumentOnThisChannelEither()
  {
    var screen = await _surface.ReadWithoutStyleAsync(OperatorSurface.Deposit);

    // La forme du formulaire, et non la discipline de qui le remplit : un champ ajouté par mégarde
    // fait échouer ce test avant qu'une pièce n'ait pu entrer.
    screen.ShouldNotContain("type=\"file\"");
    screen.ShouldNotContain("multipart/form-data");
    screen.ShouldNotContain("enctype");

    // Et l'écran le dit à qui le remplit, plutôt que de compter sur son intuition.
    var text = await _surface.ReadTextAsync(OperatorSurface.Deposit);

    text.ShouldContain("N'attachez aucune pièce d'identité");
  }

  /// <summary>
  /// Une date de réception <b>dans l'avenir</b> est refusée, et c'est le seul refus de ce champ : le
  /// mois de l'art. 12.3 court <em>avant</em> le dépôt, et une telle date rendrait une échéance
  /// fausse et un dépassement invisible. L'écran nomme le refus plutôt que de corriger en silence.
  /// </summary>
  [Fact]
  public async Task RefusesAReceptionDateInTheFutureAndSaysWhy()
  {
    var deposited = await _surface.DepositAsync(new ADeposit
    {
      Designations = [new ADesignation(DesignationKind.Email.Token, "date.future@example.fr")],
      Rights = [nameof(DataSubjectRight.Access)],
      ReceivedOn = DateTimeOffset.UtcNow.AddDays(3)
        .ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
      VerificationMethod = nameof(IdentityVerificationMethod.None),
    });

    deposited.StatusCode.ShouldBe(HttpStatusCode.OK);

    var screen = await deposited.Content.ReadAsStringAsync();

    screen.ShouldContain("dans l&#x27;avenir");
  }

  /// <summary>
  /// <b>Un détail sans méthode ne fabrique pas une motivation.</b> La méthode est ce qui se compte
  /// et ce qui survit ; une motivation de prose seule serait invisible au contrôle et mourrait à la
  /// clôture — c'est-à-dire n'aurait jamais existé.
  /// </summary>
  [Fact]
  public async Task RefusesAProseThatNoCountableMethodAccompanies()
  {
    var deposited = await _surface.DepositAsync(new ADeposit
    {
      Designations = [new ADesignation(DesignationKind.Email.Token, "prose.seule@example.fr")],
      Rights = [nameof(DataSubjectRight.Access)],
      VerificationMethod = string.Empty,
      MotivationDetail = "Je l'ai rappelée et elle a su me dire son numéro de contrat.",
    });

    deposited.StatusCode.ShouldBe(HttpStatusCode.OK);

    var screen = await _surface.ReadTextAsync(OperatorSurface.Deposit);

    // La méthode « aucune » reste offerte : le refus dit qu'il faut en choisir une, jamais qu'il
    // faut en inventer une propre.
    screen.ShouldContain(IdentityVerificationMethod.None.FrenchLabel);
  }

  /// <summary>
  /// <b>Le dépôt est signé, et sans nom il n'a pas lieu.</b> C'est le seul champ obligatoire de
  /// l'écran : il n'est pas une donnée du dossier mais la <b>signature du geste</b>, et sans lui la
  /// preuve dirait que personne n'a déposé.
  /// </summary>
  [Fact]
  public async Task RefusesADepositThatNobodySigned()
  {
    var deposited = await _surface.DepositAsync(new ADeposit
    {
      Designations = [new ADesignation(DesignationKind.Email.Token, "sans.nom@example.fr")],
      Rights = [nameof(DataSubjectRight.Access)],
      VerificationMethod = nameof(IdentityVerificationMethod.None),
      SignedBy = string.Empty,
    });

    deposited.StatusCode.ShouldBe(HttpStatusCode.OK);

    var screen = await deposited.Content.ReadAsStringAsync();

    screen.ShouldContain("Le nom du signataire");
  }
}
