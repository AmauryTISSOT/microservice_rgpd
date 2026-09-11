# ADR-0020 — Playwright (.NET) pour les tests navigateur

- **Statut** : accepté
- **Date** : 2026-09-11
- **Décidé par** : le PRD [#344](https://github.com/AmauryTISSOT/microservice_rgpd/issues/344),
  livré par [#348](https://github.com/AmauryTISSOT/microservice_rgpd/issues/348) et tracé par
  [#362](https://github.com/AmauryTISSOT/microservice_rgpd/issues/362)
- **Complète** : [ADR-0018](./0018-la-doctrine-zero-javascript-est-levee-pour-toute-l-application.md),
  dont le script ne se vérifie que dans un navigateur
- **Ne supplante aucun ADR.** Aucun ADR ne portait sur la manière de tester ; la doctrine de test du
  dépôt est écrite dans `docs/testing/`, et elle gagne une section.

## Contexte

Tant que la surface n'avait pas de JavaScript, tout ce que l'`Operator` voyait était dans le HTML
que le serveur rendait. Les tests fonctionnels le lisaient là : `CustomWebApplicationFactory`, le
serveur en mémoire d'ASP.NET Core, un vrai PostgreSQL en conteneur, et le corps de la réponse.

L'ADR-0018 met de la logique dans le navigateur, et ce que cette logique produit n'est dans aucune
réponse du serveur : une modale ouverte, un focus posé sur le premier champ en erreur, un message
qui disparaît quand le champ est corrigé, un bouton désactivé pendant l'envoi, un toast qui apparaît
puis s'en va, la confirmation qu'ouvre la touche Échap. Le serveur en mémoire n'exécute aucun
script, et aucun navigateur ne sait l'atteindre.

Le dépôt n'avait aucun test navigateur. Ce qui suit en établit l'antériorité.

## Décision

**Les tests navigateur sont écrits en C#, avec xUnit, et pilotent Playwright pour .NET**
(`Microsoft.Playwright`). Même langage, même exécuteur, mêmes assertions que le reste du dépôt :
`dotnet test` les lance avec les autres.

**Ils vivent dans leur propre projet**, `tests/MicroserviceRgpd.BrowserTests`, ajouté à la solution.

**Le service tourne tel qu'il tourne en vrai**, sous Kestrel, sur un port que l'OS attribue :
`WebApplicationFactory` avec `UseKestrel(0)`. Deux suites qui tournent ensemble ne se disputent aucun
port. La base est un vrai PostgreSQL, démarré par Testcontainers, migrations appliquées, comme dans
les tests fonctionnels. Rien n'est substitué tant qu'aucun parcours n'approche un moteur ; le jour
où l'un en exigera une doublure, elle se posera sur le port du domaine, comme dans
`CustomWebApplicationFactory`.

**Chromium est le seul navigateur**, sans tête.

**Chromium est installé par la fixture elle-même**, en code, par le pilote que le paquet copie à côté
des binaires. Ni `pwsh`, ni commande à lancer à la main avant `dotnet test` : le seul prérequis est
Docker, comme pour les autres suites. Le premier lancement télécharge le navigateur ; les suivants
le trouvent en place.

**Un seul harnais pour toute la suite** — un conteneur, un service, un Chromium —, partagé par une
fixture de collection. Chaque test ouvre son propre contexte de navigation, avec ses cookies et son
stockage, et le ferme en sortant.

**Les tests lisent l'écran comme l'`Operator` le lit** : par le rôle et le nom accessible d'un
lien, d'un titre, d'un bouton, d'un champ ; jamais par une classe CSS, jamais par la forme du DOM.
Ce qu'ils relisent en base, ils le relisent en SQL, comme les tests fonctionnels.

**Le navigateur est la troisième couture, et elle ne remplace pas les deux autres.** Les règles se
vérifient dans le domaine, par des tests unitaires de la fabrique ; le refus et la persistance, à la
couture HTTP. Le navigateur ne vérifie que ce que seul un navigateur montre.

## Les options écartées

- **Playwright pour Node, en TypeScript.** C'est la version la plus répandue. Elle aurait fait
  entrer dans le dépôt un second langage de test, `npm`, un `package.json` et un `node_modules`
  — l'outillage même que l'ADR-0018 refuse au produit — et un second exécuteur à lancer à côté de
  `dotnet test`.
- **Cypress.** Même objection, et un seul langage possible : JavaScript.
- **Selenium WebDriver.** Il exige de tenir un pilote accordé à la version du navigateur, et
  n'attend pas de lui-même qu'un élément soit prêt : chaque attente se code à la main, et chacune
  oubliée devient un test instable.
- **Des tests unitaires du module**, dans un DOM simulé comme jsdom. Ce n'est pas un navigateur : le
  `<dialog>`, l'événement `cancel`, le focus, `validity.badInput` sur un champ de date y sont absents
  ou imités — c'est-à-dire tout ce qu'il faut vérifier. Et il aurait fallu `npm` pour les lancer.
- **Le serveur en mémoire des tests fonctionnels.** Aucun navigateur ne sait l'atteindre.
- **L'`AppHost` d'Aspire.** Il démarrerait aussi le sidecar de qualification et le conteneur Ollama,
  qui exige un GPU : c'est la raison pour laquelle `AspireTests` est vide, et elle vaut ici.
- **Firefox et WebKit en plus de Chromium.** Trois fois le temps, trois fois le téléchargement, pour
  des comportements que la plateforme normalise déjà : `<dialog>`, `fetch`, `Intl`.
- **L'installation par `playwright.ps1`**, le chemin que la documentation de Playwright .NET
  indique. Elle exige PowerShell sur chaque poste, qu'un poste Linux n'a pas.

## Conséquences, y compris celles qui coûtent

⚠️ **Seul Chromium est vérifié.** Un comportement propre à Firefox ou à Safari — l'affichage d'un
champ de date, le rendu d'une modale — n'est vu par aucun test. Le service ne déclare aucune liste
de navigateurs pris en charge, et n'en déclare pas ici.

⚠️ **Le téléchargement n'apporte pas les bibliothèques système dont Chromium dépend.** Un poste qui
fait déjà tourner un navigateur les a ; une image Linux minimale peut ne pas les avoir, et le
lancement y échoue. La fixture ne les installe pas, faute des droits nécessaires : il faut les poser
une fois, par le gestionnaire de paquets du système. C'est écrit dans `docs/testing/testcontainers.md`.

**La suite s'allonge**, du démarrage d'un navigateur et du coût de chaque parcours. C'est ce qui
borne la troisième couture à ce que seul un navigateur montre : une règle vérifiée dans le navigateur
qui l'est déjà dans le domaine serait payée deux fois.

**La version du navigateur suit celle du paquet.** Mettre à jour `Microsoft.Playwright` dans
`Directory.Packages.props` change le Chromium qu'installe la fixture ; les deux ne se choisissent pas
séparément.

**Le premier lancement télécharge Chromium** dans le cache de Playwright (`~/.cache/ms-playwright`
sous Linux). Hors ligne, la première fois, la suite ne démarre pas.

## Ce que cet ADR n'ouvre pas

- **Un navigateur de plus.** Ajouter Firefox ou WebKit rouvrirait la question du temps et du
  téléchargement, pas celle de l'outil.
- **L'intégration continue.** Le dépôt n'en a pas ; la suite tourne par `dotnet test` sur le poste
  de développement.
- **Des tests visuels**, par comparaison de captures. L'ADR-0005 écarte déjà toute assertion sur une
  valeur de design ; un navigateur ne la rend pas plus pertinente.
- **Une couverture navigateur des écrans sans script.** Leur contenu se lit dans le HTML rendu, et
  les tests fonctionnels le lisent déjà. Le premier parcours — du panneau latéral au tableau des
  demandes — a servi à établir le harnais avant que le module n'existe ; il n'annonce pas les
  autres écrans.
