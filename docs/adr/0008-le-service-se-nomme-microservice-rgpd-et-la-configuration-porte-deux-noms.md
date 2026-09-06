# ADR-0008 — Le service se nomme « Microservice RGPD », et l'écran de configuration porte deux noms

- **Statut** : accepté
- **Date** : 2026-08-17
- **Décidé par** : une session de conception avec le demandeur, sans issue d'accompagnement — la spécification est portée par cet ADR lui-même
- **Supplante, sur deux points** : [ADR-0006](./0006-trois-points-d-entree-renommes-en-francais-identifiants-inchanges.md), dont la réserve n° 1 tenait le wordmark métier pour le contrepoids qui autorisait « microservice » dans la barre, et dont la règle « un seul nom, aucune forme courte » interdisait à un écran d'être nommé deux fois. Ces deux clauses ne valent plus. Tout le reste de l'ADR-0006 reste en vigueur, et son texte n'a pas été édité — on supplante un ADR, on ne le réécrit pas.

## Contexte

La barre de navigation portait, devant ses trois entrées, le wordmark « Droits des personnes
concernées ». Ce n'était pas un nom mais une description de ce que le service fait, et l'accueil la
reprenait en `<h1>`. Un utilisateur qui lançait l'application y lisait donc une phrase, là où il
cherchait le nom de ce qu'il venait d'ouvrir.

L'ADR-0006 avait vu venir la question par l'autre bout. Sa réserve n° 1 notait que le mot
« microservice », entré dans la langue de l'`Operator` par l'entrée « Configuration du microservice
RGPD », était un mot d'architecture qui faisait lire à l'utilisateur le nom du dépôt — et elle
l'acceptait parce que le wordmark, lui, parlait la langue métier. Le contrepoids était nommé. Le
retirer était donc une décision, pas un ajustement.

## Décision

Le service se nomme `Microservice RGPD`. La capitale à **M**icroservice distingue le nom propre du
produit du nom commun « microservice RGPD » que portent les phrases du domaine. Le nom vit en une
seule constante, `Navigation.ServiceName`, d'où il alimente les trois endroits qui le disent : le
wordmark de la barre, le `<h1>` de l'accueil, et la moitié droite du titre d'onglet de tous les
écrans (`Accueil — Microservice RGPD`).

Le changement de registre est assumé. « Droits des personnes concernées » disait ce que le service
*fait* ; « Microservice RGPD » dit ce qu'il *est*. C'est le renversement de la réserve n° 1, et il a
été mis au demandeur en ces termes avant d'être pris.

L'écran de configuration porte désormais deux noms :

| Où | Nom | Pourquoi |
| --- | --- | --- |
| la barre | `Configuration` | le wordmark « Microservice RGPD » la précède de quinze centimètres ; la forme pleine y répétait le nom du service à côté de lui-même |
| la carte de l'accueil, le `<h1>`, le titre d'onglet | `Configuration du microservice RGPD` | rien ne précède une carte ni un titre |
| les liens et phrases de navigation | `Configuration` | ils décrivent un geste : ils envoient l'`Operator` cliquer dans la barre, et le nom qu'il doit y reconnaître est celui qui y est écrit |

Le type `EntryPoint` porte donc deux champs de texte — `NavigationLabel` et `DoorwayName` — au lieu
d'un `Label`. Les deux autres entrées répètent la même chaîne dans les deux champs, et cette
répétition est la forme normale : un champ vide ou nul aurait fait de « la barre reprend le nom de la
carte » une règle implicite qu'aucun lecteur ne verrait.

`Tableau des demandes RGPD` garde son `RGPD`, et ce n'est pas une exception à la règle qui vient de
raccourcir l'autre entrée. L'ADR-0006, réserve n° 2, avait déjà isolé le point : « demandes RGPD »
désigne les demandes régies par le règlement, « microservice RGPD » désigne le service. Les deux
`RGPD` ne nomment pas la même chose ; retirer le second effacerait une qualification juridique, pas
une répétition.

## Les options écartées

- **Garder `Configuration du microservice RGPD` partout.** La barre aurait lu
  `Microservice RGPD │ Configuration du microservice RGPD │ …` — la redondance que ce renommage crée,
  laissée entière.
- **Raccourcir en `Configuration` partout, un seul nom.** Écarté par le demandeur. C'était l'option la
  moins chère en mécanique, mais elle aggravait une ambiguïté déjà relevée par l'ADR-0006, réserve
  n° 5 : « Configuration » tout court se lit comme des réglages d'application, et la carte de
  l'accueil n'a aucun wordmark au-dessus pour la désambiguïser.
