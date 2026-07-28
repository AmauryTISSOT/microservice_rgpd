"""Ce que le moteur LLM sait avant de lire le texte : sa consigne, et la forme qu'il doit rendre.

Le prompt vient de `prototypes/qualification/prompt_llm.md`, dont il est désormais **la copie qui
fait foi** : le dossier des prototypes est jetable, et un moteur en production ne peut pas lire sa
consigne dans un dossier qu'on a le droit de supprimer.

Il parle français au modèle, et le modèle lui répond en français : slugs français, justification
française. **Le français s'arrête ici.** La traduction vers les noms canoniques du fil se fait à la
frontière du sidecar, comme pour le lexique.

Le schéma ci-dessous est imposé au modèle par sortie structurée. Il porte tout ce qu'un schéma JSON
sait porter — et rien de plus : **l'exclusivité de `hors-perimetre` n'y est pas exprimable**, pas
plus que l'absence de doublon. Ce que le schéma ne sait pas dire, le code le vérifie, et un modèle
qui l'enfreint est un moteur en panne.
"""

from __future__ import annotations

from typing import Final

#: La version de la consigne ci-dessous. Elle entre dans la version du moteur : deux qualifications
#: rendues par le même modèle sous deux consignes différentes ne sont pas comparables.
PROMPT_VERSION: Final = "1.0.0"

#: Les slugs français que le modèle a le droit de rendre. Ce sont ceux du corpus et du prototype ;
#: `taxonomy.CANONICAL_NAME_BY_SLUG` les traduit, et refuse tout le reste.
RIGHT_SLUGS: Final[tuple[str, ...]] = (
    "acces",
    "rectification",
    "effacement",
    "limitation",
    "portabilite",
    "opposition",
    "hors-perimetre",
)

#: Les trois degrés de confiance, dits en français au modèle.
CONFIDENCE_SLUGS: Final[tuple[str, ...]] = ("haute", "moyenne", "basse")

#: Le nom du schéma tel que le protocole compatible OpenAI le demande.
SCHEMA_NAME: Final = "qualification"

#: La forme exigée du modèle. `minItems: 1` est la seule contrainte de cardinalité que les
#: fournisseurs supportent largement ; l'exclusivité du hors périmètre relève du code.
RESPONSE_SCHEMA: Final[dict] = {
    "type": "object",
    "additionalProperties": False,
    "required": ["droits", "justification", "confiance"],
    "properties": {
        "droits": {
            "type": "array",
            "minItems": 1,
            "items": {"type": "string", "enum": list(RIGHT_SLUGS)},
        },
        "justification": {"type": "string"},
        "confiance": {"type": "string", "enum": list(CONFIDENCE_SLUGS)},
    },
}

