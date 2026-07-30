"""La machinerie du banc d'essai, extraite du premier notebook pour que le second la partage.

Pourquoi un module et non une seconde copie des cellules. Le banc de la seconde itération
([Éprouver les leviers de réglage de setfit contre le lexique](https://github.com/AmauryTISSOT/microservice_rgpd/issues/56))
doit produire des chiffres **comparables** à ceux de la première : mêmes plis, même tête, même
règle d'arbitrage, même intervalle. Recopier trois cents lignes dans un second notebook aurait
rendu cette identité invérifiable — la moindre dérive d'une ligne aurait déplacé les chiffres sans
que rien ne le signale.

Le premier notebook n'est pas modifié pour autant : il est livré, ses sorties portent le verdict de
[Lire les chiffres du notebook et prononcer le verdict](https://github.com/AmauryTISSOT/microservice_rgpd/issues/52),
et le rejouer pour un refactoring serait payer six heures pour ne rien apprendre. L'équivalence est
donc **prouvée par la donnée versionnée** et non par la relecture : `verifier_equivalence()`
re-dérive les plis et rejoue la règle d'arbitrage sur les 1800 prédictions de
`temoin-predictions.jsonl`, et lève si un seul pli ou une seule décision diffère. C'est la
discipline « recalculé, jamais recopié » que le premier notebook s'appliquait déjà à son point de
comparaison, retournée vers son propre code.

Tout ce qui suit est donc une **extraction fidèle**, à deux généralisations près, toutes deux
inertes par défaut :

- `decider()` accepte un seuil **par étiquette** ; sans argument, il applique `SEUIL` partout et
  se comporte exactement comme la version d'origine.
- `regler_puis_ajuster()` accepte une grille de tête restreinte et un réglage de seuil imbriqué ;
  sans argument, il règle la même grille au même seuil que la version d'origine.
"""

from __future__ import annotations

import json
import math
import time
from collections import Counter
from pathlib import Path

import numpy as np
from sklearn.linear_model import LogisticRegression
from sklearn.multiclass import OneVsRestClassifier

# ─────────────────────────────────────────────────────────────────────────────────────────────
# Constantes — identiques au premier notebook, au caractère près.
# ─────────────────────────────────────────────────────────────────────────────────────────────


def racine_du_depot(depart: Path | None = None) -> Path:
    """Remonte jusqu'au dépôt, repéré par sa solution — jamais un chemin relatif au répertoire
    courant. Le notebook est lancé tantôt depuis `exploration/`, tantôt depuis la racine."""
    depart = (depart or Path.cwd()).resolve()
    for dossier in (depart, *depart.parents):
        if (dossier / "MicroserviceRgpd.slnx").is_file():
            return dossier
    raise RuntimeError(f"Racine du dépôt introuvable en remontant depuis {depart}")


RACINE = racine_du_depot(Path(__file__).parent)

#: Les germes. R = 5, et le critère s'applique au **pire** d'entre eux, pas à la médiane.
GERMES = [20180525, 20190523, 20200101, 20210704, 20221123]

K_EXTERNE = 5          # les plis dont sortent les prédictions hors-pli
K_INTERNE = 5          # les plis imbriqués où se règle la tête
SEUIL = 0.5            # le seuil de chaque tête, non réglé — voir la règle d'arbitrage

ENCODEUR = "intfloat/multilingual-e5-small"
#: Révision épinglée : sans elle, la reproductibilité annoncée serait creuse.
REVISION = "614241f622f53c4eeff9890bdc4f31cfecc418b3"
#: Les modèles `e5` exigent ce préfixe. L'omettre dégrade les vecteurs *sans aucun signal*.
PREFIXE_E5 = "query: "

#: Les six configurations de la tête, réglées **dans** les plis. `lbfgs` n'a aucun tirage : le
#: seul aléa des montages 1 et 2 est donc le découpage, et rien d'autre.
GRILLE_TETE = [{"C": c, "class_weight": w} for c in (0.1, 1.0, 10.0) for w in (None, "balanced")]

