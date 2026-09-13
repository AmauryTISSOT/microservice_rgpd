# Qualification RGPD

Ce contexte ne connaît que **l'instant du verdict**. Il reçoit d'une application tierce un texte libre en français et le qualifie au regard des droits que le RGPD ouvre aux personnes concernées. Un humain valide ou corrige le verdict — c'est l'`Aide à la décision`, définie une fois pour tout le dépôt dans [`CONTEXT-MAP.md`](../../../CONTEXT-MAP.md).

L'enregistrement d'une demande à sa réception appartient à [Requests](../requests/CONTEXT.md), dont ce contexte est l'amont, à l'écran seulement : la modale d'une demande appelle la surface de qualification pour **proposer** le droit invoqué, que seul l'enregistrement par l'`Operator` **choisit** ; la demande ne référence aucune qualification. Dans le code, les deux ne partagent que `DataSubjectRight`, par le noyau partagé — voir l'[ADR-0024](../../adr/0024-la-modale-d-une-demande-propose-le-droit-par-la-qualification.md). [Screening](../screening/CONTEXT.md) et [Configuration](../configuration/CONTEXT.md) ne communiquent avec celui-ci en aucune façon.

Les identifiants du code sont en anglais ; les textes destinés à l'humain — libellés, messages, documentation d'API — sont en français.

## Language

### Le texte reçu et son verdict

**RightsRequestText** :
Le texte libre reçu de l'application tierce, présumé être une demande d'exercice de droits sans qu'on préjuge qu'il en soit une.
_Avoid_ : demande, requête, message, payload

**Qualification** :
Le verdict rendu sur un `RightsRequestText` : l'ensemble des droits que le texte est jugé exercer. Une `Qualification` reconnaît toujours au moins une valeur — l'absence de droit reconnu est elle-même une valeur nommée, jamais un ensemble vide.
_Avoid_ : classification, catégorisation, analyse, évaluation

**DataSubjectRight** :
La taxonomie fermée de sept valeurs dans laquelle une `Qualification` puise. Six sont des droits ouverts par le RGPD ; la septième dit qu'aucun d'eux n'a été reconnu. ⚠️ Elle est le **noyau partagé** de ce contexte, de `Requests` et de `Configuration`, et n'appartient à aucun d'eux : son auteur est le RGPD, articles 15 à 21. `Screening` n'y touche pas. On n'y touche pas depuis ce contexte seul — voir [`CONTEXT-MAP.md`](../../../CONTEXT-MAP.md).
_Avoid_ : catégorie, label, classe, type de demande

### Les sept valeurs de la taxonomie

**Access** :
Le droit d'obtenir communication des données traitées et des informations sur leur traitement (art. 15).
_Avoid_ : consultation, communication, DSAR

**Rectification** :
Le droit de faire corriger des données inexactes ou compléter des données incomplètes (art. 16).
_Avoid_ : correction, modification, mise à jour

**Erasure** :
Le droit de faire supprimer des données, dit aussi droit à l'oubli (art. 17).
_Avoid_ : suppression, effacement, RightToBeForgotten, deletion

**Restriction** :
Le droit de faire geler le traitement de données sans les faire supprimer (art. 18).
_Avoid_ : limitation, gel, suspension, RestrictionOfProcessing

**Portability** :
Le droit de recevoir ses données dans un format réutilisable, ou de les faire transmettre à un autre responsable de traitement (art. 20).
_Avoid_ : export, transfert, DataPortability

**Objection** :
Le droit de s'opposer à un traitement, notamment à la prospection (art. 21).
_Avoid_ : opposition, refus, retrait du consentement, désinscription

**OutOfScope** :
Le verdict rendu quand aucun des six droits n'est reconnu dans le texte. Exclusif : il ne se combine jamais avec un droit. Il couvre aussi bien un texte étranger au RGPD qu'une demande relevant d'un droit resté hors de la taxonomie — déréférencement, droit à l'information, décision individuelle automatisée (art. 22).
_Avoid_ : hors sujet, inconnu, non classé, None, Unknown

### L'entrecontrôle des deux moteurs

**QualificationOpinion** :
L'avis rendu par un seul moteur sur un `RightsRequestText` : une `Qualification`, accompagnée de la `DeclaredConfidence` du moteur lorsqu'il sait en produire une.
_Avoid_ : prédiction, résultat, réponse, sortie du modèle

**DeclaredConfidence** :
L'échelle ordinale à trois degrés — `High`, `Medium`, `Low` — par laquelle un moteur dit à quel point il doute de sa propre `QualificationOpinion`. Un moteur peut n'en produire aucune, et le lexique est dans ce cas.
_Avoid_ : certitude, probabilité, score de confiance, fiabilité

