"""Le moteur qui rend le verdict : un LLM local, interrogé par un protocole qui n'engage personne.

Ce moteur a ce que le lexique n'a pas — une **confiance déclarée** et une **justification en
français** — et il a ce que le lexique n'a pas non plus : un **amont**. C'est cet amont qui lui vaut
une palette de codes plus riche, et **trois** pannes qu'il faut surtout ne pas confondre, parce
qu'elles ne se réparent pas au même endroit :

- l'amont a répondu autre chose qu'un avis → revoir le modèle ou la consigne ;
- l'amont n'est pas en état de servir → démarrer ou réparer le serveur ;
- l'amont est trop lent → changer de modèle, ou de matériel.

Les rendre sous un code commun obligerait l'exploitant à deviner laquelle des trois.

**Aucune reprise.** Température à zéro et seed fixe font d'une requête rejouée une opération nulle :
elle rendrait le même avis, en payant une seconde fois un GPU que 8 Go de VRAM sérialisent déjà. Une
reprise n'achèterait donc pas une meilleure réponse, seulement une échéance dépassée plus tard.

**Le fournisseur n'est pas un choix gravé.** Tout ce que ce module sait de son amont est un
`base_url` et un nom de modèle, tous deux en configuration : basculer vers un autre serveur parlant
le protocole compatible OpenAI ne demande de toucher aucune ligne de logique de qualification.
"""

from __future__ import annotations

import json
from collections.abc import Mapping
from dataclasses import dataclass
from typing import Final, Protocol

import openai

from qualification_sidecar.opinion import DeclaredConfidence, EngineFailure
from qualification_sidecar.prompt import (
    CONFIDENCE_SLUGS,
    PROMPT_VERSION,
    RESPONSE_SCHEMA,
    SCHEMA_NAME,
    SYSTEM_PROMPT,
)

#: Le nom du moteur, tel qu'il figure dans chaque avis.
ENGINE_NAME: Final = "llm"

#: Le nom canonique de chaque degré de confiance dit en français par le modèle. Symétrique de
#: `taxonomy.CANONICAL_NAME_BY_SLUG` : le français est local au moteur, et s'arrête à la frontière.
CANONICAL_CONFIDENCE_BY_SLUG: Final[Mapping[str, DeclaredConfidence]] = {
    "haute": DeclaredConfidence.HIGH,
    "moyenne": DeclaredConfidence.MEDIUM,
    "basse": DeclaredConfidence.LOW,
}

# Ce que le prompt autorise le modèle à dire et ce que le moteur sait traduire doivent coïncider,
# sous peine d'un degré demandé au modèle puis refusé à la traduction. Même logique que la
# projection de la taxonomie : l'écart empêche le démarrage plutôt que d'attendre un texte réel.
if set(CANONICAL_CONFIDENCE_BY_SLUG) != set(CONFIDENCE_SLUGS):
    raise RuntimeError(
        f"Les degrés demandés au modèle ({', '.join(CONFIDENCE_SLUGS)}) et ceux que le moteur sait "
        f"traduire ({', '.join(CANONICAL_CONFIDENCE_BY_SLUG)}) divergent."
    )

#: Le préfixe des variables d'environnement qui portent la configuration du moteur.
SETTING_PREFIX: Final = "QUALIFICATION_LLM_"

#: Le drapeau qui commande l'**existence** du moteur, et lui seul — les sept autres variables ne
#: sont lues que là où il dit oui.
ENABLED_SETTING: Final = SETTING_PREFIX + "ENABLED"

#: Les deux façons de l'écrire. La casse est ignorée ; rien d'autre ne l'est.
_WRITTEN_YES: Final = frozenset({"true", "1", "yes", "on"})
_WRITTEN_NO: Final = frozenset({"false", "0", "no", "off", ""})




class UnusableCompletion(EngineFailure):
    """L'amont a répondu, mais sa réponse n'est pas un avis : JSON illisible, valeur hors taxonomie,
    exclusivité violée.

    C'est bien une `EngineFailure` — **un avis invalide n'est pas un avis faible, c'est une panne du
    moteur** —, mais elle sort en `502` et non en `500` : le fautif est l'amont, pas le sidecar, et
    confondre les deux enverrait chercher un bogue Python là où c'est le modèle qui déraille.
    """


class ModelUnreachable(RuntimeError):
    """L'amont est injoignable, ou ne connaît pas le modèle demandé.

    Panne de l'amont, et **pas la même que la précédente** : celle-ci se répare en démarrant ou en
    réparant un serveur, l'autre en changeant de modèle ou de consigne. Un amont qui a bien répondu,
    fût-ce une bêtise, n'est jamais injoignable.
    """


