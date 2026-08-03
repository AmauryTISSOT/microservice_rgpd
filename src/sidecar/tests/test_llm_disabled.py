"""Le sidecar démarré **sans moteur LLM** : ce qu'il sert quand même, et ce qu'il refuse en le nommant.

Un déploiement sans matériel accéléré est le cas ordinaire — un poste sans carte graphique, un
exécuteur d'intégration continue, une démonstration. Le drapeau qui commande le moteur est donc
**éteint par défaut**, et ces tests vérifient ce que voit alors un exploitant : le lexique intact,
un refus nommé sur le point d'entrée LLM, et pas une seule variable de moteur à poser.

Ils démarrent l'application au seam FastAPI, **sans sous-processus**. Ce qui le permet est la
paresse du chargement des réglages : si celui-ci avait encore lieu à l'import, la réimportation
faite ici se heurterait au refus de démarrer qu'elle est précisément là pour écarter.
"""

import importlib

import pytest
from fastapi.testclient import TestClient

import qualification_sidecar.app

PROBLEM_JSON = "application/problem+json"

#: Les sept variables du moteur, plus le drapeau qui commande son existence. Aucune n'est posée dans
#: les tests qui suivent : c'est l'objet même de la promesse.
LLM_ENVIRONMENT = (
    "QUALIFICATION_LLM_ENABLED",
    "QUALIFICATION_LLM_BASE_URL",
    "QUALIFICATION_LLM_MODEL",
    "QUALIFICATION_LLM_API_KEY",
    "QUALIFICATION_LLM_TEMPERATURE",
    "QUALIFICATION_LLM_SEED",
    "QUALIFICATION_LLM_DEADLINE_SECONDS",
    "QUALIFICATION_LLM_CALLER_DEADLINE_SECONDS",
)


@pytest.fixture
def disabled_app(monkeypatch):
    """Rend une application réimportée alors qu'aucune variable du moteur LLM n'est posée.

    La réimportation est le cœur du test : elle rejoue l'import du module dans un environnement vide
    de tout réglage LLM, ce qu'un chargement fait à l'import rendrait impossible.
    """
    for variable in LLM_ENVIRONMENT:
        monkeypatch.delenv(variable, raising=False)

    reloaded = importlib.reload(qualification_sidecar.app)

    yield reloaded.app

    # L'environnement est rendu avant la dernière réimportation, pour que le module laissé aux
    # tests suivants soit celui que `conftest` a configuré.
    monkeypatch.undo()
    importlib.reload(qualification_sidecar.app)


@pytest.fixture
def client(disabled_app):
    """Entre dans le cycle de vie de l'application : le démarrage éteint est ici *exercé*, pas supposé."""
    with TestClient(disabled_app) as client:
        yield client


# --------------------------------------------------------------------------
# Démarrer sans moteur
# --------------------------------------------------------------------------


def test_the_sidecar_starts_without_a_single_llm_setting(client):
    """Le défaut est « éteint » : sans configuration, aucun réglage LLM n'est requis."""
    assert client.get("/health").status_code == 200


def test_importing_the_application_module_does_not_load_the_llm_settings(disabled_app):
    """La paresse est ce qui rend le démarrage éteint observable sans sous-processus.

    Que la fixture ait rendu une application prouve la propriété : elle a réimporté le module dans
    un environnement dépourvu des sept variables.
    """
    assert disabled_app is not None


def test_a_residual_deadline_pair_in_disorder_no_longer_prevents_starting(monkeypatch):
    """Éteint, la vérification d'ordre strict des deux échéances ne s'applique plus.

    Un exploitant doit pouvoir laisser d'anciens réglages écrits — fussent-ils incohérents — sans
    que le moteur éteint aille les lire. Faute d'échéance à tenir, il n'y a rien à vérifier.
    """
    monkeypatch.setenv("QUALIFICATION_LLM_ENABLED", "false")
    monkeypatch.setenv("QUALIFICATION_LLM_DEADLINE_SECONDS", "300")
    monkeypatch.setenv("QUALIFICATION_LLM_CALLER_DEADLINE_SECONDS", "30")

    reloaded = importlib.reload(qualification_sidecar.app)

    try:
        with TestClient(reloaded.app) as client:
            assert client.get("/health").status_code == 200
    finally:
        monkeypatch.undo()
        importlib.reload(qualification_sidecar.app)


# --------------------------------------------------------------------------
# Le refus nommé
# --------------------------------------------------------------------------


def test_the_llm_entry_point_refuses_by_saying_no_model_is_served(client):
    """La route est conservée plutôt que retirée : l'exploitant qui l'interroge obtient une phrase.

    `501` dit ce qui est vrai — ce déploiement n'implémente pas ce service —, là où un `404` lui
    ferait chercher une faute de frappe dans son URL et un `503` un serveur à redémarrer.
    """
    response = client.post("/opinions/llm", json={"text": "Supprimez mes données."})

    assert response.status_code == 501


def test_the_refusal_takes_the_common_error_shape_of_the_sidecar(client):
    """Une seule forme d'erreur à lire, quel que soit l'endroit où l'appelant s'est arrêté."""
    response = client.post("/opinions/llm", json={"text": "Supprimez mes données."})
    body = response.json()

    assert response.headers["content-type"].startswith(PROBLEM_JSON)
    assert body["status"] == 501
    assert set(body) == {"type", "title", "status", "detail"}


def test_the_refusal_says_in_french_that_no_model_is_served_here(client):
    """Une phrase, pas une énigme : le texte doit distinguer l'éteint du tombé."""
    body = client.post("/opinions/llm", json={"text": "Supprimez mes données."}).json()

    assert "modèle" in body["detail"]
    assert "modèle" in body["title"].lower() or "moteur" in body["title"].lower()


def test_the_refusal_is_never_dressed_up_as_an_opinion(client):
    """Un moteur éteint ne rend pas un avis vide : il ne rend pas d'avis du tout."""
    body = client.post("/opinions/llm", json={"text": "Supprimez mes données."}).json()

    assert "rights" not in body
    assert "confidence" not in body


# --------------------------------------------------------------------------
# Le lexique et la santé, strictement inchangés
# --------------------------------------------------------------------------


def test_the_lexicon_entry_point_is_untouched_by_the_extinction(client):
    """Le lexique ne dépend d'aucun matériel : éteindre le LLM ne lui retire rien."""
    response = client.post("/opinions/lexicon", json={"text": "Supprimez mes données."})
    body = response.json()

    assert response.status_code == 200
    assert body["rights"] == ["Erasure"]
    assert body["engine"]["name"] == "lexicon"


def test_the_lexicon_still_refuses_an_empty_text_the_same_way(client):
    """Sa palette courte est celle d'hier : succès, refus d'entrée, panne du moteur."""
    response = client.post("/opinions/lexicon", json={"text": "   "})

    assert response.status_code == 400
    assert response.headers["content-type"].startswith(PROBLEM_JSON)


def test_the_health_entry_point_answers_exactly_as_before(client):
    """La supervision existante doit continuer de fonctionner sans être retouchée."""
    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {"status": "healthy"}
