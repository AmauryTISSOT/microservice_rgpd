"""`Locate` sur la base : nommer les lignes rattachables, et dire lesquelles font douter.

Il n'y a pas de « table des personnes » à interroger. `clients.id` n'est la clé qu'à l'intérieur de
`clients` ; ailleurs, la personne est désignée par son adresse électronique recopiée à la main —
`newsletter.courriel`, `commandes.courriel_acheteur`, `messages.auteur_courriel`,
`clients_ancienne_boutique.mail` — et aucune de ces tables ne porte de clé étrangère vers `clients`.
D'où la forme de ce fichier : une **sonde par table**, et des références par sonde.

**Ce qu'on rend n'est plus un dénombrement mais des références**, écrites dans le vocabulaire de
Brocanto — `clients#1203`, `commandes#CMD-2026-0412`. Le service ne les découpe pas et ne les
compare pas d'un système à l'autre : il les réaffiche mot pour mot à quelqu'un qui saura les relire
ici. Il ne connaît toujours aucun nom de table avant de lire cette réponse.

**La ligne de partage : ce qu'on rattache, et ce dont on doute.**

- Une ligne trouvée sous une adresse, un téléphone ou une référence est **certaine**. La valeur est
  exacte, et c'est la personne qui nous l'a donnée.
- Une ligne trouvée sous un **nom** est une **réserve**, toujours, et même quand elle est seule. Un
  nom ne désigne personne à lui seul : `clients` ne pose aucune unicité, `adresses.destinataire` et
  `paiements.porteur_nom` nomment couramment un tiers, et Brocanto porte deux « Jean Dupont ». Nous
  ne savons pas trancher ; ce n'est pas à nous de le faire, et surtout pas en silence.

Chaque réserve propose l'**adresse** de sa ligne, quand la table en porte une. C'est le seul champ
que le service interprétera, et seulement après qu'un humain aura rattaché la réserve — c'est par là
que le second système, le journal, s'ouvre enfin.

⚠️ **Deux sondes par table, désormais, et c'est le dénombrement qui l'interdisait.** Tant qu'on
comptait, deux sondes sur `clients` auraient compté deux fois la personne trouvée par son nom *et*
par son adresse. Une référence, elle, se dédoublonne — et la ligne certaine l'emporte alors sur la
réserve, la question étant déjà tranchée.

**Ce qu'on ne regarde pas, et qu'il faudra dire un jour** : le texte libre. Les descriptions
d'annonces et le corps des messages contiennent des numéros de téléphone et des adresses postales
écrits à la main, que ces sondes ne trouvent pas. L'absence de `messages` veut donc dire « aucun
message *écrit par* cette adresse », jamais « aucun message qui parle d'elle ».
"""

from typing import NamedTuple

from adapter_rgpd import Designation, Reserve, servi_locate


class Emplacement(NamedTuple):
    """Une table, ce qui y fait référence, et ce sous quoi la personne s'y cherche."""

    table: str
    cle: str
    """La colonne qu'un humain relira ici pour retrouver la ligne. Pas toujours la clé primaire."""

    exactes: dict
    """Nature de désignation → colonne, pour ce qui identifie : adresse, téléphone, référence."""

    nominales: dict
    """Nature → colonne, pour ce qui ne fait que **nommer**. Toujours une réserve."""

    courriel: str
    """La colonne d'adresse de cette table, proposée par ses réserves. Vide si elle n'en porte pas."""

    doute: str
    """Pourquoi un nom trouvé ici ne suffit pas. Lu tel quel par l'opérateur du service."""


