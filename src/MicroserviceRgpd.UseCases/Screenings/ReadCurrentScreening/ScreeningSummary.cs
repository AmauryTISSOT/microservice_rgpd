using MicroserviceRgpd.Core.Screenings;

namespace MicroserviceRgpd.UseCases.Screenings.ReadCurrentScreening;

/// <summary>
/// Le <b>rapport sommaire</b> du dépistage courant : son entête, ses tables retriées, ses comptes et
/// son verrou.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>Il ne filtre rien, parce qu'il ne rend aucune colonne.</b> C'est la vue d'où l'on choisit
/// une table, et l'interdit de l'<c>Omission relue</c> porte sur ce qu'on montre <em>d'une table
/// ouverte</em> : masquer les <c>Unflagged</c> là serait fatal. Ici, chaque table est nommée avec
/// <b>toutes</b> ses colonnes comptées, signalées et non signalées ensemble — et le verrou dit
/// combien restent à relire.
/// </para>
/// <para>
/// <b>Tout ce qu'il porte est calculé à l'instant du rendu.</b> Aucun de ces nombres n'est persisté,
/// et aucun n'aurait de quoi se mettre à jour s'il l'était.
/// </para>
/// </remarks>
/// <param name="Id">L'identité du rapport, celle sous laquelle on ouvrira ses tables.</param>
/// <param name="Database">
/// Le nom de base que le relevé rapportait. ⚠️ <b>Un repère pour l'humain</b> qui relit son rapport
/// trois jours plus tard — jamais une identité sur laquelle bâtir une comparaison.
/// </param>
/// <param name="Dialect">
/// Le SGBD dont le relevé se déclarait. Il est rendu parce que <b>sans lui, « cette colonne n'a pas
/// de commentaire » et « ce SGBD n'en rend jamais » se lisent pareil</b>.
/// </param>
/// <param name="Engine">
/// Qui a dépisté, et dans quelle version. ⚠️ Elle ne sert qu'à l'humain qui relit ou qui compare
/// deux rapports : le domaine ne l'interprète jamais.
/// </param>
/// <param name="LaunchedOn">Quand le dépistage a été lancé — le seul fait dont dépend « courant ».</param>
/// <param name="Tables">Les tables du relevé, retriées par le service.</param>
/// <param name="Tally">Les comptes du rapport.</param>
/// <param name="Lock">Le verrou d'inachèvement, recalculé à ce rendu.</param>
public sealed record ScreeningSummary(
  ScreeningId Id,
  string Database,
  string Dialect,
  ScreeningEngineIdentity Engine,
  DateTimeOffset LaunchedOn,
  IReadOnlyList<SummarisedTable> Tables,
  ScreeningTally Tally,
  UnfinishedScreening Lock)
{
  /// <summary>Le sommaire d'un rapport, à l'instant où on le regarde.</summary>
  /// <exception cref="ArgumentNullException"><paramref name="screening"/> est absent.</exception>
  internal static ScreeningSummary Of(Screening screening)
  {
    ArgumentNullException.ThrowIfNull(screening);

    // ⚠️ Un seul regroupement, et ce n'est pas de l'optimisation prématurée. Interroger le rapport
    // table par table le rebalaie en entier à chaque table : au pire cas que le contrat de format
    // autorise — 20 000 colonnes, ~1 540 tables —, cela fait des dizaines de millions de
    // comparaisons À CHAQUE RENDU de l'écran, dont autant d'allocations, car TableIdentity est
    // reconstruite à chaque lecture. Le budget de 10 s couvre le dépôt ; ce chemin-ci est celui que
    // l'Operator reparcourt à chaque rafraîchissement, et rien ne le chiffrait.
    var columnsByTable = screening.Columns
      .GroupBy(column => column.Identity.TableIdentity)
      .ToDictionary(group => group.Key, group => group.ToArray());

    return new ScreeningSummary(
      screening.Id,
      screening.Database,
      screening.Dialect,
      screening.Engine,
      screening.LaunchedOn,
      // ⚠️ L'ORDRE reste celui de l'agrégat : c'est lui qui porte la règle du retri par le service.
      // Retrier ici aurait écrit la même règle à deux endroits, et l'un des deux aurait dérivé.
      [.. screening.Tables.Select(table => SummarisedTable.Of(table, columnsByTable[table]))],
      ScreeningTally.Of(screening),
      UnfinishedScreening.Of(screening));
  }
}

/// <summary>
/// Une table du relevé, telle que le sommaire la nomme : ce qu'elle porte, et ce qu'il y reste à
/// faire.
/// </summary>
/// <param name="Identity">Le schéma et la table.</param>
/// <param name="ColumnCount">Combien de colonnes elle porte, <b>toutes</b>.</param>
/// <param name="FlaggedCount">Combien le dépistage en a signalées.</param>
/// <param name="AwaitingCount">Combien attendent encore qu'un humain les tranche.</param>
public sealed record SummarisedTable(
  TableIdentity Identity,
  int ColumnCount,
  int FlaggedCount,
  int AwaitingCount)
{
  internal static SummarisedTable Of(TableIdentity table, IReadOnlyList<ScreenedColumn> columns)
  {
    return new SummarisedTable(
      table,
      columns.Count,
      columns.Count(column => column.IsFlagged),
      columns.Count(column => column.AwaitsAnArbitration));
  }
}

/// <summary>
/// Les cinq comptes du rapport. <b>L'avancement est un compte, jamais un état de haut niveau
/// rassurant</b> : « douze colonnes en attente » se lit, « en cours » ne se lit pas.
/// </summary>
/// <param name="Flagged">
/// Combien de colonnes le dépistage a signalées. ⚠️ Le complément est ce qu'il <b>n'a pas vu</b>,
/// jamais ce qui serait inoffensif : le service n'a jamais vu une seule valeur.
/// </param>
/// <param name="Retained">Combien un humain a retenues, sous son nom.</param>
/// <param name="SetAside">Combien un humain a écartées, sous son nom.</param>
/// <param name="Awaiting">Combien attendent encore qu'un humain les tranche.</param>
/// <param name="RetainedOnUnflagged">
/// Combien un humain a retenues là où le dépistage n'avait <b>rien vu</b>. ⚠️ <b>C'est la mesure
/// directe de ce que l'<c>Omission relue</c> a rattrapé</b>, et elle vaut zéro tant que personne n'a
/// relu — ce qui est très exactement ce qu'on lui demande de dire.
/// </param>
public sealed record ScreeningTally(
  int Flagged,
  int Retained,
  int SetAside,
  int Awaiting,
  int RetainedOnUnflagged)
{
  internal static ScreeningTally Of(Screening screening)
  {
    return new ScreeningTally(
      screening.FlaggedCount,
      screening.RetainedCount,
      screening.SetAsideCount,
      screening.AwaitingCount,
      screening.RetainedOnUnflaggedCount);
  }
}

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

  internal static UnfinishedScreening Of(Screening screening)
  {
    return new UnfinishedScreening(screening.UnreadUnflaggedCount, screening.AwaitingCount);
  }

  /// <summary>Un compte et son nom, accordés. Une surface qui se lit au compte ne peut pas écrire « 1 colonnes ».</summary>
  private static string Counted(int count, string noun)
  {
    return count == 1 ? $"{count} {noun}" : $"{count} {noun}s";
  }
}