#: Les paramètres SetFit du premier banc : ceux **publiés par défaut**, délibérément non réglés.
SETFIT_ITERATIONS = 20
SETFIT_LOT = 16
SETFIT_EPOQUES = 1
#: Le défaut de la bibliothèque `setfit`, jamais choisi — c'est le levier que #56 éprouve.
SETFIT_LR_CORPS = 2e-5

SLUGS = ["acces", "rectification", "effacement", "limitation", "portabilite", "opposition",
         "hors-perimetre"]
HORS_PERIMETRE = "hors-perimetre"
INDICE_HP = SLUGS.index(HORS_PERIMETRE)

#: La table de correspondance du sidecar, **recopiée** : le projet d'exploration ne déclare
#: aucune dépendance sur le sidecar, pas même en `path` (#49). Il ne lit que des fichiers plats.
CANONIQUE_PAR_SLUG = {
    "acces": "Access", "rectification": "Rectification", "effacement": "Erasure",
    "limitation": "Restriction", "portabilite": "Portability", "opposition": "Objection",
    "hors-perimetre": "OutOfScope",
}
SLUG_PAR_CANONIQUE = {v: k for k, v in CANONIQUE_PAR_SLUG.items()}

#: Les cinq erreurs de `qwen3:8b` que le montage actuel signale déjà.
ERREURS_ATTRAPEES_PAR_A = {"acc-10", "hop-15", "hop-22", "lim-08", "por-08"}
#: Les deux erreurs que la règle de consensus ne peut **structurellement** pas attraper.
ERREURS_INATTEIGNABLES = {"edg-04", "hop-14"}
#: Le taux de fausse alarme du montage actuel.
TAUX_A = 24 / 113

#: Les huit couples quasi identiques documentés par `corpus/README.md`.
PAIRES_MINIMALES = [
    ("edg-13", "edg-14"),   # présence ou non d'une valeur de remplacement
    ("edg-04", "edg-05"),   # question *sur* un droit ou exercice du droit
    ("eff-02", "hop-03"),   # supprimer un compte ou résilier un abonnement
    ("eff-03", "hop-24"),   # même impératif familier, objet « données » ou objet « contrat »
    ("opp-02", "mul-01"),   # l'enregistrement est préservé, ou il est aussi visé
    ("edg-11", "edg-12"),   # négation portant sur l'effacement : opposition ou limitation
    ("acc-01", "hop-22"),   # demande d'accès RGPD ou législation sectorielle
    ("eff-05", "opp-01"),   # retrait de consentement ou opposition à une finalité
]

CHEMIN_PREDICTIONS = RACINE / "exploration" / "temoin-predictions.jsonl"


# ─────────────────────────────────────────────────────────────────────────────────────────────
# Les données
# ─────────────────────────────────────────────────────────────────────────────────────────────


def charger_corpus(racine: Path = RACINE) -> list[dict]:
    with (racine / "corpus" / "demandes-rgpd.fr.jsonl").open(encoding="utf-8") as f:
        return [json.loads(ligne) for ligne in f]


def charger_avis(chemin: Path) -> dict[str, set[str]]:
    """Rend, par identifiant, l'ensemble des droits en slugs français.

    Une panne reste une panne : le sidecar traite un avis invalide comme une panne du moteur et
    jamais comme un avis faible, et l'artefact garde cette distinction.
    """
    avis = {}
    with chemin.open(encoding="utf-8") as f:
        for ligne in f:
            ligne = json.loads(ligne)
            if ligne.get("status", 200) != 200:
                raise ValueError(f"Avis en panne dans {chemin.name} : {ligne['id']}")
            avis[ligne["id"]] = {SLUG_PAR_CANONIQUE[nom] for nom in ligne["rights"]}
    return avis


