"""Le point d'entrée d'avis LLM, vu de l'appelant .NET.

Palette plus riche que celle du lexique, et c'est encore ce qu'achètent deux points d'entrée
séparés : ce moteur a un amont, donc trois échecs de plus — et **trois codes distincts** pour eux,
parce qu'une réponse inexploitable, un serveur éteint et un serveur lent ne se réparent pas au même
endroit.

**Aucun test n'appelle Ollama** : l'amont est toujours un faux.
"""

import asyncio
import json
import logging

import httpx2
import pytest
from fastapi.testclient import TestClient

from qualification_sidecar import app as app_module
from qualification_sidecar import llm
from qualification_sidecar.app import app
from tests.upstream import VALID_VERDICT

PROBLEM_JSON = "application/problem+json"


@pytest.fixture
def client():
    return TestClient(app)


# --------------------------------------------------------------------------
# L'avis rendu
# --------------------------------------------------------------------------


def test_a_text_is_qualified_in_canonical_english_names(client, upstream):
    upstream(content={**VALID_VERDICT, "droits": ["acces", "effacement"]})

    response = client.post("/opinions/llm", json={"text": "Communiquez et supprimez mes données."})

    assert response.status_code == 200
    assert response.json()["rights"] == ["Access", "Erasure"]


def test_the_answer_carries_a_declared_confidence_in_canonical_names(client, upstream):
    upstream(content=VALID_VERDICT)

    body = client.post("/opinions/llm", json={"text": "Mes données ?"}).json()

    assert body["confidence"] == "Medium"


def test_the_answer_carries_a_justification_left_in_french(client, upstream):
    """Le seul texte du sidecar destiné à un humain — et l'opérateur qui relit lit le français."""
    upstream(content=VALID_VERDICT)

    body = client.post("/opinions/llm", json={"text": "Mes données ?"}).json()

    assert body["justification"] == VALID_VERDICT["justification"]


def test_the_answer_says_which_engine_spoke_and_in_which_version(client, upstream):
    upstream(content=VALID_VERDICT, served_model="qwen-de-test:8b")

    engine = client.post("/opinions/llm", json={"text": "Mes données ?"}).json()["engine"]

    assert engine["name"] == llm.ENGINE_NAME
    assert "qwen-de-test:8b" in engine["version"]


def test_the_answer_carries_nothing_else(client, upstream):
    upstream(content=VALID_VERDICT)

    body = client.post("/opinions/llm", json={"text": "Mes données ?"}).json()

    assert set(body) == {"rights", "confidence", "justification", "engine"}


@pytest.mark.parametrize("degree", ["haute", "moyenne", "basse"])
def test_the_confidence_is_ordinal_with_three_degrees_and_never_a_number(client, upstream, degree):
    upstream(content={**VALID_VERDICT, "confiance": degree})

    body = client.post("/opinions/llm", json={"text": "Mes données ?"}).json()

    assert body["confidence"] in {"High", "Medium", "Low"}


def test_the_confidence_is_mandatory_here_where_it_is_forbidden_to_the_lexicon(client, upstream):
    upstream(content=VALID_VERDICT)

    llm_body = client.post("/opinions/llm", json={"text": "Supprimez mes données."}).json()
    lexicon_body = client.post("/opinions/lexicon", json={"text": "Supprimez mes données."}).json()

    assert "confidence" in llm_body
    assert "confidence" not in lexicon_body


# --------------------------------------------------------------------------
# Refus d'entrée — la même frontière que le lexique
# --------------------------------------------------------------------------


@pytest.mark.parametrize("body", [{}, {"text": ""}, {"text": "   \n\t "}])
def test_a_text_that_is_absent_or_empty_is_refused(client, upstream, body):
    upstream(content=VALID_VERDICT)

    response = client.post("/opinions/llm", json=body)

    assert response.status_code == 400
    assert response.headers["content-type"].startswith(PROBLEM_JSON)


def test_the_request_carries_the_text_alone(client, upstream):
    upstream(content=VALID_VERDICT)

    response = client.post(
        "/opinions/llm",
        json={"text": "Supprimez mes données.", "confiance_attendue": "haute"},
    )

    assert response.status_code == 400


# --------------------------------------------------------------------------
# Panne du moteur — jamais un avis à moitié valide
# --------------------------------------------------------------------------


@pytest.mark.parametrize(
    ("failure", "verdict"),
    [
        ("taxonomie inconnue", {"droits": ["derefencement"]}),
        ("droits vide", {"droits": []}),
        ("hors périmètre accompagné", {"droits": ["hors-perimetre", "effacement"]}),
        ("droit rendu deux fois", {"droits": ["acces", "acces"]}),
        ("confiance hors échelle", {"confiance": "certaine"}),
        ("justification vide", {"justification": "  "}),
    ],
)
def test_a_broken_model_renders_a_non_2xx(client, upstream, failure, verdict):
    upstream(content=VALID_VERDICT | verdict)

    response = client.post("/opinions/llm", json={"text": "Supprimez mes données."})

    assert response.status_code == 502, failure
    assert response.headers["content-type"].startswith(PROBLEM_JSON)


def test_an_answer_that_is_not_json_at_all_is_a_broken_model(client, upstream):
    upstream(content="je préfère répondre en prose")

    response = client.post("/opinions/llm", json={"text": "Supprimez mes données."})

    assert response.status_code == 502


