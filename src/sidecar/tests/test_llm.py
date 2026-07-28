"""Le moteur LLM sans son amont : configuration, lecture du verdict, traduction, pannes nommées.

**Aucun test n'appelle Ollama.** Un test qui exige un GPU est un test qui ne tourne jamais, et un
test qui ne tourne jamais ment. Ce que l'on couvre ici est donc tout ce qui n'est pas le modèle :
ce qu'on lui envoie, ce qu'on accepte de lui, et ce qu'on fait de ses silences.
"""

import asyncio
import json

import httpx
import openai
import pytest

from qualification_sidecar import llm
from qualification_sidecar.opinion import DeclaredConfidence, EngineFailure

SETTINGS = {
    "QUALIFICATION_LLM_BASE_URL": "http://ailleurs.invalid/v1",
    "QUALIFICATION_LLM_MODEL": "un-autre-modele:3b",
    "QUALIFICATION_LLM_API_KEY": "cle",
    "QUALIFICATION_LLM_TEMPERATURE": "0",
    "QUALIFICATION_LLM_SEED": "1789",
    "QUALIFICATION_LLM_DEADLINE_SECONDS": "20",
    "QUALIFICATION_LLM_CALLER_DEADLINE_SECONDS": "30",
}

VALID_VERDICT = {
    "droits": ["acces", "effacement"],
    "justification": "Le texte demande une copie des données et leur suppression.",
    "confiance": "haute",
}


class FakeModel:
    """Un amont qui rend ce qu'on lui a dit de rendre, et note ce qu'on lui a demandé."""

    def __init__(self, content, served_model="modele-servi:7b"):
        self._content = content if isinstance(content, str) else json.dumps(content)
        self._served_model = served_model
        self.system = None
        self.user = None

    async def answer(self, *, system, user):
        self.system, self.user = system, user
        return llm.ModelAnswer(content=self._content, served_model=self._served_model)


def qualify(content, served_model="modele-servi:7b", text="Supprimez mes données."):
    return asyncio.run(llm.qualify(text, FakeModel(content, served_model)))


# --------------------------------------------------------------------------
# Configuration — aucun chiffre en dur
# --------------------------------------------------------------------------


def test_every_setting_is_read_from_the_environment():
    settings = llm.LlmSettings.from_environment(SETTINGS)

    assert settings.base_url == "http://ailleurs.invalid/v1"
    assert settings.model == "un-autre-modele:3b"
    assert settings.temperature == 0
    assert settings.seed == 1789
    assert settings.deadline_seconds == 20


@pytest.mark.parametrize("absent", sorted(SETTINGS))
def test_an_absent_setting_prevents_the_engine_from_existing(absent):
    incomplete = {name: value for name, value in SETTINGS.items() if name != absent}

    with pytest.raises(llm.MisconfiguredEngine, match=absent):
        llm.LlmSettings.from_environment(incomplete)


def test_a_setting_that_is_not_a_number_is_refused_by_name():
    with pytest.raises(llm.MisconfiguredEngine, match="SEED"):
        llm.LlmSettings.from_environment(SETTINGS | {"QUALIFICATION_LLM_SEED": "bientot"})


@pytest.mark.parametrize("sidecar_deadline", ["30", "31"])
def test_a_deadline_not_shorter_than_the_callers_is_refused(sidecar_deadline):
    """L'inégalité stricte est ce qui fait arriver la lenteur *nommée* plutôt qu'anonyme."""
    with pytest.raises(llm.MisconfiguredEngine, match="strictement plus courte"):
        llm.LlmSettings.from_environment(
            SETTINGS | {"QUALIFICATION_LLM_DEADLINE_SECONDS": sidecar_deadline}
        )


def test_a_deadline_that_is_not_positive_is_refused():
    with pytest.raises(llm.MisconfiguredEngine):
        llm.LlmSettings.from_environment(SETTINGS | {"QUALIFICATION_LLM_DEADLINE_SECONDS": "0"})


# --------------------------------------------------------------------------
# Bascule de fournisseur — `base_url` et nom de modèle, rien d'autre
# --------------------------------------------------------------------------


