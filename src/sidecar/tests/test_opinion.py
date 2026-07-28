"""Les règles de refus de la frontière : le sidecar ne rend jamais un avis à moitié valide.

Soit un avis qui satisfait **tous** les invariants du domaine, soit une panne déclarée. Aucun
intermédiaire. L'exclusivité du hors périmètre n'étant pas exprimable en schéma JSON, elle est
vérifiée ici, en code — et une violation n'est pas un avis faible, c'est un moteur en panne.
"""

import pytest

from qualification_sidecar.opinion import Engine, EngineFailure, Opinion

ENGINE = Engine(name="lexicon", version="1.0.0")


def test_an_opinion_carrying_a_recognised_right_is_valid():
    assert Opinion(rights=("Access",), engine=ENGINE).rights == ("Access",)


def test_an_opinion_carrying_several_rights_is_valid():
    assert len(Opinion(rights=("Access", "Erasure"), engine=ENGINE).rights) == 2


def test_an_empty_list_of_rights_is_an_engine_failure():
    with pytest.raises(EngineFailure):
        Opinion(rights=(), engine=ENGINE)


def test_a_right_outside_the_taxonomy_is_an_engine_failure():
    with pytest.raises(EngineFailure):
        Opinion(rights=("Deletion",), engine=ENGINE)


def test_a_french_slug_left_untranslated_is_an_engine_failure():
    with pytest.raises(EngineFailure):
        Opinion(rights=("effacement",), engine=ENGINE)


def test_out_of_scope_accompanied_by_a_right_is_an_engine_failure():
    with pytest.raises(EngineFailure):
        Opinion(rights=("OutOfScope", "Erasure"), engine=ENGINE)


def test_out_of_scope_alone_is_valid():
    assert Opinion(rights=("OutOfScope",), engine=ENGINE).rights == ("OutOfScope",)


def test_a_right_rendered_twice_is_an_engine_failure():
    with pytest.raises(EngineFailure):
        Opinion(rights=("Access", "Access"), engine=ENGINE)


def test_an_opinion_carries_no_confidence():
    assert "confidence" not in Opinion(rights=("Access",), engine=ENGINE).model_dump()


def test_an_opinion_refuses_a_confidence_it_was_handed():
    with pytest.raises(ValueError):
        Opinion(rights=("Access",), engine=ENGINE, confidence="High")
