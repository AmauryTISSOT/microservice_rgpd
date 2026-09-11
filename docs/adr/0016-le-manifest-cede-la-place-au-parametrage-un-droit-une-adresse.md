# ADR-0016 — Le `Manifest` cède la place au Paramétrage : un droit, une adresse

- **Statut** : accepté
- **Date** : 2026-09-11
- **Décidé par** : le PRD [#328](https://github.com/AmauryTISSOT/microservice_rgpd/issues/328),
  livré par [#329](https://github.com/AmauryTISSOT/microservice_rgpd/issues/329) à
  [#333](https://github.com/AmauryTISSOT/microservice_rgpd/issues/333) et tracé par
  [#334](https://github.com/AmauryTISSOT/microservice_rgpd/issues/334)
- **Supplante, sur deux points** :
  [ADR-0008](./0008-le-service-se-nomme-microservice-rgpd-et-la-configuration-porte-deux-noms.md) —
  le nom de l'écran de configuration (« Configuration » dans le panneau, « Configuration du
  microservice RGPD » sur la carte et en titre), et sa clause « `Manifest` reste `Manifest`,
  `/manifest` reste `/manifest` ». Le reste de l'ADR-0008 tient, y compris le mécanisme des deux
  champs d'`EntryPoint`.
- **Supplante, sur un point** :
  [ADR-0006](./0006-trois-points-d-entree-renommes-en-francais-identifiants-inchanges.md) — `Manifest`
  dans la liste des identifiants C# qui ne bougent pas. Il n'a pas été renommé : il a été supprimé.
  Le motif de la clause, le contrat public et son `system_id`, reste entier.
- **Écrit la dérogation qu'un quatrième contexte doit écrire**, selon
  l'[ADR-0003](./0003-troisieme-contexte-sans-intersection-et-garde-des-traversees.md) : la liste
  blanche du garde gagne la traversée `Configuration → SharedKernel`. L'ADR-0003 n'est pas
  supplanté ; il avait prévu ce geste.

## Contexte

L'écran de configuration reposait sur le `Manifest` : un catalogue de `DeclaredSystem` que
l'`Operator` déclarait à la main — libellé, prose « contient », `Capability`, adresse d'`Adapter` —,
vérifiable contre les `Adapter` qui les servaient.

Le service n'a pas besoin de recenser des systèmes pour savoir où agir. Il a besoin de savoir, pour
l'application hôte, à quelle adresse appeler pour exercer chacun des droits RGPD. Le modèle par
système était trop riche pour cela (capacités, prose, secrets d'appel, vérification) et mal aligné :
l'intégrateur ne pense pas « mes systèmes », il pense « pour le droit d'accès, appelle ici ».

## Décision

**Le Paramétrage remplace le `Manifest` comme surface de configuration.** C'est l'agrégat
`Settings` : un singleton, propriété du service, qui associe chacun des six `DataSubjectRight` du
périmètre — jamais `OutOfScope` — à un `EndpointUrl` optionnel. Un droit sans adresse est
« non configuré », état valide. L'écran vit à `/parametrage` ; il liste les six droits avec leur
libellé français et leur article, et offre une mini-form par droit (définir, modifier, effacer)
sans aucun compte ni ratio. Enregistrer une adresse n'émet aucun appel.

**Le Paramétrage est un contexte borné à part, `Configuration`.** Il n'est pas propre à
l'instruction d'un dossier : c'est un réglage qui vaut pour toutes les demandes. Son glossaire est
[`docs/contexts/configuration/CONTEXT.md`](../contexts/configuration/CONTEXT.md). Il traverse les
quatre couches comme les autres (`Core/Configuration/`, `UseCases/Configuration/`,
`Infrastructure/Data/Configuration/`, `Web/Pages/Configuration/`).

**`Configuration` est le troisième consommateur du noyau partagé**, aux côtés de `Qualification` et
de `Casework`. Libellés et articles se lisent sur `DataSubjectRight` (`FrenchLabel`, `Article`),
jamais recopiés. Les gardes d'IL l'enregistrent :

- `ContextInspector.All` nomme `Configuration`, et `ContextRosterTests` l'ancre sur le nouveau
  glossaire ;
- la liste blanche de `ContextIsolationTests` passe à trois lignes, la troisième étant
  `Configuration → SharedKernel` ; toute autre traversée depuis ou vers `Configuration` est
  interdite ;
- la dérogation d'`AppDbContext` dans `ContextlessTypeTests` gagne `Configuration`, pour la table
  `settings`.

**Le point de montage de `Web` quitte le nom `Configurations/`** et devient `Composition/`. Le garde
lit l'appartenance à un contexte par préfixe de segment, pour attraper `Qualifications` et
`Screenings` ; il lisait donc `Web.Configurations.ServiceConfigs` comme du `Configuration` et y
voyait une traversée vers `Casework`. C'est la collision que l'ADR-0006 notait déjà (« Coût 1 — le
mot désigne déjà les réglages du service ») ; elle cesse d'être tenable le jour où le mot nomme un
contexte.

**La surface du `Manifest` disparaît** (#333) : déclaration, révision, lecture et vérification des
systèmes, l'écran et son formulaire, la sonde au faux secret. `GET /manifest` rend 404.
**`DeclaredSystem` reste**, avec sa fabrique, ses value objects, `Capability`, sa configuration EF
et la table `declared_systems` : l'instruction d'un `Case` s'y accroche encore.

**L'écran se nomme « Paramétrage ».** Le code reste en anglais (`Configuration`, `Settings`,
`EndpointUrl`), l'interface en français. Le panneau dit « Paramétrage » ; la carte de l'accueil, le
`<h1>` et le titre d'onglet disent « Paramétrage du microservice RGPD ». Le mécanisme de l'ADR-0008
— forme courte dans le panneau que le wordmark précède, forme pleine là où rien ne la précède —
tient, sur un seul mot. Les deux noms « Configuration » et « Configuration du microservice RGPD »
quittent la surface.

## Les options écartées

- **Garder le `Manifest` et y ajouter une adresse par droit.** Le grain resterait le système, et
  l'intégrateur continuerait de décrire un paysage pour dire une adresse.
- **Ranger le `Settings` dans `Casework`.** Le réglage n'appartient à aucune instruction ; le mettre
  là aurait fait lire un singleton de service comme une donnée de dossier.
- **Semer les six droits par migration.** Le `Settings` naît au premier enregistrement ; avant,
  il se lit « six droits non configurés ». La règle « il y a six droits » vit dans le code.
- **Supprimer `DeclaredSystem` dans le même mouvement.** `Case`, `Step`, `Locate`, `Read`, `Deliver`
  et `CallAdapter` en dépendent ; les recâbler est une décision à part (voir la dette ci-dessous).
- **Assouplir la lecture par préfixe plutôt que renommer `Web/Configurations/`.** Le préfixe est ce
  qui attrape les dossiers au pluriel ; l'assouplir pour un dossier de gabarit aurait affaibli le
  garde de tous les contextes.

## Conséquences, y compris celles qui coûtent

⚠️ **Dette de recâblage : l'instruction n'a plus de source de `DeclaredSystem`.** Plus aucun écran
ni aucun appel de production n'invoque `DeclaredSystem.Declare`. Le flux d'instruction compile et
ses tests passent, mais sur une base neuve un `Case` s'ouvre sans aucun `Step`, et la
`DeliveryLetter` n'a aucun système à nommer. La déclaration des systèmes, que le glossaire de
`Casework` tient pour un prérequis de l'`Omission silencieuse`, ne peut plus être faite. Une base
existante garde ses `DeclaredSystem`, qui continuent de servir mais ne se relisent ni ne se
révisent plus à l'écran. La dette se paie par l'US « le `Case` appelle par droit » : l'instruction
cible les `EndpointUrl` du Paramétrage au lieu des `DeclaredSystem`. Ce recâblage ouvrira une
traversée entre `Casework` et `Configuration`, que la matrice interdit aujourd'hui : il demandera
son propre ADR.

⚠️ **Deux textes envoient encore l'`Operator` vers « Configuration »**, une entrée que le panneau ne
porte plus :

- `IncompletenessClause.RelationToManifest` — texte gelé par l'ADR-0006 et gardé mot pour mot par
  `IncompletenessClauseTests` : « … c'est vous qui les recensez, à la main, système par système,
  dans « Configuration » ». Le nom est faux, et « système par système » ne décrit plus rien à
  l'écran : le Paramétrage ne recense pas de systèmes. Réécrire la clause est une décision sur ce
  que `Screening` dit de l'incomplétude, pas un renommage ; elle n'est pas prise ici.
- le message d'appel refusé de l'écran d'un dossier (`Case.cshtml`) — « « Configuration » et le
  programme qui la sert ne sont pas d'accord ». Il tombera avec le recâblage, qui change ce qu'un
  refus d'appel veut dire.

**Le contrat d'`Adapter` décrit une vérification qui n'existe plus.**
[`docs/api/adapter.md`](../api/adapter.md) parle encore du `Manifest`, de la sonde au faux secret et
de `VerifyManifestHandler`, dont le lien est mort. Le contrat HTTP lui-même (`system_id`, les quatre
`Capability`) n'a pas bougé.

**Le mot `Manifest` quitte les glossaires, pas le code.** Le glossaire de `Casework` le retire, avec
les entrées « Vérification du `Manifest` » et « Appel au faux secret », dont le code est parti en
#333. Il survit dans environ soixante-dix lignes de `src/` (`manifest` comme nom de
paramètre des gestionnaires, `IAdapterDisagreements`, le journal « Désaccord Manifest/Adapter »,
`IncompletenessClause.RelationToManifest`), dans le glossaire de `Screening` et dans la clause
`Aucune modification vers le Manifest`. Partout, il se lit « l'ensemble des `DeclaredSystem` ». Les
renommer n'est pas fait ici.

**La clause `Aucune modification vers le Manifest` garde son nom et son motif.** Ce qu'elle
protège est désormais l'ensemble des `DeclaredSystem`. Rien ne va non plus de `Screening` vers
`Configuration` : la matrice l'interdit, sans clause de glossaire.

## Ce que cet ADR n'ouvre pas

- **Le déclenchement de l'appel** vers un `EndpointUrl`, son authentification et son secret.
- **La datation par droit** : le `Settings` ne sait pas quand une adresse a été posée.
- **La suppression de `DeclaredSystem`** et de la table `declared_systems`.
- **La réécriture de la clause d'incomplétude** et de `docs/api/adapter.md`, nommées ci-dessus.