def test_the_upstream_is_addressed_where_the_configuration_says():
    model = llm.OpenAiCompatibleModel(llm.LlmSettings.from_environment(SETTINGS))

    assert str(model._client.base_url).startswith("http://ailleurs.invalid/v1")


def test_no_retry_is_ever_attempted_towards_the_upstream():
    model = llm.OpenAiCompatibleModel(llm.LlmSettings.from_environment(SETTINGS))

    assert model._client.max_retries == 0


def test_the_deadline_carried_to_the_client_is_the_configured_one():
    model = llm.OpenAiCompatibleModel(llm.LlmSettings.from_environment(SETTINGS))

    assert model._client.timeout == 20


def test_the_configured_model_temperature_and_seed_are_what_travels(monkeypatch):
    """Ce que le sidecar demande à l'amont vient de la configuration — pas d'une constante du code."""
    model = llm.OpenAiCompatibleModel(llm.LlmSettings.from_environment(SETTINGS))
    asked = {}

    async def create(**kwargs):
        asked.update(kwargs)
        return openai.types.chat.ChatCompletion.model_validate(
            {
                "id": "1",
                "object": "chat.completion",
                "created": 0,
                "model": "un-autre-modele:3b",
                "choices": [
                    {
                        "index": 0,
                        "finish_reason": "stop",
                        "message": {"role": "assistant", "content": json.dumps(VALID_VERDICT)},
                    }
                ],
            }
        )

    monkeypatch.setattr(model._client.chat.completions, "create", create)

    answer = asyncio.run(model.answer(system="consigne", user="texte"))

    assert asked["model"] == "un-autre-modele:3b"
    assert asked["temperature"] == 0
    assert asked["seed"] == 1789
    assert asked["response_format"]["json_schema"]["schema"]["properties"]["droits"]["minItems"] == 1
    assert json.loads(answer.content) == VALID_VERDICT


@pytest.mark.parametrize(
    ("raised", "named"),
    [
        (openai.APITimeoutError(request=httpx.Request("POST", "http://x")), llm.ModelTooSlow),
        (
            openai.APIConnectionError(message="refusée", request=httpx.Request("POST", "http://x")),
            llm.ModelUnreachable,
        ),
    ],
)
def test_a_slow_upstream_and_an_absent_one_are_two_different_failures(monkeypatch, raised, named):
    """`APITimeoutError` dérivant d'`APIConnectionError`, l'ordre des `except` fait la distinction."""
    model = llm.OpenAiCompatibleModel(llm.LlmSettings.from_environment(SETTINGS))

    async def create(**kwargs):
        raise raised

    monkeypatch.setattr(model._client.chat.completions, "create", create)

    with pytest.raises(named):
        asyncio.run(model.answer(system="consigne", user="texte"))


def test_a_completion_without_content_is_an_absent_upstream(monkeypatch):
    model = llm.OpenAiCompatibleModel(llm.LlmSettings.from_environment(SETTINGS))

    async def create(**kwargs):
        return openai.types.chat.ChatCompletion.model_validate(
            {
                "id": "1",
                "object": "chat.completion",
                "created": 0,
                "model": "un-autre-modele:3b",
                "choices": [],
            }
        )

    monkeypatch.setattr(model._client.chat.completions, "create", create)

    with pytest.raises(llm.ModelUnreachable):
        asyncio.run(model.answer(system="consigne", user="texte"))


# --------------------------------------------------------------------------
# Lecture du verdict — ce que le schéma ne sait pas dire, le code le refuse
# --------------------------------------------------------------------------


def test_a_well_formed_verdict_is_read_as_the_model_rendered_it():
    verdict = qualify(VALID_VERDICT)

    assert verdict.slugs == ("acces", "effacement")
    assert verdict.confidence_slug == "haute"
    assert verdict.justification.startswith("Le texte demande")


def test_the_verdict_keeps_the_french_slugs_untranslated():
    """La traduction a lieu à la frontière HTTP, pas ici : le français est local au moteur."""
    assert qualify(VALID_VERDICT).slugs == ("acces", "effacement")


