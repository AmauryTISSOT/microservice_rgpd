# ADR-0029 — Prolonger une demande est un Geste, une seule fois, de deux mois

- **Statut** : accepté
- **Date** : 2026-09-17
- **Décidé par** : l'US [#518](https://github.com/AmauryTISSOT/microservice_rgpd/issues/518)
  « ADR-0029 : prolonger une demande est un geste, une seule fois, de deux mois »
- **Glossaires** : [`CONTEXT-MAP.md`](../../CONTEXT-MAP.md), [Requests](../contexts/requests/CONTEXT.md)
- **Supplante, sur un point chacun** :
  - [ADR-0021](./0021-la-demande-tient-un-statut-et-une-date-limite-de-reponse.md) — la clause
    « Elle est **fixée par `DataSubjectRequest.Receive`** et enregistrée ; elle ne se recalcule
    pas », et, en fin de fichier, « **La prolongation du délai** (art. 12.3) » parmi ce qu'il
    n'ouvrait pas. La date limite de réponse cesse d'être immuable : un second `Gesture` la déplace.
    Tout le reste de l'ADR-0021 tient sans retouche — la règle de calcul « réception plus un mois,
    ramenée au dernier jour du mois suivant », le statut comme état, les signalements non
    enregistrés, et le fait que la date limite parte de la date de réception déclarée et jamais de
    l'instant d'enregistrement.
  - [ADR-0023](./0023-modifier-une-demande-est-un-geste.md) — la clause « **La date limite de
    réponse est refaite par toute modification effective**, depuis la date de réception qu'elle
    porte », et, en fin de fichier, « **La prolongation du délai** (art. 12.3) » parmi ce qu'il
    n'ouvrait pas. `Modify` ne refait plus *une* date mais *les deux*, ensemble, quand la demande
    est prolongée. Sans cette reprise, toute modification ultérieure effacerait silencieusement une
    prolongation. Tout le reste de l'ADR-0023 tient : l'empreinte qui s'écrase, la modification sans
    changement qui n'a pas eu lieu, le refus en `Conflict` sur une demande close avant toute
    validation, le dernier enregistrement qui l'emporte.

## Contexte

L'article 12 §3 du RGPD autorise le responsable du traitement à prolonger de deux mois le délai
d'un mois dont il dispose pour répondre à une demande, « compte tenu de la complexité et du nombre
de demandes ». L'`Operator` ne peut pas le faire : le service tient une date limite de réponse, il
ne sait pas la déplacer.

Avant d'écrire ce `Gesture`, une décision de système manque, et deux ADR en vigueur disent
aujourd'hui le contraire de ce que la prolongation exige.

L'**ADR-0021** pose que la date limite « est fixée par `DataSubjectRequest.Receive` et enregistrée ;
elle ne se recalcule pas », et range « la prolongation du délai (art. 12.3) » parmi ce qu'il
n'ouvre pas. Il l'avait pourtant anticipée : c'est en écartant l'option « calculer la date limite à
la lecture » qu'il écrit que « la prolongation de l'art. 12.3 [...] la fera changer ; une valeur
enregistrée est ce que ces actes modifieront ». La prolongation est l'acte annoncé.

L'**ADR-0023** pose `Modify` comme le geste qui refait la date limite depuis la date de réception
— ce qui, tel quel, **écrase silencieusement une prolongation**. Un lecteur qui n'ouvrirait que
l'ADR-0023 croirait qu'une modification remet toute demande à un mois. Il faut trancher avant, pas
pendant.

Le sujet n'est pas neuf dans ce dépôt : le contexte `Casework`, retiré par l'ADR-0017, avait porté
une migration `AddExtensionDeclarationAndLedgerIndex` qui ajoutait `extension_declared_on`,
`extension_informed_on` et `extension_motive varchar(2000)`. C'est un **précédent, pas un contrat**
— son nommage n'est pas repris, et rien de ce qui suit n'en découle.

## Décision

**Prolonger une demande est un `Gesture`** : l'acte par lequel l'`Operator` reporte de deux mois la
date limite de réponse d'une demande, au titre de l'article 12 §3.

### 1. Deux mois, une constante, et non une saisie

La date limite de réponse se prolonge **une fois, de deux mois**, portant le total autorisé à trois
mois à compter de la réception. La durée est une **constante du domaine, pas une saisie de
l'`Operator`**.

