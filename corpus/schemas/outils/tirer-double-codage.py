#!/usr/bin/env python3
"""Tire les 300 colonnes du double codage et fabrique le cahier de seconde passe.

⚠️ **À lancer AVANT que l'annotation commence.** Un échantillon tiré après coup
se choisit, même de bonne foi, en connaissance de ce qu'il contient. Le tirage
est figé par un seed ; il se rejoue à l'identique et se vérifie.

**Stratifié par schéma**, comme l'exige le § 4 du protocole : aucun schéma ne
doit être absent du double codage. L'allocation est proportionnelle au nombre de
colonnes à annoter, avec un plancher de 1, et les restes sont répartis par la
méthode du plus fort reste pour tomber exactement sur 300.

**Ce que ce script ne fait jamais** : recopier une étiquette. Le cahier de
seconde passe ne porte que les neuf champs du pivot — ceux que le moteur
recevra — et des `categorie`/`motif` vides. C'est la blindness, garantie par
construction plutôt que par la discipline : le fichier ne *contient pas* de quoi
tricher.

La passe machine n'a rien à re-remplir : ses étiquettes sur ces colonnes sont
déjà dans `annotation/`, et `accord.py` va les y chercher.

## Le tirage de la tentative 2 — 2026-08-09 (#145, #149)

⚠️ **Seed neuf, sur le complémentaire des 300 déjà brûlées.** Les 300 colonnes
de la tentative 1 ont été vues par l'annotateur humain : les retirer au sort
mesurerait un souvenir, pas un accord. Elles sont donc **exclues** du tirage, et
les 300 nouvelles se tirent parmi les 2 954 restantes.

**Le plan de sondage de [#129](https://github.com/AmauryTISSOT/microservice_rgpd/issues/129)
ne bouge pas** — mêmes strates, mêmes probabilités d'inclusion, même allocation
proportionnelle par schéma avec plancher de 1. Ce qui change est le seed et
l'ensemble tirable, rien d'autre.

⚠️ **Ce script ne touche jamais `annotation/`.** C'est
`outils/echantillonner.py` qui **écrase** ce dossier (constaté par #130), et il
n'est pas appelé ici : les 3 254 étiquettes machine et le premier échantillon
sont conservés et publiés. Le script le **vérifie** plutôt que de le promettre.

⚠️ **La fuite d'aveuglement fermée par #142 reste fermée** : le cahier n'expose
ni `strate` ni `proba_inclusion`.

Usage :  python3 outils/tirer-double-codage.py [--refaire]
"""
import os
import random
import sys

import commun

# Seed neuf : celui de la tentative 1 valait 20260809 et a brûlé ses 300 lignes.
SEED = 20260810
CIBLE = 300

ECHANTILLON = commun.echantillon()
CAHIER_2 = commun.reference_humaine()
# Les identifiants déjà vus par l'annotateur, quelle que soit la tentative.
BRULEES = [commun.echantillon(n) for n in range(1, commun.TENTATIVE_COURANTE)]

# ⚠️ Ni `strate` ni `proba_inclusion` (correction du 2026-08-09, § 4 du
# protocole). La strate est le verdict d'un pré-criblage lexical : la montrer,
# c'est donner une part de la réponse avant que l'annotateur tranche. Les deux
# champs restent dans `annotation/` et se rejoignent par `id` au moment de
# pondérer. Ne les remettez pas ici « pour la commodité ».
CHAMPS_PIVOT = ["id", "schema_source", "table", "colonne", "position", "type",
                "nullable", "commentaire_colonne", "commentaire_table",
                "table_referencee"]


def allouer(effectifs, cible):
    """Plus fort reste, avec un plancher de 1 par schéma."""
    total = sum(effectifs.values())
    exacts = {n: cible * e / total for n, e in effectifs.items()}
    parts = {n: max(1, int(v)) for n, v in exacts.items()}

    # Le plancher a pu faire dépasser la cible ; on rend au plus faible reste.
    while sum(parts.values()) > cible:
        candidat = min((n for n in parts if parts[n] > 1),
                       key=lambda n: exacts[n] - int(exacts[n]))
        parts[candidat] -= 1

    restes = sorted(exacts, key=lambda n: exacts[n] - int(exacts[n]), reverse=True)
    i = 0
    while sum(parts.values()) < cible:
        n = restes[i % len(restes)]
        if parts[n] < effectifs[n]:
            parts[n] += 1
        i += 1
    return parts


