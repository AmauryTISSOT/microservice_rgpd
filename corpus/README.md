# Corpus annoté de demandes RGPD en français

Vérité terrain servant à **choisir** puis à **évaluer** le moteur de qualification du microservice.

- Ticket : [Constituer un corpus annoté de demandes RGPD en français](https://github.com/AmauryTISSOT/microservice_rgpd/issues/2)
- Carte : [Carte : qualification RGPD d'un texte reçu d'une application tierce](https://github.com/AmauryTISSOT/microservice_rgpd/issues/1)
- Fichier : [`demandes-rgpd.fr.jsonl`](./demandes-rgpd.fr.jsonl) — 120 exemples

## Format et emplacement

**JSONL** (un objet JSON par ligne, UTF-8, fins de ligne `\n`), dans `corpus/` à la racine du dépôt.

Pourquoi ce choix :

- **JSONL plutôt que JSON, CSV ou Markdown** — une ligne par exemple, donc un diff Git lisible exemple par exemple ; pas d'échappement de retours à la ligne ni de virgules dans les textes libres, contrairement au CSV ; lecture en flux, sans charger tout le fichier.
- **`corpus/` à la racine plutôt que sous `src/` ou `tests/`** — l'actif est consommé par au moins deux mondes : les prototypes jetables du ticket [#6](https://github.com/AmauryTISSOT/microservice_rgpd/issues/6), qui vivent hors de la solution, et plus tard les tests d'évaluation. Le rattacher à un projet .NET précis maintenant présumerait d'une décision qui n'est pas prise.
- **Nom `demandes-rgpd.fr.jsonl`** — le suffixe de langue laisse la place à d'autres corpus si le périmètre linguistique s'élargit un jour.

## Schéma

Chaque ligne porte exactement ces sept champs, tous obligatoires :

| Champ | Type | Contenu |
| --- | --- | --- |
| `id` | `string` | Identifiant stable, `<préfixe>-<numéro>`. Ne jamais réutiliser un identifiant retiré. |
| `texte` | `string` | Le texte reçu, tel qu'il arriverait dans la requête HTTP. |
| `droits` | `string[]` | Les droits attendus. Non vide. |
| `registre` | `string` | Registre de langue du texte. |
| `categorie` | `string` | Rôle de l'exemple dans le corpus. |
| `note` | `string` | Pourquoi cette annotation, et le piège que l'exemple tend. |
| `source` | `string` | Provenance de la formulation. |

### Valeurs de `droits`

La taxonomie fermée de sept valeurs actée sur la carte :

`acces` (art. 15) · `rectification` (art. 16) · `effacement` (art. 17) · `limitation` (art. 18) · `portabilite` (art. 20) · `opposition` (art. 21) · `hors-perimetre`

Invariant : `hors-perimetre` est **exclusif** — il n'apparaît jamais avec un autre droit.

Ces valeurs sont celles de la taxonomie, pas nécessairement les identifiants du code : le vocabulaire du domaine est fixé par le ticket [#5](https://github.com/AmauryTISSOT/microservice_rgpd/issues/5). Si #5 retient d'autres termes, ce fichier se renomme d'un coup.

### Valeurs de `registre`

`juridique` (article cité, tournure de courrier formel) · `courant` (français correct, sans lexique juridique) · `familier` (oral, elliptique) · `maladroit` (orthographe et ponctuation dégradées)

### Valeurs de `categorie`

| Valeur | Rôle |
| --- | --- |
| `nominal` | Un seul droit, formulation représentative. Le socle. |
| `multi-droits` | Plusieurs droits légitimement portés par un même texte. |
| `hors-perimetre` | Aucun droit de la taxonomie n'est exercé. |
| `limite` | Cas difficile : très court, très long, ambigu, forme interrogative, négation, paire minimale. |

### Valeurs de `source`

`invente` · `inspire-cnil` (calqué sur un modèle de courrier ou une page CNIL) · `inspire-edpb` (calqué sur un cas des lignes directrices CEPD)

## Règles d'annotation

Tirées de la recherche [#4](https://github.com/AmauryTISSOT/microservice_rgpd/issues/4) (détail sur la branche `research/droits-rgpd-formulations`) :

1. **Aucun formalisme n'est exigible.** Un texte sans mention du RGPD ni d'article est une demande valide (CEPD, *Guidelines 01/2022* § 50). Inversement, citer un article ne suffit pas — et un article mal cité ne prime pas sur le contenu (`edg-16`).
2. **Pas de repli par défaut.** Face à une demande floue entre opposition et effacement, le CEPD proscrit de retenir l'opposition par commodité (*Guidelines 1/2024* § 77). L'annotation retient alors **les deux**.
3. **Opposition ou effacement** — une finalité nommée vise l'*usage* → opposition ; la totalité sans finalité vise l'*existence* → effacement.
4. **Limitation** — une durée ou une condition de fin, ou la double instruction « ne plus utiliser / ne pas supprimer ».
5. **Accès ou portabilité** — une intention de réutilisation ailleurs ou un format machine nommé → portabilité ; une intention de vérification ou de compréhension, ou des données inférées → accès. Sans aucun indice, les deux (`mul-07`, `edg-07`).
6. **Rectification ou effacement** — la présence d'une valeur de remplacement tranche (`edg-13` contre `edg-14`).
7. **Le retrait de consentement n'est pas une opposition** — art. 7 §3, qui déclenche l'effacement par l'art. 17 §1 b) (`eff-05`, `edg-17`).
8. **Les droits hors taxonomie retombent en `hors-perimetre`** — déréférencement, droit à l'information, art. 22 : ce sont des demandes RGPD réelles, mais la carte les a explicitement mises hors périmètre (`hop-11`, `hop-12`, `hop-13`).

## Composition

| Droit attendu | Exemples |
| --- | --- |
| `acces` | 23 |
| `effacement` | 26 |
| `opposition` | 20 |
| `rectification` | 14 |
| `limitation` | 14 |
| `portabilite` | 13 |
| `hors-perimetre` | 30 |

(La somme dépasse 120 : 19 exemples portent plusieurs droits.)

| Catégorie | Exemples | | Registre | Exemples |
| --- | --- | --- | --- | --- |
| `nominal` | 57 | | `courant` | 74 |
| `hors-perimetre` | 25 | | `juridique` | 25 |
| `limite` | 22 | | `familier` | 13 |
| `multi-droits` | 16 | | `maladroit` | 8 |

**Déséquilibres assumés.** La limitation et la portabilité sont les moins représentées : la CNIL ne publie aucun modèle de courrier pour ces deux droits, donc aucune formulation officielle de référence n'existe en français. Le hors-périmètre est au contraire sur-représenté — c'est la classe où un faux positif coûte le plus cher à l'opérateur humain qui valide.

## Données personnelles

**Aucune donnée réelle.** Tous les textes sont inventés. Les adresses électroniques utilisent le domaine réservé `.invalid`, les numéros de téléphone sont des séquences de zéros, les noms, sociétés et références de commande sont fictifs. Le corpus peut donc être envoyé à un moteur tiers lors du prototypage sans engager de transfert de données personnelles — ce qui est précisément ce que le ticket [#6](https://github.com/AmauryTISSOT/microservice_rgpd/issues/6) doit pouvoir faire.

## Paires minimales

Le corpus contient des couples volontairement quasi identiques, dont la qualification diffère. Ce sont les exemples les plus discriminants pour départager deux moteurs :

| Paire | Départage |
| --- | --- |
| `edg-13` / `edg-14` | Présence ou non d'une valeur de remplacement : rectification ou effacement. |
| `edg-04` / `edg-05` | Question *sur* un droit ou exercice du droit. |
| `eff-02` / `hop-03` | Supprimer un compte ou résilier un abonnement. |
| `eff-03` / `hop-24` | Même impératif familier, objet « données » ou objet « contrat ». |
| `opp-02` / `mul-01` | L'enregistrement est préservé, ou il est aussi visé. |
| `edg-11` / `edg-12` | Négation portant sur l'effacement : opposition ou limitation. |
| `acc-01` / `hop-22` | Demande d'accès RGPD ou demande fondée sur une législation sectorielle. |
| `eff-05` / `opp-01` | Retrait de consentement ou opposition à une finalité. |

## Faire évoluer le corpus

- Ajouter, ne pas remplacer : un identifiant retiré n'est jamais réattribué, sinon les comparaisons entre exécutions du ticket #6 deviennent illisibles.
- Toute annotation contre-intuitive se justifie dans `note`, en renvoyant si possible à un article ou à une ligne directrice.
- Les invariants du schéma (sept champs, valeurs d'énumération, exclusivité de `hors-perimetre`) ne sont pour l'instant vérifiés par aucun test automatisé. Le ticket [#11](https://github.com/AmauryTISSOT/microservice_rgpd/issues/11) décidera si cette vérification entre dans la barre de qualité.
