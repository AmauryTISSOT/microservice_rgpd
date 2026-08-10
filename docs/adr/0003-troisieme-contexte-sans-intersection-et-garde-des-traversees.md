# ADR-0003 — Un troisième contexte borné sans aucune intersection, et un garde étendu à toutes les traversées

- **Statut** : accepté
- **Date** : 2026-08-09
- **Décidé par** : [ADR : le troisième contexte borné, son découpage et son garde d'architecture](https://github.com/AmauryTISSOT/microservice_rgpd/issues/133)
- **Carte** : [Dépister les colonnes porteuses de données personnelles dans une base du client](https://github.com/AmauryTISSOT/microservice_rgpd/issues/122)
- **Glossaires** : [`CONTEXT-MAP.md`](../../CONTEXT-MAP.md), [Screening](../contexts/screening/CONTEXT.md)
- **Ne supplante rien** : l'[ADR-0002](./0002-deux-contextes-bornes-et-noyau-partage.md) reste en vigueur, voir *Portée*

## Contexte

L'ADR-0002 a découpé le dépôt **par le temps** : l'instant du verdict d'un côté, la durée de
l'instruction de l'autre, et un noyau partagé d'un seul type entre les deux. Il a posé son garde
**avant** le contexte qu'il gardait — *« la première ligne de `Casework` naîtra déjà sous
surveillance »*.

La carte [#122](https://github.com/AmauryTISSOT/microservice_rgpd/issues/122) ouvre un troisième
temps. Un `Operator` colle le relevé des colonnes d'une base du client ; le service lui rend, colonne
par colonne, une catégorie de données présumée et un motif en prose, qu'il retient ou écarte. Ce
travail vit **avant** les deux autres : à la configuration, quand aucune demande n'existe encore, et
il ne parle pas du tout des droits que le RGPD ouvre aux personnes concernées.

[#126](https://github.com/AmauryTISSOT/microservice_rgpd/issues/126) l'a nommé **`Screening`** et lui
a écrit son glossaire, son régime d'erreur propre — l'`Omission relue` — et ses deux bornes,
`Suggéré, jamais déclaré` et `Le nom, jamais la valeur`. Il a aussi établi le fait qui commande cet
ADR : **`Screening` n'a aucune intersection avec les deux autres contextes, pas même le noyau
partagé.**

## Décision

**Un troisième contexte borné sans aucune intersection, et un garde qui s'énonce désormais par ce
qu'il permet plutôt que par ce qu'il interdit.**

1. **`Screening`** — le temps d'avant. Il vit à la configuration, quand aucune demande n'existe.
2. **`Separate Ways` intégral.** Rien ne traverse entre `Screening` et les deux autres : ni type
   partagé, ni fournisseur amont, ni identifiant opaque comme le `qualificationId` que porte un
   `Case`.
3. **`Screening` ne touche pas au noyau partagé.** Il ne rattache jamais une colonne à un
   `DataSubjectRight`. C'est le premier contexte du dépôt dans ce cas, et c'est **gardé**, pas
   seulement écrit.
4. **Sa taxonomie, `PersonalDataCategory`, n'entre pas au `SharedKernel`** et lui appartient en
   propre.
5. **Le garde s'énonce en liste blanche.** Aucun contexte n'en atteint un autre, **sauf** les deux
   traversées écrites ici en toutes lettres :

   | | → `Qualification` | → `Casework` | → `Screening` | → `SharedKernel` |
   | --- | --- | --- | --- | --- |
   | **`Qualification`** | — | interdit | interdit | **permis** |
   | **`Casework`** | interdit | — | interdit | **permis** |
   | **`Screening`** | interdit | interdit | — | interdit |
   | **`SharedKernel`** | interdit | interdit | interdit | — |

   Le contrôle reste au niveau de l'**IL**, sur les quatre assemblages de production, et il est posé
   **avant** le contexte qu'il garde.

6. **La liste des contextes que le garde connaît est ancrée sur `docs/contexts/*/`.** Un nom qui ne
   désigne aucun `CONTEXT.md`, ou un `CONTEXT.md` qui n'entre pas dans la matrice, fait rougir la
   suite. `SharedKernel` en est l'exception écrite avec son motif : ce n'est pas un contexte, c'est
   ce que des contextes partagent.

## Justification

**Pourquoi un contexte, et pas un ajout à `Casework`.** Le motif positif est celui de l'ADR-0002
appliqué une fois de plus : le dépôt se découpe par le temps, et ce travail vit avant. Le motif
négatif pèse plus lourd. `Casework` porte le `Manifest`, dont la doctrine est que le grain déclaré
est le **système**, jamais le champ. Loger dans `Casework` un objet qui assume le grain de la colonne
ferait corroder par simple voisinage la doctrine qui l'y refuse : quelqu'un finirait par écrire le
pont, et un `Manifest` pré-rempli par une machine **se lirait comme complet** — l'`Omission
silencieuse` sous sa forme la plus dangereuse. Le contexte séparé rend l'absence de pont
**vérifiable**, exactement comme l'ADR-0002 l'a fait pour l'optionnalité de la `Qualification`.

**Pourquoi aucune intersection, pas même le noyau partagé.** Le critère d'entrée au noyau est écrit
depuis l'ADR-0002 : n'y entre que ce qui est vrai de tous les côtés **sans exception**.
`DataSubjectRight` échoue au test pour `Screening`, et pas faute de place : une colonne « courriel »
ne relève pas d'un droit plutôt qu'un autre, elle relève de tous. L'y raccrocher ferait peser une
troisième dépendance sur le noyau sans rien apporter. Le fait notable est qu'un contexte puisse
n'avoir **rien** en commun avec les autres alors que le noyau était jusqu'ici ce qu'ils avaient en
commun ; et un fait notable qui n'est pas vérifiable n'est qu'une phrase — d'où la règle 3.

⚠️ Le mot `Operator` est le contre-exemple qui montre ce que la règle protège : `Screening` et
`Casework` le portent tous les deux **sans partager aucun type**. L'identité de mot n'est pas une
identité de modèle, et factoriser sur elle serait la première fissure.

**Pourquoi les deux sens.** L'ADR-0002 n'a gardé qu'un sens parce qu'une seule direction était
tentante. Ici les deux le sont, et elles ne se ressemblent pas. De `Screening` vers `Casework`, un
écran d'arbitrage voudrait dire « cette colonne est déjà couverte par un `DeclaredSystem` » —
c'est la confrontation au `Manifest` que la carte refuse. De `Casework` vers `Screening`, un écran de
déclaration voudrait afficher « douze colonnes suspectes ont été dépistées » à côté du formulaire —
c'est le pré-remplissage déguisé, et c'est la corrosion par voisinage sous sa forme exacte. Ne garder
que le premier sens reviendrait à garder celui dont on n'a pas peur.

**Pourquoi le garde s'énonce à l'envers.** Une liste d'interdits ne protège que ce qu'on a pensé à y
écrire. Six interdits énumérés auraient laissé quatre traversées ouvertes en silence —
`Qualification → Casework`, et les trois par lesquelles le noyau partagé aurait pu se mettre à
dépendre d'un contexte — et un quatrième contexte serait né **non gardé** jusqu'à ce que quelqu'un
pense à allonger la liste. La liste blanche inverse la charge : rien ne traverse sauf preuve écrite,
un contexte neuf entre interdit partout, et c'est à lui d'écrire sa dérogation. Elle rend aussi
l'absence totale d'intersection de `Screening` lisible d'un coup d'œil — une ligne de la matrice sans
un seul « permis » — au lieu d'être six règles qu'il faut recouper.

⚠️ **Elle est verte le jour où on la pose**, vérifié avant de la choisir : aucune ligne de code ne
franchit aucune des dix traversées interdites. Elle ne coûte donc aucune correction, et les deux
règles qu'elle apporte en plus de ce que le ticket demandait — l'instant qui ne connaîtra pas la
durée, le noyau qui ne dépendra d'aucun contexte — sont acquises gratuitement. `SharedKernelTests`
contrôle **qui habite** le noyau ; rien ne contrôlait **ce que le noyau atteint**.

**Pourquoi l'ancrage sur `docs/contexts/*/`.** Le garde est vert par vacuité tant que `Screening` n'a
pas de code, et c'est voulu. Mais l'appartenance d'un type à un contexte se lit à son espace de noms,
par **préfixe** — parce que le dossier dit `Qualifications` là où le contexte se nomme
`Qualification`. Un nom mal écrit dans le garde, un pluriel de trop, et la règle ne trouverait
**jamais** rien : le vert d'aujourd'hui, légitime, et le vert de demain, mensonger, seraient le même
vert. Ancrer la liste sur des `CONTEXT.md` qui existent déjà fait rougir la faute de frappe le jour
où on l'écrit, et fait rougir le quatrième contexte le jour où son glossaire arrive sans sa ligne
dans la matrice.

**Pourquoi cet ADR ne supplante pas l'ADR-0002.** Son titre — « **Deux** contextes bornés » — est
**daté, pas faux** : il décidait de ce qui existait alors. Le supplanter obligerait à recopier cinq
décisions encore vivantes qu'il porte seul — la séparation par le temps, le noyau d'un seul type, le
`qualificationId` opaque, `Capability` hors du noyau, l'optionnalité de la `Qualification` — et ce
qu'on oublierait de recopier mourrait en silence. Le précédent est celui de l'ADR-0001, qui « précède
le découpage », n'a pas été réécrit, et reste vrai sur sa portée : on supplante un ADR, on ne l'édite
pas.

⚠️ Cet ADR **durcit** la portée du garde de l'ADR-0002 sans rouvrir aucune de ses décisions. La règle
`Casework → Qualification` qu'il avait posée est reprise telle quelle dans la matrice, avec son
motif ; elle n'est ni relâchée ni renégociée, elle cesse seulement d'être seule.

## Conséquences

**Acquis**

- L'absence totale d'intersection de `Screening` cesse d'être une intention : elle se lit dans une
  matrice et se vérifie à chaque exécution de la suite.
- Un quatrième contexte est gardé **à son arrivée**, sans geste — et son `CONTEXT.md` ne peut pas
  entrer sans que quelqu'un écrive sa ligne.
- Deux règles que personne n'avait demandées, acquises sans coût : `Qualification` n'atteindra pas
  `Casework`, et le noyau partagé n'atteindra aucun contexte.
- `Screening` se construit et se démontre sans `Casework`, sans `Manifest`, sans base de dossiers —
  au même titre que `Casework` se démontrait sans GPU.

**Coûts et contraintes**

- **Rattacher une colonne à un `DataSubjectRight` devient un geste de niveau ADR.** C'est le prix
  voulu : la décision de cadrage 4 de la carte le refuse déjà, et le refus était jusqu'ici une phrase
  que rien ne tenait.
- **Tout `CONTEXT.md` neuf oblige à écrire sa ligne dans la matrice avant d'aller plus loin.** C'est
  bruyant à dessein.
- **`SharedKernel` est l'exception nommée** de l'ancrage : il n'a pas de `CONTEXT.md` et n'en aura
  pas. Son motif est écrit dans le test plutôt que laissé à deviner.
- **La frontière reste détectable, pas impossible.** Seuls des assemblages séparés la rendraient
  infranchissable ; le troc assumé par l'ADR-0002 est repris tel quel — « bruyant » est le critère.

**Risque assumé, nommé**

⚠️ **Le garde naît vert par vacuité : il n'existe aucun `src/*/Screening/`, dans aucune des quatre
couches.** C'est le geste même de l'ADR-0002 — poser le garde avant le contexte, pour que la première
ligne naisse déjà sous surveillance — et c'est aussi son risque. L'ancrage sur `docs/contexts/*/`
attrape la faute de frappe sur le **nom**, il ne promet pas que le dossier de code portera ce nom-là.
La première PR qui écrira du `Screening` est le premier moment où la règle mordra vraiment.

⚠️ **Aucun témoin `Fixtures/Screening/` n'est écrit, et c'est délibéré.** `ContextInspectorTests`
retourne l'inspecteur contre son propre assemblage sur des témoins `Casework`/`Qualification` — une
fuite en corps de méthode, une fuite en signature, une fuite par argument d'attribut, un type qui ne
traverse rien. L'inspecteur est **paramétré** en `from`/`to` : les noms de contexte y sont de la
donnée. Trois témoins de plus prouveraient une seconde fois la même mécanique, pas la règle neuve.

## Alternatives écartées

| Alternative | Motif du rejet |
| --- | --- |
| **Un ajout à `Casework`** | ferait corroder par voisinage la doctrine du grain du `Manifest` ; quelqu'un finirait par écrire le pont, et un `Manifest` pré-rempli se lit comme complet |
| **Liste noire des paires interdites** | ne protège que ce qu'on a pensé à y écrire : quatre traversées restaient ouvertes en silence, et un quatrième contexte serait né non gardé |
| **Garde à sens unique**, sur le modèle littéral de l'ADR-0002 | garderait le sens dont on n'a pas peur ; la corrosion que le découpage refuse part de `Casework`, pas de `Screening` |
| **`Screening` autorisé à toucher le noyau partagé** | « aucune intersection » redeviendrait une phrase ; le rattachement à un `DataSubjectRight` est déjà refusé par la carte |
| **`PersonalDataCategory` au noyau partagé** | échoue au critère « vrai de tous les côtés sans exception » : son auteur n'est pas le RGPD, et aucun des deux autres contextes n'en a l'usage |
| **ADR-0003 supplante l'ADR-0002** | obligerait à recopier cinq décisions encore vivantes ; ce qu'on oublierait de recopier mourrait en silence |
| **Témoins `Fixtures/Screening/`** | prouveraient une seconde fois l'inspecteur, jamais la règle : l'inspecteur est paramétré, les noms y sont de la donnée |
| **Ancrage sur `src/*/Screening/`** | le dossier n'existe pas encore : le test serait rouge à sa naissance, ou obligerait à créer des dossiers vides pour lui faire plaisir |
| **Liste de contextes « actuellement vides » qui expire** | ne rattrape pas la faute de frappe — un nom erroné compte zéro type pour toujours, et le test reste vert : la mitigation ne mitige rien |
| **Un test par paire interdite** | douze méthodes se recopieraient, et une paire oubliée ne se verrait pas ; une matrice se lit d'un coup et son exhaustivité se vérifie |

## Portée de cet ADR

Cet ADR décide **de quoi le service est fait** — le critère posé par la « Portée » de l'ADR-0001 : un
troisième contexte, ses relations, et la règle qui les tient. Le reste de ce que la carte #122 a
tranché décrit **ce que fait** le service et n'en relève pas : la taxonomie fermée des catégories
([#127](https://github.com/AmauryTISSOT/microservice_rgpd/issues/127)), le format pivot d'entrée
([#128](https://github.com/AmauryTISSOT/microservice_rgpd/issues/128)), la clause d'incomplétude
([#132](https://github.com/AmauryTISSOT/microservice_rgpd/issues/132)) et le contrat des endpoints
vivent dans les `CONTEXT.md` et dans les commentaires de résolution de leurs tickets, qui font foi
sur le *pourquoi*.

⚠️ **L'emplacement du moteur de dépistage n'appartient pas à cet ADR.** Second sidecar Python ou C#
en `Infrastructure` : la décision est conditionnée à un banc mesuré qui n'a pas eu lieu, et elle aura
le sien ou un supplément à celui-ci. Ce que la matrice tranche, elle, vaut quel que soit le résultat
du banc : où que le moteur atterrisse, aucun type de `Screening` n'atteindra `Casework` ni
`Qualification`.

⚠️ L'[ADR-0002](./0002-deux-contextes-bornes-et-noyau-partage.md) **reste en vigueur**. Son titre est
daté, ses décisions ne le sont pas. Celui-ci en étend la portée du garde ; il n'en révise aucune
clause.

## Suite — ce que la première ligne de `Screening` a montré (2026-08-10)

Le « risque assumé » ci-dessus disait : *la première PR qui écrira du `Screening` est le premier
moment où la règle mordra vraiment*. Elle a mordu, et sur autre chose que ce qui était prévu.

⚠️ **Le garde lisait le paquet `Ardalis.SharedKernel` comme le noyau partagé du dépôt.**
L'appartenance à un contexte se lit au **segment d'espace de noms**, et le segment `SharedKernel` de
la bibliothèque d'où vient le marqueur `IAggregateRoot` répond au même test que
`MicroserviceRgpd.Core.SharedKernel`. Le faux positif existait depuis toujours et **ne pouvait pas se
voir** : les deux contextes qui portaient du code ont tous deux la traversée vers le noyau
**permise**, si bien qu'il y était couvert par une permission légitime. `Screening`, à qui elle est
refusée, se dénonce donc sur son premier agrégat — pour avoir implémenté le marqueur que les deux
autres implémentent. L'appartenance est désormais bornée aux espaces de noms du dépôt. **La liste
blanche n'a pas bougé et reste à deux lignes** : ce n'est pas une frontière élargie, c'est
l'inspecteur qui cesse de confondre un paquet NuGet avec un contexte.

⚠️ **Des témoins `Fixtures/Screening/` ont finalement été écrits**, contre la ligne « Alternatives
écartées » qui les refusait. Le motif du rejet tenait — *ils prouveraient une seconde fois
l'inspecteur, jamais la règle neuve* — et il ne couvre pas ce qu'ils font ici : ils tiennent la borne
ci-dessus **des deux côtés**, un agrégat portant le marqueur de bibliothèque qui ne doit **pas** être
vu, et un type atteignant `DataSubjectRight` qui doit **rester** vu. Sans le second, une borne posée
sur les espaces de noms pourrait tout éteindre sans que rien ne passe au rouge.

**Un garde de vacuité est ajouté** : la matrice doit trouver, pour chacun des noms qu'elle prétend
garder, au moins un type de production qui l'habite. `ContextRosterTests` ancre le **nom** sur un
glossaire ; il ne promet pas qu'un dossier de code le porte, et le vert d'un contexte pas encore
écrit était indiscernable du vert d'un garde qui ne trouve rien.

**Le dossier de code est `Screenings/`, au pluriel** — un type `Screening` dans un espace de noms
`Screening` est un piège de résolution de noms, et `Qualifications/` avait déjà tranché pareil. La
lecture par préfixe de l'inspecteur, écrite pour ce cas, l'absorbe sans changement. Voir
`CONTEXT-MAP.md`, § *Où vivent les contextes dans le code*.
