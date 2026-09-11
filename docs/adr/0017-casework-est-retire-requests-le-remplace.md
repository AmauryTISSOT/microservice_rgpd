# ADR-0017 — `Casework` est retiré, `Requests` le remplace

- **Statut** : accepté
- **Date** : 2026-09-11
- **Décidé par** : le PRD [#344](https://github.com/AmauryTISSOT/microservice_rgpd/issues/344),
  livré par [#346](https://github.com/AmauryTISSOT/microservice_rgpd/issues/346) à
  [#360](https://github.com/AmauryTISSOT/microservice_rgpd/issues/360) et tracé par
  [#361](https://github.com/AmauryTISSOT/microservice_rgpd/issues/361)
- **Glossaires** : [`CONTEXT-MAP.md`](../../CONTEXT-MAP.md), [Requests](../contexts/requests/CONTEXT.md)
- **Supplante, sur un point** :
  [ADR-0002](./0002-deux-contextes-bornes-et-noyau-partage.md) — le second contexte du découpage,
  `Casework`, « la durée de l'instruction », avec les deux décisions qui n'existaient que pour lui :
  le `qualificationId` opaque qui traversait vers un `Case`, et le garde à sens unique
  `Casework → Qualification`. Le reste de l'ADR-0002 tient : la séparation par le temps, le noyau
  partagé d'un seul type, le garde lu dans l'IL.
- **Supplante, sur un point** :
  [ADR-0003](./0003-troisieme-contexte-sans-intersection-et-garde-des-traversees.md) — la liste
  blanche des traversées, telle que sa matrice l'écrit : la ligne et la colonne `Casework`, et la
  traversée `Casework → SharedKernel`. Le principe « tout interdit sauf exceptions écrites » tient, et
  cet ADR écrit la dérogation que `Requests` doit écrire, comme l'ADR-0016 l'a fait pour
  `Configuration`.
- **Supplante, sur trois points** :
  [ADR-0006](./0006-trois-points-d-entree-renommes-en-francais-identifiants-inchanges.md) —
  l'adresse `/dossiers (inchangée)` du « Tableau des demandes RGPD » ; `Queue` dans la liste des
  identifiants C# qui ne bougent pas ; et le contrat d'`Adapter` (`system_id`,
  `docs/api/adapter.md`) comme moitié du motif de cette liste. Le nom « Tableau des demandes RGPD »
  tient, et les autres identifiants de la liste ne bougent pas.
- **Supplante, sur un point** :
  [ADR-0014](./0014-le-screening-n-enregistre-pas-qui-a-arbitre.md) — sa décision 4, « `Casework`
  n'est pas touché », c'est-à-dire l'asymétrie assumée entre un contexte qui enregistre qui a tranché
  et un contexte qui ne l'enregistre pas. Les décisions 1 à 3 tiennent.
- **Supplante, sur un point** :
  [ADR-0016](./0016-le-manifest-cede-la-place-au-parametrage-un-droit-une-adresse.md) — la dette de
  recâblage, et le maintien de `DeclaredSystem` qui la produisait. Le Paramétrage tient entier.

## Contexte

Le « Tableau des demandes RGPD » reposait sur `Casework` : un `Case` qui s'ouvrait, s'instruisait
et se clôturait, avec ses `Claim`, ses `Step`, son `EvidenceLog`, ses appels d'`Adapter` et ses
`DeclaredSystem`. C'était un modèle d'instruction lourd, et il avait perdu son objet : depuis que le
`Manifest` avait cédé la place au Paramétrage (ADR-0016), plus aucun écran ne déclarait de système,
et un `Case` ouvert sur une base neuve n'avait rien à appeler. Son dépôt, où « rien n'est
obligatoire », ne garantissait même pas que la date de réception, le droit invoqué ou
l'identification de la personne soient saisis.

Ce dont l'`Operator` a besoin d'abord est plus petit : garder trace d'une demande dès qu'elle
arrive, par email ou par courrier.

## Décision

**`Casework` est retiré intégralement**, dans les quatre couches et dans la documentation :
l'agrégat `Case`, les `Claim`, `Step`, `Designation`, l'`EvidenceLog`, les appels d'`Adapter` et
leur secret, la livraison, `StatutoryDeadline`, `ReceptionDate`, `DeclaredSystem`, les
gestionnaires, `POST /cases`, et les écrans `Queue`, `Deposit` et `Case`. La migration
`DropCasework` supprime ses treize tables, sans rien préserver. Son glossaire,
`docs/contexts/casework/CONTEXT.md`, est supprimé ; le contrat d'`Adapter`, `docs/api/adapter.md`,
aussi.

**`Requests` le remplace**, et repart d'une page blanche. Son type est `DataSubjectRequest` — à
l'écran, une **demande** —, là où `Casework` disait `Case` et « dossier ». Son glossaire est
[`docs/contexts/requests/CONTEXT.md`](../contexts/requests/CONTEXT.md). Il ne connaît à ce jour
qu'un `Gesture` : enregistrer une demande à sa réception. L'instruction, les délais et les statuts n'y
existent pas.

**Le temps qu'il couvre est l'arrivée d'une demande**, et non plus la durée de son instruction. Le
découpage par le temps de l'ADR-0002 tient : `Requests` prend la place que `Casework` laisse, sans
en reprendre la durée.

**`Requests` est un consommateur du noyau partagé, et de rien d'autre.** Le droit qu'une demande
invoque est l'un des six `DataSubjectRight`, lu sur le type partagé. `Requests` est donc le troisième
consommateur du noyau, avec `Qualification` et `Configuration`. La liste blanche de
`ContextIsolationTests` reste à trois lignes, toutes vers le noyau :

| | → `Qualification` | → `Requests` | → `Screening` | → `Configuration` | → `SharedKernel` |
| --- | --- | --- | --- | --- | --- |
| **`Qualification`** | — | interdit | interdit | interdit | **permis** |
| **`Requests`** | interdit | — | interdit | interdit | **permis** |
| **`Screening`** | interdit | interdit | — | interdit | interdit |
| **`Configuration`** | interdit | interdit | interdit | — | **permis** |
| **`SharedKernel`** | interdit | interdit | interdit | interdit | — |

`ContextInspector.All` nomme `Requests` à la place de `Casework`, et `ContextRosterTests` l'ancre
sur le nouveau glossaire. L'`AppDbContext` porte désormais `[Requests, Screening, Configuration]`
dans la dérogation de `ContextlessTypeTests`, pour la table `data_subject_requests`.

**Aucune relation n'est déclarée entre `Requests` et `Qualification`.** Le droit invoqué est choisi
par l'`Operator`, jamais lu d'un verdict : une demande s'enregistre sans qu'aucune qualification
n'ait eu lieu. La matrice interdit les deux sens, avec leur motif. Le `qualificationId` opaque que
portait un `Case` n'a pas de successeur.

**La route devient `/demandes`.** `/dossiers`, `/dossiers/depot` et `/dossiers/{id}` rendent 404,
sans redirection. Le point d'entrée garde son nom, « Tableau des demandes RGPD », et sa place dans
le panneau.

**L'enregistrement est un `Gesture`.** Le mot de la langue de système « Geste » devient `Gesture`,
et il est employé par `Screening` et `Requests`. Enregistrer une demande laisse une trace datée de
l'instant où elle est posée, distincte de la date de réception déclarée, et signée `operator` en
attendant l'authentification.

## Ce que chaque supplantation retire, et ce qu'elle laisse

**ADR-0002.** Le second contexte était `Casework`, la durée de l'instruction, dont l'agrégat était
le `Case`. Il n'existe plus, et deux décisions tombent avec lui : `Qualification` fournisseur amont
optionnel de `Casework` par un `qualificationId` opaque, et le garde qui interdisait `Casework →
Qualification`. L'optionnalité, elle, survit sous une forme plus stricte : il n'y a plus d'amont du
tout, et la matrice interdit les deux sens. Le motif « `Capability` reste dans `Casework` » est sans
objet, et le critère qui l'écartait du noyau tient.

**ADR-0003.** La matrice perd sa ligne et sa colonne `Casework`, et la traversée
`Casework → SharedKernel`. Elle gagne celles de `Requests` et la traversée `Requests →
SharedKernel`, ce qui est la dérogation qu'un contexte neuf doit écrire. L'ADR-0016 n'avait pas
supplanté l'ADR-0003 pour avoir **ajouté** une ligne : l'ADR-0003 avait prévu ce geste. Retirer
une traversée qu'il avait écrite en toutes lettres, elle, n'était pas prévu — d'où la
supplantation, bornée à la liste.
Ne sont **pas** supplantés, parce qu'ils ont perdu leur objet sans que rien les contredise : le
motif du pont vers le `Manifest`, qui justifiait un contexte séparé plutôt qu'un ajout à
`Casework`, et la clause `Aucune modification vers le Manifest`. Le `Manifest` et `DeclaredSystem`
sont partis ; l'isolation totale de `Screening` ne dépendait pas d'eux, et la matrice la tient seule.

**ADR-0006.** Trois points ne valent plus. L'adresse du « Tableau des demandes RGPD » était
`/dossiers (inchangée)` ; elle est `/demandes`. `Queue` figurait parmi les identifiants C# qui ne
bougent pas ; il n'a pas été renommé, il a été supprimé. Le motif de cette liste était double — le
contrat public (`system_id`, `docs/api/adapter.md`) et les frontières que les noms de types portent.
Le contrat d'`Adapter` est parti ; la seconde moitié du motif suffit aux identifiants restants,
`Screening`, `ScreenedColumn`, `IScreeningEngine`, `ScreeningEngineIdentity` et `ScreenedListing`.

**ADR-0014.** L'asymétrie assumée avec `Casework`, qui enregistrait son signataire, n'a plus de
second terme. Elle ne se reforme pas avec `Requests` : son `CreatedBy` n'est pas un nom saisi, c'est
la constante `operator`, qui ne prétend prouver aucune identité. Le mot `signature` ne reste plus
acquis à personne en propre ; il reste à écarter dans `Screening`, qui n'enregistre aucun auteur.

**ADR-0016.** La dette de recâblage disait : l'instruction d'un `Case` n'a plus de source de
`DeclaredSystem`, et la payer ouvrira une traversée `Casework → Configuration`. Elle n'est pas payée,
elle est éteinte : il n'y a plus d'instruction à recâbler, ni de `DeclaredSystem` — la table
`declared_systems` est tombée avec les autres. Le message d'appel refusé de `Case.cshtml`, que l'ADR
rangeait parmi les textes à corriger, est parti avec l'écran, et `docs/api/adapter.md` avec le
contrat qu'il décrivait.

## Les options écartées

- **Garder `Casework` et y ajouter un dépôt plus strict.** Le modèle d'instruction serait resté
  entier, sans système à appeler, et chaque ajout aurait dû composer avec lui.
- **Garder le nom `Casework` pour le nouveau contexte.** Le contexte portait le nom de son agrégat,
  le `Case`. Garder l'un sans l'autre aurait fait lire la demande enregistrée comme un dossier
  instruit.
- **Le nommer `Request`, au singulier, ou appeler le type `Request`.** Le mot seul est pris par le
  vocabulaire HTTP ; le glossaire de `Requests` le met en _Avoid_, et le type est `DataSubjectRequest`.
- **Déclarer `Qualification` fournisseur amont de `Requests` dès maintenant.** Aucune demande
  n'emploie de qualification : la déclarer aurait ouvert dans la matrice une traversée qu'aucune
  ligne de code n'emprunte.
- **Rediriger `/dossiers` vers `/demandes`.** Un dossier n'est pas une demande ; une redirection
  aurait fait croire que l'ancien écran vivait sous une autre adresse.

## Conséquences, y compris celles qui coûtent

⚠️ **La lecture par préfixe a un risque d'englobement plus large pour `Requests`.** Le garde lit
l'appartenance à un contexte au segment d'espace de noms, par préfixe. `Requests` est un mot que le
vocabulaire HTTP emploie aussi : tout segment qui commence par lui — un `RequestsLogging/`, par
exemple — se lirait comme du `Requests`. C'est le risque que l'ADR-0016 a rencontré avec
`Configurations/`, et qui a renommé le point de montage de `Web` en `Composition/`. Il n'est pas
réalisé aujourd'hui ; il est écrit dans `CONTEXT-MAP.md` et dans `ContextInspector`.

**Toute la preuve d'instruction disparaît**, et avec elle son régime d'erreur. L'`Omission
silencieuse` était le régime de `Casework` ; elle quitte la carte des contextes et le glossaire de
`Screening`, qui la nommait comme le mal à éviter. Il ne reste que deux régimes, chacun défini là
où il vaut : l'`Erreur relue` dans `Qualification`, l'`Omission relue` dans `Screening`. Le terme
survit dans une quarantaine d'occurrences du code et des tests de `Screening` — commentaires,
messages d'échec, un message d'exception —, où il se lit « une omission que personne ne voit » ;
les réécrire n'est pas fait ici.

**Le témoin Brocanto garde son adaptateur.** `temoin/adapter_rgpd.py` sert le contrat d'`Adapter`
que le service n'appelle plus, et renvoie vers `docs/api/adapter.md`, supprimé. Le témoin reste,
parce que ses schémas servent de corpus à `Screening` ; son adaptateur n'est plus appelé par
personne.

**La clause `Aucune modification vers le Manifest` quitte le glossaire de `Screening`.** Elle
protégeait l'ensemble des `DeclaredSystem`, qui n'existe plus. Elle survit dans des commentaires de
code (`ArbitrateColumnHandler`, `IScreeningExport`, `ScreeningExportService`,
`IncompletenessClause`), et dans le texte gelé `IncompletenessClause.RelationToManifest`, qui
envoie toujours l'`Operator` recenser ses systèmes « dans « Configuration » ». Ni l'un ni l'autre ne
sont réécrits ici : réécrire la clause d'incomplétude est une décision sur ce que `Screening` dit,
que l'ADR-0016 a déjà laissée ouverte.

**L'objection n° 2 de l'ADR-0006 contre le nom « Tableau des demandes RGPD »** — « `demande` n'est
pas ce que l'écran montre » — est sans objet : l'écran montrera des demandes. Sa clause de doctrine,
« le tableau des demandes RGPD est une requête, jamais un processus », a perdu les trois endroits
où elle vivait ; la règle qu'elle défendait est toujours tenue par `NothingRunsInTheBackgroundTests`.

**Le mot `Gesture` est figé par endroits dans le code sous sa forme anglaise** —
`ScreenedColumn.IsWithinReachOfABatchGesture`, `ScreenedTable.WithinReachOfABatchGestureInThisTable`
et la migration appliquée `20260806092543_AddDeliveryGesturesAndLedgerCounts`. Ils le portaient déjà
et ne changent pas.

## Ce que cet ADR n'ouvre pas

- **L'instruction d'une demande** — délais, statuts, clôture, preuve. Si elle vient, elle vient dans
  `Requests`, et le jour où elle appellera les `EndpointUrl` du Paramétrage, la traversée
  `Requests → Configuration` demandera son propre ADR.
- **Une relation entre `Requests` et `Qualification`**, y compris le bouton « Qualification du droit
  par IA », désactivé dans la modale.
- **Le renommage, dans le code de `Screening`, des identifiants qui portent déjà « Gesture ».**
- **La levée du « zéro JavaScript », l'obligation des champs à la réception et le choix de
  Playwright**, décidés par le même PRD et qui auront chacun leur ADR —
  [#362](https://github.com/AmauryTISSOT/microservice_rgpd/issues/362).
