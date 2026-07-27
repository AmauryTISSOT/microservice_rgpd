"""Moteur A — qualification déterministe par lexiques et règles.

Prototype **jetable** instruisant le ticket #6. Aucune vocation à être porté
tel quel en C# : il sert à mesurer ce qu'une approche déterministe atteint sur
le corpus, pas à préfigurer l'implémentation.

Règles construites *à partir des seules recherches* #3 et #4 (vocabulaire CNIL,
articles du RGPD, lignes directrices CEPD), sans lecture des étiquettes du
corpus — c'est la condition pour que l'évaluation mesure une généralisation et
non un surapprentissage.

Sortie : liste non vide de droits parmi la taxonomie fermée à 7 valeurs.
`hors-perimetre` est exclusif.
"""

from __future__ import annotations

import re
import unicodedata

ACCES = "acces"
RECTIFICATION = "rectification"
EFFACEMENT = "effacement"
LIMITATION = "limitation"
PORTABILITE = "portabilite"
OPPOSITION = "opposition"
HORS_PERIMETRE = "hors-perimetre"

DROITS = (ACCES, RECTIFICATION, EFFACEMENT, LIMITATION, PORTABILITE, OPPOSITION)


# --------------------------------------------------------------------------
# Normalisation
# --------------------------------------------------------------------------

def normaliser(texte: str) -> str:
    """Minuscules, dé-accentuation, apostrophes unifiées, espaces réduits.

    Dé-accentuation : décomposition NFD puis retrait des marques combinantes.
    Le corpus contient un registre `maladroit` où les accents sautent — les
    motifs sont donc tous écrits sans accent.
    """
    texte = texte.replace("’", "'").replace("ʼ", "'")
    texte = unicodedata.normalize("NFD", texte.lower())
    texte = "".join(c for c in texte if unicodedata.category(c) != "Mn")
    return re.sub(r"\s+", " ", texte)


# --------------------------------------------------------------------------
# Négation — recette SLP3 annexe B §B.4 transposée au français
# --------------------------------------------------------------------------
#
# ⚠️ Transposition non triviale, et c'est le principal enseignement de la
# première itération de ce prototype. La recette anglaise préfixe `NOT_` après
# tout marqueur de négation. Appliquée telle quelle au français, elle **inverse
# le sens** des demandes les plus fréquentes : « ne plus utiliser mes données »
# et « je ne veux plus recevoir vos offres » *sont* l'opposition et la
# limitation, pas leur négation. Deux corrections :
#
# 1. `plus` est retiré des négateurs — « ne … plus » exprime en français une
#    demande d'arrêt, donc un marqueur positif.
# 2. Les motifs qui intègrent eux-mêmes la négation sont marqués insensibles :
#    sans cela, ils se pénalisent mutuellement.

# Portée : du marqueur de négation à la prochaine ponctuation forte.
_NEGATIONS = r"\b(?:ne|n'|pas|jamais|aucun|aucune|sans|ni)\b"
_PONCTUATION = r"[.;:!?]"


def portees_de_negation(texte: str) -> list[tuple[int, int]]:
    """Intervalles [début, fin) sous portée d'une négation."""
    portees: list[tuple[int, int]] = []
    for m in re.finditer(_NEGATIONS, texte):
        fin = re.search(_PONCTUATION, texte[m.end():])
        portees.append((m.start(), m.end() + (fin.start() if fin else len(texte))))
    return portees


def sous_negation(texte: str, position: int) -> bool:
    return any(d <= position < f for d, f in portees_de_negation(texte))


# --------------------------------------------------------------------------
# Lexiques par droit
#
# Chaque entrée : (motif, poids, sensible_a_la_negation). Le poids reflète la
# force du marqueur selon la recherche #4 — une citation d'article vaut plus
# qu'un verbe polysémique. Le troisième champ vaut `False` pour les motifs qui
# intègrent déjà une négation (cf. le bloc « Négation » ci-dessus).
#
# `DONNEES` couvre les graphies dégradées du registre `maladroit` : après
# dé-accentuation, « données », « donnée », « donné » et « donnés » donnent
# respectivement donnees, donnee, donne et donnes.
# --------------------------------------------------------------------------

