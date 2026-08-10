"""Coût machine des montages du banc #134, sous contention — jamais à vide.

Reprend le dispositif de `exploration/contention/mesurer_contention.py`
(ticket #156), l'agresseur synthétique en moins : le moteur mesuré tient ce
rôle. Pendant toute la mesure, le sidecar de qualification tourne (moteur LLM
éteint) et sert `/opinions/lexicon` en boucle fermée, un client, textes de
`corpus/demandes-rgpd.fr.jsonl`. Le pire cas réel du corpus : le pivot
Dolibarr entier, 5 382 colonnes.

Grandeurs rendues, par montage : latence médiane et p95 par colonne (jamais la
moyenne), durée totale, mémoire résidente du processus moteur, p95 du lexique
pendant l'exécution. Lecture mécanique contre
`exploration/contention/borne-cout-cpu.json`.

Le sidecar doit tourner à côté, moteur LLM éteint :
    uv run --project src/sidecar uvicorn qualification_sidecar.app:app --port 8123

Usage :
    uv run --project exploration python exploration/banc-screening/mesurer_cout.py \
        --racine . --base-url http://127.0.0.1:8123 \
        --sortie exploration/banc-screening/cout.json
"""

from __future__ import annotations

import argparse
import json
import multiprocessing
import os
import statistics
import time
import urllib.request
from pathlib import Path

from banc import GRAINES_MODELE, PLIS, LigneDeBase, ModeleCPU, ReglesLexique, charger_corpus, lire_jsonl


def percentile(valeurs: list[float], p: float) -> float:
    """p95 par la méthode du rang supérieur — jamais d'interpolation optimiste."""
    tri = sorted(valeurs)
    rang = max(0, min(len(tri) - 1, int(round(p / 100.0 * len(tri) + 0.5)) - 1))
    return tri[rang]


def rss_kio(pid: int) -> int | None:
    try:
        with open(f"/proc/{pid}/status", encoding="ascii", errors="replace") as f:
            for ligne in f:
                if ligne.startswith("VmRSS:"):
                    return int(ligne.split()[1])
    except OSError:
        pass
    return None


def _marteler_lexique(base_url: str, textes: list[str], stop, latences_sortie) -> None:
    """Boucle fermée sur /opinions/lexicon, un client — le régime réel du service."""
    url = base_url.rstrip("/") + "/opinions/lexicon"
    i = 0
    while not stop.is_set():
        corps = json.dumps({"text": textes[i % len(textes)]}).encode("utf-8")
        i += 1
        req = urllib.request.Request(url, data=corps, headers={"Content-Type": "application/json"})
        t0 = time.perf_counter()
        try:
            with urllib.request.urlopen(req, timeout=30) as rep:
                rep.read()
        except OSError:
            continue
        latences_sortie.append((time.perf_counter() - t0) * 1000.0)


def lire_textes(chemin: Path) -> list[str]:
    return [json.loads(l)["texte"] for l in chemin.read_text(encoding="utf-8").splitlines() if l.strip()]


def mesurer_montage(nom: str, predire, colonnes: list[dict], base_url: str, textes: list[str]) -> dict:
    """Mesure le moteur colonne par colonne pendant que le lexique est servi."""
    manager = multiprocessing.Manager()
    latences_lexique = manager.list()
    stop = multiprocessing.Event()
    marteau = multiprocessing.Process(
        target=_marteler_lexique, args=(base_url, textes, stop, latences_lexique)
    )
    marteau.start()
    time.sleep(2.0)  # chauffe du client lexique

    latences_moteur: list[float] = []
    debut = time.perf_counter()
    for col in colonnes:
        t0 = time.perf_counter()
        predire(col)
        latences_moteur.append((time.perf_counter() - t0) * 1000.0)
    duree_totale_ms = (time.perf_counter() - debut) * 1000.0

    stop.set()
    marteau.join(timeout=35)
    lex = list(latences_lexique)
    return {
        "montage": nom,
        "colonnes": len(colonnes),
        "moteur_mediane_ms": statistics.median(latences_moteur),
        "moteur_p95_ms": percentile(latences_moteur, 95),
        "moteur_max_ms": max(latences_moteur),
        "duree_totale_ms": duree_totale_ms,
        "rss_processus_moteur_kio": rss_kio(os.getpid()),
        "lexique_requetes": len(lex),
        "lexique_mediane_ms": statistics.median(lex) if lex else None,
        "lexique_p95_ms": percentile(lex, 95) if lex else None,
    }


