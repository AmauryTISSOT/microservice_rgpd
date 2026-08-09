#!/usr/bin/env python3
"""La distribution réelle par catégorie, et le taux de repli qui la commente.

⚠️ **Le tirage est biaisé à dessein** : `echantillonner.py` met 70 % de l'effort
sur la strate A, là où le pré-criblage lexical soupçonne des catégories rares.
Compter les étiquettes brutes reviendrait donc à dire que le corpus est riche en
données de santé alors que c'est le *tirage* qui l'est. Chaque grappe porte sa
probabilité d'inclusion pour cette raison précise, et l'estimateur ci-dessous —
Horvitz-Thompson, une colonne pèse 1/π — la lui rend.

Les deux colonnes sont publiées côte à côte : la brute dit ce qui a été **annoté**,
la re-pondérée dit ce que le corpus **contient**. Elles ne répondent pas à la même
question et il ne faut jamais servir l'une pour l'autre.

⚠️ **Le taux de repli `PersonalDataUncategorised` est un instrument, pas un
défaut** (#127). Il mesure ce qui manque à la taxonomie. Le voir monter sur les
champs libres est un **résultat** du banc, à publier tel quel — pas un signal
qu'il faut inventer une quatorzième valeur.

Usage :  python3 outils/distribution.py [--markdown]
"""
import sys

import commun


def estimer(lignes):
    """Rend (effectifs bruts, poids Horvitz-Thompson) par catégorie."""
    brut = {v: 0 for v in commun.TAXONOMIE}
    pondere = {v: 0.0 for v in commun.TAXONOMIE}
    for l in lignes:
        cat = l["categorie"]
        p = l.get("proba_inclusion") or 0.0
        brut[cat] += 1
        # π = 0 ne devrait pas exister — une colonne tirée a été tirée. Si ça
        # arrive, on préfère l'ignorer bruyamment que lui donner un poids infini.
        if p <= 0:
            print(f"⚠️  {l['id']} : proba_inclusion nulle, exclue de "
                  "l'estimation pondérée", file=sys.stderr)
            continue
        pondere[cat] += 1.0 / p
    return brut, pondere


def main():
    commun.verifier_taxonomie()
    lignes = commun.annotees(commun.lire_annotation())
    total_corpus = len(commun.lire_annotation())
    if not lignes:
        sys.exit("aucune colonne annotée — rien à distribuer")
    if len(lignes) != total_corpus:
        print(f"⚠️  annotation partielle : {len(lignes)}/{total_corpus} colonnes. "
              "Les proportions ci-dessous ne valent que pour ce qui est fait, et "
              "les colonnes faciles se font en premier.", file=sys.stderr)

    brut, pondere = estimer(lignes)
    n = sum(brut.values())
    masse = sum(pondere.values())

    out = []
    out.append("# Distribution par catégorie")
    out.append("")
    out.append(f"{n} colonnes annotées sur {total_corpus}.")
    out.append("")
    out.append("| Catégorie | Annotées | Part annotée | Part estimée (pondérée) |")
    out.append("|---|---:|---:|---:|")
    for v in commun.TAXONOMIE:
        part_p = f"{100 * pondere[v] / masse:.1f} %" if masse else "—"
        out.append(f"| `{v}` | {brut[v]} | {commun.pourcent(brut[v], n)} | {part_p} |")
    out.append("")
    out.append("⚠️ La colonne *annotée* dit ce qui a été tiré ; la colonne "
               "*estimée* dit ce que le corpus contient. Le tirage sur-représente "
               "exprès les catégories rares.")

    signalees = sum(brut[v] for v in commun.TAXONOMIE if commun.signalee(v))
    repli = brut[commun.REPLI_PERSONNEL]
    out.append("")
    out.append("## Taux de repli — l'instrument de mesure de la taxonomie")
    out.append("")
    out.append(f"- colonnes signalées : **{signalees}** / {n} "
               f"({commun.pourcent(signalees, n)})")
    out.append(f"- dont `PersonalDataUncategorised` : **{repli}** "
               f"({commun.pourcent(repli, signalees)} des signalées, "
               f"{commun.pourcent(repli, n)} du corpus annoté)")
    part_pond = (f"{100 * pondere[commun.REPLI_PERSONNEL] / masse:.1f} %"
                 if masse else "—")
    out.append(f"- part estimée du repli sur le corpus : **{part_pond}**")
    out.append("")
    out.append("Ce taux **mesure la taxonomie de #127**, il ne la juge pas. Un "
               "repli massif sur les champs libres est le résultat attendu du "
               "§ 3.3 du protocole.")

    out.append("")
    out.append("## Ce qui restera muet, et qui était annoncé")
    out.append("")
    for v, attendu in (("CriminalOffenceData", "zéro sur les 15 048 colonnes du corpus"),
                       ("SpecialCategoryData", "de l'ordre de dix instances"),
                       ("HealthData", "509 des 521 candidats sont chez OpenEMR, en anglais")):
        out.append(f"- `{v}` : {brut[v]} annotée(s) — attendu : {attendu}.")
    out.append("")
    out.append("Ce sont des **trous de corpus déclarés par #129 et repris au § 5 "
               "du protocole**, pas des lacunes d'annotation. Aucun rappel ni "
               "aucune précision n'est estimable sur de tels effectifs.")

    texte = "\n".join(out)
    print(texte)
    if "--markdown" in sys.argv:
        chemin = f"{commun.RACINE}/distribution.md"
        with open(chemin, "w", encoding="utf-8") as f:
            f.write(texte + "\n")
        print(f"\n→ {chemin}", file=sys.stderr)


if __name__ == "__main__":
    main()
