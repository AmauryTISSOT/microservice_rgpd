# Qualification RGPD

Ce service reçoit d'une application tierce un texte libre en français et le qualifie au regard des droits que le RGPD ouvre aux personnes concernées. Il rend une aide à la décision : un humain, côté application tierce, valide ou corrige le verdict.

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
La taxonomie fermée de sept valeurs dans laquelle une `Qualification` puise. Six sont des droits ouverts par le RGPD ; la septième dit qu'aucun d'eux n'a été reconnu.
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

### Ce que le service ne fait pas

**Aide à la décision** :
La posture du service : il propose une `Qualification` sans jamais la trancher. La validation est le fait d'un humain, hors du service. Une erreur de qualification coûte donc peu, ce qui autorise à reconnaître plusieurs droits plutôt qu'à choisir.
_Avoid_ : décision, arbitrage, verdict automatique

**Trace d'audit** :
Le seul écrit que le service conserve d'une qualification. Il n'existe aucune entité de demande instruite dans le temps : le service ne suit pas le traitement de la demande, seulement l'acte de l'avoir qualifiée.
_Avoid_ : historique, dossier, demande, log
