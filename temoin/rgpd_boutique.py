"""`Locate` sur la base : dire si des données rattachables aux désignations existent, et combien.

Il n'y a pas de « table des personnes » à interroger. `clients.id` n'est la clé qu'à l'intérieur de
`clients` ; ailleurs, la personne est désignée par son adresse électronique recopiée à la main —
`newsletter.courriel`, `commandes.courriel_acheteur`, `messages.auteur_courriel`,
`clients_ancienne_boutique.mail` — et aucune de ces tables ne porte de clé étrangère vers `clients`.
D'où la forme de ce fichier : une **sonde par table**, et un compte par sonde.

Ce qu'on rend est un dénombrement, pas un dossier : le service ne connaît aucun nom de table avant
de lire cette réponse, et n'en connaîtra jamais d'autre que ceux qu'on écrit ici.

**Ce qu'on ne regarde pas, et qu'il faudra dire un jour** : le texte libre. Les descriptions
d'annonces et le corps des messages contiennent des numéros de téléphone et des adresses postales
écrits à la main, que ces sondes ne trouvent pas. Un compte de 0 sur `messages` veut donc dire
« aucun message *écrit par* cette adresse », jamais « aucun message qui parle d'elle ».
"""

from typing import NamedTuple

from adapter_rgpd import Servi


class Sonde(NamedTuple):
    """Une table, la question qu'on lui pose, et les valeurs liées à cette question."""

    emplacement: str
    sql: str
    params: tuple


EMPLACEMENTS = (
    # L'ordre est celui de la lecture : le compte, puis ce qui gravite autour, puis la reprise et
    # les tables sans compte.
    ("clients", {
        "email": "email",
        "name": "CONCAT_WS(' ', prenom, nom)",
        "phone": "telephone",
        "reference": "id",
    }),
    ("adresses", {
        # `destinataire` nomme parfois quelqu'un d'autre que le titulaire du compte.
        "name": "destinataire",
    }),
    ("commandes", {
        "email": "courriel_acheteur",
        "name": "nom_livraison",
        "reference": "reference",
    }),
    ("factures", {
        # Scellées dix ans (art. L123-22 c. com.) : on sait les compter, on ne saura pas les effacer.
        "email": "destinataire_courriel",
        "name": "destinataire_nom",
        "reference": "numero",
    }),
    ("clients_ancienne_boutique", {
        "email": "mail",
        "name": "nom_complet",
        "reference": "id_ancien",
    }),
    ("newsletter", {
        "email": "courriel",
    }),
    ("messages", {
        "email": "auteur_courriel",
    }),
    ("paiements", {
        "name": "porteur_nom",
    }),
)
"""Où une personne peut se trouver, table par table, et sous quelle colonne selon la désignation.

`stats_annonces_jour` et `annonce_photos` n'y figurent pas : elles ne portent aucune identité
directement, et ne se rattachent que par jointure. C'est une réserve, pas un oubli.
"""


def sondes(designations):
    """Les questions à poser à la base pour ce sac de désignations.

    Une seule sonde par table, même si trois désignations la visent : deux comptes sur la même table
    compteraient deux fois la personne trouvée par son nom *et* par son adresse.
    """
    posees = []

    for emplacement, colonnes in EMPLACEMENTS:
        applicables = [
            (colonnes[designation.nature], designation.valeur)
            for designation in designations
            if designation.nature in colonnes
        ]

        if not applicables:
            continue

        conditions = " OR ".join(f"{colonne} = %s" for colonne, _valeur in applicables)
        posees.append(
            Sonde(
                emplacement,
                f"SELECT COUNT(*) AS n FROM {emplacement} WHERE {conditions}",
                tuple(valeur for _colonne, valeur in applicables),
            )
        )

    return posees


def localiser(designations, compter):
    """Ce que la base porte sous ces désignations, table par table.

    `compter` exécute une `Sonde` et rend son nombre de lignes ; c'est par là que la connexion
    entre, et c'est tout ce que ce fichier sait de MariaDB.

    Les tables visitées sont rendues **même à zéro** : « regardé, rien trouvé » et « pas regardé »
    ne sont pas la même déclaration, et c'est la première qui a une valeur de preuve.
    """
    comptes = [
        {"emplacement": sonde.emplacement, "enregistrements": compter(sonde)}
        for sonde in sondes(designations)
    ]

    return Servi({
        "emplacements": comptes,
        "enregistrements": sum(compte["enregistrements"] for compte in comptes),
    })
