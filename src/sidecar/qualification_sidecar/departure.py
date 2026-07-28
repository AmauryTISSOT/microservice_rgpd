"""Le départ de l'appelant, et ce qu'il doit interrompre.

**La raison est matérielle.** Le GPU sérialise les générations : une génération poursuivie pour un
appelant qui n'est plus là ne gaspille pas seulement du calcul, elle **bloque la file** de celui qui
est resté. Une requête annulée ne doit donc laisser aucune trace — pas même une génération orpheline
que personne ne lira.

Rien de tout cela n'arrive tout seul. Le serveur ASGI signale le départ en mettant un
`http.disconnect` dans le canal de réception, mais il **n'interrompt pas** la tâche qui travaille :
sans ce module, la génération irait jusqu'à son terme, et le contrat interne dirait une chose que le
code ferait autrement.
"""

from __future__ import annotations

import asyncio
from collections.abc import Awaitable
from typing import Final, Protocol

#: À quelle cadence on regarde si l'appelant est encore là. Assez court pour qu'une génération
#: orpheline ne survive pas à ce qu'elle coûte, assez long pour ne pas sonder le canal en boucle.
POLL_SECONDS: Final = 0.05


class CallerGone(Exception):
    """L'appelant est parti avant que le moteur n'ait rendu son avis.

    Ce n'est ni une panne du moteur ni un refus d'entrée : personne n'attend plus de réponse. Elle
    existe pour que le chemin d'exécution s'arrête **en le disant**, plutôt qu'en rendant un avis à
    un canal fermé.
    """


class Caller(Protocol):
    """Le peu qu'on exige de la requête : savoir dire si celui qui l'a émise est encore là."""

    async def is_disconnected(self) -> bool: ...


async def serve_until_the_caller_leaves[T](caller: Caller, work: Awaitable[T]) -> T:
    """Rend ce que le travail a produit, ou l'interrompt dès que l'appelant s'en va.

    Les deux tâches courent ensemble et la première arrivée emporte tout. **Seule l'annulation de la
    génération est attendue** : c'est elle qui occupe le GPU, et rendre la main avant qu'elle ne soit
    vraiment arrêtée laisserait le calcul continuer quelques instants de plus — exactement ce qu'on
    cherche à supprimer. La veille, elle, est congédiée sans qu'on l'attende : elle ne coûte rien, et
    l'attendre exposerait à ce que le canal de réception ne rende la main qu'après la réponse, donc
    jamais.
    """
    generation = asyncio.ensure_future(work)
    watch = asyncio.ensure_future(_watch(caller))

    try:
        done, _ = await asyncio.wait({generation, watch}, return_when=asyncio.FIRST_COMPLETED)

        if generation in done:
            # `.result()` relève l'exception du travail telle quelle : les pannes du moteur gardent
            # leur type, et donc leur code.
            return generation.result()

        generation.cancel()
        await asyncio.gather(generation, return_exceptions=True)

        raise CallerGone("L'appelant est parti avant que le moteur n'ait rendu son avis.")
    finally:
        watch.cancel()


async def _watch(caller: Caller) -> None:
    """S'achève au départ de l'appelant, et jamais avant."""
    while not await caller.is_disconnected():
        await asyncio.sleep(POLL_SECONDS)
