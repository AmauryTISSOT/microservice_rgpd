"""`Locate` sur la base : où l'on regarde, avec quoi, et ce qu'on rattache ou non.

Aucune connexion MariaDB ici. La couture est le **lecteur** : `localiser` construit ses sondes et
les fait exécuter par une fonction qu'on lui passe. Ce qui est éprouvé est donc ce que Brocanto
demande à sa base — les tables visitées, les valeurs liées, et la ligne de partage entre ce qu'elle
rattache et ce dont elle doute — et non le comportement d'InnoDB.
"""

from adapter_rgpd import Designation, Servi

from rgpd_boutique import localiser, sondes

HELENE = Designation("email", "helene.petit@example.fr")
LUC = Designation("name", "Luc Moreau")
JEAN = Designation("name", "Jean Dupont")


def lecteur(par_sonde):
    """Un lecteur qui rend les lignes qu'on lui dit, et retient ce qu'on lui a demandé.

    Les lignes sont indexées par `(table, certaine)` : deux sondes visitent désormais la même table
    sans poser la même question, et confondre leurs réponses effacerait tout ce qui est en jeu ici.
    """
    demandes = []

    def lit(sonde):
        demandes.append(sonde)
        return par_sonde.get((sonde.emplacement, sonde.certaine), [])

    lit.demandes = demandes
    return lit


def ligne(reference, courriel=None):
    return {"reference": reference, "courriel": courriel}


def emplacements_de(designations, certaine=None):
    return [
        sonde.emplacement
        for sonde in sondes(designations)
        if certaine is None or sonde.certaine == certaine
    ]


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


def test_un_nom_ne_pose_jamais_qu_une_question_nominale():
    """Une adresse identifie ; un nom ne fait que nommer. Les deux sondes ne se mélangent pas."""
    assert emplacements_de([LUC], certaine=True) == []
    assert emplacements_de([HELENE], certaine=False) == []


def test_un_telephone_ne_se_cherche_que_la_ou_il_est_stocke():
    assert emplacements_de([Designation("phone", "+33 6 12 34 56 78")]) == ["clients"]


def test_une_reference_est_une_designation_parmi_d_autres_pas_une_cle():
    assert emplacements_de([Designation("reference", "1203")]) == [
        "clients",
        "commandes",
        "factures",
        "clients_ancienne_boutique",
    ]


def test_deux_designations_exactes_de_la_meme_table_ne_font_qu_une_sonde():
    sonde = next(
        s
        for s in sondes([HELENE, Designation("phone", "0612")])
        if s.emplacement == "clients"
    )

    assert sonde.params == ("helene.petit@example.fr", "0612")
    assert sonde.sql.count("%s") == 2


def test_une_table_cherchee_sous_une_adresse_et_sous_un_nom_pose_deux_questions():
    """Le dénombrement l'interdisait — il aurait compté deux fois la même personne.

    Des références se dédoublonnent ; des comptes s'additionnent. C'est ce changement de forme qui
    rend la séparation possible, et c'est cette séparation qui rend le doute visible.
    """
    posees = [(s.emplacement, s.certaine) for s in sondes([HELENE, JEAN])]

    assert ("clients", True) in posees
    assert ("clients", False) in posees


def test_les_valeurs_partent_liees_jamais_recopiees_dans_le_sql():
    for sonde in sondes([Designation("name", "Petit'; DROP TABLE clients; --")]):
        assert "DROP TABLE" not in sonde.sql
        assert sonde.params == ("Petit'; DROP TABLE clients; --",)


def test_un_sac_vide_ne_fait_regarder_nulle_part():
    assert sondes([]) == []

    assert localiser([], lecteur({})) == Servi({"certain": [], "reserved": []})


def test_ce_qui_est_trouve_sous_une_adresse_est_rattache_sans_reserve():
    """La valeur est exacte, et c'est la personne qui nous l'a donnée : nous en répondons."""
    servi = localiser(
        [HELENE],
        lecteur({
            ("clients", True): [ligne(1180)],
            ("commandes", True): [ligne("CMD-2026-0412")],
            ("newsletter", True): [ligne("helene.petit@example.fr")],
        }),
    )

    assert servi.corps == {
        "certain": ["clients#1180", "commandes#CMD-2026-0412", "newsletter#helene.petit@example.fr"],
        "reserved": [],
    }


