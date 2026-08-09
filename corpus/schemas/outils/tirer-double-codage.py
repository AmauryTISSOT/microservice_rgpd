#!/usr/bin/env python3
"""Tire les 300 colonnes du double codage, et fabrique le cahier du second codeur.

⚠️ **À lancer AVANT que l'annotation commence.** Un échantillon tiré après coup
se choisit, même de bonne foi, en connaissance de ce qu'il contient. Le tirage
est figé par une graine ; il se rejoue à l'identique et se vérifie.

**Stratifié par schéma**, comme l'exige le § 4 du protocole : aucun schéma ne
doit être absent du double codage. L'allocation est proportionnelle au nombre de
colonnes à annoter, avec un plancher de 1, et les restes sont répartis par la
méthode du plus fort reste pour tomber exactement sur 300.

**Ce que ce script ne fait jamais** : recopier une étiquette. Le cahier du second
codeur ne porte que les neuf champs du pivot — ceux que le moteur recevra — et
des `categorie`/`motif` vides. C'est la blindness, garantie par construction
plutôt que par la discipline : le fichier ne *contient pas* de quoi tricher.

Le premier codeur, lui, n'a rien à re-remplir : ses étiquettes sur ces 300
colonnes sont déjà dans `annotation/`, et `accord.py` va les y chercher. Le lui
faire recoder mesurerait sa propre constance d'un jour à l'autre, pas l'accord
entre deux personnes.

Usage :  python3 outils/tirer-double-codage.py [--refaire]
"""
import os
import random
import sys

import commun

GRAINE = 20260809  # figée, et distincte de celle du plan de sondage
CIBLE = 300

ECHANTILLON = os.path.join(commun.DOUBLE_CODAGE, "echantillon.jsonl")
CAHIER_2 = os.path.join(commun.DOUBLE_CODAGE, "codeur-2.jsonl")

CHAMPS_PIVOT = ["id", "schema_source", "table", "colonne", "position", "type",
                "nullable", "commentaire_colonne", "commentaire_table",
                "table_referencee", "strate", "proba_inclusion"]


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
    if commun.annotees(lignes):
        print("⚠️  l'annotation a déjà commencé : le tirage aurait dû être fait "
              "avant. Il reste reproductible, mais la garantie « tiré à "
              "l'aveugle » est perdue et doit être signalée au journal.",
              file=sys.stderr)

    par_schema = {}
    for l in lignes:
        par_schema.setdefault(l["schema_source"], []).append(l)

    effectifs = {n: len(v) for n, v in par_schema.items()}
    parts = allouer(effectifs, CIBLE)

    tire = []
    for nom in commun.SCHEMAS:
        rng = random.Random(f"{GRAINE}:{nom}")
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

    commun.ecrire_jsonl(ECHANTILLON, [{"id": l["id"]} for l in tire])
    commun.ecrire_jsonl(CAHIER_2, vierge)

    print(f"\ngraine {GRAINE} — {CIBLE} colonnes")
    print(f"  {ECHANTILLON}  (les identifiants, pour vérifier le tirage)")
    print(f"  {CAHIER_2}  (cahier vierge du second codeur)")
    print("\n⚠️  Le second codeur remplit codeur-2.jsonl SANS ouvrir "
          "annotation/ : il y verrait les étiquettes du premier.")


if __name__ == "__main__":
    main()
