#!/usr/bin/env python3
"""Saisie à la main des 300 colonnes de la référence humaine, une par une.

**Pourquoi cet outil existe.** Le § 4 demande à un humain de coder 300 colonnes
en aveugle. Éditer un JSONL de 300 lignes à la main invite trois erreurs qui
ruinent la mesure sans se voir : une valeur hors taxonomie, un motif posé sur un
`Unflagged`, une ligne décalée. Cet outil pose la question, contraint la réponse,
et **sauvegarde après chaque colonne** — on peut s'arrêter et reprendre.

⚠️ **Il n'affiche jamais l'étiquette machine.** Il ne lit même pas
`annotation/` : il n'a pas de quoi souffler la réponse. C'est la garantie
structurelle du § 4, et elle vaut mieux qu'une promesse de ne pas regarder.

⚠️ **Il ne juge aucune étiquette.** Il vérifie la taxonomie et l'invariant
« motif présent ⇔ ce n'est pas Unflagged », rien d'autre. Un outil qui suggère
une catégorie ne mesure plus l'accord, il le fabrique.

Usage :  python3 outils/coder.py            # reprend où on s'était arrêté
         python3 outils/coder.py --revoir    # repasse aussi les déjà codées
"""
import datetime
import json
import os
import sys

import commun

AIDE = """
  Catégories — tapez le NUMÉRO. L'ordre est celui de l'arbitrage : en cas
  d'hésitation entre deux, la plus HAUTE l'emporte.

   1 CriminalOffenceData        infractions, condamnations
   2 HealthData                 santé, art. 9
   3 SpecialCategoryData        autre art. 9 : opinions, religion, syndicat,
                                origine, vie sexuelle, biométrie
   4 AuthenticationSecret       mot de passe, jeton, clé, condensat de secret
   5 NationalIdentifier         NIR, n° fiscal, n° de pièce d'identité
   6 FinancialData              montant/IBAN rattaché à une personne (§ 3.9)
   7 LocationData               géolocalisation : GPS, trace, borne
   8 ConnectionData             IP, user-agent, session, dernière connexion
   9 Identity                   nom, date de naissance, FK vers une personne
  10 ContactDetails             email, téléphone, adresse postale
  11 ProfessionalLife           poste, employeur, carrière
  12 PersonalDataUncategorised  personnelle, mais aucune valeur ci-dessus ne va
  13 Unflagged                  rien de personnel n'est visible

  Commandes :  ?  cette aide     s  sauter (revenir plus tard)
               q  quitter (tout ce qui est saisi est déjà enregistré)
"""


def afficher(l, n, total, restant):
    largeur = 72
    print("\n" + "═" * largeur)
    print(f"  {n}/{total}   ({restant} restante(s))")
    print("─" * largeur)
    print(f"  schéma   {l['schema_source']}")
    print(f"  table    {l['table']}")
    print(f"  COLONNE  {l['colonne']}")
    print(f"  type     {l['type']}"
          f"{'  · NULL autorisé' if l.get('nullable') else ''}"
          f"  · position {l.get('position')}")
    if l.get("table_referencee"):
        print(f"  FK vers  {l['table_referencee']}")
    if l.get("commentaire_colonne"):
        print(f"  commentaire colonne : {l['commentaire_colonne']}")
    if l.get("commentaire_table"):
        print(f"  commentaire table   : {l['commentaire_table'][:200]}")
    print("═" * largeur)


def demander_categorie():
    """Rend une catégorie, 'saut' ou 'fin'. Ne rend jamais une valeur invalide."""
    while True:
        r = input("  catégorie [1-13, ? aide, s sauter, q quitter] > ").strip()
        if r in ("q", "Q"):
            return "fin"
        if r in ("s", "S"):
            return "saut"
        if r == "?":
            print(AIDE)
            continue
        if r.isdigit() and 1 <= int(r) <= len(commun.TAXONOMIE):
            return commun.TAXONOMIE[int(r) - 1]
        print("  ↳ tapez un nombre entre 1 et 13, ou ? pour l'aide.")


def demander_motif(categorie):
    """L'invariant du § 2 est appliqué ici, pas laissé à la vigilance."""
    if categorie == commun.NON_SIGNALEE:
        return None
    while True:
        m = input("  motif (une phrase, obligatoire) > ").strip()
        if m:
            return m
        print("  ↳ une étiquette autre qu'Unflagged doit dire pourquoi : "
              "c'est le motif qui la rend contestable.")


def main():
    commun.verifier_taxonomie()
    revoir = "--revoir" in sys.argv

    if not os.path.exists(commun.REFERENCE_HUMAINE):
        sys.exit(f"absent : {commun.REFERENCE_HUMAINE} — lancer "
                 "outils/tirer-double-codage.py d'abord")
    lignes = commun.lire_jsonl(commun.REFERENCE_HUMAINE)

    # ⚠️ Si le fichier portait `strate` ou `proba_inclusion`, il souffle une part
    # de la réponse : la strate est le verdict d'un pré-criblage lexical.
    fuites = sorted({c for l in lignes for c in ("strate", "proba_inclusion")
                     if c in l})
    if fuites:
        sys.exit(f"✗ le cahier expose {', '.join(fuites)} — l'aveuglement du § 4 "
                 "n'est plus garanti. Retirez ces champs avant de coder.")

    total = len(lignes)
    aujourdhui = datetime.date.today().isoformat()
    print(__doc__.split("Usage")[0].strip())
    print(f"\n{total} colonnes. Codées : "
          f"{sum(1 for l in lignes if l.get('categorie'))}.")
    print("Tapez ? à la première question pour la liste des catégories.")

    for i, l in enumerate(lignes, 1):
        if l.get("categorie") and not revoir:
            continue
        restant = sum(1 for x in lignes if not x.get("categorie"))
        afficher(l, i, total, restant)
        cat = demander_categorie()
        if cat == "fin":
            break
        if cat == "saut":
            continue
        motif = demander_motif(cat)
        l["categorie"] = cat
        l["motif"] = motif
        l["annotateur"] = "humain"
        l["date"] = aujourdhui
        # Sauvegarde à chaque colonne : une session interrompue ne perd rien.
        commun.ecrire_jsonl(commun.REFERENCE_HUMAINE, lignes)

    codees = sum(1 for l in lignes if l.get("categorie"))
    print(f"\n{codees}/{total} colonnes codées. "
          f"Enregistré dans {commun.REFERENCE_HUMAINE}")
    if codees < total:
        print("Relancez la même commande pour reprendre où vous en êtes.")
    else:
        print("\nPublier l'accord AVANT tout chiffre du moteur :")
        print("  python3 outils/accord.py --markdown")
        print("  python3 outils/distribution.py --markdown")


if __name__ == "__main__":
    try:
        main()
    except (KeyboardInterrupt, EOFError):
        print("\n\nInterrompu. Tout ce qui était saisi est enregistré.")
