"""Le garde-fou anti-dérive, vu du côté Python.

Symétrique du test unitaire .NET `WireTaxonomyProjectionTests` : celui-là confronte le fichier de
projection au domaine, celui-ci confronte la taxonomie que le sidecar connaît au même fichier.
Les deux ensemble ferment la boucle — le domaine commande, le fichier suit, Python lit.

Si un de ces tests échoue, ce n'est pas le fichier qu'il faut éditer : c'est la table de
correspondance du sidecar qui a pris du retard sur le domaine.
"""

import json

import pytest

from qualification_sidecar import taxonomy


def _fake_repository(root, projection):
    """Un dépôt de pacotille : sa solution, et une projection qui dit ce que le test veut lui faire dire."""
    (root / taxonomy.REPOSITORY_MARKER).write_text("", encoding="utf-8")

    if projection is not None:
        (root / taxonomy.WIRE_TAXONOMY_FILE).write_text(
            json.dumps({"rights": projection}), encoding="utf-8"
        )

    return root


def test_python_knows_exactly_the_seven_projected_names():
    assert set(taxonomy.CANONICAL_NAME_BY_SLUG.values()) == set(taxonomy.PROJECTED_RIGHTS)


def test_the_projection_carries_seven_names():
    assert len(taxonomy.PROJECTED_RIGHTS) == 7


def test_each_slug_maps_to_a_distinct_canonical_name():
    names = list(taxonomy.CANONICAL_NAME_BY_SLUG.values())

    assert len(set(names)) == len(names)


def test_out_of_scope_is_one_of_the_projected_names():
    assert taxonomy.OUT_OF_SCOPE in taxonomy.PROJECTED_RIGHTS


def test_a_name_the_sidecar_ignores_is_a_divergence():
    with pytest.raises(taxonomy.WireTaxonomyDivergence):
        taxonomy.ensure_no_divergence(known={"Access"}, projected={"Access", "Erasure"})


def test_a_name_the_projection_ignores_is_a_divergence():
    with pytest.raises(taxonomy.WireTaxonomyDivergence):
        taxonomy.ensure_no_divergence(known={"Access", "Deletion"}, projected={"Access"})


def test_no_divergence_when_both_sides_coincide():
    taxonomy.ensure_no_divergence(known={"Access"}, projected={"Access"})


def test_a_slug_the_sidecar_cannot_translate_is_a_divergence():
    with pytest.raises(taxonomy.WireTaxonomyDivergence):
        taxonomy.to_canonical("acces-partiel")


# --------------------------------------------------------------------------
# Ce que le sidecar fait à l'import — les deux appels ci-dessous sont exactement ceux qui
# s'exécutent au démarrage. Ce qui les fait lever empêche donc le sidecar de démarrer.
# --------------------------------------------------------------------------

def _start_up():
    taxonomy.ensure_no_divergence(
        known=set(taxonomy.CANONICAL_NAME_BY_SLUG.values()),
        projected=taxonomy.read_projection(),
    )


@pytest.fixture
def repository(tmp_path, monkeypatch):
    monkeypatch.setattr(taxonomy, "repository_root", lambda: tmp_path)
    return tmp_path


def test_a_projection_diverging_from_the_sidecar_stops_the_start_up(repository):
    _fake_repository(repository, projection=["Access", "Erasure", "Deletion"])

    with pytest.raises(taxonomy.WireTaxonomyDivergence):
        _start_up()


def test_an_absent_projection_stops_the_start_up(repository):
    _fake_repository(repository, projection=None)

    with pytest.raises(taxonomy.WireTaxonomyUnavailable):
        _start_up()


def test_a_projection_repeating_a_name_stops_the_start_up(repository):
    _fake_repository(repository, projection=[*taxonomy.PROJECTED_RIGHTS, "Access"])

    with pytest.raises(taxonomy.WireTaxonomyUnavailable):
        _start_up()


def test_the_projection_of_the_repository_itself_lets_the_sidecar_start(repository):
    _fake_repository(repository, projection=sorted(taxonomy.PROJECTED_RIGHTS))

    _start_up()