corpus = charger_corpus()
identifiants = [ligne["id"] for ligne in corpus]
textes = [ligne["texte"] for ligne in corpus]
verite = [set(ligne["droits"]) for ligne in corpus]
Y = np.array([[1 if s in ligne["droits"] else 0 for s in SLUGS] for ligne in corpus], dtype=int)

L = [charger_avis(RACINE / "src/sidecar/tests/witness/lexicon-corpus.jsonl")[i]
     for i in identifiants]
V = [charger_avis(RACINE / "exploration/qwen3-8b-corpus.jsonl")[i] for i in identifiants]

N_EXEMPLES = len(corpus)
assert N_EXEMPLES == 120 and len(L) == 120 and len(V) == 120

INDICE_PAR_ID = {ident: i for i, ident in enumerate(identifiants)}


def accord(a, b) -> bool:
    return a == b


#: Les 113 exemples où `qwen3:8b` a raison, et les 7 où il se trompe.
erreurs_llm = [i for i in range(N_EXEMPLES) if not accord(V[i], verite[i])]
corrects_llm = [i for i in range(N_EXEMPLES) if accord(V[i], verite[i])]


# ─────────────────────────────────────────────────────────────────────────────────────────────
# Le découpage — stratification multi-label **et** contrainte de groupes
# ─────────────────────────────────────────────────────────────────────────────────────────────


def construire_groupes(identifiants: list[str]) -> list[list[int]]:
    """Huit groupes de deux pour les paires minimales ; tout le reste est singleton."""
    indice = {ident: i for i, ident in enumerate(identifiants)}
    groupes = [[indice[g], indice[d]] for g, d in PAIRES_MINIMALES]
    apparies = {i for membres in groupes for i in membres}
    groupes.extend([i] for i in range(len(identifiants)) if i not in apparies)
    return groupes


def _meilleur_pli(manque_etiquette, manque_taille, tirage):
    """Le pli qui manque le plus de l'étiquette ; à égalité, celui qui manque le plus d'exemples ;
    à égalité encore, un tirage — départager par l'indice biaiserait vers les premiers plis."""
    candidats = np.flatnonzero(manque_etiquette >= manque_etiquette.max() - 1e-9)
    if len(candidats) > 1:
        second = manque_taille[candidats]
        candidats = candidats[second >= second.max() - 1e-9]
    return int(tirage.choice(candidats)) if len(candidats) > 1 else int(candidats[0])


def decouper(Y, groupes, k, germe):
    """Rend, pour chaque exemple, le numéro du pli où il est en **test**.

    À chaque tour, l'étiquette la plus rare encore à placer commande, et le groupe qui la porte va
    au pli qui en manque le plus. Les rares sont ainsi servies les premières, quand la latitude est
    maximale.
    """
    tirage = np.random.default_rng(germe)
    etiquettes_groupe = np.array([Y[membres].sum(axis=0) for membres in groupes])
    tailles_groupe = np.array([len(membres) for membres in groupes])

    manque = np.tile(etiquettes_groupe.sum(axis=0) / k, (k, 1)).astype(float)
    manque_taille = np.full(k, tailles_groupe.sum() / k, dtype=float)

    restants = list(tirage.permutation(len(groupes)))
    affectation = np.full(len(groupes), -1, dtype=int)

    while restants:
        en_attente = etiquettes_groupe[restants].sum(axis=0)
        positives = [l for l in range(Y.shape[1]) if en_attente[l] > 0]
        if positives:
            rare = min(positives, key=lambda l: (en_attente[l], l))
            candidats = [g for g in restants if etiquettes_groupe[g][rare] > 0]
        else:
            rare, candidats = None, list(restants)

        for groupe in candidats:
            colonne = manque_taille if rare is None else manque[:, rare]
            pli = _meilleur_pli(colonne, manque_taille, tirage)
            affectation[groupe] = pli
            manque[pli] -= etiquettes_groupe[groupe]
            manque_taille[pli] -= tailles_groupe[groupe]
            restants.remove(groupe)

    plis = np.empty(Y.shape[0], dtype=int)
    for groupe, membres in enumerate(groupes):
        plis[membres] = affectation[groupe]
    return plis


