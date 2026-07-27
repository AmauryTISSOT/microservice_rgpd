"""Évalue et compare les deux moteurs sur le corpus annoté.

Métriques retenues, conformément à la recherche #3 :

- **F₂ par étiquette** comme métrique de pilotage. « Values of β > 1 favor
  recall » (SLP3 ch. 4 §4.9) : la sortie est une aide à la décision validée par
  un humain, donc un oubli coûte plus cher qu'un droit en trop.
- **Macro-moyenne** plutôt que micro : « The macroaverage better reflects the
  statistics of the smaller classes » — les 7 droits comptent également, et
  limitation et portabilité sont les moins représentés.
- **Correspondance exacte** (l'ensemble prédit égale l'ensemble attendu) et
  **Hamming loss**, que ni ML.NET ni l'exactitude simple ne donnent.
- **Faux positifs hors périmètre** : la classe où une erreur coûte le plus à
  l'opérateur humain.

Usage :
    py prototypes/qualification/evaluer.py [--detail]
"""

from __future__ import annotations

import json
import pathlib
import sys
import time

sys.path.insert(0, str(pathlib.Path(__file__).parent))

from moteur_lexique import DROITS, HORS_PERIMETRE, qualifier  # noqa: E402

ETIQUETTES = list(DROITS) + [HORS_PERIMETRE]

RACINE = pathlib.Path(__file__).resolve().parents[2]
DOSSIER = pathlib.Path(__file__).parent
CORPUS = RACINE / "corpus" / "demandes-rgpd.fr.jsonl"
PREDICTIONS_LLM = DOSSIER / "predictions-llm.jsonl"
CORRESPONDANCE = DOSSIER / "correspondance-aveugle.json"


def charger_corpus() -> list[dict]:
    exemples = []
    with CORPUS.open(encoding="utf-8") as f:
        for ligne in f:
            ligne = ligne.strip()
            if ligne:
                exemples.append(json.loads(ligne))
    return exemples


def charger_predictions_llm() -> dict[str, dict]:
    """Rend les prédictions LLM réindexées sur les identifiants du corpus."""
    correspondance = json.loads(CORRESPONDANCE.read_text(encoding="utf-8"))
    predictions = {}
    with PREDICTIONS_LLM.open(encoding="utf-8") as f:
        for ligne in f:
            ligne = ligne.strip()
            if not ligne:
                continue
            p = json.loads(ligne)
            predictions[correspondance[p["id"]]] = p
    return predictions


def f_beta(precision: float, rappel: float, beta: float) -> float:
    if precision == 0 and rappel == 0:
        return 0.0
    b2 = beta * beta
    return (1 + b2) * precision * rappel / (b2 * precision + rappel)


