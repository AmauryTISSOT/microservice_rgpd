"""Fige l'avis de `qwen3:8b` sur chacun des 120 exemples du corpus, par le moteur de production.

À lancer depuis la racine du dépôt, le serveur de modèles étant démarré (voir le README voisin) :

    uv run --project src/sidecar python exploration/regenerate.py

**Le chemin est celui de la production, pas une maquette.** Le script parle à l'application ASGI du
sidecar par son point d'entrée `/opinions/llm` : consigne, sortie structurée, traduction vers les
noms de fil, distinction des trois pannes et codes HTTP compris. Reconstituer ce chemin ici en
aurait fait une seconde version de la qualification, qui aurait dérivé de la première au premier
changement — et l'artefact aurait alors décrit un moteur que personne ne sert.

**Une seule boucle d'événements pour les 120 appels.** Le client du SDK ouvre son bassin de
connexions sur la boucle qui l'a vu naître ; un client de test qui ouvre une boucle par requête le
rend injoignable dès le deuxième appel. D'où le transport ASGI tenu ouvert du début à la fin.

**Aucune parallélisation.** 8 Go de VRAM sérialisent déjà les appels : les lancer de front ne
gagnerait rien et ferait dépasser l'échéance à ceux qui attendent leur tour.

**Reprise sur interruption.** Chaque ligne est écrite et vidée dès qu'elle est connue, et une
relance repart des exemples manquants. Une exécution dure une vingtaine de minutes ; la perdre
entière pour une coupure serait une raison de ne jamais la relancer.
"""

from __future__ import annotations

import asyncio
import json
import sys
import time
from collections import Counter
from pathlib import Path

import httpx

REPOSITORY_ROOT = Path(__file__).resolve().parent.parent

sys.path.insert(0, str(REPOSITORY_ROOT / "src" / "sidecar"))

from qualification_sidecar.app import app  # noqa: E402
from tests.corpus import read_corpus  # noqa: E402

#: L'artefact. Il vit ici et non dans `src/sidecar/tests/witness/` : le témoin du lexique est un
#: garde-fou de non-régression que la suite de tests relit à chaque exécution, celui-ci est de la
#: donnée d'exploration qu'aucun test ne lit et dont un écart n'est pas une régression.
OPINIONS_FILE = Path(__file__).resolve().parent / "qwen3-8b-corpus.jsonl"

#: Le point d'entrée du moteur LLM, tel que l'appelant .NET l'appelle.
LLM_ENDPOINT = "/opinions/llm"


def _already_frozen() -> dict[str, str]:
    """Les lignes déjà écrites, indexées par identifiant — la mémoire d'une exécution interrompue."""
    if not OPINIONS_FILE.exists():
        return {}

    return {
        json.loads(line)["id"]: line
        for line in OPINIONS_FILE.read_text(encoding="utf-8").splitlines()
        if line.strip()
    }


async def _ask(client: httpx.AsyncClient, text: str) -> tuple[dict, float]:
    """Demande son avis au moteur et rend ce qu'il a répondu, avec le temps qu'il y a mis.

    Une panne n'est pas une exception ici : c'est un fait à figer. Le sidecar traite un avis
    invalide comme une panne du moteur, et l'exploration doit pouvoir compter ces pannes plutôt que
    de s'arrêter à la première.
    """
    start = time.perf_counter()
    response = await client.post(LLM_ENDPOINT, json={"text": text})
    elapsed = time.perf_counter() - start

    return {"status": response.status_code, "body": response.json()}, elapsed


def _line(example_id: str, answer: dict, elapsed: float) -> str:
    """Compose la ligne figée : l'avis rendu, ou la panne subie, jamais un mélange des deux."""
    frozen: dict[str, object] = {"id": example_id, "status": answer["status"]}

    if answer["status"] == 200:
        frozen |= answer["body"]
    else:
        frozen["failure"] = answer["body"]

    frozen["duration_seconds"] = round(elapsed, 3)

    return json.dumps(frozen, ensure_ascii=False)


async def main() -> None:
    examples = read_corpus()
    frozen = _already_frozen()

    if frozen:
        print(f"{len(frozen)} exemples déjà figés, repris tels quels.")

    OPINIONS_FILE.parent.mkdir(parents=True, exist_ok=True)

    started = time.perf_counter()

    # `timeout=None` sur le transport ASGI : l'échéance qui compte est celle du sidecar vers son
    # amont, et une seconde échéance par-dessus la masquerait au lieu de la nommer.
    async with httpx.AsyncClient(
        transport=httpx.ASGITransport(app=app),
        base_url="http://qualification-sidecar",
        timeout=None,
    ) as client:
        for rank, example in enumerate(examples, start=1):
            if example.id in frozen:
                continue

            answer, elapsed = await _ask(client, example.text)
            frozen[example.id] = _line(example.id, answer, elapsed)

            # Réécriture complète à chaque exemple, dans l'ordre du corpus : c'est ce qui garantit
            # qu'une reprise ne laisse pas le fichier dans l'ordre où les exemples ont été repassés.
            _write(examples, frozen)

            print(
                f"[{rank:>3}/{len(examples)}] {example.id} -> {answer['status']} "
                f"en {elapsed:.1f} s",
                flush=True,
            )

    _report(examples, frozen, time.perf_counter() - started)


def _write(examples: list, frozen: dict[str, str]) -> None:
    ordered = [frozen[example.id] for example in examples if example.id in frozen]
    OPINIONS_FILE.write_text("\n".join(ordered) + "\n", encoding="utf-8", newline="\n")


def _report(examples: list, frozen: dict[str, str], elapsed: float) -> None:
    """Rend les faits dont la suite dépend : durée, pannes, répartition des degrés de confiance."""
    entries = [json.loads(frozen[example.id]) for example in examples if example.id in frozen]

    failures = [entry for entry in entries if entry["status"] != 200]
    confidences = Counter(entry["confidence"] for entry in entries if entry["status"] == 200)

    print(f"\n{len(entries)} exemples figés dans {OPINIONS_FILE}")
    print(f"Durée de cette exécution : {elapsed / 60:.1f} min")
    print(f"Pannes du moteur : {len(failures)}")

    for failure in failures:
        print(f"  {failure['id']} -> {failure['status']} {failure['failure']['title']}")

    print("Répartition des confiances déclarées :")
    for degree in ("High", "Medium", "Low"):
        print(f"  {degree} : {confidences.get(degree, 0)}")


if __name__ == "__main__":
    asyncio.run(main())