GROUPES = construire_groupes(identifiants)
PLIS = {germe: decouper(Y, GROUPES, K_EXTERNE, germe) for germe in GERMES}


def verifier_decoupage(Y, plis, groupes, k):
    tailles = [int((plis == f).sum()) for f in range(k)]
    coupees = [[identifiants[i] for i in m] for m in groupes if len({plis[i] for i in m}) > 1]
    absente_apprentissage, absente_test = {}, {}
    for f in range(k):
        manquantes_a = [SLUGS[l] for l in range(Y.shape[1]) if Y[plis != f].sum(axis=0)[l] == 0]
        manquantes_t = [SLUGS[l] for l in range(Y.shape[1]) if Y[plis == f].sum(axis=0)[l] == 0]
        if manquantes_a:
            absente_apprentissage[f] = manquantes_a
        if manquantes_t:
            absente_test[f] = manquantes_t
    return {
        "tailles": tailles,
        "paires_coupees": coupees,
        "etiquette_absente_en_apprentissage": absente_apprentissage,
        "etiquette_absente_en_test": absente_test,
        "valide": not coupees and not absente_apprentissage and min(tailles) > 0,
    }


# ─────────────────────────────────────────────────────────────────────────────────────────────
# La tête et la règle de décision
# ─────────────────────────────────────────────────────────────────────────────────────────────


def ajuster_tete(X, Y_, configuration):
    tete = OneVsRestClassifier(LogisticRegression(
        solver="lbfgs", max_iter=2000, C=configuration["C"],
        class_weight=configuration["class_weight"]))
    tete.fit(X, Y_)
    return tete


#: Le seuil uniforme du premier banc, sous la forme que `decider()` attend.
SEUILS_UNIFORMES = tuple(SEUIL for _ in SLUGS)


def decider(probabilites, seuils=None):
    """La règle d'arbitrage figée. Rend aussi ce qu'*aurait* été la sortie brute, sans quoi le
    taux de violation d'I2 avant contrainte ne serait pas observable.

    `seuils` est le seuil **par étiquette** ; à `None`, `SEUIL` s'applique partout et la fonction
    est identique à celle du premier notebook.
    """
    seuils = SEUILS_UNIFORMES if seuils is None else seuils
    franchissent = [l for l in range(len(SLUGS)) if probabilites[l] >= seuils[l]]
    droits = [l for l in franchissent if l != INDICE_HP]
    hp_franchit = INDICE_HP in franchissent
    violation = hp_franchit and bool(droits)

    if hp_franchit and droits:
        predit = ([INDICE_HP] if probabilites[INDICE_HP] > max(probabilites[l] for l in droits)
                  else droits)
    elif hp_franchit or not droits:
        predit = [INDICE_HP]
    else:
        predit = droits

    triees = np.sort(probabilites)[::-1]
    return {
        "brut": [SLUGS[l] for l in sorted(franchissent)],
        "predit": [SLUGS[l] for l in sorted(predit)],
        # Score **continu**, jamais ordinal : on peut toujours grossir un continu en trois
        # degrés, jamais l'inverse.
        "marge": float(triees[0] - triees[1]) if franchissent else 0.0,
        "aucun_seuil_franchi": not franchissent,
        "violation_i2_brute": violation,
    }


def macro_f1(reference, predit):
    """Moyenne **non pondérée** sur les sept étiquettes, calculée sur la sortie *contrainte*."""
    scores = []
    for slug in SLUGS:
        vp = sum(1 for r, p in zip(reference, predit) if slug in r and slug in p)
        fp = sum(1 for r, p in zip(reference, predit) if slug not in r and slug in p)
        fn = sum(1 for r, p in zip(reference, predit) if slug in r and slug not in p)
        scores.append(0.0 if vp == 0 else 2 * vp / (2 * vp + fp + fn))
    return float(np.mean(scores))


