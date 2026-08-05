"""L'adaptateur RGPD : ce que le microservice de gestion des demandes vient nous demander.

C'est la **seule** partie de Brocanto qui sache que ce service existe. Le reste de l'application —
`app.py`, le schéma, les gabarits — l'ignore, et doit continuer de l'ignorer : ce fichier se branche
par-dessus, il ne se mélange pas.

Le sens des appels est unique : le service appelle, on répond. Rien à rappeler, aucune adresse du
service à connaître, aucune file de reprise à tenir.

Deux systèmes sont servis, de deux natures différentes :

- `brocanto-boutique` — la base MariaDB (`rgpd_boutique`) ;
- `brocanto-journal` — le journal applicatif à plat, fichiers tournés compris (`rgpd_journal`).

Le secret vient de la configuration de déploiement (`RGPD_ADAPTER_SECRET`), jamais du code. Il est
comparé avec `hmac.compare_digest` et non avec `==` : une comparaison qui s'arrête au premier octet
différent laisse mesurer combien d'octets étaient bons. C'est la seule exigence de forme que le
contrat pose sur ce fichier, et elle ne coûte rien — trois lignes de bibliothèque standard, aucune
dépendance de plus dans `requirements.txt`.

⚠️ Le secret n'est que la moitié du dispositif. L'autre moitié est le **périmètre réseau** : ces
routes ne doivent être joignables que depuis le réseau où vit le service. Exposées à l'Internet
public, elles rendraient des données personnelles à qui devine un secret — et aucune longueur de
secret ne répare cela. Voir `docs/api/adapter.md`, § 2, du dépôt du service.
"""

import hmac
from typing import NamedTuple

from flask import Blueprint, jsonify, request

EN_TETE_SECRET = "X-RGPD-Secret"
"""L'en-tête qui porte le secret partagé. Ni `Authorization`, ni schéma, ni porteur."""

PARAMETRE_SYSTEME = "system_id"
"""Le système voyage en paramètre : un seul Adapter en sert plusieurs."""

NATURES = ("email", "name", "phone", "reference")
"""Le vocabulaire fermé des désignations. Un cinquième mot n'est pas une nature qu'on devine."""


class Designation(NamedTuple):
    """Ce sous quoi chercher la personne. Ni unique, ni exact : le service ne le prétend pas."""

    nature: str
    valeur: str


class Servi(NamedTuple):
    """On a regardé, et voici ce qu'on a trouvé. Le corps est écrit dans notre vocabulaire."""

    corps: dict


class Differe(NamedTuple):
    """Le travail est trop long pour la connexion : on déclare quand on aura fini.

    L'échéance est **datée avec son décalage horaire** — le service refuse une date sans fuseau,
    qui vaudrait deux heures de moins d'un serveur à l'autre.
    """

    echeance: object


def blueprint_rgpd(secret, systemes):
    """Les routes de l'Adapter, montées sous `/rgpd`.

    `systemes` associe un `system_id` à la fonction qui sait le localiser : elle reçoit le sac de
    désignations et rend un `Servi` ou un `Differe`.

    Un `secret` vide ne laisse rien passer. C'est délibéré : un déploiement qui a oublié de le
    configurer doit refuser, pas s'ouvrir.
    """
    rgpd = Blueprint("rgpd", __name__, url_prefix="/rgpd")

    @rgpd.post("/locate")
    def locate():
        if not autorise(secret):
            # 401 ne dit rien du système appelé : il dit que nos deux moitiés n'ont plus le même
            # secret, ce qui se répare des deux côtés à la fois. Il est rendu **avant** de regarder
            # le système, sans quoi un inconnu lirait, statut par statut, ce que l'Adapter sert.
            return "Secret refusé", 401

        localise = systemes.get(request.args.get(PARAMETRE_SYSTEME, ""))
        if localise is None:
            # On refuse explicitement un système inconnu plutôt que de servir « au mieux » : un 200
            # poli sur un identifiant qu'on ne connaît pas serait exactement l'omission silencieuse
            # que tout ce dispositif cherche à rendre impossible.
            return "Système non servi", 404

        reponse = localise(designations_du_corps(request.get_json(silent=True)))

        if isinstance(reponse, Differe):
            return jsonify({"deadline": reponse.echeance.isoformat()}), 202

        return jsonify(reponse.corps), 200

    return rgpd


def autorise(secret):
    """Le secret présenté est-il le nôtre ? En temps constant, et jamais vide contre vide."""
    presente = request.headers.get(EN_TETE_SECRET, "")

    return bool(secret) and hmac.compare_digest(presente, secret)


def designations_du_corps(corps):
    """Le sac tel qu'il est arrivé, débarrassé de ce qui n'est pas une désignation.

    Le sac peut être vide, et ce n'est pas une erreur : c'est une recherche qui ne trouvera rien.
    """
    brutes = (corps or {}).get("designations") or []

    return [
        Designation(brute["kind"], brute["value"])
        for brute in brutes
        if isinstance(brute, dict) and brute.get("kind") in NATURES and "value" in brute
    ]
