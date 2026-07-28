"""Le lexique rejoué sur le corpus témoin, **exemple par exemple**.

Ce n'est **pas** une mesure de la qualité de la qualification : aucun seuil, aucun F2, aucune
métrique agrégée n'apparaît ici, et il faut que cela reste vrai. Le seul chiffre disponible pour
poser un seuil serait emprunté à un autre moteur que celui qui sera servi.

C'est de la **non-régression de code**. Le lexique est déterministe : sa sortie est figée, exemple
par exemple, et toute divergence pose une question — « ce commit a changé le comportement du
lexique sur cet exemple, était-ce voulu ? ». Un exemple par test, pour que la réponse tienne dans
le nom du test qui a rougi plutôt que dans un compte global.

Quand l'écart est voulu : régénérer le fichier témoin, incrémenter `lexicon.ENGINE_VERSION`, et
faire relire le diff — il se lit exemple par exemple, c'est ce qu'achète le format JSONL.

    uv run python tests/witness/regenerate.py
"""

import pytest

from tests.corpus import qualify, read_corpus, read_witness

CORPUS = read_corpus()
WITNESS = read_witness()

REGENERATE = "Si l'écart est voulu : uv run python tests/witness/regenerate.py"


def test_the_witness_covers_exactly_the_corpus():
    assert sorted(WITNESS) == sorted(example.id for example in CORPUS), (
        "Le fichier témoin et le corpus ne portent pas les mêmes exemples — le corpus a bougé. "
        + REGENERATE
    )


@pytest.mark.parametrize("example", CORPUS, ids=[example.id for example in CORPUS])
def test_the_lexicon_still_qualifies_this_example_as_it_did(example):
    assert qualify(example.text) == WITNESS[example.id], REGENERATE
