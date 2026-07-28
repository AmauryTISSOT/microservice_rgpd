"""Le corpus témoin du dépôt et le fichier témoin du lexique, lus par les tests de non-régression.

Le corpus est un actif du dépôt, versionné à sa racine : les tests le lisent, ils ne le copient
jamais — une copie divergerait en silence, exactement ce que la projection de la taxonomie évite
par ailleurs.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import NamedTuple

from qualification_sidecar import lexicon
from qualification_sidecar.taxonomy import repository_root, to_canonical

CORPUS_FILE = repository_root() / "corpus" / "demandes-rgpd.fr.jsonl"

WITNESS_FILE = Path(__file__).resolve().parent / "witness" / "lexicon-corpus.jsonl"


class Example(NamedTuple):
    """Un exemple du corpus, réduit à ce dont la non-régression a besoin."""

    id: str
    text: str


def read_corpus() -> list[Example]:
    return [
        Example(id=entry["id"], text=entry["texte"])
        for entry in _read_jsonl(CORPUS_FILE)
    ]


def read_witness() -> dict[str, list[str]]:
    """Ce que le lexique rendait sur chaque exemple au dernier commit qui l'a assumé."""
    return {entry["id"]: entry["rights"] for entry in _read_jsonl(WITNESS_FILE)}


def qualify(text: str) -> list[str]:
    """Le verdict du lexique tel qu'il quitterait le sidecar : en noms canoniques, trié pour comparer."""
    return sorted(to_canonical(slug) for slug in lexicon.qualify(text))


def _read_jsonl(path: Path) -> list[dict]:
    return [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]
