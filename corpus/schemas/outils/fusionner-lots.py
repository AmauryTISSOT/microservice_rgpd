#!/usr/bin/env python3
"""Recolle les étiquettes produites par lot dans les fichiers d'annotation.

Le recollement se fait **par `id`**, jamais par position : un annotateur qui a
sauté, dédoublé ou réordonné une ligne doit être détecté, pas silencieusement
recopié de travers. Une étiquette qui ne retombe pas sur un `id` connu est une
erreur, pas une ligne à ignorer.

⚠️ **Ce script refuse d'écraser une étiquette déjà posée.** Le cas normal est un
fichier d'annotation vierge ; réécrire une étiquette existante serait soit un
double traitement du même lot, soit l'effacement d'une correction humaine. Les
deux se signalent plutôt qu'ils ne se devinent. `--forcer` lève ce refus.

Usage :  python3 outils/fusionner-lots.py <répertoire-des-sorties> [--forcer]
"""
import glob
import json
import os
import sys

import commun

ATTENDUS = {"id", "categorie", "motif"}


def lire_sorties(repertoire):
    """Rend {id: (categorie, motif, lot)} et la liste des anomalies de forme."""
    etiquettes, anomalies = {}, []
    fichiers = sorted(glob.glob(os.path.join(repertoire, "*.jsonl")))
    if not fichiers:
        sys.exit(f"aucun .jsonl dans {repertoire}")
    for chemin in fichiers:
        lot = os.path.basename(chemin)[:-6]
        for n, brut in enumerate(open(chemin, encoding="utf-8"), 1):
            brut = brut.strip()
            if not brut:
                continue
            try:
                o = json.loads(brut)
            except json.JSONDecodeError as e:
                anomalies.append(f"{lot}:{n} JSON illisible — {e}")
                continue
            if not ATTENDUS <= set(o):
                anomalies.append(f"{lot}:{n} champs manquants : "
                                 f"{sorted(ATTENDUS - set(o))}")
                continue
            if o["id"] in etiquettes:
                anomalies.append(f"{lot}:{n} id déjà vu dans "
                                 f"{etiquettes[o['id']][2]} : {o['id']}")
                continue
            etiquettes[o["id"]] = (o["categorie"], o["motif"], lot)
    return etiquettes, anomalies


def main():
    commun.verifier_taxonomie()
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    repertoire = sys.argv[1]
    forcer = "--forcer" in sys.argv

    etiquettes, anomalies = lire_sorties(repertoire)
    corpus = commun.lire_annotation()
    connus = {l["id"] for l in corpus}

    orphelines = sorted(set(etiquettes) - connus)
    manquantes = sorted(connus - set(etiquettes))
    for o in orphelines[:20]:
        anomalies.append(f"étiquette sur un id inconnu du corpus : {o}")

    if anomalies:
        print(f"{len(anomalies)} anomalie(s) :", file=sys.stderr)
        for a in anomalies[:40]:
            print(f"  ✗ {a}", file=sys.stderr)
        sys.exit(1)

    deja = [l["id"] for l in corpus
            if l["categorie"] is not None and l["id"] in etiquettes]
    if deja and not forcer:
        sys.exit(f"✗ {len(deja)} colonne(s) portent déjà une étiquette "
                 f"(ex. {deja[0]}). Fusionner les écraserait. --forcer pour "
                 "passer outre, en sachant ce que ça détruit.")

    ecrites = 0
    for nom in commun.SCHEMAS:
        chemin = os.path.join(commun.ANNOTATION, f"{nom}.annotation.jsonl")
        lignes = commun.lire_jsonl(chemin)
        for l in lignes:
            if l["id"] in etiquettes:
                cat, motif, lot = etiquettes[l["id"]]
                l["categorie"] = cat
                # Un motif vide se range en None : `valider.py` teste la présence
                # du motif, et "" passerait pour rempli dans un JSON.
                l["motif"] = motif if (motif or "").strip() else None
                l["annotateur"] = f"agent:{lot}"
                l["date"] = "2026-08-09"
                ecrites += 1
        commun.ecrire_jsonl(chemin, lignes)

    print(f"{ecrites} étiquettes recollées sur {len(corpus)} colonnes.")
    if manquantes:
        print(f"⚠️  {len(manquantes)} colonne(s) sans étiquette — lots "
              f"incomplets. Ex. : {', '.join(manquantes[:5])}")
    print("\nLancer maintenant :  python3 outils/valider.py")


if __name__ == "__main__":
    main()
