#!/usr/bin/env python3
"""Ce que partagent les outils d'annotation : la taxonomie, sa lecture, ses fichiers.

La taxonomie est recopiée ici sous une forme exécutable parce que les outils en
ont besoin *mécaniquement* — pour valider une valeur, pour ordonner les treize
colonnes d'une matrice de désaccords. La source de vérité reste
`docs/contexts/screening/CONTEXT.md`, tranchée par #127 ; ce fichier en est un
reflet, et `verifier_taxonomie()` échoue si les deux divergent.
"""
import json
import os
import re
import sys

RACINE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ANNOTATION = os.path.join(RACINE, "annotation")
DOUBLE_CODAGE = os.path.join(RACINE, "double-codage")
PLAN = os.path.join(RACINE, "plan-de-sondage.json")
CONTEXTE = os.path.join(
    os.path.dirname(os.path.dirname(RACINE)),
    "docs", "contexts", "screening", "CONTEXT.md")

# L'ordre EST l'ordre d'arbitrage de #127, du plus au moins coûteux à omettre.
# Il n'est pas décoratif : `plus_haute()` s'en sert pour départager, et les
# matrices s'affichent dans cet ordre pour que les confusions de voisinage
# sautent aux yeux.
TAXONOMIE = [
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

RANG = {v: i for i, v in enumerate(TAXONOMIE)}

# Les deux replis, nommés : les confondre corrompt le banc (#127, § 3 du protocole).
NON_SIGNALEE = "Unflagged"
REPLI_PERSONNEL = "PersonalDataUncategorised"

SCHEMAS = ["dolibarr", "galette", "glpi", "openemr", "paheko-0.8.0",
           "paheko-1.0.0", "paheko-head", "sacoche", "temoin"]


def signalee(categorie):
    """Une colonne est signalée dès qu'elle n'est pas `Unflagged`.

    `PersonalDataUncategorised` EST une colonne signalée : c'est un verdict —
    personnelle, mais aucune valeur ne lui va — et non un aveu d'ignorance.
    """
    return categorie is not None and categorie != NON_SIGNALEE


def plus_haute(categories):
    """Départage selon l'ordre d'arbitrage : la plus haute du tableau l'emporte."""
    return min(categories, key=lambda c: RANG[c])


def lire_jsonl(chemin):
    lignes = []
    with open(chemin, encoding="utf-8") as f:
        for n, brut in enumerate(f, 1):
            brut = brut.strip()
            if not brut:
                continue
            try:
                lignes.append(json.loads(brut))
            except json.JSONDecodeError as e:
                sys.exit(f"{chemin}:{n} : JSON illisible — {e}")
    return lignes


def ecrire_jsonl(chemin, lignes):
    os.makedirs(os.path.dirname(chemin), exist_ok=True)
    with open(chemin, "w", encoding="utf-8") as f:
        for l in lignes:
            f.write(json.dumps(l, ensure_ascii=False) + "\n")


def lire_annotation(schemas=None):
    """Les 3 254 lignes d'annotation, dans l'ordre stable du plan de sondage."""
    lignes = []
    for nom in (schemas or SCHEMAS):
        chemin = os.path.join(ANNOTATION, f"{nom}.annotation.jsonl")
        if not os.path.exists(chemin):
            sys.exit(f"absent : {chemin} — lancer outils/echantillonner.py d'abord")
        lignes.extend(lire_jsonl(chemin))
    return lignes


def lire_plan():
    with open(PLAN, encoding="utf-8") as f:
        return json.load(f)


def verifier_taxonomie():
    """Échoue si `TAXONOMIE` a divergé du glossaire de Screening.

    Une taxonomie recopiée est une taxonomie qui dérive. Plutôt que d'espérer
    qu'on pensera à la resynchroniser, on refuse de tourner quand elle a bougé :
    un banc mesuré contre une liste périmée est pire qu'un banc qui ne tourne pas.
    """
    if not os.path.exists(CONTEXTE):
        return  # le contexte n'est pas encore écrit : rien à confronter
    with open(CONTEXTE, encoding="utf-8") as f:
        texte = f.read()
    manquantes = [v for v in TAXONOMIE if not re.search(rf"\b{v}\b", texte)]
    if manquantes:
        sys.exit("TAXONOMIE a divergé de docs/contexts/screening/CONTEXT.md — "
                 f"absentes du glossaire : {', '.join(manquantes)}")


def annotees(lignes):
    return [l for l in lignes if l.get("categorie") is not None]


def pourcent(n, d):
    return f"{100.0 * n / d:.1f} %" if d else "—"
