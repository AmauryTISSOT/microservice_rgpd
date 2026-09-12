# ADR-0023 — Modifier une demande est un Geste

- **Statut** : accepté
- **Date** : 2026-09-12
- **Décidé par** : l'US « Modifier une demande »
- **Glossaires** : [`CONTEXT-MAP.md`](../../CONTEXT-MAP.md), [Requests](../contexts/requests/CONTEXT.md)
- **Supplante, sur un point** :
  [ADR-0017](./0017-casework-est-retire-requests-le-remplace.md) — la phrase « Il ne connaît à ce
  jour qu'un `Gesture` : enregistrer une demande à sa réception ». `Requests` en connaît un second,
  modifier une demande. Le reste de la phrase, « l'instruction, les délais et les statuts n'y
  existent pas », était déjà supplanté par l'ADR-0021 pour les délais et les statuts ; l'instruction
  n'entre toujours pas.
- **Ne supplante pas l'[ADR-0021](./0021-la-demande-tient-un-statut-et-une-date-limite-de-reponse.md)**,
  il l'**honore** : celui-ci rangeait parmi les conséquences qui coûtent que « la date limite ne suit
  pas la date de réception », et annonçait que « l'US qui l'ouvrira devra recalculer la date
  limite ». C'est ce que cet ADR fait. Le reste de l'ADR-0021 tient sans retouche : la règle de
  calcul, le statut comme état, les signalements non enregistrés.

## Contexte

L'`Operator` qui se trompe en enregistrant une demande n'avait aucun moyen de se corriger. Une date
de réception fautive, un prénom mal orthographié, un droit choisi trop vite : la seule issue était de
supprimer la demande et de la ressaisir. La suppression ne laissant aucune trace (ADR-0022), la
correction d'une coquille détruisait la date d'enregistrement d'origine.

`Requests` ne connaissait qu'un `Gesture`, enregistrer une demande. Corriger une demande enregistrée
en est un second, et il pose quatre questions que la langue de système ne tranchait pas : ce que la
correction laisse derrière elle, ce qu'elle laisse quand elle ne corrige rien, sur quelles demandes
elle est offerte, et ce qu'il advient de la date limite quand la date de réception change.

## Décision

**Modifier une demande est un `Gesture`** : l'acte par lequel l'`Operator` corrige une erreur de
*saisie* dans les données de la demande telle qu'elle a été enregistrée. Il laisse une trace datée,
`ModifiedAt`, signée `ModifiedBy` — la constante `OperatorAuthor`, comme `CreatedBy`, tant que le
service n'authentifie personne.

**L'empreinte n'est pas affichée.** Elle existe en base, colonnes `modified_at` et `modified_by`,
nulles tant qu'aucune modification n'a eu lieu ; aucun écran ne la montre. Elle sert le responsable
de traitement — pouvoir un jour rendre compte de qui a touché à une demande (art. 5.2) —, pas
l'`Operator` au travail. L'écrire aujourd'hui coûte deux colonnes ; l'ajouter après coup coûterait
toutes les modifications survenues entre-temps, définitivement perdues.

⚠️ **L'empreinte s'écrase, et c'est une exception assumée à la langue de système.** Un `Gesture`
y laisse *n* traces datées, chacune se tenant seule ; modifier une demande n'en laisse qu'une, la
dernière, qui remplace la précédente. Deux colonnes sur l'agrégat, et non une table de traces : la
forme est celle d'un état, la doctrine dit une trace. L'exception est prise en connaissance de
cause, parce que ce que l'empreinte doit dire — cette demande a été corrigée, et quand pour la
dernière fois — tient tout entier dans la dernière ligne, et qu'une table de traces serait un
historique des modifications, que cet ADR n'ouvre pas.

⚠️ **Une modification sans changement n'est pas un `Gesture` : elle n'a pas eu lieu.** Quand la
saisie validée est structurellement égale aux valeurs courantes, `Modify` n'affecte aucune propriété
— pas même `ResponseDeadline` — et renvoie un succès. Aucune propriété touchée, donc aucun `UPDATE`
émis par le suivi de modifications, et l'empreinte reste celle de la modification précédente. C'est
ce qui donne son sens à l'empreinte : `ModifiedAt` date un changement, jamais une visite.

**Une demande close n'est plus modifiable.** `Modify` refuse en `Result.Conflict()` dès que le statut
n'est pas `InProgress`, et ce refus passe **avant** la validation de la saisie : lister des erreurs
de champ dans un formulaire qui ne pourra jamais enregistrer n'apprend rien à l'`Operator`. La règle
tient au domaine, indépendamment de l'écran, qui éteint par ailleurs le bouton d'une demande close.