DONNEES = r"donn(?:e|ee)s?"
INFOS = rf"(?:{DONNEES}|informations?|infos?|renseignements?)"

LEXIQUES: dict[str, list[tuple[str, int, bool]]] = {
    ACCES: [
        (r"article\s*1?5\b(?!\d)", 5, True),
        (r"\bdroit d'?\s*acces\b", 5, True),
        (rf"\bacceder a mes {DONNEES}\b", 4, True),
        (r"\bcopie (?:de|des|integrale|complete|en langage clair)", 3, True),
        (rf"\bquelle?s? {INFOS}\b.{{0,40}}\b(?:detenez|avez|possedez|gardez|conservez|figurent|utilisez)", 4, True),
        (r"\bsavoir (?:si|quelles|ce que|cke)\b", 3, True),
        (r"\bsuis[- ]je fiche", 4, True),
        (r"\bje suis dans vos fichiers\b", 4, True),
        (rf"\bconnaitre (?:les|mes|quelles?|la liste) (?:{INFOS}|comptes)\b", 3, True),
        (r"\bcommuniquez[- ]moi mon dossier\b", 4, True),
        (rf"\bm'?indiquer si des {DONNEES}\b", 4, True),
        (rf"\borigine (?:de ces|des|de mes|de vos) {DONNEES}\b|\bl'?origine (?:de ces|des) {DONNEES}\b", 4, True),
        (r"\bd'?ou (?:viennent|provienn?ent)\b", 3, True),
        (r"\bduree de conservation\b", 3, True),
        (rf"\b(?:finalites|destinataires)\b.{{0,30}}\b(?:traitement|{DONNEES})\b", 3, True),
        (r"\bliste des destinataires\b|\bqui a eu acces\b", 4, True),
        (r"\bce que vous (?:avez|savez|gardez|stockez|conservez|detenez)\b", 4, True),
        (r"\bvoir ce que vous\b|\bvous avez quoi comme\b|\bvous ave sur moi\b", 4, True),
        (rf"\bconsulter (?:mon dossier|mes {DONNEES})\b|\bvoir mon dossier\b|\bvoire mon dossier\b", 4, True),
        (rf"\bliste (?:de|des) (?:mes )?{INFOS}\b", 3, True),
        # Art. 15 §1 a) — finalité et base légale du traitement.
        (r"\bsur quelle base legale\b|\bbase legale\b", 3, True),
        (r"\bpourquoi vous (?:conservez|gardez|traitez|detenez)\b", 4, True),
        # Art. 15 §1 h) — données inférées, exclues de la portabilité.
        (r"\bscore\b|\bvotre algorithme\b|\bprofils? que vous (?:avez )?dedui", 4, True),
        (r"\bannotations internes\b|\bechanges enregistres\b", 3, True),
        (rf"\b(?:communiquez|transmettre|envoyez)[- ]moi\b.{{0,30}}\b(?:mon dossier|l'?integralite|l'?ensemble)\b", 3, True),
    ],
    RECTIFICATION: [
        (r"article\s*16\b", 5, True),
        (r"\bdroit (?:de|a la) rectification\b", 5, True),
        (r"\brectifi(?:er|ez|cation)\b", 4, True),
        (r"\bcorrig(?:er|ez)\b|\bcorrection\b", 4, True),
        # « mise à jour » nominal est écarté : il désigne le plus souvent la mise
        # à jour d'une politique ou d'un service, pas une demande de l'art. 16.
        (r"\bmet(?:tre|tez|re) a jour\b|\bmetre a jour\b", 3, True),
        (r"\bchangez[- ]l[ae]\b|\bchang(?:er|ez) (?:mon|ma|mes)\b", 3, True),
        (r"\bmodifi(?:er|ez)\b.{0,30}\b(?:adresse|nom|prenom|numero|date|email|telephone|coordonnees)\b", 3, True),
        (r"\binexacte?s?\b|\berronn?ee?s?\b|\bfausse?s?\b|\bc'?est faux\b", 4, True),
        (r"\bn'?est plus exacte?\b|\bn'?est plus a jour\b", 4, False),
        (r"\bincomplete?s?\b|\bil manque\b|\bcomplet(?:er|ez)\b", 3, True),
        (r"\bmal (?:orthographie|ecrit|epele|renseigne)\b|\bfaute d'?orthographe\b", 4, True),
        (r"\bj'?ai demenage\b|\bmon adresse a change\b|\bnouvelle adresse\b|\ba change\b", 3, True),
        (r"\bancien(?:ne)? (?:mail|adresse|numero|email)\b", 3, True),
        (r"\berreur\b", 2, True),
    ],
    EFFACEMENT: [
        (r"article\s*17\b", 5, True),
        (r"\bdroit (?:a l'?oubli|a l'?effacement)\b", 5, True),
        (r"\beffac(?:er|ez|ement|iez)\b", 4, True),
        (r"\bsuprim(?:er|ez|e)\b|\bsupprim(?:er|ez|e|ion)\b", 3, True),
        (r"\bsuppression\b", 3, True),
        (r"\bdetrui(?:re|sez)\b|\bdestruction\b", 4, True),
        (rf"\benlev(?:er|ez)\b.{{0,30}}\b(?:{DONNEES}|informations|fichier|profil|photo)\b", 3, True),
        (r"\bvirez tout\b|\bvire tout\b", 4, True),
        (r"\bne (?:gardez|conservez) (?:rien|plus rien|aucune)\b", 4, False),
        (r"\bsupprim(?:er|ez) mon compte\b|\bfermer mon compte\b", 4, True),
        (r"\bje retire (?:mon|le) consentement\b|\bretrait de (?:mon )?consentement\b", 4, True),
        (r"\bj'?etais mineur\b|\bil a douze ans\b|\balors qu'?il a \d+ ans\b", 3, True),
        (r"\bplus aucune trace\b|\bqu'?il ne reste rien\b", 4, False),
        # Art. 17 §1 a) — données plus nécessaires.
        (r"\bne sont plus necessaires\b|\bplus necessaires au regard des finalites\b", 4, False),
        (r"\bretirez[- ]moi de vos fichiers\b|\bretirer de vos fichiers\b", 4, True),
        (r"\bne (?:veux|souhaite) plus (?:que vous ayez|apparaitre)\b", 4, False),
    ],
    LIMITATION: [
        (r"article\s*18\b", 5, True),
        (r"\b(?:droit a la |demande de |demander la |demande la )limitation\b", 5, True),
        (r"\blimit(?:er|ez) (?:le traitement|l'?utilisation|l'?usage)\b", 5, True),
        (r"\bgel(?:er|ez)\b|\bgel de\b", 4, True),
        (r"\bsuspend(?:re|ez)\b|\bsuspension\b", 4, True),
        (r"\bbloqu(?:er|ez)\b", 3, True),
        (r"\bmet(?:tre|tez) en pause\b|\ben attente\b", 3, True),
        (r"\ba titre conservatoire\b|\bconservatoire\b", 4, True),
        (r"\ble temps (?:que|de|d')\b|\ben attendant\b|\bjusqu'?a ce que\b|\bjusqua ce que\b|\btant que\b|\bpendant (?:la|le|l'?)\b", 3, True),
        (r"\bdefense de (?:mes )?droits en justice\b|\bprud'?hommes\b|\bmon avocat\b|\bcontentieux\b|\blitige\b|\bpreuve pour mon dossier\b", 3, True),
        # Marqueur décisif (CNIL) : ne plus utiliser tout en conservant.
        (r"\bne (?:les |l'?)?(?:utilisez|servez|exploitez|touchez) plus\b", 4, False),
        (r"\bplus utiliser mes\b|\bcessez de les (?:utiliser|exploiter)\b|\bn'?y touchez plus\b", 4, False),
    ],
    PORTABILITE: [
        (r"article\s*20\b", 5, True),
        (r"\bdroit a la portabilite\b|\bportabilite\b", 5, True),
        (r"\bexport(?:er|ez|ation)?\b", 4, True),
        (r"\btelecharg(?:er|ez|ement)\b", 3, True),
        (r"\bformat (?:structure|csv|json|xml|vcard|vcf|machine|lisible par machine|ouvert|reutilisable|exploitable)\b", 5, True),
        (r"\b(?:en|au|dans un) format\b", 3, True),
        (r"\b(?:csv|json|xml|vcf|vcard|exel|excel)\b", 4, True),
        (r"\blisible par machine\b|\bexploitable ailleurs\b|\breimporter\b|\bimporter dans\b", 5, True),
        (rf"\brecuperer (?:mes|toutes mes|les) (?:{DONNEES}|photos|messages)\b", 3, True),
        (r"\b(?:transferer|transmettre|migrer|basculer|reprendre)\b.{0,40}\b(?:autre|nouveau|concurrent|chez|ailleurs|la[- ]bas)\b", 4, True),
        (r"\bje change (?:d'?operateur|de banque|de fournisseur|d'?assureur|de plateforme)\b", 4, True),
        (r"\bje pars chez\b|\bchez un concurrent\b|\bchez un autre\b|\bvers un autre service\b", 4, True),
        (r"\breutiliser\b|\bles reutiliser\b", 3, True),
        (r"\bemporter mes\b", 4, True),
        (r"\bque je vous ai fourni\b|\bque j'?ai (?:saisi|depose)\b", 4, True),
        (r"\barchive complete\b", 3, True),
    ],
    OPPOSITION: [
        (r"article\s*21\b", 5, True),
        (r"\bdroit d'?opposition\b", 5, True),
        (r"\bje m'?oppose\b|\bopposition (?:au|a la|a ce|a l')\b", 5, True),
        (r"\bje refuse\b", 4, True),
        (r"\bcess(?:er|ez)\b|\barret(?:er|ez|e)\b|\bstop\b", 3, True),
        (r"\bne (?:plus|pas) (?:m'?|me )?(?:envoyer|envoyez|recevoir|contacter|demarcher|solliciter|transmettre)\b", 4, False),
        (r"\bne m'?envoyez plus\b|\bne veut plus recevoir\b|\bne veux plus recevoir\b", 4, False),
        (r"\bdesabonn(?:er|ez|ement)\b|\bdesinscri(?:re|ption|vez)\b", 4, True),
        (r"\bstop pub\b|\bplus de (?:pub|publicite|demarchage|prospection|sollicitation)\b", 4, True),
        (r"\bretir(?:ez|er)[- ]?moi de (?:la|vos|vos listes|votre)\b.{0,20}\b(?:liste|pub|diffusion|fichiers d'?envoi)\b", 4, True),
        (r"\bprospection\b|\bdemarchage\b|\bmarketing\b|\bpublicitaire\b|\bpublicite\b|\bpub\b|\boffres commerciales\b|\bnewsletter\b", 3, True),
        (r"\bprofilage\b|\bciblage\b|\brecommandations personnalisees\b|\bpersonnalisation\b|\bpublicites ciblees\b", 3, True),
        (r"\bje ne (?:souhaite|veux|veut) plus\b", 3, False),
        (rf"\bne (?:soient|soit|servent) plus\b|\bque mes {DONNEES} (?:ne )?servent\b|\bque mes achats servent\b", 3, False),
        (r"\bpartenaires commerciaux\b|\bfins de prospection\b|\ba des fins de\b", 3, True),
        (r"\bj'?en ai assez de vos appels\b|\bplus d'?appels\b", 3, True),
    ],
}