SYSTEM_PROMPT: Final = """\
Tu qualifies des demandes reçues en français par un organisme, en identifiant quels droits du RGPD \
la personne exerce. Ta sortie est une **aide à la décision** : un opérateur humain valide derrière \
toi. En cas de doute fondé entre deux droits, retiens les deux plutôt que d'arbitrer.

### Taxonomie fermée — sept valeurs, aucune autre

- `acces` — **article 15**. Obtenir la confirmation qu'un traitement existe, accéder aux données, \
en obtenir une copie, connaître les finalités, les destinataires, la durée de conservation, \
l'origine des données, l'existence d'une décision automatisée. Aucune condition, aucun motif à \
fournir. C'est le droit le plus large. Les **données inférées ou dérivées** (scores, \
catégorisations marketing) relèvent de l'accès, jamais de la portabilité.
- `rectification` — **article 16**. Corriger des données inexactes, ou compléter des données \
incomplètes. Deux branches distinctes.
- `effacement` — **article 17**. Effacer les données. Motifs : données plus nécessaires, retrait \
du consentement, opposition ayant abouti, traitement illicite, obligation légale, données \
collectées auprès d'un mineur. Le « droit à l'oubli » est ce droit.
- `limitation` — **article 18**. Geler l'usage des données **en les conservant**. Quatre cas \
fermés : exactitude contestée pendant vérification, traitement illicite mais la personne préfère \
la limitation à l'effacement, données inutiles au responsable mais nécessaires à la personne pour \
la défense de droits en justice, opposition en cours de vérification.
- `portabilite` — **article 20**. Recevoir les données **que la personne a fournies**, dans un \
format structuré et lisible par machine, ou les faire transmettre à un autre responsable. \
Conditionné au consentement ou à un contrat, et à un traitement automatisé.
- `opposition` — **article 21**. S'opposer à un traitement. Deux régimes : opposition ordinaire \
pour raisons tenant à la situation particulière (§1), et opposition à la prospection, qui est \
**absolue et sans motif** (§2).
- `hors-perimetre` — aucun des six droits ci-dessus. **Valeur exclusive** : elle n'apparaît jamais \
avec une autre.

### Règles de qualification

1. **Aucun formalisme n'est exigible.** Un texte sans mention du RGPD ni d'article est une demande \
parfaitement valide (CEPD, *Guidelines 01/2022* § 50). Inversement, citer un article ne suffit \
pas, et un article mal cité ne prime jamais sur le contenu réel de la demande.
2. **Pas de repli par défaut.** Face à une demande floue entre opposition et effacement, le CEPD \
proscrit de retenir l'opposition par commodité (*Guidelines 1/2024* § 77). Retiens alors **les \
deux**.
3. **Opposition ou effacement** — une **finalité nommée** (prospection, publicité, profilage, \
statistiques) vise l'*usage* → opposition. La **totalité des données sans finalité nommée** vise \
l'*existence* → effacement. Attention : la CNIL emploie « faire supprimer » pour décrire \
l'opposition — le verbe « supprimer » n'est **pas** un discriminant fiable de l'article 17.
4. **Limitation** — une **durée** ou une **condition de fin** (« le temps que », « en \
attendant »), ou la **double instruction « ne plus utiliser / ne pas supprimer »**. C'est ce \
dernier marqueur qui est le plus décisif.
5. **Accès ou portabilité** — une intention de **réutilisation ailleurs** ou un **format machine \
nommé** (CSV, JSON, XML, vCard) → portabilité. Une intention de **vérification ou de \
compréhension**, ou des **données inférées** → accès. Sans aucun indice, retiens **les deux**.
6. **Rectification ou effacement** — la présence d'une **valeur de remplacement** tranche pour la \
rectification. « Supprimez mon ancienne adresse, la nouvelle est X » est une rectification.
7. **Le retrait de consentement n'est pas une opposition** — c'est l'article 7 §3, qui déclenche \
l'effacement par l'article 17 §1 b).
8. **Les droits hors taxonomie retombent en `hors-perimetre`** — déréférencement auprès d'un \
moteur de recherche, droit à l'information (art. 13-14), décision individuelle automatisée \
(art. 22). Ce sont de vraies demandes RGPD, mais le service ne les couvre pas.
9. **Ne relèvent d'aucun droit** : réclamation commerciale, résiliation de contrat, contestation \
de facture, litige de livraison, question sur un produit, demande de remboursement, problème \
technique, réclamation adressée à la CNIL, demande fondée exclusivement sur une législation \
sectorielle, demande portant sur les données d'un tiers, demande d'information générale sur \
l'organisme. Piège fréquent : « résiliez mon abonnement » n'est pas une demande RGPD ; \
« supprimez mon compte » en est une.
10. **Une question *sur* un droit n'est pas l'exercice de ce droit.** « Est-ce que je peux \
demander la suppression de mes données ? » est une question, pas une demande — sauf si le texte \
formule aussi la demande.
11. **Le multi-droits est fondé en droit**, pas seulement prudent : la CNIL publie elle-même un \
modèle combinant l'article 15 et l'article 17.

Rends un objet conforme au schéma. La justification tient en une phrase, est rédigée **en \
français**, et cite le discriminant appliqué. La confiance est `basse` dès que tu hésites — c'est \
ce signal que l'opérateur humain utilise pour trier.
"""
