namespace MicroserviceRgpd.Core.Casework;

/// <summary>
/// L'échéance du mois de l'art. 12.3, et le <b>calcul</b> du dépassement.
/// </summary>
/// <remarks>
/// <para>
/// <b>Il n'existe aucun état « en retard ».</b> Le dépassement se calcule sur la
/// <see cref="ReceptionDate"/> à l'instant où l'<c>Operator</c> regarde : un état persisté ferait
/// dépendre la preuve de ce qu'une minuterie ait tourné, et un retard non détecté deviendrait un
/// retard inexistant. Ce type ne se stocke donc nulle part — il se calcule, on le lit, on l'oublie.
/// </para>
/// <para>
/// <b>Il ne rend aucun nombre de jours.</b> Ni écoulés, ni restants : la règle des chiffres
/// n'autorise que le dénombrement d'une chose présente que le service détient, et « sans réponse
/// depuis N jours » est exactement la forme que ce contexte refuse. On montre le fait — une date,
/// un dépassement —, l'<c>Operator</c> juge.
/// </para>
/// <para>
/// <b>Le dénominateur est d'un mois, et une <see cref="ExtensionDeclaration"/> seule le déplace.</b>
/// Le déplacement est un <b>calcul sur la date de la déclaration</b>, jamais une propriété écrite
/// quelque part : déclarée dans le mois, la prolongation porte le délai à trois mois ; déclarée
/// après, elle laisse l'échéance où elle est — un clic ne blanchit pas un dépassement déjà acquis.
/// </para>
/// </remarks>
public sealed record StatutoryDeadline
{
  private StatutoryDeadline(DateTimeOffset on, bool extended)
  {
    On = on;
    Extended = extended;
  }

  /// <summary>Le jour où le mois de l'art. 12.3 est échu.</summary>
  public DateTimeOffset On { get; }

  /// <summary>
  /// Une <see cref="ExtensionDeclaration"/> porte-t-elle cette échéance ? <b>C'est une lecture du
  /// calcul</b>, et non un état : une prolongation déclarée hors délai laisse ce drapeau faux tout
  /// en restant inscrite au dossier.
  /// </summary>
  public bool Extended { get; }

  /// <summary>
  /// L'échéance qui court depuis cette date de réception : <b>un mois de calendrier</b>, jamais
  /// trente jours — c'est le mot du texte, et trente jours ferait un délai plus court onze mois sur
  /// douze.
  /// </summary>
  /// <remarks>
  /// Le régime de la date — déclarée ou tenue pour défaut — <b>ne change rien au calcul</b> : un
  /// dossier dont personne n'a déclaré la date n'a droit à aucun délai de faveur. Le défaut se voit
  /// dans ce que l'écran en dit, jamais dans un calcul qui l'épargnerait.
  /// </remarks>
  /// <param name="reception">La date depuis laquelle le délai se compte.</param>
  /// <param name="extension">
  /// La prolongation déclarée au dossier, ou <c>null</c> si personne n'en a déclaré. ⚠️ Elle est
  /// <b>éprouvée ici</b> plutôt que crue : déclarée après l'échéance du mois, elle ne déplace rien.
  /// </param>
  /// <exception cref="ArgumentNullException"><paramref name="reception"/> est absent.</exception>
  public static StatutoryDeadline Of(ReceptionDate reception, ExtensionDeclaration? extension = null)
  {
    ArgumentNullException.ThrowIfNull(reception);

    var month = new StatutoryDeadline(reception.On.AddMonths(1), extended: false);

    // La frontière du « dans le mois » est celle du dépassement, et elle n'est écrite qu'une fois :
    // déclarée le jour même de l'échéance, la prolongation porte — le dernier jour est dû.
    if (extension is null || month.IsOverrunAt(extension.DeclaredOn))
    {
      return month;
    }

    // Trois mois de calendrier depuis la réception : c'est le dénominateur que l'art. 12.3 ouvre,
    // et il se compte depuis le même jour que le premier — jamais depuis la déclaration, qui ferait
    // dépendre le délai dû à la personne du jour où quelqu'un a cliqué.
    return new StatutoryDeadline(reception.On.AddMonths(3), extended: true);
  }

  /// <summary>
  /// Le délai est-il dépassé à cet instant ? Faux à l'échéance même : le dernier jour est dû.
  /// </summary>
  /// <param name="instant">L'instant où quelqu'un regarde — jamais lu sur une horloge d'ici.</param>
  public bool IsOverrunAt(DateTimeOffset instant) => instant > On;
}
