"""Le contrat sur le fil : le secret, les deux refus, et ce qu'un différé déclare.

Ces tests ne joignent ni MariaDB ni le journal : les deux systèmes servis sont remplacés par des
fonctions qui rendent ce qu'on leur dit. Ce qui est éprouvé ici est **le transport** — l'en-tête,
le paramètre, les statuts — c'est-à-dire exactement ce que le service lit.
"""

from datetime import datetime, timedelta, timezone

import pytest
from flask import Flask

from adapter_rgpd import (
    EN_TETE_SECRET,
    PARAMETRE_SYSTEME,
    Designation,
    Differe,
    Piece,
    Servi,
    blueprint_rgpd,
)

SECRET = "un-secret-de-recette"


def application(systemes=None, secret=SECRET, lectures=None):
    """Une application nue portant le seul Adapter : rien de Brocanto n'est chargé ici."""
    app = Flask(__name__)
    # Ce qui casse remonte à la suite plutôt que de se ranger derrière un 500 : une rupture du
    # contrat doit se lire ici, pas au statut.
    app.testing = True
    app.register_blueprint(
        blueprint_rgpd(secret, systemes if systemes is not None else {}, lectures)
    )
    return app.test_client()


def sert_rien(_designations):
    return Servi({"emplacements": [], "enregistrements": 0})


def appelle(client, systeme="brocanto-boutique", secret=SECRET, corps=None):
    entetes = {} if secret is None else {EN_TETE_SECRET: secret}
    return client.post(
        f"/rgpd/locate?{PARAMETRE_SYSTEME}={systeme}",
        headers=entetes,
        json=corps if corps is not None else {"designations": []},
    )


def test_un_secret_juste_ouvre_la_route():
    reponse = appelle(application({"brocanto-boutique": sert_rien}))

    assert reponse.status_code == 200
    assert reponse.get_json() == {"emplacements": [], "enregistrements": 0}


@pytest.mark.parametrize(
    "presente",
    [
        None,  # aucun en-tête
        "",
        "un-secret-de-recett",  # un octet de moins : un préfixe n'ouvre rien
        "un-secret-de-recette ",
        "UN-SECRET-DE-RECETTE",
    ],
)
def test_tout_secret_qui_n_est_pas_le_secret_est_refuse(presente):
    reponse = appelle(application({"brocanto-boutique": sert_rien}), secret=presente)

    assert reponse.status_code == 401


def test_un_adapter_sans_secret_configure_ne_sert_personne():
    """Aucun mode « sans » : un déploiement qui a oublié le secret refuse, il ne s'ouvre pas."""
    reponse = appelle(application({"brocanto-boutique": sert_rien}, secret=""), secret="")

    assert reponse.status_code == 401


def test_un_systeme_non_servi_est_refuse_a_part():
    reponse = appelle(application({"brocanto-boutique": sert_rien}), systeme="brocanto-crm")

    assert reponse.status_code == 404


def test_un_appel_sans_systeme_est_un_systeme_non_servi():
    client = application({"brocanto-boutique": sert_rien})

    reponse = client.post("/rgpd/locate", headers={EN_TETE_SECRET: SECRET}, json={})

    assert reponse.status_code == 404


def test_le_secret_est_juge_avant_le_systeme():
    """Sans quoi un inconnu lirait, statut par statut, la liste des systèmes que l'Adapter sert."""
    reponse = appelle(
        application({"brocanto-boutique": sert_rien}), systeme="brocanto-crm", secret="faux"
    )

    assert reponse.status_code == 401


def test_les_deux_systemes_du_temoin_sont_servis_par_le_meme_adapter():
    servis = []

    def note(nom):
        def sert(_designations):
            servis.append(nom)
            return Servi({"emplacements": [], "enregistrements": 0})

        return sert

    client = application({"brocanto-boutique": note("boutique"), "brocanto-journal": note("journal")})

    assert appelle(client, systeme="brocanto-boutique").status_code == 200
    assert appelle(client, systeme="brocanto-journal").status_code == 200
    assert servis == ["boutique", "journal"]


