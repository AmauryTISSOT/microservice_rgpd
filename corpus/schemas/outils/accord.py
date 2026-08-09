#!/usr/bin/env python3
"""Accord machine–humain : la vérité terrain est-elle utilisable ?

⚠️ **Ce script ne mesure plus du bruit d'annotation.** Depuis l'amendement n° 2
du 2026-08-09 au § 4, les 3 254 étiquettes viennent de sous-agents et les **300
colonnes de l'échantillon sont codées à la main**. Ce qu'on confronte ici, c'est
donc une machine à un humain — et la question n'est plus « quel est le bruit ? »
mais **« ces étiquettes machine peuvent-elles servir de vérité terrain ? »**.

**Les 300 colonnes humaines font référence.** Les 3 254 sont **provisoires**
jusqu'à ce que ce chiffre les valide.

Trois lectures sont produites, et il faut les trois :

* **l'accord brut** — la part de colonnes où les deux étiquettes coïncident.
  Pris seul il est **trompeur** : la masse des `Unflagged` le gonfle, et les 436
  colonnes des familles mécaniques du § 3.1 s'accordent par simple application de
  la règle ;
* **le kappa de Cohen** sur les treize valeurs, qui corrige l'accord dû au
  hasard, précisément parce que le déséquilibre ci-dessus le fausse ;
* **l'accord restreint aux colonnes signalées** — celles qu'au moins un des deux
  a sorties de `Unflagged`. ⚠️ **C'est la seule ligne qui dit quelque chose**, et
  c'est elle qu'il faut citer.

⚠️ **La circularité doit être déclarée par #134.** Si le moteur retenu est un
modèle de langue, la vérité terrain et le concurrent relèvent de la même
technologie et le banc se mesure en partie lui-même. Si le moteur est lexical et
morphologique, le problème est nettement plus faible. Ce script ne peut pas
trancher ça ; il rappelle seulement qu'il faut le faire.

Les étiquettes machine sont lues dans `annotation/`, la référence humaine dans
`double-codage/reference-humaine.jsonl`. C'est la première et seule fois où les
deux se rencontrent.

Usage :  python3 outils/accord.py [--markdown]
"""
import sys

import commun


def kappa_cohen(paires, valeurs):
    """(po - pe) / (1 - pe) sur l'espace `valeurs`.

    Rend None quand pe vaut 1 : les deux codeurs n'ont employé qu'une seule
    valeur, le hasard « explique » alors tout l'accord et le kappa n'est pas
    défini. Le taire vaut mieux que publier un 0/0 déguisé en 0.
    """
    n = len(paires)
    if not n:
        return None
    po = sum(1 for a, b in paires if a == b) / n
    m1 = {v: sum(1 for a, _ in paires if a == v) / n for v in valeurs}
    m2 = {v: sum(1 for _, b in paires if b == v) / n for v in valeurs}
    pe = sum(m1[v] * m2[v] for v in valeurs)
    if abs(1.0 - pe) < 1e-12:
        return None
    return (po - pe) / (1.0 - pe)


def accord_brut(paires):
    if not paires:
        return None
    return sum(1 for a, b in paires if a == b) / len(paires)


def matrice(paires, valeurs):
    m = {a: {b: 0 for b in valeurs} for a in valeurs}
    for a, b in paires:
        m[a][b] += 1
    return m


def afficher_matrice(m, valeurs, sortie):
    presentes = [v for v in valeurs
                 if any(m[v].values()) or any(m[a][v] for a in valeurs)]
    if not presentes:
        return
    largeur = max(len(v) for v in presentes)
    sortie(f"\n{'machine \\ humain':<{largeur}}  " +
           "  ".join(f"{v[:6]:>6}" for v in presentes))
    for a in presentes:
        sortie(f"{a:<{largeur}}  " +
               "  ".join(f"{m[a][b] or '·':>6}" for b in presentes))


def desaccords_ordonnes(paires):
    compte = {}
    for a, b in paires:
        if a != b:
            compte[(a, b)] = compte.get((a, b), 0) + 1
    return sorted(compte.items(), key=lambda kv: kv[1], reverse=True)


