"""Le sidecar sans modèle : un drapeau éteint par défaut, et un refus qui se dit en toutes lettres.

Un déploiement qui ne dit rien ne sert aucun modèle. C'est le cas par défaut, et c'est celui que
cette suite exerce : l'application démarre alors qu'aucune des sept variables du moteur n'est posée,
sert le lexique exactement comme d'habitude, et **nomme** son refus sur le point d'entrée LLM plutôt
que de faire disparaître la route — un exploitant qui l'interroge à la main doit obtenir une phrase,
pas une énigme.

L'extinction s'observe ici **sans sous-processus** : le module de l'application est réimporté sous
un autre nom, environnement vidé, ce qui est un démarrage entier et non une simulation. C'est ce que
le chargement paresseux des réglages achète ; à l'import, plus rien ne lit la configuration du
moteur.
"""

import importlib.util
import sys
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from qualification_sidecar import app as app_module
from qualification_sidecar import llm

PROBLEM_JSON = "application/problem+json"

#: Les sept variables du moteur, telles que `conftest` les pose pour toute la suite. Les retirer est
#: ce qui rend le démarrage éteint vérifiable : s'il en lisait une seule, il échouerait ici.
ENGINE_SETTINGS = (
    "QUALIFICATION_LLM_BASE_URL",
    "QUALIFICATION_LLM_MODEL",
    "QUALIFICATION_LLM_API_KEY",
    "QUALIFICATION_LLM_TEMPERATURE",
    "QUALIFICATION_LLM_SEED",
    "QUALIFICATION_LLM_DEADLINE_SECONDS",
    "QUALIFICATION_LLM_CALLER_DEADLINE_SECONDS",
)


def start_afresh(monkeypatch, *, enabled=None, environment=None):
    """Démarre l'application depuis zéro, sans une seule variable du moteur.

    Le module est chargé sous un **autre nom** plutôt que rechargé : celui que la suite partage
    reste intact, et ce qu'on observe ici est bien un démarrage complet — le module s'exécute de sa
    première ligne à sa dernière.
    """
    for setting in ENGINE_SETTINGS:
        monkeypatch.delenv(setting, raising=False)

    monkeypatch.delenv("QUALIFICATION_LLM_ENABLED", raising=False)

    if enabled is not None:
        monkeypatch.setenv("QUALIFICATION_LLM_ENABLED", enabled)

    for name, value in (environment or {}).items():
        monkeypatch.setenv(name, value)

    name = "tests.app_started_afresh"
    specification = importlib.util.spec_from_file_location(name, Path(app_module.__file__))
    started = importlib.util.module_from_spec(specification)

    # Inscrit avant exécution, et retiré à la fin du test : Pydantic résout les annotations
    # différées des routes en cherchant leur module par son nom, et ne le trouverait pas sinon.
    monkeypatch.setitem(sys.modules, name, started)
    specification.loader.exec_module(started)

    return started


# --------------------------------------------------------------------------
# Le drapeau — éteint tant que personne n'a écrit le contraire
# --------------------------------------------------------------------------


def test_a_deployment_that_says_nothing_serves_no_model():
    """La valeur par défaut est le refus : aucun texte de personne concernée ne part vers un modèle
    par simple effet de bord d'un déploiement muet."""
    assert llm.engine_is_enabled({}) is False


@pytest.mark.parametrize("written", ["true", "True", "TRUE", "1", "yes", "on"])
def test_the_engine_exists_only_where_it_is_written(written):
    assert llm.engine_is_enabled({"QUALIFICATION_LLM_ENABLED": written}) is True


@pytest.mark.parametrize("written", ["false", "False", "0", "no", "off", ""])
def test_the_engine_stays_off_where_the_flag_says_so(written):
    assert llm.engine_is_enabled({"QUALIFICATION_LLM_ENABLED": written}) is False


def test_a_flag_that_says_neither_yes_nor_no_is_refused_by_its_name():
    """Un drapeau incompréhensible ne se replie pas sur « éteint » : le repli ferait passer une
    faute de frappe pour une décision, et l'exploitant croirait avoir allumé son moteur."""
    with pytest.raises(llm.MisconfiguredEngine, match="QUALIFICATION_LLM_ENABLED"):
        llm.engine_is_enabled({"QUALIFICATION_LLM_ENABLED": "peut-etre"})


# --------------------------------------------------------------------------
# Le démarrage éteint — sans une seule variable du moteur
# --------------------------------------------------------------------------


def test_the_application_starts_without_a_single_llm_setting(monkeypatch):
    """Le `with` n'est pas décoratif : c'est lui qui fait courir le démarrage de l'application, et
    donc lui qui met à l'épreuve ce que ce démarrage lit."""
    started = start_afresh(monkeypatch)

    with TestClient(started.app) as client:
        assert client.get("/health").status_code == 200


