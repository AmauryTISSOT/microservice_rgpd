# ADR-0010 — Un quatrième point d'entrée : la `Qualification` a sa surface

- **Statut** : accepté
- **Date** : 2026-08-20
- **Décidé par** : [Carte — La `Qualification` a sa surface](https://github.com/AmauryTISSOT/microservice_rgpd/issues/248), et le ticket des écrits qu'elle a ouvert en premier : [#249](https://github.com/AmauryTISSOT/microservice_rgpd/issues/249)
- **Supplante, sur un point** : [ADR-0006](./0006-trois-points-d-entree-renommes-en-francais-identifiants-inchanges.md), qui tenait le compte des points d'entrée à trois — la clause « Trois entrées, et pas une quatrième ». Elle s'y lit deux fois : *« une barre à trois entrées se lit d'un coup d'œil quand une barre à quatre se parcourt »*, et *« la clause "trois entrées se lisent d'un coup d'œil" est érodée »*. Cette clause ne vaut plus. Tout ce que l'ADR-0006 décide par ailleurs reste en vigueur — les trois noms français des points d'entrée, les identifiants C# inchangés, le texte gelé de la clause d'incomplétude — sous réserve des deux points que les ADR-0007 et 0008 lui avaient déjà retirés, et son texte n'a pas été réécrit. Une suite datée est appendue en fin de son fichier, qui nomme ses trois points morts sans toucher une ligne de ce qui précède.
- **S'appuie sur** : [ADR-0009](./0009-la-navigation-passe-en-panneau-lateral-repliable-sans-javascript.md), qui rangeait « une quatrième entrée » parmi ce qu'il n'ouvrait pas, tout en notant que le panneau latéral « la porterait sans effort — c'est même une des raisons de la forme ». Ce que cet ADR-là a préparé sans le décider, celui-ci le décide.
- **Ne touche pas** : le contrat HTTP. Ce qui remonte à l'écran et ce que l'API tait relèvent de l'[ADR-0011](./0011-les-internes-des-moteurs-sur-la-surface-de-l-operator.md), décision séparée et défaisable séparément.

## Contexte

Le dépôt porte trois contextes bornés et trois points d'entrée, et la correspondance passait pour un
ordre des choses. Elle n'en est pas un : `Configuration` sert le `Manifest`, la détection des données
personnelles sert `Screening`, le tableau des demandes RGPD sert `Casework`. La `Qualification` —
recevoir un texte libre français et dire quels droits il exerce — n'a aucune surface. C'est le seul
des trois contextes dans ce cas.

Elle n'existe que derrière `POST /qualifications`, un contrat HTTP destiné aux applications tierces.
Un `Operator` devant le service qui veut qualifier un texte doit sortir de l'application et fabriquer
une requête HTTP à la main.

**Le manque se voit déjà dans l'interface, et il s'y voit comme une incohérence.** Au dépôt manuel
d'une demande arrivée par courriel, l'`Operator` coche les droits entièrement à la main, sans aucune
aide, alors que le service sait les proposer. Et le `<select>` de ce même formulaire offre
`ClaimOrigin.Proposed` — « proposé en amont » — alors que rien, nulle part dans l'interface, ne
produit la qualification qui la justifierait. Une valeur du domaine est offerte à l'`Operator` sans
qu'aucun écran ne puisse la rendre vraie.

**Ce qui tenait le compte à trois n'était pas une doctrine, c'était une forme.** L'ADR-0006
raisonnait sur une barre horizontale : trois entrées se lisent d'un coup d'œil, quatre se
parcourent. Il notait lui-même la clause « érodée » par l'allongement des libellés. L'ADR-0009 a
retiré la barre. Le panneau latéral n'a pas de ligne à faire déborder, et le motif de la clause a
disparu avec la forme qui le portait — avant même que le besoin d'une quatrième entrée soit posé.

## Décision

**1. Le service ouvre un quatrième point d'entrée, à `/qualification`, nommé « Qualification ».** Il
est légitime et permanent, à égalité avec les trois autres : une entrée du panneau latéral, une carte
de l'accueil, un écran sous le `Layout`. Ce n'est pas un outil de maintenance, pas une porte de
service, pas un écran de démonstration.

**2. Le nom est « Qualification », répété à l'identique** dans le libellé du panneau et sur la carte
de l'accueil. C'est la forme normale posée par l'ADR-0008 : un seul nom par écran. Un seul écran du
service porte deux noms vivants — la configuration — et ce n'est pas celui-ci.

**3. Le rang est le troisième des quatre** : Configuration, Détection des données personnelles,
Qualification, Tableau des demandes RGPD. Lu dans l'ordre, le panneau dit « on configure, on détecte,
on qualifie, on traite ». Le rang porte le sens que les noms ne portent pas — le motif déjà écrit par
l'ADR-0006 pour l'ordre des trois.

**4. L'écran est toujours présent, y compris en production.** Il n'ajoute aucune capacité :
`POST /qualifications` est déjà anonyme et public, et l'écran ne fait rien que l'API ne fasse déjà.
Il ajoute une visibilité, et c'est l'ADR-0011 qui l'assume.

**5. Aucun dossier n'en découle.** L'écran qualifie et rend le verdict ; il n'ouvre pas de `Case`, ne
rattache rien, et la carte de l'accueil le dit pour qu'on n'y arrive pas en croyant y déposer une
demande. C'est la surface humaine de ce que l'API fait déjà pour les applications tierces, et rien de
plus.

**6. Le compte des points d'entrée n'est plus une clause.** Ce que l'ADR-0006 tenait à trois par la
lisibilité d'une barre, cet ADR ne le refixe pas à quatre : le panneau latéral porte ce qu'on y met,
et un point d'entrée se justifie désormais par ce qu'il sert — un contexte borné sans surface — pas
par un compte.

## Conséquences

- **Les trois contextes bornés ont chacun leur porte**, et l'un des trois n'en avait aucune. Ce n'est
  pas pour autant une correspondance un pour un : le panneau compte quatre entrées pour trois
  contextes, la configuration et le tableau des demandes relevant tous deux de `Casework` et du
  `Manifest` qu'il consomme. Le compte des portes ne se déduit pas du compte des contextes, ni avant
  ni après cette décision — c'est ce que le point 6 acte.
- **`ClaimOrigin.Proposed` cesse d'être une valeur que rien ne produit.** L'écart n'est pas comblé
  pour autant : l'assistance à la qualification dans le dépôt manuel est un chantier distinct, et cet
  ADR ne l'ouvre pas. La valeur cesse seulement d'être inatteignable par principe.
- **Le harnais de layout paie le passage de trois à quatre.** Ses listes sont recopiées à dessein —
  adresses, libellés du panneau, portes de l'accueil, inventaire d'écrans — et le passage se fait donc
  à la main dans une douzaine d'endroits. C'est la conception du harnais : une liste qui pointerait
  vers celle du code cesserait de garder quoi que ce soit. Les deux tests partagés nommés « les trois
  points d'entrée » et « les trois libellés » deviennent « quatre ».
- **Le panneau latéral s'allonge d'une ligne, et rien ne se déforme.** C'est ce que l'ADR-0009 avait
  prévu sans le décider.
- ⚠️ **La régression d'accessibilité consignée par l'ADR-0005 et aggravée par l'ADR-0009 s'aggrave
  encore d'un cran.** Ce qu'il faut traverser au clavier avant d'atteindre le contenu compte une
  entrée de plus, et il n'y a toujours aucun lien d'évitement. Rien n'est corrigé ici ; le motif reste
  celui de l'ADR-0005 — aucune exigence RGAA ou WCAG n'est déclarée par ce dépôt. La trace est
  reconduite, plus lourde qu'avant.
- ⚠️ **Le geste de l'écran déroge au motif POST-Redirect-GET** que le dépôt applique au dépôt d'un
  relevé : le `POST` qualifie, écrit la trace et rend la page portant le verdict, sans redirection. La
  dérogation est imposée par la conception du contexte — `IQualificationAuditTrail` n'expose qu'une
  méthode d'écriture, et son contrat écrit que *« ce n'est pas une omission qu'on comblera »*. Ouvrir
  un `GET` de relecture ferait de la trace d'audit une ressource métier exposée, l'entité même que ce
  contexte a refusée. Conséquence acceptée : un rechargement rejoue la qualification, produit un
  nouvel identifiant et une nouvelle trace. L'écran le dit en clair, sous le verdict.
- **Chaque qualification rendue par l'écran laisse une trace d'audit**, comme celles de l'API — même
  chemin, même écriture. La référence appelante y est vide : il n'y a pas d'application tierce. Aucune
  colonne de provenance de canal n'est ajoutée.

## Alternatives écartées

| Alternative | Motif du rejet |
| --- | --- |
| **Ne rien ouvrir, et documenter l'appel HTTP** | c'est l'état actuel. Il demande à un `Operator` de fabriquer une requête à la main pour se servir d'une capacité que le service possède déjà, et laisse `ClaimOrigin.Proposed` sans producteur |
| **Ouvrir la qualification directement dans le dépôt manuel**, sans écran propre | c'est la vraie valeur métier, et elle reste visée — mais elle touche l'ouverture d'un dossier, un chantier plus lourd. La faire d'abord aurait mis la qualification au monde cachée dans un formulaire d'un autre contexte, sans jamais dire qu'elle existe |
| **Un écran atteint par une adresse non annoncée**, hors du panneau et de l'accueil | une porte qu'aucune surface ne nomme n'est pas un point d'entrée, c'est une dette. Elle aurait aussi contourné la clause de l'ADR-0006 au lieu de la supplanter — le compte serait resté à trois sur le papier, et à quatre en fait |
| **Un écran présent hors production seulement** | il aurait fallu une notion d'environnement dans la surface, que le dépôt n'a pas, pour un écran qui n'ajoute aucune capacité : l'endpoint qu'il appelle est déjà anonyme et public |
| **Le rang premier ou dernier dans le panneau** | le troisième rang est le seul qui rende l'ordre lisible comme une phrase. Premier, il précéderait la configuration dont tout dépend ; dernier, il suivrait le traitement qu'il précède dans les faits |
| **Deux noms, un court pour le panneau** | l'ADR-0008 pose la forme normale à un nom par écran, et « Qualification » tient déjà dans le panneau. Un second nom serait une seconde forme à maintenir sans besoin |
| **Fondre cette décision et l'ADR-0011 en un seul ADR** | ce sont deux décisions sans rapport, qui se défont séparément : on peut retirer la porte du panneau sans rien changer à ce que l'écran montre, et l'inverse. Le dépôt supplante par points nommés ; un ADR fondu ne saurait plus se supplanter à moitié |

## Ce que cet ADR n'ouvre pas

- **Ce que l'écran affiche.** Le verdict, les avis, la confiance, les identités de moteurs et les
  latences relèvent de l'[ADR-0011](./0011-les-internes-des-moteurs-sur-la-surface-de-l-operator.md).
- **L'assistance à la qualification dans le dépôt manuel d'une demande**, ni le rattachement d'un
  identifiant de qualification à un dossier. Les deux restent des chantiers distincts, et l'écart
  entre la carte des contextes et la commande d'ouverture reste ouvert et connu.
- **Toute lecture de la trace d'audit**, sous quelque forme que ce soit : historique, relecture d'un
  verdict, adresse par identifiant. C'est le refus qui impose la dérogation au POST-Redirect-GET, et
  il n'est pas rouvert ici.
- **Une authentification ou un contrôle d'accès**, hors périmètre pour tout le service.
- **Un cinquième point d'entrée.** Le compte cesse d'être une clause, il ne devient pas une
  invitation.
- **Les ADR 0001 à 0009**, qui ne sont pas édités. Un ADR acté parle avec les mots de sa date.
