#!/usr/bin/env python3
"""Découpe les 3 254 colonnes en lots annotables par un agent, et rien de plus.

**La grappe est la table, jamais la colonne isolée.** C'est déjà la règle du plan
de sondage (#128, #129) et elle vaut deux fois ici : le § 3.3 du protocole fait
dépendre l'étiquette d'un champ libre de **ce que sa table est**, et le
commentaire de table éclaire toutes ses colonnes. Un lot qui couperait une table
en deux priverait l'annotateur d'un contexte que le moteur, lui, aura.

⚠️ **Les trois états de Paheko partent dans le même lot**, quelle que soit leur
taille cumulée. #129 les met en recensement intégral précisément pour qu'ils
soient comparables à schéma constant ; répartis sur trois annotateurs, on
confondrait « la langue a changé entre deux versions » avec « l'annotateur a
changé ». C'est la seule contrainte qui prime sur la taille des lots.

**Ce que le lot contient** : les neuf champs du pivot, et **rien d'autre**. Pas
de `strate` — c'est un pré-criblage lexical, pas une annotation, et le montrer
soufflerait la réponse. Pas de `proba_inclusion`, qui ne regarde que
l'estimateur.

Usage :  python3 outils/preparer-lots.py <répertoire-de-sortie> [--taille N]
"""
import json
import os
import sys

import commun

TAILLE = 210

# Ce que l'annotateur voit — exactement le § 1 du protocole, et pas un champ de
# plus. `id` est là pour le recollement, pas pour la décision.
CHAMPS_VISIBLES = ["id", "table", "colonne", "position", "type", "nullable",
                   "commentaire_colonne", "commentaire_table",
                   "table_referencee"]

# Ces schémas ne se séparent pas : voir l'avertissement du docstring.
INSECABLES = [("paheko", ["paheko-0.8.0", "paheko-1.0.0", "paheko-head"])]


def par_table(lignes):
    tables = []
    for l in lignes:
        cle = (l["schema_source"], l["table"])
        if not tables or tables[-1][0] != cle:
            tables.append((cle, []))
        tables[-1][1].append(l)
    return tables


def decouper(lignes, taille):
    """Des lots de ~`taille` colonnes, sans jamais scinder une table."""
    lots, courant = [], []
    for _, cols in par_table(lignes):
        if courant and len(courant) + len(cols) > taille:
            lots.append(courant)
            courant = []
        courant.extend(cols)
    if courant:
        lots.append(courant)
    # Une queue trop courte ne mérite pas son propre annotateur : elle repart
    # dans le lot précédent, quitte à le faire déborder un peu.
    if len(lots) > 1 and len(lots[-1]) < taille // 3:
        lots[-2].extend(lots.pop())
    return lots


def main():
    commun.verifier_taxonomie()
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    sortie = sys.argv[1]
    taille = TAILLE
    if "--taille" in sys.argv:
        taille = int(sys.argv[sys.argv.index("--taille") + 1])
    os.makedirs(sortie, exist_ok=True)

    groupes = []
    restants = list(commun.SCHEMAS)
    for nom, membres in INSECABLES:
        groupes.append((nom, membres))
        for m in membres:
            restants.remove(m)
    groupes.extend((s, [s]) for s in restants)

    total, n_lots = 0, 0
    index = []
    for nom, membres in sorted(groupes):
        lignes = commun.lire_annotation(membres)
        # Un groupe insécable ne se découpe pas, quelle que soit sa taille.
        lots = [lignes] if len(membres) > 1 else decouper(lignes, taille)
        for i, lot in enumerate(lots, 1):
            etiquette = f"{nom}-{i:02d}" if len(lots) > 1 else nom
            chemin = os.path.join(sortie, f"{etiquette}.jsonl")
            with open(chemin, "w", encoding="utf-8") as f:
                for l in lot:
                    f.write(json.dumps({c: l[c] for c in CHAMPS_VISIBLES},
                                       ensure_ascii=False) + "\n")
            tables = len({l["table"] for l in lot})
            index.append({"lot": etiquette, "fichier": chemin,
                          "colonnes": len(lot), "tables": tables,
                          "schemas": sorted({l["schema_source"] for l in lot})})
            print(f"  {etiquette:<16} {len(lot):>4} colonnes  {tables:>3} tables")
            total += len(lot)
            n_lots += 1

    with open(os.path.join(sortie, "index.json"), "w", encoding="utf-8") as f:
        json.dump(index, f, ensure_ascii=False, indent=2)
        f.write("\n")

    attendu = commun.lire_plan()["colonnes_a_annoter_total"]
    if total != attendu:
        sys.exit(f"✗ {total} colonnes réparties, {attendu} attendues")
    print(f"\n{n_lots} lots, {total} colonnes — aucune table scindée.")


if __name__ == "__main__":
    main()
