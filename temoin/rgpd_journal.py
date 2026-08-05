"""`Locate` sur le journal applicatif : la même question posée à des fichiers plats.

Le journal n'est pas une base. Chaque ligne y associe une date, une adresse IP, l'adresse
électronique de la session, la méthode, le chemin appelé et le statut ; logrotate en fait des
fichiers successifs, dont aucun n'est indexé. On ne peut donc que **lire**, ligne à ligne.

C'est le second système que sert cet `Adapter`, et il est là pour cette raison : ce qui vaut pour
une table doit valoir pour un fichier, sinon la branchabilité n'aurait été éprouvée que sur du SQL.

Deux conséquences, propres à cette nature de stockage :

- l'unité comptée est la **ligne**, pas l'enregistrement — le journal ne connaît pas de personne,
  il connaît des passages ;
- au-delà d'un certain volume, la lecture ne tient pas dans une requête HTTP. On répond alors
  `202` en déclarant l'échéance de la passe de nuit, plutôt que de faire attendre le service.
"""

import re
from datetime import datetime, timedelta

from adapter_rgpd import Differe, Servi

FICHIER_DU_JOURNAL = re.compile(r"^brocanto\.log(\.\d+)?$")
"""Le fichier courant et ceux que logrotate a tournés. Les archives compressées ne se lisent pas ici."""

SEUIL_OCTETS = 5 * 1024 * 1024
"""Au-delà, on déclare une échéance : lire cinq mégaoctets ligne à ligne n'est plus une requête."""

HEURE_DU_CRON = 3
"""La passe de nuit — celle qui alimente déjà les agrégats — est le moment où le volume se relit."""


def localiser(designations, dossier, seuil=SEUIL_OCTETS, maintenant=None):
    """Ce que le journal porte sous ces désignations, fichier par fichier.

    Rend un `Servi` quand la lecture tient dans l'appel, un `Differe` quand elle n'y tient pas.
    """
    if not designations:
        # Une recherche sous rien ne trouvera rien : inutile d'ouvrir quoi que ce soit.
        return Servi({"emplacements": [], "lignes": 0})

    fichiers = fichiers_du_journal(dossier)

    if sum(fichier.stat().st_size for fichier in fichiers) > seuil:
        return Differe(prochaine_fenetre_de_nuit(maintenant or datetime.now().astimezone()))

    comptes = [
        {"emplacement": fichier.name, "lignes": lignes_portant(fichier, designations)}
        for fichier in fichiers
    ]

    return Servi({
        "emplacements": comptes,
        "lignes": sum(compte["lignes"] for compte in comptes),
    })


def fichiers_du_journal(dossier):
    """Le courant d'abord, puis les tournés dans l'ordre où logrotate les a numérotés."""
    if not dossier.is_dir():
        # Un journal qui n'a pas encore été écrit se dit ; il ne casse pas l'appel.
        return []

    return sorted(
        fichier
        for fichier in dossier.iterdir()
        if fichier.is_file() and FICHIER_DU_JOURNAL.match(fichier.name)
    )


def lignes_portant(fichier, designations):
    """Les lignes où l'une des valeurs cherchées apparaît, casse indifférente.

    La comparaison porte sur la ligne entière et non sur le seul champ d'adresse : une adresse se
    retrouve aussi dans un chemin appelé — le lien de désinscription la met dans l'URL, et cette
    URL est journalisée comme les autres.
    """
    cherchees = [designation.valeur.lower() for designation in designations if designation.valeur]

    with fichier.open(encoding="utf-8", errors="replace") as lignes:
        return sum(
            1 for ligne in lignes if any(cherchee in ligne.lower() for cherchee in cherchees)
        )


def prochaine_fenetre_de_nuit(maintenant):
    """La prochaine passe du cron de nuit, datée avec le décalage horaire de la machine.

    Le décalage est obligatoire : le service refuse une échéance sans fuseau, qui vaudrait deux
    heures de moins sur un serveur et deux de plus sur un autre.
    """
    fenetre = maintenant.replace(hour=HEURE_DU_CRON, minute=0, second=0, microsecond=0)

    return fenetre if maintenant < fenetre else fenetre + timedelta(days=1)
