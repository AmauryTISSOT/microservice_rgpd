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

⚠️ **Il ne juge aucune étiquette.** Il vérifie la taxonomie, l'invariant
« motif présent ⇔ ce n'est pas Unflagged » et la **forme** du motif — jamais son
bien-fondé. Un outil qui suggère une catégorie ne mesure plus l'accord, il le
fabrique.

## Les quatre durcissements du 2026-08-09 (#145, #149)

La tentative 1 a produit κ = 0,040 avec, sur son cahier, **quatre défauts
constatés** — pas supposés. Chacun a ici son remède :

1. ⚠️ **Le motif doit citer quelque chose de vérifiable** — une sous-chaîne du
   nom de colonne, du nom de table, du type, ou un `§` du protocole. C'est le
   seul remède non négociable : 16 motifs sur 47 disaient « Nom de la colonne »
   et 11 « nom de la table », or **c'est le motif qui rend une étiquette
   contestable**, donc un désaccord arbitrable. Sans lui, la matrice ne peut pas
   dire qui a tort — c'est très exactement ce qui est arrivé.
2. **La catégorie se saisit par son nom tapé, jamais par un numéro d'ordre.**
   `ConnectionData` et `Identity` étaient aux rangs 8 et 9, `LocationData` et
   `ContactDetails` aux rangs 7 et 10 ; plusieurs colonnes portaient une
   catégorie que leur propre motif contredisait — une signature de faute de
   frappe, pas de jugement.
3. **`valider.py` passe avant la clôture.** Un motif disant « erreur ici$ » est
   allé jusqu'au cahier publié.
4. **Le § applicable s'affiche sur les clés étrangères**, première source
   numérique de désaccord, que le § 3.2 tranchait pourtant déjà.

⚠️ **Ce que ces durcissements ne font toujours pas : suggérer une catégorie.**
Le § 3.10, tout neuf, aurait pu recevoir le même rappel que le § 3.2 au
remède 4 — il ne l'a pas, et c'est délibéré : signaler « le nom de cette colonne
est opaque » revient à souffler qu'il faut hériter du domaine de la table.
Rappeler le § 3.2 sur une clé étrangère nomme une **propriété du pivot** ;
rappeler le § 3.10 nommerait un **début de réponse**.

Usage :  python3 outils/coder.py            # reprend où on s'était arrêté
         python3 outils/coder.py --revoir    # repasse aussi les déjà codées
"""
import datetime
import json
import os
import re
import sys

import commun
import valider

# ⚠️ Aucun numéro d'ordre dans cette aide : c'est le remède n° 2. Les afficher
# « pour la commodité » rétablirait la saisie par rang, donc l'adjacence de menu
# qui a produit ConnectionData(8) pour Identity(9) et LocationData(7) pour
# ContactDetails(10). L'ordre reste celui de l'arbitrage, il ne se tape pas.
AIDE = """
  Catégories — tapez le NOM (ou un début de nom sans ambiguïté).
  L'ordre ci-dessous est celui de l'arbitrage : en cas d'hésitation entre
  deux, la plus HAUTE l'emporte. Il ne se saisit pas.

  CriminalOffenceData        infractions, condamnations
  HealthData                 santé, art. 9
  SpecialCategoryData        autre art. 9 : opinions, religion, syndicat,
                             origine, vie sexuelle, biométrie
  AuthenticationSecret       mot de passe, jeton, clé, condensat de secret
  NationalIdentifier         NIR, n° fiscal, n° de pièce d'identité
  FinancialData              montant/IBAN rattaché à une personne (§ 3.9)
  LocationData               géolocalisation : GPS, trace, borne
  ConnectionData             IP, user-agent, session, dernière connexion
  Identity                   nom, date de naissance, FK vers une personne
  ContactDetails             email, téléphone, adresse postale
  ProfessionalLife           poste, employeur, carrière
  PersonalDataUncategorised  personnelle, mais aucune valeur ci-dessus ne va
  Unflagged                  rien de personnel n'est visible

  Commandes :  ?  cette aide     s  sauter (revenir plus tard)
               q  quitter (tout ce qui est saisi est déjà enregistré)
