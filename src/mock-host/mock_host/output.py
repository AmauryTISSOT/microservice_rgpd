"""Ce que le mock dit, et où il le dit.

Sur la sortie standard, et non par `logging` : uvicorn écrit ses journaux sur l'erreur standard, et
c'est la sortie standard que le dashboard Aspire présente comme telle. Les routes HTTP et le
consommateur passent par ici, pour que les deux canaux d'exercice se lisent au même endroit.
"""

from __future__ import annotations


def say(line: str) -> None:
    """Une ligne sur la sortie standard, vidée aussitôt."""
    print(line, flush=True)