def test_importing_the_application_no_longer_reads_the_llm_settings(monkeypatch):
    """Allumé mais non configuré, l'import passe quand même : la lecture a lieu plus tard.

    C'est ce report qui rend l'extinction observable en test — sans lui, la suite se heurterait au
    refus de démarrer qu'elle est précisément là pour vérifier."""
    started = start_afresh(monkeypatch, enabled="true")

    assert started.app is not None


def test_an_engine_that_is_on_without_its_deadline_refuses_to_start(monkeypatch):
    """Le report n'est pas un renoncement : allumé, la configuration est lue **au démarrage** de
    l'application, et une configuration incomplète reste une panne bruyante — jamais une panne de
    qualification au premier appel."""
    started = start_afresh(monkeypatch, enabled="true")

    with pytest.raises(llm.MisconfiguredEngine):
        with TestClient(started.app):
            pass


def test_the_strict_order_of_the_two_deadlines_no_longer_applies_when_off(monkeypatch):
    """Éteint, il n'y a plus d'échéance à vérifier : une inégalité fausse n'empêche plus rien.

    C'est l'exploitant qui laisse ses réglages écrits pour rallumer plus tard, et à qui on ne
    demande pas de les recomposer aujourd'hui."""
    started = start_afresh(
        monkeypatch,
        environment={
            "QUALIFICATION_LLM_DEADLINE_SECONDS": "300",
            "QUALIFICATION_LLM_CALLER_DEADLINE_SECONDS": "30",
        },
    )

    with TestClient(started.app) as client:
        assert client.get("/health").status_code == 200


# --------------------------------------------------------------------------
# Le refus nommé — la route reste, la réponse dit pourquoi
# --------------------------------------------------------------------------


def test_the_llm_endpoint_names_the_absence_of_any_model(monkeypatch):
    started = start_afresh(monkeypatch)

    response = TestClient(started.app).post("/opinions/llm", json={"text": "Mes données ?"})

    assert response.status_code == 501
    assert response.headers["content-type"].startswith(PROBLEM_JSON)
    assert "modèle" in response.json()["detail"]


def test_the_refusal_is_never_dressed_up_as_an_opinion(monkeypatch):
    """Un avis vide serait lu comme « hors périmètre » — l'extinction doit rester une extinction."""
    started = start_afresh(monkeypatch)

    body = TestClient(started.app).post("/opinions/llm", json={"text": "Mes données ?"}).json()

    assert "rights" not in body


def test_the_route_is_kept_rather_than_removed(monkeypatch):
    """`501` et non `404` : le premier dit « ce déploiement ne sert aucun modèle », le second
    enverrait l'exploitant chercher une faute de frappe dans son URL."""
    started = start_afresh(monkeypatch)
    client = TestClient(started.app)

    disabled = client.post("/opinions/llm", json={"text": "Mes données ?"})
    unknown = client.post("/opinions/inconnu", json={"text": "Mes données ?"})

    assert disabled.status_code == 501
    assert unknown.status_code == 404


def test_no_call_ever_leaves_towards_the_upstream(monkeypatch):
    """Éteint, aucun aller-retour n'est payé : le refus tombe avant qu'un modèle soit même construit."""
    started = start_afresh(monkeypatch)

    with TestClient(started.app) as client:
        assert started.LLM_MODEL is None, "l'amont a été construit par un démarrage sans modèle"

        client.post("/opinions/llm", json={"text": "Mes données ?"})

    assert started.LLM_MODEL is None


def test_a_flag_gone_illegible_after_startup_still_answers_in_the_common_shape(monkeypatch):
    """Le démarrage a déjà refusé un drapeau illisible ; s'il en apparaît un ensuite, c'est que
    l'environnement a changé sous les pieds du processus — et cela sort quand même en `problem+json`,
    jamais en page d'erreur nue."""
    started = start_afresh(monkeypatch)
    client = TestClient(started.app)

    monkeypatch.setenv("QUALIFICATION_LLM_ENABLED", "peut-etre")
    response = client.post("/opinions/llm", json={"text": "Mes données ?"})

    assert response.status_code == 500
    assert response.headers["content-type"].startswith(PROBLEM_JSON)
    assert "peut-etre" in response.json()["detail"]


# --------------------------------------------------------------------------
# Le lexique et la santé, strictement inchangés
# --------------------------------------------------------------------------


def test_the_lexicon_is_served_exactly_as_before_by_a_deployment_without_a_model(monkeypatch):
    started = start_afresh(monkeypatch)

    response = TestClient(started.app).post(
        "/opinions/lexicon", json={"text": "Supprimez toutes mes données."}
    )

    assert response.status_code == 200
    assert response.json()["rights"] == ["Erasure"]


def test_the_health_probe_is_served_exactly_as_before(monkeypatch):
    """La supervision existante doit continuer de fonctionner sans être retouchée."""
    started = start_afresh(monkeypatch)

    response = TestClient(started.app).get("/health")

    assert response.status_code == 200
    assert response.json() == {"status": "healthy"}
