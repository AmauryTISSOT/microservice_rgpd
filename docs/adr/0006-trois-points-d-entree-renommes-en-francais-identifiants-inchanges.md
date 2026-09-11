# ADR-0006 — Les trois points d'entrée sont renommés dans la langue humaine, et les identifiants C# ne bougent pas

- **Statut** : accepté
- **Date** : 2026-08-15
- **Décidé par** : [Carte — L'écran d'accueil de l'interface utilisateur, et les mots qu'il porte](https://github.com/AmauryTISSOT/microservice_rgpd/issues/208), et les huit tickets qu'elle a résolus : [#209](https://github.com/AmauryTISSOT/microservice_rgpd/issues/209) (l'inventaire), [#210](https://github.com/AmauryTISSOT/microservice_rgpd/issues/210), [#211](https://github.com/AmauryTISSOT/microservice_rgpd/issues/211), [#212](https://github.com/AmauryTISSOT/microservice_rgpd/issues/212) (les trois mots), [#213](https://github.com/AmauryTISSOT/microservice_rgpd/issues/213) (la phrase du seuil), [#214](https://github.com/AmauryTISSOT/microservice_rgpd/issues/214) (la forme d'une carte), [#215](https://github.com/AmauryTISSOT/microservice_rgpd/issues/215) (la relecture d'ensemble), [#217](https://github.com/AmauryTISSOT/microservice_rgpd/issues/217) (le texte gelé)
- **Étend** : [ADR-0005](./0005-design-language-documente-police-embarquee-et-fichiers-statiques.md), dont le design language acquiert ici son premier état interactif stylé
- **Complète** : [ADR-0003](./0003-troisieme-contexte-sans-intersection-et-garde-des-traversees.md) et [ADR-0004](./0004-moteur-de-depistage-en-csharp-sans-second-sidecar.md), qui parlent tous deux du contexte dont le nom français change ici — leur mot ne change pas, voir « Ce que les ADR actés gardent »

## Contexte

Les trois points d'entrée de l'interface utilisateur de l'`Operator` s'appelaient, en français,
« La file », « Le paysage déclaré » et « Le dépistage ». Les trois ont été mis en cause ensemble,
chacun pour un grief distinct, et le grief s'est établi en séance plutôt que par argument : le
demandeur, à qui l'écran des dossiers venait d'être décrit, a spontanément demandé « c'est donc là où
l'`Operator` traite les demandes ? » — or cet écran n'offre aucun geste sur un dossier
(`QueueScreen.cs:112`) et sert à décider par quoi commencer (`QueueScreen.cs:141`). Le mot avait raté
sa cible sur son premier lecteur neuf.

Le déclencheur est ailleurs, et il compte pour la portée de cette décision : le service n'avait
aucune page à la racine `/`. Un humain qui tapait l'adresse du service recevait un `404` en
`application/problem+json`. C'est en spécifiant cet accueil — trois cartes portant chacune un nom —
que les trois noms se sont retrouvés côte à côte pour la première fois, et que le grief est remonté.

### Les trois registres, et celui que ce renommage touche

Le dépôt tient depuis [#186](https://github.com/AmauryTISSOT/microservice_rgpd/issues/186) trois
registres de langue. Ce renommage porte sur le **registre 2** — les textes qu'un humain lit : écrans,
entrées de glossaire, commentaires XML français, textes gelés. Le registre 3 (la prose de
développeur) et les identifiants anglais ne sont pas touchés.

Le critère de recevabilité d'un mot est celui de #186 : le « lecteur A », le développeur qui arrive
sans RGPD ni vocabulaire judiciaire ; et un mot est bloqué s'il désigne autre chose dans le dépôt.
Les listes `_Avoid_` des glossaires, elles, ne bloquent plus un remplacement.

### Ce que l'inventaire a mesuré

L'inventaire ([#209](https://github.com/AmauryTISSOT/microservice_rgpd/issues/209)) a été fait avant
tout choix de mot, pour que ce document n'invente pas ses chiffres.

| Mot | Emplois vivants | Dont **nom d'écran** | Le reste |
| --- | ---: | ---: | --- |
| `dépistage` | ≈ **250** | 12 sites, sous quatre libellés dérivés | nom commun, et il porte de la doctrine |
| `file` | ≈ **90** | 5 sites | nom commun (« remonte dans la file », « le tri de la file ») |
| `paysage` | ≈ **45** | 8 sites | nom commun, séparable du nom d'écran |

Trois résultats commandent tout ce qui suit.

**`dépistage` coûte cinq fois `paysage`.** Ses quatre libellés dérivés — « Le dépistage », « Le
dépistage courant », « L'historique des dépistages », « Un dépistage archivé » — doivent bouger
ensemble, et ses ~240 emplois de nom commun ne sont pas un libellé : ce sont les clauses du contexte
borné lui-même.

**Le corpus est hors de portée.** `corpus/demandes-rgpd.fr.jsonl` — la donnée annotée — ne contient
aucun des trois mots. Les deux seuls emplois du répertoire sont de la prose *sur* le corpus. Le
renommage ne touche donc pas la vérité terrain, et aucun ré-annotage n'est dû.

**`file` était déjà en collision avec lui-même.** Neuf emplois nomment la file d'appels du sidecar de
qualification (`departure.py:4`, `app.py:203`, `opinion.py:62`, deux tests Python,
`docs/api/qualifications.md:123` et `:300`, `docs/spec/qualification.md:423` et `:488`). Par la règle
de #186, le mot était bloqué avant même qu'on lui cherche un remplaçant.

## Décision

Les trois points d'entrée prennent trois noms nouveaux en registre 2, dans cet ordre, sans article et
sans aucune forme courte. Les identifiants C# ne bougent pas.

| # | Ancien nom | **Nouveau nom** | Adresse |
| --- | --- | --- | --- |
| 1 | Le paysage déclaré | **Configuration du microservice RGPD** | `/manifest` (inchangée) |
| 2 | Le dépistage | **Détection des données personnelles** | `/detection` (`/depistage` meurt en 404) |
| 3 | La file | **Tableau des demandes RGPD** | `/dossiers` (inchangée) |

L'ordre est `Configuration → Détection → Tableau`, le même dans la barre et sur l'écran d'accueil.
C'est l'ordre de mise en route : on déclare ses systèmes, on cherche les données personnelles dedans,
puis on instruit les demandes. Il porte par la position ce qu'aucun des trois noms ne dit — que la
configuration est le point de départ.

### Aucune forme courte, nulle part

Il n'existe plus aucune forme courte, pour aucun des trois points d'entrée, à aucun endroit du dépôt.
Un seul nom par écran, du wordmark au glossaire, y compris dans les phrases où le nom pèse (« le
dossier remonte dans le tableau des demandes RGPD ») et dans les liens de retour en prose
(`ManifestRevise.cshtml:42`, `Queue.cshtml:140`).

Cette règle est la seule qu'aucun des trois tickets de mots ne pouvait produire seul.
[#210](https://github.com/AmauryTISSOT/microservice_rgpd/issues/210) avait autorisé
« Configuration » en forme courte dans la barre ;
[#211](https://github.com/AmauryTISSOT/microservice_rgpd/issues/211) et
[#212](https://github.com/AmauryTISSOT/microservice_rgpd/issues/212) avaient banni toute forme courte
pour les deux autres. La relecture d'ensemble
([#215](https://github.com/AmauryTISSOT/microservice_rgpd/issues/215)) a montré ce que ça donnait :
un libellé de treize caractères aligné contre deux de vingt-cinq et trente-quatre. La forme courte
« Configuration » est retirée, et la règle devient uniforme.

Le motif de fond, pris à #211 : *un mot dont la lisibilité dépend de l'écran où on le lit se
retrouvera un jour hors de cet écran*. Seul le retour pronominal dans la même phrase reste licite —
« il », « celui-ci » — parce que c'est de la grammaire, pas une seconde forme à maintenir.

Prix accepté : la barre passe à la ligne sur un écran étroit (7 / 18 / 12 caractères deviennent
25 / 34 / 34). `nav.chrome` et son `ul` sont déjà en `flex-wrap: wrap`, donc rien ne se déforme, mais
la clause « une barre à trois entrées se lit d'un coup d'œil quand une barre à quatre se parcourt »
est érodée, et c'est assumé.

### Les identifiants C# ne bougent pas

`Manifest`, `Screening`, `Queue`, `ScreenedColumn`, `IScreeningEngine`, `ScreeningEngineIdentity` et
`ScreenedListing` restent tels quels. Le motif est le contrat public : `system_id` figure dans
`docs/api/adapter.md`, les noms de types portent les frontières que l'[ADR-0002](./0002-deux-contextes-bornes-et-noyau-partage.md)
et l'[ADR-0003](./0003-troisieme-contexte-sans-intersection-et-garde-des-traversees.md) ont posées,
et les renommer serait rompre un contrat pour un motif d'ergonomie d'interface.

⚠️ **Conséquence à écrire noir sur blanc, faute de quoi un futur lecteur y verra une contradiction
frontale.** Les listes `_Avoid_` des glossaires bannissent nommément `Detection` (sur
`ScreenedColumn`) et `Report` / `Analysis` (sur `Screening`), alors même que l'interface dira
désormais « Détection des données personnelles » et « rapport de détection ». Il n'y a pas de
contradiction : ces listes portent sur les identifiants anglais, qui ne bougent pas. Le français de
l'écran et l'anglais du modèle sont deux registres, et le glossaire n'arbitre que le second.

### Le mot « système » : interrogé, et gardé

La question a été posée et elle est tranchée ici pour qu'elle ne se repose pas. `DeclaredSystem.cs:4`
définit un système déclaré comme « un endroit où des données personnelles vivent chez le client,
parce qu'un humain l'a déclaré. Unité de recensement, jamais de déploiement » — « la boutique », « la
messagerie support », « la reprise de 2019 ». Un même serveur peut porter trois systèmes déclarés. Le
mot est donc exact et non technique. Le changer toucherait 591 emplois, dont `system_id` du contrat
public. Gardé.

Distinction à ne pas perdre en chemin : le `Manifest` (la liste écrite à la main, obligatoire) n'est
pas l'`Adapter` (le programme HTTP que le client écrit, facultatif — un système sans aucune
`Capability` reste pleinement légitime, il est traité à la main).

## Ce que chaque mot a coûté, et ce qu'on a accepté de perdre

Les trois mots ont été choisis par le demandeur, après lecture des objections, et plusieurs l'ont été
contre la recommandation de l'agent. Les pertes sont réelles ; une perte non consignée se relit plus
tard comme un oubli, et quelqu'un remettra le mot.

### « Configuration du microservice RGPD » — deux coûts, dont un doctrinal

**Coût 1 — le mot désigne déjà les réglages du service.** `src/MicroserviceRgpd.Web/Configurations/`,
`appsettings.json`, `Llm:Enabled`, `Llm:SidecarDeadlineSeconds`, « la configuration du moteur LLM »
(`sidecar/README.md:102`, `:209`), `MiddlewareConfig.cs:31`.

Ce qui rend la collision tenable : les deux vivent dans des registres différents et ne se croisent
jamais.

| Registre | Mot | Lecteur | Où |
| --- | --- | --- | --- |
| **2** | « Configuration du microservice RGPD » | l'`Operator` | écrans, barre, carte d'accueil, glossaire |
| **3** | « configuration », « réglages » | un développeur | `appsettings`, commentaires XML, `README` |

**Coût 2 — le nom perd « déclaré » et perd « peut-être incomplet ».** C'est la perte la plus lourde du
renommage, et elle est acceptée les yeux ouverts :

- « déclaré » disparaît : une configuration se lit comme un réglage *du service*, pas comme ce que le
  client dit avoir. La clause `Enregistré, jamais vérifié` n'est plus portée par le nom.
- « peut-être incomplet » est pire que perdu : il est inversé. Une configuration est complète par
  définition — si c'est configuré, ça marche. Or l'`Omission silencieuse` est le risque cardinal de ce
  contexte.

L'enchaînement que le mot rend disponible à un lecteur :

```
« c'est la configuration »
  -> « donc une fois configuré, c'est bon »
    -> « donc le service couvre tout »
      -> l'Omission silencieuse, invisible.
```

⚠️ **La clause `Aucune modification vers le Manifest` est défendue ici explicitement, parce que le
nouveau nom la fragilise.** `Screening` n'a aucun droit d'écrire dans le `Manifest`, au motif qu'un
`Manifest` pré-rempli par une machine se lirait comme complet. Sous le nom « configuration »,
pré-remplir par une machine devient la chose la plus naturelle du monde : le nom appelle l'interdit.
La clause ne change pas, mais elle perd son évidence, et c'est pourquoi elle est réaffirmée dans un
ADR plutôt que laissée à la lecture d'un `_Avoid_`.

**Comment la dette est payée.** Par la phrase de la carte d'accueil, arrêtée en #215, qui rend au mot
ce que le mot a perdu :

> Vous y déclarez, à la main et un par un, les systèmes où vivent des données personnelles. Le
> service ne connaît que ceux que vous y inscrivez, et rien ne garantit qu'il n'en existe pas
> d'autres.

« vous y déclarez, à la main et un par un » rend le déclaratif ; « rien ne garantit qu'il n'en existe
pas d'autres » rend l'incomplétude, mot pour mot ce que `DeliveryLetter.cs:239` dit déjà à la
personne concernée. Le service tiendra donc le même langage aux trois endroits où la non-exhaustivité
se dit.

**Fait notable, relevé en passant.** Le dépôt portait déjà deux mots français pour le `Manifest` —
« le catalogue » (~25 emplois, registre 3) et « le recensement » (registre 2, texte gelé) — pendant
que l'écran en portait un troisième. Ce renommage n'ajoute pas un mot : il en arbitre un parmi
quatre.

### « Détection des données personnelles » — l'image médicale part, et une garantie s'affaiblit

Ce qui disparaît : le mot « dépistage », le verbe « dépister », l'image médicale qui les portait, et
l'URL `/depistage`, qui meurt en 404 sans redirection. Motif du 404 sec : interface interne
d'`Operator`, aucun lien profond partagé, et *une redirection est un second nom vivant pour la même
chose*, exactement ce que ce contexte refuse ailleurs (« un contexte qui a deux mots pour son geste
central en aura trois dans un an »).

⚠️ **Ce qu'on a accepté de perdre, et c'est la perte à ne pas oublier.** La clause de
`docs/contexts/screening/CONTEXT.md:10` disait : *« un dépistage est calibré pour la sensibilité, il
ne rend jamais un diagnostic mais des suspicions »*. Le mot « dépistage » portait par lui-même la
distinction *signale ≠ conclut* : c'est ce que le mot veut dire en épidémiologie, et le lecteur
l'avait gratuitement.

« Détection » ne la porte pas, et travaille même contre elle. Le risque a été pesé et il est retenu
tel quel : *« non détecté »* se lit plus facilement « il n'y a rien » que « la machine n'a rien vu »,
ce qui affaiblit `Unflagged`, dont le glossaire dit qu'elle « dit ce que le service n'a pas fait,
jamais ce que la colonne est ».

Trois choses amortissent ce risque, et aucune ne l'annule :

1. **La phrase de la carte nomme où s'arrête le regard de la machine** — « Il lit des noms de tables
   et de colonnes, jamais une valeur ; vous tranchez, ligne par ligne. » C'est cette moitié-là qui
   rend le terme tenable, et elle reprend ce que `Screenings/Deposit.cshtml:17` écrit déjà.
   Cette phrase a été réécrite par
   [#308](https://github.com/AmauryTISSOT/microservice_rgpd/issues/308), qui livre la voie
   connectée : le service prélève désormais quelques valeurs par colonne, et « jamais une valeur »
   avait cessé d'être vrai. Ce qui amortit le risque à sa place est ce qui reste vrai des deux
   chemins — « aucune valeur lue n'est conservée ». La décision de cet ADR ne bouge pas : c'est
   l'amortissement qui a changé de phrase, pas le nom.
2. **La clause de doctrine est réécrite, pas supprimée** : « calibré pour la sensibilité » devient
   « réglé pour signaler large, quitte à se tromper souvent — c'est à l'humain de trancher. » Le
   vocabulaire d'épidémiologie part avec le reste, sans quoi l'image médicale rentrerait par la
   fenêtre après être sortie par la porte.
3. **Les mécanismes ne bougent pas** : une `ScreenedColumn` par colonne sans exception, le verrou « ce
   rapport de détection est inachevé », l'arbitrabilité des lignes non signalées, et la clause
   d'incomplétude rendue à chaque affichage.

Le risque résiduel est consigné, pas résolu. Le mot travaille contre les garde-fous du contexte au
lieu de travailler avec eux. C'est le prix du départ de l'image médicale.

**Bénéfice collatéral, non anticipé.** « Sensibilité » était un homonyme dans son propre fichier :
sens épidémiologique en `CONTEXT.md:10`, sens RGPD art. 9 en `CONTEXT.md:171` (« la sensibilité est
une valeur, pas une seconde dimension »), à 160 lignes d'écart. Après ce renommage, « sensibilité »
ne veut plus qu'une chose dans tout le dépôt : l'article 9.

**L'objet qu'on compte s'appelle « rapport de détection », jamais « rapport ».** Le mot « rapport »
existait déjà (663 emplois, dont toute la prose des écrans), donc rien n'est inventé — mais la forme
longue est obligatoire, y compris à l'intérieur des écrans de détection où l'ambiguïté n'existe pas.
Motif : `ManifestVerification.cs:22` et `VerifyManifestHandler.cs:40` emploient déjà « rapport » pour
le rapport de vérification du `Manifest`, dans `Casework`. Arbitrage explicite du demandeur, contre
la proposition qui autorisait la forme courte en contexte non ambigu.

Homonyme connu et assumé : `Qualification` emploie déjà « détecteur » (« le lexique est détecteur,
jamais contributeur »). Contexte différent, objet différent, contextes isolés par construction —
homonyme de registre, pas collision de modèle.

### « Tableau des demandes RGPD » — deux objections écartées, portées ici

Le grief retenu est celui du malentendu en séance, décrit en tête de ce document. Les deux autres
diagnostics ont été écartés : « trop nu » ne tient pas (la ligne sous le `<h1>` dit déjà « Les
dossiers ouverts, rangés par échéance »), et « c'est faux » est faux — les dossiers y entrent
(`OpenCaseHandler`), en sortent (clôture), et l'échéance les range ; c'en est bien une, file de
priorité.

Deux objections ont été soulevées contre le mot retenu et écartées par le demandeur. Elles sont
écrites ici pour être opposées, pas redécouvertes.

1. **`table` / `tableau` désigne déjà autre chose dans le dépôt**, et précisément sous le point
   d'entrée voisin : `ScreenedTable`, `ArchivedTable`, `ArbitrateTableInBatch`, « la table
   d'arbitrage », « une table du relevé ». La règle de #186 aurait bloqué le mot. S'y ajoute la
   proximité avec le tableau de bord, interdit deux fois sur cet écran même.
2. **`demande` n'est pas ce que l'écran montre.** Le dépôt distingue la `demande` du `dossier`
   (`casework/CONTEXT.md:21`, « Une demande, un `Case` — jamais deux »), et l'écran dit lui-même
   qu'« une demande qui dort dans une boîte aux lettres est précisément ce que cet écran ne peut pas
   montrer » (`Queue.cshtml:135`). Contrepartie réelle : la carte d'accueil devient cohérente avec son
   propre titre, un seul mot pour l'écran au lieu de deux.

L'alternative proposée et refusée était « Les dossiers ouverts ».

### La clause de doctrine de la file survit, et change de sujet

> **Le tableau des demandes RGPD est une requête, jamais un processus.**

Elle vit à trois endroits — `ReadQueueQuery.cs:12`, `casework/CONTEXT.md:75`, et
`NothingRunsInTheBackgroundTests.cs:70`, où elle est le message d'échec d'un test d'architecture.
Elle ne se défendait pas contre le mot « file » mais contre l'idée qu'un `cron` tourne quelque part —
idée qui survit à n'importe quel nom. Réécrire le sujet d'une clause n'est pas rouvrir la clause : le
registre 3 n'est pas entamé, et la clause voisine (« un processus de fond interrompu rendrait une file
vide et rassurante », `casework/CONTEXT.md:78`) suit le même traitement.

Même remarque pour la règle des chiffres, nommée d'après l'écran — « la règle des chiffres que la
file applique » (`ChromeNavigation.cs:32`), « la règle des chiffres de la file » (`operator.css:186`).
Elle change de sujet, elle ne change pas de contenu.

## Le texte gelé de la clause d'incomplétude

`IncompletenessClause.cs` porte une clause de gouvernance qui impose un ADR — pas une PR — pour
toucher à la relation au `Manifest`. Cette section est cet ADR. Elle n'est pas une décision née
seule : c'est une conséquence directe du renommage, et la séparer obligerait un relecteur futur à
reconstituer le lien.

Le texte de `IncompletenessClause.cs:104` disait :

> « Ce dépistage n'est pas le recensement de votre paysage de données. Le recensement est le
> `Manifest`, et le `Manifest` se déclare à la main, système par système. »

Il portait les deux mots retirés à la fois, et un renommage mécanique aurait donné « Ce rapport de
détection n'est pas la configuration du microservice RGPD » — une phrase qui ne veut rien dire. C'est
une réécriture, pas un renommage. Le texte devient :

> **Ce rapport de détection ne recense pas vos systèmes : c'est vous qui les recensez, à la main,
> système par système, dans « Configuration du microservice RGPD ». La liste que vous y tenez ne
> garantit pas qu'il n'en existe pas d'autres.**

Et le titre de section qui le porte (`_IncompletenessClause.cshtml:78`) devient « Ce rapport de
détection et vos systèmes déclarés ».

### Deux prémisses fausses, corrigées, et qui ont changé la réponse

**Le lecteur n'est pas un client, c'est l'`Operator`.** `RelationToManifest` est rendu par
`_IncompletenessClause.cshtml:78-80`, inclus par `Report`, `Table`, `Archive` et `ArchivedTable` —
quatre écrans d'instruction. L'[ADR-0005](./0005-design-language-documente-police-embarquee-et-fichiers-statiques.md)
tient que le service n'a aucune page publique, et la lettre remise à la personne concernée
(`DeliveryLetter`) porte sa propre clause de non-exhaustivité. Le « votre » de la phrase vouvoie donc
l'`Operator` qui tient la liste.

**La contradiction sur « recensement » ne portait pas sur le lexème, mais sur la collocation.** Le
dépôt emploie « recensement » ~70 fois, dont `Manifest.cshtml:12` et `Case.cshtml:229` — l'écran du
`Manifest` s'appelle lui-même un recensement. Ce que la doctrine refuse (`ReadCaseQuery.cs:20`,
`DeliveryLetter.cs:236`) n'est pas le mot mais « l'autorité d'un recensement » et « présenté comme
complet ». Or la clause écrivait « Le recensement **est** le `Manifest` » — un défini d'identité, qui
confère précisément cette autorité.

D'où la solution : le nom défini part, le verbe reste, nié puis affirmé (« ne recense pas… c'est vous
qui les recensez »). Cela respecte la distinction nom/participe que `DeliveryLetter.cs:238` tient
délibérément, supprime l'autorité sans supprimer le mot, et le témoin l'autorise mécaniquement —
`ScreeningDepositScreen.cs:200-215` ne bannit « recensement » que dans `<title> <h1> <h2> <h3>
<button> <label> <a>`, ce qui nomme, jamais la prose d'un `<p>`.

### Pourquoi « liste », et pourquoi `Manifest` en clair disparaît

« liste » est retenu par précédent : `DeliveryLetter.cs:239` écrit déjà, dans la lettre remise à la
personne concernée, « Cette liste est celle des systèmes recensés par le responsable de traitement.
Elle ne garantit pas qu'il n'en existe pas d'autres. » Le service dira désormais la même phrase à
l'`Operator` et à la personne concernée — une cohérence qui se défend en contrôle.

Les autres mots tombent chacun pour une raison distincte : `catalogue` (156 emplois, désigne bien la
chose — refusé par le demandeur) ; `relevé` (200 emplois — bloqué, il désigne le collage de colonnes
dans `Screening` même) ; `déclaration` (126 emplois — piégé, en registre 2 il désigne *une* ligne) ;
`annuaire` (écarté, un annuaire est censé être complet — même défaut que « recensement ») ;
`recensement`, `inventaire`, `registre`, `périmètre`, `cartographie` (bloqués, voir ci-dessous).

L'identifiant `Manifest` en clair est retiré du texte — non parce qu'un client le lirait, mais parce
que depuis le renommage il désigne un écran qui ne porte plus ce nom. Un `Operator` qui cherche
« Manifest » dans la barre ne trouve que « Configuration du microservice RGPD ».

## Les mots bloqués, pour tout le renommage

Relevé pendant #210 et valable pour les trois mots. Écrit ici pour qu'un futur candidat ne repasse
pas par la même vérification.

| Mot | Bloqué par |
| --- | --- |
| **périmètre** | « le périmètre matériel de l'art. 20 » (`ReadHandler.cs:17`, `IAdapterCalls.cs:54`) ; « hors périmètre » de la qualification ; « le périmètre lu du dépistage » (`ReadScreeningTableHandler.cs:18`) |
| **registre** | RGPD art. 30 — le « registre des traitements » est un autre objet légal, fatal sur ce produit ; et les trois registres de langue de #186 |
| **cartographie** | règle écrite en dur : `Deposit.cshtml.cs:40` |
| **recensement** (le nom seul) | doctrine, deux fois : `ReadCaseQuery.cs:20`, `DeliveryLetter.cs:236` |
| **inventaire** | `IncompletenessClause.cs:250`, « des exemples, jamais un inventaire » |
| **surface** | pris dans le code, et écarté par préférence explicite du demandeur — on dit « interface utilisateur » |
| **relevé** | le collage de colonnes, dans `Screening` |

## Les réserves de vocabulaire, consignées et non corrigées

Six réserves ont été soulevées, écartées par le demandeur, et versées ici. Aucune n'est à rouvrir
isolément ; elles sont écrites pour que la relecture d'ensemble n'ait pas à être refaite.

1. **« microservice » entre dans le registre 2**, alors qu'aucun humain ne le lisait. Vérifié : le mot
   n'existait que comme namespace C# (`MicroserviceRgpd.Core.…`), registre 3 pur. Le poser sous le
   wordmark « Droits des personnes concernées » fait entrer un mot d'architecture dans la langue de
   l'`Operator`, et lui fait lire le nom du dépôt.
2. **`RGPD` est porté par deux entrées sur trois, dans deux sens différents** — « demandes RGPD »
   désigne les demandes régies par le règlement, « microservice RGPD » désigne le service. Élément
   partagé qui ne désigne pas une partie de la même chose. Deux effets de bord : « Détection des
   données personnelles », seule sans `RGPD`, se lit comme la moins RGPD des trois ; et le suffixe
   *-tion* apparie Configuration et Détection contre Tableau. *(Cette réserve requalifie la réserve
   n° 3 de #212, qui parlait d'« une entrée sur trois ».)*
3. **`schéma` est un homonyme.** Dans la phrase de la carte de Détection, il veut dire « la structure
   de la base » ; dans le code, c'est le premier niveau du triplet *schéma / table / colonne*
   (`ListedColumn.cs:59`, `RefusalCause.cs:77`, `ColumnListing.cs:136`). Par la règle de #186 le mot
   aurait été bloqué. L'alternative proposée — « un relevé de colonnes », le mot du dépôt — a été
   refusée.
4. **La seconde liste de l'écran des demandes n'est pas une demande, et on ne fait rien.** L'écran
   porte sous les demandes une section « À détruire — conservation échue » (`Queue.cshtml:154`) : des
   `EvidenceLog` de dossiers clos depuis cinq ans, sans personne, sans droit, sans délai. Sous un
   titre qui annonce « les demandes RGPD », la moitié basse de l'écran ne tient pas la promesse du
   titre. C'est le nouveau nom, plus précis, qui crée le problème — « file » ne promettait rien.
   Tranché : la section porte déjà son propre titre et sa propre clause, et le lecteur qui descend
   jusque-là comprend.
5. **La dissonance de la barre a changé de place, elle n'a pas disparu.** #211 croyait la réparer :
   « La file » et « Le paysage déclaré » nommaient des choses, « Le dépistage » un geste — d'où
   l'intrus. Aujourd'hui Détection nomme une activité, Configuration est ambiguë (c'est le coût 2
   ci-dessus), et Tableau est le seul objet concret. Deux activités et un objet, là où c'était deux
   objets et un geste.
6. **Le nom accessible des cartes d'accueil est long.** La carte entière étant le lien, un lecteur
   d'écran annonce « lien, Configuration du microservice RGPD, vous y déclarez à la main et un par
   un… » d'une traite. C'est correct et non trompeur — le nom vient en premier, la phrase le précise —
   mais c'est écrit ici pour être opposé le jour où l'accessibilité devient un chantier.

## Ce que les ADR actés gardent, et l'exemption qui le permet

Le retrait de « dépistage » passe par `RetiredVocabularyTests.RetiredTerms`, qui balaie le dépôt
entier, sans égard à la casse. Or `dépist-` vit dans trois ADR actés : `ADR-0004` (cinq emplois, plus
son titre et son nom de fichier), `ADR-0003` (trois emplois), `ADR-0005` (deux emplois) — sans compter
les renvois croisés qui les citent.

Décision : `docs/adr/` est exempté du balayage, et l'exemption est écrite en dur, ancrée au chemin,
sur le modèle de `FilesSpeakingThePreRenameSchema`.

Le motif est le même que celui des migrations, et il est déjà la doctrine du dépôt : un ADR acté
décrit une décision datée, et parle avec les mots de sa date. Réécrire `ADR-0004` — « Moteur de
dépistage en C# sans second sidecar » — lui ferait décrire une décision qui n'a jamais été prise sous
ce nom, et son nom de fichier est cité par `ADR-0003:193` et `docs/contexts/screening/CONTEXT.md:96`.
Comme pour un `.Designer.cs`, le garde pousserait sinon chaque contributeur à retoucher de l'histoire
immuable.

⚠️ **L'équivalence de lecture, notée une fois ici et valable pour tous les ADR antérieurs** :
« dépistage » dans un ADR d'avant le 2026-08-15 désigne ce que l'interface appelle désormais
« Détection des données personnelles », et « paysage déclaré » ce qu'elle appelle « Configuration du
microservice RGPD ».

Cet ADR-ci est couvert par l'exemption qu'il institue — il porte forcément les mots retirés, faute de
quoi il ne saurait pas dire ce qu'il retire. C'est la même logique que le garde qui s'exclut de son
propre balayage. L'exemption ne s'étend à rien d'autre : `docs/contexts/`, `docs/spec/`, `docs/api/`,
`exploration/`, `src/`, `tests/` et `CONTEXT-MAP.md` restent balayés sans tolérance.

## Une extension du design language de l'ADR-0005

L'écran d'accueil porte trois cartes dont la carte entière est un lien — une porte, pas la carte d'un
système déclaré : même peau (fond, filet, rayon, marge), nature différente, et aucune cible
secondaire à l'intérieur.

Le service acquiert là son premier état interactif stylé, et c'est une extension de
l'[ADR-0005](./0005-design-language-documente-police-embarquee-et-fichiers-statiques.md) au même
titre que la sémantique d'erreur, le mode sombre et le geste destructeur. La feuille de 606 lignes ne
contient aujourd'hui pas une seule déclaration `:hover`, `:focus`, `:focus-visible`, `outline` ou
`transition`, et aucune règle `a` hors de `nav.chrome` : les liens du service sont les liens bleus
soulignés du navigateur, et son seul état dynamique est `button.primary:active`.

Ce que l'extension autorise, et rien de plus :

- **survol** : le filet passe à l'accent. Rien d'autre ne change ;
- **focus visible** : un contour d'accent, avec décalage.

Pas d'ombre qui apparaît, pas d'élévation, pas de translation, pas de transition — un écran
d'instruction ne doit pas bouger sous la souris, et `--elevation-soft` reste inutilisé. L'ADR-0005
décrivait un site de vente dont les états de survol sont ceux du marketing ; celui-ci n'en est pas
un.

Aucun token nouveau, aucune valeur nouvelle sur `:root`. La disposition emploie la grille
auto-ajustée qui est déjà le seul idiome responsive du dépôt (`.clause`), donc aucune requête média —
le premier écran à sortir de la colonne unique le fait sans introduire un point de rupture.

## Conséquences

### Ce qui s'améliore

- Le service a une porte. Un humain qui tape l'adresse racine reçoit un écran, non un
  `application/problem+json`.
- Les trois noms disent sur quoi ils portent. Chacun nomme son objet là où deux sur trois nommaient
  une image (« paysage ») ou un geste (« dépistage »).
- L'ordre porte le sens que les noms ne portent pas : `Configuration → Détection → Tableau` dit par la
  position que la configuration est le point de départ.
- Une règle de forme unique remplace trois politiques divergentes : plus aucune forme courte, nulle
  part.
- « Sensibilité » devient univoque dans tout le dépôt — l'article 9, et rien d'autre.
- Le service tient le même langage des deux côtés : la phrase de non-exhaustivité dite à l'`Operator`
  est mot pour mot celle dite à la personne concernée.

### Ce qu'on paie

- **La distinction *signale ≠ conclut* n'est plus portée par le nom** du contexte de détection. Elle
  ne survit que par les clauses, la phrase de la carte et les mécanismes — trois amortisseurs, aucune
  garantie lexicale. C'est la perte principale de cette décision.
- Le déclaratif et l'incomplétude ne sont plus portés par le nom du `Manifest`, et la clause
  `Aucune modification vers le Manifest` perd son évidence.
- La barre passe à la ligne sur un écran étroit, et la clause « trois entrées se lisent d'un coup
  d'œil » est érodée.
- Un mot d'architecture entre dans la langue de l'`Operator` (« microservice »).
- Six réserves de vocabulaire restent ouvertes, listées ci-dessus, toutes écartées par le demandeur.
- `/depistage` casse. Adresse morte en 404, sans redirection.
- Un garde acquiert une exemption : `docs/adr/` sort du balayage du vocabulaire retiré.

### Ce qui ne change pas

- Les identifiants C# et le contrat public (`system_id`, `docs/api/adapter.md`).
- Le corpus annoté : aucun des trois mots n'y figure, aucun ré-annotage n'est dû.
- Les clauses de doctrine, qui changent de sujet sans changer de contenu.
- Le registre 3 : « un catalogue est un paysage de quelques systèmes » reste juste et reste écrit.
- Les listes `_Avoid_` des glossaires, qui portent sur l'anglais.

### Ce que cette décision n'ouvre pas

- La passe générale sur le vocabulaire français — les clauses du registre 3, le mot `Chrome` de
  `ChromeNavigation`, le mot `Operator`. Ce sont des clauses et des identifiants, pas des noms
  d'écran.
- Une page publique destinée aux personnes concernées : l'ADR-0005 l'a écartée, la rouvrir serait
  rouvrir l'ADR-0005.
- Tout chiffre, compteur ou tableau de bord sur l'accueil. La seule dérogation à la règle « aucun
  chiffre » est étroite : les références d'articles du RGPD dans la phrase du seuil, qui ne sont ni un
  compteur ni une mesure du service.
- La construction elle-même, qui est spécifiée par l'issue `ready-for-agent` qui accompagne cet ADR.

## Suite — le garde de vocabulaire retiré a été supprimé (2026-08-16)

⚠️ **`RetiredVocabularyTests` n'existe plus.** Le test a été retiré du dépôt le 2026-08-16, sur
décision du demandeur, le lendemain de cet ADR. Deux passages ci-dessus décrivent donc un dispositif
qui n'existe plus, et sont laissés tels quels parce qu'un ADR acté ne se réécrit pas :

- « Ce que les ADR actés gardent, et l'exemption qui le permet » — tout ce qui y est dit du balayage,
  de la table `RetiredTerms` et de l'exemption `docs/adr/` est sans objet. Plus rien ne balaie, donc
  plus rien n'a besoin d'être exempté.
- Conséquences → « Ce qu'on paie », la ligne *« Un garde acquiert une exemption »* — le coût annoncé
  n'a pas été payé, faute de garde.

**Ce que la suppression emporte réellement.** Le garde était le seul test du dépôt à chercher des mots
dans de la prose. Les autres gardes d'architecture — `ContextIsolationTests`,
`NothingRunsInTheBackgroundTests`, `ContextlessTypeTests` — lisent le code compilé, où le compilateur
sert déjà de second filet ; `ContextRosterTests` vérifie une présence, pas une absence. Les dix
clauses de doctrine qui n'existent que dans la documentation, les ADR, les commentaires XML et les
messages d'échec de test n'ont donc plus aucune surveillance. Le risque que le garde avait été écrit
pour couvrir — *le vocabulaire se défait ligne à ligne au fil des PR, et personne ne le voit* —
redevient entier.

Le retrait de « dépistage » n'a plus de mécanisme. La section ci-dessus dit que ce retrait « passe par
`RetiredVocabularyTests.RetiredTerms` ». Il ne passe plus par rien : il tient aux tests d'écran, qui
gèlent les libellés qu'ils vérifient, et à la relecture.

⚠️ **La relecture seule a déjà laissé passer un emploi.** Le renommage du vocabulaire d'écran du
contexte de détection a été livré et clos le 2026-08-16 — le jour même de cette suite — et
`Table.cshtml:270` porte toujours, dans un commentaire Razor, le mot crié en majuscules : *« un
constat sur le DÉPISTAGE, jamais sur la donnée »*. C'est exactement le cas que le garde citait pour
justifier qu'il cherche sans égard à la casse, et exactement le cas qu'une relecture humaine ne voit
pas — un commentaire, invisible à l'écran comme au compilateur. Il a survécu au ticket qui devait
l'emporter, et plus rien ne le signale.

Restent donc vivants : ce commentaire, et neuf fichiers d'`exploration/` — seize occurrences —
qu'aucun ticket ne couvrait.

Ce qui reste vrai et ne dépendait pas du garde :

- L'équivalence de lecture énoncée plus haut. C'est une clé de lecture, pas un mécanisme — elle vaut
  indépendamment de tout test.
- Le motif de fond de l'exemption : un ADR acté décrit une décision datée et parle avec les mots de
  sa date. Il ne cesse pas d'être vrai parce que plus rien ne l'applique, et il redeviendra la règle
  si un garde équivalent est un jour réécrit.
- Le tableau des mots bloqués et les réserves de vocabulaire : ce sont des relevés, pas des gardes.

## Suite — cet ADR est supplanté sur trois points, par trois ADR distincts (2026-08-20)

⚠️ **Rien de ce qui précède n'a été édité**, et rien ne le sera : on supplante un ADR, on ne le
réécrit pas. Cette section ne fait que nommer les trois points qui ne valent plus, pour qu'un lecteur
de l'ADR-0006 ne les tienne pas pour vivants faute d'ouvrir la carte des contextes. Les deux premiers
étaient jusqu'ici consignés dans les ADR qui supplantent et dans `CONTEXT-MAP.md` seulement.

- **Le mot `Chrome` de `ChromeNavigation`**, rangé par « Ce que cette décision n'ouvre pas » parmi ce
  qui restait fermé — supplanté par
  l'[ADR-0007](./0007-le-cadre-partage-des-ecrans-se-nomme-layout.md) (2026-08-17), qui le sort du
  dépôt et nomme `Layout` le cadre partagé.
- **La réserve n° 1 et la règle « aucune forme courte, nulle part »** — supplantées par
  l'[ADR-0008](./0008-le-service-se-nomme-microservice-rgpd-et-la-configuration-porte-deux-noms.md)
  (2026-08-17), qui donne son nom au service et deux noms à l'écran de configuration.
- **La clause « Trois entrées, et pas une quatrième »** — celle qui tient le compte des points
  d'entrée à trois, écrite ici sous la forme *« une barre à trois entrées se lit d'un coup d'œil quand
  une barre à quatre se parcourt »* puis reprise en conséquence. Supplantée par
  l'[ADR-0010](./0010-un-quatrieme-point-d-entree-la-qualification-a-sa-surface.md) (2026-08-20), qui
  ouvre un quatrième point d'entrée pour la `Qualification`. Le motif de la clause était la lisibilité
  d'une barre horizontale ; l'ADR-0009 a retiré la barre, et le motif a disparu avec la forme qui le
  portait.

Tout le reste de cet ADR reste en vigueur — les trois noms français des points d'entrée, les
identifiants C# inchangés, le texte gelé de la clause d'incomplétude, le tableau des mots bloqués.

## Suite — `Manifest` quitte la liste des identifiants inchangés (2026-09-11)

⚠️ **Rien de ce qui précède n'a été édité.** L'[ADR-0016](./0016-le-manifest-cede-la-place-au-parametrage-un-droit-une-adresse.md)
supplante cet ADR sur un quatrième point : `Manifest`, dans « Les identifiants C# ne bougent pas ».
Le type et l'écran à `/manifest` ont été supprimés, et non renommés ; le Paramétrage les remplace.
Le motif de la clause — le contrat public, `system_id` — reste entier, et les autres identifiants
de la liste ne bougent pas. L'écran de configuration, lui, ne se nomme plus « Configuration du
microservice RGPD » : il se nomme « Paramétrage » (voir la suite de l'ADR-0008).