def main():
    commun.verifier_taxonomie()

    if os.path.exists(CAHIER_2) and "--refaire" not in sys.argv:
        deja = commun.lire_jsonl(CAHIER_2)
        faites = len(commun.annotees(deja))
        sys.exit(f"{CAHIER_2} existe déjà ({faites} colonnes codées).\n"
                 "Le retirer effacerait le second codage. Utiliser --refaire "
                 "en connaissance de cause.")

    lignes = commun.lire_annotation()
    empreinte_avant = [(l["id"], l["categorie"]) for l in lignes]

    # ⚠️ Le complémentaire, et c'est tout l'objet de ce tirage : une colonne déjà
    # vue par l'annotateur ne mesure plus un accord, elle mesure un souvenir.
    brulees = set()
    for chemin in BRULEES:
        if not os.path.exists(chemin):
            sys.exit(f"absent : {chemin} — sans lui, le complémentaire ne peut "
                     "pas être calculé et le tirage rejouerait des colonnes "
                     "déjà vues.")
        brulees.update(l["id"] for l in commun.lire_jsonl(chemin))
    print(f"{len(brulees)} colonnes déjà brûlées, exclues du tirage.")

    tirables = [l for l in lignes if l["id"] not in brulees]
    if len(tirables) != len(lignes) - len(brulees):
        sys.exit("des identifiants brûlés sont introuvables dans annotation/ : "
                 "les deux fichiers ne parlent pas du même corpus.")
    print(f"{len(tirables)} colonnes tirables sur {len(lignes)}.")

    par_schema = {}
    for l in tirables:
        par_schema.setdefault(l["schema_source"], []).append(l)

    effectifs = {n: len(v) for n, v in par_schema.items()}
    parts = allouer(effectifs, CIBLE)

    tire = []
    for nom in commun.SCHEMAS:
        rng = random.Random(f"{SEED}:{nom}")
        candidats = sorted(par_schema[nom], key=lambda l: l["id"])
        tire.extend(rng.sample(candidats, parts[nom]))
        print(f"  {nom:<14} {parts[nom]:>3} / {effectifs[nom]:<4} colonnes")

    tire.sort(key=lambda l: l["id"])
    assert len(tire) == CIBLE, len(tire)
    assert len({l['id'] for l in tire}) == CIBLE, "doublon dans le tirage"

    vierge = []
    for l in tire:
        o = {c: l[c] for c in CHAMPS_PIVOT}
        o.update(categorie=None, motif=None, annotateur=None, date=None)
        vierge.append(o)

    # ⚠️ Vérifié, pas promis : `echantillonner.py` écrase `annotation/` (#130),
    # et le tirage neuf ne doit toucher ni les étiquettes machine ni le premier
    # échantillon. On relit le corpus et on compare avant d'écrire quoi que ce
    # soit.
    empreinte_apres = [(l["id"], l["categorie"]) for l in commun.lire_annotation()]
    if empreinte_apres != empreinte_avant:
        sys.exit("✗ annotation/ a bougé pendant le tirage — les 3 254 étiquettes "
                 "machine doivent être conservées intactes.")

    commun.ecrire_jsonl(ECHANTILLON, [{"id": l["id"]} for l in tire])
    commun.ecrire_jsonl(CAHIER_2, vierge)

    assert not set(l["id"] for l in tire) & brulees, "une brûlée est ressortie"

    print(f"\nseed {SEED} — {CIBLE} colonnes, tentative {commun.TENTATIVE_COURANTE}")
    print(f"  {ECHANTILLON}  (les identifiants, pour vérifier le tirage)")
    print(f"  {CAHIER_2}  (cahier vierge de la passe humaine)")
    print("\n⚠️  La passe humaine se remplit SANS ouvrir annotation/ : on y "
          "verrait les étiquettes machine.")
    print("⚠️  Aucune des colonnes de la tentative 1 n'est ressortie — le "
          "complémentaire est ce qui empêche de mesurer un souvenir.")
    print("⚠️  #145 n'accorde qu'UNE tentative : « deux, si un défaut "
          "d'instrument est constaté » est indiscernable de « pas de limite ».")


if __name__ == "__main__":
    main()
