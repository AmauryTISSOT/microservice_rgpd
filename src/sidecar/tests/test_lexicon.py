"""Ce que le lexique doit tenir, quoi qu'il qualifie.

Ces tests ne mesurent **pas** la qualité de la qualification — aucun seuil, aucune métrique :
c'est explicitement hors périmètre. Ils épinglent le contrat du moteur, celui dont dépend la
frontière du sidecar : une liste non vide, des slugs qu'il sait traduire, un hors périmètre
exclusif.
"""

import pytest

from qualification_sidecar import lexicon, taxonomy

TEXTS = [
    "Bonjour, je souhaite exercer mon droit d'accès à mes données personnelles.",
    "Supprimez toutes mes données, je ne veux plus rien avoir à faire avec vous.",
    "Ma commande n'est jamais arrivée, je veux être remboursé.",
    "🙂",
    "?",
    "Bonjour",
]


@pytest.mark.parametrize("text", TEXTS)
def test_the_lexicon_never_renders_an_empty_list(text):
    assert lexicon.qualify(text) != []


@pytest.mark.parametrize("text", TEXTS)
def test_the_lexicon_only_renders_slugs_it_can_translate(text):
    for slug in lexicon.qualify(text):
        assert slug in taxonomy.CANONICAL_NAME_BY_SLUG


@pytest.mark.parametrize("text", TEXTS)
def test_out_of_scope_never_comes_accompanied(text):
    rights = lexicon.qualify(text)

    if lexicon.OUT_OF_SCOPE in rights:
        assert rights == [lexicon.OUT_OF_SCOPE]


def test_a_text_foreign_to_the_rgpd_falls_out_of_scope():
    assert lexicon.qualify("Quel est le tarif de votre offre premium ?") == [lexicon.OUT_OF_SCOPE]


def test_an_explicit_access_request_is_recognised():
    rights = lexicon.qualify(
        "Conformément à l'article 15 du RGPD, communiquez-moi une copie de mes données."
    )

    assert rights == ["acces"]


def test_the_engine_says_its_name_and_version():
    assert lexicon.ENGINE_NAME == "lexicon"
    assert lexicon.ENGINE_VERSION