**La date limite de réponse est refaite par toute modification effective**, depuis la date de
réception qu'elle porte, par la même fonction que la réception — `DeadlineFor(receivedOn)`, une
seule écriture de la règle « réception plus un mois, ramenée au dernier jour du mois suivant » de
l'ADR-0021. Elle ne bouge donc que si la date de réception a bougé, mais elle est recalculée sans
qu'on ait à le demander : la modification n'invente aucune arithmétique et n'en saute aucune, elle
rejoue celle qui existe.

**Le dernier enregistrement l'emporte.** Deux `Operator` qui corrigent la même demande ne se gênent
pas : le second écrit par-dessus le premier. Pas de verrou optimiste, pas de colonne de version, pas
de jeton de concurrence. Le service compte un `Operator` non authentifié ; le conflit que ce verrou
préviendrait ne se produit pas encore, et la seule chose qu'il produirait aujourd'hui est un refus
que personne ne saurait lire.

## Les options écartées

- **Empiler les modifications dans un journal** — une table `data_subject_request_modifications`,
  une ligne par correction, avec le champ touché, l'avant et l'après. C'est ce que demanderait un
  vrai devoir de rendre compte. Écartée : l'US n'en demande pas, aucun écran ne le lirait, et un
  journal des valeurs *avant* correction conserverait précisément les données personnelles fautives
  que la correction efface.
- **N'écrire aucune empreinte du tout**, et s'en tenir aux huit champs. Cohérent avec l'ADR-0022,
  qui accepte qu'une suppression ne laisse rien. Écartée : la suppression retire la demande du
  service, la modification la garde en la changeant — un dossier conservé dont on ne sait pas qu'il
  a bougé est pire qu'un dossier absent.
- **Un verrou optimiste** — une colonne `version`, un refus en `409` quand la demande a bougé depuis
  le chargement du formulaire. Écartée pour le motif ci-dessus ; elle pourra s'ajouter le jour où le
  service authentifiera plusieurs `Operator`, sans rien retoucher d'autre.
- **Laisser modifier une demande close**, en recalculant tout. Écartée : un dossier clos qui bouge
  n'est plus clos, et le statut cesserait de dire quoi que ce soit.
- **Traiter la modification sans changement comme une modification**, en posant l'empreinte quand
  même. Plus simple à écrire — aucune comparaison —, mais l'empreinte ne dirait plus « cette demande
  a changé », seulement « ce formulaire a été rouvert ».

## Conséquences, y compris celles qui coûtent

⚠️ **`ModifiedAt` s'écrase à chaque modification.** Seule la dernière correction est datée ; les
précédentes ne laissent rien. L'empreinte répond à « cette demande a-t-elle été corrigée, et
quand pour la dernière fois ? », jamais à « qu'a-t-on corrigé, et combien de fois ? ».

⚠️ **Le service ne peut pas prouver ce qu'une demande disait avant sa correction.** Une date de
réception corrigée emporte avec elle la date limite qu'elle fondait : l'ancienne échéance n'est
nulle part.

⚠️ **Une correction peut faire basculer les signalements d'une demande.** Repousser la date de
réception retire un « En retard » ; la reculer en pose un. C'est voulu — l'échéance affichée est
celle qui engage —, mais cela veut dire qu'un retard peut disparaître du tableau sans que rien ne
dise qu'il y a été.

⚠️ **Deux corrections concurrentes se perdent en silence.** Le second enregistrement écrase le
premier, et aucune des deux parties n'apprend que l'autre a écrit.

## Ce que cet ADR n'ouvre pas

- ⚠️ **L'historique des modifications.** L'empreinte n'est pas un journal inachevé : c'est une
  décision de n'en pas tenir. Un lecteur qui trouverait `ModifiedAt` seul et voudrait « finir le
  travail » en ajoutant le champ touché, l'avant et l'après changerait la décision, il ne la
  compléterait pas. Cela demande son propre ADR.
- **L'affichage de l'empreinte**, sur la ligne du tableau ou ailleurs.
- **L'identité de l'`Operator`.** `ModifiedBy` vaut `operator` pour la même raison que `CreatedBy` :
  le service n'authentifie personne.
- **Le verrou de concurrence**, et tout refus fondé sur « la demande a bougé depuis ».
- **Les actes qui font changer le statut** — terminer, annuler —, que l'ADR-0021 rangeait déjà
  parmi ce qu'il n'ouvrait pas.
- **La prolongation du délai** (art. 12.3).
