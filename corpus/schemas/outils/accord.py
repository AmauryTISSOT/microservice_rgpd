#!/usr/bin/env python3
"""Le bruit de l'annotation : accord brut, kappa de Cohen, matrice des désaccords.

⚠️ **Ce chiffre borne ce que le banc peut prétendre.** Un moteur qui « bat » un
écart inférieur au désaccord entre deux passes n'a rien battu du tout. C'est
pourquoi le § 4 du protocole impose de le publier **avant** tout résultat de
performance du moteur, et pourquoi #130 le reprend comme plancher.

⚠️ **Et c'est un accord *intra*-annotateur** — amendement du 2026-08-09 au § 4 :
le même annotateur repasse les 300 colonnes après délai, faute d'un second
lecteur. Il mesure sa **constance**, pas la **reproductibilité du protocole**.
Une personne s'accorde avec elle-même plus qu'avec autrui : l'accord est donc
**majoré**, le bruit **minoré**, et le plancher qu'il donne au banc est
**optimiste**. Tout ce que ce script imprime porte cet avertissement, parce
qu'un tableau se recopie sans son contexte.

Trois lectures sont produites, et il faut les trois :

* **l'accord brut** — la part de colonnes où les deux étiquettes coïncident.
  Pris seul il est **trompeur** : la masse des `Unflagged` le gonfle. Deux
  codeurs qui ne s'accordent que sur « rien vu » afficheraient un accord
  flatteur ;
* **le kappa de Cohen** sur les treize valeurs, qui corrige l'accord dû au
  hasard, précisément parce que le déséquilibre ci-dessus le fausse ;
* **l'accord restreint aux colonnes signalées** — celles qu'au moins un des deux
  codeurs a sorties de `Unflagged`. C'est là que se joue la mesure.

La première passe est lue dans `annotation/`, la seconde dans
`double-codage/seconde-passe.jsonl`. C'est la première et seule fois où les deux
se rencontrent.

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
    sortie(f"\n{'passe 1 \\ passe 2':<{largeur}}  " +
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
        second = commun.lire_jsonl(commun.SECONDE_PASSE)
    except FileNotFoundError:
        sys.exit("double-codage/seconde-passe.jsonl absent — lancer d'abord "
                 "outils/tirer-double-codage.py")

    manquants_1 = [l["id"] for l in second
                   if premier.get(l["id"], {}).get("categorie") is None]
    manquants_2 = [l["id"] for l in second if l.get("categorie") is None]
    if manquants_1 or manquants_2:
        sys.exit(f"double codage incomplet : {len(manquants_1)} colonne(s) non "
                 f"annotée(s) en première passe, {len(manquants_2)} en seconde. "
                 "L'accord ne se calcule pas sur un codage partiel — il se "
                 "calculerait sur les colonnes faciles, faites en premier.")

    paires = [(premier[l["id"]]["categorie"], l["categorie"]) for l in second]
    n = len(paires)

    signalees = [(a, b) for a, b in paires
                 if commun.signalee(a) or commun.signalee(b)]

    sortie("# Accord intra-annotateur")
    sortie()
    sortie(f"{n} colonnes reprises en aveugle par le **même annotateur**, tirées "
           f"stratifiées par schéma parmi les {len(premier)} du corpus.")
    sortie()
    sortie("> ⚠️ **Ce n'est pas un accord inter-annotateurs.** Faute d'un second "
           "lecteur, le § 4 du protocole a été amendé le 2026-08-09 : le même "
           "annotateur repasse l'échantillon après délai. Ce tableau mesure sa "
           "**constance**, pas la **reproductibilité du protocole par une autre "
           "personne**.")
    sortie(">")
    sortie("> ⚠️ **Il penche du côté qui flatte.** On s'accorde avec soi-même "
           "plus qu'avec autrui : l'accord ci-dessous est **majoré**, le bruit "
           "**minoré**, et le plancher qu'il fournit au banc est donc "
           "**optimiste**. Un moteur qui le franchit de peu n'a rien démontré.")
    sortie(">")
    sortie("> ⚠️ **Une règle du § 3 mal comprise le reste aux deux passes**, et "
           "l'accord sera excellent. Un accord intra-annotateur élevé dit la "
           "stabilité du protocole, jamais sa justesse.")
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
           "`Unflagged` ; c'est la seconde ligne qui borne le banc.")

    d = desaccords_ordonnes(paires)
    sortie()
    sortie(f"## Désaccords — {len(paires) - sum(1 for a, b in paires if a == b)} "
           f"colonnes")
    sortie()
    if d:
        sortie("| Première passe | Seconde passe | n |")
        sortie("|---|---|---:|")
        for (a, b), c in d:
            sortie(f"| `{a}` | `{b}` | {c} |")
    else:
        sortie("Aucun. ⚠️ Un accord parfait sur 300 colonnes est plus "
               "vraisemblablement le signe que la seconde passe a relu la "
               "première — ou s'en souvenait — que celui d'un protocole "
               "limpide. À vérifier avant de publier.")

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
