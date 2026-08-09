#!/usr/bin/env python3
"""Construit le plan de sondage du corpus de schémas et les fichiers d'annotation.

Deux régimes, et le choix entre eux est celui du plan de sondage :

* **Recensement** — le schéma est annoté en entier. Réservé aux schémas assez
  petits pour l'être, et à ceux dont l'échantillonnage détruirait la variable
  qu'ils portent : les trois états de Paheko ne valent que comparés à schéma
  constant, et un sondage indépendant dans chacun confondrait « la langue a
  changé » avec « le tirage a changé ».
* **Sondage stratifié en grappes** — la grappe est la **table**, jamais la
  colonne isolée. Motif : le pivot porte `table` et `commentaire_table`
  précisément parce qu'un commentaire de table éclaire toutes ses colonnes
  (#128) ; tirer des colonnes isolées priverait l'annotateur humain du contexte
  que le moteur, lui, aura.

⚠️ Les strates viennent d'un **pré-criblage lexical grossier**, qui n'est PAS une
annotation et n'entre jamais au corpus : il ne sert qu'à sur-échantillonner là où
les catégories rares ont une chance de se trouver. La probabilité d'inclusion de
chaque grappe est enregistrée, sans quoi aucune prévalence ne serait estimable
depuis un tirage biaisé exprès.
"""
import json, os, random, re, sys, collections, glob

RACINE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PIVOTS = os.path.join(RACINE, "pivots")
SORTIE = os.path.join(RACINE, "annotation")

SEED = 20260808  # figé : le tirage doit se rejouer à l'identique

RECENSEMENT = ["galette", "paheko-0.8.0", "paheko-1.0.0", "paheko-head",
               "sacoche", "temoin"]
SONDAGE = {"dolibarr": 600, "glpi": 600, "openemr": 600}

# Pré-criblage lexical — large exprès : il vaut mieux qu'une strate « A » soit
# impure que rater la seule table qui portait de l'article 9.
LEXIQUE = re.compile(
    r"(health|sante|santé|medic|médic|diagnos|patholog|allerg|handicap|invalid"
    r"|maladie|patient|prescri|vaccin|symptom|religio|confession|ethni|racial"
    r"|origine|syndic|politiq|opinion|sexual|biometr|biométr|genetic|génétiq"
    r"|crimin|offen[cs]e|infraction|condamn|casier|judicia"
    r"|nir|insee|siret|siren|national|passport|passeport|permis|licen[cs]e"
    r"|passw|passe|pwd|hash|salt|token|secret|api_key|otp|2fa|mdp"
    r"|nom|name|prenom|prénom|surname|birth|naiss|adr|addr|mail|courriel"
    r"|tel|phone|mobile|iban|bic|bank|banque|carte|card|salaire|salary"
    r"|user|utilisateur|client|member|adherent|adhérent|eleve|élève|contact"
    r"|person|individu|employe|employé|civil|genre|gender|sexe)",
    re.IGNORECASE)


def lire_pivot(nom):
    entete, colonnes, pied = None, [], None
    with open(os.path.join(PIVOTS, f"{nom}.jsonl"), encoding="utf-8") as f:
        for ligne in f:
            ligne = ligne.strip()
            if not ligne:
                continue
            o = json.loads(ligne)
            if "format" in o:
                entete = o
            elif "colonne" not in o and "colonnes" in o:
                pied = o
            else:
                colonnes.append(o)
    if pied["colonnes"] != len(colonnes):
        sys.exit(f"{nom} : pied {pied['colonnes']} != {len(colonnes)} lignes lues")
    return entete, colonnes


def grappes(colonnes):
    par_table = collections.OrderedDict()
    for c in colonnes:
        par_table.setdefault((c["schema"], c["table"]), []).append(c)
    return par_table


def strate(cols_de_la_table):
    """A : la table porte au moins un indice lexical. B : aucune."""
    for c in cols_de_la_table:
        texte = f"{c['table']} {c['colonne']} {c['commentaire_colonne']} {c['commentaire_table']}"
        if LEXIQUE.search(texte):
            return "A"
    return "B"


def ligne_annotation(c, nom, st, proba):
    return {
        "id": f"{nom}:{c['table']}:{c['colonne']}",
        "schema_source": nom,
        "table": c["table"],
        "colonne": c["colonne"],
        "position": c["position"],
        "type": c["type"],
        "nullable": c["nullable"],
        "commentaire_colonne": c["commentaire_colonne"],
        "commentaire_table": c["commentaire_table"],
        "table_referencee": c["table_referencee"],
        "strate": st,
        "proba_inclusion": proba,
        # --- à remplir par l'annotateur humain ---
        "categorie": None,
        "motif": None,
        "annotateur": None,
        "date": None,
    }


