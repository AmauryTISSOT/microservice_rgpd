# Modèle A2 figé : plongements `bge-m3` et régression logistique

*Généré par `python -m pdmap.export.train_a2` (cible `make export-a2`) à partir de `manifest.json`. Ne pas éditer à la main.*

## Ce que c'est

Le modèle A2 retenu par le banc d'essai (`a2_embeddings`, contexte `C1`, tête `logreg`, augmentation ×0), entraîné sur les 9162 colonnes annotées des 35 schémas de dev, dont 1944 positives, graine 42. Il ne lit que le nom de la table et le nom de la colonne, jamais une valeur.

Pour chaque colonne, il rend :

- un **score** : `sigmoid(vecteur · coef + intercept)`, où le vecteur est le plongement normalisé L2 du texte sérialisé ;
- une **décision** : colonne personnelle si `score >= 0,4056`, seuil lu sur le dev par `tune_threshold` (β = 2), jamais sur le hold-out (valeur exacte dans `manifest.json`) ;
- une **catégorie** : le prototype personnel le plus proche ;
- une **explication** d'une ligne.

## Fichiers

| Fichier | Contenu |
|---|---|
| `weights.npz` | `coef` (float32, 1024) et `intercept` (float32, 1) |
| `prototypes.npz` | `vectors` (float32, 14 × 1024), normalisés, dans l'ordre de `prototype_names` |
| `manifest.json` | seuil, encodeur, gabarit, prototypes, provenance, limites |
| `reproduction.json` | preuve de reproduction du banc et contrôle d'équivalence |

Aucun pickle : l'artefact se recharge avec numpy seul.

## Recharger

Le module `src/pdmap/export/a2_model.py` ne dépend que de numpy et de la bibliothèque standard ; il se recopie tel quel.

```python
from a2_model import load

model = load("models/a2_c1_logreg")
host = "http://127.0.0.1:11434"
for p in model.predict([("users", "email"), ("products", "price")], host):
    print(p.column_name, p.score, p.is_personal, p.category, p.explanation)
```

`predict` vérifie le digest de l'encodeur avant tout encodage et lève `EncoderMismatch` s'il diffère.

## Préconditions

- Encodeur `bge-m3:latest` servi par Ollama, digest `7907646426070047a77226ac3e684fbbe8410524f7b4a74d02837e43f2146bab`. Même étiquette et autre digest : autres poids, le score ne vaut plus rien.
- Gabarit de mise en texte identique : `table: {table_name} | column: {column_name}`.
- Vecteurs de dimension 1024, normalisés L2 avant le produit scalaire.
- Construit avec Ollama 0.34.0.

## Performances de référence (banc d'essai)

| Mesure | Valeur | Périmètre | Source |
|---|---|---|---|
| F2 dev | 0,713 | moyenne des cinq plis de dev, hors pli | `results/metrics/metrics_dev.csv` |
| F2 hold-out | 0,697 [0,589 ; 0,774] | 12 schémas du hold-out, mesure unique | `results/metrics/metrics_holdout.csv` |
| Rappel hold-out | 0,828 | 12 schémas du hold-out, mesure unique | `results/metrics/metrics_holdout.csv` |
| Précision hold-out | 0,428 | 12 schémas du hold-out, mesure unique | `results/metrics/metrics_holdout.csv` |
| ECE hold-out | 0,159 | 12 schémas du hold-out, mesure unique | `results/metrics/metrics_holdout.csv` |

Ces chiffres sont ceux du banc. Pour savoir si cet artefact les reproduit, lire `reproduction.json`.

## Limites

- Score non calibré : ECE de 0,159 sur le hold-out. Le score ordonne les colonnes ; il ne se lit pas comme une probabilité.
- La catégorie est celle du prototype personnel le plus proche (similarité cosinus) : elle n'est pas apprise, et elle est rendue même pour une colonne jugée non personnelle.
- Validation humaine requise (D4) : rappel de 0,828 et précision de 0,428 sur le hold-out. Le moteur propose, l'humain décide.
- Valable seulement avec le même encodeur (digest) et le même gabarit de mise en texte ; le modèle ne lit que le nom de la table et le nom de la colonne.
- Mesuré sur des schémas d'applications libres ; aucun chiffre n'est une performance attendue sur un schéma d'entreprise.

## Provenance

- Commit : `f0a2654b0183d13de2f2bb1a54df83aa1d5897b1`.
- Données d'entraînement (sha256 des `column_id` triés et de leurs étiquettes) : `0d4b3830bade557028022396f9659baba8972b8d650c3896150ff354408c5ba8`.
- Python 3.11.14, numpy 2.4.6, scikit-learn 1.9.0.
