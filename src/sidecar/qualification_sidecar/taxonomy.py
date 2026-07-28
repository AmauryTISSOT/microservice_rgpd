"""La taxonomie fermée de sept valeurs, vue du côté Python.

Le vocabulaire du fil est en **noms canoniques anglais** — exactement les membres du `SmartEnum`
`DataSubjectRight` du domaine .NET. Le français est une contrainte *locale aux moteurs* : le
lexique raisonne sur des slugs français, le prompt du LLM parlera français, et chacun traduit
juste avant de répondre.

**Le sidecar ne garde aucune copie de la taxonomie.** Il lit à l'import le fichier de projection
versionné à la racine du dépôt, et confronte la table de correspondance ci-dessous à ce qu'il y
lit. Une divergence n'est pas une dégradation silencieuse : elle **empêche le démarrage**, parce
qu'un sidecar qui parle un autre alphabet que le service qu'il sert ne rend pas des avis un peu
faux — il rend des avis inexploitables.
"""

from __future__ import annotations

import json
from collections.abc import Collection, Mapping
from pathlib import Path
from typing import Final

WIRE_TAXONOMY_FILE: Final = "data-subject-rights.wire.json"

REPOSITORY_MARKER: Final = "MicroserviceRgpd.slnx"


class WireTaxonomyError(RuntimeError):
    """Le sidecar n'est pas en état de parler la taxonomie du service.

    Levée à l'import — donc au démarrage —, jamais rattrapée pour rendre un avis quand même.
    """


class WireTaxonomyUnavailable(WireTaxonomyError):
    """La projection est illisible : dépôt introuvable, fichier absent, contenu incohérent.

    Ce n'est pas un désaccord sur la taxonomie, c'est l'impossibilité d'en constater un — et les
    confondre ferait chercher une divergence là où c'est l'installation qui est en cause.
    """


class WireTaxonomyDivergence(WireTaxonomyError):
    """La taxonomie du sidecar et celle de la projection ne coïncident pas."""


#: Le nom canonique anglais de chaque slug français des moteurs. Les slugs sont ceux du corpus et
#: du prototype ; ils ne sortent jamais du sidecar.
CANONICAL_NAME_BY_SLUG: Final[Mapping[str, str]] = {
    "acces": "Access",
    "rectification": "Rectification",
    "effacement": "Erasure",
    "limitation": "Restriction",
    "portabilite": "Portability",
    "opposition": "Objection",
    "hors-perimetre": "OutOfScope",
}

#: Le verdict exclusif, sous son nom de fil. Nommé ici parce que l'invariant I2 le désigne.
OUT_OF_SCOPE: Final = "OutOfScope"


def repository_root() -> Path:
    """Remonte jusqu'au dépôt, repéré par sa solution — jamais un chemin relatif au répertoire courant.

    Le sidecar est démarré tantôt par l'`AppHost`, tantôt à la main, tantôt par `pytest` : seul le
    chemin de ce fichier est un point fixe.
    """
    for directory in Path(__file__).resolve().parents:
        if (directory / REPOSITORY_MARKER).is_file():
            return directory

    raise WireTaxonomyUnavailable(
        f"Racine du dépôt introuvable depuis {__file__} : "
        f"aucun {REPOSITORY_MARKER} en remontant, donc aucune projection à lire."
    )


def read_projection() -> frozenset[str]:
    """Lit les noms projetés sur le fil, ou refuse — un fichier illisible n'est pas une taxonomie vide."""
    path = repository_root() / WIRE_TAXONOMY_FILE

    if not path.is_file():
        raise WireTaxonomyUnavailable(f"Le fichier de projection est introuvable : {path}")

    names = json.loads(path.read_text(encoding="utf-8"))["rights"]

    if len(set(names)) != len(names):
        raise WireTaxonomyUnavailable(f"{WIRE_TAXONOMY_FILE} projette un nom en double : {names}")

    return frozenset(names)


def ensure_no_divergence(known: Collection[str], projected: Collection[str]) -> None:
    """Refuse tout écart, dans les deux sens.

    Le message nomme le sens de l'écart : c'est ce qui évite qu'on fasse taire l'erreur en éditant
    le fichier de projection, alors que c'est le domaine qui commande.
    """
    missing = sorted(set(projected) - set(known))
    unknown = sorted(set(known) - set(projected))

    if missing:
        raise WireTaxonomyDivergence(
            f"Le sidecar ignore des droits que {WIRE_TAXONOMY_FILE} projette : {', '.join(missing)}. "
            "Reportez-les dans sa table de correspondance."
        )

    if unknown:
        raise WireTaxonomyDivergence(
            f"Le sidecar connaît des droits absents de {WIRE_TAXONOMY_FILE} : {', '.join(unknown)}. "
            "N'éditez pas le fichier pour faire taire ce démarrage : corrigez le domaine, puis reportez."
        )


#: Les sept noms lus dans la projection. Source unique du côté Python : rien d'autre ne les énumère.
PROJECTED_RIGHTS: Final[frozenset[str]] = read_projection()

ensure_no_divergence(known=set(CANONICAL_NAME_BY_SLUG.values()), projected=PROJECTED_RIGHTS)


def to_canonical(slug: str) -> str:
    """Traduit un slug de moteur en nom de fil, ou refuse.

    Un slug inconnu ne rend pas un avis approximatif : il signale qu'un moteur a produit une valeur
    hors de la taxonomie, ce qui est une panne de ce moteur.
    """
    try:
        return CANONICAL_NAME_BY_SLUG[slug]
    except KeyError:
        raise WireTaxonomyDivergence(
            f"Un moteur a rendu « {slug} », qui n'est pas un droit de la taxonomie."
        ) from None
