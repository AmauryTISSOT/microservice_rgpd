namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Le port sortant par lequel le contexte <c>Screening</c> va lire une base tierce : il reçoit un
/// dialecte et une chaîne de connexion, et rend le <b>texte pivot</b> du relevé et les
/// <see cref="ColumnPreview"/> des colonnes. C'est le <b>seul</b> point du contexte qui touche une
/// base d'un tiers, et le seul que les tests fonctionnels doublent.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Le nom « scanner » passe le test du chemin collé.</b> Un scanner ne travaille jamais sur un
/// relevé collé : il ne peut donc désigner que ce qui va chercher. C'est précisément pour cela que le
/// mot reste <b>interdit</b> sur <see cref="IScreeningEngine"/>, qui, lui, sert les deux chemins.
/// </para>
/// <para>
/// ⚠️ <b>Il rapporte son avancement, et l'écran compte pour de vrai.</b> Un relevé de trois cents
/// tables se prélève table par table ; sans <see cref="ScanStep"/>, l'écran d'attente n'aurait qu'un
/// pourcentage inventé à afficher. Le <see cref="IProgress{T}"/> est facultatif — un appelant qui ne
/// montre rien ne paie rien.
/// </para>
/// <para>
/// ⚠️ <b>Il est annulable, et l'annulation coupe la requête en cours.</b> Un jeton qui ne ferait que
/// sortir de la boucle laisserait un <c>SELECT</c> courir sur la base du client après que
/// l'<c>Operator</c> a quitté l'écran : l'implémentation doit interrompre la requête elle-même.
/// </para>
/// <para>
/// ⚠️ <b>Aucune exception du pilote ne traverse ce port.</b> Ce qui rate devient une
/// <see cref="PreviewAbsenceReason"/> quand une colonne seule est en cause, ou un
/// <see cref="ScanOutcome.Failed"/> à phase et famille nommées quand c'est le scan. Ni message du
/// pilote, ni hôte, ni utilisateur : c'est ici que <c>Rien de réel ne reste</c> se tient, à la
/// frontière, et non dans chaque écran en aval.
/// </para>
/// <para>
/// ⚠️ <b>Il ne rend pas un <see cref="ColumnListing"/>.</b> Le texte pivot repasse par
/// <see cref="ColumnListingIngestion.Ingest"/> exactement comme un relevé collé — voir
/// <see cref="ScanOutcome"/>.
/// </para>
/// </remarks>
public interface IDatabaseScanner
{
  /// <summary>
  /// Joint la base, relève son catalogue, prélève ses valeurs, et rend l'une des quatre fins d'un
  /// scan.
  /// </summary>
  /// <param name="dialect">
  /// Le SGBD à joindre. Un dialecte sans pilote câblé est une erreur de programmation, pas une fin
  /// de scan.
  /// </param>
  /// <param name="connectionString">
  /// La chaîne de connexion, telle que l'<c>Operator</c> l'a fournie. Elle ne ressort d'aucun côté.
  /// </param>
  /// <param name="progress">Où rapporter les pas franchis, si quelqu'un regarde.</param>
  /// <param name="cancellationToken">
  /// Le jeton dont l'annulation coupe la requête en cours, pas seulement la boucle.
  /// </param>
  /// <exception cref="OperationCanceledException">
  /// L'appelant a repris la main. Ce n'est pas une fin du scan.
  /// </exception>
  Task<ScanOutcome> ScanAsync(
    DatabaseDialect dialect,
    string connectionString,
    IProgress<ScanStep>? progress = null,
    CancellationToken cancellationToken = default);
}
