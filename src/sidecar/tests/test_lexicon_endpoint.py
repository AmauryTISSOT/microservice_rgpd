"""Le point d'entrée d'avis lexical, vu de l'appelant .NET.

Palette courte, et c'est ce qu'achètent deux points d'entrée séparés : le lexique n'a aucun amont,
donc ni `502`, ni `503`, ni `504` ne peuvent survenir. Succès, refus d'entrée, panne du moteur.
"""

import logging

import pytest
from fastapi.testclient import TestClient

from qualification_sidecar import lexicon
from qualification_sidecar.app import app

PROBLEM_JSON = "application/problem+json"


@pytest.fixture
def client():
    return TestClient(app)


def test_a_text_is_qualified_in_canonical_english_names(client):
    response = client.post(
        "/opinions/lexicon",
        json={"text": "Conformément à l'article 15 du RGPD, communiquez-moi une copie de mes données."},
    )

    assert response.status_code == 200
    assert response.json()["rights"] == ["Access"]


def test_the_answer_says_which_engine_spoke_and_in_which_version(client):
    engine = client.post("/opinions/lexicon", json={"text": "Supprimez mes données."}).json()["engine"]

    assert engine == {"name": lexicon.ENGINE_NAME, "version": lexicon.ENGINE_VERSION}


def test_the_answer_carries_nothing_else(client):
    body = client.post("/opinions/lexicon", json={"text": "Supprimez mes données."}).json()

    assert set(body) == {"rights", "engine"}


def test_the_lexicon_never_declares_a_confidence(client):
    body = client.post("/opinions/lexicon", json={"text": "Supprimez mes données."}).json()

    assert "confidence" not in body


def test_a_single_emoji_is_qualified_not_refused(client):
    response = client.post("/opinions/lexicon", json={"text": "🙂"})

    assert response.status_code == 200
    assert response.json()["rights"] == ["OutOfScope"]


def test_an_absent_text_is_refused(client):
    response = client.post("/opinions/lexicon", json={})

    assert response.status_code == 400
    assert response.headers["content-type"].startswith(PROBLEM_JSON)


def test_a_text_empty_once_trimmed_is_refused(client):
    response = client.post("/opinions/lexicon", json={"text": "   \n\t "})

    assert response.status_code == 400


def test_an_empty_text_is_refused(client):
    assert client.post("/opinions/lexicon", json={"text": ""}).status_code == 400


def test_a_body_carrying_its_own_identifier_is_refused(client):
    response = client.post(
        "/opinions/lexicon",
        json={"text": "Supprimez mes données.", "requestId": "42"},
    )

    assert response.status_code == 400


def test_a_refusal_names_the_problem_in_french(client):
    problem = client.post("/opinions/lexicon", json={"text": ""}).json()

    assert problem["status"] == 400
    assert problem["title"]
    assert problem["detail"]


@pytest.mark.parametrize(
    ("failure", "verdict"),
    [
        ("taxonomie inconnue", ["derefencement"]),
        ("droits vide", []),
        ("hors périmètre accompagné", ["hors-perimetre", "effacement"]),
    ],
)
def test_a_broken_engine_renders_a_non_2xx(client, monkeypatch, failure, verdict):
    monkeypatch.setattr(lexicon, "qualify", lambda text: verdict)

    response = client.post("/opinions/lexicon", json={"text": "Supprimez mes données."})

    assert response.status_code >= 500, failure
    assert response.headers["content-type"].startswith(PROBLEM_JSON)


def test_the_correlation_uses_the_propagated_trace_header(client, caplog):
    traceparent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"

    with caplog.at_level(logging.INFO, logger="qualification_sidecar"):
        client.post(
            "/opinions/lexicon",
            json={"text": "Supprimez mes données."},
            headers={"traceparent": traceparent},
        )

    assert [record for record in caplog.records if getattr(record, "traceparent", None) == traceparent]


def test_no_home_made_identifier_is_invented_without_the_trace_header(client, caplog):
    with caplog.at_level(logging.INFO, logger="qualification_sidecar"):
        client.post("/opinions/lexicon", json={"text": "Supprimez mes données."})

    records = [record for record in caplog.records if hasattr(record, "traceparent")]

    assert records
    assert all(record.traceparent is None for record in records)


def test_an_unreadable_body_is_refused_in_problem_json(client):
    response = client.post(
        "/opinions/lexicon",
        content=b"{ ceci n'est pas du JSON",
        headers={"content-type": "application/json"},
    )

    assert response.status_code == 400
    assert response.headers["content-type"].startswith(PROBLEM_JSON)


def test_an_unknown_path_is_refused_in_problem_json(client):
    response = client.post("/opinions/inconnu", json={"text": "Supprimez mes données."})

    assert response.status_code == 404
    assert response.headers["content-type"].startswith(PROBLEM_JSON)


def test_a_verb_the_entry_point_does_not_serve_is_refused_in_problem_json(client):
    response = client.get("/opinions/lexicon")

    assert response.status_code == 405
    assert response.headers["content-type"].startswith(PROBLEM_JSON)


def test_the_sidecar_reports_it_is_alive(client):
    assert client.get("/health").status_code == 200
