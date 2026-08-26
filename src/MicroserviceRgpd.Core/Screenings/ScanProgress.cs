namespace MicroserviceRgpd.Core.Screenings;

/// <summary>
/// Ce qu'un <c>Scan</c> en cours donne à voir pendant qu'il court : la <b>phase</b> où il en est, et
/// le <b>compte réel</b> de cette phase. Il vit <b>en mémoire du processus</b>, n'est
/// <b>jamais</b> persisté, et porte un <see cref="ScanId"/> propre.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>C'est le transitoire qui vit <i>à côté</i> du <see cref="Screening"/>.</b> « Un
/// <c>Screening</c> n'a aucun état » ne bouge pas : l'avancement n'est pas porté par le rapport, il
/// est un objet <b>distinct</b>, qui naît avant lui et meurt quand l'écran d'attente cède la place
/// au rapport.
/// </para>
/// <para>
/// ⚠️ <b>Son compte est vrai, ou il n'est pas.</b> Tant que le catalogue n'a pas répondu,
/// <see cref="ScanSnapshot.Done"/> et <see cref="ScanSnapshot.Total"/> valent <c>null</c> — <b>pas
/// zéro</b> : un dénominateur de zéro est un chiffre, et un chiffre inventé est très exactement ce
/// que cet objet refuse. L'écran ne montre <b>aucune barre</b> avant ce retour.
/// </para>
/// <para>
/// ⚠️ <b>Aucun total global.</b> Chaque phase porte son dénominateur dans <b>son</b> unité — la
/// table pour les aperçus, la colonne pour la détection —, et rien ne les additionne : additionner
/// reviendrait à décider d'avance qu'une table « vaut » <i>n</i> colonnes.
/// </para>
/// <para>
/// ⚠️ <b>Il ne porte pas la chaîne de connexion, et il n'a aucun champ où la mettre.</b> Elle est
/// remise au port de scan et à rien d'autre. Un écran que le navigateur rafraîchit toutes les trois
/// secondes est le dernier endroit du service où un secret d'accès aurait sa place.
/// </para>
/// <para>
/// ⚠️ <b>Il se lit depuis un autre fil que celui qui l'écrit</b> — le scan court hors de la requête
/// de l'<c>Operator</c>, l'écran d'attente est servi par une requête à lui. La lecture rend donc un
/// <see cref="ScanSnapshot"/> pris <b>d'un seul coup</b> : lire la phase, puis le compte, puis la fin
/// aurait laissé l'écran afficher « aperçus, table 312 sur 312 » à propos d'une phase que le scan
/// avait déjà quittée.
/// </para>
/// </remarks>
public sealed class ScanProgress
{
  private readonly Lock _turn = new();

  private ScanSnapshot _snapshot = new(ScanPhase.Connecting, Done: null, Total: null);

  private ScanProgress(ScanId id, DatabaseDialect dialect, DateTimeOffset startedOn)
  {
    Id = id;
    Dialect = dialect;
    StartedOn = startedOn;
  }

  /// <summary>L'identité de ce scan, celle que porte l'adresse de l'écran d'attente.</summary>
  public ScanId Id { get; }

  /// <summary>
  /// Le SGBD que l'<c>Operator</c> a choisi — <b>le seul morceau de sa demande qui vive ici</b>.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>Le dialecte, et jamais l'hôte, le nom de base ou l'utilisateur.</b> Ces trois-là ne se
  /// connaissent qu'en découpant la chaîne de connexion, et une chaîne rendue par morceaux reste une
  /// chaîne rendue. Le dialecte est un choix fait dans une liste fermée de trois, affichée sur le
  /// formulaire : il est ici parce que le <b>refus d'un second lancement</b> doit dire de quelle
  /// sorte de base le service lit le schéma, sans quoi l'<c>Operator</c> ne sait pas si le service
  /// travaille pour lui ou pour quelqu'un d'autre.
  /// </remarks>
  public DatabaseDialect Dialect { get; }

  /// <summary>L'instant où l'<c>Operator</c> a lancé ce scan, lu sur l'horloge injectée.</summary>
  public DateTimeOffset StartedOn { get; }

  /// <summary>
  /// Où en est ce scan, <b>pris d'un seul coup</b>. C'est la seule lecture, et il n'en existe pas de
  /// partielle.
  /// </summary>
  public ScanSnapshot Snapshot
  {
    get
    {
      lock (_turn)
      {
        return _snapshot;
      }
    }
  }

