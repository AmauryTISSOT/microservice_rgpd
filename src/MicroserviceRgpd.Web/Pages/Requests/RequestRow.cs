using System.Globalization;
using MicroserviceRgpd.Core.Requests;
using MicroserviceRgpd.UseCases.Requests.ReadDataSubjectRequests;

namespace MicroserviceRgpd.Web.Pages.Requests;

/// <summary>
/// La <b>ligne d'une demande</b> dans le tableau, chaque cellule sous le libellé que le serveur lui
/// donne. La vue partielle <c>_RequestRow</c> la rend, pour le tableau au chargement comme pour la
/// demande qu'on vient de créer.
/// </summary>
/// <remarks>
/// ⚠️ <b>Tous les libellés sont rendus par le serveur</b> — tirets, dates, « Oui/Non », droit,
/// auteur, statut, signalement de la date limite, jusqu'à la phrase qui confirme la suppression. Le
/// script de l'écran anime les lignes ; il n'en écrit aucun mot, et ne recalcule aucun signalement.
/// La ligne porte aussi l'identifiant de sa demande, que la suppression envoie, le statut et le
/// signalement sous leur <b>nom canonique</b>, que la feuille de style colore, et ses <b>clés de
/// tri</b>, par lesquelles le script ordonne les lignes et place celle d'une demande qu'on vient de
/// créer, et enfin le <b>nom replié</b> de la personne, ce qu'elle porte <b>pour la fiche</b>.
///
/// Elle porte enfin ce que le <b>crayon</b> offre — <c>ModificationAllowed</c> — et ce que son
/// infobulle dit — <c>ModificationTooltip</c> : son libellé quand la modification est permise, la
/// raison de son extinction quand la demande est close. Les deux se calculent ici, à partir du
/// statut ; le gabarit ne teste rien.
/// </remarks>
public sealed record RequestRow(
  string Id,
  string DeletionConfirmation,
  string Email,
  string LastName,
  string FirstName,
  string ReceivedOn,
  string ResponseDeadline,
  RequestRow.Signal? DeadlineSignal,
  string IdentityVerified,
  string Right,
  string CreatedAt,
  string CreatedBy,
  string StatusLabel,
  string StatusName,
  bool ModificationAllowed,
  string ModificationTooltip,
  RequestRow.SearchableText Searchable,
  RequestRow.SortKeys Sort,
  RequestRow.Sheet ForSheet)
{
  /// <summary>
  /// Ce que le tri compare sur la ligne : la date de réception en ISO (<c>aaaa-mm-jj</c>), puis,
  /// pour départager deux demandes reçues le même jour, l'instant d'enregistrement en ISO, en UTC.
  /// Ce sont aussi les clés par lesquelles le script place la ligne d'une demande qu'on vient de
  /// créer, dans le sens du tri sélectionné.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Le script les compare comme des textes</b> : l'ordre des textes n'est celui des dates que
  /// si chacun s'écrit toujours à la même largeur. L'instant porte donc ses six décimales et son
  /// <c>Z</c>, toujours — jamais un décalage, jamais des décimales en moins.
  /// </para>
  /// <para>
  /// ⚠️ <b>Six décimales, et non sept : c'est la microseconde que la table retient.</b> L'instant que
  /// la demande tient en mémoire porte jusqu'au dixième de microseconde ; relu en base, il l'a
  /// perdu. La ligne du 201 et celle du tableau doivent être la même, au caractère près.
  /// </para>
  /// </remarks>
  public sealed record SortKeys(string ReceivedOn, string CreatedAt);

  /// <summary>
  /// Ce que la recherche parcourt sur la ligne : l'email, le nom et le prénom <b>tels
  /// qu'enregistrés</b>, vides s'ils sont absents. Le script les normalise, comme il normalise la saisie.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Ce ne sont pas les libellés des cellules</b> : le « — » d'une valeur absente n'est pas un
  /// texte que la personne a donné, et une recherche ne doit pas le trouver.
  /// </remarks>
  public sealed record SearchableText(string Email, string LastName, string FirstName);

  /// <summary>
  /// Ce que la ligne porte <b>pour la fiche</b> : le <b>nom replié</b> de la personne — « Prénom
  /// Nom », à défaut l'email seul —, celui que le titre de la fiche annonce.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Ce nom n'est jamais vide et n'est jamais « — »</b> : l'<c>Identification</c> d'une demande
  /// garantit qu'il reste toujours l'email, ou le nom et le prénom. Il se replie par
  /// <see cref="PersonOf"/>, la <b>seule</b> écriture de la règle — c'est elle aussi que la phrase de
  /// suppression appelle, pour que les deux ne nomment jamais la même personne autrement.
  /// </remarks>
  public sealed record Sheet(string Person);

  /// <summary>
  /// Le signalement de la date limite, s'il y en a un : son <b>nom canonique</b>, que la cellule
  /// porte pour que la feuille de style la colore, et son libellé, qui se lit après la date.
  /// </summary>
  public sealed record Signal(string Name, string Label);

  /// <summary>Ce qu'affiche une cellule dont la valeur est absente : une absence, pas une cellule mal rendue.</summary>
  private const string Absent = "—";

  /// <summary>
  /// Ce que dit l'infobulle du crayon quand la modification est offerte. ⚠️ <b>C'est le libellé
  /// accessible du bouton</b>, que le gabarit écrit de son côté : les deux se lisent ensemble, et
  /// doivent rester le même mot.
  /// </summary>
  private const string ModificationOffered = "Modifier la demande";

  /// <summary>
  /// Ce que dit l'infobulle du crayon d'une demande close — Terminée ou Annulée. Le bouton est
  /// éteint ; l'<c>Operator</c> doit lire <b>pourquoi</b>, et non croire à une panne.
  /// </summary>
  private const string ModificationRefused = "Une demande close ne peut plus être modifiée";

  private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

  /// <summary>La ligne d'une demande enregistrée, telle qu'elle se lit <paramref name="todayInParis"/>.</summary>
  /// <param name="request">La demande.</param>
  /// <param name="todayInParis">Aujourd'hui à Paris — voir <see cref="ParisCalendar"/>. C'est contre lui que la date limite se signale.</param>
  /// <exception cref="ArgumentNullException"><paramref name="request"/> est absente.</exception>
  public static RequestRow Of(RecordedDataSubjectRequest request, DateOnly todayInParis)
  {
    ArgumentNullException.ThrowIfNull(request);

    var signal = Core.Requests.DeadlineSignal.Of(request.Status, request.ResponseDeadline, todayInParis);
    var modificationAllowed = request.Status == RequestStatus.InProgress;

    return new RequestRow(
      request.Id.Value.ToString(),
      DeletionConfirmationOf(request),
      request.Email?.Value ?? Absent,
      request.LastName?.Value ?? Absent,
      request.FirstName?.Value ?? Absent,
      Day(request.ReceivedOn),
      Day(request.ResponseDeadline),
      signal is null ? null : new Signal(signal.Name, signal.FrenchLabel),
      request.IdentityVerified ? "Oui" : "Non",
      Capitalized(request.Right.FrenchLabel),
      ParisCalendar.InParis(request.CreatedAt).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
      request.CreatedBy == DataSubjectRequest.OperatorAuthor ? "Opérateur" : request.CreatedBy,
      request.Status.FrenchLabel,
      request.Status.Name,
      modificationAllowed,
      modificationAllowed ? ModificationOffered : ModificationRefused,
      new SearchableText(
        request.Email?.Value ?? string.Empty,
        request.LastName?.Value ?? string.Empty,
        request.FirstName?.Value ?? string.Empty),
      new SortKeys(
        request.ReceivedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        request.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture)),
      new Sheet(PersonOf(request)));
  }

  /// <summary>Un jour en <c>jj/mm/aaaa</c>.</summary>
  private static string Day(DateOnly day) => day.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

  /// <summary>
  /// « La demande de {Prénom} {Nom} ({email}) sera définitivement supprimée. Cette action est
  /// irréversible. » — le prénom et le nom omis s'ils manquent, l'email entre parenthèses quand un
  /// nom l'accompagne, <b>sans parenthèses quand il est seul</b>.
  /// </summary>
  /// <remarks>
  /// Elle nomme la personne comme l'<c>Operator</c> la lit sur la ligne : c'est elle qu'il regarde
  /// avant une suppression sans retour. « Supprimée », jamais « effacée » : l'effacement est un
  /// droit (art. 17), que la demande peut invoquer.
  /// </remarks>
  private static string DeletionConfirmationOf(RecordedDataSubjectRequest request)
  {
    var person = PersonOf(request);
    var email = request.Email?.Value;

    var whose = NameOf(request).Length == 0 || email is null ? person : $"{person} ({email})";

    return $"La demande de {whose} sera définitivement supprimée. Cette action est irréversible.";
  }

  /// <summary>
  /// Le <b>nom replié</b> de la personne : « Prénom Nom », celui des deux qui reste quand l'autre
  /// manque, et à défaut l'email seul.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La règle est écrite ici, et nulle part ailleurs</b> : la phrase de suppression et le nom
  /// que la ligne porte pour la fiche l'appellent tous deux. Écrite deux fois, elle finirait par
  /// diverger — la suppression nommerait un jour la personne autrement que la fiche, sur le même
  /// écran, pour la même demande.
  ///
  /// ⚠️ <b>Le résultat n'est jamais vide</b>, et n'est jamais « — » : l'<c>Identification</c> d'une
  /// demande exige un email, ou un nom et un prénom — quand le nom manque, l'email est là. Le repli
  /// final sur le vide est la branche que cette validation rend inatteignable.
  /// </remarks>
  private static string PersonOf(RecordedDataSubjectRequest request)
  {
    var name = NameOf(request);

    return name.Length > 0 ? name : request.Email?.Value ?? string.Empty;
  }

  /// <summary>
  /// « Prénom Nom », celui des deux qui reste quand l'autre manque, et le <b>vide</b> quand les deux
  /// manquent — sans espace parasite dans aucun des trois cas.
  /// </summary>
  /// <remarks>
  /// C'est l'absence de nom, et non une comparaison au texte de l'email, qui dit à la phrase de
  /// suppression que le nom replié est déjà l'email.
  /// </remarks>
  private static string NameOf(RecordedDataSubjectRequest request) =>
    string.Join(' ', new[] { request.FirstName?.Value, request.LastName?.Value }.OfType<string>());

  /// <summary>
  /// Le libellé du droit en tête de cellule : « droit d'accès » devient « Droit d'accès ». Le
  /// libellé du noyau partagé se lit dans une phrase, et sans son article du RGPD.
  /// </summary>
  private static string Capitalized(string label) =>
    string.Concat(char.ToUpper(label[0], French).ToString(), label[1..]);
}
