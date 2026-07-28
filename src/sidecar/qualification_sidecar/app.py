"""L'application HTTP du sidecar : un point d'entrée par moteur, jamais un point d'entrée commun.

Deux points d'entrée séparés rendent l'indépendance des avis **structurelle** — un point d'entrée
unique pourrait un jour faire dépendre un avis de l'autre sans que .NET le sache. Ils gardent aussi
les modes de panne séparés, et autorisent les palettes de codes à diverger : le lexique n'ayant
aucun amont, la sienne est courte.

| Code | Cas |
| --- | --- |
| `200` | un avis valide |
| `400` | requête malformée — texte absent ou vide après nettoyage |
| `500` | le moteur a produit autre chose qu'un avis : c'est une panne, pas un avis faible |

La corrélation avec l'appelant passe par l'en-tête `traceparent` que `ServiceDefaults` propage
déjà. **Le sidecar n'invente aucun identifiant** : un identifiant maison créerait un second
vocabulaire de corrélation, que personne ne rapprocherait du premier.
"""

from __future__ import annotations

import logging
from collections.abc import Awaitable, Callable
from typing import Annotated

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import AfterValidator, BaseModel, ConfigDict, Field
from starlette.exceptions import HTTPException as StarletteHTTPException

from qualification_sidecar import lexicon
from qualification_sidecar.opinion import Engine, EngineFailure, Opinion
from qualification_sidecar.taxonomy import WireTaxonomyDivergence, to_canonical

PROBLEM_JSON = "application/problem+json"

#: La version du **contrat HTTP** du sidecar, jamais celle d'un moteur. Les deux bougent pour des
#: raisons sans rapport : le LLM versionnera le modèle qu'il sert, le lexique ses règles, et faire
#: porter à l'API la version de l'un des deux ferait mentir l'autre.
API_VERSION = "1.0.0"

logger = logging.getLogger("qualification_sidecar")


def _not_blank(text: str) -> str:
    """Refuse un texte vide une fois ses bordures nettoyées — court n'est pas malformé, vide l'est."""
    trimmed = text.strip()

    if not trimmed:
        raise ValueError("Le texte à qualifier est vide une fois ses bordures nettoyées.")

    return trimmed


class OpinionRequest(BaseModel):
    """Le texte, et rien d'autre.

    Pas d'identifiant — la corrélation passe par `traceparent` —, pas de langue, pas de contexte.
    Tout champ supplémentaire est refusé : le contrat interne se resserre plutôt qu'il ne tolère,
    les deux extrémités étant à nous.
    """

    model_config = ConfigDict(extra="forbid")

    text: Annotated[str, AfterValidator(_not_blank)] = Field(
        description="Le texte libre reçu de l'application tierce, présumé exercer un droit."
    )


app = FastAPI(
    title="Sidecar de qualification RGPD",
    version=API_VERSION,
    description=__doc__,
)


@app.middleware("http")
async def correlate_with_the_caller(
    request: Request,
    call_next: Callable[[Request], Awaitable[JSONResponse]],
) -> JSONResponse:
    """Attache la trace de l'appelant à ce que le sidecar journalise, sans jamais en fabriquer une.

    Son absence est une information : elle dit que la propagation est cassée en amont, ce qu'un
    identifiant de repli masquerait.
    """
    response = await call_next(request)

    logger.info(
        "%s %s -> %s",
        request.method,
        request.url.path,
        response.status_code,
        extra={"traceparent": request.headers.get("traceparent")},
    )

    return response


@app.post(
    "/opinions/lexicon",
    response_model=Opinion,
    summary="Rend l'avis du moteur lexical sur un texte.",
)
def lexicon_opinion(request: OpinionRequest) -> Opinion:
    """Qualifie le texte par le lexique déterministe, en noms canoniques anglais.

    Le moteur raisonne en slugs français ; la traduction a lieu ici, juste avant de répondre. Un
    slug qu'il ne sait pas traduire n'est pas un avis approximatif : c'est une panne.
    """
    slugs = lexicon.qualify(request.text)

    try:
        rights = tuple(to_canonical(slug) for slug in slugs)
    except WireTaxonomyDivergence as divergence:
        raise EngineFailure(str(divergence)) from divergence

    return Opinion(
        rights=rights,
        engine=Engine(name=lexicon.ENGINE_NAME, version=lexicon.ENGINE_VERSION),
    )


@app.get("/health", summary="Dit si le sidecar est en état de rendre un avis.")
def health() -> dict[str, str]:
    """Le sidecar ayant vérifié sa taxonomie à l'import, être démarré suffit à être en état."""
    return {"status": "healthy"}


def _problem(status: int, title: str, detail: str) -> JSONResponse:
    return JSONResponse(
        status_code=status,
        media_type=PROBLEM_JSON,
        content={"type": "about:blank", "title": title, "status": status, "detail": detail},
    )


@app.exception_handler(RequestValidationError)
async def malformed_request(request: Request, exception: RequestValidationError) -> JSONResponse:
    """Une requête que le sidecar ne peut pas lire est un refus d'entrée, jamais un avis vide."""
    return _problem(
        status=400,
        title="Requête de qualification malformée",
        detail="; ".join(error["msg"] for error in exception.errors()),
    )


@app.exception_handler(StarletteHTTPException)
async def http_error(request: Request, exception: StarletteHTTPException) -> JSONResponse:
    """Tout ce que le routage refuse sort dans la même forme que le reste — chemin ou verbe inconnu compris.

    Sans cela, l'appelant aurait deux formes d'erreur à lire selon l'endroit où il s'est trompé.
    """
    return _problem(
        status=exception.status_code,
        title="Requête refusée par le sidecar de qualification",
        detail=str(exception.detail),
    )


@app.exception_handler(EngineFailure)
async def engine_failure(request: Request, exception: EngineFailure) -> JSONResponse:
    """Le moteur a produit autre chose qu'un avis : le sidecar le déclare en panne, sans rien rendre."""
    logger.error(
        "Le moteur lexical est en panne : %s",
        exception,
        extra={"traceparent": request.headers.get("traceparent")},
    )

    return _problem(
        status=500,
        title="Le moteur n'a pas rendu d'avis valide",
        detail=str(exception),
    )
