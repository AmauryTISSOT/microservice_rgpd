"""`Locate` sur le journal applicatif : une autre nature de stockage, le même contrat.

Le journal n'est pas une base : pas de table, pas de requête, pas d'index — des lignes dans des
fichiers que logrotate multiplie. C'est pour cela qu'il est servi par le même `Adapter` : un contrat
qui ne saurait dire que « SELECT » n'aurait pas été éprouvé.
"""

from datetime import datetime, timedelta, timezone

from adapter_rgpd import Designation, Differe, Servi

from rgpd_journal import lire, localiser, prochaine_fenetre_de_nuit

PARIS = timezone(timedelta(hours=2))

ZERO = Servi({"certain": [], "reserved": []})
"""Le zéro n'a qu'une forme, et le journal n'en a pas d'autre à rendre."""

LIGNES_COURANTES = """\
2026-05-02 09:12:04 92.184.3.7 helene.petit@example.fr GET / 200
2026-05-02 09:12:31 92.184.3.7 helene.petit@example.fr POST /connexion 302
2026-05-02 09:13:02 92.184.3.7 - GET /annonce/4 200
2026-05-02 10:41:55 88.120.9.44 Jean.Dupont@Example.fr GET /mon-compte 200
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


def test_les_fichiers_portant_l_adresse_sont_nommes_un_a_un(tmp_path):
    """La référence est le fichier : c'est le grain auquel un humain relit un journal, avec `grep`."""
    servi = localiser([Designation("email", "helene.petit@example.fr")], journal(tmp_path))

    assert servi == Servi(
        {"certain": ["brocanto.log", "brocanto.log.1"], "reserved": []}
    )


def test_les_fichiers_tournes_valent_autant_que_le_courant(tmp_path):
    """Piège n° 11 : c'est le fichier tourné qui garde l'adresse, l'IP et l'heure côte à côte."""
    servi = localiser([Designation("email", "helene.petit@example.fr")], journal(tmp_path))

    assert "brocanto.log.1" in servi.corps["certain"]


def test_un_fichier_sans_aucune_ligne_de_la_personne_n_est_pas_rattache(tmp_path):
    """Une référence rendue est un rattachement : la rendre à vide en ferait un faux."""
    servi = localiser(
        [Designation("email", "helene.petit@example.fr")],
        journal(tmp_path, courant="", tourne=LIGNES_TOURNEES),
    )

    assert servi.corps["certain"] == ["brocanto.log.1"]


def test_l_adresse_dans_une_url_de_desinscription_rattache_comme_les_autres(tmp_path):
    """Elle y est parce que le lien ne porte aucun jeton — piège n° 16, lu depuis le journal."""
    servi = localiser(
        [Designation("email", "helene.petit@example.fr")],
        journal(tmp_path, courant="", tourne=LIGNES_TOURNEES),
    )

    assert servi.corps["certain"] == ["brocanto.log.1"]


def test_la_casse_fait_perdre_le_fichier_et_c_est_tout_le_probleme(tmp_path):
    """Le piège du dossier, en un test.

    La base compare sans distinguer la casse et trouve Jean Dupont sous n'importe quelle écriture ;
    ce fichier ne compare rien du tout — il contient une chaîne, ou il ne la contient pas. La même
    désignation ouvre donc l'un et pas l'autre, et personne chez Brocanto ne l'avait vu.

    Ce qui répare le trou n'est pas un code : c'est la réserve que la base rend sur son homonyme,
    l'adresse qu'elle propose dans la casse où elle la stocke, et l'humain qui la rattache.
    """
    servi = localiser([Designation("email", "jean.dupont@example.fr")], journal(tmp_path))

    assert servi == ZERO


def test_l_adresse_dans_la_casse_du_journal_ouvre_ce_qu_aucune_autre_n_ouvrait(tmp_path):
    """La suite du même test : c'est la désignation venue de l'arbitrage qui trouve."""
    servi = localiser([Designation("email", "Jean.Dupont@Example.fr")], journal(tmp_path))

    assert servi.corps["certain"] == ["brocanto.log"]


def test_seule_une_adresse_se_cherche_dans_un_journal(tmp_path):
    """Une ligne ne porte qu'une adresse : y chercher « 1203 » rattacherait horaires et statuts.

    Ce qu'on ne peut pas chercher, on ne le rattache pas — une référence achetée par un chiffre qui
    traîne dans une date aurait l'air d'un fait.
    """
    servi = localiser(
        [Designation("reference", "2026"), Designation("name", "Hélène Petit")], journal(tmp_path)
    )

    assert servi == ZERO