Le règlement dit « ce délai peut être prolongé de deux mois », pas « jusqu'à deux mois ». Laisser
l'`Operator` saisir une durée l'inviterait à produire une date limite illicite — plus longue, donc
inopposable ; plus courte, donc engageant le responsable au-delà de ce que la loi lui demande, sans
qu'aucun écran ne sache le lui dire. Un champ que le service devrait ensuite valider à la seule
valeur `2` n'est pas un champ.

### 2. Deux motifs, et deux seulement

Le motif de la prolongation est un **vocabulaire fermé à deux valeurs** : la **complexité de la
demande** et le **nombre de demandes**.

Le vocabulaire est fermé parce que le règlement l'a fermé : l'article 12 §3 énumère ces deux
considérations et n'en ouvre pas une troisième. Une valeur « Autre » serait une invitation à sortir
du cadre légal par un champ que le service a lui-même ouvert.

### 3. Un motif fermé ne suffit pas : la justification écrite est obligatoire

Le motif coché est accompagné d'une **justification en texte libre, obligatoire**.

Les lignes directrices EDPB 01/2022 (§162-164) écartent explicitement les deux confusions qui
guettent : « le simple fait que répondre à la demande exigerait un effort important ne rend pas la
demande complexe », et un grand nombre de demandes ne déclenche pas automatiquement une
prolongation — seul un afflux exceptionnel et temporaire compte. La prolongation « est une exception
à la règle générale et ne devrait pas être surutilisée ».

Le motif fermé prouve que l'`Operator` est resté dans le cadre ; le texte libre fournit le **fait
concret** qu'une autorité de contrôle demandera — quelle complexité, quel afflux. Coché seul, le
motif ne prouve rien : il recopie la loi.

⚠️ **C'est le premier texte libre saisi par l'`Operator` dans ce domaine**, et le précédent est
assumé. Les trois « motifs » qui existent aujourd'hui dans le service sont tous produits par la
machine : les motifs de blocage d'une exécution sont calculés par le serveur (ADR-0027, ADR-0028),
la justification d'une qualification est rendue par un modèle (ADR-0011). Ici, un humain écrit, et
ce qu'il écrit est conservé.

### 4. La fenêtre est stricte : prolonger n'est possible qu'avant l'échéance

Une demande ne se prolonge que **tant que sa date limite de réponse n'est pas passée**.

Une date limite dépassée rend la prolongation sans effet juridique : l'article 12 §3 exige que la
personne concernée soit informée de la prolongation **dans le mois** suivant la réception. Passé ce
terme, le manquement est déjà constitué, et une prolongation posée après coup ne le répare pas —
elle le maquille. Le service ne doit pas offrir ce geste-là.

### 5. Une seule prolongation par demande

Une demande déjà prolongée ne se prolonge plus. **Ce n'est pas une limite technique qu'on lèverait
un jour** : une seconde prolongation produirait une date limite illicite, au-delà des trois mois que
l'article 12 §3 plafonne. Un lecteur qui trouverait cette règle et voudrait « généraliser » en
comptant les prolongations changerait la décision, il ne la compléterait pas.

### 6. La prolongation tient sur l'agrégat, en quatre valeurs

La prolongation est portée par la `DataSubjectRequest` elle-même, en **quatre valeurs** : la date
limite initiale, la date à laquelle la prolongation a été posée, le motif fermé et la justification
écrite. Il n'y a **pas d'entité de trace à côté**.

Ce qui justifie une trace séparée chez `Execute` n'existe pas ici. Le journal d'exécution
(ADR-0026) porte *n* tentatives, dont des échecs, et il survit à la suppression de la demande. Une
prolongation, elle, est **au plus unique**, elle ne peut pas échouer à moitié, et « supprimer une
demande ne laisse aucune trace » (ADR-0022) doit valoir pour elle comme pour le reste : la
justification écrite parle de la demande d'une personne, elle disparaît avec elle.

⚠️ **Exception assumée au refus des états qui court dans le dépôt**, de la même famille que celle de
l'ADR-0021 pour le statut et de l'ADR-0023 pour l'empreinte : la forme est celle d'un état, la
doctrine dit une trace. Elle est prise en connaissance de cause, pour la raison ci-dessus.

