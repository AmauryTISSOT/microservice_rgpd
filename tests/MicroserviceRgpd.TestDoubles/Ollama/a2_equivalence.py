"""Produit `a2-equivalence.json`, le jeu figé du contrôle d'équivalence du moteur A2 (#463).

Les vecteurs sont FABRIQUÉS, pas encodés : aucun Ollama ne tourne en test, et le contrôle ne porte
pas sur l'encodeur mais sur ce que le service fait des vecteurs — normalisation, régression, seuil,
prototype le plus proche. Chaque vecteur mêle un prototype de l'artefact, la direction de `coef` et
un bruit fixe, et la part de `coef` est ajustée par dichotomie pour viser un score. Les scores, les
décisions et les catégories sont ensuite calculés par `a2_model.py` lui-même, jamais par ce script.

Deux colonnes sont posées à ±1e-5 du seuil : un moteur dont le score s'écarte de plus de 1e-5 de
celui de Python rend au moins l'une des deux du mauvais côté.

Usage, depuis la racine du dépôt :

    uv run --with numpy python tests/MicroserviceRgpd.TestDoubles/Ollama/a2_equivalence.py \\
        <recherche_schema_v2>/src/pdmap/export \\
        src/MicroserviceRgpd.Infrastructure/Screenings/Embeddings/Artefact \\
        tests/MicroserviceRgpd.TestDoubles/Ollama/a2-equivalence.json
"""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import numpy as np

# (table, colonne, prototype visé ou None, écart visé au seuil)
COLUMNS = [
    ("users", "email", "contact", 0.25),
    ("users", "first_name", "identity", 0.30),
    ("users", "password_hash", "authentication", 0.20),
    ("patients", "blood_group", "health", 0.20),
    ("orders", "customer_id", "person_link", 0.15),
    ("products", "price", None, -0.30),
    ("products", "sku", None, -0.25),
    ("notes", "body", "free_text", 1e-5),
    ("orders", "status", "location", -1e-5),
]

# Ce que le double rend pour un texte qu'il ne connaît pas : une colonne sous le seuil.
DEFAULT_OFFSET = -0.35

# Aucun vecteur n'arrive normalisé : un moteur qui oublierait la normalisation L2 rendrait d'autres
# scores.
SCALE = 3.7


def main(module_dir: str, artefact_dir: str, output: str) -> None:
    sys.path.insert(0, module_dir)
    import a2_model  # noqa: PLC0415 — le module est recopié tel quel, hors de tout paquet

    model = a2_model.load(artefact_dir)
    rng = np.random.default_rng(463)
    coef_unit = model.coef / np.linalg.norm(model.coef)
    names = model.prototype_names

    def vector_for(prototype: str | None, offset: float) -> np.ndarray:
        noise = rng.standard_normal(model.coef.shape[0])
        noise /= np.linalg.norm(noise)
        base = (0.9 * model.prototypes[names.index(prototype)] + 0.35 * noise
                if prototype is not None else noise)
        target = model.threshold + offset

        def score(b: float) -> float:
            return float(model.score_vectors((base + b * coef_unit)[None, :])[0][0])

        low, high = -50.0, 50.0
        if not score(low) < target < score(high):
            raise SystemExit(f"score {target} hors d'atteinte pour {prototype}")
        for _ in range(200):
            middle = (low + high) / 2
            low, high = (middle, high) if score(middle) < target else (low, middle)
        return SCALE * (base + (low + high) / 2 * coef_unit)

    rows = []
    for table, column, prototype, offset in COLUMNS:
        vector = vector_for(prototype, offset)
        scores, categories, _ = model.score_vectors(vector[None, :])
        if prototype is not None and categories[0] != prototype:
            raise SystemExit(f"{table}.{column} : prototype le plus proche {categories[0]}, "
                             f"{prototype} visé")
        rows.append({
            "text": model.serialize(table, column),
            "table": table,
            "column": column,
            "score": float(scores[0]),
            "is_personal": bool(scores[0] >= model.threshold),
            "category": categories[0],
            "vector": [float(x) for x in vector],
        })

    default = vector_for(None, DEFAULT_OFFSET)
    default_score = float(model.score_vectors(default[None, :])[0][0])

    manifest = Path(artefact_dir, a2_model.MANIFEST_FILE).read_bytes()
    Path(output).write_text(json.dumps({
        "provenance": "vecteurs fabriqués par a2_equivalence.py ; scores, décisions et catégories "
                      "calculés par a2_model.py sur l'artefact a2_c1_logreg",
        "manifest_sha256": hashlib.sha256(manifest).hexdigest(),
        "threshold": model.threshold,
        "encoder": {"tag": model.encoder["tag"], "digest": model.encoder["digest"]},
        "columns": rows,
        "default": {
            "score": default_score,
            "is_personal": bool(default_score >= model.threshold),
            "vector": [float(x) for x in default],
        },
    }, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main(*sys.argv[1:4])
