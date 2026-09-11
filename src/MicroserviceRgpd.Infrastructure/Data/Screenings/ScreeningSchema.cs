namespace MicroserviceRgpd.Infrastructure.Data.Screenings;

/// <summary>
/// Ce que les deux tables de ce contexte partagent, et qui ne vaut la peine d'être écrit qu'une
/// fois.
/// </summary>
/// <remarks>
/// ⚠️ <b>C'est une constante <b>propre à ce contexte</b>, et non une constante partagée.</b> Qu'un
/// autre contexte veuille la même, il l'écrira chez lui : lire celle d'un autre serait une traversée
/// que le garde d'ADR-0003 refuse, pour l'économie d'un entier. La duplication est ici le prix
/// déclaré du <c>Separate Ways</c> intégral.
/// </remarks>
internal static class ScreeningSchema
{
  /// <summary>
  /// Le plafond d'une colonne portant le mot d'un <b>vocabulaire fermé</b> — un nom de membre,
  /// jamais un texte. Il dit à la base ce qu'elle attend, et la fait cesser d'accepter ce que le
  /// domaine n'écrira jamais.
  /// </summary>
  internal const int ClosedVocabularyLength = 64;

  /// <summary>La table du rapport.</summary>
  internal const string Screenings = "screenings";

  /// <summary>La table des colonnes détectées.</summary>
  internal const string ScreenedColumns = "screened_columns";
}
