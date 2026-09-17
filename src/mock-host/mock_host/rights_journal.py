"""Le journal du consommateur : la topologie qu'il déclare, et ce qu'il dit d'un message reçu.

Ce module ne connaît **ni broker, ni socket, ni aio-pika** : il ne sait que transformer un
`message-id` et un corps en une ligne de journal et en un verdict — retenu, ou ignoré parce que
déjà vu. C'est ce qui permet d'éprouver l'idempotence sans démarrer RabbitMQ.

⚠️ **La topologie appartient à l'exploitant** (ADR-0027) : le service ne déclare ni exchange, ni
file, ni binding. C'est le consommateur qui les déclare — ici, le mock, qui tient le rôle du système
hôte.

⚠️ **La déduplication n'ouvre pas le corps.** Le service pose un `message-id` égal à l'identifiant
de la demande (ADR-0028) précisément pour qu'une republication se reconnaisse sans être lue. Le
corps n'est ouvert que pour dire le droit dans la ligne.
"""

from __future__ import annotations

import json
from dataclasses import dataclass

#: L'exchange sur lequel le service publie, tel qu'on le saisit dans l'écran de Paramétrage.
#: Topic et durable : une routing key par droit, et une topologie qui survit au redémarrage du
#: broker — dont le volume est persistant.
RIGHTS_EXCHANGE = "rgpd.rights"

#: La file du mock. Son nom porte celui du consommateur : un second consommateur aurait la sienne,
#: et recevrait sa propre copie de chaque message.
RIGHTS_QUEUE = "rgpd.rights.mock-host"

#: Le binding : **tout** ce qui commence par `rights.`, donc les six droits d'un seul coup. C'est ce
#: qui rend la démonstration indifférente au droit choisi dans l'écran.
RIGHTS_BINDING_KEY = "rights.#"

#: Ce qu'une valeur absente vaut dans la ligne de journal.
UNKNOWN = "?"


@dataclass(frozen=True)
class Reception:
    """Ce qu'un message reçu devient : une ligne à journaliser, et un verdict.

    ⚠️ **`accepted` ne commande pas l'acquittement.** Un doublon est acquitté comme un neuf : le
    refuser le ferait revenir indéfiniment. « Ignoré » veut dire « pas retraité », jamais « rejeté ».
    """

    #: Faux quand le `message-id` avait déjà été vu : le message est journalisé, puis écarté.
    accepted: bool

    #: La ligne telle qu'elle part sur la sortie standard.
    line: str


class RightsJournal:
    """Les `message-id` déjà vus, et la ligne que chaque message mérite.

    ⚠️ **La mémoire est celle du processus**, et ne survit pas à son redémarrage : c'est une
    démonstration, pas un magasin d'idempotence. Un vrai destinataire retiendrait les identifiants
    là où il retient déjà ses données.
    """

    def __init__(self) -> None:
        self._seen: set[str] = set()

    def receive(self, message_id: str | None, body: bytes) -> Reception:
        """Retient `message_id`, et rend la ligne que ce message mérite.

        Un `message-id` absent ou blanc ne se déduplique pas : la ligne le dit plutôt que de laisser
        croire à une garantie que le message ne porte pas.
        """
        right, request_id = _right_and_request_of(body)
        identifier = (message_id or "").strip()

        if not identifier:
            return Reception(
                accepted=True,
                line=(
                    f"{RIGHTS_EXCHANGE} reçu sans message-id (non déduplicable) : "
                    f"droit={right} demande={request_id}"
                ),
            )

        if identifier in self._seen:
            return Reception(
                accepted=False,
                line=(
                    f"{RIGHTS_EXCHANGE} ignoré (doublon) : "
                    f"droit={right} demande={request_id} message-id={identifier}"
                ),
            )

        self._seen.add(identifier)

        return Reception(
            accepted=True,
            line=(
                f"{RIGHTS_EXCHANGE} reçu : "
                f"droit={right} demande={request_id} message-id={identifier}"
            ),
        )


def _right_and_request_of(body: bytes) -> tuple[str, str]:
    """Le droit et l'identifiant de demande que porte le corps — ou `?` quand il ne les porte pas.

    ⚠️ **Un corps illisible n'interrompt rien** : le mock est une cible de démonstration, et une
    ligne incomplète en dit plus qu'une exception qui arrêterait la consommation.
    """
    try:
        payload = json.loads(body)
    except (ValueError, TypeError):
        return UNKNOWN, UNKNOWN

    if not isinstance(payload, dict):
        return UNKNOWN, UNKNOWN

    return _text(payload.get("right")), _text(payload.get("requestId"))


def _text(value: object) -> str:
    return value if isinstance(value, str) and value.strip() else UNKNOWN
