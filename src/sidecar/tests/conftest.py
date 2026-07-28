"""La configuration du moteur LLM, posée **avant** que quoi que ce soit n'importe le sidecar.

Le sidecar refuse de démarrer sans configuration : c'est délibéré, et c'est ce qui garantit qu'aucun
`base_url` ni nom de modèle ne survit en dur dans le code. La contrepartie est ici — la suite doit
la fournir, et `conftest` est le seul endroit importé assez tôt pour cela.

Les valeurs sont volontairement **dépaysantes** : ni `qwen3:8b`, ni le port d'Ollama. Un test qui
passerait avec les vraies valeurs de production sans les lire ne prouverait rien ; celui qui passe
avec celles-ci prouve que la configuration a bien été traversée.

Aucun de ces réglages n'ouvre de connexion : l'amont réel n'est jamais joint, chaque test qui a
besoin d'un modèle lui substituant le faux de [`upstream.py`](upstream.py).
"""

import os

os.environ.setdefault("QUALIFICATION_LLM_BASE_URL", "http://modele-de-test.invalid/v1")
os.environ.setdefault("QUALIFICATION_LLM_MODEL", "modele-de-test:0b")
os.environ.setdefault("QUALIFICATION_LLM_API_KEY", "cle-inutilisee")
os.environ.setdefault("QUALIFICATION_LLM_TEMPERATURE", "0")
os.environ.setdefault("QUALIFICATION_LLM_SEED", "1789")
os.environ.setdefault("QUALIFICATION_LLM_DEADLINE_SECONDS", "20")
os.environ.setdefault("QUALIFICATION_LLM_CALLER_DEADLINE_SECONDS", "30")

# Importés *après* la configuration ci-dessus : `llm` la lit à l'import, et la suite serait sinon
# la première à se heurter au refus de démarrer qu'elle est précisément là pour vérifier.
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
