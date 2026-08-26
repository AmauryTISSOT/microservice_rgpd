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

  /// <summary>Un autre relevé a pris la place de celui-ci dans le cache, qui n'en tient qu'un.</summary>
  public static ScreeningPreviews Evicted { get; } =
    new(PreviewAvailability.Evicted, Nothing, TimeSpan.Zero);

  /// <summary>Dans quel état le cache laisse ce rapport.</summary>
  public PreviewAvailability Availability { get; }

  /// <summary>Les aperçus vivants, par colonne — <b>vides</b> dans les quatre autres états.</summary>
  public IReadOnlyDictionary<ColumnIdentity, ColumnPreview> Previews { get; }

  /// <summary>
  /// Ce qu'il reste à vivre aux aperçus <b>si l'écran n'est plus rouvert</b>, et c'est cette
  /// lecture-là qui est honnête : le glissant se réarme, le décompte ne promet donc pas une
  /// échéance, il dit un délai.
  /// </summary>
  public TimeSpan Remaining { get; }

  /// <summary>
  /// Ce rapport a-t-il, à cet instant, au moins <b>une valeur lue</b> à montrer ?
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Il compte des valeurs, et non des entrées.</b> Un aperçu qui porte une
  /// <see cref="PreviewAbsenceReason"/> occupe une entrée du dictionnaire sans rien donner à lire :
  /// compter les entrées aurait fait afficher, sur un scan dont chaque colonne a été refusée par les
  /// droits, la phrase qui met en garde contre le biais de valeurs qu'on n'a pas.
  /// </remarks>
  public bool CarriesValues =>
    Availability == PreviewAvailability.Live
    && Previews.Values.Any(preview => preview.CarriesValues);

  /// <summary>
  /// Ce que l'écran dit <b>du rapport</b> à propos de ses aperçus, ou <c>null</c> quand il n'y a
  /// rien à en dire — le relevé a été collé, et aucun prélèvement n'a jamais eu lieu.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Les phrases sont attachées à leur membre, écrites une fois, et une seule ne l'est pas.</b>
  /// <see cref="PreviewAvailability.Live"/> ne dit pas une phrase mais une phrase <b>et un délai</b>
  /// qui change à chaque rendu : la composer ici est le seul endroit où elle peut l'être. Les trois
  /// autres sont sur le membre, comme celles de <see cref="PreviewAbsenceReason"/> — composées à
  /// l'écran, elles auraient divergé d'une surface à l'autre.
  /// </remarks>
  public string? Statement => Availability == PreviewAvailability.Live
    ? $"Les aperçus de ce rapport disparaîtront dans {Countdown}."
    : Availability.Statement;

  /// <summary>
  /// Le délai restant <b>tel qu'une phrase le dit</b> — « 1 h 47 ».
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>Heures et minutes, jamais de secondes.</b> Un décompte à la seconde sur une page qui ne
  /// se rafraîchit pas — il n'y a aucun JavaScript ici — serait faux dès la seconde suivante, et
  /// faux avec une précision qui le ferait croire.
  /// </para>
  /// <para>
  /// ⚠️ <b>Les deux formes courtes ne sont pas des ornements.</b> Sous le plafond absolu, le délai
  /// restant descend sous l'heure : « 0 h 47 » se lit mal, et une phrase qui s'arrêterait aux heures
  /// dirait « dans 0 h » pendant les cinquante-neuf dernières minutes — c'est-à-dire au moment
  /// précis où le chiffre compte.
  /// </para>
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
  /// <exception cref="ArgumentNullException"><paramref name="column"/> est absent.</exception>
  public ColumnPreview? Of(ColumnIdentity column)
  {
    ArgumentNullException.ThrowIfNull(column);

    return Previews.TryGetValue(column, out var preview) ? preview : null;
  }
}

