# ADR-0004 — Le moteur de dépistage vit en C# dans `Infrastructure`, sans second sidecar Python

- **Statut** : accepté
- **Date** : 2026-08-10
- **Décidé par** : [L'emplacement du moteur : second sidecar Python ou C# en Infrastructure](https://github.com/AmauryTISSOT/microservice_rgpd/issues/135), sur le verdict du [banc d'essai](https://github.com/AmauryTISSOT/microservice_rgpd/issues/134)
- **Complète** : [ADR-0003](./0003-troisieme-contexte-sans-intersection-et-garde-des-traversees.md), qui avait explicitement laissé cette décision au banc

## Contexte

Le contexte `Screening` a besoin d'un moteur qui, colonne par colonne d'un recensement pivot, présume une catégorie de données, dérive un degré de doute de la règle qui a déclenché, et motive en prose française. Deux emplacements étaient déclarés d'avance par la décision de cadrage 13 de la carte [#122](https://github.com/AmauryTISSOT/microservice_rgpd/issues/122), départagés par un banc mesuré dont le critère était écrit avant les chiffres ([#130](https://github.com/AmauryTISSOT/microservice_rgpd/issues/130)) :

- **moteur = modèle** ⇒ second sidecar Python distinct, propre au contexte ;
- **moteur = règles pures** ⇒ C# dans `Infrastructure`.

Deux issues étaient exclues d'avance : un endpoint de plus dans `qualification_sidecar` (la carte #42 a mesuré que toute famille sauf le sac de mots est liée au CPU et affamerait le point d'entrée du lexique), et le LLM déjà servi (sorti du périmètre par #130, non battu au banc : le GPU est déjà pris et sérialisé, un dépistage derrière la file de qualification déplacerait la famine du CPU vers le GPU).

Le banc ([#134](https://github.com/AmauryTISSOT/microservice_rgpd/issues/134), rendu intégral dans `exploration/banc-screening/VERDICT.md`, PR #161) a rendu :

- **le meilleur montage est règles + lexique FR+EN** (F2 macro 0,3172, écart apparié à la ligne de base IC 95 % [+0,1833 ; +0,3136]), devant le modèle CPU même à sa meilleure graine ;
- **le modèle CPU franchit la borne de coût #156, éliminatoire ici** : p95 29,1 ms/colonne sous contention (83 × la clause de rythme), 131,7 s sur les 5 382 colonnes de Dolibarr (19 × le budget du geste), RSS 1,66 Gio. Il arrive à cette décision étiqueté **« inexploitable là où il paierait »** ;
- les montages à dictionnaire tiennent la borne avec une marge écrasante : p95 ≤ 0,103 ms/colonne, RSS 35 Mio, et aucun n'a affamé le lexique de qualification pendant les mesures.

## Décision

**Le moteur de dépistage est du C# dans `Infrastructure`. Il n'y a pas de second sidecar Python.**

1. Le montage retenu est **règles + lexique FR+EN** — un dictionnaire et des règles pures, la famille que l'ADR-0001 déclarait déjà « techniquement viable et même plus simple » à porter en C#.
2. Le contrat interne est **`IScreeningEngine`**, en `Core/Screenings`, sur le modèle exact d'`IQualificationEngine` : l'emplacement du moteur est un détail d'`Infrastructure`, et la décision est **réversible** — un futur moteur serait une implémentation de plus.
3. **Le cas mixte** (« règles plus un petit modèle de similarité ») est **forclos par le coût de son composant, mesuré** — toute variante « plus un modèle CPU » hérite de la p95 à 29,1 ms/colonne. Il n'est pas battu au banc : aucun montage mixte n'a été mesuré. Si un modèle radicalement plus léger apparaissait, c'est le **banc** qui rouvrirait, pas cette décision seule.

## Justification

**Le banc décide où vit le moteur.** La règle écrite d'avance liait l'emplacement à la famille gagnante ; la famille gagnante est un dictionnaire, et la seule famille qui aurait payé un sidecar est inexploitable sur cette machine pour ce geste. Il n'y a rien à héberger en Python.

**La clause de réexamen de l'ADR-0001 est examinée et ne se déclenche pas.** L'ADR-0001 a écarté le port C# du lexique de qualification « au seul motif de la frontière anticipée vers l'apprentissage automatique », en ajoutant : « si ce motif tombait, cette décision serait à revoir ». Ce motif **ne se transporte pas** au dépistage : le banc a mesuré qu'il n'existe pas, sur cette machine, de candidat ML exploitable à anticiper. ⚠️ **Cet ADR n'est pas une révision rampante de l'ADR-0001** : pour la qualification, le LLM est réel, la frontière est effective, et la décision du sidecar de qualification **ne se rouvre pas**.

**Payer le sidecar d'avance serait payer une facture certaine pour un gain hypothétique.** Un troisième écosystème dans la chaîne de test, une image, un empaquetage, un démarrage à froid — l'ADR-0001 note que cette question ne s'est pas résolue toute seule pour le premier sidecar — pour héberger un dictionnaire. Et si un moteur IA devait s'ajouter un jour, il serait en Python quoi qu'il arrive et viendrait avec **son** sidecar **à ce moment-là, pour le même prix** : le choix C# d'aujourd'hui ne renchérit pas ce futur, grâce au contrat `IScreeningEngine`. Le seul avantage du sidecar-maintenant — la plomberie déjà en place le jour J — est marginal contre un coût certain et immédiat.

**La valeur absolue médiocre renforce l'emplacement, elle ne le fragilise pas.** Le meilleur montage plafonne à F2 macro 0,3172 contre un plafond d'annotation de 0,839 : on n'achète pas un troisième écosystème pour un moteur qui bat une référence quasi muette. Savoir si ce niveau justifie l'existence même du moteur dans un rapport réel se juge à [#131](https://github.com/AmauryTISSOT/microservice_rgpd/issues/131), devant un rapport produit — pas ici.

## Conséquences

**Acquis**

- Aucun coût d'exploitation nouveau : pas de troisième écosystème, pas d'image, pas de froid. Le moteur démarre avec le service.
- La dette du degré de doute déclarée par #130 est **éteinte sans rouvrir la décision de cadrage 5** : le montage retenu dérive ses degrés de la règle qui déclenche — correspondance exacte, rapprochement morphologique, heuristique de type — exactement comme le cadrage l'exige.
- La décision est réversible par construction : `Core` ne bouge pas si un moteur revient en Python.

**Coûts et contraintes**

- **Les lexiques FR+EN gelés du banc** (`d413d55`) deviennent des actifs C#/`Infrastructure` ; leur forme d'embarquement relève de l'implémentation.
- **Un lexique ne sait pas avouer** : le taux de repli mesuré est anormalement bas (0,65 % des signalées contre 17,3 % chez l'annotateur ; `PersonalDataUncategorised` : F2 0,010). Propriété de la famille retenue, à garder sous les yeux à l'implémentation — l'emplacement n'y change rien.
- **Les degrés survivent comme ordre, pas comme promesse** : `exacte` se trompe à 70 %, `morphologique` à 94 %. L'ordre a un sens ; l'implémentation et l'écran d'arbitrage ne doivent rien promettre de plus.

## Alternatives écartées

| Alternative | Motif du rejet |
| --- | --- |
| **Second sidecar Python** | sa seule raison d'être — héberger un modèle — est tombée au banc : le modèle CPU est inexploitable là où il paierait (p95 83 × la clause de rythme, 19 × le budget du geste, RSS 1,66 Gio) ; resterait un dictionnaire en Python, payé au prix d'un troisième écosystème |
| **Endpoint de plus dans `qualification_sidecar`** | exclu d'avance et non rediscuté : contention CPU mesurée par la carte #42, un scan qui sature les cœurs pendant qu'une qualification attend est un incident |
| **LLM déjà servi comme moteur** | sorti du périmètre par #130, non battu au banc : le GPU est déjà pris et sérialisé ; le refus est écrit pour être opposé si la question se rouvre |
| **Cas mixte (règles + petit modèle)** | forclos par le coût mesuré de son composant modèle ; rouvrable par le banc seul, si un modèle radicalement plus léger apparaît |

## Portée de cet ADR

Il tranche l'**emplacement** du moteur de dépistage et le contrat qui le rend réversible. Il ne dit rien de la valeur d'usage du moteur (#131), rien de la forme d'implémentation (règles, chargement des lexiques, câblage), et ne révise **aucune** décision de l'ADR-0001, dont la clause de réexamen est examinée ci-dessus et laissée intacte.
