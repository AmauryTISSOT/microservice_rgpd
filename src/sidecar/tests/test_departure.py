"""Le départ de l'appelant interrompt réellement le travail en cours.

Le GPU sérialise les générations : une génération poursuivie pour quelqu'un qui est parti ne
gaspille pas seulement du calcul, elle **bloque la file** de celui qui est resté. Ce que ces tests
gardent n'est donc pas une politesse de protocole, c'est la disponibilité du service.

**Rien de tout cela n'arrive tout seul** : le serveur ASGI signale le départ, mais n'interrompt
aucune tâche. Sans les lignes que ces tests couvrent, le contrat interne dirait une chose et le code
en ferait une autre.
"""

import asyncio

import pytest

from qualification_sidecar.departure import CallerGone, serve_until_the_caller_leaves


class Caller:
    """Un appelant qui reste, ou qui part au moment qu'on lui dit."""

    def __init__(self, leaves=False):
        self._leaves = leaves

    def leave(self):
        self._leaves = True

    async def is_disconnected(self):
        return self._leaves


class Generation:
    """Un travail long, qui note s'il a été interrompu plutôt que mené à son terme."""

    def __init__(self, verdict="un avis"):
        self._verdict = verdict
        self.started = asyncio.Event()
        self.interrupted = False

    async def run(self):
        self.started.set()

        try:
            await asyncio.sleep(30)
        except asyncio.CancelledError:
            self.interrupted = True
            raise

        return self._verdict


def test_an_avis_rendered_before_the_caller_leaves_is_handed_back():
    """Le cas nominal : l'appelant est resté, et il reçoit ce que le moteur a produit."""

    async def scenario():
        async def work():
            return "un avis"

        return await serve_until_the_caller_leaves(Caller(), work())

    assert asyncio.run(scenario()) == "un avis"


def test_a_generation_left_orphaned_is_really_interrupted():
    """Le cœur de la décision : la génération **s'arrête**, elle n'est pas seulement ignorée."""
    generation = Generation()

    async def scenario():
        caller = Caller()
        served = asyncio.ensure_future(serve_until_the_caller_leaves(caller, generation.run()))

        await asyncio.wait_for(generation.started.wait(), timeout=5)
        caller.leave()

        with pytest.raises(CallerGone):
            await asyncio.wait_for(served, timeout=5)

    asyncio.run(scenario())

    assert generation.interrupted


def test_the_departure_is_awaited_rather_than_merely_requested():
    """L'annulation est **attendue** avant qu'on rende la main.

    La demander sans l'attendre laisserait le calcul continuer quelques instants après la réponse —
    précisément le gaspillage qu'on cherche à supprimer, en plus petit.
    """
    generation = Generation()

    async def scenario():
        caller = Caller()
        served = asyncio.ensure_future(serve_until_the_caller_leaves(caller, generation.run()))

        await asyncio.wait_for(generation.started.wait(), timeout=5)
        caller.leave()

        with pytest.raises(CallerGone):
            await asyncio.wait_for(served, timeout=5)

        # Constaté *à l'instant même* où le départ est signalé, sans laisser tourner la boucle.
        assert generation.interrupted

    asyncio.run(scenario())


def test_a_failure_of_the_engine_keeps_its_type_and_therefore_its_code():
    """Une panne du moteur traverse telle quelle : la course n'aplatit aucune distinction de code."""

    class Panne(RuntimeError):
        pass

    async def scenario():
        async def work():
            raise Panne("l'amont n'est pas en état de servir")

        with pytest.raises(Panne):
            await serve_until_the_caller_leaves(Caller(), work())

    asyncio.run(scenario())


def test_no_watcher_survives_a_generation_that_finished_first():
    """La tâche de veille est annulée dès que l'avis est rendu — sans quoi elle sonderait sans fin."""
    watched = []

    class CountingCaller(Caller):
        async def is_disconnected(self):
            watched.append(1)
            return False

    async def scenario():
        async def work():
            await asyncio.sleep(0)
            return "un avis"

        await serve_until_the_caller_leaves(CountingCaller(), work())

        # La veille s'est arrêtée : laissée libre, elle aurait continué à sonder pendant ce répit.
        before = len(watched)
        await asyncio.sleep(0.2)

        assert len(watched) == before

    asyncio.run(scenario())
