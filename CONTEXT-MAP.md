# Carte des contextes

Ce dépôt porte **trois contextes bornés**, et ils se distinguent par le **temps** : l'un ne connaît
que l'instant d'un verdict, l'autre que la durée d'une instruction, le troisième que le temps
d'**avant** — celui où aucune demande n'existe encore.

Les deux premiers parlent du même sujet, les droits que le RGPD ouvre aux personnes concernées. Le
troisième n'en parle pas du tout : il regarde le paysage de données du client avant que quiconque ne
réclame quoi que ce soit. C'est pourquoi il ne partage rien avec eux — voir *Relations*.

Les identifiants du code sont en anglais ; les textes destinés à l'humain — libellés, messages,
documentation d'API — sont en français. La prose française porte les identifiants anglais tels
quels : on écrit « la `Qualification` », « le `EvidenceLog` ».

## Contextes

- [Qualification](./docs/contexts/qualification/CONTEXT.md) — **l'instant du verdict.** Reçoit un
  texte libre français d'une application tierce et dit quels droits il exerce. Ne conserve que
  l'acte de l'avoir qualifié.
- [Casework](./docs/contexts/casework/CONTEXT.md) — **la durée de l'instruction.** Tient le dossier
  d'une demande d'exercice de droits de son arrivée à sa clôture, appelle les systèmes du client
  là où il les atteint, et produit la preuve qu'une procédure a été suivie — y compris là où elle
  ne l'a pas été.
- [Screening](./docs/contexts/screening/CONTEXT.md) — **le temps d'avant.** Détecte, dans le relevé
  des colonnes d'une base du client qu'un `Operator` lui colle, les colonnes qui portent
  vraisemblablement des données personnelles, et les lui rend une par une pour qu'il les retienne ou
  les écarte. C'est la **détection des données personnelles**, et ce qu'elle rend est un **rapport de
  détection**. Il ne se connecte à rien, ne lit aucune valeur, et ne touche jamais au `Manifest`.

## Relations

- **Noyau partagé — `DataSubjectRight` et lui seul, et il ne lie que deux contextes sur trois.** La
  taxonomie fermée de sept valeurs n'est le modèle ni de `Qualification` ni de `Casework` : elle est
  écrite par le RGPD, articles 15 à 21. Les deux s'y **conforment**, aucun ne la **possède**. Elle
  vit donc en dehors des deux, dans
  `src/MicroserviceRgpd.Core/SharedKernel/`, et sa clause de gouvernance est déjà écrite en toutes
  lettres dans [`data-subject-rights.wire.json`](./data-subject-rights.wire.json) : *ajouter une
  valeur est une rupture du contrat public, pas une extension — un événement de niveau ADR.*

  Le noyau partagé vaut par sa **petitesse**. Ce qui n'est pas vrai partout sans exception n'y entre
  pas : `Capability`, par exemple, est du `Casework` pur et resterait dehors ; et l'`Operator` de
  `Screening` porte le même mot que celui de `Casework` **sans partager aucun type** — l'identité de
  mot n'est pas une identité de modèle, et factoriser sur elle serait la première fissure.

  ⚠️ `Screening` n'y touche pas. Il ne rattache **jamais** une colonne à un `DataSubjectRight` : une
  colonne « courriel » ne relève pas d'un droit plutôt qu'un autre, elle relève de tous. L'y
  raccrocher ferait peser une troisième dépendance sur le noyau sans rien apporter — sa taxonomie à
  lui, `PersonalDataCategory`, lui appartient en propre et n'a pas le RGPD pour auteur.

- **`Qualification` → `Casework` : fournisseur amont *optionnel*.** Une demande peut arriver déjà
  qualifiée — par un formulaire où la personne coche son droit, ou par un opérateur qui l'atteste.
  Seul le texte libre invoque la qualification. Un `Case` doit donc pouvoir s'ouvrir, s'instruire
  et se clore sans qu'aucune qualification n'ait jamais eu lieu.