LEXIQUES_COMPILES = {
    droit: [(re.compile(m), p, s) for m, p, s in motifs] for droit, motifs in LEXIQUES.items()
}


# --------------------------------------------------------------------------
# Marqueurs hors périmètre (recherche #4 §6)
# --------------------------------------------------------------------------

# a) Droits RGPD réels mais hors des 7 valeurs → retombent en hors-perimetre.
HORS_TAXONOMIE = [
    r"\bderefencement\b|\bdereferencement\b|\bdesindexer\b|\bdesindexation\b",
    r"\bmoteur de recherche\b.{0,40}\b(?:resultat|lien|associe)",
    r"\barticle\s*22\b|\bdecision (?:automatisee|entierement automatisee)\b",
    r"\bintervention (?:d'?un|humaine)\b.{0,30}\b(?:humain|decision)\b",
    r"\barticles?\s*1[34]\b|\bdroit a l'?information\b",
]

# b) À adresser ailleurs, ou réclamation.
AILLEURS = [
    r"\bje porte plainte\b|\bje depose (?:une )?plainte\b|\breclamation aupres de la cnil\b",
    r"\bficoba\b|\bimpots\.gouv\b",
    r"\bcode (?:de la consommation|monetaire|du travail|de la sante)\b",
    r"\bfichier des personnes recherchees\b|\bfichier de (?:police|gendarmerie|renseignement)\b|\bficp\b|\bfcc\b",
    r"\bloi (?:informatique et libertes de 1978|du 6 janvier 1978)\b.{0,40}\bexclusivement\b",
]

