# Profil de contention du sidecar de qualification, et borne de coût CPU du banc

Ticket [#156](https://github.com/AmauryTISSOT/microservice_rgpd/issues/156) — obligation
d'avant-exécution du protocole [#130](https://github.com/AmauryTISSOT/microservice_rgpd/issues/130),
portée par la résolution de [#154](https://github.com/AmauryTISSOT/microservice_rgpd/issues/154) :
la borne d'admissibilité du coût machine des montages du banc
[#134](https://github.com/AmauryTISSOT/microservice_rgpd/issues/134) doit être **chiffrée avant la
première exécution du banc**, faute de quoi elle serait ajustée aux chiffres. Elle est chiffrée ici
contre un profil de contention **mesuré** — jamais à vide — du sidecar de qualification.

**Aucun chiffre de moteur de dépistage ne figure dans ce dossier.** Ce qui est mesuré est le
sidecar *existant* (le lexique de qualification, carte #42) sous une charge CPU artificielle ; les
montages du banc n'ont pas tourné.

## Ce que contient ce dossier

| Fichier | Rôle |
| --- | --- |
| [`mesurer_contention.py`](mesurer_contention.py) | l'instrument — bibliothèque standard seule, aucune dépendance ajoutée |
| [`profil-contention-sidecar.json`](profil-contention-sidecar.json) | le relevé brut de la mesure du 2026-08-10 |
| [`borne-cout-cpu.json`](borne-cout-cpu.json) | **la borne**, lue mécaniquement au rendu de #134 |

## Le dispositif

Le sidecar est lancé comme en production — uvicorn, un seul processus, moteur LLM éteint (le
lexique et `/health` sont servis, `POST /opinions/llm` rend son `501` nommé) :

```sh
uv run --project src/sidecar uvicorn qualification_sidecar.app:app --port 8123
python3 exploration/contention/mesurer_contention.py \
  --corpus corpus/demandes-rgpd.fr.jsonl \
  --sortie exploration/contention/profil-contention-sidecar.json
```

Un client en **boucle fermée** — le régime réel du service, une demande à la fois — poste les
120 textes de `corpus/demandes-rgpd.fr.jsonl` sur `/opinions/lexicon` pendant 30 s par palier,
3 s de chauffe écartées. Pendant ce temps, un **agresseur** sature 0, 2, 4, 6, 7 puis 8 cœurs de
calcul Python pur, lié au CPU et sans entrée-sortie — le régime que la décision de cadrage 13 de la
carte #122 déclare dangereux pour le point d'entrée du lexique (spec qualification § 5.8), et celui
du futur moteur de dépistage. Latence rapportée en **médiane et p95, jamais la moyenne** ; mémoire
résidente lue dans `/proc`.

Machine : 8 cœurs, noyau Linux 7.0.0-28-generic — le gabarit machine de l'ADR-0001.

## Le profil mesuré (2026-08-10)

| Cœurs saturés | Requêtes | Médiane | p95 | Max | RSS sidecar |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 0 (référence) | 13 447 | 2,07 ms | 3,09 ms | 27,6 ms | 35 780 kio |
| 2 | 11 221 | 2,46 ms | 3,82 ms | 13,2 ms | 35 780 kio |
| 4 | 8 420 | 3,06 ms | 6,18 ms | 34,9 ms | 35 780 kio |
| 6 | 8 864 | 2,77 ms | 6,69 ms | 79,5 ms | 35 780 kio |
| 7 | 9 259 | 2,60 ms | 6,62 ms | 45,1 ms | 35 780 kio |
| 8 | 5 692 | 4,93 ms | 10,04 ms | 31,9 ms | 35 780 kio |

**La phrase de contention, en clair.** Huit cœurs saturés de calcul Python pur ne suffisent pas à
affamer le lexique : sa médiane passe de 2,1 ms à 4,9 ms et son p95 de 3,1 ms à 10,0 ms — un
facteur 3,3, qui laisse le pire palier **cinq cents fois sous l'échéance contractuelle de 5 s**
(spec § 7.5). La famine du § 5.8 n'est donc pas, sur cette machine, un risque d'ordonnanceur pour
un point d'entrée de 2 ms : elle reste un risque de **file** — un appel bloquant de 120 s devant le
lexique — que ce profil ne mesure pas et que la concurrence des deux points d'entrée, déjà testée
par la suite `pytest` du sidecar, adresse par ailleurs. Réciproquement, le sidecar sous flux
soutenu occupe de l'ordre d'**un cœur** (≈ 450 requêtes/s à ~2 ms, client compris) et **35 Mio de
mémoire résidente, stables du premier au dernier palier** : un moteur de dépistage en contention
avec lui dispose de l'essentiel des huit cœurs.

## La borne, et d'où viennent ses chiffres

Le point d'appui est [#136](https://github.com/AmauryTISSOT/microservice_rgpd/issues/136) : le
geste de dépôt synchrone est budgété 3 s d'écriture + **7 s de moteur**, pour un relevé pouvant
atteindre le plafond de 20 000 colonnes (#128) — soit **0,35 ms/colonne**, qui était une
estimation, pas une mesure. La confrontation au profil dit ce qu'elle peut dire sans faire tourner
un moteur : la contention ne rend pas ce rythme infaisable par elle-même — l'ordonnanceur prélève
un facteur ≈ 3,3 sur le p95 du *perdant* d'une saturation totale, et le sidecar laisse
l'essentiel des cœurs libres — mais **si** 0,35 ms/colonne est atteignable par un montage réel est
précisément la question du banc, et elle reste ouverte ici.

Les trois clauses de [`borne-cout-cpu.json`](borne-cout-cpu.json), toutes ancrées sur un chiffre
qui préexiste à ce ticket — aucun seuil n'est inventé ici, le « chiffre sorti de nulle part »
contre lequel #123 met en garde :

1. **Rythme par colonne** — p95 par colonne sous contention ≤ **0,35 ms**. C'est le rythme
   qu'exige le pire dépôt licite (7 s / 20 000), tenu jusque dans la queue de distribution : un
   moteur dont le p95 dépasse le rythme du plafond tient Dolibarr mais pas le contrat.
2. **Budget du geste** — durée totale du moteur sur les 5 382 colonnes de Dolibarr sous
   contention ≤ **7 000 ms**. La clause 1 borne le rythme colonne par colonne ; celle-ci referme la
   queue au-delà du p95, que le rythme seul ne borne pas.
3. **Non-famine du lexique** — p95 de `/opinions/lexicon` **pendant** l'exécution du moteur
   ≤ **5 000 ms**, l'échéance contractuelle du § 7.5 — l'incident que la décision 13 refuse, lu
   mécaniquement. L'enveloppe de référence est le profil ci-dessus (10,0 ms à 8/8) : la phrase de
   contention du banc doit citer l'écart à cette enveloppe, pas seulement le franchissement.

**Lecture.** La borne est tenue si et seulement si les trois clauses le sont. Non éliminatoire chez
#134 — un montage qui la franchit arrive chez
[#135](https://github.com/AmauryTISSOT/microservice_rgpd/issues/135) étiqueté *« inexploitable là
où il paierait »* — et éliminatoire chez #135. Décalque de la borne anti-précédent de #154 : elle
est **inamendable dès le premier chiffre de moteur**.

## Ce que ce profil ne dit pas

- Rien sur les montages du banc : aucun n'a tourné, aucun chiffre de moteur n'existe.
- L'agresseur est du calcul Python pur ; un moteur réel a un profil mémoire et des fautes de cache
  différents. Le profil borne l'effet d'ordonnanceur, pas l'effet de cache.
- Le palier « 0 cœur » n'est qu'une référence de lecture — la borne, elle, ne se lit jamais à vide.
- Le moteur LLM était éteint : la contention mesurée est celle du lexique seul, conformément au
  périmètre de la carte #122 (le LLM est hors périmètre du dépistage depuis #130).