def evaluer(exemples: list[dict], predire) -> dict:
    """`predire` prend un exemple et rend (liste de droits, durée en ms)."""
    vp = {e: 0 for e in ETIQUETTES}
    fp = {e: 0 for e in ETIQUETTES}
    fn = {e: 0 for e in ETIQUETTES}

    exactes = 0
    erreurs_hamming = 0
    total_etiquettes = 0
    durees: list[float] = []
    erreurs: list[dict] = []
    fp_hors_perimetre: list[dict] = []
    fn_hors_perimetre: list[dict] = []

    for ex in exemples:
        attendus = set(ex["droits"])
        predits, duree = predire(ex)
        predits = set(predits)
        durees.append(duree)

        for e in ETIQUETTES:
            a, p = e in attendus, e in predits
            if a and p:
                vp[e] += 1
            elif p and not a:
                fp[e] += 1
            elif a and not p:
                fn[e] += 1
            if a != p:
                erreurs_hamming += 1
            total_etiquettes += 1

        if attendus == predits:
            exactes += 1
        else:
            entree = {
                "id": ex["id"],
                "categorie": ex["categorie"],
                "registre": ex["registre"],
                "attendus": sorted(attendus),
                "predits": sorted(predits),
                "texte": ex["texte"][:110],
            }
            erreurs.append(entree)
            if HORS_PERIMETRE in predits and HORS_PERIMETRE not in attendus:
                fp_hors_perimetre.append(entree)
            if HORS_PERIMETRE in attendus and HORS_PERIMETRE not in predits:
                fn_hors_perimetre.append(entree)

    par_etiquette = {}
    for e in ETIQUETTES:
        precision = vp[e] / (vp[e] + fp[e]) if (vp[e] + fp[e]) else 0.0
        rappel = vp[e] / (vp[e] + fn[e]) if (vp[e] + fn[e]) else 0.0
        par_etiquette[e] = {
            "support": vp[e] + fn[e],
            "vp": vp[e], "fp": fp[e], "fn": fn[e],
            "precision": precision,
            "rappel": rappel,
            "f1": f_beta(precision, rappel, 1),
            "f2": f_beta(precision, rappel, 2),
        }

    n = len(ETIQUETTES)
    durees_triees = sorted(durees)
    return {
        "n": len(exemples),
        "correspondance_exacte": exactes / len(exemples),
        "hamming_loss": erreurs_hamming / total_etiquettes,
        "macro_f1": sum(m["f1"] for m in par_etiquette.values()) / n,
        "macro_f2": sum(m["f2"] for m in par_etiquette.values()) / n,
        "macro_precision": sum(m["precision"] for m in par_etiquette.values()) / n,
        "macro_rappel": sum(m["rappel"] for m in par_etiquette.values()) / n,
        "par_etiquette": par_etiquette,
        "latence_ms_mediane": durees_triees[len(durees_triees) // 2],
        "latence_ms_p95": durees_triees[int(len(durees_triees) * 0.95)],
        "latence_ms_max": durees_triees[-1],
        "erreurs": erreurs,
        "fp_hors_perimetre": fp_hors_perimetre,
        "fn_hors_perimetre": fn_hors_perimetre,
    }


def predire_lexique(ex: dict) -> tuple[list[str], float]:
    debut = time.perf_counter()
    droits = qualifier(ex["texte"])
    return droits, (time.perf_counter() - debut) * 1000


def faire_predire_llm(predictions: dict[str, dict]):
    def predire(ex: dict) -> tuple[list[str], float]:
        # Latence non mesurable : aucune clé d'API disponible (cf. README §
        # « Ce que ce prototype ne mesure pas »).
        return predictions[ex["id"]]["droits"], float("nan")
    return predire


def formater(nom: str, r: dict) -> str:
    lignes = [
        f"### {nom}",
        "",
        f"- Correspondance exacte : **{r['correspondance_exacte']:.1%}** "
        f"({round(r['correspondance_exacte'] * r['n'])}/{r['n']})",
        f"- Macro-F₂ : **{r['macro_f2']:.3f}** · macro-F₁ : {r['macro_f1']:.3f}",
        f"- Macro-précision : {r['macro_precision']:.3f} · macro-rappel : {r['macro_rappel']:.3f}",
        f"- Hamming loss : {r['hamming_loss']:.4f}",
        "",
        "| Droit | Support | VP | FP | FN | Précision | Rappel | F₁ | F₂ |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    ]
    for e in ETIQUETTES:
        m = r["par_etiquette"][e]
        lignes.append(
            f"| `{e}` | {m['support']} | {m['vp']} | {m['fp']} | {m['fn']} | "
            f"{m['precision']:.2f} | {m['rappel']:.2f} | {m['f1']:.2f} | {m['f2']:.2f} |"
        )
    return "\n".join(lignes)


def main() -> int:
    detail = "--detail" in sys.argv
    exemples = charger_corpus()
    predictions_llm = charger_predictions_llm()

    manquants = [e["id"] for e in exemples if e["id"] not in predictions_llm]
    if manquants:
        print(f"Prédictions LLM manquantes pour : {manquants}", file=sys.stderr)
        return 1

    # Chauffe : la première qualification compile les motifs.
    qualifier("bonjour")

    resultats = {
        "Moteur A — lexiques et règles": evaluer(exemples, predire_lexique),
        "Moteur B — LLM à sortie structurée": evaluer(exemples, faire_predire_llm(predictions_llm)),
    }

    for nom, r in resultats.items():
        print(formater(nom, r))
        print()

    a = resultats["Moteur A — lexiques et règles"]
    print("### Latence mesurée — moteur A uniquement\n")
    print(f"- Médiane : {a['latence_ms_mediane']:.3f} ms")
    print(f"- p95 : {a['latence_ms_p95']:.3f} ms")
    print(f"- Maximum : {a['latence_ms_max']:.3f} ms")
    print()

    print("### Hors périmètre — coût pour l'opérateur\n")
    for nom, r in resultats.items():
        print(
            f"- {nom} : {len(r['fp_hors_perimetre'])} faux positifs "
            f"(classé hors périmètre à tort) · {len(r['fn_hors_perimetre'])} faux négatifs "
            f"(droit inventé sur un texte hors périmètre)"
        )
    print()

    # --- Le signal de confiance du moteur B est-il exploitable ? ------------
    # Question décisive pour une aide à la décision : l'opérateur peut-il
    # trier sur ce signal plutôt que tout relire ?
    print("### Moteur B — pouvoir de tri du signal de confiance\n")
    b = resultats["Moteur B — LLM à sortie structurée"]
    ids_errones = {e["id"] for e in b["erreurs"]}
    par_confiance: dict[str, dict[str, int]] = {}
    for ex in exemples:
        c = predictions_llm[ex["id"]]["confiance"]
        seau = par_confiance.setdefault(c, {"total": 0, "erreurs": 0})
        seau["total"] += 1
        seau["erreurs"] += 1 if ex["id"] in ids_errones else 0

    print("| Confiance déclarée | Exemples | Erreurs | Taux d'erreur |")
    print("| --- | ---: | ---: | ---: |")
    for c in ("haute", "moyenne", "basse"):
        if c in par_confiance:
            s = par_confiance[c]
            print(f"| {c} | {s['total']} | {s['erreurs']} | {s['erreurs'] / s['total']:.1%} |")
    non_hautes = sum(s["total"] for c, s in par_confiance.items() if c != "haute")
    erreurs_non_hautes = sum(s["erreurs"] for c, s in par_confiance.items() if c != "haute")
    print()
    print(
        f"Relire les {non_hautes} textes de confiance non haute "
        f"({non_hautes / len(exemples):.0%} du corpus) capte "
        f"{erreurs_non_hautes}/{len(ids_errones)} des erreurs."
    )
    print()

    # --- Paires minimales ---------------------------------------------------
    print("### Paires minimales — les plus discriminantes du corpus\n")
    paires = [
        ("edg-13", "edg-14", "valeur de remplacement"),
        ("edg-04", "edg-05", "question sur un droit ou exercice"),
        ("eff-02", "hop-03", "compte ou abonnement"),
        ("eff-03", "hop-24", "objet données ou objet contrat"),
        ("opp-02", "mul-01", "enregistrement préservé ou visé"),
        ("edg-11", "edg-12", "opposition ou limitation"),
        ("acc-01", "hop-22", "RGPD ou législation sectorielle"),
        ("eff-05", "opp-01", "retrait de consentement ou opposition"),
    ]
    par_id = {e["id"]: e for e in exemples}
    erreurs_a = {e["id"] for e in resultats["Moteur A — lexiques et règles"]["erreurs"]}

    print("| Paire | Départage | Moteur A | Moteur B |")
    print("| --- | --- | :---: | :---: |")
    reussies_a = reussies_b = 0
    for g, d, depart in paires:
        if g not in par_id or d not in par_id:
            continue
        ok_a = g not in erreurs_a and d not in erreurs_a
        ok_b = g not in ids_errones and d not in ids_errones
        reussies_a += ok_a
        reussies_b += ok_b
        print(f"| `{g}` / `{d}` | {depart} | {'✅' if ok_a else '❌'} | {'✅' if ok_b else '❌'} |")
    print()
    print(f"Paires entièrement départagées — moteur A : {reussies_a}/8 · moteur B : {reussies_b}/8")
    print()

    if detail:
        for nom, r in resultats.items():
            print(f"### Erreurs — {nom} ({len(r['erreurs'])})\n")
            for e in r["erreurs"]:
                print(
                    f"- `{e['id']}` [{e['categorie']}/{e['registre']}] "
                    f"attendu {e['attendus']} → prédit {e['predits']}\n"
                    f"  > {e['texte']}"
                )
            print()

    return 0


if __name__ == "__main__":
    sys.exit(main())
