"""L'application HTTP du sidecar : un point d'entrée par moteur, jamais un point d'entrée commun.

Deux points d'entrée séparés rendent l'indépendance des avis **structurelle** — un point d'entrée
unique pourrait un jour faire dépendre un avis de l'autre sans que .NET le sache. Ils gardent aussi
les modes de panne séparés, et autorisent les palettes de codes à diverger : le lexique n'ayant
aucun amont, la sienne est courte, là où le LLM en a un et doit dire lequel de ses deux échecs il
subit.

| Code | Lexique | LLM |
| --- | --- | --- |
| `200` | un avis valide | un avis valide |
| `400` | requête malformée — texte absent ou vide après nettoyage | idem |
| `500` | le moteur a produit autre chose qu'un avis : une panne, pas un avis faible | — |
| `502` | — | l'amont a répondu, mais sa réponse n'est pas un avis |
| `503` | — | l'amont est injoignable, ou son modèle n'est pas chargé |
| `504` | — | l'échéance vers l'amont est passée : **la lenteur arrive nommée** |
| `499` | — | l'appelant est parti : la génération a été interrompue, personne ne lit ce corps |
| `501` | — | ce déploiement ne sert aucun modèle : le moteur y est éteint, et rien n'est en panne |

Le `500` du lexique et le `502` du LLM disent la même chose du domaine — un moteur en panne — et
diffèrent par le seul fait qui compte pour l'exploitant : **qui est à réparer**. Un bogue Python
d'un côté, un modèle qui déraille de l'autre.

La corrélation avec l'appelant passe par l'en-tête `traceparent` que `ServiceDefaults` propage
déjà. **Le sidecar n'invente aucun identifiant** : un identifiant maison créerait un second
vocabulaire de corrélation, que personne ne rapprocherait du premier.
"""

from __future__ import annotations

import logging
import os
from collections.abc import AsyncIterator, Awaitable, Callable
from contextlib import asynccontextmanager
from typing import Annotated

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import AfterValidator, BaseModel, ConfigDict, Field
from starlette.exceptions import HTTPException as StarletteHTTPException

from qualification_sidecar import departure, lexicon, llm
from qualification_sidecar.departure import CallerGone
from qualification_sidecar.opinion import Engine, EngineFailure, Opinion, ReasonedOpinion
from qualification_sidecar.taxonomy import WireTaxonomyDivergence, to_canonical

PROBLEM_JSON = "application/problem+json"

#: La version du **contrat HTTP** du sidecar, jamais celle d'un moteur. Les deux bougent pour des
#: raisons sans rapport : le LLM versionnera le modèle qu'il sert, le lexique ses règles, et faire
#: porter à l'API la version de l'un des deux ferait mentir l'autre.
API_VERSION = "1.0.0"

logger = logging.getLogger("qualification_sidecar")

#: L'amont réel, **construit à la demande et non à l'import**. Remplaçable en test — c'est le seul
#: objet à écarter pour que la suite entière tourne sans GPU.
#:
#: La paresse n'est pas une optimisation : c'est elle qui permet d'importer ce module dans un
#: déploiement éteint, où aucune des sept variables du moteur n'est posée. Le chargement à l'import
#: y aurait fait échouer l'import lui-même, donc le démarrage du lexique avec.
LLM_MODEL: llm.StructuredModel | None = None


def llm_model() -> llm.StructuredModel:
    """Rend l'amont du moteur LLM, en le construisant à sa première demande — jamais avant.

    Ne l'appeler que **le moteur allumé** : sur un déploiement éteint, il n'y a pas de configuration
    à lire, et la lire refuserait un démarrage que ce déploiement a le droit d'obtenir.
    """
    global LLM_MODEL

    if LLM_MODEL is None:
        LLM_MODEL = llm.OpenAiCompatibleModel(llm.LlmSettings.from_environment(os.environ))

    return LLM_MODEL


