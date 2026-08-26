using System.Globalization;

namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Ce que le cache d'aperçus a <b>encore</b> à montrer pour un rapport, et ce qu'il en dit quand il
/// n'a plus rien : les valeurs vivantes et leur décompte, ou la phrase qui explique leur absence.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il n'y a pas de troisième forme dans la case d'une colonne, et ce type est ce qui la tient
/// dehors.</b> Un aperçu reste « soit des valeurs, soit une raison » — voir
/// <see cref="ColumnPreview"/>. L'expiration ne remplit donc <b>aucune</b> case : elle se dit à
/// l'échelle du <b>rapport</b>, une fois, et le bloc d'aperçu quitte l'écran entièrement. Écrire
/// « valeurs expirées » à la place des valeurs aurait fabriqué la troisième forme dans le champ
/// même que la clause protège.
/// </para>
/// <para>
/// ⚠️ <b>Les raisons d'absence ne sont pas ici, et elles restent à l'écran après l'expiration.</b>
/// Elles sont enregistrées sur la <see cref="ScreenedColumn"/> : ce sont des propriétés de la
/// colonne, pas des aperçus. Un écran qui les retirerait avec les valeurs rendrait un rapport
/// d'une heure <b>moins</b> renseigné que son archive, ce qui est le mode de panne exact que
/// <see cref="PreviewAbsenceReason"/> existe pour ne pas avoir.
/// </para>
/// <para>
/// ⚠️ <b>La phrase de l'après est vraie sans réserve, et c'est ce qui autorise « ils ne reviendront
/// pas ».</b> Aucun geste ne rend ses aperçus à <b>ce</b> rapport : relancer un scan en produit un
/// autre et fait reculer celui-ci à l'archive. Dire « relancez un scan » aurait proposé, pour
/// récupérer cinq valeurs, de faire perdre des jours d'arbitrage.
/// </para>
/// </remarks>
public sealed class ScreeningPreviews
{
  /// <summary>
  /// Ce que l'<c>Operator</c> lit quand les aperçus ont expiré. <b>Une phrase de rapport, jamais de
  /// colonne</b>, et elle dit du même souffle que l'arbitrage reste ouvert.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La seconde moitié est la moitié utile.</b> Sans elle, un <c>Operator</c> devant une fiche
  /// dont l'aperçu a disparu lit une <b>péremption</b> — et la seule issue qu'il imaginerait,
  /// relancer un scan, détruirait le travail déjà tranché. Le motif de forme reste lisible, il
  /// reste arbitrable, et l'écran le dit plutôt que de le laisser deviner.
  /// </remarks>
  public const string ExpiredStatement =
    "Les aperçus de ce rapport ont expiré ; ils ne reviendront pas pour ce rapport. Vous pouvez "
    + "arbitrer quand même : le motif de chaque colonne signalée reste lisible, et les raisons de "
    + "n'avoir aucune valeur à montrer sont toujours affichées.";

  /// <summary>
  /// Ce que l'<c>Operator</c> lit quand le service a redémarré depuis le scan.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est dit, et ce n'est pas réparé.</b> Les valeurs lues vivent en mémoire du processus et
  /// nulle part ailleurs : les faire survivre à un redémarrage demanderait de les <b>écrire</b>,
  /// c'est-à-dire de faire du service un détenteur durable de données personnelles du client. Le
  /// prix est sans commune mesure avec le gain, et il est refusé nommément — voir <c>Rien de réel
  /// ne reste</c>.
  /// </remarks>
  public const string ClearedByRestartStatement =
    "Les aperçus de ce rapport ont été effacés par un redémarrage du service ; ils ne reviendront "
    + "pas pour ce rapport. Vous pouvez arbitrer quand même : le motif de chaque colonne signalée "
    + "reste lisible, et les raisons de n'avoir aucune valeur à montrer sont toujours affichées.";