**QualificationEngineIdentity** :
Le nom et la version qu'un moteur joint à sa `QualificationOpinion` — celle de ses règles pour un lexique, celle du modèle servi pour un LLM. Elle sert à la `Trace d'audit`, qui conserve les avis avec le moteur dont ils relèvent, et le domaine ne l'interprète jamais. ⚠️ **Aucune réponse du contrat HTTP ne la porte ; la surface de l'`Operator` peut la montrer.** Ce que la clause protège est le contrat des applications tierces — le publier sur le fil graverait l'architecture dans un contrat public et inviterait l'appelant à recalculer chez lui la règle que le service tient. Elle ne dit rien de ce que le service montre à l'humain qui est devant lui, et pour qui l'identité du moteur relève du diagnostic. Voir l'[ADR-0011](../../adr/0011-les-internes-des-moteurs-sur-la-surface-de-l-operator.md).
_Avoid_ : modèle, moteur, provenance, signature

**LexiconOpinion** :
La `QualificationOpinion` du lexique. En marche nominale elle sert exclusivement à corroborer ou contester celle du LLM, sans jamais contribuer à la `Qualification` rendue. Elle ne devient elle-même la `Qualification` qu'en `Mode dégradé`, quand le LLM n'a rendu aucun avis. Les deux rôles ne coexistent jamais : elle ne vote pas aux côtés du LLM, l'union de deux avis étant indéfinissable puisque `OutOfScope` est exclusif.
_Avoid_ : second avis, avis secondaire, vote, contre-expertise

**ReviewSignal** :
Ce que le service dit à l'opérateur humain de l'urgence à relire la `Qualification`, déduit de la comparaison des deux `QualificationOpinion` lorsque les deux existent. Il priorise la relecture, il ne la déclenche pas — un humain valide de toute façon chaque qualification.
_Avoid_ : alerte, statut, drapeau, score global

**Contested** :
Les deux moteurs divergent. Signal le plus fort, parce qu'il est le seul à ne rien devoir à l'auto-évaluation du LLM — il l'emporte donc quand la confiance est basse par ailleurs.
_Avoid_ : conflit, désaccord, litige

**NeedsReview** :
Le verdict n'a pas reçu de contrôle indépendant favorable assorti d'une confiance haute. Deux situations le produisent : les deux moteurs s'accordent mais la `DeclaredConfidence` du LLM n'est pas `High`, ou bien le contrôle n'a pas pu avoir lieu faute d'un moteur — le `Mode dégradé`, où c'est donc le seul signal possible.
_Avoid_ : incertain, à vérifier, douteux

**Corroborated** :
Les deux moteurs s'accordent et la `DeclaredConfidence` du LLM est `High`. Inatteignable en `Mode dégradé`.
_Avoid_ : validé, confirmé, certain

**Mode dégradé** :
L'état d'une `Qualification` rendue alors que le service n'était pas entier — un des deux moteurs n'ayant pas produit d'avis. Il recouvre deux situations que le contrat public ne distingue pas, le booléen `degraded` étant sa seule expression vers l'appelant : le LLM absent, auquel cas la `LexiconOpinion` tient lieu de `Qualification` et rien n'est justifié ; ou le lexique absent, auquel cas le verdict est normal mais sans contrôle. La `Trace d'audit` les distingue, elle, par la nullité de l'avis manquant. Un moteur peut n'avoir rendu aucun avis pour deux raisons que le mode ne distingue pas davantage : il a défailli, ou bien son rôle n'est **délibérément pas pourvu** par ce déploiement — configuré sans moteur LLM, le service qualifie en `Mode dégradé` à chaque appel, alors que rien n'est en panne. Sa portée s'arrête en revanche aux moteurs : une base de données indisponible est une panne du service, pas un mode dégradé.
_Avoid_ : mode secours, repli, fallback, panne partielle

### Les bornes de ce contexte

**Erreur relue** :
Le régime d'erreur de ce contexte. Toute `Qualification` passe sous les yeux d'un humain avant de produire le moindre effet, et le texte qui l'a produite est sous ses yeux en même temps : une erreur y est donc une ligne **fausse et visible**, et elle coûte peu. C'est ce qui autorise à reconnaître plusieurs droits plutôt qu'à choisir.
_Avoid_ : erreur bénigne, faux positif, erreur rattrapable

⚠️ Ce régime ne vaut **que dans ce contexte**. Celui de `Screening` est l'`Omission relue`, où l'erreur qui coûte est la ligne manquante — et il commande l'inverse : ne jamais affirmer qu'on a tout couvert.

**Trace d'audit** :
Le seul écrit que ce contexte conserve d'une qualification : l'acte de l'avoir qualifiée, et rien de plus. Il n'y a ici aucune entité de demande — la demande enregistrée est la matière de `Requests`, qui ne référence aucune qualification. ⚠️ Un Message qualifié depuis la modale d'une demande y reste, intégral et en clair, quand la demande est supprimée : sans lien retrouvable vers elle, et c'est assumé (ADR-0024).
_Avoid_ : historique, dossier, demande, log