def garde_annotation():
    """Refuse d'écraser un travail d'annotation déjà commencé.

    Ce script réécrit `annotation/` de bout en bout, et `annotation/` porte les
    étiquettes — plusieurs séances de travail humain que rien ne régénère. La
    consigne « ne jamais relancer une fois l'annotation commencée » existait déjà
    en avertissement ; elle est ici mécanique, parce qu'un avertissement ne
    rattrape pas une commande relancée de mémoire six semaines plus tard.
    """
    faites = []
    for chemin in sorted(glob.glob(os.path.join(SORTIE, "*.annotation.jsonl"))):
        with open(chemin, encoding="utf-8") as f:
            for ligne in f:
                ligne = ligne.strip()
                if ligne and json.loads(ligne).get("categorie") is not None:
                    faites.append(os.path.basename(chemin))
                    break
    if faites:
        sys.exit(
            "REFUS : l'annotation a commencé dans " + ", ".join(faites) + ".\n"
            "Ce script réécrit annotation/ et détruirait ces étiquettes.\n"
            "Si le corpus doit vraiment être re-tiré, sauvegarder annotation/ "
            "hors du dépôt d'abord — et savoir que le double codage et les "
            "chiffres d'accord sont à refaire avec.")


def main():
    garde_annotation()
    os.makedirs(SORTIE, exist_ok=True)
    plan = {"seed": SEED, "schemas": {}}
    total = 0

    for nom in RECENSEMENT + list(SONDAGE):
        entete, colonnes = lire_pivot(nom)
        tables = grappes(colonnes)
        strates = {t: strate(cs) for t, cs in tables.items()}
        retenues, probas = [], {}

        if nom in RECENSEMENT:
            retenues = list(tables)
            probas = {t: 1.0 for t in retenues}
            regime = "recensement"
        else:
            regime = "sondage stratifié en grappes (table)"
            rng = random.Random(f"{SEED}:{nom}")
            cible = SONDAGE[nom]
            a = sorted([t for t in tables if strates[t] == "A"])
            b = sorted([t for t in tables if strates[t] == "B"])
            rng.shuffle(a); rng.shuffle(b)
            # 70 % de l'effort sur la strate A, 30 % sur B : B reste
            # indispensable, c'est là que vivent les vrais négatifs.
            pris_a = prendre(a, tables, int(cible * 0.7))
            pris_b = prendre(b, tables, cible - sum(len(tables[t]) for t in pris_a))
            retenues = pris_a + pris_b
            for grp, pris in (("A", pris_a), ("B", pris_b)):
                source = a if grp == "A" else b
                p = (len(pris) / len(source)) if source else 0.0
                for t in pris:
                    probas[t] = round(p, 6)

        lignes = []
        for t in retenues:
            for c in tables[t]:
                lignes.append(ligne_annotation(c, nom, strates[t], probas[t]))

        chemin = os.path.join(SORTIE, f"{nom}.annotation.jsonl")
        with open(chemin, "w", encoding="utf-8") as f:
            for l in lignes:
                f.write(json.dumps(l, ensure_ascii=False) + "\n")

        plan["schemas"][nom] = {
            "regime": regime,
            "dialecte": entete["dialecte"],
            "tables_total": len(tables),
            "colonnes_total": len(colonnes),
            "tables_retenues": len(retenues),
            "colonnes_a_annoter": len(lignes),
            "tables_strate_A": sum(1 for s in strates.values() if s == "A"),
            "tables_strate_B": sum(1 for s in strates.values() if s == "B"),
        }
        total += len(lignes)
        print(f"{nom:<14} {regime:<34} "
              f"{len(retenues):>4}/{len(tables):<4} tables  "
              f"{len(lignes):>5}/{len(colonnes):<5} colonnes")

    plan["colonnes_a_annoter_total"] = total
    with open(os.path.join(RACINE, "plan-de-sondage.json"), "w", encoding="utf-8") as f:
        json.dump(plan, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"\nTOTAL à annoter : {total} colonnes")


def prendre(candidates, tables, budget):
    """Prend des grappes entières jusqu'à approcher le budget en colonnes."""
    pris, n = [], 0
    for t in candidates:
        if n >= budget:
            break
        pris.append(t)
        n += len(tables[t])
    return pris


if __name__ == "__main__":
    main()
