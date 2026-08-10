# Banc d'essai du moteur de dépistage — ticket #134

Le banc qui tranche le moteur de `Screening`, en exécution du protocole de
[#130](https://github.com/AmauryTISSOT/microservice_rgpd/issues/130) (amendé par #145, #150, #154).
Il ne rediscute rien du protocole ; ce qu'il a fallu décider en fabriquant est déclaré ci-dessous,
avec son motif.

| Fichier | Rôle |
| --- | --- |
| [`banc.py`](./banc.py) | Le banc : plis, montages, F2, bootstrap de l'écart apparié. |
| [`mesurer_cout.py`](./mesurer_cout.py) | Le coût sous contention, contre la borne de #156. |
| [`lexiques/`](./lexiques/) | Les trois lexiques gelés (#155) — la seule connaissance lexicale du banc. |
| [`predictions/`](./predictions/) | Les prédictions, par montage et par pli — le verdict est vérifiable sans le banc. |
| [`resultats.json`](./resultats.json) | Tous les chiffres. |
| [`cout.json`](./cout.json) | Le coût mesuré et la lecture mécanique de la borne. |
| [`VERDICT.md`](./VERDICT.md) | Le rendu : verdict mécanique, réserves obligatoires, écarts. |

## Environnements — la règle de la carte #42

Ce qui calcule sur des fichiers figés (`banc.py`) tourne avec l'environnement de l'exploration
(`exploration/pyproject.toml`). Ce qui parle à un moteur servi (`mesurer_cout.py`, qui martèle
`/opinions/lexicon` pendant la mesure) exige le sidecar lancé avec l'environnement de la
production (`src/sidecar`), moteur LLM éteint.

```bash
uv run --project exploration python exploration/banc-screening/banc.py --racine .
# puis, sidecar lancé à côté (uv run --project src/sidecar uvicorn qualification_sidecar.app:app --port 8123) :
uv run --project exploration python exploration/banc-screening/mesurer_cout.py --racine .
```

## Ce que le protocole fixait, tenu tel quel

Six plis « laisser un schéma dehors » (`paheko` trois états réunis, `galette-pg` hors mesure en
contrôle de dialecte, `temoin` hors des deux côtés — 3 182 colonnes mesurées) ; quatre montages
plus la ligne de base triviale ; F2 macro (β = 2) sur l'ensemble publié par
`corpus/schemas/macro-f2.json`, F2 micro en second, **aucune chirurgie du dénominateur** ;
bootstrap de grappe rééchantillonnant les six schémas, portant sur l'**écart apparié**
montage − ligne de base ; pire graine sur R = 5 pour le modèle CPU (30 entraînements) ;
Wilson colonne en diagnostic seul ; critère : borne basse de l'IC à 95 % strictement positive.

## Ce que la fabrication a dû décider — et pourquoi ce ne sont pas des réglages

Le protocole spécifie la ligne de base jusqu'au geste, mais laisse l'intérieur des montages au
banc. Tout ce qui suit est **générique** — aucune règle ne connaît le corpus ; la seule
connaissance lexicale vit dans les lexiques gelés.

1. **Découpe d'identifiants** : séparateurs non alphanumériques, frontières camelCase, chiffres
   ôtés, accents pliés (NFD). C'est la découpe la plus simple qui traite `dt_naiss` et
   `CERInscriptionDate` du même geste ; toute découpe plus fine (glouton à dictionnaire) aurait
   fait du dictionnaire un algorithme, ce que la grille des montages sépare exprès.
2. **Règles et leur force** (décision de cadrage 5) : jeton du nom de colonne égal à une entrée ⇒
   `exacte` ; rapprochement par préfixe (couvrant le plus court des deux, longueur ≥ 4) ou mot
   d'un commentaire ⇒ `morphologique` ; type `json`/`jsonb` ⇒ « conteneur libre »,
   `PersonalDataUncategorised`, force `type` — la règle transférée sur #134 par #132, au grain
   strict (`json` seul ; l'issue `text`/`blob` aurait signalé 44 % de Paheko-head).
3. **Héritage par la table** (§ 3.10 de #145) : si rien ne déclenche au niveau de la colonne,
   les jetons du nom de table et les mots du commentaire de table déclenchent en `morphologique` —
   jamais sur les clés purement techniques `id`/`rowid`. L'héritage porte le domaine de la table,
   pas la lecture de la colonne : sa force n'est jamais `exacte`.
4. **Arbitrage** : l'ordre du tableau de #127, hérité, jamais redécidé — y compris pour les deux
   termes que FR et EN valuent différemment (`conviction`, `coord`) dans l'union FR+EN.
5. **`type`, `nullable`, `table_referencee` restent des filtres, jamais des signaux** (#128) — la
   seule exception est « conteneur libre », transférée nommément. Le modèle CPU ne les lit pas
   non plus.
6. **Modèle CPU** : plongements `intfloat/multilingual-e5-small` — l'encodeur déjà retenu par
   l'exploration du dépôt (#47 : hors d'atteinte du bug d'accents GGUF), bilingue comme le
   corpus — sur `table | colonne | commentaires`, tête scikit-learn dont la graine est le seul
   aléa : une tête déterministe aurait fait des cinq graines du théâtre.
   ⚠️ **Écart déclaré, avec son motif chiffré : la tête a été remplacée une fois.** La première
   (`MLPClassifier`, 128 neurones cachés) s'est effondrée sur `Unflagged` pour les **cinq**
   graines — F2 macro 0,0 partout, **0 colonne signalée sur 3 182**, la classe majoritaire pesant
   76,1 % du corpus. Un montage qui ne rend aucun verdict ne mesure pas la famille « plongements
   + classifieur », il mesure un défaut de fabrication du banc. Remède standard appliqué **une
   seule fois**, décidé sur le seul constat de l'effondrement et avant toute comparaison à la
   barre : tête linéaire `SGDClassifier` (perte logistique, `class_weight="balanced"`, graine par
   le mélange des exemples). Aucune itération supplémentaire, quel que soit son score — itérer
   jusqu'à passer la barre serait l'ajustement aux chiffres que le protocole interdit.
7. **Graines déclarées avant tout chiffre** : modèle {1, 2, 3, 4, 5}, bootstrap 134,
   B = 10 000.

## Lectures fixées avant les chiffres

- **F2 d'une catégorie sans support** dans un rééchantillon bootstrap : indéfinie, la catégorie
  sort de la macro de ce réplicat (jamais 0 par décret) ; les réplicats devenus entièrement
  indéfinis sont comptés et publiés.
- **Cause ④ (ligne de base dégénérée)** : « score nul ou quasi nul sur tous les plis » se lit
  sur la F2 macro par pli de la ligne de base.
- **Les métriques comptent les colonnes annotées sans pondération** — c'est la lecture que
  `macro-f2.json` (peuplement en comptes bruts) a publiée avant tout chiffre.
- **Latence par colonne du modèle CPU** : inférence colonne par colonne (lot de 1) — la borne
  parle d'une latence *par colonne* et c'est sa seule lecture honnête ; la durée totale du même
  passage sert de lecture du « budget du geste ».