- **`Screening` : `Separate Ways` intégral, et aucun noyau partagé du tout.** C'est le seul contexte
  du dépôt qui n'a **aucune** intersection avec les autres : pas de noyau partagé, pas de fournisseur
  amont, pas même un identifiant opaque qui traverserait comme le `qualificationId` que porte un
  `Case`. Il vit avant, il ne connaît aucune demande, et rien de ce qu'il produit ne descend nulle
  part. En particulier, **rien ne va du `Screening` au `Manifest`** — c'est la clause `Aucune modification vers
  le Manifest`, écrite dans son glossaire : un `Manifest` pré-rempli par une machine se lirait comme
  complet, ce qui est l'`Omission silencieuse` sous sa forme la plus dangereuse.

- **Aucune dépendance de compilation entre `Screening` et les deux autres, dans les deux sens.** Le
  garde de `tests/MicroserviceRgpd.ArchitectureTests/` s'étend, avec la même lecture au niveau de
  l'IL. ⚠️ Ce qui rend cette règle tenable est justement l'absence d'intersection : il n'y a rien à
  factoriser, donc rien à négocier — à la différence de `Casework → Qualification`, où la règle doit
  résister à une tentation réelle.

- **Separate Ways pour tout le reste.** Rien d'autre ne traverse la frontière. `QualificationOpinion`,
  `LexiconOpinion`, `ReviewSignal`, `DeclaredConfidence`, `Mode dégradé` n'ont aucun sens dans la
  durée. Un `Case` référence un `qualificationId` **opaque**, qu'il ne déréférence jamais. Il n'y a
  donc aucune couche anticorruption : on ne traduit pas un opaque.

- **Aucune dépendance de compilation `Casework` → `Qualification`.** C'est l'optionnalité rendue
  vérifiable : sans cette règle, la promesse « le second contexte se démontre sans GPU » ne serait
  qu'une intention. Elle est gardée par un test du code compilé — un test de signatures seules
  afficherait vert sur un gestionnaire qui appelle le moteur dans un corps de méthode, c'est-à-dire
  sur la fuite même que l'on craint. Le garde vit dans `tests/MicroserviceRgpd.ArchitectureTests/`,
  et il est posé **avant** le contexte qu'il garde : la première ligne de `Casework` naîtra déjà
  sous surveillance.

## Langue de système

Un seul terme est vrai des trois côtés à la fois, et il est écrit ici plutôt que dupliqué dans les
trois glossaires — une doctrine tenue partout doit être écrite **une fois, au-dessus**, sinon elle
n'est tenue nulle part.

**Aide à la décision** :
La posture du service, sur toute sa durée : il propose, recense, rappelle et prouve ; il ne tranche
jamais. Aucun de ces verbes n'est « décider ». L'issue est toujours le fait d'un humain, nommé et
daté — qu'il s'agisse de valider une `Qualification`, de clore un `Case` ou de retenir une
`ScreenedColumn`. Une machine ne produit jamais une issue.
_Avoid_ : décision, arbitrage, verdict automatique, automatisation
⚠️ **Ce que cette liste interdit est de nommer une issue que la _machine_ produirait**, jamais de
nommer le geste d'un humain. `Case.Arbitrate` et l'écran d'arbitrage d'une réserve de `Locate` sont
donc légitimes, et le mot y est repris sciemment : ils ne nomment que l'`Operator` tranchant, nommé
et daté — c'est-à-dire très exactement ce que la posture exige, et non ce qu'elle refuse. Un
`ArbitrationEngine`, un « arbitrage automatique » ou un seuil qui trancherait tomberaient, eux,
sous la liste.

⚠️ Cette posture n'emporte **pas** la même économie d'erreur d'un contexte à l'autre, et c'est le
piège que le découpage rend visible. Chez `Qualification`, l'erreur est une ligne fausse qu'un humain
a sous les yeux : c'est l'`Erreur relue`, et elle coûte peu. Chez `Casework`, l'erreur est une ligne
manquante que personne ne verra jamais : c'est l'`Omission silencieuse`, et la relecture n'a aucune
prise sur elle. Chez `Screening`, l'erreur qui coûte est bien l'omission — mais elle est **relisible**,
et seulement parce que le rapport de détection rend **toutes** les colonnes du relevé, y compris
celles où rien n'a été vu : c'est l'`Omission relue`, et elle cesse d'exister le jour où quelqu'un
filtre l'affichage. Chaque régime est défini dans le glossaire du contexte où il vaut, et **nulle
part ailleurs**.

## Décisions

- `docs/adr/` — décisions de **système**, valables au-delà d'un seul contexte.
- `docs/contexts/<contexte>/adr/` — décisions propres à un contexte. Aucun n'existe à ce jour.

Six ADR de système sont en vigueur, et aucun n'en supplante un autre :

- [ADR-0001](./docs/adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md) — l'architecture
  polyglotte et le moteur auto-hébergé.
- [ADR-0002](./docs/adr/0002-deux-contextes-bornes-et-noyau-partage.md) — le découpage par le temps
  et le noyau partagé d'un seul type. ⚠️ Son titre dit « deux contextes » : il est **daté, pas
  faux** — il décidait de ce qui existait alors, et ses décisions restent en vigueur.
- [ADR-0003](./docs/adr/0003-troisieme-contexte-sans-intersection-et-garde-des-traversees.md) — le
  troisième contexte borné, son absence totale d'intersection, et le garde énoncé en **liste
  blanche** : rien ne traverse d'un contexte à l'autre sauf les deux traversées vers le noyau
  partagé, écrites en toutes lettres. Un quatrième contexte naîtrait donc interdit partout, et son
  `CONTEXT.md` ne peut pas entrer sans que quelqu'un écrive sa ligne.
- [ADR-0004](./docs/adr/0004-moteur-de-depistage-en-csharp-sans-second-sidecar.md) — le moteur de la
  détection des données personnelles vit en C# dans `Infrastructure`, sans second sidecar Python.
  ⚠️ Son nom de fichier et son titre portent le mot que l'interface n'emploie plus : ils sont
  **datés, pas faux** — un ADR acté parle avec les mots de sa date, et on ne le réécrit pas.
- [ADR-0005](./docs/adr/0005-design-language-documente-police-embarquee-et-fichiers-statiques.md) —
  le design language documenté est adopté, sa police est embarquée, et le service ouvre ses fichiers
  statiques.
- [ADR-0006](./docs/adr/0006-trois-points-d-entree-renommes-en-francais-identifiants-inchanges.md) —
  les trois points d'entrée sont renommés dans la langue humaine — « Configuration du microservice
  RGPD », « Détection des données personnelles », « Tableau des demandes RGPD » — et les
  identifiants C# ne bougent pas.

⚠️ [ADR-0001](./docs/adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md) **précède le
découpage** : il a été écrit quand le dépôt n'avait qu'un contexte. Il se lit comme un ADR de
système, sa clause porteuse pour `Casework` étant l'auto-hébergement intégral et le refus de toute
sous-traitance au sens de l'art. 28 — sur lesquels tout le reste s'appuie. Il n'a pas été réécrit :
on supplante un ADR, on ne l'édite pas.

## Où vivent les contextes dans le code

`src/` est découpé **par couche** (Clean Architecture : `Core`, `UseCases`, `Infrastructure`, `Web`),
et chaque contexte **traverse** les quatre. Il n'existe donc pas de `src/<contexte>/` où poser un
`CONTEXT.md` — d'où `docs/contexts/<contexte>/`, qui s'écarte sciemment du layout multi-contexte
générique. À l'intérieur des couches, les contextes se lisent au dossier : `Core/Qualifications/`,
`Core/Casework/`, `Core/Screenings/`, `Core/SharedKernel/`.

⚠️ **Deux de ces dossiers sont au pluriel, et pour la même raison mécanique** : un type `Qualification`
dans un espace de noms `Qualification`, un type `Screening` dans un espace de noms `Screening`, sont
un piège de résolution de noms en C# — le compilateur doit départager le type et l'espace de noms à
chaque usage, et il ne le fait pas partout de la même façon. Le dossier prend donc le pluriel là où le
contexte a un type qui porte son nom, et le garde d'ADR-0003 lit l'appartenance **par préfixe** pour
que ce pluriel ne lui échappe pas. `Casework` et `SharedKernel` restent au singulier : aucun type ne
porte ces noms-là.
