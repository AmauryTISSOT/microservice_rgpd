# Verdict — la vérité terrain est validée

*2026-08-10. Écrit après la mesure, jamais avant. Tentative 2, la seule accordée
par [#145](https://github.com/AmauryTISSOT/microservice_rgpd/issues/145).*

## Le chiffre

| Mesure | Effectif | Accord brut | Kappa de Cohen |
|---|---:|---:|---:|
| Toutes colonnes | 300 | 90,7 % | **0,793** |
| **Colonnes signalées** | **93** | **69,9 %** | **0,631** |

Le seuil de convention retenu par
[#130](https://github.com/AmauryTISSOT/microservice_rgpd/issues/130) était
κ = 0,60. La ligne qui compte — celle des colonnes signalées, la seule que le § 4
autorise à citer — en est à **0,631**.

**Conséquence, lue mécaniquement le 2026-08-10 : les 3 254 étiquettes machine
constituent la vérité terrain du banc.** Le banc de
[#134](https://github.com/AmauryTISSOT/microservice_rgpd/issues/134) part sur ce
socle, la branche d'échec écrite d'avance par #145 ne s'applique pas, et la
destination de la carte garde les mots « tranché par un banc mesuré ».

⚠️ **La marge est de 0,031, et elle n'a aucun intervalle.** À 93 colonnes
signalées, 0,631 et 0,60 ne sont probablement pas distinguables. Le seuil a été
traité comme une **porte** — ce qu'il est par convention — jamais comme une
mesure. C'est la condition qui rendait le verdict lisible sans renégociation, et
c'est exactement la même qui aurait fait abandonner le banc à 0,58.

## Ce qui a changé depuis 0,040, et ce n'est pas le jugement

Le montage est **identique** à celui de la tentative 1 : machine = corpus,
300 colonnes humaines = référence, plan de sondage inchangé, tirage neuf à graine
nouvelle sur le complémentaire des 300 brûlées. Ce qui a changé est
l'**instrument**, durci par
[#149](https://github.com/AmauryTISSOT/microservice_rgpd/issues/149).

| | Tentative 1 | Tentative 2 |
|---|---:|---:|
| κ colonnes signalées | 0,040 | **0,631** |
| Désaccords | 79 | **28** |
| Motifs sous 25 caractères | 37 sur 47 | **0 sur 91** |
| Médiane des motifs | — | **101 caractères** |
| Désaccords chez OpenEMR | 33 | **7** |

⚠️ **Le remède n° 1 se vérifie négativement : aucun des 91 motifs n'est
irrecevable.** Là où 19 motifs sur 47 disaient « nom de la colonne » et 11 « nom
de la table », les 91 citent tous une sous-chaîne du pivot ou un `§`. Le pari de
#149 n'était pas que l'humain jugerait mieux, mais qu'un instrument refusant les
motifs vides le ferait juger **autrement** — et le § 2 dit pourquoi : c'est le
motif qui rend une étiquette contestable, donc un désaccord arbitrable.

⚠️ **Le § 3.10 a fait ce qu'on attendait de lui.** La famille « machine
`Unflagged` → humain `HealthData` », qui pesait 18 désaccords sur 79 et
constituait toute la Pile B, tombe à **4**.

## Le résultat non prévu : les désaccords ne sont plus symétriques

Sur les 28 désaccords, **22 sont de la forme « machine `Unflagged`, humain
signale »**, 2 l'inverse, 4 opposent deux catégories signalées. La vérité terrain
**sous-signale** par rapport à une passe humaine outillée. Trois familles portent
presque tout :

1. **§ 3.9, faits financiers — 8 colonnes.** `remise_percent`,
   `discount_percent`, `mode_reglement`, `price`, `plan_name`,
   `ext_payment_site`. L'humain rattache le fait financier à la personne par la
   table qui le porte (`llx_product_customer_price_log`, `insurance_data`) ; la
   machine a laissé `Unflagged`. Le § 3.9 est né **pendant** l'annotation de #142
   et n'a produit que six bascules : il n'a jamais été repassé sur l'ensemble du
   corpus.
2. **§ 3.3 cas 2, champ libre sous clé étrangère vers une personne —
   7 colonnes**, de même forme.
3. **§ 3.10 — 4 colonnes** résiduelles.

⚠️ **C'est une huitième réserve pour le rendu de #134, et elle mord sur le
chiffre.** Le banc note des moteurs contre une vérité terrain qui sous-signale :
un moteur qui signalerait `remise_percent` **à raison** sera compté faux positif.
Avec un F2 pondéré sur le rappel, le biais joue **contre les moteurs sensibles**.
Sa direction est connue, son ampleur non.

⚠️ **Et cette réserve ne se répare pas.** Repasser le § 3.9 sur les
3 254 colonnes maintenant serait une chirurgie **post-hoc sur la vérité terrain,
décidée en ayant vu les désaccords** — le geste même que #142 a refusé en
écartant une seconde passe de codage, et pour le motif exact : 0,631 obtenu
proprement vaut mieux que 0,70 obtenu après avoir su où appuyer. La
sous-signalisation se **déclare**, elle ne se corrige pas.

## Les quatre réserves obligatoires de #145, tenues

1. **Accord intra-annotateur** — un seul codeur ; 0,631 mesure la stabilité d'une
   personne autant que la justesse d'une étiquette.
2. **Circularité** — la vérité terrain est produite par des modèles de langue,
   les montages mesurés ne le sont pas. Septième réserve à porter au rendu de
   #134. Atténuée par le fait que la Pile B s'atteint par le nom de la table,
   donc dans le régime schéma-seul ; non supprimée.
3. **Contamination doctrinale déclarée, jamais réparée** — l'annotateur abordait
   cette passe en sachant que les clés étrangères et l'héritage par la table
   étaient les deux familles qui se jouaient. Un tirage neuf ne l'annule pas.
4. **Le § 3.10 est un arbitrage post-hoc en faveur de la machine** sur la famille
   la plus lourde du corpus — et il vient de rapporter 14 désaccords évités
   sur 79.

## Ce que ce chiffre ne dit pas

- **Il ne dit pas que la machine a raison colonne par colonne.** κ est une
  distance entre deux passes, pas un jugement sur l'une d'elles ; il valide la
  référence **en moyenne**. Une famille où les agents se trompent
  systématiquement et que l'échantillon touche peu passe au travers.
- **Les 2 désaccords inverses** (`Identity` → `Unflagged`) ne sont pas arbitrés
  et sont publiés tels quels — comme les deux erreurs machine de Pile A de #149.
- **`CriminalOffenceData`, `SpecialCategoryData` et `LocationData` restent à
  zéro** dans la distribution : ce sont les trous déclarés par
  [#129](https://github.com/AmauryTISSOT/microservice_rgpd/issues/129) et
  [#137](https://github.com/AmauryTISSOT/microservice_rgpd/issues/137), pas des
  lacunes de cette passe.
- **Le codage n'a pas de kappa sur lui-même** : un seul lecteur, ici comme
  en #149.

## Ce que cela ferme, et ce que cela ouvre

- **Fermé** :
  [#150](https://github.com/AmauryTISSOT/microservice_rgpd/issues/150). La
  tentative unique est consommée, et le résultat est publié tel qu'il est sorti.
- **Ouvert** : le banc de #134 part, avec **huit** réserves au rendu et non sept.
- **Statut de `distribution.md`** : la distribution décrit les étiquettes
  machine, qui ne sont plus provisoires. Elle hérite désormais du statut
  **validé**, sous les réserves ci-dessus.
