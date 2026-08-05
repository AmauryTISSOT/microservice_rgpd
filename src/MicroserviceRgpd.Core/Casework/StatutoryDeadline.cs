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
/// <b>Le dénominateur est d'un mois, et il ne bouge pas ici.</b> La prolongation de deux mois de
/// l'art. 12.3 est une <c>ExtensionDeclaration</c> ; elle déplacera cette échéance par un calcul sur
/// la date de sa déclaration, jamais par une propriété de l'objet.
/// </para>
/// </remarks>
public sealed record StatutoryDeadline
{
  private StatutoryDeadline(DateTimeOffset on)
  {
    On = on;
  }

  /// <summary>Le jour où le mois de l'art. 12.3 est échu.</summary>
  public DateTimeOffset On { get; }

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
  /// <exception cref="ArgumentNullException"><paramref name="reception"/> est absent.</exception>
  public static StatutoryDeadline Of(ReceptionDate reception)
  {
    ArgumentNullException.ThrowIfNull(reception);

    return new StatutoryDeadline(reception.On.AddMonths(1));
  }

  /// <summary>
  /// Le délai est-il dépassé à cet instant ? Faux à l'échéance même : le dernier jour est dû.
  /// </summary>
  /// <param name="instant">L'instant où quelqu'un regarde — jamais lu sur une horloge d'ici.</param>
  public bool IsOverrunAt(DateTimeOffset instant) => instant > On;
}