class ModelTooSlow(RuntimeError):
    """L'échéance vers l'amont est passée avant sa réponse.

    **La lenteur arrive nommée.** Un `504` distinct dit à l'exploitant que le serveur répond mais
    trop lentement — un diagnostic que le code d'une panne d'amont lui aurait caché.
    """


class MisconfiguredEngine(RuntimeError):
    """La configuration du moteur est absente ou incohérente : le sidecar refuse de démarrer.

    Levée au démarrage de l'application — jamais au premier appel —, et jamais rattrapée pour servir
    un moteur qui parlerait à un amont deviné. Une valeur par défaut cachée dans le code serait un
    chiffre en dur qui ne dit pas son nom. Vaut aussi pour le drapeau lui-même : un moteur allumé
    sans son échéance est une panne bruyante au démarrage, pas une panne de qualification plus tard.
    """


class EngineDisabled(RuntimeError):
    """Ce déploiement ne sert aucun modèle, et l'a décidé — ce n'est pas une panne.

    Un déploiement qui ne dit rien est dans ce cas : le moteur est éteint par défaut, pour qu'aucun
    texte de personne concernée ne parte vers un modèle génératif par simple effet de bord. Le point
    d'entrée LLM reste servi et **nomme** ce refus, plutôt que de disparaître : un exploitant qui
    l'interroge à la main doit obtenir une phrase, pas une énigme.
    """


def engine_is_enabled(environment: Mapping[str, str]) -> bool:
    """Dit si ce déploiement sert un modèle. **Non tant que personne n'a écrit le contraire.**

    C'est le seul réglage du moteur qui ait une valeur par défaut, et la seule qui puisse en avoir
    une : ne pas servir de modèle est un comportement entier, là où un `base_url` deviné ne rendrait
    que des avis inexploitables.

    Un drapeau incompréhensible ne se replie pas sur « éteint » : le repli ferait passer une faute
    de frappe pour une décision, et l'exploitant qui croit avoir allumé son moteur ne l'apprendrait
    qu'au premier texte qualifié sans lui.
    """
    written = environment.get(ENABLED_SETTING, "").strip().lower()

    if written in _WRITTEN_YES:
        return True

    if written in _WRITTEN_NO:
        return False

    raise MisconfiguredEngine(
        f"La variable {ENABLED_SETTING} vaut « {written} », qui ne dit ni oui "
        f"({', '.join(sorted(_WRITTEN_YES))}) ni non ({', '.join(sorted(_WRITTEN_NO - {''}))})."
    )


@dataclass(frozen=True, slots=True)
class LlmSettings:
    """Tout ce que le moteur sait de son amont, et pas un chiffre de plus dans le code.

    Ces valeurs sont **arbitraires et assumées** : aucune n'a été mesurée, `qwen3:8b` n'ayant pas
    encore tourné sous charge. Les tenir en configuration est ce qui permettra de les réviser le
    jour de la mesure sans rouvrir ce fichier.
    """

    base_url: str
    model: str
    api_key: str
    temperature: float
    seed: int
    deadline_seconds: float
    caller_deadline_seconds: float

    def __post_init__(self) -> None:
        if self.deadline_seconds <= 0:
            raise MisconfiguredEngine(
                f"L'échéance vers l'amont doit être strictement positive, et vaut "
                f"{self.deadline_seconds}."
            )

        # L'inégalité est ce qui rend la lenteur *nommée* : si le sidecar rendait la main après
        # l'appelant .NET, celui-ci abandonnerait le premier et n'obtiendrait qu'une échéance
        # anonyme, là où le sidecar sait dire « l'amont est trop lent ».
        if self.deadline_seconds >= self.caller_deadline_seconds:
            raise MisconfiguredEngine(
                f"L'échéance du sidecar vers l'amont ({self.deadline_seconds} s) doit être "
                f"strictement plus courte que celle de l'appelant .NET "
                f"({self.caller_deadline_seconds} s) : sinon l'appelant abandonne le premier et "
                "la lenteur lui arrive sans nom."
            )

    @classmethod
    def from_environment(cls, environment: Mapping[str, str]) -> LlmSettings:
        """Lit la configuration, ou refuse — une variable absente n'est pas une valeur par défaut."""
        return cls(
            base_url=_read(environment, "BASE_URL", str),
            model=_read(environment, "MODEL", str),
            api_key=_read(environment, "API_KEY", str),
            temperature=_read(environment, "TEMPERATURE", float),
            seed=_read(environment, "SEED", int),
            deadline_seconds=_read(environment, "DEADLINE_SECONDS", float),
            caller_deadline_seconds=_read(environment, "CALLER_DEADLINE_SECONDS", float),
        )