def test_a_broken_model_and_a_broken_lexicon_do_not_send_to_the_same_place(client, upstream, monkeypatch):
    """Même panne du domaine, deux codes : ce qui diffère est *qui* est à réparer.

    Un bogue Python dans le lexique, un modèle qui déraille en face — les confondre enverrait
    l'exploitant chercher l'un là où c'est l'autre.
    """
    from qualification_sidecar import lexicon

    upstream(content=VALID_VERDICT | {"droits": []})
    broken_llm = client.post("/opinions/llm", json={"text": "Supprimez mes données."})

    monkeypatch.setattr(lexicon, "qualify", lambda text: [])
    broken_lexicon = client.post("/opinions/lexicon", json={"text": "Supprimez mes données."})

    assert broken_llm.status_code == 502
    assert broken_lexicon.status_code == 500


# --------------------------------------------------------------------------
# Les trois échecs de l'amont, et leurs trois codes
# --------------------------------------------------------------------------


def test_an_absent_upstream_and_a_slow_one_never_share_a_code(client, upstream):
    """Le premier se répare en démarrant Ollama, le second en changeant de modèle ou de matériel."""
    upstream(raises=llm.ModelUnreachable("connexion refusée"))
    absent = client.post("/opinions/llm", json={"text": "Supprimez mes données."})

    upstream(raises=llm.ModelTooSlow("échéance passée"))
    slow = client.post("/opinions/llm", json={"text": "Supprimez mes données."})

    assert absent.status_code == 503
    assert slow.status_code == 504
    assert absent.status_code != slow.status_code


def test_the_three_upstream_failures_have_three_codes(client, upstream):
    """Répondu-mais-inexploitable, injoignable, trop lent : trois causes, trois codes."""
    upstream(content=VALID_VERDICT | {"droits": []})
    unusable = client.post("/opinions/llm", json={"text": "Supprimez mes données."})

    upstream(raises=llm.ModelUnreachable("connexion refusée"))
    absent = client.post("/opinions/llm", json={"text": "Supprimez mes données."})

    upstream(raises=llm.ModelTooSlow("échéance passée"))
    slow = client.post("/opinions/llm", json={"text": "Supprimez mes données."})

    codes = {unusable.status_code, absent.status_code, slow.status_code}

    assert codes == {502, 503, 504}


@pytest.mark.parametrize(
    "raised", [llm.ModelUnreachable("refusée"), llm.ModelTooSlow("trop lente")]
)
def test_an_upstream_failure_is_named_in_french_problem_json(client, upstream, raised):
    upstream(raises=raised)

    response = client.post("/opinions/llm", json={"text": "Supprimez mes données."})

    assert response.headers["content-type"].startswith(PROBLEM_JSON)
    assert response.json()["title"]
    assert response.json()["detail"]


def test_an_upstream_failure_is_never_dressed_up_as_an_opinion(client, upstream):
    """Un avis vide serait lu comme « hors périmètre » — la panne doit rester une panne."""
    upstream(raises=llm.ModelTooSlow("échéance passée"))

    body = client.post("/opinions/llm", json={"text": "Supprimez mes données."}).json()

    assert "rights" not in body


def test_the_upstream_failure_is_correlated_with_the_callers_trace(client, upstream, caplog):
    traceparent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
    upstream(raises=llm.ModelUnreachable("connexion refusée"))

    with caplog.at_level(logging.ERROR, logger="qualification_sidecar"):
        client.post(
            "/opinions/llm",
            json={"text": "Supprimez mes données."},
            headers={"traceparent": traceparent},
        )

    assert [r for r in caplog.records if getattr(r, "traceparent", None) == traceparent]


# --------------------------------------------------------------------------
# Deux points d'entrée, deux moteurs, aucune famine
# --------------------------------------------------------------------------


def test_the_lexicon_is_served_while_an_llm_call_is_still_in_flight(monkeypatch):
    """Le lexique doit répondre pendant qu'un appel LLM dure — c'est son double rôle qui l'exige.

    Un appel au modèle dure des secondes ; s'il occupait un fil de travail, le moteur de dernier
    recours deviendrait indisponible exactement quand le LLM va mal.
    """
    started = asyncio.Event()
    release = asyncio.Event()

    class SlowModel:
        async def answer(self, *, system, user):
            started.set()
            await release.wait()
            return llm.ModelAnswer(content=json.dumps(VALID_VERDICT), served_model="lent:1b")

    monkeypatch.setattr(app_module, "LLM_MODEL", SlowModel())

    async def both_at_once():
        transport = httpx2.ASGITransport(app=app)
        async with httpx2.AsyncClient(transport=transport, base_url="http://sidecar") as http:
            slow = asyncio.create_task(http.post("/opinions/llm", json={"text": "Mes données ?"}))

            await asyncio.wait_for(started.wait(), timeout=5)
            witness = await asyncio.wait_for(
                http.post("/opinions/lexicon", json={"text": "Supprimez mes données."}),
                timeout=5,
            )

            release.set()
            return witness, await slow

    witness, slow = asyncio.run(both_at_once())

    assert witness.status_code == 200
    assert witness.json()["rights"] == ["Erasure"]
    assert slow.status_code == 200