def wilson(succes, total, z=1.959963984540054):
    """Intervalle binomial de Wilson — l'instrument d'incertitude retenu par #48."""
    if total == 0:
        return (0.0, 1.0)
    p = succes / total
    d = 1 + z ** 2 / total
    centre = (p + z ** 2 / (2 * total)) / d
    demi = z * math.sqrt(p * (1 - p) / total + z ** 2 / (4 * total ** 2)) / d
    return (max(0.0, centre - demi), min(1.0, centre + demi))


# ─────────────────────────────────────────────────────────────────────────────────────────────
# La boucle de mesure
# ─────────────────────────────────────────────────────────────────────────────────────────────

#: La grille de seuils du réglage par étiquette. Grossière **par précaution** et non par
#: paresse : sur 13-14 positifs, un pli interne n'en voit que 2 ou 3, et une grille fine
#: choisirait le bruit (#56). Le pas de 0,05 est le garde-fou.
GRILLE_SEUILS = tuple(round(0.30 + 0.05 * i, 2) for i in range(12))   # 0,30 → 0,85

#: Répétitions du découpage interne quand on règle un seuil — l'équivalent de
#: `RepeatedStratifiedKFold`, second garde-fou demandé par #56. Le réglage de la *tête* n'en
#: profite pas : il reste sur la première répétition, à l'identique du premier banc, pour que la
#: comparaison isole bien le seuil.
REPETITIONS_SEUIL = 2


def _groupes_locaux(indices_apprentissage):
    """Réindexe les groupes dans le repère du seul jeu d'apprentissage."""
    groupe_de = {i: g for g, membres in enumerate(GROUPES) for i in membres}
    local = {int(i): j for j, i in enumerate(indices_apprentissage)}
    par_groupe = {}
    for i in indices_apprentissage:
        par_groupe.setdefault(groupe_de[int(i)], []).append(local[int(i)])
    return list(par_groupe.values())


def _regler_seuils_f1(probabilites, Y_reference):
    """Choisit un seuil par étiquette en maximisant le **F1 de cette étiquette**, séparément.

    Les têtes étant `one-vs-rest`, chaque étiquette a sa propre sigmoïde et son seuil se règle
    indépendamment : c'est exactement la restriction de `TunedThresholdClassifierCV`, qui est
    binaire, appliquée **par tête** et non autour du `OneVsRestClassifier` (#56).

    ⚠️ Cet objectif n'est **pas** celui du critère. Il se lit *avant* la règle d'arbitrage, donc
    séparément par étiquette ; le critère de #56, lui, porte sur l'**accord exact** des sept
    étiquettes après arbitrage. Régler l'un ne règle pas l'autre, et la mesure montre qu'il peut
    le dégrader. Conservé pour que l'écart entre les deux objectifs soit lisible, jamais employé
    comme réglage de référence.
    """
    seuils = []
    for l in range(len(SLUGS)):
        colonne, reference = probabilites[:, l], Y_reference[:, l]
        meilleur, meilleur_score = SEUIL, -1.0
        for seuil in GRILLE_SEUILS:
            predit = colonne >= seuil
            vp = int((predit & (reference == 1)).sum())
            fp = int((predit & (reference == 0)).sum())
            fn = int((~predit & (reference == 1)).sum())
            score = 0.0 if vp == 0 else 2 * vp / (2 * vp + fp + fn)
            # `>` strict, et 0,5 essayé en premier par l'initialisation : à égalité de F1 le
            # seuil non réglé l'emporte, et le réglage ne bouge que s'il gagne vraiment.
            if score > meilleur_score + 1e-12:
                meilleur, meilleur_score = seuil, score
        seuils.append(meilleur)
    return tuple(seuils)


#: Passes de la montée par coordonnées. Deux suffisent : la seconde ne fait plus bouger qu'un
#: seuil sur les jeux mesurés, et une troisième n'achèterait que du sur-ajustement.
PASSES_SEUILS = 2