### 7. La date limite initiale est enregistrée, pas dérivée

La date limite **initiale** — celle qui valait avant la prolongation — est **enregistrée**, et non
recalculée par soustraction de deux mois.

`AddMonths` n'est pas inversible : 31 décembre + 2 mois = 28 février, et 28 février − 2 mois =
28 décembre, pas le 31. Or c'est la date limite initiale qui fixe l'échéance de **l'obligation
d'information** de l'article 12 §3 : le mois dans lequel la personne concernée doit être avertie de
la prolongation. Elle ne peut pas être approximative, et une colonne qui paraît redondante ne l'est
pas.

### 8. `Modify` recalcule les deux dates ensemble

Sur une demande prolongée, une modification effective **recalcule les deux dates ensemble**, depuis
la date de réception corrigée : la date limite initiale par la règle de l'ADR-0021, et la date
limite de réponse par cette même règle augmentée des deux mois de la prolongation. `Modify` ne
touche **ni au motif, ni à la justification, ni à la date de prolongation** : la prolongation a eu
lieu, elle a été motivée, et corriger une coquille ne défait pas cet acte.

La date de réception est le fait dont tout découle. Si elle était fausse, la date limite initiale
l'était aussi ; la laisser figée graverait une erreur de saisie dans l'avertissement légal.

⚠️ **Sans cette clause, toute modification ultérieure annulerait silencieusement une prolongation.**
C'est le point précis par lequel le présent ADR supplante l'ADR-0023.

### 9. Corriger la date de réception est toujours accepté

Une correction de la date de réception est acceptée **même quand les dates recalculées placent
rétroactivement la prolongation après une échéance dépassée** — c'est-à-dire même quand la demande
corrigée n'aurait jamais pu être prolongée sous la règle de la clause 4.

L'outil doit savoir **afficher cette vérité, pas l'empêcher**. Refuser rendrait une faute de frappe
indéracinable et forcerait à supprimer puis re-saisir la demande — soit exactement la perte de trace
(ADR-0022) que l'outil existe pour éviter. La règle de la clause 4 garde la **porte d'entrée** du
geste ; elle ne s'applique pas rétroactivement à un geste déjà posé.

### 10. Le service n'enregistre pas qui a prolongé

Aucune imputation nominative n'est écrite. Il n'y a **aucune identification des `Operator`** dans le
service : `CreatedBy` et `ModifiedBy` valent tous deux la constante `operator` (ADR-0023). Une
colonne qui ne peut porter qu'une seule valeur ne prouve rien ; elle **simule une redevabilité qui
n'existe pas**, et un auditeur qui la lirait y verrait une garantie qu'elle n'offre pas.

C'est la même décision que celle de l'ADR-0014 pour `Screening`, et pour le même motif : ce que la
trace doit prouver est qu'un humain a tranché, et la date le prouve. L'imputation nominative attend
une identification des `Operator` ; elle n'est pas un oubli, elle est un prérequis nommé.

### 11. L'information de la personne concernée reste hors du service

L'article 12 §3 exige que la personne concernée soit informée de la prolongation et de ses motifs
dans le mois suivant la réception. Le service **ne l'envoie pas** : il se contente de la rappeler à
l'`Operator`, qui informe ailleurs.

⚠️ Une prolongation non suivie d'information **n'est pas opposable**, et le service ne peut pas le
vérifier. Une demande prolongée dans l'outil peut donc être en infraction sans que rien ne le dise.

## Les options écartées

- **Une durée saisie par l'`Operator`**, avec un maximum de deux mois. Plus souple, et le
  responsable pourrait s'engager à moins. Écartée : le règlement ne dit pas « jusqu'à deux mois », et
  un champ que le service valide à la seule valeur licite n'est pas un champ — il est une occasion
  d'erreur, dont le prix est une date limite illicite.
- **Un motif en texte libre seul**, sans vocabulaire fermé. Écartée : rien ne rattacherait alors la
  prolongation aux deux considérations de l'article 12 §3, et le service ne saurait plus dire si
  l'`Operator` est resté dans le cadre. Le fermé prouve le cadre, le libre prouve le fait ; il faut
  les deux (clause 3).