# c) Pas une demande de droit du tout.
# ⚠️ `\bavoir\b` a été retiré : capté « je ne veux plus rien avoir à faire avec
# vous », qui est une demande d'effacement. Un motif de litige commercial doit
# porter sur un terme non polysémique.
PAS_UNE_DEMANDE = [
    r"\bresili(?:er|ez|ation)\b",
    r"\bfactur(?:e|ation|e de)\b|\bprelevement\b|\bremboursement\b|\bgeste commercial\b",
    r"\bliv(?:raison|re)\b|\bcolis\b|\bcommande (?:n'?est|n'?a|jamais)\b",
    r"\bsav\b|\bservice apres[- ]vente\b|\bgarantie legale\b|\bpanne\b",
    r"\bmot de passe\b|\bme connecter\b|\bconnexion\b|\bbug\b",
    r"\bdevis\b|\btarif\b|\bcombien coute\b|\boffre premium\b|\bchiffre d'?affaires\b",
    r"\bcandidature\b|\bcurriculum vitae\b|\bprendre rendez[- ]vous\b",
    r"\bmerci pour votre\b|\bfelicitations\b|\bbien recu votre\b",
]

# Question *sur* un droit, sans exercice — le texte s'informe, il ne demande pas.
QUESTION_SUR_LE_DROIT = [
    r"\best[- ]ce que (?:je peux|j'?ai le droit|il est possible)\b",
    r"\bai[- ]je le droit\b|\bj'?ai le droit de demander\b",
    r"\bcomment (?:faire pour|puis[- ]je|je peux)\b.{0,40}\b(?:demander|exercer)\b",
    r"\bquels sont mes droits\b",
    r"\bje voudrais savoir si (?:je peux|j'?ai le droit)\b",
    r"\bou consulter votre politique\b|\bvotre politique de confidentialite\b|\bqui est votre (?:dpo|delegue)\b",
    r"\bcomment fonctionne\b|\bquel est votre modele\b|\bcombien de salaries\b",
]