  private static readonly IReadOnlyDictionary<ColumnIdentity, ColumnPreview> Nothing =
    new Dictionary<ColumnIdentity, ColumnPreview>();

  private ScreeningPreviews(
    PreviewAvailability availability,
    IReadOnlyDictionary<ColumnIdentity, ColumnPreview> previews,
    TimeSpan remaining)
  {
    Availability = availability;
    Previews = previews;
    Remaining = remaining;
  }

  /// <summary>
  /// Rien n'a jamais été prélevé pour ce rapport — il a été <b>collé</b>. ⚠️ <b>Ce n'est pas une
  /// expiration</b>, et l'écran ne dit rien du tout : annoncer que « les aperçus ont expiré » sur un
  /// relevé collé affirmerait qu'un prélèvement a eu lieu, très exactement comme quatre comptes à
  /// zéro y inventeraient une incomplétude.
  /// </summary>
  public static ScreeningPreviews NeverTaken { get; } =
    new(PreviewAvailability.NeverTaken, Nothing, TimeSpan.Zero);

  /// <summary>La durée est écoulée : plus aucune valeur, et la phrase du rapport le dit.</summary>
  public static ScreeningPreviews Expired { get; } =
    new(PreviewAvailability.Expired, Nothing, TimeSpan.Zero);

  /// <summary>Le processus a redémarré depuis le scan : les valeurs n'existent plus nulle part.</summary>
  public static ScreeningPreviews ClearedByRestart { get; } =
    new(PreviewAvailability.ClearedByRestart, Nothing, TimeSpan.Zero);

  /// <summary>Dans quel état le cache laisse ce rapport.</summary>
  public PreviewAvailability Availability { get; }

  /// <summary>Les aperçus vivants, par colonne — <b>vides</b> dans les trois autres états.</summary>
  public IReadOnlyDictionary<ColumnIdentity, ColumnPreview> Previews { get; }

  /// <summary>
  /// Ce qu'il reste à vivre aux aperçus <b>si l'écran n'est plus rouvert</b>, et c'est cette
  /// lecture-là qui est honnête : le glissant se réarme, le décompte ne promet donc pas une
  /// échéance, il dit un délai.
  /// </summary>
  public TimeSpan Remaining { get; }

  /// <summary>Ce rapport a-t-il des valeurs à montrer, à cet instant ?</summary>
  public bool CarriesValues => Availability == PreviewAvailability.Live && Previews.Count > 0;

  /// <summary>
  /// Ce que l'écran dit <b>du rapport</b> à propos de ses aperçus, ou <c>null</c> quand il n'y a
  /// rien à en dire — le relevé a été collé, et aucun prélèvement n'a jamais eu lieu.
  /// </summary>
  public string? Statement
  {
    get
    {
      if (Availability == PreviewAvailability.Live)
      {
        return $"Les aperçus de ce rapport disparaîtront dans {Countdown}.";
      }

      if (Availability == PreviewAvailability.Expired)
      {
        return ExpiredStatement;
      }

      return Availability == PreviewAvailability.ClearedByRestart
        ? ClearedByRestartStatement
        : null;
    }
  }

  /// <summary>
  /// Le délai restant <b>tel qu'une phrase le dit</b> — « 1 h 47 ».
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Heures et minutes, jamais de secondes.</b> Un décompte à la seconde sur une page qui ne
  /// se rafraîchit pas — il n'y a aucun JavaScript ici — serait faux dès la seconde suivante, et
  /// faux avec une précision qui le ferait croire.
  /// </remarks>
  public string Countdown
  {
    get
    {
      if (Remaining < TimeSpan.FromMinutes(1))
      {
        return "moins d'une minute";
      }

      var hours = (int)Remaining.TotalHours;

      return hours == 0
        ? string.Create(CultureInfo.InvariantCulture, $"{Remaining.Minutes} min")
        : string.Create(CultureInfo.InvariantCulture, $"{hours} h {Remaining.Minutes:00}");
    }
  }

