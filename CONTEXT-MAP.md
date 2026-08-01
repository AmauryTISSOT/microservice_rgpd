# Carte des contextes

Ce dépôt porte **deux contextes bornés**. Ils ne se distinguent pas par le sujet — les deux
parlent des droits que le RGPD ouvre aux personnes concernées — mais par le **temps** :
l'un ne connaît que l'instant d'un verdict, l'autre ne connaît que la durée d'une instruction.

Les identifiants du code sont en anglais ; les textes destinés à l'humain — libellés, messages,
documentation d'API — sont en français. La prose française porte les identifiants anglais tels
quels : on écrit « la `Qualification` », « le `Ledger` ».

## Contextes

- [Qualification](./docs/contexts/qualification/CONTEXT.md) — **l'instant du verdict.** Reçoit un
  texte libre français d'une application tierce et dit quels droits il exerce. Ne conserve que
  l'acte de l'avoir qualifié.
- [Casework](./docs/contexts/casework/CONTEXT.md) — **la durée de l'instruction.** Tient le dossier
  d'une demande d'exercice de droits de son arrivée à sa clôture, appelle les systèmes du client
  là où il les atteint, et produit la preuve qu'une procédure a été suivie — y compris là où elle
  ne l'a pas été.

## Relations

- **Noyau partagé — `DataSubjectRight` et lui seul.** La taxonomie fermée de sept valeurs n'est le
  modèle d'aucun des deux contextes : elle est écrite par le RGPD, articles 15 à 21. Les deux
  contextes s'y **conforment**, aucun ne la **possède**. Elle vit donc en dehors des deux, dans
  `src/MicroserviceRgpd.Core/SharedKernel/`, et sa clause de gouvernance est déjà écrite en toutes
  lettres dans [`data-subject-rights.wire.json`](./data-subject-rights.wire.json) : *ajouter une
  valeur est une rupture du contrat public, pas une extension — un événement de niveau ADR.*

  Le noyau partagé vaut par sa **petitesse**. Ce qui n'est pas vrai des deux côtés sans exception
  n'y entre pas : `Capability`, par exemple, est du `Casework` pur et resterait dehors.

- **`Qualification` → `Casework` : fournisseur amont *optionnel*.** Une demande peut arriver déjà
  qualifiée — par un formulaire où la personne coche son droit, ou par un opérateur qui l'atteste.
  Seul le texte libre invoque la qualification. Un `Case` doit donc pouvoir s'ouvrir, s'instruire
  et se clore sans qu'aucune qualification n'ait jamais eu lieu.

- **Separate Ways pour tout le reste.** Rien d'autre ne traverse la frontière. `QualificationOpinion`,
  `WitnessOpinion`, `ReviewSignal`, `DeclaredConfidence`, `Mode dégradé` n'ont aucun sens dans la
  durée. Un `Case` référence un `qualificationId` **opaque**, qu'il ne déréférence jamais. Il n'y a
  donc aucune couche anticorruption : on ne traduit pas un opaque.

- **Aucune dépendance de compilation `Casework` → `Qualification`.** C'est l'optionnalité rendue
  vérifiable : sans cette règle, la promesse « le second contexte se démontre sans GPU » ne serait
  qu'une intention. Elle est gardée par un test au niveau de l'IL — un test de signatures seules
  afficherait vert sur un gestionnaire qui appelle le moteur dans un corps de méthode, c'est-à-dire
  sur la fuite même que l'on craint.

## Langue de système

Un seul terme est vrai des deux côtés de la frontière, et il est écrit ici plutôt que dupliqué
dans les deux glossaires — une doctrine tenue des deux côtés doit être écrite **une fois,
au-dessus**, sinon elle n'est tenue nulle part.

**Aide à la décision** :
La posture du service, sur toute sa durée : il propose, recense, rappelle et prouve ; il ne tranche
jamais. Aucun de ces verbes n'est « décider ». L'issue est toujours le fait d'un humain, nommé et
daté — qu'il s'agisse de valider une `Qualification` ou de clore un `Case`. Une machine ne produit
jamais une issue.
_Avoid_ : décision, arbitrage, verdict automatique, automatisation

⚠️ Cette posture n'emporte **pas** la même économie d'erreur des deux côtés, et c'est le piège que
le découpage rend visible. À gauche, l'erreur est une ligne fausse qu'un humain a sous les yeux :
c'est l'`Erreur relue`, et elle coûte peu. À droite, l'erreur est une ligne manquante que personne
ne verra jamais : c'est l'`Omission silencieuse`, et la relecture n'a aucune prise sur elle. Chaque
régime est défini dans le glossaire du contexte où il vaut, et **nulle part ailleurs**.

## Décisions

- `docs/adr/` — décisions de **système**, valables au-delà d'un seul contexte.
- `docs/contexts/<contexte>/adr/` — décisions propres à un contexte. Aucun n'existe à ce jour.

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
`Core/Casework/`, `Core/SharedKernel/`.
