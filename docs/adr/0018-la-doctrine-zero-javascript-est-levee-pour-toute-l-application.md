# ADR-0018 — La doctrine « zéro JavaScript » est levée pour toute l'application

- **Statut** : accepté
- **Date** : 2026-09-11
- **Décidé par** : le PRD [#344](https://github.com/AmauryTISSOT/microservice_rgpd/issues/344),
  livré par [#356](https://github.com/AmauryTISSOT/microservice_rgpd/issues/356) à
  [#360](https://github.com/AmauryTISSOT/microservice_rgpd/issues/360) et tracé par
  [#362](https://github.com/AmauryTISSOT/microservice_rgpd/issues/362)
- **Glossaires** : [`CONTEXT-MAP.md`](../../CONTEXT-MAP.md), [Requests](../contexts/requests/CONTEXT.md)
- **Supplante, sur un point** :
  [ADR-0005](./0005-design-language-documente-police-embarquee-et-fichiers-statiques.md) — la
  seconde moitié du motif qui écartait « un framework client ou un pipeline front » : « la surface
  reste rendue par le serveur, sans JavaScript ». La première moitié tient : le service n'a ni
  framework client ni pipeline front, et n'en acquiert pas. Tient aussi la décision 3, qui interdit
  les ressources **tierces** et autorise celles que le service sert lui-même : c'est elle qui couvre
  désormais le module.
- **Supplante, sur deux points** :
  [ADR-0009](./0009-la-navigation-passe-en-panneau-lateral-repliable-sans-javascript.md) — dans sa
  décision 3, la phrase « Le service continue de ne servir aucun JavaScript », avec la clause de
  l'ADR-0005 qu'elle déclarait tenue sans exception ; et le motif de l'alternative « Persistance
  par JavaScript et `localStorage` », qui était de traverser cette clause. Le reste de l'ADR-0009
  tient : le repli est une case à cocher, et il n'a toujours pas une ligne de JavaScript derrière
  lui ; son état ne survit pas à la navigation, par le choix du demandeur.

## Contexte

Depuis l'ADR-0005, la surface de l'`Operator` n'avait pas une ligne de JavaScript. L'ADR-0009 l'a
tenu jusque dans le repli du panneau latéral, une case à cocher et trois règles CSS ; l'ADR-0012,
jusque dans l'écran d'attente du scan, rafraîchi en `<meta http-equiv="refresh">`. Tout ce que le
service faisait tenait dans un aller-retour : un formulaire, un envoi, une page rendue.

Le PRD #344 demande autre chose. L'`Operator` enregistre une demande **sans quitter le tableau** :
un bouton ouvre une modale, la saisie s'y fait, la modale se ferme sur un toast. Et ce qui rend la
modale sûre ne se déclare pas en HTML :

- une saisie modifiée ne se perd jamais par mégarde, quel que soit le mode de fermeture — Annuler,
  la croix, Échap, un clic sur le fond — : il faut comparer la saisie à celle de l'ouverture ;
- « aujourd'hui », valeur par défaut et borne de la date, se recalcule à chaque ouverture, à l'heure
  de Paris : une page restée ouverte au-delà de minuit ne doit ni proposer la veille, ni interdire
  le jour même ;
- un champ en erreur perd son message dès qu'il est corrigé, sans nouvel envoi ;
- « Créer » est désactivé pendant l'envoi, et l'échec d'un envoi laisse la saisie intacte.

Aucun de ces comportements ne se tient sans script. Un aller-retour serveur par fermeture ou par
correction les rendrait tous, mais au prix de la modale elle-même, qui ne survit pas à un
rechargement de page.

## Décision

**La doctrine « zéro JavaScript » est levée pour toute l'application**, et non pour un écran. Un
écran qui a besoin d'un comportement que le HTML ne déclare pas peut charger un script, sans ADR
de plus, dans la forme qui suit.

**La forme est fermée, et c'est elle que cet ADR fixe :**

- **JavaScript vanilla, en modules ES** (`<script type="module">`). Pas de framework, pas de
  bibliothèque, pas de bundler, pas de transpilation, pas de `package.json` : le fichier servi est
  le fichier écrit.
- **Les primitives de la plateforme d'abord.** La modale et sa confirmation sont des `<dialog>`
  natifs : le navigateur en tient le focus, le fond inerte et la touche Échap, que le script n'a
  plus qu'à écouter. L'envoi est un `fetch`, la saisie se lit par `FormData`, « aujourd'hui à
  Paris » se calcule par `Intl.DateTimeFormat` et son `timeZone`.
- **Servi par l'application elle-même**, depuis ses fichiers statiques (`wwwroot/js/`), sous le
  type `text/javascript`. Jamais chez un tiers : c'est la décision 3 de l'ADR-0005, et la clause
  porteuse de l'ADR-0001 derrière elle.
- **Chargé seulement là où un écran en a besoin.** Le layout n'en charge aucun ; il offre une
  section `Scripts`, que seul l'écran concerné remplit. Aujourd'hui, un écran sur douze en charge
  un : le tableau des demandes RGPD, à `/demandes`, avec `requests-board.js`.

**Le serveur rend, le script anime.** La modale, ses champs, leurs valeurs par défaut, la
confirmation et le bandeau d'échec sont rendus par le serveur ; le script les ouvre, les remet à
zéro, les révèle ou les cache, et n'écrit aucun mot de lui-même. Les dix messages d'erreur lui
sont fournis par la page, dans un îlot de données — un `<script type="application/json">`, qui ne
s'exécute pas —, depuis `DataSubjectRequestMessages` : le script porte la logique des règles,
jamais leurs mots.

**Le script n'a pas l'autorité.** Ce qu'il juge, le serveur le rejuge et fait foi : c'est
l'ADR-0019. La validation du navigateur est un confort.

## Ce que chaque supplantation retire, et ce qu'elle laisse

**ADR-0005.** Le motif de rejet d'« un framework client ou un pipeline front » disait deux choses :
le service n'en a pas et n'en acquiert pas ; la surface reste rendue par le serveur, sans
JavaScript. La seconde tombe. La première tient, et elle est même ce qui borne la levée : un module
écrit à la main, servi tel quel, n'est ni un framework ni un pipeline. La surface reste rendue par
le serveur — seulement plus sans JavaScript. La règle que le commentaire de doctrine du layout porte
depuis l'ADR-0005 — aucune ressource tierce, celles que le service sert lui-même autorisées — n'a pas
eu à changer : elle couvrait déjà le module.

**ADR-0009.** La décision 3 disait que le service continuait de ne servir aucun JavaScript, et que
la clause de l'ADR-0005 était tenue « sans exception, et non pas tenue à un script près ». La phrase
ne vaut plus, et le repli qu'elle accompagnait ne bouge pas : il reste une case à cocher, identique
sur tous les écrans, y compris celui qui charge un module. L'alternative « Persistance par
JavaScript et `localStorage` » était écartée parce qu'elle traversait la clause ; ce motif est
tombé. Elle reste écartée, sur le seul motif qui demeure : le demandeur a choisi « CSS pur, sans
persistance ». Rouvrir la persistance ne demande donc plus de lever une doctrine, seulement de
revenir sur ce choix.

## Les options écartées

- **Garder la doctrine, et enregistrer une demande sur une page à part**, par un formulaire posté,
  comme l'ancien dépôt de `Casework`. Tenable, et c'est ce que le service faisait. Mais ni la
  confirmation d'abandon sur les quatre modes de fermeture, ni la revalidation en direct, ni le
  recalcul d'« aujourd'hui » à l'ouverture n'y existent ; et le PRD demande la saisie sans quitter
  le tableau.
- **Une exception pour `/demandes` seulement.** Une dérogation écran par écran se rediscuterait à
  chaque écran, et aucune ne dirait la règle qui compte vraiment : un script ne se charge que là où
  un écran en a besoin. Cette règle-là tient l'effet de l'exception sans en avoir la fragilité.
- **Les commandes déclaratives des navigateurs récents** (`commandfor`, `command`), qui ouvrent une
  modale sans script. Elles ouvrent et ferment ; elles ne comparent pas une saisie, ne recalculent
  pas une date, n'envoient rien sans recharger la page.
- **Un framework ou une bibliothèque** — React, Vue, Alpine, htmx — **ou une chaîne de build** —
  bundler, TypeScript, `npm`. Chacun ajoute une dépendance à tenir à jour, ou un outillage à
  installer avant de construire, pour des besoins que la plateforme couvre déjà : `<dialog>`,
  `fetch`, `FormData`, `Intl`.
- **Un script chargé par un CDN.** Interdit par l'ADR-0005 et l'ADR-0001 : un service qui outille
  le RGPD ne transfère pas lui-même les adresses IP de ses utilisateurs à un tiers.
- **Un script chargé par le layout, sur tous les écrans.** Onze écrans sur douze porteraient un code
  dont ils n'ont pas l'usage, et l'écran qui n'en demande pas en recevrait un.

## Conséquences, y compris celles qui coûtent

⚠️ **Sans JavaScript, « Créer une demande » ne fait rien.** Le formulaire de création n'a pas de
repli : le bouton qui ouvre la modale est un simple bouton, et aucun bouton du formulaire ne le
soumet nativement. C'est un choix du PRD, qui range ce repli hors de son périmètre. Un navigateur
qui refuse les scripts voit le tableau, et ne peut pas y enregistrer une demande.

**La logique des règles de saisie vit désormais en deux langages**, C# et JavaScript, et peut
diverger. Ce qui tient la divergence est écrit dans l'ADR-0019, qui décide de ces règles.

**Tester le script exige un navigateur.** Les tests fonctionnels lisent le HTML rendu et n'exécutent
aucun script : ils ne voient ni un focus, ni une modale ouverte, ni un toast. D'où un projet de tests
de plus, et l'ADR-0020.

**Les assertions « aucun script » sont retirées de cinq tests** — l'accueil, la qualification,
l'écran d'attente du scan, la table de détection, l'export de la cartographie. Une seule assertion
les remplace, sur toute la surface : le module est chargé sur `/demandes`, comme module, servi par
l'application sous le type `text/javascript` ; aucun autre écran ne charge un script. Le contrôle des
ressources tierces du layout est conservé, et il couvre désormais les scripts.

**Le repli du panneau et l'écran d'attente du scan restent sans script.** Rien de ce qui tenait sans
JavaScript n'est réécrit pour en avoir.

**Le glossaire de `Screening` motive les deux liens de l'export**, JSON et CSV, en partie par
« sans JavaScript, un menu n'existe pas ». Cette prémisse est tombée ; le second motif suffit à
garder les deux liens — un lien qu'on colle dans un courriel doit annoncer ce qu'il rend. Le
glossaire n'est pas réécrit ici.

## Ce que cet ADR n'ouvre pas

- **Un script sur les écrans existants.** La levée autorise, elle n'invite pas : aucun écran ne
  gagne un script pour un confort qu'il rendait déjà sans lui.
- **Un framework, une bibliothèque, un bundler, `npm`.** Les introduire rouvrirait cet ADR.
- **Un repli sans JavaScript du formulaire de création.**
- **La persistance du repli du panneau latéral**, qui reste un choix du demandeur (ADR-0009).
- **Les ADR 0001 à 0017**, qui ne sont pas édités. Un ADR acté parle avec les mots de sa date :
  « sans JavaScript » reste écrit dans le titre de l'ADR-0009, et y reste vrai du repli.
