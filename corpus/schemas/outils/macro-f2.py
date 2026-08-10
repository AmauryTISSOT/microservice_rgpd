#!/usr/bin/env python3
"""L'ensemble des catégories de la macro F2, dérivé mécaniquement de `annotation/`.

La règle est celle du protocole de mesure (#130), écrite avant tout chiffre :

> La macro est moyennée sur les catégories peuplées dans au moins cinq plis
> sur six, ensemble fixé et publié à l'issue de #142, avant tout chiffre de
> moteur.

Ce script est le porteur de l'obligation (#157, cause structurelle constatée
par #154) : l'ensemble n'est pas énuméré à la main, il se **calcule** depuis
les fichiers d'annotation, et sa publication (`macro-f2.md` + `macro-f2.json`)
est commitée avant le premier commit d'exécution du banc. Aucun chiffre de
moteur n'entre ici ni n'en sort.

Les six plis sont ceux de #130 : `dolibarr` · `glpi` · `openemr` · `sacoche` ·
`paheko` (trois états réunis) · `galette`. `galette-pg` est exclu de la mesure
et gardé en contrôle de dialecte — ses étiquettes se transfèrent par nom, il
n'a pas de fichier d'annotation propre. `temoin` est exclu des deux côtés.

⚠️ **`Unflagged` n'entre pas dans la macro, et c'est une règle de lecture à
publier, pas une évidence à taire.** La F2 par catégorie est une mesure
un-contre-tous de *détection* ; `Unflagged` est sa classe négative — l'absence
de signalement, pas une catégorie de données (#127 : « motif présent ⇔ ce n'est
pas `Unflagged` »). Peuplée à ~76 % du corpus dans les six plis, elle
franchirait le seuil trivialement et offrirait à chaque montage une catégorie
gratuite dominée par la classe majoritaire — l'inverse exact du motif qui a
fait choisir la macro contre la micro (« c'est la moyenne macro qui rend
visible qu'une catégorie est morte »).

Usage :  python3 outils/macro-f2.py [--publier]
"""
import json
import sys

import commun

# Les six plis de #130. L'ordre est celui du protocole.
PLIS = {
    "dolibarr": ["dolibarr"],
    "glpi": ["glpi"],
    "openemr": ["openemr"],
    "sacoche": ["sacoche"],
    "paheko": ["paheko-0.8.0", "paheko-1.0.0", "paheko-head"],
    "galette": ["galette"],
}
SEUIL = 5  # « au moins cinq plis sur six »

REGLE = ("La macro est moyennée sur les catégories peuplées dans au moins "
         "cinq plis sur six, ensemble fixé et publié à l'issue de #142, "
         "avant tout chiffre de moteur.")

MOTIF_UNFLAGGED = (
    "Classe négative de la F2 un-contre-tous, pas une catégorie de données "
    "(#127) : peuplée dans les six plis, elle franchirait le seuil "
    "trivialement et fondrait la classe majoritaire dans la moyenne que la "
    "macro existe pour protéger.")

# Ce que #129 et #137 avaient déclaré d'avance sur les catégories creuses —
# rappelé ici pour que l'exclusion se lise comme un trou déclaré, pas comme
# une découverte.
ANNONCES = {
    "CriminalOffenceData": "zéro attendu sur les 15 048 colonnes (#137 : "
                           "propriété de la population, pas trou de corpus)",
    "SpecialCategoryData": "de l'ordre de dix instances attendues (#129)",
    "HealthData": "mono-schéma attendu — 509 des 521 candidats chez OpenEMR (#129)",
}


def peuplement():
    """Compte, par pli et par catégorie, les colonnes annotées ainsi."""
    comptes = {p: {v: 0 for v in commun.TAXONOMIE} for p in PLIS}
    for pli, sources in PLIS.items():
        lignes = commun.lire_annotation(sources)
        vides = [l["id"] for l in lignes if l.get("categorie") is None]
        if vides:
            sys.exit(f"pli {pli} : {len(vides)} colonne(s) sans catégorie — "
                     "l'ensemble ne se dérive que d'une annotation complète")
        for l in lignes:
            comptes[pli][l["categorie"]] += 1
    return comptes