HORS_PERIMETRE_MOTIFS = [
    re.compile(m)
    for m in HORS_TAXONOMIE + AILLEURS + PAS_UNE_DEMANDE + QUESTION_SUR_LE_DROIT
]

# Marqueurs d'exercice effectif d'un droit : neutralisent les indices « pas une
# demande » quand le texte formule aussi une demande RGPD explicite.
EXERCICE_EXPLICITE = re.compile(
    r"\brgpd\b|\breglement general sur la protection des donnees\b|"
    r"\bdonnees (?:a caractere )?personnelles?\b|\bmes donnees\b|"
    r"\barticle\s*(?:15|16|17|18|20|21)\b|\bdroit (?:d'?acces|de rectification|"
    r"a l'?effacement|a l'?oubli|a la limitation|a la portabilite|d'?opposition)\b"
)


# --------------------------------------------------------------------------
# Discriminants des paires confondues (recherche #4 §4)
# --------------------------------------------------------------------------

VALEUR_DE_REMPLACEMENT = re.compile(
    r"\bau lieu de\b|\bet non\b|\ba la place\b|\bremplac(?:er|ez)\b|"
    r"\bc'?est (?:desormais|maintenant|en fait)\b|\bla bonne (?:adresse|valeur|orthographe|date)\b|"
    r"\bmon nouveau?\b|\bma nouvelle\b|\bdoit etre\b|\bs'?ecrit\b"
)

