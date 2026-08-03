namespace MicroserviceRgpd.Infrastructure.Data.Casework;

/// <summary>
/// Ce que les tables de ce contexte partagent, et qui ne vaut la peine d'être écrit qu'une fois.
/// </summary>
internal static class CaseworkSchema
{
  /// <summary>
  /// Le plafond d'une colonne portant le mot d'un <b>vocabulaire fermé</b> — un nom de membre,
  /// jamais un texte. Il dit à la base ce qu'elle attend, et la fait cesser d'accepter ce que le
  /// domaine n'écrira jamais.
  /// <para>
  /// Il vit ici plutôt qu'en double dans chaque configuration : deux constantes valant chacune 64
  /// finiraient par ne plus valoir la même chose, et deux colonnes de même nature se borneraient
  /// alors différemment sans que personne ne l'ait décidé.
  /// </para>
  /// </summary>
  internal const int ClosedVocabularyLength = 64;
}
