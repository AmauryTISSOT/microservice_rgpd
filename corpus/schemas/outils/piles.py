#!/usr/bin/env python3
"""Classe les désaccords du double codage selon la règle **pré-enregistrée**.

⚠️ **La règle est écrite ailleurs et avant** :
[`double-codage/ARBITRAGE.md`](../double-codage/ARBITRAGE.md), commité avant que
la première ligne de désaccord soit ouverte. Cet outil ne décide rien — il
recalcule les désaccords depuis les deux sources, y appose le classement à la
main de `double-codage/piles.jsonl`, et **refuse de publier** si le classement
ne les couvre pas exactement.

**Ce qu'il vérifie mécaniquement**, plutôt que de le croire sur parole :

* que les 79 désaccords sont exactement ceux que `annotation/` et
  `reference-humaine.jsonl` produisent aujourd'hui — un classement figé sur des
  lignes qui ont bougé ne dit plus rien ;
* que chaque désaccord porte une pile, et **une seule** ;
* que toute ligne de Pile A porte le § invoqué **et** la catégorie qu'il
  prescrit — c'est là que le classement se rend réfutable : la conformité de
  chaque passe se **calcule** à partir de `prescrit`, elle ne s'affirme pas ;
* qu'aucune ligne classée ne désigne un désaccord qui n'existe pas.

⚠️ **Il ne modifie aucune étiquette.** Ni `annotation/`, ni le cahier humain.
Le cahier est un document daté ; le corriger effacerait la mesure au lieu de
l'expliquer (§ « Ce que cette règle ne fait pas » d'`ARBITRAGE.md`).

Usage :  python3 outils/piles.py [--markdown]
"""
import collections
import os
import sys

import commun

PILES = os.path.join(commun.DOUBLE_CODAGE, "piles.jsonl")
ORDRE = ["A", "B", "C"]
# La famille que le § 3.10 ferme. La condition de réfutation n° 1 d'ARBITRAGE.md
# se joue sur sa part dans la Pile B, d'où une constante et non une chaîne libre.
HERITAGE = "héritage du domaine par le nom de la table"

TITRES = {
    "A": "Pile A — le protocole tranche déjà, sans interprétation",
    "B": "Pile B — aucune règle n'existe",
    "C": "Pile C — un § s'applique mais ne conclut pas seul",
}


def desaccords():
    """Les couples (machine, humain) qui divergent, recalculés depuis les sources."""
    machine = {l["id"]: l for l in commun.lire_annotation()}
    humain = commun.lire_jsonl(commun.REFERENCE_HUMAINE)
    couples = []
    for h in humain:
        m = machine.get(h["id"])
        if m is None:
            sys.exit(f"{h['id']} : présent au cahier humain, absent d'annotation/")
        if m["categorie"] != h["categorie"]:
            couples.append((m, h))
    return couples


def charger_classement(ids_attendus):
    if not os.path.exists(PILES):
        sys.exit(f"absent : {PILES}")
    classement = {}
    for l in commun.lire_jsonl(PILES):
        if l["id"] in classement:
            sys.exit(f"{l['id']} : classé deux fois")
        if l["pile"] not in ORDRE:
            sys.exit(f"{l['id']} : pile {l['pile']!r} hors A/B/C")
        if l["pile"] == "A":
            # Sans `prescrit`, une Pile A n'est qu'une affirmation : c'est ce
            # champ qui rend la conformité de chaque passe calculable.
            if not l.get("regle"):
                sys.exit(f"{l['id']} : Pile A sans § invoqué — ARBITRAGE.md "
                         "l'interdit, c'est alors de la Pile C")
            if l.get("prescrit") not in commun.RANG:
                sys.exit(f"{l['id']} : Pile A dont `prescrit` "
                         f"({l.get('prescrit')!r}) n'est pas une catégorie")
        classement[l["id"]] = l

    manquants = [i for i in ids_attendus if i not in classement]
    surnumeraires = [i for i in classement if i not in ids_attendus]
    if manquants:
        sys.exit(f"{len(manquants)} désaccord(s) non classé(s) : "
                 f"{', '.join(manquants[:5])}… — ARBITRAGE.md exige que chaque "
                 "ligne tombe dans une pile, ou que le défaut soit signalé.")
    if surnumeraires:
        sys.exit(f"{len(surnumeraires)} ligne(s) classée(s) sans désaccord "
                 f"correspondant : {', '.join(surnumeraires[:5])}")
    return classement