  /// <summary>
  /// Les aperçus vivants d'un rapport, et ce qu'il leur reste à vivre.
  /// </summary>
  /// <param name="previews">Un aperçu par colonne prélevée.</param>
  /// <param name="remaining">Le délai restant, tel que le cache vient de le réarmer.</param>
  /// <exception cref="ArgumentNullException"><paramref name="previews"/> est absent.</exception>
  public static ScreeningPreviews Live(
    IReadOnlyDictionary<ColumnIdentity, ColumnPreview> previews,
    TimeSpan remaining)
  {
    ArgumentNullException.ThrowIfNull(previews);

    return new ScreeningPreviews(PreviewAvailability.Live, previews, remaining);
  }

  /// <summary>
  /// L'aperçu d'une colonne, ou <c>null</c> — parce qu'elle n'a pas été prélevée, ou parce que les
  /// aperçus de ce rapport ne sont plus là.
  /// </summary>
  /// <param name="column">La colonne dont on cherche l'aperçu.</param>
  public ColumnPreview? Of(ColumnIdentity column)
  {
    return column is not null && Previews.TryGetValue(column, out var preview) ? preview : null;
  }
}

/// <summary>
/// Dans quel état le cache d'aperçus laisse un rapport. <b>Quatre états, et l'écran ne dit pas la
/// même chose dans trois d'entre eux</b> — il ne dit rien du tout dans le quatrième.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b><see cref="NeverTaken"/> et <see cref="ClearedByRestart"/> ne se confondent pas, et c'est
/// tout l'objet du type.</b> Les deux rendent un cache vide ; l'un est un relevé <b>collé</b>, où
/// aucun prélèvement n'a jamais eu lieu et où il n'y a donc rien à annoncer, l'autre un rapport
/// <b>scanné</b> dont les valeurs ont été effacées par un redémarrage — un fait à dire. Fondus en un
/// seul, l'écran aurait dû choisir entre annoncer une perte à qui n'a rien perdu et taire une perte
/// à qui vient d'en subir une.
/// </para>
/// <para>
/// ⚠️ <b><see cref="Expired"/> n'est pas une cinquième <see cref="PreviewAbsenceReason"/>.</b> Une
/// raison d'absence dit pourquoi <b>une colonne</b> n'a rien à montrer, et elle est enregistrée ;
/// l'expiration porte sur le <b>rapport</b> entier, elle n'est enregistrée nulle part, et elle
/// n'entre dans aucun des quatre comptes de la <see cref="IncompletenessClause"/>. Les ranger
/// ensemble aurait fait grandir de un une énumération dont la fermeture est la promesse.
/// </para>
/// </remarks>
public sealed class PreviewAvailability : SmartEnum<PreviewAvailability>
{
  /// <summary>Le relevé a été collé : rien n'a jamais été prélevé, et il n'y a rien à en dire.</summary>
  public static readonly PreviewAvailability NeverTaken = new(nameof(NeverTaken), 1);

  /// <summary>Les valeurs sont là, et le décompte dit combien de temps encore.</summary>
  public static readonly PreviewAvailability Live = new(nameof(Live), 2);

  /// <summary>La durée est écoulée — glissante ou absolue, l'écran ne distingue pas les deux.</summary>
  /// <remarks>
  /// ⚠️ <b>Une seule phrase pour les deux bornes, délibérément.</b> Savoir laquelle des deux a mordu
  /// ne fait rien faire à l'<c>Operator</c> : dans les deux cas les valeurs ne reviendront pas pour
  /// ce rapport, et il lui reste très exactement le même geste.
  /// </remarks>
  public static readonly PreviewAvailability Expired = new(nameof(Expired), 3);

  /// <summary>Le service a redémarré depuis le scan, et les valeurs vivaient dans sa mémoire.</summary>
  public static readonly PreviewAvailability ClearedByRestart = new(nameof(ClearedByRestart), 4);

  private PreviewAvailability(string name, int value)
    : base(name, value)
  {
  }
}