def _read[T](environment: Mapping[str, str], name: str, parse: type[T]) -> T:
    """Lit une variable et la convertit, en nommant celle qui manque plutôt que la première venue."""
    variable = SETTING_PREFIX + name

    try:
        raw = environment[variable]
    except KeyError:
        raise MisconfiguredEngine(
            f"La variable {variable} est absente : le moteur LLM ne se configure pas tout seul."
        ) from None

    try:
        return parse(raw)
    except ValueError:
        raise MisconfiguredEngine(
            f"La variable {variable} vaut « {raw} », qui n'est pas un {parse.__name__}."
        ) from None


def engine_version(served_model: str) -> str:
    """Compose la version du moteur : **le modèle réellement servi**, puis sa consigne.

    Le seul code du sidecar ne suffirait pas. Une qualification rendue par `qwen3:8b` et une autre
    par son successeur ne sont pas comparables, et quelqu'un devra un jour trier les traces d'audit
    selon ce qui les a produites — il faut donc que la trace le dise. La consigne suit pour la même
    raison : deux verdicts du même modèle sous deux prompts différents ne sont pas comparables non
    plus.
    """
    return f"{served_model}+prompt.{PROMPT_VERSION}"


@dataclass(frozen=True, slots=True)
class ModelAnswer:
    """Ce que l'amont a rendu : son texte, et le modèle qu'il déclare avoir servi."""

    content: str
    served_model: str


class StructuredModel(Protocol):
    """Le peu que le moteur exige de son amont : une consigne, un texte, une réponse structurée.

    C'est cette étroitesse qui rend la suite de tests exécutable sans GPU. Un test qui exigerait un
    GPU est un test qui ne tourne jamais, et un test qui ne tourne jamais ment.
    """

    async def answer(self, *, system: str, user: str) -> ModelAnswer: ...


class OpenAiCompatibleModel:
    """L'amont réel, derrière le protocole compatible OpenAI — et rien de ce protocole ne fuit plus loin.

    Les échecs du client y sont traduits en les trois pannes que le domaine distingue : au-delà de
    cette classe, plus personne n'a à connaître la taxonomie d'erreurs d'un SDK.
    """

    def __init__(self, settings: LlmSettings) -> None:
        self._settings = settings
        self._client = openai.AsyncOpenAI(
            base_url=settings.base_url,
            api_key=settings.api_key,
            timeout=settings.deadline_seconds,
            # Aucune reprise : rejouer une requête déterministe rendrait le même avis en payant une
            # seconde fois un GPU sérialisé.
            max_retries=0,
        )

    async def answer(self, *, system: str, user: str) -> ModelAnswer:
        try:
            completion = await self._client.chat.completions.create(
                model=self._settings.model,
                messages=[
                    {"role": "system", "content": system},
                    {"role": "user", "content": user},
                ],
                temperature=self._settings.temperature,
                seed=self._settings.seed,
                response_format={
                    "type": "json_schema",
                    "json_schema": {
                        "name": SCHEMA_NAME,
                        "strict": True,
                        "schema": RESPONSE_SCHEMA,
                    },
                },
            )
        # Les trois `except` suivent l'ordre de la hiérarchie du SDK, du plus précis au plus large,
        # parce que c'est là que se joue la distinction des codes. `APITimeoutError` dérive
        # d'`APIConnectionError`, qui dérive d'`APIError` : rattraper large en premier ferait
        # ressortir un dépassement d'échéance sous le code d'un serveur éteint.
        except openai.APITimeoutError as timeout:
            raise ModelTooSlow(
                f"L'amont n'a pas répondu en {self._settings.deadline_seconds} s."
            ) from timeout
        # Injoignable au sens du domaine : soit personne ne répond, soit l'amont répond qu'il ne
        # connaît pas ce modèle. Dans les deux cas il n'est pas en état de servir, et c'est
        # l'installation qu'on va corriger, jamais la consigne.
        except (openai.APIConnectionError, openai.NotFoundError) as unreachable:
            raise ModelUnreachable(
                f"L'amont n'est pas en état de servir « {self._settings.model} » : {unreachable}"
            ) from unreachable
        # Tout le reste — statut d'erreur, contexte dépassé, `response_format` non supporté — est un
        # amont **qui a bien répondu**, mais autre chose qu'un avis. Le confondre avec l'injoignable
        # enverrait redémarrer un serveur qui tourne.
        except openai.OpenAIError as unusable:
            raise UnusableCompletion(
                f"L'amont a répondu autre chose qu'une complétion : {unusable}"
            ) from unusable

        # Le modèle absent du serveur ressort ici en statut d'erreur, donc en `502` : Ollama a
        # répondu pour dire qu'il ne connaît pas ce modèle, ce qui n'est pas la même chose que ne
        # pas répondre.
        content = completion.choices[0].message.content if completion.choices else None

        if not content:
            raise UnusableCompletion("L'amont a rendu une complétion sans contenu.")

        # Le modèle déclaré par l'amont prime sur celui demandé : c'est celui qui a réellement parlé.
        return ModelAnswer(content=content, served_model=completion.model or self._settings.model)


