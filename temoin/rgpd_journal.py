"""`Locate` sur le journal applicatif : la même question posée à des fichiers plats.

Le journal n'est pas une base. Chaque ligne y associe une date, une adresse IP, l'adresse
électronique de la session, la méthode, le chemin appelé et le statut ; logrotate en fait des
fichiers successifs, dont aucun n'est indexé. On ne peut donc que **lire**, ligne à ligne.

C'est le second système que sert cet `Adapter`, et il est là pour cette raison : ce qui vaut pour
une table doit valoir pour un fichier, sinon la branchabilité n'aurait été éprouvée que sur du SQL.

Quatre conséquences, propres à cette nature de stockage :

- **seule l'adresse électronique s'y cherche.** C'est la seule désignation qu'une ligne porte ; y
  chercher un nom ne trouverait rien, et y chercher une référence comme « 1203 » rattacherait des
  horaires, des statuts et des identifiants d'annonce. Une ligne rattachée par un chiffre qui traîne
  dans une date serait pire qu'une ligne manquante : elle aurait l'air d'un fait ;
- **la comparaison est sensible à la casse**, et c'est le piège de tout ce dossier. Voir plus bas ;
- **la référence rendue est le fichier, pas la ligne.** Une référence par ligne ferait de la réponse
  un déversement du journal dans le service, quand le contrat demande une référence qu'un humain
  relira ici — et ici, on relit un journal avec `grep`, sur un fichier ;
- au-delà d'un certain volume, la lecture ne tient pas dans une requête HTTP. On répond alors
  `202` en déclarant l'échéance de la passe de nuit, plutôt que de faire attendre le service.

⚠️ **La casse, et pourquoi on ne l'ignore pas.** Un fichier plat n'a pas de collation : comparer en
minuscules serait un choix que ce journal ne fait nulle part ailleurs — ni `grep`, ni l'astreinte
qui le lit à trois heures du matin. Le journal recopie l'adresse **telle que la session l'a
présentée**, et c'est cette chaîne-là, et pas une autre, qu'on y retrouve.

La conséquence est qu'une même désignation ouvre `rgpd_boutique` — MariaDB compare sans distinguer
la casse — et **ne trouve rien ici**. Deux systèmes du même client, la même personne, la même
adresse, et deux réponses opposées ; personne ne l'avait vu avant que le service ne pose la question
aux deux d'affilée. Ce qui répare le trou n'est pas un code : c'est la réserve que la base rend sur
son homonyme, l'adresse qu'elle propose dans la casse où elle la stocke, et l'humain qui tranche.

⚠️ **Ce que la nouvelle forme de réponse a coûté ici : le compte.** Ce fichier rendait un nombre de
lignes, et le contrat n'en veut plus. « 412 passages » disait quelque chose de l'intensité d'un
usage ; « brocanto.log » ne le dit pas. Le compte n'est perdu que pour le service, qui n'en faisait
rien de bon — il ne peut ni le vérifier ni le comparer d'un système à l'autre — et il reste ici,
sous la référence, pour qui vient regarder.
"""

import re
from datetime import datetime, timedelta

from adapter_rgpd import Differe, servi_locate

FICHIER_DU_JOURNAL = re.compile(r"^brocanto\.log(\.\d+)?$")
"""Le fichier courant et ceux que logrotate a tournés. Les archives compressées ne se lisent pas ici."""

SEUIL_OCTETS = 5 * 1024 * 1024
"""Au-delà, on déclare une échéance : lire cinq mégaoctets ligne à ligne n'est plus une requête."""

HEURE_DU_CRON = 3
"""La passe de nuit — celle qui alimente déjà les agrégats — est le moment où le volume se relit."""


def localiser(designations, dossier, seuil=SEUIL_OCTETS, maintenant=None):
    """Ce que le journal porte sous ces désignations, fichier par fichier.

    Rend un `Servi` quand la lecture tient dans l'appel, un `Differe` quand elle n'y tient pas.

    Aucune réserve n'est jamais rendue : une ligne portant l'adresse cherchée est la personne, sans
    quoi ce ne serait pas sa session. Le doute du journal n'est pas là — il est dans les lignes
    qu'il ne rendra pas, celles où l'adresse est écrite dans une autre casse.
    """
    cherchees = adresses_cherchees(designations)

    if not cherchees:
        # Ni sac vide, ni sac sans adresse ne fait ouvrir un fichier : il n'y a rien à y chercher.
        return servi_locate()

    fichiers = fichiers_du_journal(dossier)

    if sum(fichier.stat().st_size for fichier in fichiers) > seuil:
        return Differe(prochaine_fenetre_de_nuit(maintenant or datetime.now().astimezone()))

    return servi_locate(
        fichier.name for fichier in fichiers if porte_une_ligne(fichier, cherchees)
    )


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


def adresses_cherchees(designations):
    """Les adresses du sac, telles quelles. Les autres natures ne se cherchent pas dans un journal.

    Telles quelles, et non en minuscules : ce fichier ne normalise rien, parce que rien de ce qui
    lit ce journal ne normalise — voir le préambule du module.
    """
    return [
        designation.valeur
        for designation in designations
        if designation.nature == "email" and designation.valeur
    ]


def porte_une_ligne(fichier, cherchees):
    """Ce fichier porte-t-il au moins une ligne où l'une des adresses cherchées apparaît ?

    La comparaison porte sur la ligne entière et non sur le seul champ d'adresse : une adresse se
    retrouve aussi dans un chemin appelé — le lien de désinscription la met dans l'URL, et cette
    URL est journalisée comme les autres.

    On s'arrête à la première ligne trouvée : la référence rendue est le fichier, et lire les cinq
    cents suivantes n'y ajouterait rien.
    """
    with fichier.open(encoding="utf-8", errors="replace") as lignes:
        return any(
            cherchee in ligne for ligne in lignes for cherchee in cherchees
        )


def prochaine_fenetre_de_nuit(maintenant):
    """La prochaine passe du cron de nuit, datée avec le décalage horaire de la machine.

    Le décalage est obligatoire : le service refuse une échéance sans fuseau, qui vaudrait deux
    heures de moins sur un serveur et deux de plus sur un autre.
    """
    fenetre = maintenant.replace(hour=HEURE_DU_CRON, minute=0, second=0, microsecond=0)

    return fenetre if maintenant < fenetre else fenetre + timedelta(days=1)