  /// <summary>
  /// Un scan qui commence : la phase de connexion, et <b>aucun compte</b> — rien n'a encore répondu.
  /// </summary>
  /// <param name="id">L'identité engendrée pour ce scan.</param>
  /// <param name="dialect">Le SGBD choisi, seul morceau de la demande qui vive ici.</param>
  /// <param name="startedOn">L'instant du lancement, lu sur l'horloge injectée.</param>
  /// <exception cref="ArgumentNullException"><paramref name="dialect"/> est absent.</exception>
  public static ScanProgress Starting(
    ScanId id,
    DatabaseDialect dialect,
    DateTimeOffset startedOn)
  {
    ArgumentNullException.ThrowIfNull(dialect);

    return new ScanProgress(id, dialect, startedOn);
  }

  /// <summary>
  /// Un pas rapporté par le port de scan : il apporte la phase, ce qui est fait, et le dénominateur
  /// réel de cette phase.
  /// </summary>
  /// <param name="step">Le pas franchi, tel que le scanner l'a rapporté.</param>
  /// <exception cref="ArgumentNullException"><paramref name="step"/> est absent.</exception>
  public void Record(ScanStep step)
  {
    ArgumentNullException.ThrowIfNull(step);

    lock (_turn)
    {
      if (_snapshot.HasEnded)
      {
        // Un pas rapporté après la fin est le pas d'un scan que plus personne n'attend : le laisser
        // écraser la fin ferait reculer l'écran d'attente du rapport vers une phase.
        return;
      }

      _snapshot = _snapshot with { Phase = step.Phase, Done = step.Done, Total = step.Total };
    }
  }

  /// <summary>
  /// Le scan a rendu son relevé ; la <b>détection</b> commence, et son dénominateur est la colonne.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>C'est la phase que le scan n'atteint jamais</b> — elle existe aussi sur le chemin collé.
  /// L'attente en couvre une de plus que le scan parce que l'<c>Operator</c> attend un rapport, et
  /// non un <see cref="ColumnListing"/>.
  /// </remarks>
  /// <param name="columnCount">Le nombre de colonnes du relevé, qui est le dénominateur réel.</param>
  /// <exception cref="ArgumentOutOfRangeException"><paramref name="columnCount"/> est négatif.</exception>
  public void Detecting(int columnCount)
  {
    ArgumentOutOfRangeException.ThrowIfNegative(columnCount);

    lock (_turn)
    {
      _snapshot = _snapshot with { Phase = ScanPhase.Detecting, Done = 0, Total = columnCount };
    }
  }

  /// <summary>
  /// Le rapport est écrit : ce scan est fini, et l'écran d'attente n'a plus qu'à céder la place.
  /// </summary>
  /// <param name="report">L'identité du <see cref="Screening"/> que ce scan a produit.</param>
  public void Produced(ScreeningId report)
  {
    lock (_turn)
    {
      if (_snapshot.HasEnded)
      {
        // ⚠️ La fin ne recule jamais, et la règle vaut pour les TROIS écritures — pas seulement pour
        // les pas rapportés. Sans cela, un rattrapage d'exception posé après coup écrirait « arrêté
        // sans rapport » par-dessus un rapport réellement écrit, et l'écran d'attente dirait à
        // l'Operator le contraire de ce qui vient de se passer.
        return;
      }

      _snapshot = _snapshot with
      {
        Done = _snapshot.Total,
        Ending = ScanEnding.Listed,
        Report = report,
      };
    }
  }

  /// <summary>
  /// Le scan s'est terminé <b>sans produire de rapport</b> : l'une des deux fins à zéro objet, ou un
  /// échec à phase et famille nommées.
  /// </summary>
  /// <remarks>
  /// ⚠️ <b>L'écran qui dit ces trois fins vient au ticket des fins anormales.</b> Ce qui est tenu
  /// ici est ce sans quoi ce ticket-là n'aurait rien à écrire : la fin est <b>enregistrée sur le
  /// transitoire</b> plutôt que perdue, et le scan cesse d'être en vol — sans quoi une base
  /// injoignable aurait bloqué le déploiement entier jusqu'au redémarrage.
  /// </remarks>
  /// <param name="ending">La fin que le scan a connue, et elle n'est jamais <see cref="ScanEnding.Listed"/>.</param>
  /// <param name="failure">La phase et la famille, quand la fin est un échec.</param>
  /// <exception cref="ArgumentNullException"><paramref name="ending"/> est absent.</exception>
  /// <exception cref="ArgumentException"><paramref name="ending"/> est la fin qui produit un rapport.</exception>
  public void EndedWithoutAReport(ScanEnding ending, ScanFailure? failure = null)
  {
    ArgumentNullException.ThrowIfNull(ending);

    if (ending == ScanEnding.Listed)
    {
      throw new ArgumentException(
        "La fin qui produit un relevé se dit par Produced, qui porte l'identité du rapport : "
        + "l'écran d'attente doit pouvoir rediriger vers quelque chose.",
        nameof(ending));
    }

    lock (_turn)
    {
      if (_snapshot.HasEnded)
      {
        // ⚠️ Même règle, et c'est ici qu'elle protège le plus : le rattrapage d'exception du lanceur
        // appelle cette méthode sans savoir si le rapport a été écrit. Une fin déjà posée gagne.
        return;
      }

      _snapshot = _snapshot with { Ending = ending, Failure = failure };
    }
  }