def main():
    commun.verifier_taxonomie()
    lignes = []

    def sortie(s=""):
        lignes.append(s)

    premier = {l["id"]: l for l in commun.lire_annotation()}
    try:
        second = commun.lire_jsonl(commun.REFERENCE_HUMAINE)
    except FileNotFoundError:
        sys.exit("double-codage/reference-humaine.jsonl absent — lancer d'abord "
                 "outils/tirer-double-codage.py")

    manquants_1 = [l["id"] for l in second
                   if premier.get(l["id"], {}).get("categorie") is None]
    manquants_2 = [l["id"] for l in second if l.get("categorie") is None]
    if manquants_1 or manquants_2:
        sys.exit(f"confrontation impossible : {len(manquants_1)} colonne(s) sans "
                 f"étiquette machine, {len(manquants_2)} sans référence humaine. "
                 "L'accord ne se calcule pas sur un codage partiel — il se "
                 "calculerait sur les colonnes faciles, faites en premier.")

    paires = [(premier[l["id"]]["categorie"], l["categorie"]) for l in second]
    n = len(paires)

    signalees = [(a, b) for a, b in paires
                 if commun.signalee(a) or commun.signalee(b)]

    sortie("# Accord machine–humain")
    sortie()
    sortie(f"{n} colonnes codées **à la main** par l'annotateur humain, en "
           f"aveugle, confrontées aux étiquettes produites par sous-agents sur "
           f"les {len(premier)} colonnes du corpus.")
    sortie()
    sortie("> ⚠️ **Ce n'est pas du bruit d'annotation.** Amendement n° 2 du "
           "2026-08-09 au § 4 : la première passe est machine. Ce tableau ne "
           "borne pas le banc — il dit si la vérité terrain est **utilisable**. "
           "Les 300 colonnes humaines font **référence** ; les étiquettes "
           "machine sont **provisoires** jusqu'à ce chiffre.")
    sortie(">")
    sortie("> ⚠️ **Seule la ligne « colonnes signalées » veut dire quelque "
           "chose.** L'accord global est gonflé par la masse des `Unflagged` et "
           "par les 436 colonnes des familles mécaniques du § 3.1, qui "
           "s'accordent par simple application de la règle.")
    sortie(">")
    sortie("> ⚠️ **Circularité à déclarer par #134.** Si le moteur retenu est un "
           "modèle de langue, la vérité terrain et le concurrent relèvent de la "
           "même technologie et le banc se mesure en partie lui-même. Si le "
           "moteur est lexical et morphologique, le problème est bien plus "
           "faible.")
    sortie(">")
    sortie("> ⚠️ **Ces 300 colonnes valident en moyenne, pas colonne par "
           "colonne.** Une famille où les agents se trompent systématiquement, "
           "et que l'échantillon touche peu, passera au travers. La matrice des "
           "désaccords est le seul endroit où ça se verra.")
    sortie()

    ab = accord_brut(paires)
    k = kappa_cohen(paires, commun.TAXONOMIE)
    ab_s = accord_brut(signalees)
    k_s = kappa_cohen(signalees, commun.TAXONOMIE)

    def pc(x):
        return "—" if x is None else f"{100 * x:.1f} %"

    def kp(x):
        return "non défini" if x is None else f"{x:.3f}"

    sortie("| Mesure | Effectif | Accord brut | Kappa de Cohen |")
    sortie("|---|---:|---:|---:|")
    sortie(f"| Toutes colonnes | {n} | {pc(ab)} | {kp(k)} |")
    sortie(f"| Colonnes signalées | {len(signalees)} | {pc(ab_s)} | {kp(k_s)} |")
    sortie()
    sortie("⚠️ L'accord *toutes colonnes* est gonflé par la masse des "
           "`Unflagged` et par les familles mécaniques du § 3.1 ; c'est la "
           "seconde ligne qu'il faut citer.")

    d = desaccords_ordonnes(paires)
    sortie()
    sortie(f"## Désaccords — {len(paires) - sum(1 for a, b in paires if a == b)} "
           f"colonnes")
    sortie()
    if d:
        sortie("| Étiquette machine | Référence humaine | n |")
        sortie("|---|---|---:|")
        for (a, b), c in d:
            sortie(f"| `{a}` | `{b}` | {c} |")
    else:
        sortie("Aucun. ⚠️ Un accord parfait sur 300 colonnes est plus "
               "vraisemblablement le signe que la référence humaine a été codée "
               "en voyant les étiquettes machine que celui d'une vérité terrain "
               "parfaite. À vérifier avant de publier.")

    sortie()
    sortie("## Matrice des désaccords")
    sortie()
    sortie("```")
    afficher_matrice(matrice(paires, commun.TAXONOMIE), commun.TAXONOMIE, sortie)
    sortie("```")

    texte = "\n".join(lignes)
    print(texte)
    if "--markdown" in sys.argv:
        chemin = f"{commun.DOUBLE_CODAGE}/accord.md"
        with open(chemin, "w", encoding="utf-8") as f:
            f.write(texte + "\n")
        print(f"\n→ {chemin}", file=sys.stderr)


if __name__ == "__main__":
    main()
