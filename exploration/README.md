# Exploration : le banc d'essai du troisième témoin

Le matériau et le banc d'essai de la [carte #42](https://github.com/AmauryTISSOT/microservice_rgpd/issues/42),
qui cherche si un troisième moyen de détection, **non génératif**, apporte au `ReviewSignal` une
valeur d'alarme que le montage à deux moteurs n'a pas.

| Fichier | Rôle | Ticket |
| --- | --- | --- |
| [`qwen3-8b-corpus.jsonl`](./qwen3-8b-corpus.jsonl) | Les avis figés de `qwen3:8b` sur les 120 exemples — le point de comparaison. | [#46](https://github.com/AmauryTISSOT/microservice_rgpd/issues/46) |
| [`regenerate.py`](./regenerate.py) | Rejoue ces avis contre le moteur **servi**. | [#46](https://github.com/AmauryTISSOT/microservice_rgpd/issues/46) |
| [`troisieme-temoin.ipynb`](./troisieme-temoin.ipynb) | Le banc d'essai lui-même, sorties comprises : il **est** le rendu. | [#50](https://github.com/AmauryTISSOT/microservice_rgpd/issues/50) |
| [`temoin-predictions.jsonl`](./temoin-predictions.jsonl) | Les prédictions hors-pli, par montage × germe × pli. | [#50](https://github.com/AmauryTISSOT/microservice_rgpd/issues/50) |

## Deux environnements, une règle qui les départage

Ce dossier porte deux commandes, et la règle tient en une ligne :

> Ce qui **parle à un moteur servi** tourne avec l'environnement de **production** ; ce qui
> **calcule sur des fichiers figés** tourne avec celui de l'**exploration**.

Concrètement, `regenerate.py` appelle le sidecar servi et tourne donc sous
`uv run --project src/sidecar` ; le notebook ne lit que des fichiers plats et tourne sous
`uv run --project exploration`.

Ce n'est pas un arrangement de commodité. Faire produire les avis figés sous une résolution de
dépendances qui n'est plus celle de la production affaiblirait leur argument central — *le chemin
est celui de la production, pas une maquette*. **La frontière que la carte défend se lit ainsi dans
la commande elle-même.**

Le projet d'exploration ne déclare **aucune dépendance** sur le sidecar, pas même en `path` : le
lock de la production ne résout jamais `torch`, et la sobriété de ses quatre dépendances devient un
fait de structure plutôt qu'une affaire de `uv sync --no-group` bien tapé.

## Pourquoi ce dossier, et pas `src/sidecar/tests/witness/`

Le dossier `witness` porte le **témoin de non-régression du lexique** : la suite de tests le relit à
chaque exécution, et tout écart y est un échec jusqu'à preuve du contraire. Ce qui vit ici est de la
**donnée d'exploration** — aucun test ne la lit, et un écart d'une exécution à l'autre n'est pas une
régression mais un fait à interpréter. Les ranger ensemble reviendrait à promettre à un relecteur
qu'un diff dans ce dossier doit l'alarmer, ce qui est faux ici.

Le dossier est à la racine pour la même raison que `corpus/` : l'artefact est consommé par un
notebook d'exploration, pas par un projet .NET ni par le sidecar. Le rattacher à l'un des deux
présumerait d'une décision qui n'est pas prise.

## Le lexique : aucun fichier ici

Le lexique a **déjà** son fichier d'avis figés — c'est
[`src/sidecar/tests/witness/lexicon-corpus.jsonl`](../src/sidecar/tests/witness/lexicon-corpus.jsonl),
vérifié à jour au moment où ce dossier est né : le rejouer par
[`tests/witness/regenerate.py`](../src/sidecar/tests/witness/regenerate.py) ne produit aucun écart.
Le recopier ici en ferait une seconde vérité qui divergerait au premier changement de règle du
lexique — exactement ce que le dépôt évite en faisant lire le corpus aux tests plutôt qu'en le
copiant. **Le notebook lit le témoin là où il est.**

Le témoin ne porte que `id` et `rights`, et c'est complet : le lexique n'a ni confiance ni
justification, par construction. Sa version de moteur n'y figure pas non plus — elle est
`lexicon.ENGINE_VERSION`, tenue dans le même commit que le fichier.

## `qwen3-8b-corpus.jsonl`

Une ligne par exemple, dans l'ordre du corpus, produite par le point d'entrée `/opinions/llm` du
sidecar — le chemin de production, pas une maquette.

Avis rendu :

| Champ | Contenu |
| --- | --- |
| `id` | L'identifiant de l'exemple du corpus. |
| `status` | `200`. |
| `rights` | Les droits, en noms de fil. |
| `confidence` | `High`, `Medium` ou `Low`. |
| `justification` | Le texte français que lirait l'opérateur humain. |
| `engine` | `{ "name": "llm", "version": "<modèle servi>+prompt.<version>" }`. |
| `duration_seconds` | Le temps de l'appel, échéance comprise. |

Panne du moteur — le sidecar traite un avis invalide comme une **panne**, jamais comme un avis
faible, et l'artefact garde cette distinction :

| Champ | Contenu |
| --- | --- |
| `id`, `status`, `duration_seconds` | Comme ci-dessus, `status` valant `502`, `503` ou `504`. |
| `failure` | Le corps `application/problem+json` rendu par le sidecar. |

## Régénérer

Le serveur de modèles doit tourner. Sous Aspire, il est publié sur un port local que la console
donne ; sinon, un `ollama serve` avec `qwen3:8b` tiré fait l'affaire. Les réglages sont ceux de
[`appsettings.json` de l'AppHost](../src/MicroserviceRgpd.AspireHost/appsettings.json) — température
nulle et graine fixe, sans quoi l'artefact ne serait pas rejouable.

```powershell
$env:QUALIFICATION_LLM_BASE_URL = "http://127.0.0.1:<port>/v1"
$env:QUALIFICATION_LLM_MODEL = "qwen3:8b"
$env:QUALIFICATION_LLM_API_KEY = "ollama-ne-lit-pas-cette-cle"
$env:QUALIFICATION_LLM_TEMPERATURE = "0"
$env:QUALIFICATION_LLM_SEED = "20180525"
$env:QUALIFICATION_LLM_DEADLINE_SECONDS = "120"
$env:QUALIFICATION_LLM_CALLER_DEADLINE_SECONDS = "150"

uv run --project src/sidecar python exploration/regenerate.py
```

Comptez une vingtaine de minutes : 8 Go de VRAM sérialisent les appels, et le script ne parallélise
rien — de front, les appels ne gagneraient rien et se dépasseraient mutuellement l'échéance. Une
exécution interrompue reprend où elle en était : supprimer le fichier force la reprise à zéro.

**Aucun test du dépôt n'appelle Ollama, et ce dossier ne change pas cette règle.** Ce qui vit ici est
produit par un script, à la main, quand quelqu'un décide de le produire.

## `troisieme-temoin.ipynb` — le banc d'essai

Le notebook **est** le rendu du ticket [#50](https://github.com/AmauryTISSOT/microservice_rgpd/issues/50) :
son fil narratif vit dans ses cellules Markdown, GitHub le rend nativement, et ses sorties sont
versionnées. Il n'y a pas de rapport dérivé — un artefact de plus serait un second endroit où un
chiffre peut être faux. Le verdict vit en commentaire de résolution de
[#52](https://github.com/AmauryTISSOT/microservice_rgpd/issues/52).

```powershell
uv run --project exploration jupyter lab exploration/troisieme-temoin.ipynb
```

**Aucune image embarquée, jamais.** Les résultats sont des tableaux et des intervalles textuels.
Une figure `matplotlib` dans un `.ipynb` est un PNG en base64 — c'est-à-dire exactement le binaire
versionné que ce dépôt refuse partout ailleurs, glissé à l'intérieur d'un fichier qui a l'air d'être
du texte. C'est la contrepartie non négociable des sorties versionnées.

**Comptez une bonne demi-heure avec une carte NVIDIA, plusieurs heures sans.** Le montage SetFit
réentraîne l'encodeur une fois par pli externe, soit 25 entraînements contrastifs. La durée mesurée
est imprimée dans le notebook.

### L'entraînement contrastif tourne sur le GPU ; la latence se mesure sur le CPU

Ce n'est pas une hésitation, c'est la même règle appliquée deux fois : **on mesure un coût là où il
serait payé**.

- La **latence** rapportée est celle du **service**. Le sidecar déployé n'a pas de carte à lui —
  l'ADR-0001 la donne à `qwen3:8b`. Une latence GPU décrirait un service qui n'existe pas, donc
  l'encodeur du montage 2 est chargé sur CPU et y reste.
- La **durée d'entraînement** est un coût de **banc**. On la paie une fois, hors production, et
  rien n'oblige à la payer sur le processeur. Mesurée sur CPU, elle valait 832 s par corps, soit
  près de six heures les huit cœurs physiques saturés d'un bout à l'autre — au point de rendre la
  machine inutilisable pendant l'exécution. Sur une RTX 3070 Laptop : 72 s de médiane sur les 25,
  33 min au total, et 4,13 Gio de VRAM de crête.

Les roues CUDA sont donc **verrouillées dans `uv.lock`** (index `download.pytorch.org/whl/cu126`,
`torch` déclaré en dépendance directe pour que `[tool.uv.sources]` s'y applique). Elles fonctionnent
sans carte : le notebook détecte l'absence de GPU et retombe sur le CPU tout seul.

**Ne faites pas tourner `qwen3:8b` en même temps.** 8 Gio de VRAM ne logent pas les deux. Le
notebook imprime la VRAM libre au démarrage et prévient s'il en reste moins de 5 Gio — mais il ne
vous en empêchera pas.

**Premier lancement : ~470 Mo de téléchargement, réseau requis une seule fois.** Les poids de
`intfloat/multilingual-e5-small` vivent dans le cache Hugging Face de l'utilisateur, **hors de
l'arborescence du dépôt** : la règle « aucun binaire versionné » n'est donc même pas mise à
l'épreuve. La **révision est épinglée** dans le notebook — `main` sur le Hub peut bouger, et sans
épinglage deux exécutions à six mois d'écart chargeraient deux modèles différents sans que rien ne
le signale.

Ce téléchargement **n'entame pas l'auto-hébergement strict de l'ADR-0001** : c'est un flux
*entrant*, une fois, de paramètres publics. Aucun texte du corpus ne quitte la machine et
l'inférence est entièrement locale — il n'y a pas de sous-traitance au sens de l'art. 28 quand rien
de personnel ne sort.

### Reproductibilité : les conclusions, pas les décimales

| Situation | Exigence |
| --- | --- |
| Même machine, même `uv.lock`, même appareil d'entraînement | **Les mêmes chiffres.** |
| Machine différente, ou CPU au lieu du GPU | **Le même verdict** — la borne haute de Wilson reste du même côté de la barre. |

L'identité bit à bit entre machines n'est pas atteignable avec `torch` — nombre de fils BLAS,
version de bibliothèque, jeu d'instructions, noyaux CUDA —, et la prétendre serait une promesse
creuse. `cudnn.deterministic` est activé et `cudnn.benchmark` désactivé : le corps contrastif n'a
aucune convolution, la contrainte ne coûte donc rien, et elle retire de l'équation le choix
d'algorithme au chronomètre.
`torch.use_deterministic_algorithms(True)` avec un seul fil BLAS a été **explicitement refusé** :
il allongerait considérablement 25 entraînements SetFit et ferait lever certaines opérations au
lieu de tourner. Ce serait payer cher une propriété dont l'ADR-0001 n'a besoin que pour le
**service**, pas pour le banc d'essai qui le choisit.

Une cellule versionnée imprime l'environnement d'exécution — versions, nombre de fils, CPU. Sans
elle, un écart de chiffres entre deux machines serait indécidable ; avec elle, il est
diagnosticable.

## `temoin-predictions.jsonl`

Une ligne par **montage × germe × pli × exemple**, écrite par le notebook. Même nature et même
justification que `qwen3-8b-corpus.jsonl`, dont elle partage le dossier.

| Champ | Contenu |
| --- | --- |
| `montage` | `tfidf`, `e5-gele` ou `setfit`. |
| `germe`, `pli` | Le germe de découpage et le pli **de test** d'où sort cette prédiction. |
| `id` | L'identifiant de l'exemple du corpus. |
| `probabilites` | Les sept probabilités brutes de la tête, par slug. |
| `brut` | Les étiquettes franchissant leur seuil, **avant** la règle d'arbitrage. |
| `predit` | La sortie après arbitrage — l'exclusivité de `hors-perimetre` y est acquise. |
| `marge` | Écart entre la plus haute probabilité et la suivante. Score **continu**. |
| `aucun_seuil_franchi` | Le témoin n'a rien reconnu et retombe sur le résidu. |
| `violation_i2_brute` | La sortie brute violait l'exclusivité, et l'arbitrage a tranché. |
| `reglage` | La configuration de tête retenue par les plis **internes**. |

L'effet recherché : [#52](https://github.com/AmauryTISSOT/microservice_rgpd/issues/52) recalcule les
chiffres du verdict **en secondes** au lieu de rejouer 25 entraînements SetFit. Le protocole et le
critère sont des agrégations de ces prédictions : les avoir sous forme de donnée rend le verdict
**vérifiable indépendamment du notebook qui le produit**.

Contrepartie : toute reprise du notebook doit réécrire cet artefact honnêtement, faute de quoi il
décrirait un montage que personne ne fait tourner.
