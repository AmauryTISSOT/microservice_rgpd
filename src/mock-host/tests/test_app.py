"""Le mock du système hôte, route par route. Le délai y est réduit pour que la suite reste rapide."""

import time

import pytest
from starlette.testclient import TestClient

from mock_host.app import DEFAULT_DELAY_MS, RIGHT_ROUTES, create_app, delay_ms_from_environment

#: Assez long pour qu'une réponse prématurée soit mesurable, assez court pour ne pas ralentir la suite.
SHORT_DELAY_MS = 200


@pytest.fixture
def client() -> TestClient:
    return TestClient(create_app(delay_ms=0))


def _call(client: TestClient, method: str, path: str, **kwargs):
    return client.request(method, path, **kwargs)


#: Chaque route de droit avec sa méthode, `POST` pour toutes (ADR-0026), et son code de succès.
ROUTES = [(path, "POST", status_code) for path, status_code in RIGHT_ROUTES]


# AC1 — Chaque droit a sa route et son code


@pytest.mark.parametrize(("path", "method", "status_code"), ROUTES)
def test_each_right_answers_with_its_code(client, path, method, status_code):
    response = _call(client, method, path)

    assert response.status_code == status_code


@pytest.mark.parametrize(("path", "method", "status_code"), [r for r in ROUTES if r[2] in (200, 202)])
def test_200_and_202_carry_status_ok(client, path, method, status_code):
    response = _call(client, method, path)

    assert response.json() == {"status": "ok"}


@pytest.mark.parametrize(("path", "method", "status_code"), [r for r in ROUTES if r[2] == 204])
def test_204_has_an_empty_body(client, path, method, status_code):
    response = _call(client, method, path)

    assert response.content == b""


def test_the_table_is_the_one_of_the_story():
    assert set(RIGHT_ROUTES) == {
        ("/rights/access", 200),
        ("/rights/portability", 200),
        ("/rights/rectification", 204),
        ("/rights/erasure", 204),
        ("/rights/restriction", 202),
        ("/rights/objection", 202),
    }


# AC2 — Délai simulé de 2 secondes par défaut


def test_default_delay_is_two_seconds(monkeypatch):
    monkeypatch.delenv("MOCK_DELAY_MS", raising=False)

    assert DEFAULT_DELAY_MS == 2_000
    assert delay_ms_from_environment() == 2_000


# AC3 — Délai configurable


@pytest.mark.parametrize(("path", "method", "status_code"), ROUTES)
def test_response_never_arrives_before_the_configured_delay(monkeypatch, path, method, status_code):
    monkeypatch.setenv("MOCK_DELAY_MS", str(SHORT_DELAY_MS))
    client = TestClient(create_app())

    started = time.monotonic()
    response = _call(client, method, path)
    elapsed_ms = (time.monotonic() - started) * 1_000

    assert response.status_code == status_code
    assert elapsed_ms >= SHORT_DELAY_MS


def test_zero_delay_answers_without_waiting(monkeypatch):
    monkeypatch.setenv("MOCK_DELAY_MS", "0")
    client = TestClient(create_app())

    started = time.monotonic()
    client.post("/rights/access")
    elapsed_ms = (time.monotonic() - started) * 1_000

    assert elapsed_ms < SHORT_DELAY_MS


@pytest.mark.parametrize("raw", ["deux", "-1", "1.5"])
def test_an_unreadable_delay_refuses_to_start(monkeypatch, raw):
    monkeypatch.setenv("MOCK_DELAY_MS", raw)

    with pytest.raises(ValueError, match="MOCK_DELAY_MS"):
        create_app()


# AC4 — Aucune validation de la requête


@pytest.mark.parametrize(("path", "method", "status_code"), ROUTES)
@pytest.mark.parametrize(
    "kwargs",
    [
        {},
        {"json": {"subject": "jean@example.org", "fields": ["email"]}},
        {"content": b"\x00\xffpas du json", "headers": {"Content-Type": "application/octet-stream"}},
        {"content": "texte libre", "headers": {"Content-Type": "text/plain", "X-Anything": "oui"}},
    ],
)
def test_any_body_or_headers_are_accepted(client, path, method, status_code, kwargs):
    response = _call(client, method, path, **kwargs)

    assert response.status_code == status_code


