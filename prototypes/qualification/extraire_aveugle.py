"""Extrait le corpus en aveugle : texte seul, sous identifiant opaque.

Sert à faire tourner le moteur LLM (§ « Protocole » du README) sans que
l'opérateur ni le modèle ne voient la vérité terrain.

Deux précautions, sans lesquelles l'aveugle n'en est pas un :

- **Identifiants opaques.** Les identifiants du corpus portent un préfixe
  sémantique (`acc-`, `eff-`, `hop-`, `mul-`, `edg-`) qui révèle l'étiquette
  ou la catégorie. Ils sont remplacés par `t-001`…`t-120`.
- **Ordre permuté.** Le corpus est groupé par droit ; lu dans l'ordre, le
  voisinage suffit à deviner. La permutation est déterministe (tri par SHA-256
  du texte) donc reproductible d'une exécution à l'autre.

La table de correspondance est écrite à part, pour la phase d'évaluation.
"""

import hashlib
import json
import pathlib
import sys

RACINE = pathlib.Path(__file__).resolve().parents[2]
CORPUS = RACINE / "corpus" / "demandes-rgpd.fr.jsonl"
DOSSIER = pathlib.Path(__file__).parent
SORTIE = DOSSIER / "textes-aveugles.jsonl"
CORRESPONDANCE = DOSSIER / "correspondance-aveugle.json"


def main() -> int:
    exemples = []
    with CORPUS.open(encoding="utf-8") as f:
        for ligne in f:
            ligne = ligne.strip()
            if ligne:
                exemples.append(json.loads(ligne))

    exemples.sort(key=lambda e: hashlib.sha256(e["texte"].encode("utf-8")).hexdigest())

    lignes, correspondance = [], {}
    for i, ex in enumerate(exemples, start=1):
        opaque = f"t-{i:03d}"
        correspondance[opaque] = ex["id"]
        lignes.append(json.dumps({"id": opaque, "texte": ex["texte"]}, ensure_ascii=False))

    SORTIE.write_text("\n".join(lignes) + "\n", encoding="utf-8")
    CORRESPONDANCE.write_text(
        json.dumps(correspondance, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(f"{len(lignes)} textes écrits dans {SORTIE.name}, table dans {CORRESPONDANCE.name}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
