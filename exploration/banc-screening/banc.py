"""Banc d'essai du moteur de dépistage — ticket #134.

Exécute le protocole de #130 (résolution du 2026-08-09, amendée par #145, #150,
#154) et n'en rediscute rien. Six plis « laisser un schéma dehors », quatre
montages plus la ligne de base triviale, F2 macro sur l'ensemble publié par
`corpus/schemas/macro-f2.json`, bootstrap de grappe sur l'écart apparié
montage − ligne de base, pire seed (R = 5) pour le modèle CPU.

Environnement : celui de l'exploration (`exploration/pyproject.toml`) — le banc
calcule sur des fichiers figés (règle d'environnements de la carte #42). Le coût
sous contention, qui parle à un moteur servi, vit dans `mesurer_cout.py`.

Usage :
    uv run --project exploration python exploration/banc-screening/banc.py \
        --racine . --sortie exploration/banc-screening

Tout est déterministe : la graine du bootstrap et les cinq graines du modèle
CPU sont déclarées ci-dessous, avant tout chiffre.
"""

from __future__ import annotations

import argparse
import json
import math
import re
import unicodedata
from collections import Counter, defaultdict
from pathlib import Path

# ---------------------------------------------------------------------------
# Constantes du protocole — rien ici n'est un réglage.
# ---------------------------------------------------------------------------

# L'ordre d'arbitrage est celui du tableau de #127, hérité et jamais redécidé.
ORDRE_ARBITRAGE = [
    "CriminalOffenceData",
    "HealthData",
    "SpecialCategoryData",
    "AuthenticationSecret",
    "NationalIdentifier",
    "FinancialData",
    "LocationData",
    "ConnectionData",
    "Identity",
    "ContactDetails",
    "ProfessionalLife",
    "PersonalDataUncategorised",
    "Unflagged",
]
RANG = {c: i for i, c in enumerate(ORDRE_ARBITRAGE)}

# Les six plis de #130 ; galette-pg hors mesure (contrôle de dialecte),
# temoin hors des deux côtés.
PLIS = {
    "dolibarr": ["dolibarr"],
    "glpi": ["glpi"],
    "openemr": ["openemr"],
    "sacoche": ["sacoche"],
    "paheko": ["paheko-0.8.0", "paheko-1.0.0", "paheko-head"],
    "galette": ["galette"],
}

# Graines déclarées avant tout chiffre. Cinq pour le modèle CPU (R = 5,
# pire des cinq), une pour le bootstrap.
GRAINES_MODELE = [1, 2, 3, 4, 5]
GRAINE_BOOTSTRAP = 134
B_BOOTSTRAP = 10_000

BETA2 = 4.0  # β = 2, assumé par #130.


# ---------------------------------------------------------------------------
# Chargement
# ---------------------------------------------------------------------------


def lire_jsonl(chemin: Path) -> list[dict]:
    lignes = []
    with chemin.open(encoding="utf-8") as f:
        for ligne in f:
            ligne = ligne.strip()
            if ligne:
                lignes.append(json.loads(ligne))
    return lignes


def lire_tsv(chemin: Path) -> list[tuple[str, str]]:
    paires = []
    with chemin.open(encoding="utf-8") as f:
        entete = f.readline()
        assert "\t" in entete
        for ligne in f:
            ligne = ligne.rstrip("\n")
            if not ligne:
                continue
            terme, valeur = ligne.split("\t")
            paires.append((terme, valeur))
    return paires


def charger_corpus(racine: Path) -> dict[str, list[dict]]:
    """Les colonnes annotées, par pli. 3 182 mesurées sur 3 254 annotées."""
    par_pli: dict[str, list[dict]] = {}
    for pli, sources in PLIS.items():
        colonnes = []
        for source in sources:
            colonnes.extend(
                lire_jsonl(racine / "corpus/schemas/annotation" / f"{source}.annotation.jsonl")
            )
        par_pli[pli] = colonnes
    return par_pli