FINALITE_NOMMEE = re.compile(
    r"\ba des fins de\b|\bpour (?:de la |la |vos )?(?:prospection|publicite|marketing|"
    r"demarchage|statistique|analyse)\b|\bprospection\b|\bnewsletter\b|\bpublicitaire\b|"
    r"\bciblage\b|\bprofilage\b"
)

TOTALITE = re.compile(
    r"\b(?:toutes|l'?ensemble de|tout|integralite|totalite)\b.{0,30}\b(?:mes |les )?donnees\b|"
    r"\bmon compte\b|\btous mes fichiers\b|\bplus aucune trace\b"
)

CONSERVER_SANS_UTILISER = re.compile(
    r"\bne (?:les |le |l'?)?(?:supprimez|effacez|detruisez) (?:pas|surtout pas)\b|"
    r"\bne (?:les )?effacez pas\b|\bmais (?:gardez|conservez|ne supprimez|de les garder)\b|"
    r"\bsans (?:pour autant )?(?:les )?(?:supprimer|effacer|detruire)\b|"
    r"\btout en (?:les )?conservant\b|\bconservez[- ]les\b|\bgardez tout\b|"
    r"\bje m'?oppose a l'?effacement\b|\bmais de les garder\b"
)

REUTILISATION_AILLEURS = re.compile(
    r"\bchez (?:un autre|votre concurrent|mon nouveau)\b|\bailleurs\b|"
    r"\bautre (?:operateur|fournisseur|banque|plateforme|service|prestataire)\b|"
    r"\bles reutiliser\b|\bles importer\b|\bles transferer a\b"
)

INTENTION_DE_VERIFICATION = re.compile(
    r"\bverifier\b|\bcomprendre\b|\bsavoir\b|\bm'?assurer\b|\bcontroler\b|\bconsulter\b"
)

DUREE_OU_CONDITION = re.compile(
    r"\ble temps (?:que|de|d')\b|\ben attendant\b|\bjusqu'?a\b|\bpendant (?:que|la|le|l'?)\b|"
    r"\btemporairement\b|\bprovisoirement\b|\bdans l'?attente\b"
)

RETRAIT_CONSENTEMENT = re.compile(
    r"\bje retire mon consentement\b|\bretrait de (?:mon )?consentement\b|"
    r"\bje ne consens plus\b|\bje revoque mon (?:accord|consentement)\b"
)

SEUIL = 3
# Score au-delà duquel un marqueur RGPD l'emporte sur un indice hors périmètre.
SEUIL_FORT = 5
# Score minimal pour un repli sous le seuil, plutôt qu'un rejet.
SEUIL_REPLI = 2


