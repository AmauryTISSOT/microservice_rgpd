"""Régénère le fichier témoin du lexique à partir du corpus.

À lancer **seulement** quand un écart de comportement est délibéré, depuis `src/sidecar` :

    uv run python tests/witness/regenerate.py

Le diff qui en sort se relit exemple par exemple — c'est ce que le format JSONL achète, et c'est
la seule chose qui rende l'écart discutable en revue. Incrémenter `lexicon.ENGINE_VERSION` dans le
même commit : la version des règles et le comportement figé ne se séparent jamais.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))

from tests.corpus import WITNESS_FILE, qualify, read_corpus  # noqa: E402


def main() -> None:
    lines = [
        json.dumps({"id": example.id, "rights": qualify(example.text)}, ensure_ascii=False)
        for example in read_corpus()
    ]

    WITNESS_FILE.parent.mkdir(parents=True, exist_ok=True)
    WITNESS_FILE.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")

    print(f"{len(lines)} exemples figés dans {WITNESS_FILE}")


if __name__ == "__main__":
    main()