EMPLACEMENTS = (
    # L'ordre est celui de la lecture : le compte, puis ce qui gravite autour, puis la reprise et
    # les tables sans compte.
    Emplacement(
        table="clients",
        cle="id",
        exactes={"email": "email", "phone": "telephone", "reference": "id"},
        nominales={"name": "CONCAT_WS(' ', prenom, nom)"},
        courriel="email",
        doute="Un compte au même nom, et rien dans « clients » n'impose l'unicité d'un nom.",
    ),
    Emplacement(
        table="adresses",
        cle="id",
        exactes={},
        # `destinataire` nomme parfois quelqu'un d'autre que le titulaire du compte.
        nominales={"name": "destinataire"},
        courriel="",
        doute="Une adresse de livraison à ce nom : c'est souvent le titulaire du compte, "
        "mais tout aussi souvent la personne à qui il fait livrer.",
    ),
    Emplacement(
        table="commandes",
        cle="reference",
        exactes={"email": "courriel_acheteur", "reference": "reference"},
        nominales={"name": "nom_livraison"},
        courriel="courriel_acheteur",
        doute="Une commande livrée à ce nom, sans que l'adresse de l'acheteur ait été cherchée : "
        "on commande pour un proche.",
    ),
    Emplacement(
        table="factures",
        # Scellées dix ans (art. L123-22 c. com.) : on sait les nommer, on ne saura pas les effacer.
        cle="numero",
        exactes={"email": "destinataire_courriel", "reference": "numero"},
        nominales={"name": "destinataire_nom"},
        courriel="destinataire_courriel",
        doute="Une facture à ce nom. La pièce est scellée dix ans et ne sera de toute façon pas "
        "effaçable ; reste à savoir si elle concerne la personne.",
    ),
    Emplacement(
        table="clients_ancienne_boutique",
        cle="id_ancien",
        exactes={"email": "mail", "reference": "id_ancien"},
        nominales={"name": "nom_complet"},
        courriel="mail",
        doute="Une ligne de la reprise PrestaShop de 2019, portant ce nom. Elle n'a jamais été "
        "rapprochée des comptes actuels — personne ici ne sait si c'est la même personne.",
    ),
    Emplacement(
        table="newsletter",
        cle="courriel",
        exactes={"email": "courriel"},
        nominales={},
        courriel="courriel",
        doute="",
    ),
    Emplacement(
        table="messages",
        cle="id",
        exactes={"email": "auteur_courriel"},
        nominales={},
        courriel="auteur_courriel",
        doute="",
    ),
    Emplacement(
        table="paiements",
        cle="reference_psp",
        exactes={},
        nominales={"name": "porteur_nom"},
        courriel="",
        doute="Un paiement dont la carte porte ce nom. Le porteur n'est pas toujours l'acheteur, "
        "et le détail est chez le prestataire, pas ici.",
    ),
)
"""Où une personne peut se trouver, table par table, et sous quelle colonne selon la désignation.

`stats_annonces_jour` et `annonce_photos` n'y figurent pas : elles ne portent aucune identité
directement, et ne se rattachent que par jointure. C'est une réserve, pas un oubli.

⚠️ `newsletter` a pour clé **l'adresse elle-même** : sa référence porte donc une donnée personnelle.
Une référence est opaque pour le service — il ne la lit pas — mais elle n'est pas anonyme pour
autant, et c'est le prix d'une table dont la personne *est* la clé. Rien ici ne permet de faire
autrement sans fabriquer un identifiant que personne ne saurait relire.
"""


class Sonde(NamedTuple):
    """Une table, la question qu'on lui pose, les valeurs liées, et ce qu'on saura de sa réponse."""

    emplacement: str
    sql: str
    params: tuple
    certaine: bool
    """Les lignes rendues sont-elles rattachées, ou seulement nommées ?"""


def sondes(designations):
    """Les questions à poser à la base pour ce sac de désignations.

    Deux sondes au plus par table : celle des désignations qui **identifient**, et celle du nom, qui
    ne fait que nommer. Elles ne se mélangent pas, parce que ce qu'on saura de leurs lignes n'est
    pas la même chose — et c'est très exactement ce que le service demande de ne pas confondre.
    """
    posees = []

    for emplacement in EMPLACEMENTS:
        exacte = _sonde(emplacement, emplacement.exactes, designations, certaine=True)
        if exacte:
            posees.append(exacte)

        nominale = _sonde(emplacement, emplacement.nominales, designations, certaine=False)
        if nominale:
            posees.append(nominale)

    return posees


