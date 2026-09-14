"""Le mock du système hôte : une route par droit configurable dans le Paramétrage.

Il n'existe que pour qu'une adresse collée dans `/parametrage` soit **joignable**. Le service ne
l'appelle pas (ADR-0016) ; le mock ne connaît donc aucun contrat, ne valide rien et ne rend aucune
donnée factice. Il simule seulement un temps de traitement, puis réussit.

| Droit (article) | Route | Code |
| --- | --- | --- |
| Accès (15) | `GET /rights/access` | `200` |
| Portabilité (20) | `GET /rights/portability` | `200` |
| Rectification (16) | `PATCH /rights/rectification` | `204` |
| Effacement (17) | `DELETE /rights/erasure` | `204` |
| Limitation (18) | `POST /rights/restriction` | `202` |
| Opposition (21) | `POST /rights/objection` | `202` |

Les segments sont les noms anglais de `DataSubjectRight` ; `OutOfScope` n'a pas de route. Une
méthode non prévue sur l'une de ces routes rend `405`, ce que le routage fait de lui-même.
"""

from __future__ import annotations

import asyncio
import os

from fastapi import FastAPI, Request, Response
from fastapi.responses import JSONResponse

#: Le délai simulé quand `MOCK_DELAY_MS` est absente : assez long pour qu'une attente se voie à
#: l'écran, assez court pour ne pas lasser une démonstration.
DEFAULT_DELAY_MS = 2_000

#: Chaque route de droit : son chemin, sa méthode, son code de succès.
RIGHT_ROUTES: tuple[tuple[str, str, int], ...] = (
    ("/rights/access", "GET", 200),
    ("/rights/portability", "GET", 200),
    ("/rights/rectification", "PATCH", 204),
    ("/rights/erasure", "DELETE", 204),
    ("/rights/restriction", "POST", 202),
    ("/rights/objection", "POST", 202),
)


def delay_ms_from_environment() -> int:
    """Lit `MOCK_DELAY_MS`. Une valeur illisible arrête le démarrage plutôt que de passer pour zéro."""
    raw = os.environ.get("MOCK_DELAY_MS", "").strip()

    if not raw:
        return DEFAULT_DELAY_MS

    try:
        delay_ms = int(raw)
    except ValueError:
        raise ValueError(f"MOCK_DELAY_MS vaut « {raw} », qui n'est pas un nombre entier de millisecondes.") from None

    if delay_ms < 0:
        raise ValueError(f"MOCK_DELAY_MS vaut « {raw} » : un délai ne peut pas être négatif.")

    return delay_ms


def create_app(delay_ms: int | None = None) -> FastAPI:
    """Construit le mock. Sans délai explicite, il est lu dans l'environnement."""
    delay_seconds = (delay_ms_from_environment() if delay_ms is None else delay_ms) / 1_000
    app = FastAPI(title="mock-host", openapi_url=None, docs_url=None, redoc_url=None)

    @app.get("/health")
    async def health() -> dict[str, str]:
        return {"status": "ok"}

    for path, method, status_code in RIGHT_ROUTES:
        app.add_api_route(
            path,
            _right_endpoint(status_code, delay_seconds),
            methods=[method],
            status_code=status_code,
            include_in_schema=False,
        )

    return app


def _right_endpoint(status_code: int, delay_seconds: float):
    async def endpoint(request: Request) -> Response:
        body = await request.body()
        # Sur la sortie standard, et non par `logging` : uvicorn écrit ses journaux sur l'erreur
        # standard, et c'est la sortie standard que le dashboard Aspire présente comme telle.
        print(
            f"{request.method} {request.url.path} {body.decode('utf-8', errors='replace')}",
            flush=True,
        )

        if delay_seconds > 0:
            await asyncio.sleep(delay_seconds)

        if status_code == 204:
            return Response(status_code=204)

        return JSONResponse({"status": "ok"}, status_code=status_code)

    return endpoint


app = create_app()
