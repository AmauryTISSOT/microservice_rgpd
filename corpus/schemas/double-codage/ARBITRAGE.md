# Règle d'arbitrage des 79 désaccords — **pré-enregistrée**

*Écrite et commitée le 2026-08-09, **avant** d'avoir ouvert une seule des 79
lignes de désaccord. L'ordre est la seule chose qui donne sa valeur à ce
document, et il se vérifie dans `git log` : le commit qui porte ce fichier
précède celui qui porte les piles.*

## Pourquoi elle s'écrit d'avance

Un arbitrage rendu après avoir vu les lignes est un moyen de faire dire au corpus
ce qu'on veut qu'il dise. Les trois piles de
[#145](https://github.com/AmauryTISSOT/microservice_rgpd/issues/145) sont un
diagnostic — « les 79 désaccords ne sont pas un tas, ils en sont trois » — et un
diagnostic qui choisit ses critères après avoir compté est une conclusion
déguisée en observation.

C'est le décalque exact de deux gestes déjà posés sur cette carte :

- le **protocole d'annotation écrit avant la première étiquette**
  ([`PROTOCOLE-ANNOTATION.md`](../PROTOCOLE-ANNOTATION.md), préambule) — « un
  doute qu'on ne tranche pas d'avance se tranche douze fois différemment » ;
- le **dictionnaire figé une seule fois avant tout accès aux étiquettes**
  ([#130](https://github.com/AmauryTISSOT/microservice_rgpd/issues/130)), dont la
  violation est une cause close de « non concluant » et dont la garantie est
  elle aussi `git log` et rien d'autre.

⚠️ **Ce document ne se relit pas au milieu du classement pour l'ajuster à un cas
gênant.** Un cas qui n'entre dans aucune des trois piles est un **défaut de cette
règle**, et il se signale comme tel au lieu d'être rangé de force.

## Ce que cette règle ne fait pas

Elle **ne dit pas qui a raison**. Elle classe un désaccord ; elle ne le tranche
pas. La pile est une propriété du *désaccord* — de l'état du protocole face à
cette colonne — et non un verdict sur la passe machine ou sur la passe humaine.

Elle **ne modifie aucune étiquette**. Ni les 3 254 lignes d'`annotation/`, ni les
300 de `double-codage/reference-humaine.jsonl` ne sont éditées par ce geste. Le
cahier humain, ses fautes de frappe comprises, est un **document daté** : le
corriger effacerait la mesure au lieu de l'expliquer.

Elle **ne rejoue pas `accord.py`**. Le κ = 0,040 de
[`VERDICT.md`](./VERDICT.md) est publié ; il ne se réécrit pas.

## La règle

Un **désaccord** est une colonne de l'échantillon des 300 où l'étiquette machine
et la référence humaine diffèrent. Chaque désaccord va dans **exactement une**
des trois piles.

### Pile A — le protocole tranche déjà, sans interprétation

> Il existe un § du protocole — § 3.1 à § 3.9, ou une ligne des *conventions
> communes* de [`journal-arbitrages.md`](../journal-arbitrages.md), qui n'en sont
> qu'une application nommée — dont **l'antécédent se lit dans le pivot seul** et
> dont **le conséquent nomme une seule catégorie**.

⚠️ **« Sans interprétation » se teste** : deux lecteurs appliquant ce § au même
pivot en tirent la même catégorie **sans avoir à décider ce que la colonne
mesure**. Dès qu'il faut savoir ce que `ODSPH` veut dire pour conclure, on n'est
pas en Pile A.

Le § invoqué **se nomme, colonne par colonne**. Une ligne rangée en Pile A sans
que le § applicable puisse être cité n'est pas en Pile A : c'est du reste, donc
de la Pile C.

### Pile B — aucune règle n'existe

> Aucun § ne nomme la famille, et le désaccord porte précisément sur la question
> que le protocole n'a pas posée.

C'est la pile qui **amende le protocole**, par la procédure du § 6 : la famille
se porte au journal des arbitrages **avant** d'être tranchée, puis remonte en § 3
par un amendement daté.

### Pile C — le reste

> Un § s'applique mais **ne conclut pas seul** : son antécédent se lit, mais la
> catégorie dépend d'une lecture de la colonne que le § ne mécanise pas ; ou deux
> § se disputent la ligne sans que l'ordre d'arbitrage du § 2 les départage.

C'est la pile du **pari** : #145 fait l'hypothèse que l'instrument durci les fait
disparaître, et cette hypothèse n'a **qu'une tentative**.

### Ordre d'application

**A testée en premier, puis B, C par solde.** A et B s'excluent par construction
— « un § existe » et « aucun § n'existe » ne peuvent être vrais ensemble. C
n'est pas un test, c'est ce qui reste.

## Ce qui se publie, quel que soit le résultat

⚠️ **Le résultat se publie même s'il donne tort à la machine**, et même s'il
donne tort à #145.

1. Les **trois effectifs réels**. Les dimensionnements ~20 / ~28 / ~31 de #145
   sont des **criblages lexicaux**, pas des constats ; ce geste les remplace par
   des chiffres, et l'écart entre les deux se publie tel quel.
2. Le **détail ligne à ligne** — les 79 désaccords, leurs deux étiquettes, leurs
   deux motifs, la pile, et **le § invoqué** quand il y en a un.
3. La **concentration par schéma**. #145 annonce qu'OpenEMR concentre 33 des 79 ;
   le chiffre réel se publie, y compris s'il infirme cette lecture.

## Ce que chaque pile déclenche — fixé d'avance

| Pile | Remède | Fixé par |
|---|---|---|
| **A** | **Aucun ré-étiquetage.** Le désaccord est un défaut de l'instrument de saisie ; le remède est le durcissement de `coder.py` (geste 3), et le tirage neuf est l'endroit où il se vérifie. | #145 |
| **B** | **Amendement du protocole** — § 3.10, l'héritage du domaine par le nom de la table, hors champ libre, **en faveur de la machine**, avec sa borne. Journal des arbitrages, puis passe d'`incoherences.py` sur toute la famille. | #145 |
| **C** | **Le pari.** Rien de spécifique : ces lignes reposent sur l'hypothèse que l'instrument durci les fait disparaître. Une seule tentative. | #145 |

⚠️ **Aucune pile ne déclenche une correction manuelle d'étiquette.** Le montage
retenu par #145 est inchangé — machine = corpus, 300 colonnes humaines =
référence — et c'est le **tirage neuf** qui refait la référence, pas une reprise
des 300 brûlées.

## Conditions de réfutation, écrites avant les chiffres

Elles ne sont pas des précautions de style : chacune nomme un résultat qui rend
le geste suivant faux, et l'engagement est de **le dire** plutôt que de
poursuivre.

1. ⚠️ **Si la Pile B n'est pas majoritairement l'héritage du domaine par le nom
   de la table**, alors le § 3.10 tel que #145 le rédige **ne tranche pas la Pile
   B**. Il ferme la famille qu'il nomme, et le reste de B reste ouvert, sans
   règle — ce qui doit se publier, pas se taire.
2. ⚠️ **Si la Pile A est vide ou quasi vide**, le diagnostic de #145 — « la
   phrase du verdict était trop agnostique sur la Pile A » — est **faux**, et
   avec lui l'argument qui fait du durcissement de l'instrument le remède
   principal. Le durcissement resterait justifié par les quatre défauts
   *constatés* du cahier ; il perdrait sa justification *numérique*.
3. ⚠️ **Si la Pile C domine largement**, le pari de #145 ne porte plus sur un
   reste mais sur la majorité des désaccords, et « une seule tentative » devient
   une prise de risque qu'il faut énoncer comme telle avant le tirage, pas
   découvrir après.
4. ⚠️ **Si un désaccord n'entre dans aucune des trois piles**, c'est cette règle
   qui est en défaut. Il se signale nommément ici plutôt que d'être forcé dans la
   pile la moins gênante.

## Ce que ce classement ne rattrape pas

- Il porte sur les **79 désaccords tirés**, donc sur les seules colonnes que
  l'échantillon a touchées. **Rien ne garantit que la machine ait appliqué la
  même règle partout où le § 3.10 l'imposera** — c'est précisément ce que la
  passe d'`incoherences.py` du geste 2 va chercher, et c'est un « non couvert »
  déclaré par #145.
- Il est fait par **un seul lecteur**, comme tout le reste de cette carte. Le
  classement lui-même n'a **pas de kappa** ; sa garantie est le pré-enregistrement
  et la publication ligne à ligne, pas un accord.
- ⚠️ Il **hérite de la contamination doctrinale** déclarée par #145 : celui qui
  classe a lu le protocole, le verdict et #145. Le pré-enregistrement borne ce
  qu'il peut décider **après** avoir vu les lignes ; il ne le rend pas naïf.