def _accord_exact_sous_arbitrage(probabilites, references, seuils):
    """Le nombre d'exemples dont les **sept** étiquettes sont exactes après arbitrage."""
    return sum(1 for p, r in zip(probabilites, references)
               if set(decider(p, seuils)["predit"]) == r)


def _regler_seuils_accord(probabilites, Y_reference):
    """Choisit les seuils en maximisant l'**accord exact après arbitrage** — l'objectif du critère.

    Pourquoi ce n'est pas séparable, et pourquoi il faut donc une montée par coordonnées : la
    règle d'arbitrage couple les sept têtes. Le seuil de `hors-perimetre` décide si l'exclusivité
    s'applique, et le seuil d'une étiquette de droit décide si `hors-perimetre` l'emporte sur elle.
    Un réglage étiquette par étiquette optimise donc une quantité que le critère ne lit pas.

    On part de `SEUIL` partout — le réglage du premier banc — et on ne bouge un seuil que s'il
    gagne **strictement** : le montage non réglé garde la main sur toutes les égalités, si bien
    que ce réglage ne peut pas dégrader le jeu sur lequel il est réglé. Qu'il puisse dégrader le
    pli tenu à l'écart est une autre affaire, et c'est précisément ce que la mesure doit dire.
    """
    references = [{SLUGS[l] for l in np.flatnonzero(ligne)} for ligne in Y_reference]
    seuils = list(SEUILS_UNIFORMES)
    meilleur = _accord_exact_sous_arbitrage(probabilites, references, tuple(seuils))
    for _ in range(PASSES_SEUILS):
        bouge = False
        for l in range(len(SLUGS)):
            courant = seuils[l]
            for candidat in GRILLE_SEUILS:
                if candidat == courant:
                    continue
                seuils[l] = candidat
                score = _accord_exact_sous_arbitrage(probabilites, references, tuple(seuils))
                if score > meilleur:
                    meilleur, courant, bouge = score, candidat, True
            seuils[l] = courant
        if not bouge:
            break
    return tuple(seuils)


#: L'objectif de réglage de référence. Le critère de #56 porte sur l'accord exact : régler autre
#: chose serait régler à côté.
def _regler_seuils(probabilites, Y_reference, objectif="accord"):
    if objectif == "accord":
        return _regler_seuils_accord(probabilites, Y_reference)
    if objectif == "f1":
        return _regler_seuils_f1(probabilites, Y_reference)
    raise ValueError(f"Objectif de réglage inconnu : {objectif}")


def regler_puis_ajuster(X, Y_, groupes, germe, grille=None, regler_seuils=False,
                        objectif_seuil="accord"):
    """Choisit la configuration sur des plis **internes**, puis réajuste sur tout l'apprentissage.

    Quand `regler_seuils` est vrai, les seuils par étiquette sont choisis sur ces mêmes plis
    internes — jamais sur le pli externe de test, qui n'entre pas ici. Le réglage reste donc
    imbriqué, au sens où la carte l'exige.
    """
    grille = GRILLE_TETE if grille is None else grille
    repetitions = REPETITIONS_SEUIL if regler_seuils else 1
    decoupages = [decouper(Y_, groupes, K_INTERNE, germe + 1 + r) for r in range(repetitions)]

    meilleure, meilleur_score = None, -1.0
    probabilites_internes = {}
    for configuration in grille:
        collecte = []
        for r, internes in enumerate(decoupages):
            for f in range(K_INTERNE):
                appr, val = internes != f, internes == f
                if Y_[appr].sum(axis=0).min() == 0:
                    continue      # une étiquette absente rend cette configuration inévaluable ici
                tete = ajuster_tete(X[appr], Y_[appr], configuration)
                P = np.asarray(tete.predict_proba(X[val]), dtype=float)
                collecte.append((r, P, Y_[val]))
        # Le score de sélection de la tête ne lit que la **première** répétition et le seuil non
        # réglé : à l'identique du premier banc, pour que le seuil soit la seule chose qui bouge.
        predits, references = [], []
        for r, P, Y_val in collecte:
            if r != 0:
                continue
            predits += [set(decider(p)["predit"]) for p in P]
            references += [{SLUGS[l] for l in np.flatnonzero(ligne)} for ligne in Y_val]
        score = macro_f1(references, predits) if predits else -1.0
        if score > meilleur_score + 1e-12:
            meilleure, meilleur_score = configuration, score
            probabilites_internes = collecte
    if meilleure is None:
        raise RuntimeError("Aucune configuration n'a pu être évaluée : réglage qui ne converge pas.")

    seuils = None
    if regler_seuils:
        P = np.vstack([p for _, p, _ in probabilites_internes])
        R = np.vstack([y for _, _, y in probabilites_internes])
        seuils = _regler_seuils(P, R, objectif_seuil)

    return ajuster_tete(X, Y_, meilleure), meilleure, meilleur_score, seuils