def test_deux_homonymes_font_deux_reserves_motivees_et_aucun_rattachement():
    """Le cas dur, en toutes lettres : deux « Jean Dupont », et rien ici ne les départage.

    Ce n'est pas à nous de trancher, et surtout pas en silence. On rend les deux lignes, on dit
    pourquoi on doute, et on propose l'adresse de chacune — c'est le seul champ que le service
    reprendra, une fois qu'un humain aura tranché.
    """
    servi = localiser(
        [JEAN],
        lecteur({
            ("clients", False): [
                ligne(1203, "Jean.Dupont@Example.fr"),
                ligne(4417, "j.dupont1954@example.fr"),
            ],
        }),
    )

    assert servi.corps["certain"] == []
    assert servi.corps["reserved"] == [
        {
            "reference": "clients#1203",
            "reason": "2 lignes de « clients » portent ce nom. Un compte au même nom, "
            "et rien dans « clients » n'impose l'unicité d'un nom.",
            "designations": [{"kind": "email", "value": "Jean.Dupont@Example.fr"}],
        },
        {
            "reference": "clients#4417",
            "reason": "2 lignes de « clients » portent ce nom. Un compte au même nom, "
            "et rien dans « clients » n'impose l'unicité d'un nom.",
            "designations": [{"kind": "email", "value": "j.dupont1954@example.fr"}],
        },
    ]


def test_une_ligne_seule_sous_un_nom_reste_une_reserve():
    """Un nom ne désigne personne à lui seul, même quand il n'en trouve qu'une.

    Rattacher l'unique ligne trouvée ferait dire à l'absence d'homonyme quelque chose qu'elle ne dit
    pas : qu'aucun autre Jean Dupont n'existe *dans cette base*, jamais qu'il n'en existe pas.
    """
    servi = localiser([JEAN], lecteur({("clients", False): [ligne(1203, "jd@example.fr")]}))

    assert servi.corps["certain"] == []
    assert servi.corps["reserved"][0]["reason"].startswith("Trouvée sur le seul nom.")


def test_une_reserve_sans_adresse_a_proposer_reste_locale_et_opaque():
    """`adresses` et `paiements` ne portent aucune adresse électronique — et n'ont rien de plus à dire.

    Elle s'arbitrera quand même : ce qu'elle apporte est une ligne de plus au dossier, pas une
    désignation de plus au sac.
    """
    servi = localiser([LUC], lecteur({("paiements", False): [ligne("psp_7f31c0")]}))

    reserve = servi.corps["reserved"][0]

    assert reserve["reference"] == "paiements#psp_7f31c0"
    assert reserve["designations"] == []


def test_une_ligne_trouvee_des_deux_facons_n_est_plus_un_doute():
    """Trouvée sous l'adresse *et* sous le nom : la question est tranchée, la réserve tombe.

    Faire arbitrer un doute déjà levé userait l'attention qu'on demande aux vrais.
    """
    servi = localiser(
        [HELENE, Designation("name", "Hélène Petit")],
        lecteur({
            ("clients", True): [ligne(1180)],
            ("clients", False): [ligne(1180, "helene.petit@example.fr")],
        }),
    )

    assert servi.corps == {"certain": ["clients#1180"], "reserved": []}


def test_rien_trouve_est_un_zero_et_un_seul():
    """Le contrat ne demande pas de distinguer « rien trouvé » de « rien à trouver »."""
    servi = localiser([HELENE], lecteur({}))

    assert servi == Servi({"certain": [], "reserved": []})


def test_chaque_sonde_est_executee_une_fois_avec_ses_propres_valeurs():
    lit = lecteur({})

    localiser([HELENE], lit)

    assert [sonde.emplacement for sonde in lit.demandes] == [
        "clients",
        "commandes",
        "factures",
        "clients_ancienne_boutique",
        "newsletter",
        "messages",
    ]
    assert all(sonde.params == (HELENE.valeur,) for sonde in lit.demandes)