- **Une entité de trace `RequestExtension` à côté**, une ligne par prolongation, sur le modèle du
  journal d'exécution. Fidèle à la doctrine du dépôt. Écartée : il y a au plus une prolongation, elle
  ne peut pas échouer à moitié, et elle doit disparaître avec la demande — les trois raisons qui
  fondent le journal d'exécution manquent toutes (clause 6).
- **La date limite initiale dérivée par soustraction de deux mois**, sans colonne. Une colonne de
  moins. Écartée : `AddMonths` n'est pas inversible, et c'est l'échéance de l'obligation
  d'information qui serait approximative (clause 7).
- **`Modify` refusé sur une demande prolongée**, en `Conflict`, comme il l'est sur une demande close.
  Simple, et la prolongation serait à l'abri. Écartée : une prolongation est un acte de plus sur une
  demande en cours, pas une clôture ; refuser rendrait indéracinable la faute de frappe que `Modify`
  existe pour corriger (clauses 8 et 9).
- **Une colonne `extended_by`**, sur le modèle de `ModifiedBy`. Écartée : elle ne pourrait porter que
  `operator`, et une redevabilité simulée est pire qu'une redevabilité absente (clause 10).

## Conséquences, y compris celles qui coûtent

⚠️ **Une demande prolongée dans l'outil peut être en infraction sans que rien ne le dise.** Le
service ne sait pas si la personne concernée a été informée dans le mois (clause 11) ; il enregistre
une prolongation dont il ne peut pas établir l'opposabilité.

⚠️ **Le service conserve un texte libre écrit par un humain.** La justification est une donnée de
plus, qui parle d'une personne identifiée, et que personne ne relit. Elle suit le sort de la demande
— supprimée avec elle (ADR-0022) —, et rien ne la contrôle à la saisie.

⚠️ **Une correction de la date de réception peut produire une prolongation incohérente avec la règle
qui l'a autorisée** : une demande dont la date limite initiale recalculée est déjà passée restera
prolongée (clause 9). L'écran devra savoir montrer cet état sans le nier.

⚠️ **La date limite initiale est une colonne qui paraît redondante.** Un lecteur qui la croirait
dérivable la supprimerait ; la clause 7 existe pour l'en dissuader, et elle est la seule chose qui
l'en dissuade.

⚠️ **Un signalement peut disparaître du tableau sans laisser de trace.** Une prolongation retire un
« En retard » ou une « Échéance proche » (ADR-0021) : l'échéance affichée est celle qui engage, mais
rien ne dit qu'une autre a existé. C'est la même conséquence que celle que l'ADR-0023 assumait déjà
pour la correction de la date de réception, désormais atteignable par un geste dont c'est
précisément l'objet.

⚠️ **La règle de calcul de la date limite est augmentée, pas remplacée.** Elle reste écrite une
seule fois (ADR-0021), mais elle se lit désormais en deux temps — réception plus un mois, puis plus
deux mois si la demande est prolongée. Un lecteur qui n'appliquerait que le premier temps
retrouverait la date limite initiale, et croirait avoir la bonne.

## Ce que cet ADR n'ouvre pas

- **Les noms de colonnes, les codes HTTP et la forme de la modale.** Ce sont des choix
  d'implémentation ; ils vivent dans le ticket qui écrira le geste, et le nommage de l'ancien
  `Casework` n'y a aucune autorité.
- ⚠️ **L'information de la personne concernée** (art. 12 §3, 3e phrase). Elle reste hors du service ;
  le jour où le geste d'informer y entrera, il fera l'objet d'une décision propre.
- **La suspension du délai** pendant une demande d'identité ou de précision (art. 12 §6,
  considérant 63, EDPB 01/2022 §159). Elle ne se confond pas avec la prolongation.
- **Le report de la date limite au jour ouvrable suivant.** Il relève de la règle générale de calcul
  de l'ADR-0021, pas de la prolongation.
- **L'identification des `Operator`.** Elle est nommée comme prérequis d'une imputation nominative
  (clause 10), elle n'est pas ouverte ici.
- ⚠️ **Une seconde prolongation.** Ce n'est pas un plafond technique laissé à lever : voir la
  clause 5.
- **L'historique des modifications**, que l'ADR-0023 rangeait déjà parmi ce qu'il n'ouvrait pas.
