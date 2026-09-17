"""Le journal du consommateur : ce qu'il dit d'un message reçu, et ce qu'il fait d'un doublon.

C'est la moitié « l'idempotence est la charge du consommateur » de l'avertissement de la modale, ici
**démontrée** : le service pose un `message-id` égal à l'identifiant de la demande (ADR-0028), et
c'est au destinataire de s'en servir. Le broker n'est pas nécessaire pour l'éprouver — le journal
est un seam pur.
"""

import json

import pytest

from mock_host.rights_journal import (
    RIGHTS_BINDING_KEY,
    RIGHTS_EXCHANGE,
    RIGHTS_QUEUE,
    RightsJournal,
)

#: Une demande telle que le service la publie : cinq clés en camelCase (ADR-0026).
A_REQUEST_ID = "3f1d9a3c-1c2e-4f4a-9a9e-5b8c0f2d7e11"


def _body(request_id: str = A_REQUEST_ID, right: str = "Erasure") -> bytes:
    return json.dumps(
        {
            "requestId": request_id,
            "right": right,
            "email": "jean@example.org",
            "firstName": "Jean",
            "lastName": None,
        }
    ).encode("utf-8")


# La topologie est celle que la démonstration annonce


def test_the_topology_is_the_one_of_the_story():
    assert RIGHTS_EXCHANGE == "rgpd.rights"
    assert RIGHTS_BINDING_KEY == "rights.#"
    assert RIGHTS_QUEUE.startswith("rgpd.rights.")


# Un message neuf est retenu, et sa ligne nomme droit, demande et message-id


def test_a_first_message_is_accepted():
    journal = RightsJournal()

    reception = journal.receive(A_REQUEST_ID, _body())

    assert reception.accepted


def test_the_line_names_the_right_the_request_and_the_message_id():
    journal = RightsJournal()

    line = journal.receive(A_REQUEST_ID, _body(right="Access")).line

    assert "Access" in line
    assert A_REQUEST_ID in line
    assert "message-id" in line


def test_the_line_never_carries_personal_data():
    journal = RightsJournal()

    line = journal.receive(A_REQUEST_ID, _body()).line

    assert "jean@example.org" not in line
    assert "Jean" not in line


# Un `message-id` déjà vu est ignoré, et le consommateur le dit


def test_a_message_id_already_seen_is_ignored():
    journal = RightsJournal()
    journal.receive(A_REQUEST_ID, _body())

    reception = journal.receive(A_REQUEST_ID, _body())

    assert not reception.accepted


def test_the_consumer_says_it_ignores_a_duplicate():
    journal = RightsJournal()
    journal.receive(A_REQUEST_ID, _body())

    line = journal.receive(A_REQUEST_ID, _body()).line

    assert "doublon" in line
    assert A_REQUEST_ID in line


def test_two_different_message_ids_are_both_accepted():
    journal = RightsJournal()
    other = "8c2b6f10-0a44-4d55-bb77-9e3a1c6d5f22"

    assert journal.receive(A_REQUEST_ID, _body()).accepted
    assert journal.receive(other, _body(request_id=other)).accepted


def test_a_duplicate_is_ignored_whatever_its_body_says():
    """Le `message-id` seul décide : dédupliquer n'ouvre pas le corps (ADR-0028)."""
    journal = RightsJournal()
    journal.receive(A_REQUEST_ID, _body(right="Erasure"))

    reception = journal.receive(A_REQUEST_ID, _body(right="Access"))

    assert not reception.accepted


# Ce que le journal fait d'un message mal formé — il journalise quand même, et acquitte quand même


@pytest.mark.parametrize("raw", [b"", b"pas du json", b"{}", b"[1, 2, 3]"])
def test_an_unreadable_body_is_still_journalled(raw):
    journal = RightsJournal()

    reception = journal.receive(A_REQUEST_ID, raw)

    assert reception.accepted
    assert A_REQUEST_ID in reception.line


def test_a_message_without_a_message_id_is_accepted_and_said_undeduplicable():
    journal = RightsJournal()

    first = journal.receive(None, _body())
    second = journal.receive(None, _body())

    assert first.accepted
    assert second.accepted
    assert "sans message-id" in second.line


@pytest.mark.parametrize("blank", ["", "   "])
def test_a_blank_message_id_counts_as_absent(blank):
    journal = RightsJournal()

    assert journal.receive(blank, _body()).accepted
    assert journal.receive(blank, _body()).accepted