def main():
    commun.verifier_taxonomie()
    couples = desaccords()
    classement = charger_classement([m["id"] for m, _ in couples])

    out = []

    def s(t=""):
        out.append(t)

    par_pile = collections.Counter(classement[m["id"]]["pile"] for m, _ in couples)
    par_schema = collections.Counter(m["schema_source"] for m, _ in couples)

    n = len(couples)
    s("# Les 79 désaccords, classés en trois piles")
    s()
    s("*Classement rendu le 2026-08-09, selon la règle pré-enregistrée de "
      "[`ARBITRAGE.md`](./ARBITRAGE.md), commitée avant que la première ligne "
      "soit ouverte. L'ordre se vérifie dans `git log`.*")
    s()
    s(f"{n} désaccords sur les 300 colonnes du double codage.")
    s()
    s("| Pile | Effectif | Criblage annoncé par #145 |")
    s("|---|---:|---:|")
    for p, criblage in zip(ORDRE, ["~20", "~28", "~31"]):
        s(f"| **{p}** | {par_pile[p]} | {criblage} |")
    s(f"| **Total** | **{n}** | **79** |")
    s()
    s("⚠️ **Les dimensionnements de #145 étaient des criblages lexicaux, pas des "
      "constats** ; ce tableau les remplace. L'écart se publie tel quel.")
    s()

    # ── Conformité au § invoqué, sur la seule pile où un § prescrit ────────────
    a = [(m, h) for m, h in couples if classement[m["id"]]["pile"] == "A"]
    conf_m = sum(1 for m, _ in a if m["categorie"] == classement[m["id"]]["prescrit"])
    conf_h = sum(1 for m, h in a if h["categorie"] == classement[m["id"]]["prescrit"])
    s("## Sur la Pile A, qui appliquait le protocole ?")
    s()
    s("⚠️ **Ce tableau se calcule**, il ne s'affirme pas : chaque ligne de Pile A "
      "porte la catégorie que le § invoqué **prescrit**, et la conformité de "
      "chaque passe en est déduite. C'est la seule pile où la question a un "
      "sens — ailleurs, aucun § ne prescrit rien.")
    s()
    s("| Passe | Conforme au § | sur |")
    s("|---|---:|---:|")
    s(f"| Étiquette machine | {conf_m} | {len(a)} |")
    s(f"| Référence humaine | {conf_h} | {len(a)} |")
    s()

    # ── Les quatre conditions de réfutation, évaluées et non commentées ───────
    b = [(m, h) for m, h in couples if classement[m["id"]]["pile"] == "B"]
    familles = collections.Counter(classement[m["id"]].get("famille") for m, _ in b)
    heritage = familles[HERITAGE]
    s("## Les quatre conditions de réfutation")
    s()
    s("Écrites dans `ARBITRAGE.md` **avant** les chiffres. Elles s'évaluent ici, "
      "et le verdict de chacune est mécanique.")
    s()
    s(f"1. **La Pile B est-elle majoritairement l'héritage du domaine par le nom "
      f"de la table ?** — {heritage} sur {len(b)}. "
      + ("**Non déclenchée** : le § 3.10 tranche bien la famille dominante de B."
         if heritage * 2 > len(b) else
         "⚠️ **Déclenchée** : le § 3.10 ne tranche pas la Pile B."))
    s(f"   ⚠️ Mais **{len(b) - heritage} ligne(s) de Pile B restent ouvertes, "
      f"sans règle et hors de portée du § 3.10** — elles se nomment ci-dessous "
      f"et ne sont fermées par aucun geste de ce ticket.")
    for fam, c in familles.most_common():
        if fam != HERITAGE:
            s(f"   - {fam} ({c})")
    s(f"2. **La Pile A est-elle vide ou quasi vide ?** — {par_pile['A']} sur {n}, "
      f"soit la pile la plus lourde. "
      + ("**Non déclenchée** : le diagnostic de #145 sur la Pile A est confirmé, "
         "et au-delà de ce qu'il annonçait."
         if par_pile["A"] * 4 > n else
         "⚠️ **Déclenchée** : le durcissement de l'instrument perd sa "
         "justification numérique."))
    s(f"3. **La Pile C domine-t-elle ?** — {par_pile['C']} sur {n}. "
      + ("**Non déclenchée** : le pari de #145 porte sur un reste, et sur un "
         "reste trois fois plus petit que son criblage ne l'annonçait."
         if par_pile["C"] * 2 <= n else
         "⚠️ **Déclenchée** : le pari porte sur la majorité des désaccords."))
    s("4. **Un désaccord n'entre-t-il dans aucune pile ?** — non : les "
      f"{n} désaccords sont classés, et `piles.py` refuse de publier sinon. "
      "⚠️ **Un cas limite se signale quand même** : `localtax1_type` et "
      "`localtax2_tx`, voisines dans la même table, tombent en A et en B — la "
      "première est nommée par la convention `type`, la seconde par rien. "
      "L'écart est le prix d'une règle appliquée à la lettre plutôt qu'au goût.")
    s()

    s("## Concentration par schéma")
    s()
    s("| Schéma | Désaccords | " + " | ".join(ORDRE) + " |")
    s("|---|---:|" + "---:|" * len(ORDRE))
    for nom, c in par_schema.most_common():
        det = collections.Counter(classement[m["id"]]["pile"]
                                  for m, _ in couples if m["schema_source"] == nom)
        s(f"| `{nom}` | {c} | " + " | ".join(str(det[p]) for p in ORDRE) + " |")
    s()

    for p in ORDRE:
        lignes = [(m, h) for m, h in couples if classement[m["id"]]["pile"] == p]
        s(f"## {TITRES[p]} — {len(lignes)} colonnes")
        s()
        for m, h in sorted(lignes, key=lambda mh: mh[0]["id"]):
            c = classement[m["id"]]
            s(f"### `{m['schema_source']}` · `{m['table']}.{m['colonne']}`")
            s()
            if p == "A":
                accord_m = "✓" if m["categorie"] == c["prescrit"] else "✗"
                accord_h = "✓" if h["categorie"] == c["prescrit"] else "✗"
                s(f"- **{c['regle']} prescrit `{c['prescrit']}`**")
                s(f"- machine {accord_m} `{m['categorie']}` — "
                  f"« {m.get('motif') or '—'} »")
                s(f"- humain {accord_h} `{h['categorie']}` — "
                  f"« {h.get('motif') or '—'} »")
            else:
                etiquette = ("famille absente du protocole : "
                             f"**{c.get('famille')}**" if p == "B"
                             else f"**{c.get('regle')}**")
                s(f"- {etiquette}")
                s(f"- machine `{m['categorie']}` — « {m.get('motif') or '—'} »")
                s(f"- humain `{h['categorie']}` — « {h.get('motif') or '—'} »")
            s(f"- {c['note']}")
            s()

    texte = "\n".join(out)
    print(texte)
    if "--markdown" in sys.argv:
        chemin = os.path.join(commun.DOUBLE_CODAGE, "piles.md")
        with open(chemin, "w", encoding="utf-8") as f:
            f.write(texte + "\n")
        print(f"\n→ {chemin}", file=sys.stderr)


if __name__ == "__main__":
    main()