def test_the_model_receives_the_prompt_and_the_text_it_must_qualify():
    fake = FakeModel(VALID_VERDICT)

    asyncio.run(llm.qualify("Effacez tout.", fake))

    assert "article 17" in fake.system
    assert fake.user == "Effacez tout."


@pytest.mark.parametrize(
    ("failure", "content"),
    [
        ("pas du JSON", "je préfère répondre en prose"),
        ("un tableau au lieu d'un objet", "[]"),
        ("droits absent", {"justification": "j", "confiance": "haute"}),
        ("droits vide", {"droits": [], "justification": "j", "confiance": "haute"}),
        ("droits non liste", {"droits": "acces", "justification": "j", "confiance": "haute"}),
        ("droits non textuels", {"droits": [15], "justification": "j", "confiance": "haute"}),
        ("confiance absente", {"droits": ["acces"], "justification": "j"}),
        (
            "confiance hors échelle",
            {"droits": ["acces"], "justification": "j", "confiance": "certaine"},
        ),
        (
            "confiance chiffrée",
            {"droits": ["acces"], "justification": "j", "confiance": 0.9},
        ),
        ("justification absente", {"droits": ["acces"], "confiance": "haute"}),
        (
            "justification vide",
            {"droits": ["acces"], "justification": "   ", "confiance": "haute"},
        ),
    ],
)
def test_anything_that_is_not_an_opinion_is_an_engine_failure(failure, content):
    with pytest.raises(EngineFailure):
        qualify(content)


def test_a_failure_names_what_the_model_rendered():
    """Le diagnostic doit dire *quoi*, sinon il envoie relire un prompt de cent lignes au hasard."""
    with pytest.raises(EngineFailure, match="confiance"):
        qualify({"droits": ["acces"], "justification": "j", "confiance": "certaine"})


# --------------------------------------------------------------------------
# Traduction — du français du moteur aux noms du fil
# --------------------------------------------------------------------------


@pytest.mark.parametrize(
    ("slug", "canonical"),
    [
        ("haute", DeclaredConfidence.HIGH),
        ("moyenne", DeclaredConfidence.MEDIUM),
        ("basse", DeclaredConfidence.LOW),
    ],
)
def test_each_declared_degree_has_its_canonical_name(slug, canonical):
    assert llm.to_canonical_confidence(slug) is canonical


def test_the_three_degrees_are_the_only_ones():
    assert set(llm.CANONICAL_CONFIDENCE_BY_SLUG.values()) == set(DeclaredConfidence)


def test_an_unknown_degree_is_never_folded_back_onto_a_known_one():
    """Un repli inventerait une auto-évaluation que le modèle n'a pas rendue."""
    with pytest.raises(EngineFailure):
        llm.to_canonical_confidence("certaine")


def test_what_the_prompt_allows_and_what_the_engine_translates_coincide():
    """Les mêmes degrés des deux côtés, sous peine d'un mot demandé au modèle puis refusé de lui."""
    from qualification_sidecar.prompt import CONFIDENCE_SLUGS, RIGHT_SLUGS
    from qualification_sidecar.taxonomy import CANONICAL_NAME_BY_SLUG

    assert set(llm.CANONICAL_CONFIDENCE_BY_SLUG) == set(CONFIDENCE_SLUGS)
    assert set(RIGHT_SLUGS) == set(CANONICAL_NAME_BY_SLUG)


# --------------------------------------------------------------------------
# Version du moteur — elle identifie le modèle réellement servi
# --------------------------------------------------------------------------


def test_the_engine_version_names_the_model_actually_served():
    assert "modele-servi:7b" in llm.engine_version(qualify(VALID_VERDICT).served_model)


def test_the_engine_version_also_carries_the_version_of_its_prompt():
    """Deux qualifications rendues sous deux consignes différentes ne sont pas comparables."""
    from qualification_sidecar.prompt import PROMPT_VERSION

    assert llm.engine_version("un-modele:8b") == f"un-modele:8b+prompt.{PROMPT_VERSION}"


def test_the_served_model_prevails_over_the_one_that_was_asked_for():
    assert qualify(VALID_VERDICT, served_model="autre-chose:1b").served_model == "autre-chose:1b"