@asynccontextmanager
async def refuse_to_start_a_misconfigured_engine(_: FastAPI) -> AsyncIterator[None]:
    """Allumé, le sidecar lit sa configuration LLM **au démarrage** et refuse de servir sans elle.

    La lecture a quitté l'import, pas le démarrage : une configuration incomplète reste une panne
    bruyante au démarrage plutôt qu'une panne de qualification au premier appel. Éteint, il n'y a
    rien à lire, et rien à refuser — pas même une échéance résiduelle laissée là pour rallumer plus
    tard.
    """
    if llm.engine_is_enabled(os.environ):
        llm_model()

    yield


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
    lifespan=refuse_to_start_a_misconfigured_engine,
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


@app.post(
    "/opinions/llm",
    response_model=ReasonedOpinion,
    summary="Rend l'avis du moteur LLM sur un texte, avec sa confiance et sa justification.",
)
async def llm_opinion(opinion: OpinionRequest, request: Request) -> ReasonedOpinion:
    """Qualifie le texte par le LLM local, en noms canoniques anglais et confiance à trois degrés.

    `async` sans négociation : un appel au modèle dure des secondes, et le tenir sur un fil de
    travail affamerait le lexique, dont le double rôle exige qu'il réponde même quand le LLM peine.

    **La génération s'arrête au départ de l'appelant.** Le serveur ASGI signale ce départ, mais
    n'interrompt rien de lui-même : sans cette course, la génération irait à son terme sur un GPU
    qui sérialise, et bloquerait la file de l'appelant resté. Le lexique n'en a pas besoin — il
    répond en une fraction de milliseconde, et l'interrompre coûterait plus que de le laisser finir.

    Le modèle raisonne et justifie en français ; la traduction a lieu ici, juste avant de répondre,
    comme pour le lexique. La justification, elle, **reste en français** : c'est le seul texte du
    sidecar qu'un humain lira.

    **Le moteur peut ne pas exister.** Éteint, la route reste servie et rend un refus nommé plutôt
    que de disparaître : retirée, elle laisserait un exploitant devant un `404` à chercher une faute
    de frappe dans son URL.
    """
    if not llm.engine_is_enabled(os.environ):
        raise llm.NoModelServed(
            "Ce déploiement du sidecar ne sert aucun modèle : le moteur LLM y est éteint. Seul le "
            "point d'entrée lexical rend un avis. Pour l'allumer, poser "
            f"{llm.ENABLED_SETTING}=true et les réglages du moteur."
        )

    verdict = await departure.serve_until_the_caller_leaves(
        request, llm.qualify(opinion.text, llm_model()))

    # Sur ce chemin, toute panne de moteur est imputable à l'amont : la requalifier ici évite que
    # deux causes identiques — un slug hors taxonomie, une exclusivité violée — sortent tantôt en
    # `500`, tantôt en `502`, selon l'étape qui les a repérées.
    try:
        rights = tuple(to_canonical(slug) for slug in verdict.slugs)

        return ReasonedOpinion(
            rights=rights,
            confidence=llm.to_canonical_confidence(verdict.confidence_slug),
            justification=verdict.justification,
            engine=Engine(
                name=llm.ENGINE_NAME,
                version=llm.engine_version(verdict.served_model),
            ),
        )
    except llm.UnusableCompletion:
        raise
    except (EngineFailure, WireTaxonomyDivergence) as unusable:
        raise llm.UnusableCompletion(str(unusable)) from unusable


@app.get("/health", summary="Dit si le sidecar est en état de rendre un avis.")
def health() -> dict[str, str]:
    """Le sidecar ayant vérifié sa taxonomie et sa configuration au démarrage, être démarré suffit.

    Rien n'est demandé à l'amont du LLM : une sonde qui l'interrogerait ferait déclarer le sidecar
    malade alors que son moteur lexical, lui, répond parfaitement. **Un moteur LLM éteint ne rend
    pas ce sidecar malade non plus** : il sert son lexique, qui est ce que cette sonde surveille.
    """
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
    """Le moteur a produit autre chose qu'un avis : le sidecar le déclare en panne, sans rien rendre.

    Vaut pour les deux moteurs : le chemin appelé dit déjà lequel a parlé, et distinguer les codes
    ferait passer pour une différence de nature ce qui n'est qu'une différence d'expéditeur.
    """
    logger.error(
        "Un moteur est en panne sur %s : %s",
        request.url.path,
        exception,
        extra={"traceparent": request.headers.get("traceparent")},
    )

    return _problem(
        status=500,
        title="Le moteur n'a pas rendu d'avis valide",
        detail=str(exception),
    )