def deriver(comptes):
    """Applique la règle et rend (retenues, exclues) avec leur nombre de plis."""
    plis_peuples = {
        v: sum(1 for p in PLIS if comptes[p][v] > 0)
        for v in commun.TAXONOMIE if v != commun.NON_SIGNALEE
    }
    retenues = [v for v, n in plis_peuples.items() if n >= SEUIL]
    exclues = [v for v, n in plis_peuples.items() if n < SEUIL]
    return plis_peuples, retenues, exclues


def rendre_markdown(comptes, plis_peuples, retenues, exclues):
    out = []
    out.append("# L'ensemble des catégories de la macro F2")
    out.append("")
    out.append(f"> {REGLE}")
    out.append("")
    out.append("Dérivé mécaniquement de `annotation/` par `outils/macro-f2.py` "
               "(#157). Plis de #130 : " +
               " · ".join(f"`{p}`" for p in PLIS) +
               " (`paheko` réunit ses trois états ; `galette-pg` est contrôle "
               "de dialecte, ses étiquettes se transfèrent par nom ; `temoin` "
               "est exclu des deux côtés). Aucun chiffre de moteur ici.")
    out.append("")
    out.append("## Catégories retenues")
    out.append("")
    for v in retenues:
        out.append(f"- `{v}` — peuplée dans {plis_peuples[v]} plis sur {len(PLIS)}")
    out.append("")
    out.append("## Catégories exclues de la macro — rapportées séparément, "
               "jamais fondues dans un score")
    out.append("")
    out.append("| Catégorie | Plis peuplés | " +
               " | ".join(f"`{p}`" for p in PLIS) + " |")
    out.append("|---|---:|" + "---:|" * len(PLIS))
    for v in exclues:
        out.append(f"| `{v}` | {plis_peuples[v]}/{len(PLIS)} | " +
                   " | ".join(str(comptes[p][v]) for p in PLIS) + " |")
    out.append("")
    for v in exclues:
        if v in ANNONCES:
            out.append(f"- `{v}` : {ANNONCES[v]}.")
    out.append("")
    out.append("⚠️ Le banc publie la F2 par catégorie **y compris** pour les "
               "exclues, avec ce motif d'exclusion nommé (#130).")
    out.append("")
    out.append("## `Unflagged` n'est pas une catégorie de la macro")
    out.append("")
    out.append(MOTIF_UNFLAGGED)
    out.append("")
    out.append("## Peuplement complet par pli")
    out.append("")
    out.append("| Catégorie | " + " | ".join(f"`{p}`" for p in PLIS) +
               " | Plis peuplés |")
    out.append("|---|" + "---:|" * len(PLIS) + "---:|")
    for v in commun.TAXONOMIE:
        n = ("—" if v == commun.NON_SIGNALEE
             else f"{plis_peuples[v]}/{len(PLIS)}")
        out.append(f"| `{v}` | " +
                   " | ".join(str(comptes[p][v]) for p in PLIS) + f" | {n} |")
    out.append("")
    out.append("Totaux par pli : " +
               " · ".join(f"`{p}` {sum(comptes[p].values())}" for p in PLIS) +
               ".")
    return "\n".join(out)


def main():
    commun.verifier_taxonomie()
    comptes = peuplement()
    plis_peuples, retenues, exclues = deriver(comptes)

    texte = rendre_markdown(comptes, plis_peuples, retenues, exclues)
    print(texte)

    if "--publier" in sys.argv:
        with open(f"{commun.RACINE}/macro-f2.md", "w", encoding="utf-8") as f:
            f.write(texte + "\n")
        donnees = {
            "regle": REGLE,
            "seuil_plis": SEUIL,
            "plis": PLIS,
            "peuplement": {v: {p: comptes[p][v] for p in PLIS}
                           for v in commun.TAXONOMIE},
            "macro": retenues,
            "exclues": [{"categorie": v, "plis_peuples": plis_peuples[v]}
                        for v in exclues],
            "unflagged_hors_macro": MOTIF_UNFLAGGED,
        }
        with open(f"{commun.RACINE}/macro-f2.json", "w", encoding="utf-8") as f:
            json.dump(donnees, f, ensure_ascii=False, indent=2)
            f.write("\n")
        print(f"\n→ {commun.RACINE}/macro-f2.md", file=sys.stderr)
        print(f"→ {commun.RACINE}/macro-f2.json", file=sys.stderr)


if __name__ == "__main__":
    main()
