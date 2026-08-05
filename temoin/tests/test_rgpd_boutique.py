"""`Locate` sur la base : où l'on regarde, avec quoi, et ce qu'on compte.

Aucune connexion MariaDB ici. La couture est le **compteur** : `localiser` construit ses sondes et
les fait exécuter par une fonction qu'on lui passe. Ce qui est éprouvé est donc ce que Brocanto
demande à sa base — les tables visitées et les valeurs liées — et non le comportement d'InnoDB.
"""

from adapter_rgpd import Designation, Servi

from rgpd_boutique import localiser, sondes

HELENE = Designation("email", "helene.petit@example.fr")
LUC = Designation("name", "Luc Moreau")


def compteur(par_emplacement):
    """Un compteur qui rend ce qu'on lui dit, et retient ce qu'on lui a demandé."""
    demandes = []

    def compte(emplacement, sql, params):
        demandes.append((emplacement, sql, tuple(params)))
        return par_emplacement.get(emplacement, 0)

    compte.demandes = demandes
    return compte


def emplacements_de(designations):
    return [sonde.emplacement for sonde in sondes(designations)]


def test_une_adresse_visite_les_six_tables_qui_en_portent_une():
    """Piège n° 1 : la personne n'a pas d'identifiant — une adresse est éparpillée."""
    assert emplacements_de([HELENE]) == [
        "clients",
        "commandes",
        "factures",
        "clients_ancienne_boutique",
        "newsletter",
        "messages",
    ]


def test_un_nom_visite_les_tables_qui_gardent_un_nom_recopie():
    """Dont `adresses.destinataire` et `paiements.porteur_nom`, qui nomment parfois un tiers."""
    assert emplacements_de([LUC]) == [
        "clients",
        "adresses",
        "commandes",
        "factures",
        "clients_ancienne_boutique",
        "paiements",
    ]


def test_un_telephone_ne_se_cherche_que_la_ou_il_est_stocke():
    assert emplacements_de([Designation("phone", "+33 6 12 34 56 78")]) == ["clients"]


def test_une_reference_est_une_designation_parmi_d_autres_pas_une_cle():
    assert emplacements_de([Designation("reference", "1203")]) == [
        "clients",
        "commandes",
        "factures",
        "clients_ancienne_boutique",
    ]


def test_deux_designations_de_la_meme_table_ne_font_qu_une_sonde():
    """Sans quoi une personne trouvée par son nom *et* par son adresse serait comptée deux fois."""
    sonde = next(s for s in sondes([HELENE, Designation("phone", "0612")]) if s.emplacement == "clients")

    assert sonde.params == ("helene.petit@example.fr", "0612")
    assert sonde.sql.count("%s") == 2


def test_les_valeurs_partent_liees_jamais_recopiees_dans_le_sql():
    for sonde in sondes([Designation("name", "Petit'; DROP TABLE clients; --")]):
        assert "DROP TABLE" not in sonde.sql
        assert sonde.params == ("Petit'; DROP TABLE clients; --",)


def test_un_sac_vide_ne_fait_regarder_nulle_part():
    assert sondes([]) == []

    servi = localiser([], compteur({}))

    assert servi == Servi({"emplacements": [], "enregistrements": 0})


def test_ce_qui_est_servi_dit_ou_l_on_a_regarde_y_compris_pour_rien():
    """« Regardé, rien trouvé » et « pas regardé » ne sont pas la même déclaration."""
    servi = localiser([HELENE], compteur({"clients": 2, "commandes": 1}))

    assert servi.corps == {
        "emplacements": [
            {"emplacement": "clients", "enregistrements": 2},
            {"emplacement": "commandes", "enregistrements": 1},
            {"emplacement": "factures", "enregistrements": 0},
            {"emplacement": "clients_ancienne_boutique", "enregistrements": 0},
            {"emplacement": "newsletter", "enregistrements": 0},
            {"emplacement": "messages", "enregistrements": 0},
        ],
        "enregistrements": 3,
    }


def test_chaque_sonde_est_executee_une_fois_avec_ses_propres_valeurs():
    compte = compteur({})

    localiser([HELENE], compte)

    assert [emplacement for emplacement, _sql, _params in compte.demandes] == [
        "clients",
        "commandes",
        "factures",
        "clients_ancienne_boutique",
        "newsletter",
        "messages",
    ]
    assert all(params == (HELENE.valeur,) for _e, _sql, params in compte.demandes)