def _sonde(emplacement, colonnes, designations, certaine):
    """Une sonde, ou rien si aucune désignation du sac ne se cherche sous ces colonnes."""
    applicables = [
        (colonnes[designation.nature], designation.valeur)
        for designation in designations
        if designation.nature in colonnes
    ]

    if not applicables:
        return None

    # Une réserve doit pouvoir proposer l'adresse de sa ligne : c'est ce champ, et lui seul, que le
    # service reprendra une fois qu'un humain aura tranché.
    courriel = emplacement.courriel if not certaine and emplacement.courriel else "NULL"
    conditions = " OR ".join(f"{colonne} = %s" for colonne, _valeur in applicables)

    return Sonde(
        emplacement.table,
        f"SELECT {emplacement.cle} AS reference, {courriel} AS courriel "
        f"FROM {emplacement.table} WHERE {conditions}",
        tuple(valeur for _colonne, valeur in applicables),
        certaine,
    )


def localiser(designations, lire):
    """Ce que la base porte sous ces désignations : ce qu'on rattache, et ce dont on doute.

    `lire` exécute une `Sonde` et rend ses lignes — des dictionnaires à deux clés, `reference` et
    `courriel` ; c'est par là que la connexion entre, et c'est tout ce que ce fichier sait de
    MariaDB.

    Un sac vide, ou sans rien de cherchable, ne fait ouvrir aucune table et rend un zéro. On n'a
    alors « pas regardé » — mais le contrat ne demande pas de le dire : le service tient déjà, de
    son côté, la différence entre un système non appelé et un système qui a rendu zéro.
    """
    certain = []
    reserves = []
    doutes = {emplacement.table: emplacement for emplacement in EMPLACEMENTS}

    for sonde in sondes(designations):
        lignes = list(lire(sonde))

        if sonde.certaine:
            certain.extend(_reference(sonde.emplacement, ligne) for ligne in lignes)
            continue

        emplacement = doutes[sonde.emplacement]
        reserves.extend(
            Reserve(
                _reference(sonde.emplacement, ligne),
                _motif(emplacement, len(lignes)),
                _propose(ligne),
            )
            for ligne in lignes
        )

    # Le dédoublonnage se fait sur la référence, et la ligne certaine l'emporte : une ligne trouvée
    # sous l'adresse *et* sous le nom n'a plus rien de douteux, et faire arbitrer un doute déjà levé
    # userait l'attention qu'on demande aux vrais.
    certain = list(dict.fromkeys(certain))
    tranchees = set(certain)

    return servi_locate(
        certain,
        [reserve for reserve in reserves if reserve.reference not in tranchees],
    )


def _reference(table, ligne):
    """La ligne, dite dans le vocabulaire de Brocanto. Opaque pour le service, relisible ici."""
    return f"{table}#{ligne['reference']}"


def _motif(emplacement, homonymes):
    """Pourquoi cette ligne fait douter, en une phrase qu'on dirait à un collègue.

    Le nombre d'homonymes trouvés entre dans la phrase parce qu'il change ce qu'un humain doit
    faire : deux lignes à départager ne se lisent pas comme une ligne isolée dont on ne sait pas si
    elle est la bonne. C'est de la prose, jamais un score.
    """
    if homonymes > 1:
        return f"{homonymes} lignes de « {emplacement.table} » portent ce nom. {emplacement.doute}"

    return f"Trouvée sur le seul nom. {emplacement.doute}"


def _propose(ligne):
    """Ce que cette ligne propose comme désignation, s'il y a quelque chose à proposer.

    Une réserve sans désignation reste locale et opaque : elle s'arbitre, mais elle n'apprendra rien
    à personne d'autre. C'est le cas des tables qui ne portent aucune adresse — `adresses` et
    `paiements` — et ce n'est pas un manque : elles n'ont vraiment rien de plus à dire.
    """
    courriel = ligne.get("courriel")

    return (Designation("email", courriel),) if courriel else ()
