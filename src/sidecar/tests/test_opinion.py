"""Les règles de refus de la frontière : le sidecar ne rend jamais un avis à moitié valide.

Soit un avis qui satisfait **tous** les invariants du domaine, soit une panne déclarée. Aucun
intermédiaire. L'exclusivité du hors périmètre n'étant pas exprimable en schéma JSON, elle est
vérifiée ici, en code — et une violation n'est pas un avis faible, c'est un moteur en panne.
"""

import pytest

from qualification_sidecar.opinion import (
    DeclaredConfidence,
    Engine,
    EngineFailure,
    Opinion,
    ReasonedOpinion,
)

ENGINE = Engine(name="lexicon", version="1.0.0")

LLM_ENGINE = Engine(name="llm", version="1.0.0+un-modele:8b")


def reasoned(**overrides):
    return ReasonedOpinion(
        **{
            "rights": ("Access",),
            "confidence": DeclaredConfidence.HIGH,
            "justification": "Le texte réclame une copie des données.",
            "engine": LLM_ENGINE,
            **overrides,
        }
    )


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


# --------------------------------------------------------------------------
# L'avis raisonné — mêmes invariants, plus ce que seul le LLM apporte
# --------------------------------------------------------------------------


@pytest.mark.parametrize(
    ("failure", "rights"),
    [
        ("liste vide", ()),
        ("hors de la taxonomie", ("Deletion",)),
        ("slug français non traduit", ("effacement",)),
        ("hors périmètre accompagné", ("OutOfScope", "Erasure")),
        ("droit rendu deux fois", ("Access", "Access")),
    ],
)
def test_the_domain_invariants_bind_the_reasoned_opinion_exactly_as_the_other(failure, rights):
    """Les mêmes invariants des deux côtés : c'est ce que promet « jamais un avis à moitié valide »."""
    with pytest.raises(EngineFailure):
        reasoned(rights=rights)


def test_a_reasoned_opinion_declares_a_confidence_and_justifies_itself():
    opinion = reasoned()

    assert opinion.confidence is DeclaredConfidence.HIGH
    assert opinion.justification


def test_the_confidence_is_mandatory_here_where_the_lexicon_may_not_have_one():
    with pytest.raises(ValueError):
        ReasonedOpinion(rights=("Access",), justification="Parce que.", engine=LLM_ENGINE)


def test_a_confidence_outside_the_three_degrees_is_refused():
    with pytest.raises(ValueError):
        reasoned(confidence="Certain")


def test_the_scale_has_exactly_three_degrees():
    """Ordinale et fermée : un quatrième degré ferait mentir tout ce qui compare deux avis."""
    assert [degree.value for degree in DeclaredConfidence] == ["High", "Medium", "Low"]


def test_a_justification_is_mandatory_and_never_empty():
    """Un avis sans raison priverait l'opérateur humain du seul texte sur lequel il relit."""
    with pytest.raises(ValueError):
        reasoned(justification="")


def test_a_reasoned_opinion_carries_nothing_else():
    assert set(reasoned().model_dump()) == {"rights", "confidence", "justification", "engine"}