- **`Configuration des systèmes déclarés`**, un seul nom, complément métier au lieu du complément
  d'architecture. Proposé deux fois, écarté deux fois par le demandeur. Il obtenait le même résultat
  sans double nom.

## Conséquences, y compris celles qui coûtent

⚠️ **La redondance survit sur l'accueil.** La barre surmonte les cartes sur le même écran :
l'`Operator` y lit *Microservice RGPD* puis, quinze centimètres plus bas, *Configuration du
microservice RGPD*. Le double nom a chassé la répétition de la barre, pas de l'écran le plus
fréquenté. Signalé au demandeur avant la décision, et accepté.

**La clause d'incomplétude cite désormais la forme courte, et c'est le seul texte de domaine
concerné.** `IncompletenessClause.RelationToManifest` vit dans `Core` et dit « … dans
« Configuration » ». Le coût est réel mais borné, et la borne a été vérifiée plutôt que supposée :
cette clause n'est rendue qu'en un seul endroit, `_IncompletenessClause.cshtml`, inclus par les
quatre écrans de détection (`Report`, `Table`, `Archive`, `ArchivedTable`), qui portent tous la barre,
donc le mot que la phrase cite est écrit à l'écran au moment où on la lit. Elle ne part ni dans une
`DeliveryLetter`, ni dans une charge d'API : le contexte `Screening` n'en a aucune. Le jour où un
texte de domaine portant la forme courte quitterait la surface, c'est là que la règle « aucune forme
courte » de l'ADR-0006 se retournerait — et il faudra alors y remettre la forme pleine, pas assouplir
la règle.

**Un site emploie la forme courte hors des trois lignes du tableau ci-dessus**, et il est nommé ici
plutôt que corrigé en douce : le message d'appel refusé de l'écran d'un dossier (`Case.cshtml`) —
« « Configuration » et le programme qui la sert ne sont pas d'accord ». Le nom n'y est ni un libellé
de barre, ni un titre, ni un geste de navigation : il désigne l'une des deux parties en désaccord,
sans qu'on clique. C'est le seul endroit où la forme courte porte une charge de sens plutôt qu'une
adresse, et la forme pleine s'y défendrait.

**Le lien de retour du tableau des demandes est devenu un lien orphelin d'un mot** (`Queue.cshtml`) :
un paragraphe dont tout le contenu est « Configuration », c'est-à-dire mot pour mot et vers la même
adresse la première entrée de la barre du même écran. Avant, sa forme pleine se décrivait elle-même ;
il ne dit plus rien que la barre ne dise déjà.

**Le titre d'onglet de l'écran de configuration est redondant** :
`Configuration du microservice RGPD — Microservice RGPD`. Aucune troisième forme n'a été inventée
pour l'éviter.

⚠️ **Rien n'empêche mécaniquement les deux formes de se rejoindre.** Ni le compilateur, qui voit deux
champs distincts porter des chaînes, ni le type. Le garde est un test, et un seul :
`SharedLayout.CarriesTheThreeNavigationLabelsWordForWordAndInOrder`, qui recopie les trois libellés
de barre à la main et se lit avec `Doorstep.CarriesTheThreeDoorwaysWordForWordAndInOrder`. La barre
n'était jusqu'ici éprouvée que sur ses adresses : sans ce test neuf, un retour silencieux à la forme
pleine dans la barre ne se serait vu nulle part.

⚠️ **Aucun garde contre le retour de l'ancien wordmark.** Le mécanisme a existé
(`RetiredVocabularyTests`, cité par l'ADR-0006) et ne vit plus dans le dépôt. Il n'a pas été rétabli
pour l'occasion — écarté par le demandeur. « Droits des personnes concernées » peut donc réapparaître
sans qu'aucun test ne s'y oppose.

## Ce que cet ADR n'ouvre pas

- **La phrase de présentation de l'accueil** (`Navigation.Presentation`) est intacte. Elle dit ce que
  le service fait — ce que le nouveau nom ne dit plus. Elle est la seule à le dire encore.
- **Les ADR 0001 à 0007** ne sont pas édités. Un ADR acté parle avec les mots de sa date ; l'ancien
  wordmark y reste écrit, et c'est ce qui rend cet ADR-ci lisible.
- **Le `README.md`** ne portait pas l'ancien wordmark. Vérifié, rien à y changer.
- **Les identifiants du code** restent en anglais, et aucun n'est touché par ce renommage hors du
  `EntryPoint` lui-même : `Manifest` reste `Manifest`, `/manifest` reste `/manifest`.