# ---------------------------------------------------------------------------
# Normalisation et découpe d'identifiants — mécanique générique, sans aucune
# connaissance du corpus : la seule connaissance lexicale du banc est dans les
# fichiers gelés de `lexiques/`.
# ---------------------------------------------------------------------------


def normaliser(texte: str) -> str:
    nfd = unicodedata.normalize("NFD", texte)
    sans_accents = "".join(c for c in nfd if unicodedata.category(c) != "Mn")
    return sans_accents.lower()


_RE_CAMEL = re.compile(r"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")
_RE_NON_ALNUM = re.compile(r"[^a-zA-Z0-9]+")


def decouper(identifiant: str) -> list[str]:
    """Découpe un identifiant en jetons : séparateurs, camelCase, chiffres ôtés."""
    morceaux = _RE_NON_ALNUM.split(identifiant)
    jetons = []
    for m in morceaux:
        for j in _RE_CAMEL.split(m):
            j = normaliser(j).strip("0123456789")
            if j:
                jetons.append(j)
    return jetons


def mots(prose: str) -> list[str]:
    """Les mots d'un commentaire en prose, normalisés."""
    return [normaliser(m) for m in _RE_NON_ALNUM.split(prose) if m]


# ---------------------------------------------------------------------------
# La ligne de base triviale — spec de #130 : correspondance exacte insensible
# à la casse du nom entier, lit le seul champ `colonne`, aucune découpe.
# ---------------------------------------------------------------------------


class LigneDeBase:
    nom = "ligne-de-base"

    def __init__(self, chemin: Path):
        self.lexique = {t.lower(): v for t, v in lire_tsv(chemin)}

    def predire(self, col: dict) -> dict:
        valeur = self.lexique.get(col["colonne"].lower())
        if valeur is None:
            return {"categorie": "Unflagged", "rule_strength": None, "motif": None}
        return {
            "categorie": valeur,
            "rule_strength": "exacte",
            "motif": f"nom entier « {col['colonne']} » présent tel quel dans le lexique de base",
        }


# ---------------------------------------------------------------------------
# Le montage règles + lexique.
#
# Règles, dans l'ordre où elles s'énoncent ; leur force suit la décision de
# cadrage 5 (exacte ⇒ forte, rapprochement morphologique ⇒ moyenne,
# heuristique de type ⇒ faible) :
#   1. jeton du nom de colonne égal à une entrée            → exacte
#   2. jeton du nom de colonne apparié par préfixe (≥ 4)     → morphologique
#   3. mot du commentaire de colonne égal à une entrée       → morphologique
#   4. héritage du domaine par le nom de table (§ 3.10) :
#      jeton du nom de table ou mot du commentaire de table  → morphologique
#      (jamais appliqué aux clés purement techniques `id`/`rowid`)
#   5. type `json`/`jsonb` → PersonalDataUncategorised,
#      « conteneur libre » (règle transférée par #132 sur ce ticket) → type
#
# `type`, `nullable` et `table_referencee` restent des filtres, jamais des
# signaux (#128) — la seule exception est la règle « conteneur libre »,
# transférée nommément comme règle du moteur.
# ---------------------------------------------------------------------------


def _apparier_morpho(jeton: str, lexique: dict[str, str]) -> str | None:
    """Rapprochement par préfixe : le jeton et l'entrée partagent un préfixe
    couvrant entièrement le plus court des deux, longueur minimale 4."""
    if len(jeton) < 4:
        return None
    for terme in lexique:
        if len(terme) < 4:
            continue
        if jeton.startswith(terme) or terme.startswith(jeton):
            return terme
    return None


FORCE = {"exacte": 0, "morphologique": 1, "type": 2}


