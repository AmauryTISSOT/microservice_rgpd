# ADR-0015 — Le scan survit à la requête qui l'a lancé, et « rien ne tourne » tient quand même

- **Statut** : accepté
- **Date** : 2026-08-26
- **Décidé par** : la livraison [#299](https://github.com/AmauryTISSOT/microservice_rgpd/issues/299), et le ticket qui l'a relue de bout en bout : [#311](https://github.com/AmauryTISSOT/microservice_rgpd/issues/311)
- **Amende un garde de dépôt**, pas un ADR : `NothingRunsInTheBackgroundTests`, dans `tests/MicroserviceRgpd.ArchitectureTests/`. Le garde n'est pas assoupli — sa liste ne bouge pas, et aucun `IHostedService`, aucune minuterie, aucun ordonnanceur n'entre. Ce qui est écrit ici, c'est où passe sa frontière, que son propre texte ne disait pas.
- **Ne supplante aucun ADR.** [ADR-0012](./0012-la-connexion-le-scan-et-les-echantillons-entrent-dans-screening.md) est tenu : il avait prévu que « le scan est asynchrone » et que le `Screening` acquiert « un état transitoire » ; il n'avait pas écrit que le travail dépasserait l'échange HTTP qui le déclenche, ni ce que cela ferait au garde. C'est le trou que cet ADR bouche.

## Contexte

Deux textes du dépôt se lisent, aujourd'hui, comme s'ils se contredisaient.

Le premier est le garde le plus important de la surface :

> **Rien ne tourne.** Aucun `IHostedService`, aucun `BackgroundService`, aucune minuterie, aucun
> ordonnanceur : le tableau des demandes RGPD de l'`Operator` est une **requête**, évaluée à chaque
> affichage.

Son motif est écrit juste dessous, et il est excellent : un processus de fond interrompu rendrait un
tableau des demandes RGPD vide et rassurant, et ferait dépendre la preuve de ce qu'un `cron` ait
tourné. Un retard non détecté deviendrait un retard inexistant.

Le second est `ScanLauncher`, livré par #299 :

```csharp
_ = Task.Run(
  () => RunAsync(progress, dialect, connectionString, abandon),
  CancellationToken.None);
```

Un scan part, la requête HTTP répond `303`, l'écran d'attente se rafraîchit, et le travail continue.
Il survit à l'échange qui l'a lancé, et le jeton d'annulation n'est délibérément ni celui de la
requête ni celui de l'hôte.

Un lecteur qui trouve ce `Task.Run` et le garde ci-dessus dans le même dépôt a le droit de croire que
l'un des deux a été oublié. Aucun des deux ne l'a été, mais rien nulle part ne l'écrit.

Un ADR est nécessaire parce que les trois critères sont réunis. La décision est difficile à défaire :
rendre le scan synchrone maintenant supposerait de reprendre l'écran d'attente, l'abandon et le
régime de progression, c'est-à-dire l'essentiel de la livraison. Elle est surprenante sans contexte,
au sens le plus littéral — deux textes du dépôt paraissent se contredire. Et elle résulte d'un
arbitrage réel : la frontière aurait pu passer ailleurs.

**Une note de glossaire ne suffisait pas.** La règle amendée est un garde de dépôt, qui porte sur les
quatre assemblages de production, pas sur le seul contexte `Screening`. La consigner dans le
glossaire d'un contexte l'aurait rendue invisible à qui lit le garde, et le prochain contexte qui
voudra faire durer un travail plus longtemps qu'un échange ne saurait pas où lire ce qui a déjà été
tranché.

## Décision

### 1. Ce que le garde interdit est ce qui part tout seul, pas ce qui court plus longtemps qu'un échange

C'est la frontière, et elle est la seule chose que cet ADR grave.

Un scan survit à la requête HTTP qui l'a lancé. Il est en revanche :

- **déclenché par un geste** — un `Operator` a rempli un écran et a cliqué ;
- **fini** — il a un terme, atteint ou manqué, jamais une échéance suivante ;
- **sans rattrapage** — il ne laisse derrière lui aucun travail qu'un processus devrait reprendre.

Ce que le garde refuse est ce qui possède la propriété inverse : un travail dont le déclencheur n'est
pas un humain. Un `IHostedService` démarre parce que l'hôte démarre. Une minuterie repart parce que le
temps passe. Un ordonnanceur part parce qu'une expression `cron` l'a dit. Aucun des trois n'a de geste
derrière lui, et c'est exactement pourquoi leur silence est indétectable : quand un travail que
personne n'a demandé ne se produit pas, personne ne l'attend.

Un scan qui ne part pas, lui, laisse un `Operator` devant un écran qui ne se remplit pas.

### 2. Trois formes tomberaient du mauvais côté, et on les nomme

Pour que la frontière serve à quelque chose, il faut dire ce qu'elle exclut :

- un scan **périodique**, qui rescannerait la base d'un client à intervalle fixe ;
- un scan **relancé automatiquement** après un échec ;
- une **reprise planifiée** d'un scan que l'arrêt du processus a interrompu.

⚠️ **La reprise est de toute façon impossible par construction**, et c'est le garde-fou de
l'[ADR-0012](./0012-la-connexion-le-scan-et-les-echantillons-entrent-dans-screening.md) qui la rend
telle : la chaîne de connexion ne survit pas au scan. Un processus qui redémarre n'a plus de quoi
rouvrir la base. Le seul chemin vers la reprise passerait par la persistance du secret d'accès, que
l'ADR-0012 déclare non négociable.

### 3. Le garde ne change pas, et sa liste non plus

Aucun nom n'est retiré de `WhatWouldRunOnItsOwn`, aucun de `SchedulingLibraries`. Le `Task.Run` du
`ScanLauncher` n'y a jamais figuré et n'a pas à y figurer : `Task.Run` ne déclenche rien, il déplace
sur un autre fil un travail que quelqu'un vient de demander.

On ne pose pas non plus de garde neuf pour « un seul `Task.Run`, ici ». Un garde qui autoriserait
nommément un site d'appel se périmerait au premier renommage et se lirait comme une dérogation, quand
ce qui est décidé est une règle. Ce que cet ADR laisse au dépôt est un texte à opposer en revue, pas
un test de plus.

## Conséquences

- **Le dépôt sait où passe la frontière**, et la prochaine demande de faire durer un travail se
  tranche en lisant le point 1 plutôt qu'en argumentant à neuf.
- **Le `Screening` porte, seul, un travail qui dépasse l'échange.** Aucun autre contexte n'en a, et
  celui qui en voudrait un devra passer par ici.
- **Le motif originel du garde reste intact** : le tableau des demandes RGPD demeure une requête,
  évaluée à chaque affichage. Rien de ce que cet ADR autorise ne s'en approche — un scan ne calcule
  aucun retard, ne remplit aucun tableau, et son échec est visible à l'écran de celui qui l'a lancé.
- **Un scan perdu au redémarrage est perdu, et c'est acquis.** L'écran d'attente le dit, l'`Operator`
  relance. C'est le prix de l'absence de reprise, et il est payé plutôt que la persistance du secret
  d'accès.

## Alternatives écartées

| Alternative | Motif du rejet |
| --- | --- |
| **Rendre le scan synchrone**, dans la requête HTTP | une lecture de schéma sur une base de production réelle dure des dizaines de secondes ; l'échange expirerait chez le premier intermédiaire venu, et un onglet fermé couperait une lecture déjà lancée sur la base d'un tiers |
| **Assouplir le garde** en retirant un nom de sa liste | ce n'est pas ce qui a changé. Aucun `IHostedService` n'entre, et le jour où l'un d'eux voudra entrer, c'est le garde intact qui doit le dire |
| **Ajouter un garde qui autorise nommément ce `Task.Run`** | se périme au premier renommage, et inscrit une dérogation là où il fallait une règle. Motif écrit au point 3 |
| **Une note dans le glossaire de `Screening`** | c'est ce qui avait été fait d'abord, et c'est ce que #311 a corrigé : la règle amendée est un garde de dépôt, pas une clause de contexte. Elle serait restée invisible à qui lit le garde |
| **Un `IHostedService` qui dépilerait une file de scans** | traverse exactement la frontière du point 1 : le travail partirait parce que l'hôte a démarré, et un scan silencieusement non dépilé redeviendrait indétectable |

## Ce que cet ADR n'ouvre pas

- **La liste du garde**, qui ne bouge pas.
- **La persistance de la chaîne de connexion**, qui reste interdite par l'ADR-0012 — et qui est la
  seule porte vers la reprise.
- **Le grain du scan** — un rapport courant par déploiement, un scan à la fois — que l'ADR-0012 a déjà
  tranché.
- **La reprise, la relance et la périodicité**, nommément écartées au point 2.
- **Les ADR 0001 à 0014**, qui ne sont pas édités.
