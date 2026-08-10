"""Profil de contention du sidecar de qualification — ticket #156.

Mesure la latence du point d'entrée `/opinions/lexicon` (médiane et p95, jamais
la moyenne) et la mémoire résidente du sidecar, pendant qu'un agresseur CPU
sature un nombre croissant de cœurs — jamais à vide : le palier « 0 cœur »
n'est là que comme référence de lecture, le profil est la série entière.

L'agresseur simule le régime du futur moteur de dépistage : du calcul Python
pur, lié au CPU, sans entrée-sortie. C'est le régime que la décision de
cadrage 13 de la carte #122 déclare dangereux pour le point d'entrée du
lexique (spec qualification § 5.8).

Bibliothèque standard uniquement — le banc exige un journal des dépendances
ajoutées ; ce script n'en ajoute aucune.

Usage :
    python3 mesurer_contention.py --base-url http://127.0.0.1:8123 \
        --corpus ../../corpus/demandes-rgpd.fr.jsonl \
        --sortie profil-contention-sidecar.json

Le sidecar doit tourner à côté, moteur LLM éteint :
    uv run --project src/sidecar uvicorn qualification_sidecar.app:app --port 8123
"""

from __future__ import annotations

import argparse
import json
import multiprocessing
import os
import statistics
import time
import urllib.request


def _spin(stop: multiprocessing.Event) -> None:
    """Un cœur saturé : du calcul Python pur, comme un moteur lexical de dépistage."""
    x = 0
    while not stop.is_set():
        for i in range(10_000):
            x = (x * 31 + i) % 1_000_003


def lire_textes(chemin: str) -> list[str]:
    textes = []
    with open(chemin, encoding="utf-8") as f:
        for ligne in f:
            ligne = ligne.strip()
            if ligne:
                textes.append(json.loads(ligne)["texte"])
    if not textes:
        raise SystemExit(f"aucun texte dans {chemin}")
    return textes


def trouver_pid_sidecar() -> int | None:
    """Retrouve le processus uvicorn du sidecar via /proc, sans dépendance."""
    for pid in os.listdir("/proc"):
        if not pid.isdigit():
            continue
        try:
            with open(f"/proc/{pid}/cmdline", "rb") as f:
                cmd = f.read().decode(errors="replace")
        except OSError:
            continue
        if "qualification_sidecar.app" in cmd:
            return int(pid)
    return None


def rss_kio(pid: int) -> int | None:
    try:
        with open(f"/proc/{pid}/status", encoding="ascii", errors="replace") as f:
            for ligne in f:
                if ligne.startswith("VmRSS:"):
                    return int(ligne.split()[1])
    except OSError:
        pass
    return None


def percentile(valeurs: list[float], p: float) -> float:
    """p95 par la méthode du rang supérieur — jamais d'interpolation optimiste."""
    tri = sorted(valeurs)
    rang = max(0, min(len(tri) - 1, int(round(p / 100.0 * len(tri) + 0.5)) - 1))
    return tri[rang]


def mesurer_palier(
    base_url: str, textes: list[str], duree_s: float, chauffe_s: float
) -> list[float]:
    """Boucle fermée, un seul client — le régime réel du service : une demande à la fois."""
    latences: list[float] = []
    debut = time.perf_counter()
    i = 0
    url = base_url.rstrip("/") + "/opinions/lexicon"
    while True:
        maintenant = time.perf_counter()
        if maintenant - debut >= duree_s + chauffe_s:
            break
        corps = json.dumps({"text": textes[i % len(textes)]}).encode("utf-8")
        i += 1
        req = urllib.request.Request(
            url, data=corps, headers={"Content-Type": "application/json"}
        )
        t0 = time.perf_counter()
        with urllib.request.urlopen(req, timeout=30) as rep:
            rep.read()
            if rep.status != 200:
                raise SystemExit(f"lexique non-200 : {rep.status}")
        t1 = time.perf_counter()
        if t0 - debut >= chauffe_s:
            latences.append((t1 - t0) * 1000.0)
    return latences


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--base-url", default="http://127.0.0.1:8123")
    ap.add_argument("--corpus", required=True)
    ap.add_argument("--sortie", required=True)
    ap.add_argument("--duree", type=float, default=30.0, help="secondes de mesure par palier")
    ap.add_argument("--chauffe", type=float, default=3.0, help="secondes écartées en début de palier")
    ap.add_argument(
        "--paliers",
        default="0,2,4,6,7,8",
        help="nombres de cœurs saturés par l'agresseur, séparés par des virgules",
    )
    args = ap.parse_args()

    textes = lire_textes(args.corpus)
    pid = trouver_pid_sidecar()
    if pid is None:
        raise SystemExit("sidecar introuvable — le lancer d'abord (voir l'en-tête du script)")

    resultats = {
        "machine": {
            "coeurs": os.cpu_count(),
            "noyau": os.uname().release,
        },
        "sidecar": {"pid": pid, "rss_kio_avant": rss_kio(pid)},
        "corpus_requetes": {"fichier": args.corpus, "textes": len(textes)},
        "duree_par_palier_s": args.duree,
        "paliers": [],
    }

    for n_coeurs in [int(x) for x in args.paliers.split(",")]:
        stop = multiprocessing.Event()
        agresseurs = [
            multiprocessing.Process(target=_spin, args=(stop,), daemon=True)
            for _ in range(n_coeurs)
        ]
        for p in agresseurs:
            p.start()
        time.sleep(0.5)
        try:
            latences = mesurer_palier(args.base_url, textes, args.duree, args.chauffe)
        finally:
            stop.set()
            for p in agresseurs:
                p.join(timeout=5)
        palier = {
            "coeurs_satures": n_coeurs,
            "requetes": len(latences),
            "mediane_ms": round(statistics.median(latences), 3),
            "p95_ms": round(percentile(latences, 95.0), 3),
            "max_ms": round(max(latences), 3),
            "rss_kio": rss_kio(pid),
        }
        resultats["paliers"].append(palier)
        print(
            f"{n_coeurs} cœur(s) saturé(s) : {palier['requetes']} requêtes, "
            f"médiane {palier['mediane_ms']} ms, p95 {palier['p95_ms']} ms, "
            f"max {palier['max_ms']} ms, RSS {palier['rss_kio']} kio"
        )

    resultats["sidecar"]["rss_kio_apres"] = rss_kio(pid)
    with open(args.sortie, "w", encoding="utf-8") as f:
        json.dump(resultats, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"profil écrit dans {args.sortie}")


if __name__ == "__main__":
    main()
