# Verdict du banc — le moteur de dépistage est tranché

Ticket [#134](https://github.com/AmauryTISSOT/microservice_rgpd/issues/134), en exécution du
protocole de [#130](https://github.com/AmauryTISSOT/microservice_rgpd/issues/130) (amendé par
#145, #150, #154), daté du 2026-08-10. Les chiffres sortent de
[`resultats.json`](./resultats.json) et [`cout.json`](./cout.json) ; les prédictions sont
versionnées par montage et par pli dans [`predictions/`](./predictions/) — le verdict est
vérifiable sans le banc qui l'a produit.

## Le verdict, lu mécaniquement

Critère de #130 : borne basse de l'intervalle à 95 % de l'écart apparié par schéma
(F2 macro montage − ligne de base, bootstrap de grappe sur les six schémas, prédictions
hors-pli, pire graine pour le modèle CPU) **strictement positive**, et aucune cause close de
« non concluant ».

| montage | F2 macro | F2 micro | écart apparié, IC 95 % | verdict |
|---|---:|---:|---|---|
| ligne de base triviale | 0,0642 | 0,0252 | — (référence) | — |
| règles + lexique FR | 0,2138 | 0,1042 | [+0,0706 ; +0,2467] | **OUI** |
| règles + lexique EN | 0,2891 | 0,1771 | [+0,1560 ; +0,2825] | **OUI** |
| règles + lexique FR+EN | **0,3172** | **0,1963** | [+0,1833 ; +0,3136] | **OUI** |
| modèle CPU (pire graine : 1) | 0,2464 | 0,1863 | [+0,1280 ; +0,2486] | **OUI** |

**Les quatre montages franchissent la barre.** Aucun intervalle ne chevauche zéro.
La conséquence annoncée d'avance — « il est probable qu'aucun montage ne franchisse la
barre » — ne s'est pas produite : la ligne de base triviale est battue par tout ce qui
découpe un identifiant.

**La valeur absolue, en évidence et sans être jugée ici** (elle appartient à #131 devant un
rapport réel, et à #135) : le meilleur montage plafonne à **F2 macro 0,3172**, très loin du
plafond d'annotation (0,839 — voir plus bas). Un verdict OUI sur l'écart n'est pas un verdict
sur la valeur d'usage.

### Les causes closes de « non concluant » — aucune ne se déclenche

| cause | constat |
|---|---|
| ① κ de #142/#150 sous 0,60 sur les signalées | **non** — κ = 0,631 ([#150](https://github.com/AmauryTISSOT/microservice_rgpd/issues/150)) |
| ② pivot corrompu, plis utilisables sous six | **non** — six plis, 602 · 610 · 603 · 548 · 625 · 194 = 3 182 colonnes, les comptes exacts du protocole |
| ③ gel violé (cause amendée par #154) | **non** — lexiques et transcriptions commités par `d413d55`, seul commit qui les touche, antérieur au premier commit d'exécution du banc ; aucune entrée éditée depuis |
| ④ ligne de base dégénérée | **non** — F2 macro non nulle sur quatre plis sur six (dolibarr 0,072 · glpi 0,060 · openemr 0,069 · paheko 0,105), 24 colonnes signalées : faible, pas muette |
| ⑤ banc non mené à terme | **non** — six plis, cinq montages, 30 entraînements, prédictions versionnées |

« Tous les montages échouent » n'avait de toute façon pas eu lieu — et aurait été un NON.

## Ce que le verdict commande sans le décider ici

L'emplacement du moteur appartient à [#135](https://github.com/AmauryTISSOT/microservice_rgpd/issues/135),
qui hérite de trois faits mesurés :

1. **Le meilleur montage est règles + lexique FR+EN** (F2 macro 0,3172), devant le modèle CPU
   à sa pire graine (0,2464) — et même à sa meilleure (0,3271), l'écart entre les deux familles
   est dans le bruit inter-graines.
2. **La borne de coût est tenue par tous les montages à dictionnaire et franchie par le modèle
   CPU** (détail plus bas). Le modèle CPU arrive chez #135 étiqueté **« inexploitable là où il
   paierait »**, la borne y étant éliminatoire.
3. **La dette du degré de doute** : le modèle CPU ne dérive aucun `RuleStrength` — s'il devait
   l'emporter chez #135, la décision de cadrage 5 se rouvrirait (dette écrite d'avance par #130).

## Le coût, mesuré sous contention — jamais à vide

**La phrase de contention, en clair** : pendant toute la mesure, le sidecar de qualification
(moteur LLM éteint) servait `/opinions/lexicon` en boucle fermée, un client, textes de
`corpus/demandes-rgpd.fr.jsonl` — le dispositif du profil #156, l'agresseur synthétique en
moins, le moteur mesuré tenant ce rôle. Le p95 du lexique pendant l'exécution des moteurs est
resté entre **2,92 et 3,86 ms**, sous son enveloppe mesurée à huit cœurs saturés (10,036 ms,
`profil-contention-sidecar.json`) et très loin de l'échéance .NET de 5 s : **aucun moteur n'a
affamé le point d'entrée du lexique**, y compris le modèle CPU pendant ses 131,7 s.

Pivot Dolibarr entier, 5 382 colonnes — le pire cas réel. Latences **médiane et p95, jamais la
moyenne**. Borne de #156, chiffrée avant la première exécution, lue mécaniquement, **non
éliminatoire ici** :

| montage | médiane / p95 par colonne | durée totale | RSS | borne (0,35 ms · 7 s · lexique 5 s) |
|---|---|---:|---:|---|
| ligne de base | 0,0007 / 0,0045 ms | 9 ms | 35 Mio | **tenue** |
| règles + lexique FR | 0,0281 / 0,0624 ms | 171 ms | 35 Mio | **tenue** |
| règles + lexique EN | 0,0314 / 0,0757 ms | 192 ms | 35 Mio | **tenue** |
| règles + lexique FR+EN | 0,0460 / 0,1029 ms | 270 ms | 35 Mio | **tenue** |
| modèle CPU | 24,93 / **29,12 ms** | **131 678 ms** | **1,66 Gio** | **franchie** — p95 à 83 × la clause de rythme, durée à 19 × le budget du geste |

Inférence du modèle colonne par colonne (lot de 1) : la clause parle d'une latence *par
colonne* et c'est sa seule lecture honnête ; un traitement par lot réduirait la durée totale
mais pas d'un facteur 19 sur cette machine. **Journal des dépendances** : aucune dépendance
ajoutée aux manifestes ; à l'exécution, les montages à dictionnaire tiennent dans la
bibliothèque standard, le modèle CPU exige `torch` + `sentence-transformers` + `scikit-learn`
(l'environnement d'exploration, jamais celui de la production).

## Rapporté sans peser sur le verdict

- **Wilson colonne (diagnostic seul)** — exactitude : ligne de base 0,770 [0,755 ; 0,784] ;
  FR+EN 0,640 [0,623 ; 0,656] ; modèle CPU 0,753 [0,738 ; 0,768]. La fourchette étroite dit
  ce que #130 annonçait : elle répond à une question qui n'intéresse personne — le montage le
  plus exact en colonne est la ligne de base, précisément parce qu'elle ne signale presque rien.
- **F2 par catégorie du meilleur montage (FR+EN)** : `ContactDetails` 0,679 ·
  `AuthenticationSecret` 0,574 · `FinancialData` 0,256 · `ConnectionData` 0,206 · `Identity`
  0,117 · `HealthData` 0,108 · `PersonalDataUncategorised` 0,010. Exclues de la macro, avec
  leur motif (`macro-f2.json`, publié avant tout chiffre) : `CriminalOffenceData`,
  `SpecialCategoryData`, `LocationData` (zéro pli peuplé), `HealthData` (2 plis),
  `NationalIdentifier` (2), `FinancialData` (4), `ProfessionalLife` (3).
- **Taux d'erreur par degré** (FR+EN) : `exacte` 70,0 % (404 lignes), `morphologique` 93,9 %
  (515). L'ordre des degrés garde un sens — l'exacte se trompe moins — mais les deux niveaux
  se trompent massivement : `RuleStrength` survit comme ordre, pas comme promesse.
- ⚠️ **Taux de repli `PersonalDataUncategorised`, comparé à l'annotateur** — la lecture que
  #130 déclarait la plus inquiétante est constatée : annotateur **17,3 %** des signalées,
  montages à dictionnaire **0,65 %**, modèle CPU 6,6 %. Un lexique ne sait pas avouer : il
  force la colonne dans la catégorie de l'entrée qui a déclenché ou se tait, et la F2 macro le
  sanctionne ailleurs sans le dire (le repli est à F2 0,010 chez FR+EN pour 128 colonnes de
  support). C'est une propriété de la famille, transmise à #135 et #131.
- **Contrôle de dialecte `galette` / `galette-pg`** : ligne de base et modèle CPU, 0 divergence
  sur 194 ; montages à dictionnaire, **2 divergences — et ce sont exactement les deux colonnes
  `jsonb` du corpus** (`galette_field_types.field_specifications`,
  `galette_searches.parameters`), que la règle « conteneur libre » signale en PostgreSQL
  (`jsonb`) et pas en MariaDB (`longtext`). Ce n'est **ni un défaut du pivot ni un défaut du
  moteur** : les deux dialectes déclarent réellement des types différents, et la règle lit le
  type. À verser au dossier de #132 : le signal d'incomplétude détectable dépend du dialecte.
- **Plafond d'annotation** : F2 macro du second codeur contre les étiquettes du corpus, sur les
  293 colonnes doublement codées appariées (les 7 restantes sont du pli `temoin`, hors mesure
  des deux côtés) : **0,839** (micro 0,861). Aucun montage n'entre
  dans la bande de désaccord humain — le meilleur est à 0,317 : **tout progrès reste mesurable
  sur ce corpus**, et la marge est immense.
- **Instabilité inter-graines du modèle CPU** : F2 macro de 0,246 à 0,327 selon la graine —
  un écart (0,081) de l'ordre du signal mesuré, exactement l'instabilité que la règle du pire
  des cinq existe pour rendre visible. Un montage qu'on ne sait pas reproduire ne se déploie pas.

## Écarts au protocole — déclarés, avec leur motif chiffré

1. **La tête du modèle CPU a été remplacée une fois.** La tête initiale (`MLPClassifier`,
   128 neurones) s'est effondrée sur `Unflagged` aux **cinq** graines — F2 macro 0,0, **0
   colonne signalée sur 3 182**, la classe majoritaire pesant 76,1 %. Un montage qui ne rend
   aucun verdict mesure un défaut de fabrication du banc, pas la famille « plongements +
   classifieur ». Remède standard appliqué une seule fois, décidé sur le seul constat de
   l'effondrement et avant toute comparaison à la barre : tête linéaire à classes pondérées
   (`SGDClassifier`, perte logistique). Aucune itération ensuite — le score obtenu est final.
2. **Catégorie sans support dans un réplicat bootstrap** : sa F2 est indéfinie et elle sort de
   la macro de ce réplicat (jamais 0 par décret). En pratique : **0 réplicat sur 10 000**
   n'a perdu la macro entière, pour tous les montages.
3. **Les métriques comptent les colonnes annotées sans pondération par probabilité
   d'inclusion** — la lecture que `macro-f2.json` (peuplement en comptes bruts) a publiée
   avant tout chiffre de moteur.
4. **Latence du modèle CPU en lot de 1** (motif dans la section coût).

## Les réserves — section obligatoire, son absence serait un défaut du banc

① **Le banc mesure du schéma propre.** La base sédimentée que le client collera n'est pas
couverte — les 17 migrations SACoche en portent une série diachronique de seize ans, hors corpus.

② **Les dictionnaires des montages ont été rédigés en aveugle (#155), mais les six schémas ont
été lus nommément pendant #125, #129 et #137** ; la fuite de réglage est déclarée, jamais
réparée. Le chiffre est un **plafond optimiste** de ce qu'on obtiendrait sur une base jamais vue.

③ **`HealthData` n'est mesurable que chez OpenEMR, en anglais médical.** Un montage qui
« réussit la santé » ici a réussi *la santé en anglais chez OpenEMR* — et de fait le meilleur
montage y plafonne à F2 0,108.

④ **`CriminalOffenceData` n'est pas mesurée du tout** — propriété de la population des
logiciels libres (#137), pas un trou de corpus.

⑤ **Aucun montage LLM n'a été mesuré**, par décision de périmètre (#130) — et non parce qu'il
aurait perdu.

⑥ **Six schémas d'une population étroite** — associatif, scolaire, médical, gestion, parc
informatique — dont aucun n'est une base d'entreprise sédimentée.

⑦ **La vérité terrain est produite par des modèles de langue ; les montages mesurés ne le sont
pas.** Le banc mesure en partie à quel point un moteur lexical reproduit la lecture qu'un
modèle de langue fait d'un nom de colonne. Atténuée (l'exemple `form_eye_*` est atteignable
par le nom de table, présent dans le pivot), non supprimée.

⑧ **La vérité terrain sous-signale** (#150) : 22 des 28 désaccords résiduels du double codage
sont « machine `Unflagged`, humain signale ». Un moteur qui signale `remise_percent` à raison
est compté faux positif ; le biais joue **contre les montages sensibles**, direction connue,
ampleur inchiffrable sans corrompre le corpus.

⑨ **Le gel par rédaction aveugle est une preuve plus faible que l'antériorité `git log`
d'origine** (#154), et rédacteurs comme annotateurs sont des modèles de langue — corrélation
d'a priori lexicaux de direction anti-conservatrice, extension de ⑦, irréparable sans second
codeur humain.

Et une limite transférée sur ce ticket par #132 : **le signal « conteneur libre » est réel en
production et quasi absent du corpus mesuré** — 2 colonnes `json`/`jsonb` sur tout le corpus,
toutes deux dans `galette-pg`, hors mesure. La règle est dans le moteur, mesurée sur presque
rien.

## Dette transmise

- **Le degré de doute n'entre pas au critère, et le modèle CPU n'en dérive aucun** — dette
  transmise à #135, pas un échec (#130).
- **La valeur absolue (0,3172 macro / 0,1963 micro au mieux) part chez #131** : savoir si un
  rapport à ce niveau vaut le temps d'un `Operator` se tranche devant un rapport réel.
- **Le taux de repli quasi nul des montages à dictionnaire** est une propriété de la famille à
  garder sous les yeux au moment de l'implémentation : le moteur retenu devra savoir avouer,
  ou son rapport forcera des catégories fausses en silence.
