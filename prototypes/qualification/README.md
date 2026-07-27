# Prototypes jetables — moteurs de qualification

Instruit le ticket [#6](https://github.com/AmauryTISSOT/microservice_rgpd/issues/6)
de la carte [#1](https://github.com/AmauryTISSOT/microservice_rgpd/issues/1).

**→ Le livrable est [`COMPARAISON.md`](./COMPARAISON.md).**

Ce dossier est **jetable**. Il vit hors de la solution .NET, en Python, et
n'a aucune vocation à être porté tel quel : son seul rôle est de mesurer un
écart entre deux familles d'approches pour éclairer le choix du moteur.

## Contenu

| Fichier | Rôle |
| --- | --- |
| [`moteur_lexique.py`](./moteur_lexique.py) | **Moteur A** — lexiques, règles et discriminants, déterministe. |
| [`prompt_llm.md`](./prompt_llm.md) | **Moteur B** — prompt système et schéma de sortie structurée. |
| [`predictions-llm.jsonl`](./predictions-llm.jsonl) | Sorties du moteur B sur les 120 textes. |
| [`extraire_aveugle.py`](./extraire_aveugle.py) | Anonymise et permute le corpus pour l'exécution en aveugle. |
| [`textes-aveugles.jsonl`](./textes-aveugles.jsonl) | Le corpus sans ses étiquettes, sous identifiants opaques. |
| [`correspondance-aveugle.json`](./correspondance-aveugle.json) | Table opaque → identifiant du corpus, pour l'évaluation. |
| [`evaluer.py`](./evaluer.py) | Métriques, analyse d'erreurs, paires minimales. |
| [`COMPARAISON.md`](./COMPARAISON.md) | Le tableau de comparaison et son interprétation. |

## Protocole

Trois précautions conditionnent la validité des chiffres.

**Le moteur A n'a pas vu les étiquettes.** Ses règles ont été écrites à partir
des seules recherches [#3](https://github.com/AmauryTISSOT/microservice_rgpd/issues/3)
et [#4](https://github.com/AmauryTISSOT/microservice_rgpd/issues/4) — vocabulaire
CNIL, articles du RGPD, lignes directrices CEPD — avant toute évaluation. Il a
ensuite subi **une** passe de correction, portant sur deux bugs de mécanisme et
non sur des exemples ; les deux scores sont rapportés (65,0 % puis 78,3 %),
parce que l'écart entre les deux est lui-même une information sur le coût de
mise au point d'un lexique.

**Le moteur B a tourné en aveugle.** Les identifiants du corpus portent un
préfixe qui trahit l'étiquette (`acc-`, `eff-`, `hop-`, `mul-`, `edg-`) et le
fichier est groupé par droit. `extraire_aveugle.py` remplace les identifiants
par `t-001`…`t-120` et permute l'ordre par hachage du texte — déterministe,
donc reproductible.

**Le moteur B n'a pas été appelé via une API.** Aucune clé n'est disponible
dans cet environnement. Cette limite et ses conséquences sont détaillées dans
[`COMPARAISON.md` § *Ce que cette comparaison ne mesure pas*](./COMPARAISON.md#ce-que-cette-comparaison-ne-mesure-pas) —
**à lire avant de citer le moindre chiffre du moteur B**.

## Exécuter

Aucune dépendance : bibliothèque standard de Python 3.10+.

```bash
py prototypes/qualification/extraire_aveugle.py
py prototypes/qualification/evaluer.py [--detail]
```

Sous Windows, préfixer par `PYTHONIOENCODING=utf-8` : la sortie contient des
indices Unicode (F₂).
