# ADR-0007 — Le cadre partagé des écrans se nomme `Layout`, la barre se nomme `Navigation`, et le mot `Chrome` quitte le dépôt

- **Statut** : accepté
- **Date** : 2026-08-17
- **Décidé par** : une session de conception avec le demandeur, **sans issue d'accompagnement** — la spécification du renommage est portée par cet ADR lui-même, ci-dessous
- **Supplante, sur un point** : [ADR-0006](./0006-trois-points-d-entree-renommes-en-francais-identifiants-inchanges.md), qui rangeait « le mot `Chrome` de `ChromeNavigation` » parmi ce que sa décision **n'ouvrait pas**. Cette clause ne vaut plus. Tout le reste de l'ADR-0006 reste en vigueur, et son texte n'a pas été édité — on supplante un ADR, on ne le réécrit pas.

## Contexte

Le cadre fixe que portent les douze écrans de la surface s'appelait `Chrome` dans le code : le type
`ChromeNavigation` et son `ChromeEntryPoint` côté `Web`, le harnais `ChromeSurface` et les tests
`SharedChrome` côté fonctionnel, le dossier et l'espace de noms `FunctionalTests.Chrome`, et les
classes CSS `nav.chrome` et `.chrome-version`.

Le mot venait du vocabulaire du design, où il est juste et bien établi : le *chrome* d'une interface
est ce qui entoure le contenu. Mais dans **ce** dépôt il est homographe d'autre chose, et pas
d'une hypothèse — `scripts/run-project.sh` cherche `google-chrome`, `google-chrome-stable`, `chromium`
pour ouvrir le service dans un navigateur. Le même dépôt écrivait donc `nav.chrome` et
`google-chrome` en voulant dire deux choses sans rapport.

Deux constats ont orienté la sortie plutôt qu'un simple remplacement mot pour mot.

**Le mot désignait deux granularités à la fois.** `ChromeNavigation`, `nav.chrome` et
`.chrome-version` nommaient **la barre**. `ChromeSurface` et `SharedChrome` nommaient **le cadre
partagé tout entier** — la feuille de style et la police que le service sert lui-même, la balise
`<main>`, l'absence de pied de page et de lien d'évitement, et la barre parmi eux. Un seul mot pour
les deux rendait invisible que l'un contient l'autre.

**Le dépôt avait déjà nommé ce cadre, et il ne s'en servait pas pour ses fichiers.** Le mot
« layout » était déjà écrit en prose à huit endroits, comme nom du concept et non du fichier Razor :
« Le chrome n'appartient ni au `Casework` ni au `Screening` : **il est du layout** »
(`ChromeSurface`), « écrit une fois dans le **layout partagé** » (`SharedChrome`, deux fois),
« L'accueil ne relève ni du `Casework` ni du `Screening` : **il est du layout** » (`Doorstep`, dans
le test comme dans le gabarit), et jusque dans l'ADR-0005 — « une soixantaine de règles CSS écrites
à même **le layout partagé** ».

## Décision

`Chrome` disparaît du dépôt, remplacé par **deux** noms qui séparent les deux granularités :

| Avant | Après | Ce que le nom désigne |
| --- | --- | --- |
| `ChromeNavigation` | `Navigation` | la barre |
| `ChromeEntryPoint` | `EntryPoint` | un des trois points d'entrée |
| `<nav class="chrome">` | `<nav>`, sans classe | — |
| `.chrome-version` | `.version` | la version du produit, dans la barre |
| `ChromeSurface` | `LayoutSurface` | le porte-parole HTTP du harnais |
| `SharedChrome` | `SharedLayout` | les tests du layout partagé |
| `tests/…/Chrome/` | `tests/…/Layout/` | le dossier et l'espace de noms |

`Layout` entre au glossaire de système de `CONTEXT-MAP.md`, avec `chrome`, `habillage`, `shell`,
`enveloppe` et `cadre` en `_Avoid_`.

**La prose ne gagne aucun mot neuf.** Elle perd « chrome » là où il apparaissait, et « layout » et
« la barre » — déjà les termes employés — prennent la place. Aucun terme français n'est introduit :
« habillage » aurait été un second nom pour la chose au moment même où l'on en supprime un.

**Trois zones ne sont pas touchées**, et ne sont pas des restes à balayer :

- `docs/design/DESIGN.md`, où *chrome* porte le sens du designer, en anglais, à propos du langage de
  design de Notion. C'est le sens où le mot est juste, et le renommer couperait le lien avec la
  littérature dont ce document est l'analyse.
- l'ADR-0005 et l'ADR-0006, comptes-rendus datés. Un ADR acté parle avec les mots de sa date.
- `scripts/run-project.sh`, où `google-chrome` désigne le navigateur — c'est-à-dire la raison même de
  cet ADR.

## Justification

**`Layout` n'est pas un mot choisi, c'est le mot trouvé.** Les huit occurrences en prose montrent que
la décision de nommer ce cadre « layout » était déjà prise ; seuls les fichiers ne l'avaient pas
suivie. Introduire un troisième mot — `Shell`, `Furniture`, `Bezel` — aurait ajouté un synonyme à
l'endroit exact où l'on en retire un.

**La séparation des deux noms est asymétrique, et c'est ce qui la rend juste.** La barre est une
partie du layout ; le layout n'est pas une sorte de barre. Nommer le tout `Navigation` aurait fait
atterrir les assertions de police, de feuille de style et de « aucune ressource tierce » dans un
fichier qui prétend parler de liens — et ce sont les plus importantes du dossier : celle qui garde
qu'aucune adresse IP d'utilisateur ne fuite vers un hébergeur de polices. Dans l'autre sens la
relation tient : `SharedLayout` porte légitimement des assertions de barre, parce que la barre est du
layout.