def mesurer(montage, representation, germes=None, journal=True, grille=None,
            regler_seuils=False, objectif_seuil="accord", extra=None):
    """Rend une prédiction **hors-pli** par exemple et par germe.

    `representation(indices_apprentissage, germe, pli)` rend la matrice des 120 exemples. Elle
    reçoit les indices d'apprentissage parce que certaines représentations s'ajustent — le
    vocabulaire TF-IDF, l'encodeur SetFit — et qu'un ajustement sur les 120 serait une fuite.
    """
    germes = GERMES if germes is None else germes
    enregistrements = []
    for germe in germes:
        plis = PLIS[germe]
        for f in range(K_EXTERNE):
            debut = time.perf_counter()
            appr = np.flatnonzero(plis != f)
            test = np.flatnonzero(plis == f)
            X = representation(appr, germe, f)
            tete, configuration, score, seuils = regler_puis_ajuster(
                X[appr], Y[appr], _groupes_locaux(appr), germe,
                grille=grille, regler_seuils=regler_seuils, objectif_seuil=objectif_seuil)
            P = np.asarray(tete.predict_proba(X[test]), dtype=float)
            for rang, i in enumerate(test):
                enregistrements.append({
                    "montage": montage, "germe": germe, "pli": f, "id": identifiants[int(i)],
                    "probabilites": {s: round(float(P[rang][l]), 6) for l, s in enumerate(SLUGS)},
                    "reglage": configuration,
                    "seuils": None if seuils is None else dict(zip(SLUGS, seuils)),
                    **(extra or {}),
                    **decider(P[rang], seuils),
                })
            if journal:
                print(f"  {montage} germe {germe} pli {f} : {configuration} "
                      f"{'seuils ' + str(seuils) + ' ' if seuils else ''}"
                      f"macro-F1 interne {score:.3f} — {time.perf_counter() - debut:.1f} s",
                      flush=True)
    return enregistrements


# ─────────────────────────────────────────────────────────────────────────────────────────────
# L'équivalence avec le premier banc — prouvée sur la donnée, pas sur la relecture
# ─────────────────────────────────────────────────────────────────────────────────────────────


def charger_predictions(chemin: Path = CHEMIN_PREDICTIONS) -> list[dict]:
    with chemin.open(encoding="utf-8") as f:
        return [json.loads(ligne) for ligne in f]