def test_le_journal_ne_reserve_jamais_rien(tmp_path):
    """Une ligne portant l'adresse cherchée est la personne : sans quoi ce ne serait pas sa session.

    Le doute du journal n'est pas dans ce qu'il rend, il est dans ce qu'il ne rend pas.
    """
    servi = localiser([Designation("email", "helene.petit@example.fr")], journal(tmp_path))

    assert servi.corps["reserved"] == []


def test_un_sac_vide_ne_fait_lire_aucun_fichier(tmp_path):
    assert localiser([], journal(tmp_path)) == ZERO


def test_un_journal_absent_se_dit_plutot_qu_il_ne_casse(tmp_path):
    servi = localiser([Designation("email", "helene.petit@example.fr")], tmp_path / "logs")

    assert servi == ZERO


def test_ce_qui_n_est_pas_une_ligne_de_journal_n_est_pas_lu(tmp_path):
    journal(tmp_path)
    (tmp_path / "brocanto.log.gz").write_bytes(b"\x1f\x8bhelene.petit@example.fr")

    servi = localiser([Designation("email", "helene.petit@example.fr")], tmp_path)

    assert servi.corps["certain"] == ["brocanto.log", "brocanto.log.1"]


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


# --- `read` : les mêmes lignes, recopiées jusqu'au bout ------------------------------------------


def test_les_lignes_portant_l_adresse_sont_recopiees_telles_quelles(tmp_path):
    """Ce qu'un opérateur relira est ce qu'un `grep` lui aurait rendu ici, et rien d'autre."""
    piece = lire([Designation("email", "helene.petit@example.fr")], journal(tmp_path), "Access")

    assert piece.contenu.decode("utf-8") == (
        "2026-05-02 09:12:04 92.184.3.7 helene.petit@example.fr GET / 200\n"
        "2026-05-02 09:12:31 92.184.3.7 helene.petit@example.fr POST /connexion 302\n"
        "2026-04-28 22:03:10 92.184.3.7 - "
        "GET /newsletter/desinscription?courriel=helene.petit@example.fr 200\n"
    )


def test_la_piece_du_journal_dit_ce_qu_elle_est(tmp_path):
    piece = lire([Designation("email", "helene.petit@example.fr")], journal(tmp_path), "Access")

    assert piece.type_mime == "text/plain; charset=utf-8"
    assert piece.nom == "brocanto-journal.log"


def test_la_casse_perd_ici_ce_que_la_base_aurait_trouve(tmp_path):
    """⚠️ Le piège du dossier, et il vaut pour `read` comme pour `locate` : rien n'est normalisé."""
    piece = lire([Designation("email", "jean.dupont@example.fr")], journal(tmp_path), "Access")

    assert piece.contenu == b""

    exacte = lire([Designation("email", "Jean.Dupont@Example.fr")], journal(tmp_path), "Access")

    assert b"Jean.Dupont@Example.fr" in exacte.contenu


def test_une_adresse_qu_aucune_ligne_ne_porte_rend_une_piece_vide(tmp_path):
    piece = lire([Designation("email", "inconnue@example.fr")], journal(tmp_path), "Access")

    assert piece.contenu == b""


def test_un_sac_sans_adresse_n_ouvre_aucun_fichier(tmp_path):
    piece = lire([Designation("name", "Hélène Petit")], journal(tmp_path), "Access")

    assert piece.contenu == b""


def test_le_droit_ne_restreint_rien_dans_un_journal(tmp_path):
    """Une ligne de journal est un seul objet : elle n'a pas de colonnes à trier par article."""
    dossier = journal(tmp_path)
    helene = [Designation("email", "helene.petit@example.fr")]

    assert lire(helene, dossier, "Portability").contenu == lire(helene, dossier, "Access").contenu


def test_un_volume_trop_gros_differe_la_lecture_comme_il_differe_la_localisation(tmp_path):
    """Le seuil tient à la nature du stockage, pas à la capacité appelée."""
    maintenant = datetime(2026, 8, 5, 14, 30, tzinfo=PARIS)

    differe = lire(
        [Designation("email", "helene.petit@example.fr")],
        journal(tmp_path),
        "Access",
        seuil=10,
        maintenant=maintenant,
    )

    assert differe == Differe(datetime(2026, 8, 6, 3, 0, tzinfo=PARIS))