class ReglesLexique:
    def __init__(self, nom: str, lexiques: list[Path]):
        self.nom = nom
        self.lexique: dict[str, str] = {}
        for chemin in lexiques:
            for terme, valeur in lire_tsv(chemin):
                terme_n = normaliser(terme)
                if terme_n in self.lexique and self.lexique[terme_n] != valeur:
                    # Union mécanique FR+EN : les deux entrées déclenchent, et
                    # l'ordre d'arbitrage tranche — on garde la plus coûteuse
                    # à omettre, comme pour tout double déclenchement.
                    if RANG[valeur] < RANG[self.lexique[terme_n]]:
                        self.lexique[terme_n] = valeur
                else:
                    self.lexique[terme_n] = valeur

    def _declenchements(self, col: dict) -> list[tuple[str, str, str]]:
        """(categorie, force, motif) pour chaque règle qui déclenche."""
        decl: list[tuple[str, str, str]] = []
        jetons_colonne = decouper(col["colonne"])
        for j in jetons_colonne:
            if j in self.lexique:
                decl.append(
                    (self.lexique[j], "exacte", f"jeton « {j} » du nom de colonne, entrée du lexique")
                )
        for j in jetons_colonne:
            if j in self.lexique:
                continue
            terme = _apparier_morpho(j, self.lexique)
            if terme:
                decl.append(
                    (
                        self.lexique[terme],
                        "morphologique",
                        f"jeton « {j} » rapproché de l'entrée « {terme} »",
                    )
                )
        for m in mots(col.get("commentaire_colonne") or ""):
            if m in self.lexique:
                decl.append(
                    (self.lexique[m], "morphologique", f"mot « {m} » du commentaire de colonne")
                )
        if not decl and col["colonne"].lower() not in ("id", "rowid"):
            # Héritage du domaine par la table (§ 3.10) : seulement si rien
            # n'a déclenché au niveau de la colonne.
            for j in decouper(col["table"]):
                if j in self.lexique:
                    decl.append(
                        (
                            self.lexique[j],
                            "morphologique",
                            f"héritage : jeton « {j} » du nom de la table « {col['table']} »",
                        )
                    )
            for m in mots(col.get("commentaire_table") or ""):
                if m in self.lexique:
                    decl.append(
                        (self.lexique[m], "morphologique", f"héritage : mot « {m} » du commentaire de table")
                    )
        if "json" in (col.get("type") or "").lower():
            decl.append(
                (
                    "PersonalDataUncategorised",
                    "type",
                    "conteneur libre : le contenu n'est pas lisible depuis le schéma",
                )
            )
        return decl

    def predire(self, col: dict) -> dict:
        decl = self._declenchements(col)
        if not decl:
            return {"categorie": "Unflagged", "rule_strength": None, "motif": None}
        categorie = min((c for c, _, _ in decl), key=lambda c: RANG[c])
        retenues = [(c, f, m) for c, f, m in decl if c == categorie]
        force = min(retenues, key=lambda d: FORCE[d[1]])[1]
        motif = "; ".join(sorted({m for _, _, m in retenues}))
        ecartees = sorted({c for c, _, _ in decl if c != categorie}, key=lambda c: RANG[c])
        if ecartees:
            motif += f" (a aussi déclenché : {', '.join(ecartees)})"
        return {"categorie": categorie, "rule_strength": force, "motif": motif}


# ---------------------------------------------------------------------------
# Le modèle CPU : plongements + classifieur (#130). L'encodeur est celui de
# l'exploration du dépôt, `intfloat/multilingual-e5-small` (bilingue — le
# corpus l'est), la tête un perceptron multicouche scikit-learn dont la graine
# est le seul aléa. Il ne lit ni `type`, ni `nullable`, ni `table_referencee`
# (filtres, jamais signaux — #128) ; il ne dérive aucun degré de doute (dette
# déclarée par #130).
# ---------------------------------------------------------------------------


def texte_modele(col: dict) -> str:
    morceaux = [f"table: {col['table']}", f"colonne: {col['colonne']}"]
    if col.get("commentaire_colonne"):
        morceaux.append(f"commentaire: {col['commentaire_colonne']}")
    if col.get("commentaire_table"):
        morceaux.append(f"commentaire de table: {col['commentaire_table']}")
    return "query: " + " | ".join(morceaux)


