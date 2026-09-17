"""Le consommateur RabbitMQ du mock : il déclare la topologie, consomme, journalise, acquitte.

C'est **le consommateur qui déclare** l'exchange, la file et le binding — jamais le service, dont
la topologie du bus n'est pas la propriété (ADR-0027). Le mock tient ici le rôle du système hôte :
ce qu'il fait au démarrage est exactement ce qu'un intégrateur ferait de son côté.

⚠️ **Il n'attend pas le broker pour exister.** `fail_fast` désarmé : broker éteint, le mock démarre,
répond sur ses routes HTTP, et se raccroche seul quand le broker arrive. C'est la contrepartie de
l'absence de `WaitFor` dans l'AppHost — sans quoi un démarrage à froid, où le mock part avant le
conteneur, laisserait un consommateur mort.

⚠️ **Le mock ne construit ni ne détient de chaîne de connexion URI.** Hôte, port, vhost et
identifiants sont passés séparément à `aio_pika`, pour le motif qui vaut déjà côté service
(`RabbitMqOptions`) : une URL se journalise, se recopie et s'affiche. `aio_pika` en assemble une
pour son propre usage, et masque le mot de passe quand il la journalise ; ce qui compte ici est
qu'aucun code du mock n'en tienne une.

Ce que le journal dit d'un message, et ce qu'il fait d'un doublon, vit dans `rights_journal` — sans
broker, donc éprouvable par pytest.
"""

from __future__ import annotations

import asyncio
import os
from collections.abc import Callable
from dataclasses import dataclass

import aio_pika

from mock_host.environment import int_setting
from mock_host.output import say
from mock_host.rights_journal import (
    RIGHTS_BINDING_KEY,
    RIGHTS_EXCHANGE,
    RIGHTS_QUEUE,
    RightsJournal,
)

#: Le port AMQP d'un environnement qui ne dit rien — celui de RabbitMQ, et celui que le service
#: retient aussi quand son déploiement se tait.
DEFAULT_PORT = 5672

#: Le vhost d'un environnement qui ne dit rien, comme côté service.
DEFAULT_VIRTUAL_HOST = "/"

#: L'identité d'un environnement qui ne dit rien : celle que RabbitMQ crée de lui-même.
DEFAULT_CREDENTIAL = "guest"

#: Le délai avant une nouvelle tentative quand la connexion tombe, ou n'a jamais pu s'établir.
RECONNECT_INTERVAL_SECONDS = 5.0


@dataclass(frozen=True)
class BrokerConnection:
    """Où joindre le broker. Posé par l'AppHost, qui tient les mêmes valeurs que le service."""

    host: str
    port: int
    virtual_host: str
    user: str
    password: str


def connection_from_environment() -> BrokerConnection | None:
    """La connexion que l'environnement déclare — ou `None` quand il n'en déclare aucune.

    ⚠️ **Pas d'hôte, pas de consommateur** : le mock reste alors la cible HTTP qu'il a toujours été,
    et rien n'échoue. C'est le même état légal que côté service (ADR-0028).

    Un hôte présent avec un port illisible arrête le démarrage, comme `MOCK_DELAY_MS`.
    """
    host = os.environ.get("MOCK_RABBITMQ_HOST", "").strip()

    if not host:
        return None

    return BrokerConnection(
        host=host,
        port=int_setting(
            "MOCK_RABBITMQ_PORT",
            default=DEFAULT_PORT,
            minimum=1,
            maximum=65535,
            what="un port entre 1 et 65535",
        ),
        virtual_host=os.environ.get("MOCK_RABBITMQ_VHOST", "").strip() or DEFAULT_VIRTUAL_HOST,
        user=os.environ.get("MOCK_RABBITMQ_USER", "").strip() or DEFAULT_CREDENTIAL,
        # Seul réglage non rogné : une espace peut faire partie d'un mot de passe, et la retirer
        # ferait échouer la connexion sur une correction que personne n'a demandée.
        password=os.environ.get("MOCK_RABBITMQ_PASSWORD") or DEFAULT_CREDENTIAL,
    )


async def consume(
    connection: BrokerConnection,
    journal: RightsJournal,
    handling_seconds: float = 0.0,
    announce: Callable[[str], None] = say,
) -> None:
    """Déclare la topologie, puis consomme jusqu'à ce que la tâche soit annulée.

    ⚠️ **Rien ici n'abandonne.** Une connexion refusée, un broker qui tombe, un exchange déjà
    déclaré autrement : la panne est annoncée puis retentée. Laisser la tâche mourir rendrait le
    mock sourd en silence, et la démonstration s'arrêterait sans un mot d'explication.

    ⚠️ **Tout message est acquitté**, doublon compris : refuser un doublon le ferait revenir sans
    fin. « Ignoré » veut dire « pas retraité » — c'est le temps de traitement simulé qu'un doublon
    n'obtient pas.
    """
    while True:
        try:
            await _consume_once(connection, journal, handling_seconds, announce)
        except asyncio.CancelledError:
            raise
        except Exception as interrupted:  # noqa: BLE001 — le mock survit à tout ce que le bus lui fait.
            announce(f"mock-host : consommation interrompue ({interrupted!r}), nouvelle tentative.")
            await asyncio.sleep(RECONNECT_INTERVAL_SECONDS)


async def _consume_once(
    connection: BrokerConnection,
    journal: RightsJournal,
    handling_seconds: float,
    announce: Callable[[str], None],
) -> None:
    """Une connexion, sa topologie, et la consommation qu'elle porte — jusqu'à ce qu'elle tombe."""
    amqp = await aio_pika.connect_robust(
        host=connection.host,
        port=connection.port,
        virtualhost=connection.virtual_host,
        login=connection.user,
        password=connection.password,
        # ⚠️ Ces deux-là voyagent en **texte** : `aio_pika` les lit comme des paramètres de l'URL
        # qu'il construit, et refuse un booléen.
        # Le broker n'est pas attendu au démarrage : la première tentative a le droit d'échouer, et
        # `aio_pika` réessaie de lui-même plutôt que de lever.
        fail_fast="0",
        reconnect_interval=str(RECONNECT_INTERVAL_SECONDS),
    )

    async with amqp:
        channel = await amqp.channel()
        # Un message à la fois : la démonstration se lit dans l'ordre où elle arrive.
        await channel.set_qos(prefetch_count=1)

        # C'est ici, et seulement ici, que la topologie naît. Durable des deux côtés : elle survit
        # au redémarrage du broker, dont le volume est persistant.
        exchange = await channel.declare_exchange(
            RIGHTS_EXCHANGE, aio_pika.ExchangeType.TOPIC, durable=True
        )
        queue = await channel.declare_queue(RIGHTS_QUEUE, durable=True)
        await queue.bind(exchange, routing_key=RIGHTS_BINDING_KEY)

        announce(
            f"mock-host consomme {RIGHTS_QUEUE} "
            f"(exchange {RIGHTS_EXCHANGE}, binding {RIGHTS_BINDING_KEY})"
        )

        async with queue.iterator() as messages:
            async for message in messages:
                # `requeue=False` : ce qui est acquitté l'est, quoi que le journal en ait dit.
                async with message.process(requeue=False):
                    reception = journal.receive(message.message_id, message.body)
                    announce(reception.line)

                    if reception.accepted and handling_seconds > 0:
                        await asyncio.sleep(handling_seconds)
