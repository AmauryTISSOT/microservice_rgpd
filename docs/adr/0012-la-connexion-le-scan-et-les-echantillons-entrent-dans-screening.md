# ADR-0012 — La connexion, le scan et les échantillons de valeurs entrent dans `Screening`

- **Statut** : accepté
- **Date** : 2026-08-24
- **Décidé par** : [Carte — Le `Screening` se connecte, scanne et prélève](https://github.com/AmauryTISSOT/microservice_rgpd/issues/261), et le premier ticket qu'elle a ouvert : [#262](https://github.com/AmauryTISSOT/microservice_rgpd/issues/262)
- **Renverse une clause de glossaire**, pas un ADR : `Aucune donnée réelle n'entre`, dans [le glossaire de `Screening`](../contexts/screening/CONTEXT.md). **Cet ADR ne réécrit pas le glossaire** — il décide, il ne nomme pas. Le sort exact de cette clause, des deux mots réadmis et des deux termes à nommer relève de [#263](https://github.com/AmauryTISSOT/microservice_rgpd/issues/263).
- **Ne supplante aucun ADR.** [ADR-0004](./0004-moteur-de-depistage-en-csharp-sans-second-sidecar.md) est **tenu**, pas rouvert : la détection par la forme des valeurs est de la reconnaissance de motifs déterministe, donc du C# dans `Infrastructure`. [ADR-0005](./0005-design-language-documente-police-embarquee-et-fichiers-statiques.md) et [ADR-0009](./0009-la-navigation-passe-en-panneau-lateral-repliable-sans-javascript.md) sont tenus **sans exception** : l'avancement du scan est un écran d'attente rafraîchi en `<meta http-equiv="refresh">`, la barre est une largeur CSS. [ADR-0003](./0003-troisieme-contexte-sans-intersection-et-garde-des-traversees.md) est tenu : aucune dépendance neuve entre `Screening` et les deux autres contextes.

## Contexte

`Screening` a été bâti sur une abstention, et cette abstention était une clause de glossaire écrite
en toutes lettres :

> **Aucune donnée réelle n'entre.** Le service lit des noms de tables et de colonnes, des types, des
> contraintes et des commentaires. Aucune donnée personnelle réelle n'entre, et **il n'existe aucun
> chemin par lequel elle entrerait** : pas de chaîne de connexion, pas de socket vers la production
> du client, pas d'échantillon de valeurs, pas de sondage.

L'`Operator` exécutait la requête que le service lui fournissait, collait le `ColumnListing` obtenu,
et le service détectait à partir du **nom** de chaque colonne. Trois interdictions tenaient ensemble
et se justifiaient l'une l'autre.

**Les trois tombent d'un coup**, et c'est ce que cet ADR tranche. Le workflow demandé est celui-ci :
l'`Operator` choisit son SGBD, saisit une chaîne de connexion, le service **scanne** — relève le
schéma et prélève quelques valeurs par colonne —, rend un rapport de détection, l'`Operator` arbitre,
puis exporte sa **cartographie**.

**Un ADR est nécessaire parce que les trois critères sont réunis.** La décision est difficile à
défaire — pour une raison qui n'est pas celle qu'on croit, et qui est écrite au point 6. Elle est
surprenante sans contexte : un lecteur qui trouve dans le glossaire une interdiction formulée avec
« il n'existe aucun chemin » et dans le code un client PostgreSQL doit pouvoir lire pourquoi. Et elle
résulte d'un arbitrage réel, entre une doctrine qui protégeait quelque chose de précis et un gain
qu'on ne sait pas encore mesurer.

**Ce qui a rouvert la question n'est pas une envie de fonctionnalité**, mais un constat inscrit dans
le corpus lui-même. Le [registre des limites de méthode](../../corpus/schemas/limites-de-methode.md)
recense les colonnes dont on sait par ailleurs qu'elles portent davantage que leur nom ne dit :
`sacoche_saisie.saisie_note` porte le résultat scolaire nominatif d'un élève mineur ;
`sacoche_livret_modaccomp.livret_modaccomp_code` porte des PAI et des PPS, données de santé au sens
de l'article 9. Le nom, seul, ne les atteint pas. Le registre existait déjà, en creux, comme la liste
de ce que le régime schéma-seul renonçait à voir.

## Décision

### 1. La connexion, le scan et le prélèvement de valeurs entrent — et le collage reste

Les trois interdictions de la clause tombent. Le service peut **se connecter** à une base du client,
**relever** son schéma, et **prélever** quelques valeurs par colonne.

⚠️ **La connexion s'ajoute au collage ; elle ne le remplace pas.** Les deux chemins produisent un
`ColumnListing` au **même format pivot** — rien en aval ne bouge. Le motif est opérationnel et il est
décisif : beaucoup d'`Operator` n'obtiendront jamais l'ouverture d'un port depuis le microservice
vers leur production. Un service qui n'aurait plus que la connexion serait inutilisable là où il est
le plus utile.

**Deux garde-fous survivent au renversement, et ils ne sont pas négociables.**

- **La chaîne de connexion ne survit pas au scan.** Elle vit en mémoire le temps du relevé, n'est
  jamais persistée, jamais journalisée, jamais tracée, jamais reprise dans un message d'erreur. Le
  service ne détient **aucun secret d'accès durable**.
- **Les valeurs échantillons ne sont jamais enregistrées.** Elles vivent dans un cache **en mémoire
  du processus**, indexé par `ScreeningId`, avec une durée de vie explicite, affichée et décomptée.
  Un redémarrage du service les efface. Elles n'apparaissent **ni** dans un log, **ni** dans une
  trace, **ni** dans un message d'erreur, **ni** dans l'export.

Ces deux garde-fous sont ce que la clause renversée protégeait **et qui reste vrai**. Les jeter avec
elle serait l'erreur exacte que cet ADR existe pour empêcher : sans eux, rien n'interdirait, dans six
mois, d'enregistrer la chaîne de connexion « pour le confort de l'utilisateur ».

### 2. Le service lit le nom de la colonne, **le nom de sa table**, et ses valeurs

La détection s'élargit à trois sources, et l'ordre dans lequel elles sont nommées ici n'est pas
neutre.

**Le nom de la table est déjà là, et il n'a jamais été lu.** Le format pivot porte `table` et
`commentaire_table` depuis toujours ; le collage les transmet déjà. Le registre des limites de
méthode dit du plus lourd de ses cas — OpenEMR, le schéma le plus sensible du corpus — que « le
caractère `HealthData` est porté par le **nom de la table**, très rarement par celui de la colonne
(`code`, `value`, `date`) », et qu'« un moteur qui ne lit que la colonne rate la majorité du schéma
le plus sensible du corpus ».

⚠️ **Cette source est gratuite : elle ne rouvre aucune clause, n'exige aucune connexion, et
n'introduit aucune donnée personnelle réelle.** Elle est donc **retenue en propre**, et non comme un
repli. Elle est retenue **d'abord**.

**Les échantillons de valeurs ne paient que le reliquat.** Ce que la table n'atteint pas et que les
valeurs atteignent, c'est une classe précise : la colonne au nom **sincère mais divergent** (`ref_2`
qui contient des courriels), et la colonne **fourre-tout** dont ni le nom ni la table ne disent le
contenu. C'est ce reliquat, et lui seul, qui justifie le prix payé par cet ADR.

⚠️ **Écrire l'inverse serait malhonnête et l'ADR serait attaqué dessus.** Si les valeurs étaient
présentées comme le seul remède aux angles morts du nom, le premier lecteur sérieux répondrait :
« vous avez ouvert la porte aux données personnelles réelles pour un gain que le nom de la table
donnait gratuitement ».

**Ce n'est pas une confirmation d'un diagnostic déjà posé par le lexique** : c'est le même périmètre
de détection, élargi. Le moteur détecte à partir du nom **et** des valeurs, pas à partir du nom
**puis** des valeurs.

### 3. Le corollaire « ce n'est donc pas un NER » **se déplace** — il ne tombe pas

La clause renversée portait un corollaire :

> **Corollaire contre-intuitif, et il faut le dire : ce n'est donc pas un NER.** Un NER s'entraîne sur
> de la prose, et `dt_naiss` n'est pas de la prose.

Sa prémisse tombe : une valeur de colonne `commentaire` ou `contenu`, elle, **est** de la prose. Le
corollaire, lui, **reste vrai pour une autre raison**, et c'est cette raison qu'il faut écrire, faute
de quoi son maintien se lira comme un oubli.

**Ce que le moteur reconnaît, ce sont des formes, pas des entités nommées en contexte.** Un courriel,
un IBAN, un NIR, un numéro de téléphone français, une date : chacun est un **motif**, reconnaissable
par sa morphologie, sans modèle et sans corpus d'entraînement. `+33612345678` est un motif ;
« Madame Dupont a téléphoné » est une entité nommée dans une phrase, et cet ADR n'y touche pas.

⚠️ **`NER` reste sur la liste _Avoid_.** Le service que rendait le corollaire — empêcher qu'on aille
chercher des outils calibrés pour un problème qu'on n'a pas — est intact, et il est même plus utile
qu'avant : c'est maintenant qu'un lecteur, voyant des valeurs textuelles entrer, aura le réflexe d'y
penser. C'est aussi ce qui tient l'[ADR-0004](./0004-moteur-de-depistage-en-csharp-sans-second-sidecar.md)
sans le rouvrir : de la reconnaissance de motifs déterministe est du C# dans `Infrastructure`, pas un
second sidecar. `RuleStrength` reste externe et déterministe ; aucun ratio ne désigne un gagnant.

### 4. Le service devient un traitement — l'ADR en donne les faits, **pas la base légale**

Un microservice RGPD qui lit quelques valeurs de données personnelles réelles par colonne effectue un
**traitement de données à caractère personnel**. Ce fait est acquis et l'ADR le nomme.

**Ce que cet ADR écrit, parce que nous seuls le savons :**

| Fait | Ce qui est décidé |
| --- | --- |
| **Ce qui entre** | Quelques valeurs par colonne, prélevées par une requête aux colonnes **nommées explicitement** depuis le schéma déjà relevé. Types binaires **exclus** du prélèvement. Textes longs tronqués par le SGBD. |
| **Où cela vit** | En **mémoire du processus**, indexé par `ScreeningId`. Jamais sur disque, jamais en base, jamais dans un fichier temporaire. |
| **Combien de temps** | Une durée de vie **explicite, affichée et décomptée à l'écran**. Un redémarrage du service les efface. |
| **Ce qui n'en sort jamais** | Aucun log, aucune trace, aucun message d'erreur, aucun export — ni JSON, ni CSV. |
| **Ce qui est conservé** | La **cartographie** : ce que l'`Operator` a retenu ou écarté, nommé et daté. Aucune valeur échantillon n'y figure. |
| **Ce que le déployeur doit inscrire à son registre** | L'existence de ce traitement, sa finalité telle qu'il la définit, les catégories de données atteintes (toutes celles présentes dans les colonnes scannées), la durée de conservation ci-dessus, et l'absence de destinataire externe. |

**Ce que cet ADR n'écrit pas, délibérément : la base légale et la finalité.** Elles relèvent du
déployeur, qui seul connaît son secteur, son contrat et ses obligations. Le service ne les connaît
pas et ne doit pas prétendre les connaître.

⚠️ **C'est l'`Aide à la décision` appliquée au juridique.** Une base légale écrite par nous serait
recopiée sur notre autorité par un DPO qui n'a aucun moyen de vérifier qu'elle vaut dans son cas ;
elle se lirait comme une conformité fournie, ce qui est l'`Omission silencieuse` en habit juridique.
Le service **signale**, l'humain **tranche** — ici aussi.

⚠️ **Le grain reste le déploiement**, et l'écran doit le dire au lancement du scan : un seul rapport
courant à la fois, donc scanner une seconde base fait **reculer** la première.

### 5. L'ADR grave un **ordre de grandeur**, pas un paramètre

Ce qui est décidé ici, c'est **quelques valeurs par colonne — cinq à la livraison**. Passer de cinq à
trois ou à huit ne rouvre pas cet ADR. Passer à cinq cents, si.

⚠️ **La frontière qui compte n'est pas le nombre, c'est la nature du geste.** En deçà, le service
montre à un humain un **aperçu** qu'il lit de ses yeux pendant qu'il arbitre. Au-delà, le service
**analyse le contenu** de la base du client et en tire des statistiques — ce qui n'est plus le même
traitement, et que le déployeur devrait redéclarer au registre décrit au point 4. C'est cette
frontière que l'ADR tient, et la franchir est ce qui le rouvre.

De même pour le cache : l'ADR grave la **propriété** — durée explicite, affichée, décomptée, effacée
au redémarrage — et **pas** sa valeur en minutes, qui relève de la spécification.

### 6. La vraie irréversibilité n'est ni le code ni cet écrit — c'est le **recalibrage du moteur**

C'est le point le plus important de cet ADR, parce que c'est celui que personne ne voit venir.

Ce qui est facile à défaire :

- **Le code de connexion.** Quelques classes d'`Infrastructure` et les pilotes par dialecte. Une
  session de travail.
- **Cet écrit.** Un ADR se supersède ; c'est le mécanisme même.

**Ce qui est difficile à défaire, et invisible pendant qu'il se produit :** le moteur, une fois les
valeurs disponibles, apprendra à s'appuyer dessus. Le mécanisme est prévisible. Aujourd'hui le
lexique doit deviner `tel_dom` sur son seul nom, donc sa règle est **large** — elle signale beaucoup
et se trompe souvent, ce qui est assumé et porte le nom d'`Erreur relue`. Une fois les valeurs là,
cette règle large devient **bruyante** : elle signale aussi `tel_support`, qui est un numéro
d'assistance, et l'`Operator` s'en plaint. Le réflexe sera de **resserrer la règle lexicale**,
puisque la valeur rattrape. Retirez les valeurs un an plus tard : le lexique resserré, seul, détecte
**moins bien qu'avant qu'on ait commencé**.

⚠️ **On ne serait pas revenu au point de départ : on serait passé dessous, sans qu'aucune alerte ne
se déclenche.** Le `Screening` continue d'afficher toutes les colonnes — l'`Omission relue` tient —,
il en signale simplement moins. Le client qui n'a jamais pu ouvrir un port, et qui colle donc son
relevé comme avant, reçoit en silence un service dégradé.

**Clause de non-régression du chemin collé, opposable avant la livraison.** Le banc de
[#134](https://github.com/AmauryTISSOT/microservice_rgpd/issues/134)
(`exploration/banc-screening/`) mesure le moteur sur les six plis de
[#130](https://github.com/AmauryTISSOT/microservice_rgpd/issues/130), avec ses lexiques gelés et ses
prédictions versionnées. Le montage retenu y atteint **F2 macro 0,3172**.

> **Le moteur enrichi rejoue ce banc, chemin collé — nom et table, sans valeurs — et ne descend pas
> sous 0,3172. Ce rejeu est une condition de la livraison unique.**

Le motif est écrit ci-dessus : sans mesure, la contrainte des deux chemins coexistants devient un
mensonge, le second se dégradant en silence pendant qu'on regarde ailleurs. L'outil, les plis et la
vérité terrain existent déjà ; le coût du rejeu est de quelques minutes.

⚠️ **Le rejeu est manuel et nommé comme tel.** Le brancher en intégration continue supposerait de
câbler un banc Python sur un moteur C# : c'est un chantier réel, qui n'a pas à être décidé ici.

## Ce que nous n'avons pas mesuré

Cette section est le pendant obligatoire du point 2. **Le gain que cet ADR achète n'est pas chiffré,
et il ne pouvait pas l'être** — l'écrire est plus honnête que produire un chiffre qui n'en serait
pas un.

**Pourquoi la mesure est impossible aujourd'hui.** Le corpus de `corpus/schemas/` est
**schéma-seul** : ses pivots portent des noms, des types, des contraintes et des commentaires, et
**aucune valeur**. Il ne peut pas, par construction, mesurer ce que les valeurs feraient gagner. Le
banc qui le pourrait suppose des bases peuplées ; il reste à construire.

**Le seul proxy disponible, et ce qu'il vaut.** Sur les 3 182 colonnes annotées du corpus (témoin
exclu), 738 portent une catégorie de données personnelles. Parmi elles, **18 seulement** portent un
nom strictement muet — `code`, `value`, `note`, `contenu`, `label`, `message` et leurs semblables :
**2,4 % des colonnes personnelles**.

⚠️ **Ce proxy sous-estime, et il faut dire en quoi** : il ne voit ni la colonne au nom sincère dont
les valeurs divergent, ni la colonne fourre-tout, qui sont précisément les deux classes que les
valeurs existent pour attraper. Il ne le mesure pas — il en donne un plancher.

**L'objection que cet ADR ne sait pas réfuter.** Le verdict du banc de #134 est public : le moteur
retenu atteint **F2 macro 0,3172**, contre un **plafond d'annotation de 0,839** — ce qu'un humain
atteint sur le nom seul. Il restait donc une marge considérable **dans le régime schéma-seul**, et
cet ADR ouvre la porte aux données réelles sans l'avoir épuisée.

**Nous ne savons pas départager les deux marges.** Nous ne prouvons pas qu'elles portent sur des
colonnes disjointes ; l'affirmer serait exactement l'intuition déguisée en argument que cet ADR veut
éviter. C'est une part assumée du pari.

> **Condition d'annulation, posée d'avance :** si un banc futur, sur des bases peuplées, montre que
> les valeurs n'apportent rien que le nom et la table n'apportaient déjà, **cet ADR se supersède** —
> et la connexion redevient un chemin de commodité, non un chemin de détection.

## Conséquences

- **Le service devient responsable de données qu'il ne détenait pas.** Toute la surface — écrans,
  logs, traces, messages d'erreur, exports — doit désormais être relue avec la question « une valeur
  échantillon peut-elle passer par là ? ». C'est une charge d'entretien permanente, et c'est le prix
  réel de cette décision.
- **Le `Screening` acquiert un état transitoire**, alors que le glossaire dit qu'« un `Screening` n'a
  aucun état ». Le scan est asynchrone ; cet état vit **à côté** du `Screening`, jamais dessus.
- **Le prélèvement peut échouer là où le relevé du schéma ne le peut pas.** Le schéma reste **entier
  ou inexistant** — c'est ce sur quoi repose l'`Omission relue`. L'échec d'un prélèvement, lui, est
  **toléré à condition d'être nommé** sur la ligne : « valeurs non prélevées — type binaire »,
  jamais confondu avec « rien vu ».
- **SQLite est accepté par la connexion**, l'écran disant que le fichier doit être atteignable par le
  service. Le téléversement d'un fichier `.sqlite` est écarté : il ferait entrer la base entière.
- **L'`Omission relue` est intacte** et le reste : le rapport rend **toutes** les colonnes,
  `Unflagged` comprises et arbitrables. Aucun filtrage par défaut, nulle part.
- **`Aucune modification vers le Manifest` est intacte.** Ce qui est découvert le reste et n'accède
  jamais au statut de déclaration. La connexion ne rouvre pas ce pont, et le fait que le service ait
  désormais vu de vraies valeurs le rend **plus** important, pas moins.
- ⚠️ **Le risque de fuite se déplace vers la capture d'écran.** Les valeurs sont à l'écran pendant
  l'arbitrage ; ni le service ni cet ADR ne peuvent empêcher qu'on les photographie. C'est un motif
  de plus pour la durée de vie courte et affichée du point 4, et ce n'est pas une parade complète.

## Alternatives écartées

| Alternative | Motif du rejet |
| --- | --- |
| **Renoncer aux échantillons** et s'en tenir au schéma | c'est le régime actuel, et le registre des limites de méthode dit ce qu'il rate : les données de santé d'un livret scolaire, le résultat nominatif d'un élève mineur. Ces colonnes-là ne sont pas atteignables par le nom, quelle que soit la qualité du moteur |
| **Ne garder qu'un signal dérivé** — « 5/5 valeurs ont la forme d'un courriel » — sans jamais montrer les valeurs | séduisant, et **écarté pour une raison de fond** : le motif serait invérifiable. Le glossaire exige qu'une ligne signalée porte un motif qui **s'arbitre** ; « 5/5 valeurs ont la forme d'un courriel » demande à l'`Operator` de croire un comptage qu'il ne peut pas contredire. C'est le retour de la machine qui refuse de s'expliquer, écarté ailleurs par l'ADR-0011. Le dérivé sans les valeurs n'est d'ailleurs **pas moins** un traitement : il faut lire les données pour le calculer |
| **Persister les échantillons**, pour éviter de rescanner | fait du service un détenteur durable de données personnelles du client, avec tout ce que cela entraîne — chiffrement au repos, purge, droit d'accès sur nos propres sauvegardes. Le gain — ne pas rescanner — est disproportionné au prix. C'est le garde-fou du point 1, et il n'est pas négociable |
| **Téléverser le fichier SQLite** plutôt que de s'y connecter | ferait entrer la **base entière**, et non quelques lignes par colonne. Exactement le renversement que cet ADR refuse de faire |
| **Remplacer le collage par la connexion** | rendrait le service inutilisable chez les clients qui n'obtiendront jamais l'ouverture d'un port vers leur production — c'est-à-dire beaucoup, et souvent les plus régulés |
| **Lire seulement le nom de la table**, sans les valeurs | **n'est pas une alternative : c'est retenu en propre**, au point 2, et retenu d'abord. Ce qui est écarté, c'est de s'y **arrêter** — la table n'atteint ni la colonne au nom divergent, ni le fourre-tout |
| **Attendre le banc sur bases peuplées** avant de trancher | bloquerait toute la carte sur un banc qui n'existe pas et dont la construction suppose la connexion qu'il devrait justifier. Le pari est assumé et sa condition d'annulation est écrite ci-dessus |
| **Un ADR séparé pour la qualification juridique** | les faits juridiques du point 4 sont la **conséquence directe** des trois interdictions levées ; séparés, ils se liraient comme une note d'accompagnement, et le lecteur qui trouve le client PostgreSQL n'arriverait jamais jusqu'à eux |

## Ce que cet ADR n'ouvre pas

- **Le glossaire de `Screening`.** Cet ADR décide, il ne nomme pas. Le sort de la clause
  `Aucune donnée réelle n'entre`, celui des mots `scan` et `cartographie`, et les termes à nommer
  relèvent entièrement de [#263](https://github.com/AmauryTISSOT/microservice_rgpd/issues/263).
- **Le pont `Screening` → `Manifest`**, qui reste interdit. Aucun pré-remplissage, aucun export,
  aucune confrontation, aucun bouton.
- **Le grain « base connectée »** — un rapport courant par base plutôt qu'un par déploiement, et la
  comparaison de deux relevés de la même base dans le temps. Le grain reste le **déploiement**.
- **Qui peut faire se connecter le service, et où** — authentification de l'`Operator`, liste d'hôtes
  autorisés, journalisation de l'hôte atteint. Hors du périmètre de cette décision.
- **[ADR-0004](./0004-moteur-de-depistage-en-csharp-sans-second-sidecar.md)**, que le point 3 tient
  plutôt qu'il ne rejoue. `IScreeningEngine` existe pour que ce choix reste réversible sans que
  `Core` ne bouge ; il n'a pas à être rouvert ici.
- **Le format pivot**, qui ne change pas : les deux chemins d'entrée produisent le même
  `ColumnListing`, et rien en aval ne bouge.
- **Le contenu de l'export**, qui porte tout le rapport, la clause d'incomplétude en en-tête, et
  **aucune** valeur échantillon.
- **Les ADR 0001 à 0011**, qui ne sont pas édités.