_ENCODEUR = None


def obtenir_encodeur():
    """Un seul encodeur pour tout le banc — un second chargement a suffi à
    faire tuer le processus par le noyau (mémoire) lors de la première
    exécution ; l'instance est partagée, jamais rechargée."""
    global _ENCODEUR
    if _ENCODEUR is None:
        from sentence_transformers import SentenceTransformer

        _ENCODEUR = SentenceTransformer("intfloat/multilingual-e5-small", device="cpu")
    return _ENCODEUR


class ModeleCPU:
    def __init__(self, cache: dict[str, "object"]):
        self.cache = cache  # id -> vecteur

    @staticmethod
    def encoder(colonnes: list[dict]) -> dict[str, "object"]:
        modele = obtenir_encodeur()
        textes = [texte_modele(c) for c in colonnes]
        vecteurs = modele.encode(textes, batch_size=64, show_progress_bar=False, normalize_embeddings=True)
        return {c["id"]: v for c, v in zip(colonnes, vecteurs)}

    def entrainer_predire(
        self, entrainement: list[dict], test: list[dict], graine: int
    ) -> list[dict]:
        import numpy as np
        from sklearn.linear_model import SGDClassifier

        X = np.array([self.cache[c["id"]] for c in entrainement])
        y = [c["categorie"] for c in entrainement]
        # Écart déclaré (README, « tête remplacée une fois ») : la première tête
        # (MLP sans pondération) s'est effondrée sur `Unflagged` pour les cinq
        # graines — F2 0,0 partout, 0 colonne signalée sur 3 182. Remède
        # standard appliqué une seule fois : tête linéaire à classes pondérées,
        # sensible à la graine par le mélange des exemples. Aucune itération
        # supplémentaire, quel que soit son score.
        tete = SGDClassifier(
            loss="log_loss",
            class_weight="balanced",
            random_state=graine,
            max_iter=1000,
            tol=1e-3,
        )
        tete.fit(X, y)
        Xt = np.array([self.cache[c["id"]] for c in test])
        pred = tete.predict(Xt)
        return [
            {"categorie": p, "rule_strength": None, "motif": None} for p in pred
        ]


# ---------------------------------------------------------------------------
# Métriques
# ---------------------------------------------------------------------------


def f2(tp: int, fp: int, fn: int) -> float | None:
    if tp + fn == 0:
        return None  # catégorie sans support : F2 indéfinie, jamais 0 par décret
    denominateur = (1 + BETA2) * tp + BETA2 * fn + fp
    if denominateur == 0:
        return None
    return (1 + BETA2) * tp / denominateur


def comptes_par_categorie(verites: list[str], predictions: list[str]) -> dict[str, dict[str, int]]:
    comptes: dict[str, dict[str, int]] = defaultdict(lambda: {"tp": 0, "fp": 0, "fn": 0})
    for v, p in zip(verites, predictions):
        if v == p:
            if v != "Unflagged":
                comptes[v]["tp"] += 1
        else:
            if p != "Unflagged":
                comptes[p]["fp"] += 1
            if v != "Unflagged":
                comptes[v]["fn"] += 1
    return comptes


def f2_macro(verites: list[str], predictions: list[str], categories: list[str]) -> float | None:
    comptes = comptes_par_categorie(verites, predictions)
    scores = []
    for cat in categories:
        c = comptes[cat]
        s = f2(c["tp"], c["fp"], c["fn"])
        if s is not None:
            scores.append(s)
    return sum(scores) / len(scores) if scores else None


def f2_micro(verites: list[str], predictions: list[str]) -> float | None:
    comptes = comptes_par_categorie(verites, predictions)
    tp = sum(c["tp"] for c in comptes.values())
    fp = sum(c["fp"] for c in comptes.values())
    fn = sum(c["fn"] for c in comptes.values())
    return f2(tp, fp, fn)


