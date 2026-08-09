# Verdict — la vérité terrain n'est pas validée

*2026-08-09. Écrit après la mesure, jamais avant.*

## Le chiffre

| Mesure | Effectif | Accord brut | Kappa de Cohen |
|---|---:|---:|---:|
| Toutes colonnes | 300 | 73,7 % | **0,298** |
| **Colonnes signalées** | **97** | **18,6 %** | **0,040** |

Le seuil de convention retenu par
[#130](https://github.com/AmauryTISSOT/microservice_rgpd/issues/130) était
κ = 0,60. La ligne qui compte — celle des colonnes signalées, la seule que le § 4
autorise à citer — en est à 0,040, c'est-à-dire l'accord qu'on obtiendrait en
tirant les étiquettes au sort.

**Conséquence, tranchée le 2026-08-09 : les 3 254 étiquettes machine ne
constituent pas une vérité terrain validée.** Elles restent dans `annotation/`,
elles ne sont pas détruites, et elles ne servent de référence à aucune mesure de
moteur.

## Pourquoi on publie ce chiffre plutôt qu'un meilleur

Une seconde passe de codage avait été proposée et **écartée**. Elle aurait
produit un kappa plus flatteur, et c'est exactement ce qui la disqualifiait : la
seconde passe se serait faite en connaissant l'existence du désaccord, donc plus
en aveugle. Publier 0,040 obtenu proprement vaut mieux que publier 0,55 obtenu
après avoir su qu'il fallait faire mieux.

C'est la règle que le § 4 posait d'avance : **l'accord se publie avant tout
chiffre de moteur, quel qu'il soit.**

## Ce que ce chiffre ne dit pas — limites de la mesure elle-même

⚠️ **Il faut lire 0,040 en sachant ceci, sans quoi le dossier est trompeur dans
l'autre sens.** Ces faits sont constatés sur le cahier de référence, ils ne sont
pas des hypothèses :

1. **Les motifs de la passe humaine ne sont pas des motifs.** Sur les 47 posés,
   19 disent « nom de la colonne » et 11 « nom de la table » ; 37 sur 47 font
   moins de 25 caractères. Le § 2 exige le motif parce que c'est lui qui rend une
   étiquette **contestable** : sans lui, aucun désaccord n'est arbitrable, et la
   matrice ne peut pas être exploitée pour dire *qui* a tort.
2. **Une saisie se déclare elle-même fausse** — un motif porte le texte
   « erreur ici$ ».
3. **Il existe une signature de faute de frappe.** `ConnectionData` et `Identity`
   sont voisines au menu de saisie (8 et 9), de même que `LocationData` et
   `ContactDetails` (7 et 10). Plusieurs colonnes classées `ConnectionData`
   portent un motif qui dit « foreign key » — or le § 3.2 range une clé étrangère
   vers une personne en `Identity`. Le motif contredit la catégorie.
4. **Le § 3.2 n'a pas été appliqué sur la famille des clés étrangères**, qui est
   numériquement la première source de désaccord.

Autrement dit : **0,040 mesure la distance entre deux passes dont l'une a des
défauts de saisie constatés.** Il ne démontre pas que les étiquettes machine sont
mauvaises ; il démontre qu'**on ne dispose d'aucune référence humaine permettant
de le savoir**. C'est la même conclusion opérationnelle — la vérité terrain n'est
pas validée — mais ce n'est pas le même fait, et confondre les deux ferait
condamner un travail qui n'a pas été jugé.

## Ce que cela ferme, et ce que cela ouvre

- **Fermé** : #142. La question posée était « les 3 254 colonnes sont-elles
  annotées et l'accord est-il mesuré ? ». Elles le sont, il l'est. La réponse est
  **négative** ; un ticket de décision se ferme aussi sur un non.
- **Ouvert** : le banc de
  [#134](https://github.com/AmauryTISSOT/microservice_rgpd/issues/134) **ne peut
  pas partir sur ce socle**. Mesurer un moteur contre une vérité terrain non
  validée produirait un chiffre dont personne ne pourrait dire s'il juge le
  moteur ou la référence. La question du socle devient un ticket à elle seule.

## Ce qui reste utilisable

- Le **protocole** (`PROTOCOLE-ANNOTATION.md`), amendé de trois règles que
  l'annotation a fait apparaître (§ 3.3 cas 2, § 3.8, § 3.9). Il vaut pour toute
  reprise.
- Le **plan de sondage** et son tirage rejouable : probabilités d'inclusion,
  strates, seed.
- L'**outillage** : `valider.py`, `accord.py`, `distribution.py`,
  `incoherences.py`, `coder.py`.
- Le **registre des limites de méthode**, enrichi de ce que l'annotation a vu
  sans pouvoir l'étiqueter — dont le fait que `HealthData` chez OpenEMR est porté
  par les noms de **tables** et presque jamais par ceux des colonnes.
- La **distribution** (`distribution.md`), pondérée par les probabilités
  d'inclusion. ⚠️ Elle décrit les étiquettes machine ; elle hérite donc du même
  statut non validé.