def lire_borne(resultat: dict, borne: dict) -> dict:
    lecture = []
    valeurs = {
        "rythme-par-colonne": resultat["moteur_p95_ms"],
        "budget-du-geste": resultat["duree_totale_ms"],
        "non-famine-du-lexique": resultat["lexique_p95_ms"],
    }
    for clause in borne["clauses"]:
        v = valeurs[clause["id"]]
        tenue = v is not None and v <= clause["seuil_ms"]
        lecture.append(
            {"clause": clause["id"], "valeur_ms": v, "seuil_ms": clause["seuil_ms"], "tenue": tenue}
        )
    return {"clauses": lecture, "borne_tenue": all(c["tenue"] for c in lecture)}


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--racine", default=".")
    ap.add_argument("--base-url", default="http://127.0.0.1:8123")
    ap.add_argument("--sortie", default="exploration/banc-screening/cout.json")
    ap.add_argument("--sans-modele", action="store_true")
    args = ap.parse_args()
    racine = Path(args.racine)

    # Le pivot Dolibarr entier — 5 382 colonnes, le pire cas réel (#156).
    dolibarr = [
        {**c, "id": f"dolibarr-pivot:{c['table']}:{c['colonne']}"}
        for c in lire_jsonl(racine / "corpus/schemas/pivots/dolibarr.jsonl")
        if "table" in c
    ]
    textes = lire_textes(racine / "corpus/demandes-rgpd.fr.jsonl")
    borne = json.loads(
        (racine / "exploration/contention/borne-cout-cpu.json").read_text(encoding="utf-8")
    )
    lexiques = racine / "exploration/banc-screening/lexiques"

    # Vérifier que le lexique répond avant de mesurer quoi que ce soit.
    req = urllib.request.Request(
        args.base_url.rstrip("/") + "/opinions/lexicon",
        data=json.dumps({"text": textes[0]}).encode(),
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=30) as rep:
        assert rep.status == 200

    montages: list[tuple[str, object]] = [
        ("ligne-de-base", LigneDeBase(lexiques / "ligne-de-base.tsv")),
        ("regles-lexique-fr", ReglesLexique("fr", [lexiques / "dictionnaire-fr.tsv"])),
        ("regles-lexique-en", ReglesLexique("en", [lexiques / "dictionnaire-en.tsv"])),
        (
            "regles-lexique-fr-en",
            ReglesLexique("fr-en", [lexiques / "dictionnaire-fr.tsv", lexiques / "dictionnaire-en.tsv"]),
        ),
    ]

    resultats = {
        "dispositif": (
            "CPU seul, jamais à vide : pendant toute la mesure, le sidecar de qualification "
            "(moteur LLM éteint) sert /opinions/lexicon en boucle fermée, un client, textes de "
            "corpus/demandes-rgpd.fr.jsonl — le dispositif de mesurer_contention.py, agresseur "
            "en moins, le moteur mesuré tenant ce rôle."
        ),
        "pivot": "dolibarr, 5 382 colonnes (pire cas réel du corpus)",
        "machine": {"coeurs": os.cpu_count(), "noyau": os.uname().release},
        "montages": [],
    }

    for nom, moteur in montages:
        r = mesurer_montage(nom, moteur.predire, dolibarr, args.base_url, textes)
        r["lecture_borne"] = lire_borne(r, borne)
        resultats["montages"].append(r)
        print(f"{nom} : p95 {r['moteur_p95_ms']:.4f} ms/col, total {r['duree_totale_ms']:.0f} ms, "
              f"lexique p95 {r['lexique_p95_ms']:.2f} ms, borne tenue : {r['lecture_borne']['borne_tenue']}")

    if not args.sans_modele:
        # Le modèle CPU du banc : pire graine, entraîné hors-dolibarr — le régime
        # réel, prédire un schéma jamais vu. L'entraînement est un coût de banc,
        # pas de service : seul l'inférence est mesurée.
        graines = json.loads(
            (racine / "exploration/banc-screening/resultats.json").read_text(encoding="utf-8")
        )["modele_cpu_graines"]
        pire = int(graines["pire_graine"])
        par_pli = charger_corpus(racine)
        entrainement = [c for p, cs in par_pli.items() if p != "dolibarr" for c in cs]
        import numpy as np
        from sentence_transformers import SentenceTransformer
        from sklearn.neural_network import MLPClassifier

        encodeur = SentenceTransformer("intfloat/multilingual-e5-small", device="cpu")
        from banc import texte_modele

        X = encodeur.encode(
            [texte_modele(c) for c in entrainement], batch_size=64, normalize_embeddings=True
        )
        tete = MLPClassifier(
            hidden_layer_sizes=(128,), random_state=pire, max_iter=500,
            early_stopping=True, n_iter_no_change=10,
        )
        tete.fit(np.array(X), [c["categorie"] for c in entrainement])

        def predire_modele(col: dict) -> str:
            v = encodeur.encode([texte_modele(col)], normalize_embeddings=True)
            return tete.predict(np.array(v))[0]

        r = mesurer_montage("modele-cpu", predire_modele, dolibarr, args.base_url, textes)
        r["graine"] = pire
        r["note"] = (
            "inférence colonne par colonne (lot de 1) : la latence par colonne de la borne "
            "n'a pas d'autre lecture honnête ; un traitement par lot serait plus rapide en "
            "durée totale et est rapporté par la mesure elle-même comme diagnostic"
        )
        r["lecture_borne"] = lire_borne(r, borne)
        resultats["montages"].append(r)
        print(f"modele-cpu : p95 {r['moteur_p95_ms']:.4f} ms/col, total {r['duree_totale_ms']:.0f} ms, "
              f"lexique p95 {r['lexique_p95_ms']:.2f} ms, borne tenue : {r['lecture_borne']['borne_tenue']}")

    Path(args.sortie).write_text(
        json.dumps(resultats, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(f"coût écrit dans {args.sortie}")


if __name__ == "__main__":
    main()
