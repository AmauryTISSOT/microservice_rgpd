"""L'avis rendu par un moteur, et la seule forme sous laquelle il peut quitter le sidecar.

**Le sidecar ne rend jamais un avis à moitié valide.** Soit un avis satisfaisant tous les
invariants du domaine — liste jamais vide, hors périmètre exclusif de tout autre droit,
appartenance stricte aux sept valeurs de la projection —, soit un code non-2xx.

Les invariants sont portés par le type lui-même, non par une vérification que l'appelant pourrait
oublier : construire un avis invalide est impossible. C'est la contrepartie de la promesse faite à
l'appelant .NET, qui ne demandera jamais « cet avis est-il bien formé ? ».
"""

from __future__ import annotations

from collections.abc import Sequence
from enum import StrEnum

from pydantic import BaseModel, ConfigDict, Field, model_validator

from qualification_sidecar.taxonomy import OUT_OF_SCOPE, PROJECTED_RIGHTS


class EngineFailure(RuntimeError):
    """Un moteur a produit quelque chose qui n'est pas un avis.

    **Un avis invalide n'est pas un avis faible, c'est une panne du moteur**, et il se présente
    comme telle : non-2xx, jamais un `200` dégradé que l'appelant devrait interpréter.
    """


class Engine(BaseModel):
    """L'identité du moteur qui a parlé — ce qui rend une régression interprétable plus tard."""

    model_config = ConfigDict(frozen=True, extra="forbid")

    name: str
    version: str


class Opinion(BaseModel):
    """L'avis lexical : des droits, et le moteur qui les a rendus.

    Aucune confiance : le lexique n'a pas d'avis sur sa propre fiabilité, et une constante lui en
    donnerait l'apparence. Aucune justification non plus : sa « raison » est une table de scores,
    qui est du diagnostic, pas de l'aide à la décision.
    """

    model_config = ConfigDict(frozen=True, extra="forbid")

    rights: tuple[str, ...]
    engine: Engine

    @model_validator(mode="after")
    def _honour_the_domain_invariants(self) -> Opinion:
        ensure_valid_qualification(self.rights)
        return self


class DeclaredConfidence(StrEnum):
    """L'échelle **ordinale à trois degrés** par laquelle un moteur dit à quel point il doute de lui.

    Ordinale, donc fermée : trois degrés nommés, et surtout pas un nombre. Un pourcentage inviterait
    à des seuils fins que rien ne fonde, là où le service n'a besoin que de trier une file de
    relecture. Les noms sont ceux du fil — anglais canoniques —, le moteur traduisant les siens
    juste avant de répondre.
    """

    HIGH = "High"
    MEDIUM = "Medium"
    LOW = "Low"


class ReasonedOpinion(BaseModel):
    """L'avis d'un moteur qui sait douter de lui-même et dire pourquoi il a tranché.

    Distinct d'`Opinion`, et pas une version enrichie de celle-ci : la confiance et la
    justification sont **obligatoires ici et interdites là-bas**. Les fondre en un seul type à
    champs optionnels rendrait exprimable un avis lexical assorti d'une confiance — exactement ce
    que le domaine interdit.

    La justification est en français : c'est l'unique texte du sidecar destiné à un humain, et
    l'opérateur qui relit la qualification lit le français. Le fil reste anglais pour tout le
    reste — les noms de champs comme les droits.

    Elle fait partie du contrat **du LLM seulement** : le lexique n'en rend pas, sa « raison » étant
    une table de scores, qui est du diagnostic et non de l'aide à la décision.
    """

    model_config = ConfigDict(frozen=True, extra="forbid")

    rights: tuple[str, ...]
    confidence: DeclaredConfidence
    justification: str = Field(min_length=1)
    engine: Engine

    @model_validator(mode="after")
    def _honour_the_domain_invariants(self) -> ReasonedOpinion:
        ensure_valid_qualification(self.rights)
        return self


def ensure_valid_qualification(rights: Sequence[str]) -> None:
    """Vérifie en code ce qu'aucun schéma JSON ne sait exprimer, et refuse le reste.

    Chaque écart est nommé séparément : « le moteur a rendu une liste vide » et « le moteur a
    accompagné le hors périmètre » sont deux pannes différentes, et les confondre priverait le
    diagnostic de ce qui le rend utile.
    """
    if not rights:
        raise EngineFailure(
            "Un moteur a rendu une liste de droits vide : l'absence de droit reconnu s'écrit "
            f"« {OUT_OF_SCOPE} », elle ne se déduit pas d'un silence."
        )

    unknown = [right for right in rights if right not in PROJECTED_RIGHTS]
    if unknown:
        raise EngineFailure(
            f"Un moteur a rendu des valeurs hors de la taxonomie : {', '.join(unknown)}."
        )

    if len(set(rights)) != len(rights):
        raise EngineFailure(f"Un moteur a rendu deux fois le même droit : {', '.join(rights)}.")

    if OUT_OF_SCOPE in rights and len(rights) > 1:
        raise EngineFailure(
            f"Un moteur a accompagné « {OUT_OF_SCOPE} » d'un autre droit, alors qu'il est exclusif : "
            f"{', '.join(rights)}."
        )