@dataclass(frozen=True, slots=True)
class UntranslatedOpinion:
    """Le verdict tel que le modèle l'a rendu : en français, non traduit, non validé au-delà de sa forme.

    Il ne quitte jamais le sidecar sous cette forme. La traduction en noms de fil a lieu à la
    frontière HTTP, exactement comme pour le lexique.
    """

    slugs: tuple[str, ...]
    confidence_slug: str
    justification: str
    served_model: str


async def qualify(text: str, model: StructuredModel) -> UntranslatedOpinion:
    """Demande son avis au modèle et vérifie qu'il en a rendu un, sans encore le traduire.

    Tout écart à la forme attendue lève `UnusableCompletion` : **un avis à moitié valide n'est pas
    un avis faible, c'est une panne du moteur**. Le schéma de sortie structurée est censé rendre ces
    écarts impossibles ; le vérifier quand même est ce qui empêche un fournisseur qui l'ignorerait
    de faire entrer une valeur inventée dans une trace d'audit.
    """
    answer = await model.answer(system=SYSTEM_PROMPT, user=text)

    try:
        verdict = json.loads(answer.content)
    except json.JSONDecodeError as malformed:
        raise UnusableCompletion(
            f"Le modèle a rendu autre chose que du JSON : {malformed}"
        ) from malformed

    if not isinstance(verdict, dict):
        raise UnusableCompletion(
            f"Le modèle a rendu un {type(verdict).__name__} là où le schéma exige un objet."
        )

    return UntranslatedOpinion(
        slugs=_read_slugs(verdict),
        confidence_slug=_read_confidence(verdict),
        justification=_read_justification(verdict),
        served_model=answer.served_model,
    )


def _read_slugs(verdict: Mapping[str, object]) -> tuple[str, ...]:
    slugs = verdict.get("droits")

    if not isinstance(slugs, list) or not all(isinstance(slug, str) for slug in slugs):
        raise UnusableCompletion(
            f"Le modèle a rendu « droits » sous la forme {slugs!r}, là où le schéma exige une "
            "liste de chaînes."
        )

    # La liste vide est refusée ici plutôt que laissée aux invariants du domaine : c'est le même
    # refus, mais celui-ci nomme le modèle, et l'autre nommerait « un moteur ».
    if not slugs:
        raise UnusableCompletion(
            "Le modèle a rendu une liste de droits vide : l'absence de droit reconnu s'écrit "
            "« hors-perimetre », elle ne se déduit pas d'un silence."
        )

    return tuple(slugs)


def _read_confidence(verdict: Mapping[str, object]) -> str:
    """Ne vérifie que la *forme*, jamais l'appartenance à l'échelle.

    Symétrique des droits, dont l'appartenance à la taxonomie est refusée par `to_canonical` et par
    lui seul : un degré hors échelle est refusé par `to_canonical_confidence`, et par lui seul.
    Vérifier ici *aussi* ferait deux gardiens d'une même règle, dont l'un finirait par diverger.
    """
    confidence = verdict.get("confiance")

    if not isinstance(confidence, str):
        raise UnusableCompletion(
            f"Le modèle a rendu la confiance {confidence!r}, là où le schéma exige une chaîne."
        )

    return confidence


def _read_justification(verdict: Mapping[str, object]) -> str:
    justification = verdict.get("justification")

    # Une justification vide serait un avis sans raison : le moteur n'a pas le droit d'en rendre un,
    # puisque c'est le seul texte sur lequel l'opérateur humain s'appuie pour relire.
    if not isinstance(justification, str) or not justification.strip():
        raise UnusableCompletion(
            f"Le modèle a rendu la justification {justification!r} : le moteur LLM justifie toujours, "
            "c'est ce qu'il apporte de plus que le lexique."
        )

    return justification.strip()


def to_canonical_confidence(slug: str) -> DeclaredConfidence:
    """Traduit un degré de confiance français en degré de fil, ou refuse.

    Un degré inconnu ne se replie pas sur `Low` : un repli inventerait une auto-évaluation que le
    modèle n'a pas rendue, et c'est précisément sur elle que l'opérateur humain trie sa relecture.
    """
    try:
        return CANONICAL_CONFIDENCE_BY_SLUG[slug]
    except KeyError:
        raise UnusableCompletion(
            f"Le modèle a rendu la confiance « {slug} », hors de l'échelle à trois degrés."
        ) from None
