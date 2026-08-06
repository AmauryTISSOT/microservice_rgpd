"""L'adaptateur RGPD : ce que le microservice de gestion des demandes vient nous demander.

C'est la **seule** partie de Brocanto qui sache que ce service existe. Le schéma, les gabarits et
toutes les routes de la brocante l'ignorent, et doivent continuer de l'ignorer ; `app.py` n'en
connaît que le montage, en bas de fichier — un import et un `register_blueprint`, comme pour
n'importe quelle greffe.

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
from datetime import datetime
from typing import NamedTuple

from flask import Blueprint, Response, jsonify, request

EN_TETE_SECRET = "X-RGPD-Secret"
"""L'en-tête qui porte le secret partagé. Ni `Authorization`, ni schéma, ni porteur."""

PARAMETRE_SYSTEME = "system_id"
"""Le système voyage en paramètre : un seul Adapter en sert plusieurs."""

NATURES = ("email", "name", "phone", "reference")
"""Le vocabulaire fermé des désignations. Un cinquième mot n'est pas une nature qu'on devine."""

DROITS = ("Access", "Rectification", "Erasure", "Portability", "Restriction", "Objection")
"""Les droits que le service sait porter, sous leurs noms canoniques anglais.

Ils sont **ici** et non dans les modules qui lisent : c'est du vocabulaire de fil, comme `NATURES`.
Un droit inconnu n'est pas deviné — voir `droit_du_corps`.
"""

DROIT_PAR_DEFAUT = "Access"
"""Le droit d'un corps qui n'en déclare pas.

Le plus **large** des périmètres, délibérément : un appel mal formé qui rendrait moins que demandé
serait une omission silencieuse, tandis qu'en rendre plus se voit — l'opérateur du service a la
pièce sous les yeux et lit ce qu'elle contient.
"""


class Designation(NamedTuple):
    """Ce sous quoi chercher la personne. Ni unique, ni exact : le service ne le prétend pas."""

    nature: str
    valeur: str


class Servi(NamedTuple):
    """On a regardé, et voici ce qu'on a trouvé. Le corps est écrit dans notre vocabulaire."""

    corps: dict


class Reserve(NamedTuple):
    """Une ligne trouvée sans qu'on sache dire si c'est la personne.

    Le `motif` est lu **tel quel** par l'opérateur du service : on y écrit la phrase qu'on dirait à
    un collègue, jamais un code ni un score. Une réserve sans motif est refusée par le service, et
    il a raison — ce serait un doute qu'on demande de trancher sans dire lequel.

    Les `designations` sont le seul champ que le service interprète, et seulement après qu'un humain
    a rattaché la réserve : elles entrent alors au sac, et l'appel suivant les porte. C'est par là
    que l'adresse trouvée dans la base ouvre le journal.
    """

    reference: str
    motif: str
    designations: tuple = ()


def servi_locate(certain=(), reserves=()):
    """Le corps d'un `locate` servi : ce qu'on rattache, et ce dont on doute.

    Les deux listes ne se confondent pas. `certain`, c'est ce dont on répond ; `reserved`, c'est ce
    qu'on refuse de trancher soi-même. Le service ne tranchera pas non plus : un humain le fera,
    nommément et à une date.

    Un zéro n'a **qu'une** forme — deux listes vides. Le contrat ne nous demande pas de distinguer
    « rien trouvé » de « rien à trouver », ni de rendre un compte ; ce qu'on aurait mis dans la
    nuance se met dans une réserve motivée, qui, elle, sera lue.
    """
    return Servi({
        "certain": list(certain),
        "reserved": [
            {
                "reference": reserve.reference,
                "reason": reserve.motif,
                "designations": [
                    {"kind": designation.nature, "value": designation.valeur}
                    for designation in reserve.designations
                ],
            }
            for reserve in reserves
        ],
    })


class Piece(NamedTuple):
    """Ce qu'un `read` rend : des octets, et de quoi les nommer. Rien de plus.

    **Aucune forme n'est imposée.** Le service ne lit pas le corps, ne le désérialise pas, et ne
    saura jamais si ces octets sont un CSV, un ZIP ou un PDF — il recopie le `Content-Type` qu'on
    écrit et le montre à un humain. C'est ce qui rend cette route tenable ici : exporter un CSV que
    la brocante sait déjà écrire coûte trois lignes, tandis qu'un schéma commun aurait demandé de
    traduire nos tables dans le vocabulaire de quelqu'un d'autre.

    Un `contenu` vide est une **réponse** : « on a regardé, il n'y a rien ». Le service la distingue
    d'une pièce absente, et cette gratuité-là est délibérée — dire « rien » ne doit rien coûter,
    sans quoi on serait tenté de ne rien dire du tout.

    Le `nom` est facultatif. Quand il manque, le service dégrade sur le `system_id` plutôt que de
    perdre la pièce : `send_file()` seul doit suffire.
    """

    contenu: bytes
    type_mime: str = "application/octet-stream"
    nom: str = ""


class Differe(NamedTuple):
    """Le travail est trop long pour la connexion : on déclare quand on aura fini.

    L'échéance est **datée avec son décalage horaire** — le service refuse une date sans fuseau,
    qui vaudrait deux heures de moins d'un serveur à l'autre.
    """

    echeance: datetime


