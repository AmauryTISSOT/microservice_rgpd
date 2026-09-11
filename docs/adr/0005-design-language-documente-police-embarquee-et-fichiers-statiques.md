# ADR-0005 — Le design language documenté est adopté, sa police est embarquée, et le service ouvre ses fichiers statiques

- **Statut** : accepté
- **Date** : 2026-08-15
- **Décidé par** : [Appliquer le design language documenté à la surface de l'`Operator`](https://github.com/AmauryTISSOT/microservice_rgpd/issues/201), livré par [#202](https://github.com/AmauryTISSOT/microservice_rgpd/issues/202) (les fondations) et [#203](https://github.com/AmauryTISSOT/microservice_rgpd/issues/203) (la structure)
- **Complète** : [ADR-0001](./0001-architecture-polyglotte-et-moteur-auto-heberge.md), dont le refus de toute sous-traitance au sens de l'art. 28 est ce qui interdit ici la police servie par un tiers

## Contexte

La surface de l'`Operator` — les douze écrans qui portent la file, le dossier, le `Manifest` et le
dépistage — n'avait jamais reçu de traitement visuel. Tout son style tenait dans une soixantaine de
règles CSS écrites à même le layout partagé : polices système, aucune couleur de fond, aucun token,
un rouge recopié cinq fois en dur. Rien ne hiérarchisait ce qui se lit, et les dix-huit formulaires
du service rendaient le même bouton gris — celui qui prend un dossier en charge et celui qui détruit
un journal de preuve compris.

Le dépôt portait par ailleurs deux directions visuelles contradictoires, dont aucune n'atteignait le
produit :

- un **document de design importé** sous `docs/design/`, analyse du design language de Notion, avec
  sa palette, son échelle typographique, ses espacements, ses rayons et ses élévations. Il est marqué
  en version alpha et décrit l'organisation d'un tiers ;
- une **maquette d'arbitrage jetable**, `docs/contexts/casework/prototypes/gui-operateur.html`,
  produite pour trancher la forme de l'écran d'arbitrage. Elle portait la seule palette de tokens
  réelle du dépôt, couvrait déjà le mode sombre, et parlait une langue plus proche de celle du
  produit — froide, administrative, à petit rayon (`--accent: #2E5266`, `--radius: 3px`).

Le projet Web ne servait alors aucun fichier statique : le répertoire dédié était vide et
l'intergiciel correspondant n'était jamais activé.

## Décision

Le design language documenté sous `docs/design/` est adopté pour la surface de l'`Operator`, valeurs
comprises. Sa police est embarquée dans le service. Le service ouvre ses fichiers statiques.

Les trois décisions ne sont pas séparables, et c'est pourquoi elles tiennent dans un seul ADR : on
adopte ce design language, donc on embarque la police qu'il prescrit — un service qui outille le RGPD
ne peut pas faire fuiter l'adresse IP de ses utilisateurs vers un hébergeur de polices — donc on
ouvre le service de fichiers statiques, seul moyen de servir cette police soi-même. L'embarquement
n'a de motif que l'adoption, et l'ouverture des statiques n'a de motif que l'embarquement.

1. **Le design language.** On en retient le socle — canevas, encres, accent structurel unique,
   filets, élévations discrètes, échelle typographique, espacements, rayons — et on écarte tout ce
   qui relève du marketing : la bande de héros indigo, les cartes de tarif, les pastilles
   promotionnelles, les boutons en pilule et la palette décorative « sticker ». Le document décrit un
   site de vente ; le service n'a aucune page publique et ses douze écrans sont tous des écrans
   d'instruction. Toutes les valeurs vivent en propriétés personnalisées sur `:root`, dans une
   feuille unique.
2. **La police.** Un seul fichier variable, sous-ensemble latin, servi par le service. Le document
   emploie quatre graisses ; un fichier variable les porte toutes en une requête au lieu de quatre.
   La pile de repli complète que le document donne lui-même est déclarée, et l'affichage est immédiat
   dans cette pile si le fichier n'arrive jamais.
3. **Les fichiers statiques.** L'intergiciel est activé dans la configuration d'intergiciels. Le CSS
   quitte le fichier de vue pour une feuille servie : une fois l'intergiciel ouvert pour la police,
   garder le CSS dans la vue ne protégeait plus de rien. Aucune ressource tierce n'est introduite —
   les deux fichiers ajoutés sont servis par le service. Le commentaire de doctrine du layout, qui
   interdisait « toute ressource externe », interdit désormais les ressources **tierces** et autorise
   celles que le service sert lui-même : la lettre du texte d'alors tiendrait encore, mais sa raison
   ne tiendrait plus.

## Justification

**Le demandeur a tranché pour le document, valeurs comprises.** C'est l'arbitrage porteur, et il
règle du même coup le sort de la maquette : un système mêlant les deux directions aurait produit des
valeurs dont plus personne n'aurait pu dire d'où elles viennent. Adopter les valeurs telles quelles
rend chaque couleur traçable à une ligne du document.

**Embarquer la police est la clause porteuse de l'ADR-0001 appliquée à la surface.** Le refus de
toute sous-traitance au sens de l'art. 28 ne souffrirait pas qu'un service qui outille le RGPD
transfère lui-même les adresses IP de ses utilisateurs à un hébergeur de polices pour afficher un
écran d'instruction.

**Le document est en alpha et décrit une organisation tierce ; il a été adopté tel quel par
décision.** Le fait est consigné ici pour qu'il ne se découvre pas plus tard : la source n'est ni
stable ni interne, et les endroits où elle ne disait rien ont dû être comblés (voir plus bas).

**Aucun test n'assertera une valeur de design** — pas une couleur, pas un rayon, pas une taille. Une
telle assertion lirait le contenu de la feuille de style : elle décrirait l'implémentation, casserait
à chaque retouche, et n'attraperait jamais rien. Conséquence assumée : le changement visuel lui-même
n'est pas testé, sa vérification est humaine. La suite protège le contenu des écrans et la présence
du chrome, pas leur apparence.

## Conséquences

**Acquis**

- Toutes les valeurs de design vivent en un seul endroit : une couleur se change en une ligne au lieu
  de cinq. Le rouge de signalement, jusque-là recopié cinq fois — dépassement d'échéance, constat
  réclamé, refus au dépôt, bloc des refus, dépistage inachevé —, est conservé tel quel et simplement
  nommé : la factorisation ne change aucun rendu.
- Le CSS quitte le fichier de vue, et le layout redevient lisible.
- Le service peut désormais servir un fichier statique — capacité acquise une fois, disponible
  ensuite.

**Coûts et contraintes**

- **La perte du mode sombre est une conséquence acceptée, pas un oubli.** Les écrans s'affichaient
  jusqu'ici correctement en sombre sans qu'une seule couleur soit écrite, parce qu'aucune couleur de
  fond ni de texte n'était déclarée et que le thème était délégué au navigateur. Fixer un canevas et
  une encre verrouille le clair. Le document ne fournit aucune valeur sombre, et en inventer une
  quinzaine reviendrait à écrire un autre design language plutôt qu'à appliquer celui-ci. Les tokens
  sont structurés pour qu'un thème sombre soit greffable plus tard sans toucher à un seul écran.
- **L'absence de lien d'évitement, et la régression d'accessibilité qui en découle.** Décision
  explicite du demandeur. La barre de navigation se répète sur les douze écrans sans moyen de la
  sauter au clavier, ce qui est une régression par rapport à l'état antérieur, où aucune barre
  n'existait. Le dépôt ne documente aujourd'hui aucune exigence RGAA ou WCAG et aucune n'est
  introduite ici. La conséquence est consignée, pas traitée — elle est écrite pour être opposée le
  jour où l'accessibilité devient un chantier.
- **Trois extensions du document**, parce qu'il ne disait rien à ces endroits :
  - **La sémantique d'erreur.** Le document déclare n'exposer aucune palette d'erreur ou de succès
    dans son chrome : le statut y est porté par la palette « sticker » décorative. Le service en a
    besoin. Le rouge d'erreur est celui qui était déjà dans les écrans, repris tel quel ; « succès »
    et « attention » reprennent deux couleurs sticker promues au rang sémantique, écrites sur demande
    explicite pour être disponibles bien qu'aucun écran ne les emploie. ⚠️ **Elles ne doivent jamais
    servir de couleur de texte** : mesurées sur blanc elles atteignent 3,0:1 et 3,8:1, sous le seuil
    WCAG AA de 4,5:1. Aucun token « information » n'est créé : il aurait valu la même couleur que
    l'accent, et le même bleu aurait voulu dire à la fois « clique ici » et « ceci est une
    information ». Là où un orange doit se lire en texte — la date limite en échéance proche
    (ADR-0021) —, c'est l'orange profond du document qui est promu, `--signal-warning-ink` : il
    atteint 9,1:1 sur blanc.
  - **Le mode sombre**, absent du document, traité ci-dessus.
  - **Le geste destructeur.** Le document n'en prévoit aucun, parce qu'il décrit un site de vente où
    rien ne se détruit. Le domaine nomme déjà « le geste irréversible » et en porte trois. Le bouton
    existe donc, en filet et texte rouge sur fond blanc plutôt qu'en aplat : un aplat se lit comme
    l'action que l'écran attend, et aucun de ces trois gestes ne l'est jamais.
- Le poids servi augmente d'un fichier de police et d'une feuille de style — les deux servis par le
  service, aucun tiers contacté.
- Le document adopté est en alpha et décrit une organisation tierce. Il peut bouger sous nos pieds
  sans nous prévenir ; c'est la copie du dépôt qui fait foi.

## Alternatives écartées

| Alternative | Motif du rejet |
| --- | --- |
| **La direction visuelle de la maquette d'arbitrage** | le demandeur a tranché pour le document, valeurs comprises. Elle avait trois avantages réels, consignés ici : la seule palette de tokens réelle du dépôt, la couverture du mode sombre, et un registre — froid, administratif, à petit rayon — plus proche de celui du produit que le registre chaud du document. Elle est abandonnée et la maquette supprimée : deux directions contradictoires ne peuvent pas coexister dans les docs |
| **Un système mêlant les deux directions** | il aurait produit des valeurs dont personne n'aurait pu dire d'où elles viennent — ni du document, ni de la maquette |
| **Police chargée depuis un hébergeur tiers** | interdit par la clause porteuse de l'ADR-0001 : un service qui outille le RGPD ne transfère pas lui-même les adresses IP de ses utilisateurs hors de l'infrastructure du client |
| **Quatre fichiers de police statiques** | un fichier variable porte les quatre graisses du document pour un poids équivalent, en une requête au lieu de quatre ; ajouter une graisse plus tard ne coûtera rien |
| **Garder le CSS dans le fichier de vue** | une fois l'intergiciel de statiques ouvert pour la police, l'inliner ne protégeait plus de rien, et le layout restait illisible |
| **Livrer un mode sombre** | le document ne fournit aucune valeur sombre ; en inventer une quinzaine serait écrire un autre design language. Rouvrable : les tokens sont structurés pour l'accueillir |
| **Un lien d'évitement** | écarté explicitement par le demandeur, avec sa conséquence consignée ci-dessus |
| **Un framework client ou un pipeline front** | le service n'en a pas et n'en acquiert pas : la surface reste rendue par le serveur, sans JavaScript |

## Portée de cet ADR

Il tranche l'adoption d'un design language, l'embarquement de sa police et l'ouverture du service de
fichiers statiques — trois décisions de système qui se justifient l'une l'autre. Il ne dit rien de la
forme des écrans pris un à un, rien des largeurs de conteneur, qui sont conservées inchangées et dont
le motif reste écrit là où il vaut, et il ne couvre pas la boutique témoin : elle joue le site d'un
client tiers et doit rester visuellement étrangère au service.
