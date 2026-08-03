"""La configuration du moteur LLM, posée **avant** que quoi que ce soit n'importe le sidecar.

Le moteur LLM est **éteint par défaut** : la suite l'allume ici, parce que la quasi-totalité de ce
qu'elle exerce est le sidecar qui sert un modèle. Le déploiement éteint, lui, se vérifie dans
[`test_disabled_llm.py`](test_disabled_llm.py), qui redémarre l'application environnement vidé.

Le sidecar allumé refuse de démarrer sans configuration : c'est délibéré, et c'est ce qui garantit
qu'aucun `base_url` ni nom de modèle ne survit en dur dans le code. La contrepartie est ici — la
suite doit la fournir, et `conftest` est le seul endroit importé assez tôt pour cela.

Les valeurs sont volontairement **dépaysantes** : ni `qwen3:8b`, ni le port d'Ollama. Un test qui
passerait avec les vraies valeurs de production sans les lire ne prouverait rien ; celui qui passe
avec celles-ci prouve que la configuration a bien été traversée.

Aucun de ces réglages n'ouvre de connexion : l'amont réel n'est jamais joint, chaque test qui a
besoin d'un modèle lui substituant le faux de [`upstream.py`](upstream.py).
"""

import os

os.environ.setdefault("QUALIFICATION_LLM_ENABLED", "true")
os.environ.setdefault("QUALIFICATION_LLM_BASE_URL", "http://modele-de-test.invalid/v1")
os.environ.setdefault("QUALIFICATION_LLM_MODEL", "modele-de-test:0b")
os.environ.setdefault("QUALIFICATION_LLM_API_KEY", "cle-inutilisee")
os.environ.setdefault("QUALIFICATION_LLM_TEMPERATURE", "0")
os.environ.setdefault("QUALIFICATION_LLM_SEED", "1789")
os.environ.setdefault("QUALIFICATION_LLM_DEADLINE_SECONDS", "20")
os.environ.setdefault("QUALIFICATION_LLM_CALLER_DEADLINE_SECONDS", "30")

# Importés *après* la configuration ci-dessus : bien que le moteur ne lise plus ses réglages à
# l'import, les tests qui démarrent l'application allumée les exigent, et les poser ici est ce qui
# garantit qu'ils y sont quel que soit l'ordre de collecte.
import pytest  # noqa: E402

from tests.upstream import FakeModel  # noqa: E402


@pytest.fixture
def upstream(monkeypatch):
    """Substitue l'amont du point d'entrée LLM, et rend de quoi le régler test par test."""
    from qualification_sidecar import app as app_module

    def substitute(**kwargs):
        model = FakeModel(**kwargs)
        monkeypatch.setattr(app_module, "LLM_MODEL", model)
        return model

    return substitute