def blueprint_rgpd(secret, systemes, lectures=None):
    """Les routes de l'Adapter, montées sous `/rgpd`.

    `systemes` associe un `system_id` à la fonction qui sait le **localiser** : elle reçoit le sac
    de désignations et rend un `Servi` ou un `Differe`.

    `lectures` associe un `system_id` à la fonction qui sait le **lire** : elle reçoit le sac et le
    droit au titre duquel on lit, et rend une `Piece` ou un `Differe`. Les deux tables sont
    séparées parce que les deux capacités le sont : un système peut être localisable sans être
    lisible, et le `Manifest` du service le déclare ainsi.

    Un `secret` vide ne laisse rien passer. C'est délibéré : un déploiement qui a oublié de le
    configurer doit refuser, pas s'ouvrir.
    """
    rgpd = Blueprint("rgpd", __name__, url_prefix="/rgpd")
    lectures = lectures or {}

    @rgpd.post("/locate")
    def locate():
        refus, localise = servant(secret, systemes)
        if refus is not None:
            return refus

        reponse = localise(designations_du_corps(request.get_json(silent=True)))

        if isinstance(reponse, Differe):
            return differe(reponse)

        return jsonify(reponse.corps), 200

    @rgpd.post("/read")
    def read():
        refus, lit = servant(secret, lectures)
        if refus is not None:
            return refus

        corps = request.get_json(silent=True)
        reponse = lit(designations_du_corps(corps), droit_du_corps(corps))

        if isinstance(reponse, Differe):
            return differe(reponse)

        return piece_servie(reponse)

    return rgpd


def servant(secret, servants):
    """Qui servira cet appel — ou le refus à rendre, secret d'abord et système ensuite.

    L'ordre n'est pas un détail : rendre 404 avant de juger le secret laisserait un inconnu lire,
    statut par statut, la liste des systèmes que cet Adapter sert.

    Le 404 est aussi ce qu'une **capacité non servie** rend. Un système localisable mais non lisible
    est un système que `/read` ne connaît pas, et « je ne sers pas cela » se dit d'une seule façon.
    """
    if not autorise(secret):
        # 401 ne dit rien du système appelé : il dit que nos deux moitiés n'ont plus le même secret,
        # ce qui se répare des deux côtés à la fois.
        return ("Secret refusé", 401), None

    servant_du_systeme = servants.get(request.args.get(PARAMETRE_SYSTEME, ""))

    if servant_du_systeme is None:
        # On refuse explicitement un système inconnu plutôt que de servir « au mieux » : un 200 poli
        # sur un identifiant qu'on ne connaît pas serait exactement l'omission silencieuse que tout
        # ce dispositif cherche à rendre impossible.
        return ("Système non servi", 404), None

    return None, servant_du_systeme


def differe(reponse):
    """Le 202 et son échéance déclarée, écrits une fois pour toutes les capacités."""
    return jsonify({"deadline": echeance_declarable(reponse.echeance)}), 202


def piece_servie(piece):
    """La pièce sur le fil : les octets, leur type, et leur nom s'il y en a un.

    ⚠️ **Le corps part tel quel, y compris vide.** Un 204 dirait autre chose — le service le lit
    comme une panne —, et un 200 sans octets est précisément la façon dont le contrat écrit
    « on a regardé, il n'y a rien ».
    """
    entetes = {"Content-Type": piece.type_mime}

    if piece.nom:
        entetes["Content-Disposition"] = f'attachment; filename="{piece.nom}"'

    return Response(piece.contenu, status=200, headers=entetes)


def autorise(secret):
    """Le secret présenté est-il le nôtre ? En temps constant, et jamais vide contre vide."""
    presente = request.headers.get(EN_TETE_SECRET, "")

    return bool(secret) and hmac.compare_digest(presente, secret)


def echeance_declarable(echeance):
    """L'échéance écrite pour le fil, décalage horaire compris.

    Une date sans fuseau n'est pas déclarable : le service la lit comme une panne, pas comme un
    différé. Le défaut se répare ici, chez celui qui l'a écrite — mieux vaut donc casser bruyamment
    dans nos propres journaux que déclarer une échéance que personne ne saura relire.
    """
    if echeance.utcoffset() is None:
        raise ValueError(
            "Une échéance déclarée sans décalage horaire vaudrait deux heures de moins d'un "
            "serveur à l'autre : le service la refuse, et il a raison."
        )

    return echeance.isoformat()


def designations_du_corps(corps):
    """Le sac tel qu'il est arrivé, débarrassé de ce qui n'est pas une désignation.

    Le sac peut être vide, et ce n'est pas une erreur : c'est une recherche qui ne trouvera rien.

    Ce qui n'a ni nature connue ni valeur textuelle est écarté plutôt que de casser l'appel : une
    désignation qu'on ne sait pas lire est une désignation de moins, pas une panne de l'Adapter.
    """
    brutes = (corps or {}).get("designations") or []

    return [
        Designation(brute["kind"], brute["value"])
        for brute in brutes
        if isinstance(brute, dict)
        and brute.get("kind") in NATURES
        and isinstance(brute.get("value"), str)
    ]


def droit_du_corps(corps):
    """Le droit au titre duquel on lit, ou `Access` si le corps n'en déclare pas.

    C'est **la seule chose que le service impose à un `read`**, et il n'impose rien de la réponse :
    le périmètre matériel se décide ici, ligne par ligne, parce que celui de l'art. 20 n'est pas
    celui de l'art. 15 et que personne d'autre que nous ne sait quelles colonnes la personne nous a
    fournies elle-même.

    Un droit hors du vocabulaire dégrade sur le défaut plutôt que de casser l'appel : le service ne
    l'enverrait pas, et rendre une panne sur un mot qu'on ne reconnaît pas ferait perdre une pièce
    qu'on savait servir.
    """
    declare = (corps or {}).get("right")

    return declare if declare in DROITS else DROIT_PAR_DEFAUT
