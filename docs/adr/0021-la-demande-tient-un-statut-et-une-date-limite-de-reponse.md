# ADR-0021 — La demande tient un statut et une date limite de réponse

- **Statut** : accepté
- **Date** : 2026-09-11
- **Décidé par** : l'US « Consulter la liste des demandes RGPD »
- **Glossaires** : [`CONTEXT-MAP.md`](../../CONTEXT-MAP.md), [Requests](../contexts/requests/CONTEXT.md)
- **Supplante, sur un point** :
  [ADR-0017](./0017-casework-est-retire-requests-le-remplace.md) — la phrase « l'instruction, les
  délais et les statuts n'y existent pas ». Les délais et les statuts entrent dans `Requests` ;
  l'instruction n'y entre pas. Le reste de l'ADR-0017 tient.
- **Ne supplante pas l'[ADR-0019](./0019-la-reception-exige-date-message-droit-et-identification.md)**,
  qui rangeait l'échéance parmi ce qu'il n'ouvrait pas, et posait qu'un délai, s'il revenait,
  « partira de la date déclarée ». C'est ce que cet ADR fait.

## Contexte

`Requests` ne connaissait qu'un `Gesture` : enregistrer une demande. Une demande enregistrée
n'avait ni échéance ni statut, et la table `data_subject_requests` le disait en toutes lettres :
« Ni statut, ni échéance ».

L'`Operator` doit désormais consulter les demandes dans un tableau, et y repérer celles qui
approchent de leur date limite ou l'ont dépassée. Il faut pour cela que chaque demande porte une
date limite de réponse, et un statut qui dise si elle attend encore une réponse.

## Décision

**Une demande tient une date limite de réponse**, `ResponseDeadline`, colonne `response_deadline`.
Elle vaut la date de réception plus un mois, ramenée au dernier jour du mois suivant quand ce jour
n'y existe pas — ce que font `DateOnly.AddMonths` et `+ interval '1 month'` de PostgreSQL, à
l'identique. Elle est **fixée par `DataSubjectRequest.Receive`** et enregistrée ; elle ne se
recalcule pas à la lecture. Elle part de la date de réception déclarée, jamais de l'instant
d'enregistrement.

**Une demande tient un statut**, `Status`, du vocabulaire fermé `RequestStatus` : `InProgress`,
`Completed`, `Cancelled` — à l'écran « En cours », « Terminée », « Annulée ». Il est stocké par son
nom dans la colonne `status`, comme l'origine et le droit. Une demande naît `InProgress`.

⚠️ **Le statut est un état, et non la trace d'un `Gesture`.** C'est une exception assumée au refus
des états qui court dans le dépôt : un `Screening` n'a aucun état, un arbitrage est une trace datée.
Une demande, elle, se lit d'abord par où elle en est, et le tableau la trie et la signale par là.
Ce que les actes qui feront changer le statut laisseront comme trace est la décision de l'US qui
les introduira.

**Aucun `Gesture` ne fait encore changer le statut.** `Completed` et `Cancelled` existent dans le
vocabulaire sans être atteignables : terminer et annuler une demande font l'objet d'une autre US.
Les trois valeurs sont posées ensemble pour que cette US n'ait pas à migrer la colonne.

**Les signalements ne sont pas enregistrés.** « Échéance proche » — la date limite entre aujourd'hui
et aujourd'hui plus sept jours, bornes comprises — et « En retard » — la date limite antérieure à
aujourd'hui — ne valent que pour une demande `InProgress`. Ils sont calculés par le serveur quand il
rend la ligne, avec « aujourd'hui » lu par `ParisCalendar`. Le script ne les recalcule pas.

**Les demandes déjà enregistrées sont reprises par la migration** : statut `InProgress`, date
limite calculée en SQL depuis `received_on`.

## Les options écartées

- **Déduire le statut des `Gesture`, sans colonne.** Fidèle à la doctrine, et le statut vaudrait
  « En cours » tant qu'aucun acte ne le change. Écarté : l'US demande un statut enregistré, et la
  forme des actes à venir — terminer, annuler — n'est pas décidée ; les forcer dès maintenant à
  laisser des traces datées, c'est trancher à leur place.
- **N'introduire que `InProgress`.** Deux valeurs inatteignables se lisent comme une promesse ; mais
  les ajouter plus tard ne demanderait qu'une migration de plus pour un vocabulaire que le RGPD ne
  fera pas bouger.
- **Calculer la date limite à la lecture.** Tenable tant qu'elle ne dépend que de la date de
  réception. Écarté : la prolongation de l'art. 12.3 et le recalcul après modification de la date de
  réception la feront changer ; une valeur enregistrée est ce que ces actes modifieront.
- **Faire recalculer les signalements par le script**, pour une page restée ouverte au-delà de
  minuit. La règle aurait été écrite deux fois, en C# et en JavaScript, pour un tableau qui n'est
  de toute façon pas en direct.

## Conséquences, y compris celles qui coûtent

⚠️ **Une page ouverte au-delà de minuit garde les signalements de la veille** jusqu'à son
rechargement. C'est le prix d'une règle écrite une seule fois.

⚠️ **La date limite ne suit pas la date de réception.** Tant que la modification d'une demande
n'existe pas, rien ne les désaccorde ; l'US qui l'ouvrira devra recalculer la date limite.

**Le commentaire « Ni statut, ni échéance » de `DataSubjectRequestConfiguration` tombe**, avec son
motif : la colonne d'état n'est plus la promesse d'une instruction absente, c'est ce que le tableau
lit.

## Ce que cet ADR n'ouvre pas

- **L'instruction**, et tout acte qui fait changer le statut.
- **La prolongation du délai** (art. 12.3).
- **Le filtre par statut.**
