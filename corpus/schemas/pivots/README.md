# Les pivots du corpus — figés dans une forme antérieure

Les dix `*.jsonl` de ce dossier sont **gelés dans une forme antérieure à
`docs/contexts/screening/pivot-format.md`**, sur deux points de forme :

| | Ces fichiers | `pivot-format.md` aujourd'hui |
| --- | --- | --- |
| nullabilité | `"nullable": 0` / `"nullable": 1` | `"nullable": false` / `"nullable": true` |
| ligne de fin | `{"colonnes": 194}` | `{"fin":true,"colonnes":194}` |

⚠️ **Ce n'est pas une dette, et il n'y a rien à réextraire.** Ne « réparez » pas ces fichiers pour
les aligner sur le format courant.

## Pourquoi ils restent tels quels

Ce corpus est un **instrument de mesure**, et le banc qui s'en sert est **clos**. La clause
opposable de [l'ADR-0012](../../../docs/adr/0012-la-connexion-le-scan-et-les-echantillons-entrent-dans-screening.md)
exige que le moteur enrichi rejoue le banc de
[#134](https://github.com/AmauryTISSOT/microservice_rgpd/issues/134) sans descendre sous **F2 macro
0,3172**. Un plancher n'est comparable que si l'entrée l'est : le relevé rejoué doit être, bit pour
bit, celui qui a produit 0,3172.

Réextraire ces pivots avec les requêtes corrigées changerait l'instrument **après** la mesure. Le
banc mesurerait alors autre chose et le dirait avec le même chiffre — la panne la plus coûteuse
possible pour une clause de non-régression, puisqu'elle est muette.

⚠️ Les écarts de forme ne touchent **rien de ce que le moteur lit** : noms de table, de colonne,
types et commentaires arrivent intacts. La nullabilité est de toute façon collectée comme *filtre*,
jamais comme *signal*.

## Comment ils sont lus malgré tout

`ScreeningOnTheTemoinPivotTests` convertit les deux détails à la volée, par deux `Replace`, avant de
passer le pivot à l'ingestion. **La conversion reste dans le test** — il n'y a qu'un appelant, et la
remonter en dur dans l'ingestion ferait accepter au service, en production, la forme que
`pivot-format.md` a précisément écartée.

## Où sont les relevés à la forme courante

Dans `exploration/releves-authentiques/captures/` : les sorties réelles des requêtes de `releves/`,
rejouées contre l'ingestion à chaque build. C'est là qu'on éprouve la forme, et **ici** qu'on
préserve la mesure. Les deux dossiers ont des rôles opposés ; ne les confondez pas.