  /// <summary>
  /// L'<c>Operator</c> reprend la main : le scan s'arrête <b>là où il en est</b>, et n'aura pas de
  /// rapport.
  /// </summary>
  /// <remarks>
  /// <para>
  /// ⚠️ <b>La fin est posée <i>avant</i> que la requête soit coupée, et c'est ce qui rend l'écran
  /// honnête.</b> Attendre que le port lève ferait répondre à la requête d'abandon un écran d'attente
  /// qui se rafraîchit encore, à propos d'un scan que l'<c>Operator</c> vient d'arrêter.
  /// </para>
  /// <para>
  /// ⚠️ <b>Un abandon arrivé après la fin n'a aucune conséquence.</b> Le rapport vient peut-être
  /// d'être écrit : dire « abandonné » par-dessus annoncerait le contraire de ce qui s'est passé,
  /// alors que le rapport d'avant est déjà parti à l'archive.
  /// </para>
  /// <para>
  /// ⚠️ <b>Aucune <see cref="ScanFailure"/> n'accompagne l'abandon.</b> Rien n'a raté : lui coller
  /// une famille de cause enverrait l'<c>Operator</c> chercher une panne du réseau ou de la base là
  /// où il n'y en a eu aucune.
  /// </para>
  /// </remarks>
  public void Abandon()
  {
    EndedWithoutAReport(ScanEnding.Abandoned);
  }
}

/// <summary>
/// Où en est un <c>Scan</c> à l'instant où quelqu'un regarde : la phase, le compte de cette phase
/// s'il est <b>réel</b>, et la fin s'il en a une.
/// </summary>
/// <remarks>
/// <para>
/// <b>Immuable, et pris d'un seul coup.</b> C'est ce qui permet à l'écran d'attente d'afficher une
/// phase et un compte qui parlent du même instant.
/// </para>
/// <para>
/// ⚠️ <b><see cref="Done"/> et <see cref="Total"/> sont absents, jamais zéro, tant qu'aucun
/// dénominateur n'est honnête.</b> « 0 sur 0 » se lit comme une mesure ; l'absence, elle, ne se lit
/// pas du tout — et c'est exactement ce qu'il faut afficher quand on ne sait pas encore.
/// </para>
/// </remarks>
/// <param name="Phase">La phase où le scan en est, ou celle où il s'est arrêté.</param>
/// <param name="Done">Ce qui est fait dans l'unité de la phase, ou <c>null</c> avant le catalogue.</param>
/// <param name="Total">Le dénominateur réel de la phase, ou <c>null</c> avant le catalogue.</param>
/// <param name="Ending">La fin de ce scan, ou <c>null</c> tant qu'il court.</param>
/// <param name="Report">Le rapport produit, présent sur la seule fin qui en produise un.</param>
/// <param name="Failure">La phase et la famille de ce qui l'a fait tomber, sur la seule fin qui tombe.</param>
public sealed record ScanSnapshot(
  ScanPhase Phase,
  int? Done,
  int? Total,
  ScanEnding? Ending = null,
  ScreeningId? Report = null,
  ScanFailure? Failure = null)
{
  /// <summary>
  /// Y a-t-il une barre à montrer ? <b>Non tant que le catalogue n'a pas répondu</b> : avant lui,
  /// aucun dénominateur n'est honnête.
  /// </summary>
  public bool ShowsABar => Done is not null && Total is not null;

  /// <summary>Ce scan est-il fini, de quelque façon que ce soit ?</summary>
  public bool HasEnded => Ending is not null;
}
