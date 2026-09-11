# ADR-0009 — La navigation passe en panneau latéral repliable, et le repli n'a pas une ligne de JavaScript

- **Statut** : accepté
- **Date** : 2026-08-17
- **Décidé par** : une session de conception avec le demandeur, précédée d'un prototype à trois variantes, jeté après avoir servi. Il n'en subsiste rien dans le dépôt. La spécification est portée par cet ADR lui-même.
- **Supplante, sur un point** : [ADR-0007](./0007-le-cadre-partage-des-ecrans-se-nomme-layout.md), qui posait que le layout portait **une** barre, nommée `Navigation`. Le layout en porte désormais deux, et le mot « la barre » ne désigne plus rien sans ambiguïté. `Navigation` et `EntryPoint` gardent leur sens — ils nomment le modèle des points d'entrée, pas la région qui les affiche. Tout le reste de l'ADR-0007 reste en vigueur, et son texte n'a pas été édité.
- **Aggrave, sans la corriger, une régression consignée par** : [ADR-0005](./0005-design-language-documente-police-embarquee-et-fichiers-statiques.md). Voir « Conséquences ».

## Contexte

Les trois points d'entrée vivaient dans une barre horizontale posée en haut de chacun des douze
écrans. Deux choses la rendaient étroite.

**Elle ne tenait pas la croissance.** Trois entrées passent dans une ligne ; une quatrième ou une
cinquième la font déborder, et il n'existe aucun endroit où les mettre. La forme imposait un plafond
qu'aucune décision n'avait choisi.

**Sur les écrans longs, le retour se payait d'un défilement.** Un dossier de mille lignes se quitte en
remontant jusqu'en haut. La barre était bien répétée sur tous les écrans, mais elle n'était
atteignable qu'au sommet du document.

Un prototype à trois variantes a été construit pour trancher la forme, et le demandeur a choisi la
variante B — le panneau qui se replie entièrement, sans rien laisser derrière lui. Une seconde
question lui a été posée séparément, sur le mécanisme du repli, et il a tranché « CSS pur, sans
persistance ».

## Décision

**1. Les trois points d'entrée quittent la barre horizontale pour un panneau latéral gauche**, haut
de tout l'écran sous le header, large de 17,5 rem, ouvert par défaut. Il vit dans le layout : un écran neuf le porte
sans que personne y pense.

**2. Le layout garde une seconde région, le header**, mince, en haut de l'écran sur toute sa largeur — le panneau se pose sous lui. Il
porte le hamburger, le nom du service et la version, et il survit au repli. C'est ce qui distingue ce
panneau d'un tiroir qui ne se rouvrirait plus : panneau replié, l'écran garde un chemin de retour à
l'accueil et le moyen de redéployer la navigation.

**3. Le repli est une case à cocher, et le hamburger est son `label`.** Trois règles CSS derrière
`:checked ~` font tout le travail. Le service continue de ne servir aucun JavaScript — la clause de
l'ADR-0005 (« le service n'en a pas et n'en acquiert pas : la surface reste rendue par le serveur,
sans JavaScript ») est tenue sans exception, et non pas tenue à un script près.

**4. L'état du repli ne survit pas à la navigation.** La case repart décochée à chaque écran, et le
panneau s'y rouvre. C'est la conséquence directe et assumée du point 3.

**5. Le vocabulaire.** Deux régions nouvelles se nomment, et le dépôt n'en avait qu'une :

| Objet | Dans le code | En français |
| --- | --- | --- |
| La région gauche, repliable, qui porte les trois entrées | `sidepanel` | le **panneau latéral** |
| La région haute, qui survit au repli | `header` | le **header** |
| La boîte qui range les deux colonnes | `layout` | le layout |
| Le modèle des points d'entrée | `Navigation`, `EntryPoint` | *inchangé* |

Deux motifs, et ils ne sont pas symétriques :

- Le panneau prend un nom français en prose — « panneau latéral » — parce que la prose du dépôt parle
  français et que le mot n'entre en collision avec rien.
- ⚠️ **Le header garde le mot anglais dans les deux registres, parce que « bandeau » est déjà pris.**
  Le dépôt l'emploie pour le *bandeau d'avertissement permanent* d'un écran (`DepositScreen`,
  `CaseScreen`, `CaseTests`, `LocateHandlerTests`), un objet sans aucun rapport. Deux choses
  différentes ne portent pas le même mot — la règle même qui a fait sortir « chrome » à l'ADR-0007,
  appliquée avant plutôt qu'après.

`shell` ne devient pas un nom du dépôt. La boîte de disposition avait d'abord été écrite
`<div class="shell">`, mot que l'entrée `Layout` du glossaire range en `_Avoid_` depuis l'ADR-0007.
Elle se nomme `layout`. « coque » rejoint le même `_Avoid_`, pour la même raison.

## Conséquences

- **Le choix de l'`Operator` est perdu à chaque changement d'écran.** Celui qui replie le panneau le
  retrouve ouvert sur l'écran suivant. C'est la contrepartie exacte de « aucun JavaScript », elle est
  écrite dans le layout comme ici, et elle est rouvrable sans rien casser : un cookie posé par un
  aller-retour serveur la lèverait sans introduire un script.