**`Layout` couvre un troisième cas que tout autre candidat ratait.** Le dossier de test porte aussi
`Doorstep.cs`, l'écran d'accueil, dont le code dit deux fois « il est du layout ». Un écran entier
n'est pas un « shell » ni un « habillage » ; il est du layout au même titre que la barre.

**La classe CSS de `nav` disparaît au lieu d'être renommée.** `<nav class="navigation">` aurait été
une tautologie : l'élément dit déjà ce que la classe répète. Il n'y a qu'un `<nav>` dans la surface,
donc le sélecteur `nav` suffit — et c'est une occurrence de moins à renommer la prochaine fois.

**La classe de la version, elle, est conservée sous le nom `.version`.** Elle est porteuse : le
harnais reconnaît l'élément de version **à sa classe, comme un mot** de la liste, et c'est ce qui
permet au balayage « aucun chiffre dans la barre » de retirer l'exception au lieu de la tolérer. La
supprimer aurait forcé le test à viser un `<span>` nu, plus fragile.

**Aucun piège de résolution de noms n'est ouvert.** `CONTEXT-MAP.md` consigne qu'un type portant le
nom de son espace de noms est un piège en C# — d'où `Core/Qualifications/` au pluriel. Ici, aucun
type ne s'appelle `Layout` : l'espace de noms `FunctionalTests.Layout` contient `LayoutSurface`,
`SharedLayout` et `Doorstep`, et aucun d'eux ne porte le nom de l'espace de noms qui les tient.
Et `Layout = "_Layout"` n'existe que dans `_ViewStart.cshtml`, une propriété Razor
que rien ne masque, puisque le type renommé côté `Web` est `Navigation`.

## Conséquences

- **`CONTEXT-MAP.md` cesse de dire qu'aucun ADR n'en supplante un autre.** Sa section « Décisions »
  l'affirmait pour six ADR ; elle en compte sept, et nomme le seul point supplanté.
- **Une phrase du harnais devient tautologique et est corrigée.** « Le chrome n'appartient ni au
  `Casework` ni au `Screening` : il est du layout » perd sa seconde moitié, qui répétait le sujet.
- **Les fixtures du harnais suivent le mot.** Elles se nommaient d'après lui — `chrome@example.fr`,
  un identifiant de système préfixé `chrome-`, un libellé « Le système du chrome » — et disent
  désormais `layout`.
- **Le style de la barre s'attache désormais à l'élément `nav` nu, et un garde le rend sûr.** Un
  second `nav` posé un jour — une pagination, un fil d'Ariane — hériterait du fond blanc et du
  liseré de la barre, et le harnais, qui lit le premier `nav` du document, se mettrait à lire la
  mauvaise barre **sans échouer**. `NavigationBarIn` vérifie donc le **compte** et non la seule
  présence : exactement un `nav` par écran, sur le modèle de ce que l'élément de version fait déjà.
  C'était la seule façon dont ce renommage pouvait se retourner en silence.
- **La classe CSS `.chrome-version` était publique par construction** : servie dans le HTML de tous
  les écrans. Rien à l'extérieur du dépôt ne s'y accroche aujourd'hui, mais la renommer est le genre
  de geste qui coûte cher une fois qu'un tiers s'y accroche — c'est une des raisons pour lesquelles
  ce renommage est fait maintenant plutôt que plus tard.
- **Ce que cet ADR ne règle pas** : `SharedLayout` porte deux tests d'**adressage**
  (`RetiresTheSixFormerScreeningAddressesWithoutRedirecting`,
  `ServesTheSixScreeningScreensUnderTheirNewPrefix`) qui ne relèvent ni du layout ni de la barre. Le
  défaut préexiste au renommage, aucun nom ne l'aurait résolu, et il n'est pas ouvert ici.

## Alternatives écartées

- **Ne rien changer.** Le mot était défendable et l'ADR-0006 l'avait laissé en place. Écarté parce
  que la collision n'est pas théorique : le dépôt nomme le vrai navigateur, dans un script livré.
- **`Navigation` partout, un seul mot.** Écarté : le nom aurait menti pour dix des seize tests de
  `SharedChrome`, dont celui qui garde la promesse RGPD sur les ressources tierces.
- **`Shell`, `Furniture`, `Bezel`, `Surround`.** Écartés : tous trois auraient été un mot neuf pour
  un concept que le dépôt nommait déjà « layout », et aucun n'aurait pu couvrir l'écran d'accueil.
- **« Habillage » comme terme français.** Écarté : le dépôt a déjà naturalisé « layout » dans sa
  prose française, comme il l'a fait pour `Operator` et `Casework`. Deux noms pour une chose est le
  défaut que cet ADR corrige.
- **`Envelope` / « enveloppe ».** Écarté d'office : `TransportEnvelope` nomme déjà l'emballage HTTP
  d'un transport de `Casework`.
- **Éditer l'ADR-0006 en place** plutôt que le supplanter. Écarté : cela effacerait la trace du
  revirement, et le dépôt tient déjà la règle — on supplante un ADR, on ne l'édite pas.
- **Scinder les fichiers de test** pour que chaque nom ne couvre qu'un concept. Écarté comme hors
  périmètre : c'est une refonte des tests, pas un renommage, et elle mérite sa propre décision.

## Portée de cet ADR

Cet ADR décide **un vocabulaire et les identifiants qui le portent**, à comportement constant. Aucun
écran, aucune règle de style, aucune assertion ne change de sens ; la suite fonctionnelle du layout
passe avant comme après. Il ne décide rien sur l'organisation des fichiers de test, ni sur le
vocabulaire français des clauses de doctrine, ni sur le mot `Operator` — que l'ADR-0006 avait rangés
avec `Chrome` dans ce qu'il n'ouvrait pas, et qui y restent.