def wilson(succes: int, total: int, z: float = 1.959964) -> tuple[float, float]:
    if total == 0:
        return (0.0, 1.0)
    p = succes / total
    d = 1 + z * z / total
    centre = (p + z * z / (2 * total)) / d
    marge = z * math.sqrt(p * (1 - p) / total + z * z / (4 * total * total)) / d
    return (centre - marge, centre + marge)


# ---------------------------------------------------------------------------
# Bootstrap de grappe : rééchantillonne les six schémas, porte sur l'écart
# apparié montage − ligne de base, jamais sur les scores séparés (#130).
# ---------------------------------------------------------------------------


def bootstrap_ecart(
    par_pli_verites: dict[str, list[str]],
    par_pli_montage: dict[str, list[str]],
    par_pli_base: dict[str, list[str]],
    categories: list[str],
) -> dict:
    import random

    rng = random.Random(GRAINE_BOOTSTRAP)
    plis = list(PLIS.keys())
    ecarts = []
    replicats_indefinis = 0
    for _ in range(B_BOOTSTRAP):
        tirage = [rng.randrange(len(plis)) for _ in range(len(plis))]
        v: list[str] = []
        m: list[str] = []
        b: list[str] = []
        for i in tirage:
            pli = plis[i]
            v.extend(par_pli_verites[pli])
            m.extend(par_pli_montage[pli])
            b.extend(par_pli_base[pli])
        fm = f2_macro(v, m, categories)
        fb = f2_macro(v, b, categories)
        if fm is None or fb is None:
            replicats_indefinis += 1
            continue
        ecarts.append(fm - fb)
    ecarts.sort()
    n = len(ecarts)
    return {
        "b": B_BOOTSTRAP,
        "replicats_indefinis": replicats_indefinis,
        "ic95": [ecarts[int(0.025 * n)], ecarts[min(n - 1, int(0.975 * n))]],
        "mediane": ecarts[n // 2],
    }


# ---------------------------------------------------------------------------
# Exécution
# ---------------------------------------------------------------------------


def evaluer_ensemble(colonnes: list[dict], predictions: list[dict], categories: list[str]) -> dict:
    verites = [c["categorie"] for c in colonnes]
    preds = [p["categorie"] for p in predictions]
    par_cat = {}
    comptes = comptes_par_categorie(verites, preds)
    for cat in ORDRE_ARBITRAGE:
        if cat == "Unflagged":
            continue
        c = comptes[cat]
        par_cat[cat] = {
            "support": c["tp"] + c["fn"],
            "tp": c["tp"],
            "fp": c["fp"],
            "fn": c["fn"],
            "f2": f2(c["tp"], c["fp"], c["fn"]),
        }
    exact = sum(1 for v, p in zip(verites, preds) if v == p)
    signalees_pred = sum(1 for p in preds if p != "Unflagged")
    repli_pred = sum(1 for p in preds if p == "PersonalDataUncategorised")
    return {
        "colonnes": len(colonnes),
        "f2_macro": f2_macro(verites, preds, categories),
        "f2_micro": f2_micro(verites, preds),
        "exactitude": exact / len(colonnes),
        "wilson_exactitude_ic95": wilson(exact, len(colonnes)),
        "par_categorie": par_cat,
        "signalees_predites": signalees_pred,
        "taux_repli_parmi_signalees": (repli_pred / signalees_pred) if signalees_pred else None,
    }


def erreurs_par_degre(colonnes: list[dict], predictions: list[dict]) -> dict:
    par_degre: dict[str, dict[str, int]] = defaultdict(lambda: {"total": 0, "erreurs": 0})
    for c, p in zip(colonnes, predictions):
        if p["categorie"] == "Unflagged" or p["rule_strength"] is None:
            continue
        d = par_degre[p["rule_strength"]]
        d["total"] += 1
        if p["categorie"] != c["categorie"]:
            d["erreurs"] += 1
    return {
        degre: {**v, "taux_erreur": v["erreurs"] / v["total"] if v["total"] else None}
        for degre, v in par_degre.items()
    }


def ecrire_predictions(dossier: Path, pli: str, colonnes: list[dict], predictions: list[dict]) -> None:
    dossier.mkdir(parents=True, exist_ok=True)
    with (dossier / f"{pli}.jsonl").open("w", encoding="utf-8") as f:
        for c, p in zip(colonnes, predictions):
            f.write(
                json.dumps(
                    {
                        "id": c["id"],
                        "categorie": p["categorie"],
                        "rule_strength": p["rule_strength"],
                        "motif": p["motif"],
                    },
                    ensure_ascii=False,
                )
                + "\n"
            )


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--racine", default=".", help="racine du dépôt")
    ap.add_argument("--sortie", default="exploration/banc-screening")
    ap.add_argument("--sans-modele", action="store_true", help="sauter le modèle CPU (mise au point)")
    ap.add_argument(
        "--reprendre",
        action="store_true",
        help="recharger les prédictions du modèle CPU déjà versionnées au lieu de réentraîner",
    )
    args = ap.parse_args()
    racine = Path(args.racine)
    sortie = Path(args.sortie)

    macro_spec = json.loads((racine / "corpus/schemas/macro-f2.json").read_text(encoding="utf-8"))
    categories_macro = macro_spec["macro"]

    par_pli = charger_corpus(racine)
    lexiques = racine / "exploration/banc-screening/lexiques"

    montages: dict[str, object] = {
        "ligne-de-base": LigneDeBase(lexiques / "ligne-de-base.tsv"),
        "regles-lexique-fr": ReglesLexique("regles-lexique-fr", [lexiques / "dictionnaire-fr.tsv"]),
        "regles-lexique-en": ReglesLexique("regles-lexique-en", [lexiques / "dictionnaire-en.tsv"]),
        "regles-lexique-fr-en": ReglesLexique(
            "regles-lexique-fr-en",
            [lexiques / "dictionnaire-fr.tsv", lexiques / "dictionnaire-en.tsv"],
        ),
    }

    # ------------------------------------------------------------------
    # Test de fumée sur `temoin` : vérifie que le banc tourne, ne mesure rien.
    # ------------------------------------------------------------------
    temoin = lire_jsonl(racine / "corpus/schemas/annotation/temoin.annotation.jsonl")
    for nom, moteur in montages.items():
        preds = [moteur.predire(c) for c in temoin]
        assert len(preds) == len(temoin), f"fumée : {nom} n'a pas rendu toutes les colonnes"
    print(f"fumée : {len(temoin)} colonnes de temoin traversent les {len(montages)} montages déterministes")

    resultats: dict = {
        "graines": {"modele_cpu": GRAINES_MODELE, "bootstrap": GRAINE_BOOTSTRAP, "b": B_BOOTSTRAP},
        "categories_macro": categories_macro,
        "montages": {},
    }

    predictions_par_montage: dict[str, dict[str, list[dict]]] = {}

    # Montages déterministes : six exécutions chacun (une par pli de test).
    for nom, moteur in montages.items():
        predictions_par_montage[nom] = {}
        for pli, colonnes in par_pli.items():
            preds = [moteur.predire(c) for c in colonnes]
            predictions_par_montage[nom][pli] = preds
            ecrire_predictions(sortie / "predictions" / nom, pli, colonnes, preds)

    # Modèle CPU : 6 plis × 5 graines = 30 entraînements ; le pire des cinq compte.
    if not args.sans_modele:
        toutes = [c for cs in par_pli.values() for c in cs]
        cache: dict = {}
        modele = ModeleCPU(cache)
        f2_par_graine: dict[int, float] = {}
        preds_par_graine: dict[int, dict[str, list[dict]]] = {}
        for graine in GRAINES_MODELE:
            preds_par_graine[graine] = {}
            for pli, colonnes in par_pli.items():
                chemin_preds = sortie / "predictions" / "modele-cpu" / f"graine-{graine}" / f"{pli}.jsonl"
                if args.reprendre and chemin_preds.exists():
                    sauvees = lire_jsonl(chemin_preds)
                    assert [s["id"] for s in sauvees] == [c["id"] for c in colonnes]
                    preds = [
                        {"categorie": s["categorie"], "rule_strength": None, "motif": None}
                        for s in sauvees
                    ]
                    preds_par_graine[graine][pli] = preds
                    continue
                if not cache:
                    print("modèle CPU : encodage des plongements…")
                    cache.update(ModeleCPU.encoder(toutes))
                entrainement = [c for p, cs in par_pli.items() if p != pli for c in cs]
                preds = modele.entrainer_predire(entrainement, colonnes, graine)
                preds_par_graine[graine][pli] = preds
                ecrire_predictions(
                    sortie / "predictions" / "modele-cpu" / f"graine-{graine}", pli, colonnes, preds
                )
            v = [c["categorie"] for cs in par_pli.values() for c in cs]
            p = [
                x["categorie"]
                for pli in PLIS
                for x in preds_par_graine[graine][pli]
            ]
            score = f2_macro(v, p, categories_macro)
            f2_par_graine[graine] = score
            print(f"modèle CPU graine {graine} : F2 macro hors-pli {score:.4f}")
        pire = min(f2_par_graine, key=f2_par_graine.get)
        resultats["modele_cpu_graines"] = {
            "f2_macro_par_graine": f2_par_graine,
            "pire_graine": pire,
        }
        predictions_par_montage["modele-cpu"] = preds_par_graine[pire]

    # ------------------------------------------------------------------
    # Évaluation : point global (six plis réunis), par pli, et bootstrap de
    # l'écart apparié contre la ligne de base.
    # ------------------------------------------------------------------
    par_pli_verites = {pli: [c["categorie"] for c in cs] for pli, cs in par_pli.items()}
    base_par_pli = {
        pli: [p["categorie"] for p in predictions_par_montage["ligne-de-base"][pli]] for pli in PLIS
    }

    for nom, preds_par_pli in predictions_par_montage.items():
        toutes_colonnes = [c for pli in PLIS for c in par_pli[pli]]
        toutes_preds = [p for pli in PLIS for p in preds_par_pli[pli]]
        entree = evaluer_ensemble(toutes_colonnes, toutes_preds, categories_macro)
        entree["par_pli"] = {
            pli: evaluer_ensemble(par_pli[pli], preds_par_pli[pli], categories_macro) for pli in PLIS
        }
        entree["erreurs_par_degre"] = erreurs_par_degre(toutes_colonnes, toutes_preds)
        if nom != "ligne-de-base":
            montage_par_pli = {pli: [p["categorie"] for p in preds_par_pli[pli]] for pli in PLIS}
            entree["ecart_apparie_vs_base"] = bootstrap_ecart(
                par_pli_verites, montage_par_pli, base_par_pli, categories_macro
            )
        resultats["montages"][nom] = entree
        print(
            f"{nom} : F2 macro {entree['f2_macro']:.4f}, micro {entree['f2_micro']:.4f}"
            + (
                f", écart IC95 {entree['ecart_apparie_vs_base']['ic95']}"
                if "ecart_apparie_vs_base" in entree
                else ""
            )
        )

    # ------------------------------------------------------------------
    # Taux de repli de l'annotateur, pour la comparaison exigée par #130.
    # ------------------------------------------------------------------
    verites_toutes = [c["categorie"] for pli in PLIS for c in par_pli[pli]]
    signalees = [v for v in verites_toutes if v != "Unflagged"]
    resultats["annotateur"] = {
        "signalees": len(signalees),
        "taux_repli_parmi_signalees": sum(
            1 for v in signalees if v == "PersonalDataUncategorised"
        )
        / len(signalees),
    }

    # ------------------------------------------------------------------
    # Contrôle de dialecte galette / galette-pg : mêmes colonnes, deux
    # dialectes — le verdict change-t-il ? (Montages déterministes ; pour le
    # modèle CPU, celui du pli galette, pire graine.)
    # ------------------------------------------------------------------
    galette_pg = [
        {**c, "id": f"galette-pg:{c['table']}:{c['colonne']}"}
        for c in lire_jsonl(racine / "corpus/schemas/pivots/galette-pg.jsonl")
        if "table" in c  # la première ligne du pivot est son en-tête
    ]
    galette_par_cle = {
        (c["table"], c["colonne"]): c for c in par_pli["galette"]
    }
    apparites = [
        (c, galette_par_cle[(c["table"], c["colonne"])])
        for c in galette_pg
        if (c["table"], c["colonne"]) in galette_par_cle
    ]
    controle: dict = {"colonnes_appariees": len(apparites), "colonnes_galette_pg": len(galette_pg)}
    for nom, moteur in montages.items():
        divergences = []
        for c_pg, c_my in apparites:
            p_pg = moteur.predire(c_pg)["categorie"]
            p_my = moteur.predire(c_my)["categorie"]
            if p_pg != p_my:
                divergences.append(
                    {"table": c_pg["table"], "colonne": c_pg["colonne"], "mysql": p_my, "pg": p_pg}
                )
        controle[nom] = {"divergences": len(divergences), "detail": divergences}
    if not args.sans_modele:
        pire = resultats["modele_cpu_graines"]["pire_graine"]
        entrainement = [c for p, cs in par_pli.items() if p != "galette" for c in cs]
        if not cache:
            print("modèle CPU : encodage des plongements (contrôle de dialecte)…")
            cache.update(ModeleCPU.encoder(toutes))
        cache_pg = ModeleCPU.encoder(galette_pg)
        modele_pg = ModeleCPU({**cache, **cache_pg})
        preds_pg = modele_pg.entrainer_predire(entrainement, galette_pg, pire)
        preds_pg_par_cle = {
            (c["table"], c["colonne"]): p["categorie"] for c, p in zip(galette_pg, preds_pg)
        }
        preds_my_par_cle = {
            (c["table"], c["colonne"]): p["categorie"]
            for c, p in zip(par_pli["galette"], predictions_par_montage["modele-cpu"]["galette"])
        }
        divergences = [
            {
                "table": t,
                "colonne": col,
                "mysql": preds_my_par_cle[(t, col)],
                "pg": preds_pg_par_cle[(t, col)],
            }
            for (t, col) in preds_my_par_cle
            if (t, col) in preds_pg_par_cle and preds_pg_par_cle[(t, col)] != preds_my_par_cle[(t, col)]
        ]
        controle["modele-cpu"] = {"divergences": len(divergences), "detail": divergences}
    resultats["controle_dialecte"] = controle

    # ------------------------------------------------------------------
    # Plafond d'annotation : F2 macro du second codeur (300 colonnes de
    # double-codage-2) contre les étiquettes du corpus.
    # ------------------------------------------------------------------
    reference = lire_jsonl(racine / "corpus/schemas/double-codage-2/reference-humaine.jsonl")
    corpus_par_id = {c["id"]: c for cs in par_pli.values() for c in cs}
    couples = [(corpus_par_id[r["id"]], r) for r in reference if r["id"] in corpus_par_id]
    v = [c["categorie"] for c, _ in couples]
    p = [r["categorie"] for _, r in couples]
    resultats["plafond_annotation"] = {
        "colonnes": len(couples),
        "f2_macro_second_codeur": f2_macro(v, p, categories_macro),
        "f2_micro_second_codeur": f2_micro(v, p),
        "regle_de_lecture": (
            "si l'estimation ponctuelle du meilleur montage entre dans la bande de "
            "désaccord humain, tout progrès supplémentaire est inmesurable sur ce corpus"
        ),
    }

    sortie.mkdir(parents=True, exist_ok=True)
    (sortie / "resultats.json").write_text(
        json.dumps(resultats, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(f"résultats écrits dans {sortie / 'resultats.json'}")


if __name__ == "__main__":
    main()