def test_un_differe_repond_202_et_declare_son_echeance():
    echeance = datetime(2026, 8, 6, 3, 0, tzinfo=timezone(timedelta(hours=2)))

    reponse = appelle(application({"brocanto-boutique": lambda _d: Differe(echeance)}))

    assert reponse.status_code == 202
    assert reponse.get_json() == {"deadline": "2026-08-06T03:00:00+02:00"}


def test_le_sac_de_designations_arrive_tel_quel():
    recu = []

    def sert(designations):
        recu.extend(designations)
        return Servi({"emplacements": [], "enregistrements": 0})

    appelle(
        application({"brocanto-boutique": sert}),
        corps={
            "designations": [
                {"kind": "email", "value": "helene.petit@example.fr"},
                {"kind": "name", "value": "Hélène Petit"},
                {"kind": "phone", "value": "+33 6 12 34 56 78"},
                {"kind": "reference", "value": "1203"},
            ]
        },
    )

    assert recu == [
        Designation("email", "helene.petit@example.fr"),
        Designation("name", "Hélène Petit"),
        Designation("phone", "+33 6 12 34 56 78"),
        Designation("reference", "1203"),
    ]


def test_une_echeance_sans_fuseau_ne_part_pas_sur_le_fil():
    """Le service la refuserait comme une panne ; autant se le dire ici, où le défaut est réparable."""
    sans_fuseau = datetime(2026, 8, 6, 3, 0)
    client = application({"brocanto-boutique": lambda _d: Differe(sans_fuseau)})

    with pytest.raises(ValueError, match="décalage"):
        appelle(client)


def test_une_valeur_qui_n_est_pas_du_texte_n_est_pas_une_designation():
    """Un nombre là où le contrat écrit une chaîne : on l'écarte plutôt que de casser sur lui."""
    recu = []

    def sert(designations):
        recu.extend(designations)
        return Servi({"emplacements": [], "enregistrements": 0})

    reponse = appelle(
        application({"brocanto-boutique": sert}),
        corps={
            "designations": [
                {"kind": "reference", "value": 1203},
                {"kind": "email", "value": "luc.moreau@example.fr"},
            ]
        },
    )

    assert reponse.status_code == 200
    assert recu == [Designation("email", "luc.moreau@example.fr")]


def test_une_nature_hors_du_vocabulaire_ferme_est_ecartee():
    """Le contrat fixe quatre mots ; un cinquième n'est pas une nature qu'on devine."""
    recu = []

    def sert(designations):
        recu.extend(designations)
        return Servi({"emplacements": [], "enregistrements": 0})

    appelle(
        application({"brocanto-boutique": sert}),
        corps={
            "designations": [
                {"kind": "iban", "value": "FR76…"},
                {"kind": "email", "value": "luc.moreau@example.fr"},
            ]
        },
    )

    assert recu == [Designation("email", "luc.moreau@example.fr")]


def test_un_sac_vide_est_une_recherche_qui_ne_trouve_rien_pas_une_erreur():
    reponse = appelle(application({"brocanto-boutique": sert_rien}), corps={"designations": []})

    assert reponse.status_code == 200


def test_un_corps_absent_vaut_un_sac_vide():
    client = application({"brocanto-boutique": sert_rien})

    reponse = client.post(
        f"/rgpd/locate?{PARAMETRE_SYSTEME}=brocanto-boutique", headers={EN_TETE_SECRET: SECRET}
    )

    assert reponse.status_code == 200


def test_l_adapter_ne_sert_que_locate_et_read():
    """`erase` et `rectify` sont déclarables ; Brocanto ne les sert pas encore."""
    client = application({"brocanto-boutique": sert_rien})

    for capacite in ("erase", "rectify"):
        reponse = client.post(
            f"/rgpd/{capacite}?{PARAMETRE_SYSTEME}=brocanto-boutique",
            headers={EN_TETE_SECRET: SECRET},
            json={"designations": []},
        )
        assert reponse.status_code == 404


# --- `read` -----------------------------------------------------------------------------------


def sert_une_piece(contenu=b"nom;commande\n", type_mime="text/csv", nom="export.csv"):
    def lit(_designations, _droit):
        return Piece(contenu, type_mime, nom)

    return lit


def lit(client, systeme="brocanto-boutique", secret=SECRET, corps=None):
    entetes = {} if secret is None else {EN_TETE_SECRET: secret}
    return client.post(
        f"/rgpd/read?{PARAMETRE_SYSTEME}={systeme}",
        headers=entetes,
        json=corps if corps is not None else {"designations": []},
    )


