#!/usr/bin/env python3
"""Vérifie mécaniquement ce que le protocole d'annotation impose, et rien de plus.

Cet outil ne juge **aucune** étiquette — il ne sait pas si `adr_l1` est
`ContactDetails`, et c'est très bien ainsi : la vérité terrain est humaine.
Il vérifie les règles que le protocole a rendues **testables**, à commencer par
celle du § 2 : *motif présent ⇔ ce n'est pas `Unflagged`*.

Cette règle-là mérite une machine. `Unflagged` sans motif et
`PersonalDataUncategorised` avec motif sont à un caractère l'un de l'autre dans
un fichier JSONL, et les confondre est exactement ce qui « corrompt le banc »
selon #127. Une relecture humaine sur 3 254 lignes ne l'attrapera pas ; un test
si.

Usage :  python3 outils/valider.py [--strict]
         --strict exige en plus que TOUTES les lignes soient annotées.
"""
import sys

import commun


def valider(lignes):
    """Rend la liste des anomalies, chacune préfixée de l'id de la colonne."""
    anomalies = []

    def ko(l, message):
        anomalies.append(f"{l['id']} : {message}")

    vus = set()
    for l in lignes:
        if l["id"] in vus:
            ko(l, "id en double dans le corpus d'annotation")
        vus.add(l["id"])

        cat, motif = l.get("categorie"), l.get("motif")
        motif_rempli = bool(motif and motif.strip())

        if cat is None:
            # Ligne non annotée : elle doit être vierge de bout en bout. Une
            # ligne à moitié remplie est une annotation interrompue qu'on
            # prendrait plus tard pour une annotation faite.
            if motif_rempli:
                ko(l, "motif rempli mais categorie absente — annotation interrompue")
            if l.get("annotateur") or l.get("date"):
                ko(l, "annotateur/date remplis mais categorie absente")
            continue

        if cat not in commun.RANG:
            ko(l, f"categorie hors taxonomie : {cat!r}")
            continue

        # Le cœur : la règle mécanique du § 2 du protocole.
        if cat == commun.NON_SIGNALEE and motif_rempli:
            ko(l, "Unflagged porte un motif — soit la colonne est signalée, "
                  "soit le motif est de trop")
        if cat != commun.NON_SIGNALEE and not motif_rempli:
            ko(l, f"{cat} sans motif — une étiquette sans motif n'est pas "
                  "contestable, et le double codage ne mesure alors rien")

        if not l.get("annotateur"):
            ko(l, "annotateur absent")
        if not l.get("date"):
            ko(l, "date absente")

    return anomalies


def main():
    commun.verifier_taxonomie()
    strict = "--strict" in sys.argv

    lignes = commun.lire_annotation()
    plan = commun.lire_plan()
    attendu = plan["colonnes_a_annoter_total"]
    if len(lignes) != attendu:
        sys.exit(f"corpus incohérent : {len(lignes)} lignes lues, "
                 f"{attendu} annoncées par plan-de-sondage.json")

    faites = commun.annotees(lignes)
    anomalies = valider(lignes)

    print(f"corpus      : {len(lignes)} colonnes")
    print(f"annotées    : {len(faites)} ({commun.pourcent(len(faites), len(lignes))})")
    for nom in commun.SCHEMAS:
        du_schema = [l for l in lignes if l["schema_source"] == nom]
        n = len(commun.annotees(du_schema))
        print(f"  {nom:<14} {n:>4}/{len(du_schema):<4} "
              f"{commun.pourcent(n, len(du_schema))}")

    if anomalies:
        print(f"\n{len(anomalies)} anomalie(s) :")
        for a in anomalies[:50]:
            print(f"  ✗ {a}")
        if len(anomalies) > 50:
            print(f"  … et {len(anomalies) - 50} autres")
        sys.exit(1)

    print("\n✓ aucune anomalie")
    if strict and len(faites) != len(lignes):
        sys.exit(f"✗ --strict : {len(lignes) - len(faites)} colonnes non annotées")


if __name__ == "__main__":
    main()
