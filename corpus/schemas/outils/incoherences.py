#!/usr/bin/env python3
"""Repère les colonnes de même nom étiquetées différemment, et rien d'autre.

**Pourquoi cet outil existe.** Le corpus a été annoté par lots, et un lot est un
annotateur. Deux annotateurs qui tranchent différemment la même colonne
produisent une vérité terrain qui fait payer au moteur une incohérence qui est la
nôtre. Le § 6 du protocole prévoit la procédure — la famille remonte au § 3 par
amendement daté, et les colonnes concernées sont repassées — mais encore
faut-il **voir** les familles.

⚠️ **Un désaccord n'est pas forcément une erreur.** `name` dans une table de
personnes et `name` dans une table de nomenclature relèvent légitimement de deux
étiquettes : c'est le § 3.3 et le § 3.4 qui le veulent. Cet outil ne corrige
rien et n'accuse personne ; il **classe** les divergences pour qu'un humain
tranche celles qui doivent l'être.

Deux niveaux, et le premier est bien plus grave que le second :

* **intra-schéma** — même logiciel, même nom de colonne, deux étiquettes. Le
  contexte de table peut le justifier, mais c'est ici que se cachent les vraies
  incohérences d'annotateur ;
* **inter-schémas** — deux logiciels différents. Souvent légitime : `code` chez
  GLPI et `code` chez Galette ne désignent pas la même chose.

Usage :  python3 outils/incoherences.py [--seuil N] [--markdown]
"""
import collections
import sys

import commun


def divergences(lignes, par_schema):
    """{(clé, colonne): {catégorie: [ids]}} pour les colonnes multi-étiquetées."""
    index = collections.defaultdict(lambda: collections.defaultdict(list))
    for l in lignes:
        cle = l["schema_source"] if par_schema else "*"
        index[(cle, l["colonne"].lower())][l["categorie"]].append(l)
    return {k: v for k, v in index.items() if len(v) > 1}


def bloc(titre, div, sortie, seuil):
    sortie(f"## {titre} — {len(div)} nom(s) de colonne")
    sortie()
    if not div:
        sortie("Aucune.")
        sortie()
        return
    ordonne = sorted(div.items(),
                     key=lambda kv: sum(len(v) for v in kv[1].values()),
                     reverse=True)
    for (cle, colonne), cats in ordonne:
        total = sum(len(v) for v in cats.values())
        if total < seuil:
            continue
        prefixe = "" if cle == "*" else f"`{cle}` · "
        sortie(f"### {prefixe}`{colonne}` — {total} occurrence(s)")
        sortie()
        for cat, ls in sorted(cats.items(), key=lambda kv: -len(kv[1])):
            exemples = ", ".join(f"`{l['table']}`" for l in ls[:4])
            reste = f" +{len(ls) - 4}" if len(ls) > 4 else ""
            sortie(f"- **{cat}** ({len(ls)}) — {exemples}{reste}")
            # Le motif dit pourquoi ; c'est lui qui rend le désaccord jugeable.
            motif = next((l["motif"] for l in ls if l["motif"]), None)
            if motif:
                sortie(f"  - motif : « {motif} »")
        sortie()


def main():
    commun.verifier_taxonomie()
    seuil = 2
    if "--seuil" in sys.argv:
        seuil = int(sys.argv[sys.argv.index("--seuil") + 1])

    out = []

    def sortie(s=""):
        out.append(s)

    lignes = commun.annotees(commun.lire_annotation())
    intra = divergences(lignes, par_schema=True)
    inter = divergences(lignes, par_schema=False)

    sortie("# Divergences d'étiquetage entre lots")
    sortie()
    sortie(f"{len(lignes)} colonnes annotées. Un lot est un annotateur ; ce "
           "rapport cherche les colonnes de même nom qu'ils ont tranchées "
           "différemment.")
    sortie()
    sortie("⚠️ **Une divergence n'est pas forcément une erreur** : le § 3.3 et "
           "le § 3.4 font légitimement dépendre l'étiquette de la table. Ce "
           "rapport classe, il ne corrige pas.")
    sortie()
    bloc("Intra-schéma — même logiciel", intra, sortie, seuil)
    bloc("Inter-schémas — logiciels différents", inter, sortie, seuil)

    texte = "\n".join(out)
    print(texte)
    if "--markdown" in sys.argv:
        chemin = f"{commun.RACINE}/divergences.md"
        with open(chemin, "w", encoding="utf-8") as f:
            f.write(texte + "\n")
        print(f"\n→ {chemin}", file=sys.stderr)


if __name__ == "__main__":
    main()
