# ADR-0014 — `Screening` n'enregistre pas qui a arbitré

- **Statut** : accepté
- **Date** : 2026-08-25
- **Décidé par** : [#273](https://github.com/AmauryTISSOT/microservice_rgpd/issues/273), conséquences tranchées par [#283](https://github.com/AmauryTISSOT/microservice_rgpd/issues/283), porté par [#292](https://github.com/AmauryTISSOT/microservice_rgpd/issues/292)
- **Hors de la [carte #261](https://github.com/AmauryTISSOT/microservice_rgpd/issues/261)** : le retrait ne touche que l'écran d'arbitrage existant, ses deux commandes, le type et la base. Rien du chemin connecté.
- **Ne supplante aucun ADR.** [ADR-0003](./0003-troisieme-contexte-sans-intersection-et-garde-des-traversees.md) est tenu : `Casework` n'est pas touché, et aucune dépendance neuve n'apparaît entre les contextes.

## Contexte

Chaque arbitrage d'une `ScreenedColumn` exigeait trois champs entrant ensemble : l'état, **le nom
saisi par l'humain**, et la date. Le nom était obligatoire — une signature manquante n'était pas un
champ vide, c'était un arbitrage qui n'avait pas eu lieu — et il n'était **jamais authentifié** : le
service n'a aucune authentification, et le glossaire lui-même qualifiait ce nom de « saisi, jamais
vérifié ».

Deux contextes du même dépôt traitent donc la même question de façon opposée, et **aucun glossaire de
contexte ne peut expliquer cette asymétrie** puisqu'elle porte sur ce que deux contextes font
différemment. C'est la raison d'être de cet ADR.

**Les trois critères sont réunis.** La décision est **irréversible** : la donnée est détruite, et
rien ne la reconstitue. Elle est **surprenante sans contexte** : un lecteur qui trouve `Signatory`,
`SignatoryKind` et `SignerVerification` dans `Casework` et rien du tout dans `Screening` doit pouvoir
lire pourquoi. Et elle résulte d'un **arbitrage réel** : 686 frappes au clavier sur un rapport de
taille courante, contre une déclaration qu'aucun mécanisme ne vérifie.

## Décision

### 1. `Screening` n'enregistre pas qui a arbitré

L'`Arbitration` devient `(State, RenderedOn)`. `SignedBy` disparaît du type, de la base, des deux
commandes et de l'écran. `SignedOn` devient `RenderedOn` — la fabrique `Arbitration.Rendered` rime
désormais avec son propre champ.

⚠️ **L'invariant survit intact, et son raisonnement tient mot pour mot sur un duo** : *deux champs
qu'un chemin d'écriture peut dissocier finissent par se dissocier*. Il n'existe ni `Retained` ni
`SetAside` sans date, `Awaiting` reste par construction le seul état sans arbitrage, et une date
manquante n'est pas un champ vide mais un arbitrage qui n'a pas eu lieu. La contrainte de contrôle de
table `ck_screened_columns_arbitration` continue de le tenir jusque dans la base, réécrite en duo.

### 2. Ce que la trace doit prouver est qu'un humain a tranché — et la date le prouve

Un nom non authentifié ne prouve pas l'identité de la personne : il ne prouve que ce que la date
prouve déjà, à savoir **qu'un geste humain a eu lieu**. La machine, elle, ne signe pas et ne date
aucun arbitrage, puisqu'elle n'en rend aucun. C'est le geste, et non l'auteur nommé, qui sépare la
`Cartographie` du `Screening`.

### 3. Les noms déjà enregistrés sont détruits, sans recours

Une seule migration, nommée pour ce qu'elle détruit : `DROP COLUMN signed_by`, `RENAME COLUMN
signed_on` → `rendered_on`, contrainte réécrite en duo.

⚠️ **Y compris sur les rapports archivés.** Aucune autre colonne ne porte ces noms, l'`Arbitration`
n'a pas d'histoire, et il n'existe pas d'`EvidenceLog` dans ce contexte. **C'est assumé** : la donnée
détruite est une déclaration que le service n'a jamais vérifiée, sur des rapports qu'un dépôt neuf
détruit de toute façon en entier.

### 4. `Casework` n'est pas touché

`Casework` continue d'enregistrer son signataire, et le mot `signature` lui reste acquis en propre.
L'asymétrie est **voulue** et tient à une différence de nature : `Casework` produit des actes
opposables à un tiers, tracés dans un `EvidenceLog` dont le grain est le dossier ; `Screening` produit
une configuration interne, dont la trace est l'état courant seul, au grain du déploiement.

## Conséquences

- Le geste de lot **ne change pas** : il pose toujours *n* arbitrages individuels, jamais un état de
  lot, chaque colonne atteinte portant sa propre date de service.
- ⚠️ **La règle « aucun geste de lot ne porte sur une colonne signalée » perd son péage.** Signer 374
  fois à la main la rendait impraticable ; il ne reste que 374 clics sans friction, et le bouton
  unique deviendra tentant. Le prix n'a jamais été le motif — le nom n'en était que l'exécuteur
  incident. Le motif est écrit au même endroit que la règle, sur
  `ScreenedColumn.IsWithinReachOfABatchGesture`, là où le lira qui viendra proposer d'élargir le lot.
- Le mot `signature` reste _Avoid_ dans le glossaire de `Screening`, désormais **pour deux raisons
  cumulées** : il est pris par `Casework`, et il ne nomme plus rien ici.
- L'export de la cartographie passe de dix-sept à seize champs. **Rien à faire en code** : l'export
  décidé par [#269](https://github.com/AmauryTISSOT/microservice_rgpd/issues/269) n'est pas construit.

## Alternatives écartées

| Alternative | Pourquoi non |
| --- | --- |
| **Geler la colonne** — ne plus l'écrire, la garder lisible | fabriquerait volontairement ce que la documentation du type dénonce : une colonne peuplée sur les vieilles lignes et vide sur les neuves n'est plus une donnée, c'est une **date de déploiement déguisée en information métier**. Écartée nommément par [#283](https://github.com/AmauryTISSOT/microservice_rgpd/issues/283) |
| **Rendre le nom facultatif** plutôt que de le retirer | poserait très exactement le `string?` que le type refusait, et laisserait deux régimes cohabiter sur la même colonne sans que rien ne dise lequel s'applique |
| **Authentifier l'`Operator`** pour que le nom veuille dire quelque chose | est une décision d'un tout autre ordre, qui n'est ni ticketée ni tranchée. L'attendre bloquerait ce retrait derrière elle |
| **Attacher le retrait à la livraison de la [carte #261](https://github.com/AmauryTISSOT/microservice_rgpd/issues/261)** | la contrainte « une seule livraison » de la carte protège l'`Operator` d'un demi-workflow, pas d'une simplification autonome. Attaché, ce retrait serait bloqué derrière des décisions qui ne sont pas encore ticketées |

## Ce que cet ADR n'ouvre pas

- **L'authentification de l'`Operator`**, ni la question « qui a le droit d'arbitrer ». Le service
  n'en a aucun mécanisme, et cet ADR n'en crée pas.
- **`Casework`**, son `Signatory`, son `SignatoryKind`, sa `SignerVerification` et son
  `EvidenceLogEntry`, qui ne bougent pas d'une ligne.
- **Une histoire des arbitrages.** Il n'y en avait pas, il n'y en a toujours pas : un second
  arbitrage écrase le premier, et la trace **est** l'état courant seul.
- **Le grain du `Screening`**, qui reste le déploiement, et son absence d'état, qui reste entière.
- **La [carte #261](https://github.com/AmauryTISSOT/microservice_rgpd/issues/261)** et le chemin
  connecté, dont rien n'est touché ici.