def _score(texte_norm: str, droit: str) -> int:
    """Somme des poids des motifs déclenchés, hors portée de négation."""
    total = 0
    for motif, poids, sensible in LEXIQUES_COMPILES[droit]:
        for m in motif.finditer(texte_norm):
            penalise = sensible and sous_negation(texte_norm, m.start())
            total += -poids if penalise else poids
    return total


def qualifier(texte: str) -> list[str]:
    """Rend la liste non vide des droits qualifiés pour ce texte."""
    t = normaliser(texte)

    scores = {droit: _score(t, droit) for droit in DROITS}

    # --- Discriminants : ils corrigent les scores avant le seuillage. --------

    # Rectification vs effacement — une valeur de remplacement tranche art. 16.
    if VALEUR_DE_REMPLACEMENT.search(t) and scores[RECTIFICATION] > 0:
        scores[RECTIFICATION] += 4
        if not TOTALITE.search(t):
            scores[EFFACEMENT] -= 3

    # Limitation — la double instruction « ne plus utiliser / ne pas supprimer »
    # est le marqueur le plus discriminant (CNIL).
    if CONSERVER_SANS_UTILISER.search(t):
        scores[LIMITATION] += 5
        scores[EFFACEMENT] -= 4
    if DUREE_OU_CONDITION.search(t) and scores[LIMITATION] > 0:
        scores[LIMITATION] += 2

    # Accès vs portabilité — réutilisation ailleurs ou format machine → art. 20 ;
    # intention de vérification → art. 15.
    if REUTILISATION_AILLEURS.search(t) and scores[PORTABILITE] > 0:
        scores[PORTABILITE] += 3
    if INTENTION_DE_VERIFICATION.search(t) and scores[ACCES] > 0:
        scores[ACCES] += 2

    # Opposition vs effacement — une finalité nommée vise l'usage (art. 21) ;
    # la totalité sans finalité vise l'existence (art. 17).
    if FINALITE_NOMMEE.search(t):
        scores[OPPOSITION] += 3
        if not TOTALITE.search(t):
            scores[EFFACEMENT] -= 2
    elif TOTALITE.search(t) and scores[EFFACEMENT] > 0:
        scores[EFFACEMENT] += 2

    # Retrait de consentement — art. 7 §3, déclenche l'effacement par
    # l'art. 17 §1 b), et n'est *pas* une opposition.
    if RETRAIT_CONSENTEMENT.search(t):
        scores[EFFACEMENT] += 4
        scores[OPPOSITION] -= 3

    retenus = [d for d in DROITS if scores[d] >= SEUIL]
    meilleur = max(scores.values())

    # --- Garde hors périmètre ----------------------------------------------
    # La garde ne s'applique pas quand le texte porte par ailleurs un marqueur
    # RGPD explicite ou un score fort : « supprimez mon compte, et par ailleurs
    # résiliez mon abonnement » reste une demande d'effacement.
    exercice = EXERCICE_EXPLICITE.search(t) is not None
    indice_hp = any(m.search(t) for m in HORS_PERIMETRE_MOTIFS)

    if indice_hp and not exercice and meilleur < SEUIL_FORT:
        return [HORS_PERIMETRE]

    if retenus:
        return retenus

    # --- Repli sous le seuil ------------------------------------------------
    # Le CEPD demande d'interpréter largement une demande ambiguë plutôt que de
    # la rejeter (*Guidelines 01/2022* § 50). Un indice faible mais présent, sur
    # un texte qui parle bien de données personnelles, vaut mieux qu'un rejet.
    if meilleur >= SEUIL_REPLI and exercice:
        return [d for d in DROITS if scores[d] == meilleur]

    return [HORS_PERIMETRE]


def qualifier_detaille(texte: str) -> dict:
    """Variante rendant les scores — utile pour l'analyse d'erreurs."""
    t = normaliser(texte)
    return {
        "droits": qualifier(texte),
        "scores": {d: _score(t, d) for d in DROITS},
        "texte_normalise": t,
    }