/// <summary>
/// Dans quel état le cache d'aperçus laisse un rapport, et <b>ce que l'écran en dit</b>. Cinq états,
/// dont quatre ont une phrase — le cinquième n'a rien à annoncer.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Trois façons de n'avoir plus rien, et elles ne se disent pas de la même manière.</b> Une
/// durée écoulée, un redémarrage du service et un jeu évincé par le relevé suivant rendent tous un
/// cache vide. Fondus en une seule phrase, l'écran aurait dû choisir laquelle des trois mentir :
/// annoncer un redémarrage qui n'a pas eu lieu à qui vient de supprimer un rapport, ou taire une
/// expiration en la déguisant en panne. La formule commune — « ils ne reviendront pas pour ce
/// rapport » — est vraie des trois, et c'est elle qui les fait tenir ensemble.
/// </para>
/// <para>
/// ⚠️ <b><see cref="NeverTaken"/> ne se confond avec aucune des trois.</b> C'est un relevé
/// <b>collé</b>, où aucun prélèvement n'a jamais eu lieu : il n'y a pas de perte à annoncer, et
/// l'écran se tait. Lui donner une phrase affirmerait qu'un prélèvement a eu lieu, exactement comme
/// quatre comptes d'absence à zéro y inventeraient une incomplétude.
/// </para>
/// <para>
/// ⚠️ <b>Aucun de ces états n'est une <see cref="PreviewAbsenceReason"/> de plus.</b> Une raison
/// d'absence dit pourquoi <b>une colonne</b> n'a rien à montrer, et elle est enregistrée ; ceux-ci
/// portent sur le <b>rapport</b> entier, ne sont enregistrés nulle part, et n'entrent dans aucun des
/// quatre comptes de la <see cref="IncompletenessClause"/>. Les ranger ensemble aurait fait grandir
/// de trois une énumération dont la fermeture est la promesse.
/// </para>
/// <para>
/// ⚠️ <b>La phrase est attachée au membre, écrite une fois</b> — la règle de
/// <see cref="PreviewAbsenceReason"/>, appliquée ici pour le même motif. <see cref="Live"/> fait
/// exception et n'en porte aucune : elle dit une phrase <b>et un délai</b> qui change à chaque
/// rendu, et se compose donc chez <see cref="ScreeningPreviews"/>.
/// </para>
/// </remarks>
public sealed class PreviewAvailability : SmartEnum<PreviewAvailability>
{
  /// <summary>
  /// Ce que les trois pertes disent toutes les trois, et qui est la moitié utile de leur phrase.
  /// </summary>
  /// <remarks>
  /// ⚠️ Sans elle, un <c>Operator</c> devant une fiche dont l'aperçu a disparu lit une
  /// <b>péremption</b> — et la seule issue qu'il imaginerait, relancer un scan, détruirait le
  /// travail déjà tranché. Le motif de forme reste lisible, il reste arbitrable, et l'écran le dit
  /// plutôt que de le laisser deviner.
  /// </remarks>
  private const string StillArbitrable =
    " Vous pouvez arbitrer quand même : le motif de chaque colonne signalée reste lisible, et les "
    + "raisons de n'avoir aucune valeur à montrer sont toujours affichées.";

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
  public static readonly PreviewAvailability Expired = new(
    nameof(Expired),
    3,
    "Les aperçus de ce rapport ont expiré ; ils ne reviendront pas pour ce rapport." + StillArbitrable);

  /// <summary>Le service a redémarré depuis le scan, et les valeurs vivaient dans sa mémoire.</summary>
  /// <remarks>
  /// ⚠️ <b>C'est dit, et ce n'est pas réparé.</b> Les faire survivre à un redémarrage demanderait de
  /// les <b>écrire</b>, c'est-à-dire de faire du service un détenteur durable de données
  /// personnelles du client — voir <c>Rien de réel ne reste</c>.
  /// </remarks>
  public static readonly PreviewAvailability ClearedByRestart = new(
    nameof(ClearedByRestart),
    4,
    "Les aperçus de ce rapport ont été effacés par un redémarrage du service ; ils ne reviendront "
    + "pas pour ce rapport." + StillArbitrable);

  /// <summary>
  /// Un relevé plus récent a pris la place de celui-ci dans un cache qui n'en tient qu'un — et ce
  /// rapport-ci est redevenu le courant depuis, parce qu'on a supprimé celui qui l'avait évincé.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Il existe pour que <see cref="ClearedByRestart"/> cesse d'être une déduction.</b> Le
  /// cache vide se lisait « le service a redémarré », ce qui est faux du rapport qu'une suppression
  /// dans l'historique vient de faire remonter : ses aperçus ont bien disparu, mais aucune panne
  /// n'a eu lieu, et annoncer un redémarrage à qui n'en a pas subi est exactement ce que ce
  /// glossaire refuse ailleurs.
  /// </remarks>
  public static readonly PreviewAvailability Evicted = new(
    nameof(Evicted),
    5,
    "Les aperçus de ce rapport ne sont plus là : un autre relevé a pris leur place. Ils ne "
    + "reviendront pas pour ce rapport." + StillArbitrable);

  private PreviewAvailability(string name, int value, string? statement = null)
    : base(name, value)
  {
    Statement = statement;
  }

  /// <summary>
  /// Ce que l'<c>Operator</c> lit, ou <c>null</c> quand cet état n'a rien à annoncer — le relevé
  /// collé, qui n'a rien perdu, et l'état vivant, dont la phrase porte un délai.
  /// </summary>
  public string? Statement { get; }

  /// <summary>
  /// Cet état annonce-t-il une <b>perte</b> ? ⚠️ <b>C'est ce qui décide du ton de l'écran</b>, et il
  /// est lu ici plutôt que recomposé à chaque surface : un aiguillage rendu au gabarit aurait été
  /// à reprendre à chaque état neuf, sur une surface où personne ne pense à le chercher.
  /// </summary>
  public bool AnnouncesALoss => Statement is not null && this != Live;
}