@app.exception_handler(llm.UnusableCompletion)
async def unusable_completion(request: Request, exception: llm.UnusableCompletion) -> JSONResponse:
    """L'amont a répondu, mais pas un avis. Panne du moteur, comme le `500` du lexique — autre fautif.

    Ce gestionnaire est plus spécifique que celui d'`EngineFailure`, dont `UnusableCompletion`
    dérive ; Starlette remonte la hiérarchie de l'exception et retient donc celui-ci.
    """
    logger.error(
        "Le modèle a rendu autre chose qu'un avis : %s",
        exception,
        extra={"traceparent": request.headers.get("traceparent")},
    )

    return _problem(
        status=502,
        title="Le serveur de modèles n'a pas rendu d'avis valide",
        detail=str(exception),
    )


@app.exception_handler(llm.NoModelServed)
async def no_model_served(request: Request, exception: llm.NoModelServed) -> JSONResponse:
    """Le moteur est éteint : le sidecar le dit en une phrase, et **ne journalise aucune erreur**.

    `501` est le seul code de la palette à ne pas nommer une panne : il dit que ce déploiement
    n'implémente pas ce service, ce qui est exactement le cas. Le journaliser en erreur ferait
    sonner les alertes d'un exploitant pour un choix qu'il a lui-même écrit.
    """
    return _problem(
        status=501,
        title="Ce déploiement ne sert aucun modèle",
        detail=str(exception),
    )


@app.exception_handler(llm.ModelUnreachable)
async def model_unreachable(request: Request, exception: llm.ModelUnreachable) -> JSONResponse:
    """L'amont est injoignable ou son modèle n'est pas chargé — ce n'est pas une panne du sidecar."""
    logger.error(
        "L'amont du moteur LLM est injoignable : %s",
        exception,
        extra={"traceparent": request.headers.get("traceparent")},
    )

    return _problem(
        status=503,
        title="Le serveur de modèles est injoignable",
        detail=str(exception),
    )


@app.exception_handler(CallerGone)
async def caller_gone(request: Request, exception: CallerGone) -> JSONResponse:
    """L'appelant est parti : la génération a été interrompue, et **rien n'est rendu à personne**.

    Ce corps ne sera lu par personne — le canal est fermé —, et c'est justement pourquoi il existe :
    sans lui, le départ ressortirait en exception non gérée, donc en `500` dans les journaux, et un
    exploitant chercherait une panne là où il n'y a qu'un appelant qui a raccroché. `499` est le code
    par lequel les serveurs frontaux nomment déjà cette situation ; aucune RFC ne le définit, et il
    n'engage rien puisque personne ne le reçoit.
    """
    logger.info(
        "L'appelant est parti avant la fin de la génération : %s",
        exception,
        extra={"traceparent": request.headers.get("traceparent")},
    )

    return _problem(
        status=499,
        title="L'appelant est parti avant la fin de la qualification",
        detail=str(exception),
    )


@app.exception_handler(llm.ModelTooSlow)
async def model_too_slow(request: Request, exception: llm.ModelTooSlow) -> JSONResponse:
    """L'échéance vers l'amont est passée. **Un code à elle seule** : la lenteur arrive nommée.

    Aucune reprise n'est tentée avant d'en arriver là : la requête étant déterministe, la rejouer
    rendrait le même avis en dépassant l'échéance de l'appelant .NET par-dessus le marché.
    """
    logger.error(
        "Le moteur LLM a dépassé son échéance : %s",
        exception,
        extra={"traceparent": request.headers.get("traceparent")},
    )

    return _problem(
        status=504,
        title="Le serveur de modèles n'a pas répondu dans l'échéance",
        detail=str(exception),
    )
