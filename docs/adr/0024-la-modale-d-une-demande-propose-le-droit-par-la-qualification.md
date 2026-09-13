# ADR-0024 — La modale d'une demande propose le droit par la qualification

- **Statut** : accepté
- **Date** : 2026-09-13
- **Décidé par** : le PRD [#446](https://github.com/AmauryTISSOT/microservice_rgpd/issues/446),
  tracé par [#447](https://github.com/AmauryTISSOT/microservice_rgpd/issues/447)
- **Glossaires** : [`CONTEXT-MAP.md`](../../CONTEXT-MAP.md),
  [Requests](../contexts/requests/CONTEXT.md), [Qualification](../contexts/qualification/CONTEXT.md)
- **Supplante, sur un point** :
  [ADR-0017](./0017-casework-est-retire-requests-le-remplace.md) — la décision « Aucune relation
  n'est déclarée entre `Requests` et `Qualification` », et, parmi ce qu'il n'ouvrait pas, « une
  relation entre `Requests` et `Qualification`, y compris le bouton « Qualification du droit par
  IA », désactivé dans la modale ». Le reste de l'ADR-0017 tient, et en particulier la matrice des
  traversées : elle interdit toujours les deux sens entre `Requests` et `Qualification`.
- **Écrit une réserve sur l'[ADR-0022](./0022-supprimer-une-demande-ne-laisse-aucune-trace.md)**,
  sans le supplanter : supprimer une demande ne laisse aucune trace *dans `Requests`*, mais un
  Message qualifié depuis la modale reste dans la `Trace d'audit` de `Qualification`. La décision
  de l'ADR-0022 tient entière ; c'est sa portée qui est écrite ici.

## Contexte

Quand l'`Operator` enregistre ou modifie une demande, il lit le Message et choisit seul le Droit
invoqué parmi les six droits. Le service sait déjà qualifier un texte — c'est le métier de
`Qualification` —, mais la modale de création et de modification d'une demande n'en profitait pas :
le bouton « Qualification du droit par IA » y était désactivé, et l'ADR-0017 rangeait toute relation
entre les deux contextes parmi ce qu'il n'ouvrait pas, en annonçant qu'elle demanderait son propre
ADR.

Le PRD #446 rend ce bouton actif. Une relation s'ouvre donc, et deux questions avec elle : **où** elle
passe — dans le domaine ou à l'écran —, et ce qu'elle fait à la promesse « sans trace » de la
suppression, puisque qualifier un Message l'écrit dans la trace d'audit de `Qualification`.

## Décision

**`Qualification` est en amont, en _Open Host_, à l'écran.** La modale de `Requests` appelle un
handler de la surface de `Qualification` — `POST /qualification?handler=Propose` —, par HTTP, depuis
le navigateur. Ce handler envoie la commande existante, `QualifyCommand`, et rend une projection
dédiée à l'écran : les droits proposés avec leur libellé, le `ReviewSignal`, le booléen `degraded`
et la justification. Il ne rend ni l'identifiant de la qualification, ni la référence appelante, et
le contrat public de `POST /qualifications` ne bouge pas (ADR-0011).

**`Core` et `UseCases` de `Requests` ignorent `Qualification`.** La relation ne traverse que par une
adresse que la vue du tableau rend, et par une réponse JSON que le script lit : aucun type de
`Requests` ne dépend d'un type de `Qualification`, et le page model du tableau n'envoie pas
`QualifyCommand`. C'est pourquoi **la liste blanche de `ContextIsolationTests` ne change pas** : le
garde lit l'IL, et la relation n'y paraît pas. Les deux sens restent interdits, et leur motif reste
vrai au niveau où le garde le vérifie — le domaine de `Requests` ne lit jamais un verdict.

**La qualification propose le droit ; seul l'enregistrement par l'`Operator` le choisit.** La
proposition ne fait que sélectionner une valeur dans le select, que l'`Operator` peut changer, et
qui n'a d'effet que s'il enregistre. C'est l'`Aide à la décision` de la langue de système : la
machine propose, un humain tranche.

**La demande ne référence aucune qualification.** Rien de la proposition n'est enregistré sur la
demande — ni identifiant, ni droits proposés, ni justification, ni signal. La qualification est
posée sans référence appelante : la trace d'audit ne sait pas qu'elle est venue d'une demande. Le
`qualificationId` opaque que portait un `Case` n'a toujours pas de successeur.

**Seul le Message est envoyé.** L'Origine, le nom, le prénom et l'email de la personne ne quittent
pas la demande.

## La réserve sur l'ADR-0022

L'ADR-0022 décide que supprimer une demande la retire définitivement, « et rien d'autre n'est
écrit ». Cela reste vrai **dans `Requests`**. Mais un Message qualifié depuis la modale a été écrit,
au moment de la qualification, dans la `Trace d'audit` de `Qualification` : intégral et en clair,
comme tout texte qualifié. Supprimer la demande ne l'en retire pas.

Ce Message n'a **aucun lien retrouvable** vers la demande : pas de référence appelante, pas
d'identifiant de demande, et la demande n'a jamais porté l'identifiant de la qualification. Il ne
permet donc pas de prouver qu'une demande a existé, ni de la reconstituer ; mais les données
personnelles que le Message contient y demeurent.

⚠️ **C'est assumé.** La trace d'audit relève de la redevabilité de `Qualification` — elle prouve
qu'un verdict a été rendu, sur quel texte, par quels moteurs —, et sa durée de conservation se
décide chez elle, pas au rythme des demandes de `Requests`. Effacer la ligne d'audit à la
suppression exigerait justement le lien que cet ADR refuse d'écrire.

## Les options écartées

- **Faire envoyer `QualifyCommand` par le page model du tableau des demandes.** Plus court, sans
  aller-retour HTTP. Écartée : le page model appartient à `Requests` par son espace de noms, la
  traversée paraîtrait dans l'IL, et il faudrait élargir la liste blanche pour une relation qui ne
  vit qu'à l'écran.
- **Ouvrir une traversée `Requests → Qualification` dans le domaine**, un port de `UseCases` que
  `Qualification` implémenterait. Écartée : aucune règle de `Requests` ne dépend d'un verdict. Le
  droit reste choisi par l'`Operator`, et un port de domaine ferait croire le contraire.
- **Appeler `POST /qualifications`, le contrat public.** Écartée : il est fait pour les applications
  tierces, rend l'identifiant de qualification et accepte une référence appelante, et la modale n'a
  besoin ni de l'un ni de l'autre. La projection d'écran évite aussi de graver dans le contrat ce
  que l'écran lit.
- **Relier la demande à sa qualification** — une référence appelante égale à l'identifiant de la
  demande, ou l'identifiant de qualification enregistré sur la demande. Elle permettrait d'effacer la
  trace d'audit à la suppression, ou de prouver ce qui a été proposé. Écartée : la demande deviendrait
  dépendante d'un verdict, et la trace d'audit prendrait une valeur d'écran pour une donnée fournie
  par une application tierce.
- **Effacer de la trace d'audit tout Message qualifié depuis l'écran**, ou ne pas l'y écrire.
  Écartée : la trace ne distingue pas l'origine d'une qualification, et une qualification sans trace
  n'est plus prouvable.

## Conséquences, y compris celles qui coûtent

⚠️ **« Sans trace » a désormais une portée.** Une demande supprimée ne laisse rien dans `Requests` ;
le Message qu'on a qualifié pour elle reste dans la trace d'audit de `Qualification`, sans lien vers
elle. Un responsable de traitement qui répond à une personne « votre demande a été supprimée » doit
le savoir.

⚠️ **Le garde d'isolation ne voit pas la relation.** Il n'a pas été conçu pour la voir : il protège
le domaine et les cas d'usage, et la relation n'y entre pas. Une relation qui passerait un jour par un
type — le page model du tableau qui enverrait la commande, un DTO partagé — le ferait échouer, et ce
serait le signal voulu.

**La relation est écrite dans `CONTEXT-MAP.md`**, qui ne dit plus « aucune relation déclarée », et
dans les glossaires : au Droit invoqué de `Requests`, « qualification » sort des _Avoid_, puisqu'une
`Qualification` peut désormais le proposer ; l'introduction du glossaire de `Qualification` dit de
même.

**Le message d'échec du garde pour `Requests → Qualification`** — « jamais lu d'un verdict » — n'est
pas réécrit : il parle du domaine, où il reste exact.

## Ce que cet ADR n'ouvre pas

- **L'enregistrement de la qualification sur la demande**, sous quelque forme que ce soit.
- **La qualification automatique**, sans clic de l'`Operator`.
- **Une traversée de domaine entre `Requests` et `Qualification`**, dans un sens ou dans l'autre :
  elle demanderait son propre ADR et l'élargissement de la liste blanche.
- **La distinction, dans la trace d'audit, d'une qualification venue de l'écran**, et la durée de
  conservation de cette trace.
- **Une modification de `QualifyResponse`** ou du contrat de `POST /qualifications`.
