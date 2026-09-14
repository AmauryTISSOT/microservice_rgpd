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
/// <b>Il est asynchrone, bien que le moteur lexique soit local et déterministe.</b> C'était le prix
/// de la réversibilité, et A2 l'a encaissé (ADR-0025) : une signature synchrone aurait obligé le
/// moteur servi par Ollama à bloquer sur son propre transport, et cette dette-là se serait payée
/// dans <c>Core</c>.
/// </para>
/// <para>
/// ⚠️ <b>Il reçoit les aperçus <i>à côté</i> du relevé, jamais dedans, et ils ne ressortent pas.</b>
/// Le <see cref="ColumnListing"/> est <b>persisté</b> : y loger des valeurs ferait tomber
/// <c>Rien de réel ne reste</c> par le plus court des chemins, et effacerait au passage la clause qui
/// veut qu'un relevé scanné et un relevé collé soient le même objet. Le chemin collé n'en fournit
/// simplement aucun — <see cref="NoPreviews"/> — et rien ne les fait ressortir : ce que le port rend
/// est un <see cref="ScreenedListing"/>, dont aucun champ ne sait porter une valeur.
/// </para>
/// <para>
/// ⚠️ <b>Il n'y a pas de second port pour les valeurs.</b> Un moteur de formes appelé à côté de
/// celui-ci rendrait <b>deux</b> rapports à fusionner, donc un étage qui arbitre « ce que dit le
/// nom » contre « ce que disent les valeurs » — l'étage exact que le modèle refuse. Il n'y a qu'une
/// détection, et elle lit les deux.
/// </para>
/// </remarks>
public interface IScreeningEngine
{
  /// <summary>
  /// Ce que le <b>chemin collé</b> passe en second : aucun aperçu. Il est nommé plutôt que fabriqué
  /// à chaque appel, parce qu'un dictionnaire vide écrit à la main sur chaque site d'appel se lirait
  /// comme un oubli — alors que c'est une propriété du chemin, et la seule chose qui distingue les
  /// deux.
  /// </summary>
  public static IReadOnlyDictionary<ColumnIdentity, ColumnPreview> NoPreviews { get; } =
    new Dictionary<ColumnIdentity, ColumnPreview>();

  /// <summary>
  /// Détecte sur le relevé entier, et rend <b>une ligne par colonne</b> — dans l'ordre du relevé — avec
  /// l'identité du moteur qui les a produites.
  /// </summary>
  /// <param name="listing">Le relevé, collé ou scanné, déjà accepté en entier par son ingestion.</param>
  /// <param name="previews">
  /// Les aperçus, par colonne — <see cref="NoPreviews"/> sur le chemin collé. Une colonne absente de
  /// ce dictionnaire est une colonne dont aucune valeur n'a été lue, ce qui n'est <b>jamais</b> un
  /// motif de ne pas la signaler : une règle de forme ne peut qu'<b>ajouter</b> un signalement,
  /// jamais en retirer un.
  /// </param>
  /// <param name="cancellationToken">
  /// L'annulation de l'appelant, propagée jusqu'au moteur : un relevé de vingt mille colonnes
  /// détecté pour quelqu'un qui est parti occupe la place de celui qui est resté.
  /// </param>
  /// <exception cref="ArgumentNullException"><paramref name="listing"/> ou <paramref name="previews"/> est absent.</exception>
  Task<ScreenedListing> ScreenAsync(
    ColumnListing listing,
    IReadOnlyDictionary<ColumnIdentity, ColumnPreview> previews,
    CancellationToken cancellationToken = default);
}