# AC5 — Mauvaise méthode refusée


@pytest.mark.parametrize(("path", "method", "status_code"), ROUTES)
def test_an_unexpected_method_is_refused(client, path, method, status_code):
    for other in {"GET", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"}:
        assert _call(client, other, path).status_code == 405, f"{other} {path}"


# AC6 — Health check immédiat


def test_health_answers_immediately_whatever_the_delay(monkeypatch):
    monkeypatch.setenv("MOCK_DELAY_MS", "5000")
    client = TestClient(create_app())

    started = time.monotonic()
    response = client.get("/health")
    elapsed_ms = (time.monotonic() - started) * 1_000

    assert response.status_code == 200
    assert elapsed_ms < 1_000


# AC7 — Chaque appel est journalisé


def test_each_call_is_written_to_standard_output(client, capsys):
    client.post("/rights/rectification", json={"email": "jean@example.org"})

    out = capsys.readouterr().out
    assert "POST /rights/rectification" in out
    assert '{"email":"jean@example.org"}' in out


# `status` — un échec simulé, droit par droit


@pytest.mark.parametrize(("path", "method", "status_code"), ROUTES)
@pytest.mark.parametrize("forced", [200, 204, 302, 404, 500, 503])
def test_status_is_the_response_code(client, path, method, status_code, forced):
    response = _call(client, method, path, params={"status": forced})

    assert response.status_code == forced


def test_a_forced_204_has_an_empty_body(client):
    response = client.post("/rights/access", params={"status": 204})

    assert response.content == b""


# `delay_ms` — un délai simulé, droit par droit


@pytest.mark.parametrize(("path", "method", "status_code"), ROUTES)
def test_delay_ms_delays_the_response(client, path, method, status_code):
    started = time.monotonic()
    response = _call(client, method, path, params={"delay_ms": SHORT_DELAY_MS})
    elapsed_ms = (time.monotonic() - started) * 1_000

    assert response.status_code == status_code
    assert elapsed_ms >= SHORT_DELAY_MS


def test_delay_ms_replaces_the_environment_delay_for_this_call(monkeypatch):
    monkeypatch.setenv("MOCK_DELAY_MS", "5000")
    client = TestClient(create_app())

    started = time.monotonic()
    response = client.post("/rights/erasure", params={"delay_ms": 0})
    elapsed_ms = (time.monotonic() - started) * 1_000

    assert response.status_code == 204
    assert elapsed_ms < 1_000


def test_status_and_delay_ms_combine(client):
    started = time.monotonic()
    response = client.post("/rights/objection", params={"status": 503, "delay_ms": SHORT_DELAY_MS})
    elapsed_ms = (time.monotonic() - started) * 1_000

    assert response.status_code == 503
    assert elapsed_ms >= SHORT_DELAY_MS


def test_the_parameters_hold_for_this_call_only(client):
    client.post("/rights/access", params={"status": 503})

    assert client.post("/rights/access").status_code == 200


# Une valeur illisible est refusée en 400


@pytest.mark.parametrize(("path", "method", "status_code"), ROUTES)
@pytest.mark.parametrize(
    "params",
    [
        {"status": "abc"},
        {"status": ""},
        {"status": "50.3"},
        {"status": "99"},
        {"status": "600"},
        {"delay_ms": "deux"},
        {"delay_ms": ""},
        {"delay_ms": "-1"},
        {"delay_ms": "1.5"},
    ],
)
def test_an_unreadable_parameter_is_refused(client, path, method, status_code, params):
    response = _call(client, method, path, params=params)

    assert response.status_code == 400
    assert next(iter(params)) in response.json()["detail"]


def test_an_unreadable_parameter_is_refused_without_waiting(monkeypatch):
    monkeypatch.setenv("MOCK_DELAY_MS", "5000")
    client = TestClient(create_app())

    started = time.monotonic()
    response = client.post("/rights/access", params={"status": "abc"})
    elapsed_ms = (time.monotonic() - started) * 1_000

    assert response.status_code == 400
    assert elapsed_ms < 1_000