- ⚠️ **La régression d'accessibilité consignée par l'ADR-0005 est aggravée d'un cran.** Cet ADR-là
  notait que la navigation se répète sur les douze écrans sans lien d'évitement pour la sauter au
  clavier, et que la conséquence était « écrite pour être opposée le jour où l'accessibilité devient
  un chantier ». Le panneau précède désormais le contenu dans l'ordre du document sur chaque écran :
  ce qu'il faut traverser au clavier avant d'atteindre le contenu s'allonge. Rien n'est corrigé ici,
  et le motif reste le même — aucune exigence RGAA ou WCAG n'est déclarée par ce dépôt. La trace est
  reconduite, plus lourde qu'avant.
- **La case du repli reste atteignable au clavier.** Elle est masquée par `clip-path`, jamais par
  `hidden` ni `display: none` : les deux l'auraient sortie de l'ordre de tabulation et de l'arbre
  d'accessibilité, et le hamburger serait devenu un contrôle que seule une souris actionne. L'anneau
  de focus est reporté sur le hamburger, faute de quoi tabuler jusqu'au repli ne se verrait nulle
  part.
- **La doctrine de test change, et elle ne s'affaiblit pas.** Elle disait « l'écran porte exactement
  une barre `nav` » ; elle dit maintenant « exactement deux régions, reconnues à leur classe, et lues
  séparément ». Séparément est le mot qui porte : concaténées, le premier point d'entrée deviendrait
  « le premier lien », et l'assertion sur le nom du service se serait mise à lire `Configuration` sans
  jamais échouer. Un troisième `nav` posé un jour fait échouer le compte plutôt que de se faire lire à
  la place de l'un des deux.
- **Le balayage « aucun chiffre dans la navigation » porte sur les deux régions**, et gagne une
  seconde exception nommée à côté de la version : le tracé SVG du hamburger. `M2 4h14M2 9h14` est une
  géométrie, pas un compte. L'exception est nommée dans le code, pas obtenue en désarmant le
  balayage.
- **Panneau replié, aucun des trois écrans n'est à un clic** : il faut d'abord rouvrir. C'est le prix
  de cette forme, et il est assumé — voir les alternatives.
- **Le service reste sans une seule media query.** Le panneau est large de 17,5 rem en toutes
  circonstances. Sur un écran étroit il mange donc une part fixe de la largeur, là où la barre
  horizontale se contentait de passer à la ligne. Le dépôt n'a jamais visé le petit écran et ne
  commence pas ici ; le repli offre, de fait, la parade manuelle.
- Le poids servi ne bouge pas : aucun fichier de plus, aucun tiers contacté, un seul dessin ajouté et
  il est écrit en SVG inline dans le layout.

## Alternatives écartées

| Alternative | Motif du rejet |
| --- | --- |
| **Variante A du prototype** — le panneau se réduit à un rail d'icônes | le service n'a aucun vocabulaire d'icônes, et il aurait fallu en inventer trois. Une icône seule, sans mot, sur un service que personne n'utilise tous les jours, est une devinette |
| **Variante C du prototype** | recommandée par l'assistant, écartée par le demandeur, qui a tranché pour B. La mention est ici parce qu'un ADR consigne aussi les recommandations non suivies |
| **Persistance par cookie** | tenable sans script, mais elle demandait un aller-retour serveur pour un état d'affichage. Écartée par le demandeur, qui a choisi le CSS pur en connaissant la conséquence. Rouvrable : c'est la sortie la moins coûteuse si l'oubli du repli devient gênant |
| **Persistance par JavaScript et `localStorage`** | traverse la clause de l'ADR-0005. Cinq lignes de script auraient fait du « sans JavaScript » une approximation, pour un confort d'affichage. C'est l'amélioration qu'un lecteur pressé sera tenté d'apporter, et cet ADR est là pour la lui opposer |
| **Remonter les trois entrées dans le header quand le panneau est replié** | il aurait fallu tenir deux mises en page de navigation en parallèle, pour toujours, et la seconde n'aurait été vue que par ceux qui replient |
| **Un panneau non repliable** | c'est la demande initiale qui l'exclut, et la table d'arbitrage colonne par colonne a besoin de toute la largeur |
| **Garder les glyphes que la variante B montrait** | même motif que la variante A : le panneau est en texte seul, et `EntryPoint` ne gagne pas un champ pour porter une iconographie que le service n'a pas |
| **Nommer la région haute « bandeau »** | le mot désigne déjà le bandeau d'avertissement permanent d'un écran. Voir la décision, point 5 |
| **Nommer la boîte de disposition `shell`** | `_Avoid_` de l'entrée `Layout` du glossaire depuis l'ADR-0007 |

## Ce que cet ADR n'ouvre pas

- **Le lien d'évitement.** Il reste écarté, et sa conséquence reste consignée — plus lourde qu'avant.
  Ce n'est pas une décision reprise ici, c'est une décision de l'ADR-0005 dont on note qu'elle coûte
  davantage.
- **Le mode sombre**, toujours absent, et pour le motif de l'ADR-0005 : le design language ne fournit
  aucune valeur sombre. Les deux régions neuves n'écrivent que des tokens, et ne referment donc rien.
- **Le petit écran.** Aucune media query n'est introduite, aucune n'est promise.
- **Une quatrième entrée.** Le panneau la porterait sans effort — c'est même une des raisons de la
  forme — mais l'ADR-0006 tient toujours le compte à trois, et il n'est pas touché.
- **Les ADR 0001 à 0008** ne sont pas édités. Un ADR acté parle avec les mots de sa date : « la
  barre » y reste écrite, et c'est ce qui rend cet ADR-ci lisible.
