# Avis figés des moteurs sur le corpus

Ce que les moteurs **actuellement servis** rendent sur les 120 exemples de
[`corpus/demandes-rgpd.fr.jsonl`](../corpus/demandes-rgpd.fr.jsonl). C'est le point de comparaison
de la [carte #42](https://github.com/AmauryTISSOT/microservice_rgpd/issues/42), qui cherche si un
troisième moyen de détection, non génératif, apporte au `ReviewSignal` une valeur d'alarme que le
montage à deux moteurs n'a pas : on ne mesure pas une valeur marginale sans savoir ce qui existe
déjà.

- Ticket : [Figer les avis du lexique et de qwen3:8b sur les 120 exemples du corpus](https://github.com/AmauryTISSOT/microservice_rgpd/issues/46)

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
