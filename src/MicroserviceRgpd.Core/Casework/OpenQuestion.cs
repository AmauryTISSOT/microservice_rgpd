namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// Une question <b>datée</b> qui attend une réponse, accrochée à ce qu'elle empêche <b>réellement</b>
/// d'avancer — le <see cref="Case"/> pour la désignation de la personne, un <see cref="Claim"/> pour
/// le contenu d'un droit.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle n'arrête jamais le délai de l'art. 12.3, et ne barre jamais la route.</b> La suspension du
/// mois n'a aucune base textuelle : le compteur court pendant qu'on cherche, et une question ouverte
/// est ce que le contrôle doit voir plutôt qu'un état qui excuserait le temps passé.
/// </para>
/// <para>
/// ⚠️ <b>Elle affiche la date à laquelle elle a été posée, jamais « sans réponse depuis N jours ».</b>
/// Aucun nombre du droit ne fonde N, un seuil réglable serait une case à laisser pourrir de plus, et
/// trier sur lui ferait passer devant un compteur sans force juridique. On montre le fait,
/// l'<c>Operator</c> juge.
/// </para>
/// <para>
/// <b>Celle que ce lot fait naître est celle du dossier</b> : <b>tous</b> les <c>Locate</c> ont rendu
/// zéro, et la désignation ne suffit donc pas. Six zéros ne doivent pas se lire « cette personne
/// n'est pas chez nous » — c'est le seul rempart contre cette lecture, et il est daté.
/// </para>
/// <para>
/// <b>Elle n'est pas un agrégat, et n'a aucun dépôt.</b> Elle naît, se lit et meurt par la racine.
/// </para>
/// </remarks>
public sealed class OpenQuestion
{
  internal OpenQuestion(OpenQuestionSubject subject, DateTimeOffset askedOn)
  {
    Subject = subject;
    AskedOn = askedOn;
  }

  /// <summary>Le constructeur qu'EF Core emprunte pour rematérialiser une ligne. Il ne rejoue aucun invariant.</summary>
  private OpenQuestion()
  {
    Subject = OpenQuestionSubject.Designation;
  }

  /// <summary>Ce sur quoi elle porte, dans un vocabulaire fermé.</summary>
  public OpenQuestionSubject Subject { get; private set; }

  /// <summary>
  /// Le jour où elle a été posée — <b>la seule chose que l'écran en dise</b>. Elle ne se pose
  /// qu'une fois : reposer la même question tous les matins ferait d'une question ouverte un bruit
  /// quotidien, et l'écart entre cette date et aujourd'hui est précisément ce qui se lit.
  /// </summary>
  public DateTimeOffset AskedOn { get; private set; }
}

/// <summary>
/// Ce sur quoi une <see cref="OpenQuestion"/> porte. Vocabulaire <b>fermé</b> : une question qu'aucune
/// valeur ne nomme n'entre pas par une colonne de prose libre.
/// </summary>
/// <remarks>
/// <b>Une seule valeur aujourd'hui, et c'est la raison d'être du type.</b> La question du contenu d'un
/// droit s'accrochera à un <see cref="Claim"/> le jour où <c>Read</c> sera exercé, et elle entrera
/// ici sans que la question d'aujourd'hui devienne indiscernable de celle de demain — même mécanique
/// que <c>SignerVerification</c>.
/// </remarks>
public sealed class OpenQuestionSubject : SmartEnum<OpenQuestionSubject>
{
  /// <summary>
  /// La <b>désignation ne suffit pas</b> : tous les <c>Locate</c> du dossier ont rendu zéro
  /// rattachement. Elle se pose sur le dossier, et non sur un droit : c'est la personne qu'on ne
  /// sait pas désigner, pas la réponse due sur un droit qui bloque.
  /// </summary>
  public static readonly OpenQuestionSubject Designation =
    new(nameof(Designation), 0, "la désignation de la personne");

  private OpenQuestionSubject(string name, int value, string frenchLabel)
    : base(name, value)
  {
    FrenchLabel = frenchLabel;
  }

  /// <summary>Le libellé destiné à l'<c>Operator</c>. Le français reste hors des identifiants.</summary>
  public string FrenchLabel { get; }
}
