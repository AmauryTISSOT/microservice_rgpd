namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Le port par lequel le domaine fait détecter les données personnelles d'un
/// <see cref="ColumnListing"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>C'est la couture de réversibilité d'ADR-0004, et non une couture de test.</b> Le banc a
/// désigné un dictionnaire, et l'ADR en a tiré que le moteur vit en C# dans <c>Infrastructure</c> ;
/// ce port est ce qui rend cette décision réversible — un moteur qui reviendrait en Python serait
/// une implémentation de plus, et <c>Core</c> ne bougerait pas. Le domaine ignore donc s'il parle à
/// des règles locales, à un modèle servi, ou à un troisième moteur pas encore écrit.
/// </para>
/// <para>
/// <b>Il rend une ligne par colonne du relevé, ou il échoue.</b> Une détection partielle n'existe
/// pas :
/// l'<c>Omission relue</c> repose entièrement sur le fait que le rapport rende <b>toutes</b> les
/// colonnes, y compris celles où rien n'a été vu. Un moteur qui ne saurait traiter qu'une part du
/// relevé lève plutôt que de rendre ce qui se lirait comme complet.
/// </para>
/// <para>
/// ⚠️ <b>Il ne rend aucun score, et il ne peut pas en rendre.</b> Ce qui sort est une
/// <see cref="ScreenedColumn"/>, dont les fabriques exigent une <see cref="PersonalDataCategory"/>,
/// une <see cref="RuleStrength"/> — dérivée de la règle qui a déclenché, jamais d'une
/// auto-évaluation — et un motif en prose française. Aucun chemin ne porte un nombre.
/// </para>
/// <para>
/// <b>Il est asynchrone bien que le moteur retenu soit local et déterministe.</b> C'est le prix de
/// la réversibilité : une signature synchrone obligerait un futur moteur servi à bloquer sur son
/// propre transport, et cette dette-là se paierait dans <c>Core</c>.
/// </para>
/// </remarks>
public interface IScreeningEngine
{
  /// <summary>
  /// Détecte sur le relevé entier, et rend <b>une ligne par colonne</b> — dans l'ordre du relevé — avec
  /// l'identité du moteur qui les a produites.
  /// </summary>
  /// <param name="listing">Le relevé collé, déjà accepté en entier par son ingestion.</param>
  /// <param name="cancellationToken">
  /// L'annulation de l'appelant, propagée jusqu'au moteur : un relevé de vingt mille colonnes
  /// détecté pour quelqu'un qui est parti occupe la place de celui qui est resté.
  /// </param>
  /// <exception cref="ArgumentNullException"><paramref name="listing"/> est absent.</exception>
  Task<ScreenedListing> ScreenAsync(ColumnListing listing, CancellationToken cancellationToken = default);
}