"""

# Remède n° 4 : le § 3.2 est la première source numérique de désaccord, et il
# tranchait déjà. On le rappelle quand le pivot dit qu'on est dessus.
FK_NOM = re.compile(r"^(fk_|.*_id$|.*s_id$|id_)", re.I)

RAPPEL_FK = """  ┌─ § 3.2 — cette colonne a la forme d'une clé étrangère
  │  Elle s'annote sur ce qu'elle DÉSIGNE, pas sur ce qu'elle stocke.
  │  · vers une table de personnes (user, adherent, eleve, patient,
  │    societe, contact) → Identity
  │  · vers une table de nomenclature (c_pays, type_contact) → Unflagged
  └─ table_referencee est vide chez GLPI, OpenEMR et SACoche : on se rabat
     sur le nom, comme le moteur devra le faire."""


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
    # Remède n° 4 — le rappel porte sur une propriété du pivot, jamais sur une
    # catégorie : il dit « vous êtes sur une clé étrangère », pas laquelle.
    if l.get("table_referencee") or FK_NOM.match(l["colonne"]):
        print(RAPPEL_FK)


def demander_categorie():
    """Rend une catégorie, 'saut' ou 'fin'. Ne rend jamais une valeur invalide.

    Remède n° 2 : **saisie par nom**. Un numéro est refusé explicitement plutôt
    qu'ignoré — refuser en silence inviterait à retenter, et c'est le retour du
    rang qu'on veut fermer.
    """
    while True:
        r = input("  catégorie [nom, ? aide, s sauter, q quitter] > ").strip()
        if r in ("q", "Q"):
            return "fin"
        if r in ("s", "S"):
            return "saut"
        if r == "?":
            print(AIDE)
            continue
        if not r:
            continue
        if r.isdigit():
            print("  ↳ la saisie par numéro est fermée depuis le 2026-08-09 : "
                  "les rangs voisins du menu ont produit des étiquettes que "
                  "leur propre motif contredisait. Tapez le NOM.")
            continue
        correspond = [v for v in commun.TAXONOMIE if v.lower() == r.lower()]
        if not correspond:
            correspond = [v for v in commun.TAXONOMIE
                          if v.lower().startswith(r.lower())]
        if len(correspond) == 1:
            return correspond[0]
        if len(correspond) > 1:
            print(f"  ↳ « {r} » est ambigu : {', '.join(correspond)}. Précisez.")
        else:
            print(f"  ↳ « {r} » n'est aucune des treize catégories. "
                  "? pour la liste.")


def fragments(ligne):
    """Ce qu'un motif peut « citer » : les mots du pivot, pas ceux du français.

    Les noms se découpent sur `_`, sur les bascules de casse et sur les
    chiffres, parce que c'est ainsi qu'ils portent leur sens : `subscriber_mname`
    rend `subscriber` et `mname`, `ODSPH` rend `ODSPH`. Deux caractères ne font
    pas une citation — le seuil est à trois.
    """
    mots = set()
    for champ in ("colonne", "table", "type", "table_referencee"):
        brut = str(ligne.get(champ) or "")
        for m in re.split(r"[^A-Za-z0-9]+", brut):
            for part in re.findall(r"[A-Z]+(?![a-z])|[A-Z][a-z]+|[a-z]+|\d+", m):
                if len(part) >= 3:
                    mots.add(part.lower())
            if len(m) >= 3:
                mots.add(m.lower())
    return mots


def motif_recevable(motif, ligne):
    """Remède n° 1 — le seul non négociable. Rend None si le motif passe.

    ⚠️ **Vérifie la forme, jamais le bien-fondé.** Un motif qui cite le pivot
    peut être faux ; c'est justement ce qui le rend **contestable**, donc le
    désaccord arbitrable. Un motif qui ne cite rien — « nom de la colonne »,
    « erreur ici$ » — n'est pas contestable, et la tentative 1 a mesuré ce que
    ça coûte : la matrice des désaccords y est devenue inexploitable.
    """
    bas = motif.lower()
    if re.search(r"§\s*\d", motif):
        return None
    if any(f in bas for f in fragments(ligne)):
        return None
    return ("ce motif ne cite ni le nom de la colonne, ni celui de la table, ni "
            "le type, ni un §. Il ne rend donc l'étiquette contestable par "
            "personne — c'est le défaut qui a rendu la tentative 1 "
            "inexploitable. Citez ce que vous avez lu dans le pivot, ou le § "
            "que vous appliquez.")


def demander_motif(categorie, ligne):
    """L'invariant du § 2 est appliqué ici, pas laissé à la vigilance."""
    if categorie == commun.NON_SIGNALEE:
        return None
    while True:
        m = input("  motif (une phrase, obligatoire) > ").strip()
        if not m:
            print("  ↳ une étiquette autre qu'Unflagged doit dire pourquoi : "
                  "c'est le motif qui la rend contestable.")
            continue
        refus = motif_recevable(m, ligne)
        if refus is None:
            return m
        print(f"  ↳ {refus}")


def main():
    commun.verifier_taxonomie()
    revoir = "--revoir" in sys.argv

    cahier = commun.reference_humaine()
    if not os.path.exists(cahier):
        sys.exit(f"absent : {cahier} — lancer "
                 "outils/tirer-double-codage.py d'abord")
    lignes = commun.lire_jsonl(cahier)

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
        motif = demander_motif(cat, l)
        l["categorie"] = cat
        l["motif"] = motif
        l["annotateur"] = "humain"
        l["date"] = aujourdhui
        # Sauvegarde à chaque colonne : une session interrompue ne perd rien.
        commun.ecrire_jsonl(cahier, lignes)

    codees = sum(1 for l in lignes if l.get("categorie"))
    print(f"\n{codees}/{total} colonnes codées. Enregistré dans {cahier}")
    if codees < total:
        print("Relancez la même commande pour reprendre où vous en êtes.")
        return

    # Remède n° 3 — la clôture passe par le validateur. « erreur ici$ » est allé
    # jusqu'au cahier PUBLIÉ de la tentative 1 ; la porte se ferme ici, pas dans
    # la vigilance de quelqu'un qui vient de coder 300 colonnes.
    anomalies = valider.valider(lignes)
    if anomalies:
        print(f"\n✗ {len(anomalies)} anomalie(s) — le cahier n'est PAS clos :")
        for a in anomalies[:20]:
            print(f"  ✗ {a}")
        if len(anomalies) > 20:
            print(f"  … et {len(anomalies) - 20} autres")
        print("\nReprenez-les avec --revoir. Publier un cahier qui porte ces "
              "anomalies, c'est republier le défaut de la tentative 1.")
        sys.exit(1)

    print("\n✓ validation passée — le cahier est clos.")
    print("\nPublier l'accord AVANT tout chiffre du moteur :")
    print("  python3 outils/accord.py --markdown")
    print("  python3 outils/distribution.py --markdown")


if __name__ == "__main__":
    try:
        main()
    except (KeyboardInterrupt, EOFError):
        print("\n\nInterrompu. Tout ce qui était saisi est enregistré.")
