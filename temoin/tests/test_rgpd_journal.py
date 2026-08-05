"""`Locate` sur le journal applicatif : une autre nature de stockage, le même contrat.

Le journal n'est pas une base : pas de table, pas de requête, pas d'index — des lignes dans des
fichiers que logrotate multiplie. C'est pour cela qu'il est servi par le même `Adapter` : un contrat
qui ne saurait dire que « SELECT » n'aurait pas été éprouvé.
"""

from datetime import datetime, timedelta, timezone

from adapter_rgpd import Designation, Differe, Servi

from rgpd_journal import localiser, prochaine_fenetre_de_nuit

PARIS = timezone(timedelta(hours=2))

LIGNES_COURANTES = """\
2026-05-02 09:12:04 92.184.3.7 helene.petit@example.fr GET / 200
2026-05-02 09:12:31 92.184.3.7 helene.petit@example.fr POST /connexion 302
2026-05-02 09:13:02 92.184.3.7 - GET /annonce/4 200
2026-05-02 10:41:55 88.120.9.44 luc.moreau@example.fr GET /mon-compte 200
"""

LIGNES_TOURNEES = """\
2026-04-28 22:03:10 92.184.3.7 - GET /newsletter/desinscription?courriel=helene.petit@example.fr 200
2026-04-28 22:07:44 10.0.0.9 - GET /admin/clients?cle=brocanto2019 200
"""


def journal(dossier, courant=LIGNES_COURANTES, tourne=LIGNES_TOURNEES):
    (dossier / "brocanto.log").write_text(courant, encoding="utf-8")
    if tourne is not None:
        (dossier / "brocanto.log.1").write_text(tourne, encoding="utf-8")
    return dossier


def test_les_lignes_sont_comptees_fichier_par_fichier(tmp_path):
    servi = localiser([Designation("email", "helene.petit@example.fr")], journal(tmp_path))

    assert servi == Servi(
        {
            "emplacements": [
                {"emplacement": "brocanto.log", "lignes": 2},
                {"emplacement": "brocanto.log.1", "lignes": 1},
            ],
            "lignes": 3,
        }
    )


def test_les_fichiers_tournes_comptent_autant_que_le_courant(tmp_path):
    """Piège n° 11 : c'est le fichier tourné qui garde l'adresse, l'IP et l'heure côte à côte."""
    servi = localiser([Designation("email", "helene.petit@example.fr")], journal(tmp_path))

    assert {"emplacement": "brocanto.log.1", "lignes": 1} in servi.corps["emplacements"]


def test_l_adresse_dans_une_url_de_desinscription_compte_comme_les_autres(tmp_path):
    """Elle y est parce que le lien ne porte aucun jeton — piège n° 16, lu depuis le journal."""
    servi = localiser(
        [Designation("email", "helene.petit@example.fr")],
        journal(tmp_path, courant="", tourne=LIGNES_TOURNEES),
    )

    assert servi.corps["lignes"] == 1


def test_la_casse_ne_fait_pas_perdre_une_ligne(tmp_path):
    """Piège n° 2 : la même personne s'écrit `Jean.Dupont@Example.fr` un jour sur deux."""
    servi = localiser([Designation("email", "HELENE.PETIT@EXAMPLE.FR")], journal(tmp_path))

    assert servi.corps["lignes"] == 3


def test_un_sac_vide_ne_fait_lire_aucun_fichier(tmp_path):
    servi = localiser([], journal(tmp_path))

    assert servi == Servi({"emplacements": [], "lignes": 0})


def test_un_journal_absent_se_dit_plutot_qu_il_ne_casse(tmp_path):
    servi = localiser([Designation("email", "helene.petit@example.fr")], tmp_path / "logs")

    assert servi == Servi({"emplacements": [], "lignes": 0})


def test_ce_qui_n_est_pas_une_ligne_de_journal_n_est_pas_lu(tmp_path):
    journal(tmp_path)
    (tmp_path / "brocanto.log.gz").write_bytes(b"\x1f\x8bhelene.petit@example.fr")

    servi = localiser([Designation("email", "helene.petit@example.fr")], tmp_path)

    assert [e["emplacement"] for e in servi.corps["emplacements"]] == [
        "brocanto.log",
        "brocanto.log.1",
    ]


def test_au_dela_du_seuil_le_travail_se_declare_au_lieu_de_tenir_la_connexion(tmp_path):
    maintenant = datetime(2026, 8, 5, 14, 30, tzinfo=PARIS)

    differe = localiser(
        [Designation("email", "helene.petit@example.fr")],
        journal(tmp_path),
        seuil=10,
        maintenant=maintenant,
    )

    assert differe == Differe(datetime(2026, 8, 6, 3, 0, tzinfo=PARIS))


def test_l_echeance_declaree_est_la_prochaine_passe_du_cron_de_nuit():
    avant = datetime(2026, 8, 5, 1, 15, tzinfo=PARIS)
    apres = datetime(2026, 8, 5, 3, 0, tzinfo=PARIS)

    assert prochaine_fenetre_de_nuit(avant) == datetime(2026, 8, 5, 3, 0, tzinfo=PARIS)
    assert prochaine_fenetre_de_nuit(apres) == datetime(2026, 8, 6, 3, 0, tzinfo=PARIS)


def test_l_echeance_porte_un_decalage_explicite():
    """Sans lui, elle vaudrait deux heures de moins d'un serveur à l'autre — le service la refuse."""
    echeance = prochaine_fenetre_de_nuit(datetime(2026, 8, 5, 14, 30, tzinfo=PARIS))

    assert echeance.utcoffset() is not None
    assert echeance.isoformat() == "2026-08-06T03:00:00+02:00"
