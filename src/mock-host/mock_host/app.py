"""Le mock du système hôte : une route par droit configurable dans le Paramétrage.

Il est la cible d'une adresse collée dans `/parametrage`, que le service appelle quand l'`Operator`
exécute une demande (ADR-0026) : un `POST` JSON par droit. Le mock ne valide ni le corps ni les
en-têtes et ne rend aucune donnée factice. Il simule un temps de traitement, puis répond.

| Droit (article) | Route | Code |
| --- | --- | --- |
| Accès (15) | `POST /rights/access` | `200` |
| Portabilité (20) | `POST /rights/portability` | `200` |
| Rectification (16) | `POST /rights/rectification` | `204` |
| Effacement (17) | `POST /rights/erasure` | `204` |
| Limitation (18) | `POST /rights/restriction` | `202` |
| Opposition (21) | `POST /rights/objection` | `202` |

Les segments sont les noms anglais de `DataSubjectRight` ; `OutOfScope` n'a pas de route. Toute
autre méthode rend `405`, ce que le routage fait de lui-même.

Deux paramètres de l'adresse simulent un échec ou une attente, droit par droit, pour le seul appel
qui les porte : `status` force le code de réponse, `delay_ms` remplace `MOCK_DELAY_MS`. Une valeur
illisible rend `400` sur-le-champ.
"""

from __future__ import annotations

import asyncio
import os
import re

from fastapi import FastAPI, Request, Response
from fastapi.responses import JSONResponse

#: Le délai simulé quand `MOCK_DELAY_MS` est absente : assez long pour qu'une attente se voie à
#: l'écran, assez court pour ne pas lasser une démonstration.
DEFAULT_DELAY_MS = 2_000

#: Chaque route de droit : son chemin et son code de succès. Toutes répondent à `POST` (ADR-0026).
RIGHT_ROUTES: tuple[tuple[str, int], ...] = (
    ("/rights/access", 200),
    ("/rights/portability", 200),
    ("/rights/rectification", 204),
    ("/rights/erasure", 204),
    ("/rights/restriction", 202),
    ("/rights/objection", 202),
)

#: Les codes qu'accepte `status`. Un code `1xx` n'est pas une réponse finale, uvicorn ne l'enverrait pas.
FORCEABLE_STATUS_CODES = range(200, 600)

#: Les codes dont la réponse n'a pas de corps.
BODILESS_STATUS_CODES = frozenset({204, 304})


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

    for path, status_code in RIGHT_ROUTES:
        app.add_api_route(
            path,
            _right_endpoint(status_code, delay_seconds),
            methods=["POST"],
            status_code=status_code,
            include_in_schema=False,
        )

    return app


def _right_endpoint(success_status_code: int, default_delay_seconds: float):
    async def endpoint(request: Request) -> Response:
        try:
            status_code = _query_int(request, "status", success_status_code)
            if status_code not in FORCEABLE_STATUS_CODES:
                raise ValueError(f"status vaut « {status_code} » : un code de réponse va de 200 à 599.")

            delay_ms = _query_int(request, "delay_ms", None)
            if delay_ms is not None and delay_ms < 0:
                raise ValueError(f"delay_ms vaut « {delay_ms} » : un délai ne peut pas être négatif.")
        except ValueError as error:
            return JSONResponse({"detail": str(error)}, status_code=400)

        delay_seconds = default_delay_seconds if delay_ms is None else delay_ms / 1_000
        body = await request.body()
        # Sur la sortie standard, et non par `logging` : uvicorn écrit ses journaux sur l'erreur
        # standard, et c'est la sortie standard que le dashboard Aspire présente comme telle.
        print(
            f"{request.method} {request.url.path} {body.decode('utf-8', errors='replace')}",
            flush=True,
        )

        if delay_seconds > 0:
            await asyncio.sleep(delay_seconds)

        if status_code in BODILESS_STATUS_CODES:
            return Response(status_code=status_code)

        return JSONResponse({"status": "ok" if status_code < 300 else "error"}, status_code=status_code)

    return endpoint


def _query_int(request: Request, name: str, default: int | None) -> int | None:
    """Lit un paramètre entier de l'adresse. Absent, il vaut `default` ; illisible, il lève `ValueError`."""
    if name not in request.query_params:
        return default

    raw = request.query_params[name]
    # `int()` accepterait « +5 », « 5_03 », une espace ou des chiffres non latins : ils sont illisibles ici.
    if not re.fullmatch(r"-?[0-9]+", raw):
        raise ValueError(f"{name} vaut « {raw} », qui n'est pas un nombre entier.")

    return int(raw)


app = create_app()
