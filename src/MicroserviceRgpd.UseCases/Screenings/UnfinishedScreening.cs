using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings;

/// <summary>
/// Le verrou « ce dépistage est inachevé », <b>recalculé à chaque rendu</b>.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est un compte, et non un état sur l'agrégat.</b> Un <c>Screening</c> n'en a aucun : il se
/// compte. La nuance n'est pas de style — un état aurait eu besoin de quelque chose pour le mettre à
/// jour, et ce quelque chose serait le processus de fond que ce dépôt interdit au niveau de l'IL.
/// </para>
/// <para>
/// <b>Sans lui, la surface a un défaut propre et sérieux</b> : on déclare lues quarante tables, on
/// croit le travail fini, et l'<c>Omission relue</c> n'a rien rattrapé — les colonnes signalées sont
/// la minorité du rapport, et les relire toutes ne relit rien de ce qui a été omis.
/// </para>
/// <para>
/// ⚠️ <b>Il porte sur le rapport entier, et il se rend aussi sur l'écran d'une table.</b> C'est là
/// qu'il compte le plus : une table relue jusqu'au bout est l'instant précis où l'on croit avoir
/// fini.
/// </para>
/// </remarks>
/// <param name="UnreadUnflagged">Combien de colonnes où rien n'a été vu n'ont pas encore été relues.</param>
/// <param name="Awaiting">
/// Combien de colonnes, <b>toutes confondues</b>, attendent encore qu'un humain les tranche. ⚠️ Il
/// ne commande pas le verrou — il <b>interdit la phrase rassurante</b> tant qu'il n'est pas nul.
/// </param>
public sealed record UnfinishedScreening(int UnreadUnflagged, int Awaiting)
{
  /// <summary>Le dépistage est-il inachevé ? Vrai tant qu'il reste une colonne où rien n'a été vu à relire.</summary>
  /// <remarks>
  /// ⚠️ <b>Le verrou porte sur les seules colonnes non signalées, et c'est délibéré.</b> Il existe
  /// pour l'<c>Omission relue</c> : ce qui échappe au dépistage n'est rattrapé que si quelqu'un relit
  /// là où il n'a <em>rien</em> vu. Des colonnes signalées non tranchées sont du travail visible, que
  /// le compte <c>En attente</c> dit déjà — elles n'ont pas besoin d'un verrou pour être vues.
  /// </remarks>
  public bool IsUnfinished => UnreadUnflagged > 0;

  /// <summary>
  /// Ce que le verrou dit à l'<c>Operator</c>, en toutes lettres. Il vit ici plutôt que dans l'écran
  /// pour que le mot <b>dépistage</b> soit celui du glossaire partout où il se rend.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>La phrase d'achèvement a trois branches, et la branche du milieu est celle qui compte.</b>
  /// Écrite à deux, elle affirmait « toutes les colonnes ont été relues » dès que les non signalées
  /// l'étaient — <b>y compris sur un rapport où personne n'avait rien tranché</b>, lorsque le relevé
  /// n'a aucune colonne non signalée. L'<c>Operator</c> lisait alors le travail comme fini juste
  /// au-dessus d'un compte <c>En attente</c> non nul, dans la même page : très exactement la surface
  /// rassurante contre laquelle ce verrou a été écrit.
  /// </remarks>
  public string Statement => IsUnfinished
    ? $"Ce dépistage est inachevé — {Counted(UnreadUnflagged, "colonne")} où rien n'a été vu "
      + (UnreadUnflagged == 1 ? "n'a" : "n'ont") + " pas encore été relue"
      + (UnreadUnflagged == 1 ? "." : "s.")
    : Awaiting > 0
      ? "Toutes les colonnes où rien n'a été vu ont été relues ; "
        + $"{Counted(Awaiting, "colonne")} signalée{(Awaiting == 1 ? string.Empty : "s")} "
        + (Awaiting == 1 ? "attend" : "attendent") + " encore d'être tranchée"
        + (Awaiting == 1 ? "." : "s.")
      : "Toutes les colonnes de ce dépistage ont été relues, y compris celles où rien n'a été vu.";

  internal static UnfinishedScreening Of(ScreeningCounts counts)
  {
    return new UnfinishedScreening(counts.UnreadUnflagged, counts.Awaiting);
  }

  /// <summary>Un compte et son nom, accordés. Une surface qui se lit au compte ne peut pas écrire « 1 colonnes ».</summary>
  private static string Counted(int count, string noun)
  {
    return count == 1 ? $"{count} {noun}" : $"{count} {noun}s";
  }
}