def verifier_equivalence(enregistrements=None) -> dict:
    """Re-dérive les plis et rejoue la règle d'arbitrage sur les prédictions **versionnées**.

    Si ce module avait dérivé du notebook qui a produit `temoin-predictions.jsonl` — un germe
    déplacé, une comparaison retournée, un seuil bougé — les plis ou les décisions rejouées
    différeraient. Ils ne diffèrent pas, ou cette fonction lève.
    """
    enregistrements = charger_predictions() if enregistrements is None else enregistrements
    plis_verifies = decisions_verifiees = 0
    for r in enregistrements:
        i = INDICE_PAR_ID[r["id"]]
        if int(PLIS[r["germe"]][i]) != r["pli"]:
            raise AssertionError(
                f"Pli divergent pour {r['id']} au germe {r['germe']} : "
                f"le module dit {PLIS[r['germe']][i]}, l'artefact dit {r['pli']}")
        plis_verifies += 1

        rejoue = decider(np.array([r["probabilites"][s] for s in SLUGS], dtype=float))
        for champ in ("brut", "predit", "aucun_seuil_franchi", "violation_i2_brute"):
            if rejoue[champ] != r[champ]:
                raise AssertionError(
                    f"Décision divergente pour {r['id']} ({r['montage']}, germe {r['germe']}), "
                    f"champ {champ} : le module dit {rejoue[champ]}, l'artefact dit {r[champ]}")
        if abs(rejoue["marge"] - r["marge"]) > 1e-6:
            raise AssertionError(f"Marge divergente pour {r['id']} ({r['montage']})")
        decisions_verifiees += 1

    montages = sorted({r["montage"] for r in enregistrements})
    return {
        "plis_verifies": plis_verifies,
        "decisions_verifiees": decisions_verifiees,
        "montages": montages,
        "germes": sorted({r["germe"] for r in enregistrements}),
    }


# ─────────────────────────────────────────────────────────────────────────────────────────────
# Le critère de la seconde itération
# ─────────────────────────────────────────────────────────────────────────────────────────────

#: L'exactitude du lexique en accord exact sur les 120 — la barre que #56 demande de dépasser
#: **strictement**, au pire des 5 germes. Recalculée et non recopiée, comme partout ailleurs.
EXACTITUDE_LEXIQUE = sum(1 for i in range(N_EXEMPLES) if accord(L[i], verite[i]))


def exactitude(enregistrements, montage=None, germe=None) -> int:
    """L'accord exact sur les 120 exemples — la métrique du critère de #56."""
    par_id = {r["id"]: set(r["predit"]) for r in enregistrements
              if (montage is None or r["montage"] == montage)
              and (germe is None or r["germe"] == germe)}
    if len(par_id) != N_EXEMPLES:
        raise AssertionError(
            f"Prédictions hors-pli incomplètes : {len(par_id)} au lieu de {N_EXEMPLES}")
    return sum(1 for i, ident in enumerate(identifiants) if par_id[ident] == verite[i])


def exactitudes_par_germe(enregistrements, montage=None, germes=None) -> dict[int, int]:
    germes = sorted({r["germe"] for r in enregistrements}) if germes is None else germes
    return {germe: exactitude(enregistrements, montage, germe) for germe in germes}


def verdict_critere(exactitudes: dict[int, int]) -> dict:
    """Le critère de #56, appliqué sans retouche : **strictement** au-dessus du lexique, au
    **pire** des germes ; l'estimation ponctuelle fait foi, l'intervalle est rapporté."""
    pire = min(exactitudes.values())
    bas, haut = wilson(pire, N_EXEMPLES)
    return {
        "par_germe": dict(exactitudes),
        "pire": pire,
        "meilleur": max(exactitudes.values()),
        "mediane": float(np.median(list(exactitudes.values()))),
        "barre": EXACTITUDE_LEXIQUE,
        "wilson_pire": (bas, haut),
        "franchi": pire > EXACTITUDE_LEXIQUE,
        "germes_evalues": len(exactitudes),
    }


if __name__ == "__main__":
    print(f"racine du dépôt : {RACINE}")
    print(f"{N_EXEMPLES} exemples, {len(GROUPES)} groupes, germes {GERMES}")
    print("étiquettes  :", dict(zip(SLUGS, Y.sum(axis=0))))
    print(f"lexique en accord exact : {EXACTITUDE_LEXIQUE}/{N_EXEMPLES}")
    print("équivalence avec le premier banc :", verifier_equivalence())
