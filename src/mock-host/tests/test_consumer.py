"""Ce que l'environnement dit au consommateur, et ce que le mock en fait.

Aucun broker n'est joint ici : ces tests portent sur la lecture des réglages et sur la règle « pas
d'hôte, pas de consommateur ». Ce que le consommateur journalise s'éprouve dans
`test_rights_journal`.
"""

import pytest
from starlette.testclient import TestClient

from mock_host.app import create_app
from mock_host.consumer import (
    DEFAULT_CREDENTIAL,
    DEFAULT_PORT,
    DEFAULT_VIRTUAL_HOST,
    BrokerConnection,
    connection_from_environment,
)

BROKER_VARIABLES = (
    "MOCK_RABBITMQ_HOST",
    "MOCK_RABBITMQ_PORT",
    "MOCK_RABBITMQ_VHOST",
    "MOCK_RABBITMQ_USER",
    "MOCK_RABBITMQ_PASSWORD",
)


@pytest.fixture(autouse=True)
def _a_silent_environment(monkeypatch):
    for name in BROKER_VARIABLES:
        monkeypatch.delenv(name, raising=False)


# Pas d'hôte, pas de consommateur


def test_without_a_host_no_connection_is_declared():
    assert connection_from_environment() is None


def test_without_a_host_the_mock_starts_and_answers_http():
    with TestClient(create_app(delay_ms=0)) as client:
        assert client.get("/health").status_code == 200
        assert client.post("/rights/access").status_code == 200


@pytest.mark.parametrize("blank", ["", "   "])
def test_a_blank_host_counts_as_absent(monkeypatch, blank):
    monkeypatch.setenv("MOCK_RABBITMQ_HOST", blank)

    assert connection_from_environment() is None


# Ce qu'un hôte déclaré emporte avec lui


def test_a_declared_host_is_read_with_its_port_and_credentials(monkeypatch):
    monkeypatch.setenv("MOCK_RABBITMQ_HOST", "localhost")
    monkeypatch.setenv("MOCK_RABBITMQ_PORT", "5673")
    monkeypatch.setenv("MOCK_RABBITMQ_VHOST", "rgpd")
    monkeypatch.setenv("MOCK_RABBITMQ_USER", "integrateur")
    monkeypatch.setenv("MOCK_RABBITMQ_PASSWORD", "un-mot-de-passe")

    assert connection_from_environment() == BrokerConnection(
        host="localhost",
        port=5673,
        virtual_host="rgpd",
        user="integrateur",
        password="un-mot-de-passe",
    )


def test_a_host_alone_takes_the_defaults(monkeypatch):
    monkeypatch.setenv("MOCK_RABBITMQ_HOST", "  broker  ")

    connection = connection_from_environment()

    assert connection == BrokerConnection(
        host="broker",
        port=DEFAULT_PORT,
        virtual_host=DEFAULT_VIRTUAL_HOST,
        user=DEFAULT_CREDENTIAL,
        password=DEFAULT_CREDENTIAL,
    )


@pytest.mark.parametrize("raw", ["cinq-mille", "-1", "0", "5672.0", "+5672", "70000", "5_672"])
def test_an_unreadable_port_refuses_to_start(monkeypatch, raw):
    monkeypatch.setenv("MOCK_RABBITMQ_HOST", "localhost")
    monkeypatch.setenv("MOCK_RABBITMQ_PORT", raw)

    with pytest.raises(ValueError, match="MOCK_RABBITMQ_PORT"):
        create_app(delay_ms=0)


# Les identifiants ne se recopient dans aucune URL


def test_the_connection_carries_no_url():
    """⚠️ Aucune chaîne de connexion URI : la règle du service (`RabbitMqOptions`) vaut ici aussi.

    Une URL se journalise, se recopie et s'affiche ; `aio_pika` reçoit donc les parties séparément.
    """
    connection = BrokerConnection(
        host="localhost", port=5672, virtual_host="/", user="jean", password="secret"
    )

    assert not any("url" in name.lower() for name in dir(connection))


def test_a_password_keeps_its_spaces(monkeypatch):
    """Rogner un mot de passe serait corriger ce que personne n'a demandé de corriger."""
    monkeypatch.setenv("MOCK_RABBITMQ_HOST", "localhost")
    monkeypatch.setenv("MOCK_RABBITMQ_PASSWORD", "  un secret  ")

    assert connection_from_environment().password == "  un secret  "