def test_une_piece_part_avec_son_type_et_son_nom():
    reponse = lit(application(lectures={"brocanto-boutique": sert_une_piece()}))

    assert reponse.status_code == 200
    assert reponse.data == b"nom;commande\n"
    assert reponse.headers["Content-Type"] == "text/csv"
    assert reponse.headers["Content-Disposition"] == 'attachment; filename="export.csv"'


def test_une_piece_vide_est_une_reponse_et_non_un_204():
    """Un 204 se lirait comme une panne ; « on a regardé, il n'y a rien » est un 200 sans octets."""
    reponse = lit(application(lectures={"brocanto-boutique": sert_une_piece(contenu=b"")}))

    assert reponse.status_code == 200
    assert reponse.data == b""


def test_une_piece_sans_nom_part_quand_meme():
    """`send_file()` seul doit suffire : le service dégradera le nom sur le `system_id`."""
    reponse = lit(application(lectures={"brocanto-boutique": sert_une_piece(nom="")}))

    assert reponse.status_code == 200
    assert "Content-Disposition" not in reponse.headers


def test_le_droit_arrive_tel_quel():
    recu = []

    def lit_en_notant(_designations, droit):
        recu.append(droit)
        return Piece(b"", "text/csv", "")

    client = application(lectures={"brocanto-boutique": lit_en_notant})

    lit(client, corps={"designations": [], "right": "Portability"})

    assert recu == ["Portability"]


@pytest.mark.parametrize("declare", [None, "Toboggan", 12])
def test_un_droit_qu_on_ne_reconnait_pas_degrade_sur_le_plus_large(declare):
    """Rendre moins que demandé serait une omission silencieuse ; en rendre plus se voit."""
    recu = []

    def lit_en_notant(_designations, droit):
        recu.append(droit)
        return Piece(b"", "text/csv", "")

    corps = {"designations": []}
    if declare is not None:
        corps["right"] = declare

    lit(application(lectures={"brocanto-boutique": lit_en_notant}), corps=corps)

    assert recu == ["Access"]


def test_le_sac_arrive_a_la_lecture_comme_il_arrive_a_la_localisation():
    recu = []

    def lit_en_notant(designations, _droit):
        recu.extend(designations)
        return Piece(b"", "text/csv", "")

    lit(
        application(lectures={"brocanto-boutique": lit_en_notant}),
        corps={"designations": [{"kind": "email", "value": "helene.petit@example.fr"}]},
    )

    assert recu == [Designation("email", "helene.petit@example.fr")]


def test_un_differe_sur_read_declare_son_echeance_comme_ailleurs():
    echeance = datetime(2026, 8, 6, 3, 0, tzinfo=timezone(timedelta(hours=2)))

    reponse = lit(application(lectures={"brocanto-boutique": lambda _d, _r: Differe(echeance)}))

    assert reponse.status_code == 202
    assert reponse.get_json() == {"deadline": "2026-08-06T03:00:00+02:00"}


def test_les_deux_systemes_du_temoin_servent_read():
    """Le critère d'acceptation, dit ici : l'Adapter sert `read` pour ses deux `system_id`."""
    client = application(
        lectures={
            "brocanto-boutique": sert_une_piece(contenu=b"boutique"),
            "brocanto-journal": sert_une_piece(contenu=b"journal", type_mime="text/plain"),
        }
    )

    assert lit(client, systeme="brocanto-boutique").data == b"boutique"
    assert lit(client, systeme="brocanto-journal").data == b"journal"


def test_un_systeme_localisable_mais_non_lisible_est_un_systeme_non_servi():
    """Les deux tables sont séparées : déclarer `locate` n'engage pas `read`."""
    client = application({"brocanto-boutique": sert_rien}, lectures={})

    assert appelle(client).status_code == 200
    assert lit(client).status_code == 404


def test_le_secret_est_juge_avant_le_systeme_sur_read_aussi():
    reponse = lit(
        application(lectures={"brocanto-boutique": sert_une_piece()}),
        systeme="brocanto-crm",
        secret="faux",
    )

    assert reponse.status_code == 401
