# ADR-0011 — Les internes des moteurs paraissent sur la surface de l'`Operator`, et le contrat HTTP ne bouge pas

- **Statut** : accepté
- **Date** : 2026-08-20
- **Décidé par** : [Carte — La `Qualification` a sa surface](https://github.com/AmauryTISSOT/microservice_rgpd/issues/248), et le ticket des écrits qu'elle a ouvert en premier : [#249](https://github.com/AmauryTISSOT/microservice_rgpd/issues/249)
- **Ne supplante rien.** Ce que le glossaire de `Qualification` disait — *« aucune réponse publique ne la porte »* — n'était pas une décision d'ADR mais une **clause de glossaire, formulée trop largement**. Elle est précisée par ce même ticket, et la précision est écrite ci-dessous.
- **Complète** : [ADR-0010](./0010-un-quatrieme-point-d-entree-la-qualification-a-sa-surface.md), qui ouvre l'écran dont il est question ici. **Les deux décisions se défont séparément** : on peut retirer la porte du panneau sans rien changer à ce que l'écran montre, et l'inverse. C'est pourquoi elles ne sont pas fondues.

## Contexte

Le service qualifie en faisant travailler **deux moteurs** — un LLM et un lexique — et en comparant
leurs deux `QualificationOpinion`. De cette comparaison sort le `ReviewSignal` : `Corroborated`,
`NeedsReview`, `Contested`. C'est l'entrecontrôle, et c'est le cœur du contexte.

**Rien de ce travail ne sort du service.** `QualifyResponse` tait délibérément les avis bruts, la
`DeclaredConfidence` et l'identité des moteurs, et un test de l'endpoint —
`QualificationsPost.NeverPublishesTheRawOpinionsTheDeclaredConfidenceOrTheEngines` — le
garde nommément. Le motif est écrit dans le contrat lui-même : les publier « graverait l'architecture
dans le contrat public et inviterait l'appelant à recalculer, hors de tout test, la règle que le
service tient pour lui ».

Ce motif vaut, et il vaut **contre une application tierce**. L'ADR-0010 ouvre un écran destiné à un
`Operator`, et la question s'y pose autrement : un écran qui affiche `Contested` sans dire **sur quoi
les deux moteurs divergent** demande à un humain de croire une machine qui refuse de s'expliquer.

**Le mot « public » portait deux sens à la fois**, et c'est ce qui rend la question difficile à
trancher sans l'écrire. Le glossaire de `Qualification` disait de
`QualificationEngineIdentity` : *« aucune réponse publique ne la porte »*. Lu tel quel, un lecteur de
bonne foi y voit une interdiction générale d'exposer l'identité d'un moteur **où que ce soit** — y
compris sur un écran interne. Lu dans son intention, il y voit ce que le contrat HTTP tait aux
applications tierces. Ce sont deux règles très différentes, et une seule phrase les portait.

**L'obstacle est aussi mécanique.** Les deux avis, la confiance déclarée, les deux identités de
moteurs et les trois latences existent déjà dans le gestionnaire, en variables locales, et partent
déjà dans l'entrée de trace d'audit. Mais `QualificationOutcome` ne les porte pas : elles meurent au
retour du gestionnaire, et rien au-dessus ne peut les lire.

## Décision

### 1. « Public » veut dire « le contrat HTTP », et rien d'autre

**La clause de non-publication porte sur `QualifyResponse` et sur elle seule.** Ce qu'elle protège est
le contrat des applications tierces : ce qu'un client peut typer, dont il peut dépendre, et dont la
rupture est une rupture. Elle ne dit rien de ce que le service montre à l'`Operator` qui est **devant
lui**.

La distinction est écrite dans le glossaire de `Qualification`, sur l'entrée
`QualificationEngineIdentity`, et **c'est le seul ajout au glossaire de ce ticket** : un écran n'est
pas un concept du domaine, et `CONTEXT.md` refuse d'être la carte de l'interface.

⚠️ **Ce n'est pas un affaiblissement de la clause, c'est sa lecture exacte.** Le garde de l'endpoint
reste vert, mot pour mot, après tout ce qui suit — et c'est précisément lui qui rend l'élargissement
sûr : le jour où quelqu'un laisserait fuir un avis brut sur le fil, le test le dit.

### 2. `QualificationOutcome` s'élargit ; `QualifyResponse` jette

**`QualificationOutcome` porte désormais les deux `QualificationOpinion` et les trois latences** —
par moteur, et totale. Elles ne sont pas calculées pour l'occasion : elles existent déjà dans le
gestionnaire et partent déjà dans la trace d'audit. **Elles cessent simplement d'y mourir.**

⚠️ **Elles sont facultatives, et elles doivent l'être** — l'avis d'un moteur muet est nul, et sa
latence l'est avec lui. Le motif est écrit dans le gestionnaire et ne change pas ici : *« mesurer le
temps qu'il a mis à ne rien rendre ferait passer une panne pour une lenteur »*. En `Mode dégradé`,
l'écran a donc **un** avis et **deux** latences à montrer, et l'absence de l'autre est ce qui dit
lequel des deux moteurs manquait — la même lecture par la nullité que la `Trace d'audit` pratique
déjà. Seule la latence totale est toujours présente.

**`QualifyResponse.From` les jette à la projection.** Le contrat HTTP public est **strictement
inchangé** : pas un champ de plus, pas un champ renommé, pas une contrainte durcie.

C'est la forme qui suit la nature des deux objets. `QualificationOutcome` est un objet **interne** :
ce que le service a produit. `QualifyResponse` est un objet **de frontière** : ce qu'il consent à
dire au dehors. Que l'interne en sache plus que la frontière est l'ordre normal des choses ; c'est
l'inverse qui serait une anomalie.

### 3. Deux raisons distinctes justifient ce que l'écran montre, et elles restent séparées

C'est la clause la plus importante de cet ADR, parce que c'est celle qu'un lecteur pressé fondra.
**Les deux raisons ne se remplacent pas l'une l'autre, et ne tombent pas ensemble.**

**Raison A — expliquer le `ReviewSignal`.** Les **deux avis** et la **`DeclaredConfidence`** sont là
parce que le signal, seul, est une affirmation sans preuve. Afficher `Contested` sans dire sur quoi
les moteurs divergent demande à un humain de croire une machine qui refuse de s'expliquer ; afficher
`NeedsReview` sur un verdict où les deux moteurs s'accordent est **incompréhensible** tant qu'on ne
lit pas la confiance que le moteur principal déclare. C'est exactement le raisonnement que la trace
d'audit tient déjà pour elle-même — elle conserve les avis avec le moteur dont ils relèvent, parce
qu'un verdict sans ses prémisses ne se relit pas. Cette raison tient à l'`Aide à la décision` : un
service qui propose sans jamais décider doit rendre sa proposition **contestable**, et une
proposition qu'on ne peut pas contredire n'est pas une aide.

**Raison B — le diagnostic.** Les **identités de moteurs** et les **trois latences** ne sont là que
pour ça. Elles n'expliquent **rien** du verdict : savoir quel modèle a répondu et en combien de
millisecondes n'aide personne à juger si le texte exerçait un droit d'accès. Elles servent à dire
plus tard *quel moteur a rendu quel verdict*, et à savoir ce que la qualification coûte réellement.

**Pourquoi les garder séparées.** Elles n'ont ni la même durée de vie ni le même sort. Le jour où
l'entrecontrôle changerait de forme — un troisième moteur, une autre règle de corroboration —, la
raison A suit la règle et se réécrit avec elle. La raison B ne bouge pas : un moteur aura toujours un
nom et un temps. Inversement, le jour où le dépôt acquerrait une vraie télémétrie, la raison B
pourrait s'éteindre sur l'écran sans que la raison A perde quoi que ce soit. **Fondues en un seul
motif — « on montre les internes » —, elles tomberaient ensemble, et l'explication du `ReviewSignal`
mourrait d'un argument qui ne la concernait pas.**

C'est aussi ce qui commande la **disposition** de l'écran : le verdict en haut, les internes en bas
sous un dépliant natif, dont le résumé **nomme ce qu'il cache** — « Les deux avis dont ce verdict est
tiré ». Ni « Détails », ni « Avancé » : un dépliant qui ne dit pas ce qu'il cache ne s'ouvre pas.

## Conséquences

- **Un `Operator` peut contredire le service en connaissance de cause.** C'est la posture d'`Aide à
  la décision` rendue opérante plutôt que seulement affirmée.
- **L'architecture du contexte devient visible à qui ouvre le dépliant** : le service dit qu'il a
  deux moteurs, lesquels, et dans quelles versions. C'est assumé — l'écran n'est pas un contrat, et
  personne n'écrit de code contre un `<details>`.
- ⚠️ **Le risque que la clause protégeait n'est pas nul, il est déplacé.** Un `Operator` qui prend
  l'habitude de lire les avis bruts pourrait se mettre à trancher lui-même, avis par avis, au lieu de
  lire le `ReviewSignal` — c'est-à-dire à recalculer de tête la règle que le service tient. La parade
  est la **disposition**, pas le secret : le verdict occupe le haut, les internes sont repliés par
  défaut, et le dépliant dit qu'il porte « les avis dont ce verdict est tiré » — subordonnés, jamais
  concurrents.
- **`QualifyResponse` acquiert une charge d'entretien discrète** : chaque champ neuf de
  `QualificationOutcome` est désormais un champ que la projection doit **délibérément** ne pas
  publier. Le garde de l'endpoint est ce qui rend l'oubli visible ; sans lui, cette décision ne
  serait pas prenable.
- **Le test unitaire du gestionnaire n'est touché que si sa construction du résultat est
  positionnelle.** L'élargissement d'un record n'a pas d'autre effet mécanique.
- **Rien de tout cela n'est nouveau dans le service** : les cinq valeurs circulaient déjà, et partaient
  déjà dans la trace d'audit. Cette décision ne fait pas produire une donnée de plus ; elle en fait
  **survivre** cinq au retour du gestionnaire.

## Alternatives écartées

| Alternative | Motif du rejet |
| --- | --- |
| **Un second chemin de qualification, dédié à l'écran** — un port ou un gestionnaire parallèle rendant un résultat plus riche | **deux chemins qui divergeraient.** C'est la doctrine déjà tenue ailleurs dans le dépôt, où elle s'écrit *« deux formes voudraient dire deux chemins de recherche à maintenir, et l'un des deux finirait par ne plus être celui qu'on croit »* (`Designation.cs`, `OpenCaseRequest.cs`, `DepositForm.cs` — la recherche y est le sujet, le motif est le même) —, et le second chemin est toujours celui qui prend du retard. Ici il aurait pris du retard **sur la règle de corroboration elle-même** : l'écran aurait fini par afficher un `ReviewSignal` calculé autrement que celui de l'API, sur le même texte |
| **L'écran appelant les deux moteurs lui-même**, et comparant les avis dans sa page | deux défauts, et le premier est rédhibitoire : **il ne graverait aucune trace d'audit**, donc une qualification rendue par le service échapperait à ce dont le responsable de traitement doit pouvoir répondre plus tard. Le second : il **redirait la règle de corroboration** hors du gestionnaire qui la tient — une deuxième implémentation d'un `ReviewSignal`, dans une page Razor, sans les tests qui gardent la première |
| **Publier les internes dans `QualifyResponse`** et laisser l'écran lire l'API comme un client | grave l'architecture dans le contrat public, invite l'appelant tiers à recalculer la règle chez lui, et fait de tout changement de moteur une rupture de contrat. C'est très exactement ce que la clause protège, et elle n'est pas rouverte |
| **N'afficher que le `ReviewSignal`**, sans les avis | demande à un humain de croire une machine qui refuse de s'expliquer, et rend `NeedsReview` sur un accord des deux moteurs proprement incompréhensible. C'est le besoin qui a ouvert la question |
| **N'afficher que les avis, sans les identités ni les latences** | tenable — c'est la raison A seule —, mais il aurait fallu **rouvrir le sujet** au premier ticket de support demandant quel moteur avait rendu quel verdict. Les deux raisons sont servies d'un coup, en restant nommées séparément |
| **Fondre les deux raisons en un seul motif** — « l'écran montre les internes » | elles ne tombent pas ensemble, et un motif unique les ferait tomber ensemble. Voir la décision, point 3 |
| **Un ADR unique avec l'ADR-0010** | les deux décisions se défont séparément, et le dépôt supplante **par points nommés** |

## Ce que cet ADR n'ouvre pas

- **Le contrat HTTP**, dans un sens comme dans l'autre. Aucun champ n'y entre, aucun n'en sort.
- **Toute lecture de la trace d'audit** — historique, relecture d'un verdict, adresse par identifiant.
  Les internes paraissent **dans l'échange qui les a produits**, et nulle part ailleurs.
- **Des textes d'exemple, un corpus cliquable, ou l'affichage des droits attendus à côté du verdict.**
  Cette dernière ferait de l'écran une seconde mesure des moteurs, informelle, à côté de celle que les
  tests du sidecar font déjà sur le même corpus — et c'est précisément la finalité que la trace
  d'audit écarte nommément.
- **Une notion de télémétrie exposée.** Les trois latences sont ce que le gestionnaire a mesuré pour
  cet appel, rendues avec lui ; elles ne sont ni agrégées, ni conservées, ni comparées à quoi que ce soit.
- **L'affichage des internes ailleurs dans la surface.** Aucun autre écran n'en montre, et cet ADR
  n'autorise que celui de l'ADR-0010.
- **Les ADR 0001 à 0010**, qui ne sont pas édités.
