# Référentiels existants de catégories de données personnelles

_Recherche pour l'issue [#123](https://github.com/AmauryTISSOT/microservice_rgpd/issues/123), enfant de la carte [#122](https://github.com/AmauryTISSOT/microservice_rgpd/issues/122). 8 août 2026._

**Ce document ne tranche pas la taxonomie.** Il rassemble la matière : ce que chaque référentiel
énumère, en quoi ils divergent, et quelles questions cette divergence pose au ticket de taxonomie.

**Cadre de lecture, hérité de la carte #122.** Le service ne lit qu'un **schéma SQL** — noms de
tables, de colonnes, types, commentaires — **jamais des valeurs**. La taxonomie visée est
**fermée**, rend **une catégorie par colonne**, et **n'est pas raccrochée** aux `DataSubjectRight`
(art. 15-21). Tout référentiel examiné ici est donc jugé sur un critère qu'aucun de ses auteurs
n'avait en tête : *est-il énonçable depuis un nom de colonne ?*

---

## 1. Le RGPD lui-même — les deux seules listes de droit dur

### 1.1 Ce que le règlement énumère effectivement

Le RGPD **n'énumère nulle part les catégories de données personnelles**. Il définit la notion en
extension nulle et en compréhension maximale — [art. 4, point 1](https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=CELEX:32016R0679) :

> «données à caractère personnel», toute information se rapportant à une personne physique
> identifiée ou identifiable […] notamment par référence à un identifiant, tel qu'un nom, un
> numéro d'identification, des données de localisation, un identifiant en ligne, ou à un ou
> plusieurs éléments spécifiques propres à son identité physique, physiologique, génétique,
> psychique, économique, culturelle ou sociale

Les six adjectifs finaux — physique, physiologique, génétique, psychique, économique, culturelle,
sociale — sont **une énumération de facettes de l'identité, pas une nomenclature de catégories de
données**. Ils sont introduits par « notamment » : la liste est explicitement ouverte.

Le règlement n'écrit que **deux listes fermées**, et elles ne servent pas à décrire un patrimoine
de données : elles déclenchent une **interdiction** ou une **réserve d'autorité publique**.

### 1.2 Article 9, paragraphe 1 — les catégories particulières

Texte exact ([EUR-Lex, version française](https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=CELEX:32016R0679)) :

> Le traitement des données à caractère personnel qui révèle l'origine raciale ou ethnique, les
> opinions politiques, les convictions religieuses ou philosophiques ou l'appartenance syndicale,
> ainsi que le traitement des données génétiques, des données biométriques aux fins d'identifier
> une personne physique de manière unique, des données concernant la santé ou des données
> concernant la vie sexuelle ou l'orientation sexuelle d'une personne physique sont interdits.

Décompte utile pour une taxonomie — la phrase porte **huit items**, répartis en deux régimes
grammaticaux qu'il faut distinguer :

| # | Item | Régime |
|---|------|--------|
| 1 | Origine raciale ou ethnique | données « **qui révèlent** » |
| 2 | Opinions politiques | données « qui révèlent » |
| 3 | Convictions religieuses ou philosophiques | données « qui révèlent » |
| 4 | Appartenance syndicale | données « qui révèlent » |
| 5 | Données génétiques | catégorie **en propre** (déf. art. 4, 13) |
| 6 | Données biométriques **aux fins d'identifier de manière unique** | catégorie en propre, **conditionnée par la finalité** (déf. art. 4, 14) |
| 7 | Données concernant la santé | catégorie en propre (déf. art. 4, 15) |
| 8 | Vie sexuelle **ou** orientation sexuelle | données « qui révèlent » |

Trois observations qui pèsent directement sur un détecteur de schéma :

- **Les quatre premiers items et le huitième ne sont pas des types de données, mais un effet.** Le
  texte vise ce qui « révèle » l'origine, l'opinion, la conviction. Une colonne `commune_naissance`
  ou `nom_association` peut révéler l'origine ethnique ou l'appartenance syndicale sans être nommée
  pour cela. Aucun nom de colonne ne porte cet effet de façon fiable — c'est le contexte du
  traitement qui le porte.
- **Le biométrique est conditionné par la finalité, pas par le type.** [Considérant 51](https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=CELEX:32016R0679) :
  « Le traitement des photographies ne devrait pas systématiquement être considéré comme
  constituant un traitement de catégories particulières […] étant donné que celles-ci ne relèvent
  de la définition de données biométriques que lorsqu'elles sont traitées selon un mode technique
  spécifique permettant l'identification ou l'authentification unique ». Une colonne `photo_url`
  n'est donc **pas** de l'art. 9 par elle-même. Un détecteur de schéma ne peut pas trancher ce
  point ; au mieux il le signale.
- **La santé est définie très largement.** [Considérant 35](https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=CELEX:32016R0679)
  y range « un numéro, un symbole ou un élément spécifique attribué à une personne physique pour
  l'identifier de manière unique **à des fins de santé** », ainsi que tout ce qui concerne « une
  maladie, un handicap, un risque de maladie, les antécédents médicaux, un traitement clinique ou
  l'état physiologique ou biomédical ». Un identifiant patient est donc une donnée de santé.

### 1.3 Article 10 — condamnations pénales et infractions

Texte exact ([EUR-Lex](https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=CELEX:32016R0679)) :

> Le traitement des données à caractère personnel relatives aux condamnations pénales et aux
> infractions ou aux mesures de sûreté connexes fondé sur l'article 6, paragraphe 1, ne peut être
> effectué que sous le contrôle de l'autorité publique, ou si le traitement est autorisé par le
> droit de l'Union ou par le droit d'un État membre qui prévoit des garanties appropriées pour les
> droits et libertés des personnes concernées. Tout registre complet des condamnations pénales ne
> peut être tenu que sous le contrôle de l'autorité publique.

**L'art. 10 est une catégorie distincte, pas un neuvième item de l'art. 9.** Son régime est
différent : l'art. 9 pose une interdiction levable par dérogations énumérées ; l'art. 10 pose une
réserve de contrôle par l'autorité publique. Une taxonomie qui fusionnerait les deux sous
« données sensibles » écraserait une différence que le règlement a délibérément écrite en deux
articles séparés.

### 1.4 En quoi ces deux listes ont une portée que les autres n'ont pas

Trois raisons, et elles sont d'ordre différent des raisons d'ingénierie :

1. **Elles sont fermées par le législateur.** Contrairement à `infoTypes` ou `entity types`, on ne
   peut ni en ajouter ni en retirer un item par décision de produit. Les huit items de l'art. 9
   sont exhaustifs, la CJUE en interprète les contours, personne ne les étend.
2. **Elles emportent des conséquences juridiques mécaniques.** Interdiction de principe (art. 9,
   § 1) ; base légale spécifique parmi les dérogations du § 2 ; et, en aval, l'art. 35, § 3, b)
   fait du « traitement à grande échelle de catégories particulières de données visées à
   l'article 9, paragraphe 1, ou de données à caractère personnel relatives à des condamnations
   pénales et à des infractions visées à l'article 10 » un cas où l'AIPD est **obligatoire**.
   Aucune catégorie de Presidio, DLP ou Macie n'a d'effet de droit.
3. **Elles nomment un risque, pas un format.** Le [considérant 75](https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=CELEX:32016R0679)
   énumère les dommages redoutés — « discrimination », « vol ou usurpation d'identité », « perte
   financière », « atteinte à la réputation » — et c'est ce registre de risque, pas la forme
   syntaxique de la donnée, qui commande la liste. Les outils de détection, eux, énumèrent des
   **formats reconnaissables** ; c'est la divergence de fond, développée au § 5.

### 1.5 Ce que le RGPD dit du registre — et qu'il ne nomme aucune catégorie

L'[art. 30, § 1, c)](https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=CELEX:32016R0679)
exige du registre « une description des **catégories** de personnes concernées et des **catégories**
de données à caractère personnel ». **Le règlement impose l'exercice de catégorisation sans en
fournir le vocabulaire.** Toute nomenclature de catégories « ordinaires » — état civil, coordonnées,
vie professionnelle — est donc **doctrine, outil ou usage**, jamais du droit dur. C'est
exactement l'espace que la CNIL occupe (§ 2) et que les outils occupent autrement (§ 3-4).

---

## 2. La CNIL

### 2.1 Le modèle de registre simplifié — la liste la plus proche de notre besoin

Page : [RGPD — le registre des activités de traitement](https://www.cnil.fr/fr/RGPD-le-registre-des-activites-de-traitement).
Modèle simplifié :
[`registre-traitement-simplifie.ods`](https://www.cnil.fr/sites/cnil/files/atoms/files/registre-traitement-simplifie.ods).
Le classeur a été décompressé et son `content.xml` lu : les libellés ci-dessous sont exacts au
caractère près, coquille comprise.

Onglet « 3 - Modèle de fiche de registre », bloc **« Catégories de données personnelles
concernées »** — **six lignes**, chacune en regard d'une colonne « Description » et d'une colonne
« Durée de conservation » :

1. `État civil, identité, données d'identification, images…`
2. `Vie personnelle (habitudes de vie, situation familiale, etc.)`
3. `Informations d'ordre économique et financier (revenus, situation financière, situation fiscale, etc.)`
4. `Données de connexion (adress IP, logs, etc.)` — *sic*
5. `Données de localisation (déplacements, données GPS, GSM, etc.)`
6. `Numéro de Sécurité Sociale (ou NIR)` — commentaire de cellule : « Cf. article 87 du règlement
   qui prévoit des règles nationales spécifiques pour cette donnée. Numéro INSEE ou numéro de
   Sécurité Sociale. »

Puis un **second bloc, distinct**, intitulé **« Données sensibles »** — **neuf lignes** :

1. `Données révélant l'origine raciale ou ethnique`
2. `Données révélant les opinions politiques`
3. `Données révélant les convictions religieuses ou philosophiques`
4. `Données révélant l'appartenance syndicale`
5. `Données génétiques`
6. `Données biométriques aux fins d'identifier une personne physique de manière unique`
7. `Données concernant la santé`
8. `Données concernant la vie sexuelle ou l'orientation sexuelle`
9. `Données relatives à des condamnations pénales ou infractions`

**Trois enseignements de structure, et ils sont importants.**

- **La CNIL sépare en deux blocs ce qu'un outil de détection met sur un seul plan.** Les six
  catégories « ordinaires » et les neuf items sensibles ne sont pas la même liste : ce sont deux
  axes cochés en parallèle. Une colonne peut donc être `État civil` *et* signalée sensible.
- **Les huit items de l'art. 9 sont repris mot pour mot, sans agrégation ni découpage.** Ni fusion
  en « données sensibles », ni éclatement.
- **L'art. 10 est ajouté comme neuvième ligne du bloc**, mais le commentaire d'aide du fichier
  maintient la distinction : « Les données d'infraction ou de condamnation pénale font également
  l'objet de règles particulières » — *également*, donc à côté, pas dedans. Le régime différent est
  préservé jusque dans un tableur.

**Absents du modèle simplifié** : pas de ligne « Vie professionnelle », pas de ligne
« Internet/cookies ». Elles existaient dans l'ancien modèle (§ 2.3).

L'onglet « 2 - Liste des traitements », lui, ne porte qu'une colonne booléenne
`Données sensibles ? Oui/non` — c'est-à-dire, à ce niveau de synthèse, **une seule valeur pour tout
l'art. 9 réunis**. La CNIL emploie donc les deux granularités selon le grain du document (cf. § 7).

### 2.2 Liste fermée ou champ libre ? La réponse est « ni l'un ni l'autre »

Point décisif pour notre taxonomie, vérifié dans le XML du classeur :

- Les quinze libellés sont des **cellules de texte statiques**, **pas** des listes déroulantes. Les
  seules validations fermées du classeur (`val1`…`val12`) portent sur *Catégories de personnes
  concernées*, *Destinataires*, *Mesures de sécurité*, *Pays* et *Type de garanties* — **aucune
  n'est attachée aux catégories de données**.
- Aucune protection de feuille (`table:protected`) : l'utilisateur peut éditer les libellés et
  insérer des lignes.
- La colonne réellement exploitée est **« Description »**, en **texte libre**. L'onglet d'exemple
  « Gestion de la paie » y écrit `Noms, prénoms, adresses`, `RIB`,
  `Numéros de sécurité sociale des salariés`.

**Le modèle CNIL est donc une grille de six catégories fixes servant de guide de remplissage, dont
la valeur informative réelle vit dans un champ libre adjacent.** Ce n'est pas une taxonomie fermée,
et ce n'est pas un champ libre nu : c'est un **cadre d'amorçage**. Il n'a pas à trancher les cas
limites parce qu'un humain écrit la description à côté — **luxe que notre taxonomie fermée à une
valeur par colonne n'a pas.** C'est la limite principale de la transposition directe de cette liste.

### 2.3 L'ancien modèle, lui, a une valeur de repli

L'ancien modèle, toujours téléchargeable
([`registre_rgpd_basique.pdf`](https://www.cnil.fr/sites/default/files/atoms/files/registre_rgpd_basique.pdf)),
présente une **liste à cocher de huit items** :

`☐ État-civil, identité, données d'identification, images` · `☐ Vie personnelle` ·
`☐ Vie professionnelle (ex. CV, situation professionnelle, scolarité, formation, distinctions, diplômes, etc.)` ·
`☐ Informations d'ordre économique et financier` · `☐ Données de connexion` ·
`☐ Données de localisation` ·
`☐ Internet (ex. cookies, traceurs, données de navigation, mesures d'audience, …)` ·
**`☐ Autres catégories de données (précisez) :`**

**C'est la seule nomenclature française publiée qui porte explicitement une valeur de repli**, et
c'est la plus proche de la forme « taxonomie fermée + échappatoire » que la carte #122 doit
trancher. Réserve : ce PDF range le NIR parmi les « données particulièrement sensibles » de sa
question binaire — formulation plus lâche que celle du `.ods` récent et que la doctrine actuelle de
la CNIL (§ 2.5). **Le `.ods` est la version corrigée ; l'ancien PDF n'est à citer que pour sa
structure, pas pour sa qualification du NIR.**

### 2.4 Les référentiels sectoriels raisonnent par finalité, pas par type

Les [référentiels sectoriels](https://www.cnil.fr/fr/autres-referentiels), qui remplacent les
anciennes normes simplifiées, listent tous leurs « données traitées » — mais **avec un découpage
entièrement différent de celui du registre**.

Le [référentiel Gestion des ressources humaines](https://www.cnil.fr/sites/cnil/files/2023-09/referentiel_gestion_des_ressources_humaines.pdf)
(ex-NS-46) emploie un tableau **« Catégories de données » / « Exemples de données »** où la
« catégorie » est en réalité une **finalité** : *Identification de l'employé*, *Suivi de la carrière
et de la formation*, *Établissement des fiches de paie*… Un second niveau nomme ensuite les blocs
de champs : *Données relatives à l'identité*, *Données relatives à la situation professionnelle*,
*Données relatives au titre valant autorisation de travail*, *Évaluation professionnelle*, *Suivi
administratif des visites médicales*…

Le [référentiel Alertes professionnelles](https://www.cnil.fr/sites/cnil/files/2023-07/referentiel_alertes_professionnelles.pdf)
découpe **par rôle** : identité, fonctions et coordonnées de l'émetteur de l'alerte, de la personne
visée, des personnes intervenant dans le recueil, puis faits signalés et suites données. Voir aussi
le [référentiel Gestion des activités commerciales](https://www.cnil.fr/sites/cnil/files/atoms/files/referentiel_traitements-donnees-caractere-personnel_gestion-activites-commerciales.pdf)
(ex-NS-48).

**Conclusion, et c'est une réponse frontale à la question du ticket : il n'existe aucune nomenclature
transversale unique chez la CNIL.** Le registre découpe par **type de donnée**, les référentiels
découpent par **finalité** ou par **rôle de la personne concernée**, et les deux découpages sont
incompatibles. Une même colonne `date_visite_medecine_travail` est *Vie personnelle* au registre,
et *Suivi administratif des visites médicales* dans le référentiel RH.

Cela a une conséquence directe : **le découpage par finalité est hors de portée d'un détecteur de
schéma.** La finalité n'est pas dans le nom de la colonne, elle est dans le traitement. Des deux
découpages CNIL, un seul nous est transposable.

### 2.5 Données sensibles, NIR et infractions : trois régimes, pas un

La [définition CNIL de « donnée sensible »](https://www.cnil.fr/fr/definition/donnee-sensible)
reprend exactement les huit items de l'art. 9 — **ni le NIR, ni les données d'infraction n'y
figurent**. La page
[Focus sur certaines catégories de données personnelles](https://cnil.fr/fr/recherche-scientifique-hors-sante/focus-certaines-categories-donnees-personnelles)
est la plus explicite : elle traite en **trois blocs séparés**, chacun avec son régime propre, (1)
les données sensibles de l'art. 9, (2) le **NIR**, (3) les **condamnations pénales, infractions et
mesures de sûreté connexes**.

**Le NIR n'est pas une donnée sensible au sens de l'art. 9.** Son encadrement est franco-français :
l'[art. 30 de la loi Informatique et Libertés](https://www.legifrance.gouv.fr/loda/article_lc/LEGIARTI000038886929)
renvoie à un décret en Conseil d'État, pris après avis motivé et publié de la CNIL, qui détermine
les catégories de responsables et les finalités autorisant l'usage du NIR ou la consultation du
RNIPP. Ce décret est le
[« décret cadre NIR » n° 2019-341 du 19 avril 2019](https://www.legifrance.gouv.fr/jorf/id/JORFTEXT000038396526/),
qui énumère les usages autorisés par secteur. La CNIL le commente
[pour le secteur social](https://www.cnil.fr/fr/numero-dinscription-des-personnes-dans-le-secteur-social-le-decret-cadre-nir-en-questions)
et [pour la santé](https://www.cnil.fr/fr/tout-savoir-sur-le-decret-cadre-nir-dans-le-champ-de-la-sante).

**C'est le point où le droit français ajoute une catégorie que le RGPD n'a pas écrite** — et le
`.ods` de la CNIL en fait, très logiquement, une **septième ligne de son bloc ordinaire**, ni
sensible ni banale. Pour un détecteur de schéma français, `num_secu` / `nir` / `insee` est
simultanément la catégorie la plus détectable morphologiquement et l'une des plus lourdes de
conséquences. Une taxonomie qui la noierait dans « identifiant national » perdrait le régime.

### 2.6 Autres nomenclatures signalées, non creusées

- **EDPB** — aucune nomenclature de catégories de données ; seulement des lignes directrices
  thématiques ([index](https://www.edpb.europa.eu/our-work-tools/general-guidance/guidelines-recommendations-best-practices_fr)).
- **ISO/IEC 19944-1:2020**, *Cloud computing and distributed platforms — Data flow, data categories
  and data use* — porte une véritable taxonomie de catégories de données
  ([notice ISO](https://www.iso.org/standard/79573.html)). **Norme payante, non consultée** :
  signalée comme piste, pas comme source.
- **NIST Privacy Framework** — approche par fonctions et catégories de contrôle, pas par types de
  données ([page](https://www.nist.gov/privacy-framework)).

---

## 3. Microsoft Presidio

### 3.0 Avertissement de source

`microsoft.github.io/presidio` **redirige en 301** vers `data-privacy-stack.github.io/presidio`,
lui-même redirigé vers **<https://presidio.dataprivacystack.org/>** : le projet est passé sous
l'organisation « Data Privacy Stack ». Les URL `github.com/microsoft/presidio` continuent de servir
le code par redirection de transfert de dépôt. Constaté le 8 août 2026 ; toute référence à
« Microsoft Presidio » dans une doc de plus d'un an est à revérifier.

### 3.1 Une liste plate d'entités, sans familles

Liste officielle : [supported entities](https://presidio.dataprivacystack.org/supported_entities/).

**Entités globales** : `CREDIT_CARD`, `CRYPTO`, `DATE_TIME`, `EMAIL_ADDRESS`, `IBAN_CODE`,
`IP_ADDRESS`, `MAC_ADDRESS`, `NRP`, `LOCATION`, `PERSON`, `PHONE_NUMBER`, `MEDICAL_LICENSE`, `URL`.
Deux entités existent dans le code sans figurer au tableau de la doc : `UUID`
([`generic/uuid_recognizer.py`](https://github.com/microsoft/presidio/tree/main/presidio-analyzer/presidio_analyzer/predefined_recognizers/generic))
et `ABA_ROUTING_NUMBER`.

**Il n'y a aucune taxonomie en familles.** Pas de hiérarchie, pas de champ « catégorie » sur
`EntityRecognizer`, pas de regroupement « identifiants d'État / financiers / santé ». Le seul axe
de regroupement du code est le `country_code` ISO 3166-1 alpha-2, ajouté pour permettre
`load_predefined_recognizers(countries=[...])`. Presidio est donc, des trois outils, **le plus
plat** : Macie a trois catégories, Google trois axes, Presidio zéro.

### 3.2 La France : rien

Réponse nette, vérifiée sur le
[répertoire `country_specific`](https://github.com/microsoft/presidio/tree/main/presidio-analyzer/presidio_analyzer/predefined_recognizers/country_specific)
qui contient 18 pays (`australia`, `canada`, `finland`, `germany`, `india`, `italy`, `korea`,
`nigeria`, `philippines`, `poland`, `singapore`, `south_africa`, `spain`, `sweden`, `thai`,
`turkey`, `uk`, `us`) :

- **Aucun répertoire `france`**, aucune entité `FR_*`. **Pas de `FR_NIR`** — le sujet le mentionnait
  comme hypothèse, elle est fausse.
- **Pas de SIREN, pas de SIRET**, pas de CNI, pas de plaque, pas de code postal français. À
  comparer avec les identifiants d'entreprise qui existent pour d'autres pays : `AU_ABN`, `AU_ACN`,
  `SG_UEN`, `SE_ORGANISATIONSNUMMER`, `DE_HANDELSREGISTER`.
- La seule chose française du code est le **pattern IBAN FR**, noyé dans le dictionnaire de ~75
  patterns pays de `generic/iban_patterns.py`, et il produit l'entité générique `IBAN_CODE`, pas
  `FR_IBAN`.
- Le registre par défaut ne charge que `supported_languages: [en]`. La
  [doc des langues](https://presidio.dataprivacystack.org/analyzer/languages/) confirme : « In its
  default configuration, it contains recognizers and models for English. »

À titre de contraste, l'Allemagne a neuf entités (`DE_TAX_ID`, `DE_TAX_NUMBER`, `DE_PASSPORT`,
`DE_ID_CARD`, `DE_SOCIAL_SECURITY`, `DE_HEALTH_INSURANCE`, `DE_KFZ`, `DE_HANDELSREGISTER`,
`DE_PLZ`) et le Royaume-Uni six. **La France est le grand pays européen le moins couvert du
catalogue.** Aucune taxonomie française n'est donc à récupérer chez Presidio ; il n'en offre que la
mécanique.

### 3.3 Ce que fait un recognizer

Modèle typique, `country_specific/us/us_ssn_recognizer.py` :

```python
PATTERNS = [
    Pattern("SSN1 (very weak)", r"\b([0-9]{5})-([0-9]{4})\b", 0.05),
    ...
    Pattern("SSN5 (medium)", r"\b([0-9]{3})[- .]([0-9]{2})[- .]([0-9]{4})\b", 0.5),
]
CONTEXT = ["social", "security", "ssn", "ssns", "ssid"]
```

Chaque pattern porte **son propre score**, de 0,05 (« very weak ») à 0,5 (« medium ») ; les mots de
`CONTEXT` présents autour du match **remontent** le score via le `LemmaContextAwareEnhancer`.
`medical_license_recognizer.py` ajoute une `validate_result()` qui applique un **Luhn** et
invalide le résultat en cas d'échec.

**C'est le précédent le plus intéressant pour la décision de cadrage 5 de la carte #122** (« le
degré de doute est dérivé de la règle qui a déclenché »). Presidio ne demande pas à un modèle
d'évaluer sa propre confiance : **le score est écrit à la main sur la règle**, pattern par pattern,
puis modulé par des mécanismes déterministes — contexte lexical, checksum. C'est exactement
l'architecture que la carte a choisie, déjà éprouvée en production ailleurs.

### 3.4 L'article 9 chez Presidio : `NRP` et le bloc médical opt-in

Presidio **n'a aucune notion formelle de l'art. 9** : pas de drapeau « catégorie particulière », pas
de renvoi au règlement. La couverture est incidente.

**`NRP`** — documenté comme « **Nationality, religious or political group** ». Ce n'est **pas** une
regex : c'est une sortie du modèle NER. `nlp_engine/ner_model_configuration.py` mappe `NORP="NRP"`,
`NORP` étant le label spaCy standard *Nationalities Or Religious or Political groups*. Une entité
unique recouvre donc **trois items distincts de l'art. 9** (origine ethnique, convictions
religieuses, opinions politiques), agrégés non par analyse juridique mais **parce que le modèle
spaCy amont les avait déjà fusionnés**. C'est le cas d'école de la granularité subie plutôt que
choisie.

**Santé** — attention au piège : `MEDICAL_LICENSE` est un **numéro de licence de praticien** (DEA),
donc une donnée d'identification du professionnel, pas une donnée de santé du patient. Le seul vrai
bloc art. 9 santé est produit par le `MedicalNERRecognizer` (`MEDICAL_DISEASE_DISORDER`,
`MEDICAL_MEDICATION`, `MEDICAL_THERAPEUTIC_PROCEDURE`, `MEDICAL_CLINICAL_EVENT`,
`MEDICAL_BIOLOGICAL_ATTRIBUTE`, `MEDICAL_BIOLOGICAL_STRUCTURE`, `MEDICAL_FAMILY_HISTORY`,
`MEDICAL_HISTORY`) — **opt-in**, non chargé par défaut, dépendance `transformers`.

**Angles morts complets** : pas de `SEXUAL_ORIENTATION`, pas de données génétiques, pas de
biométrie, pas d'appartenance syndicale, rien sur l'art. 10.

### 3.5 Aucun repli

Si aucun recognizer ne matche, `analyze()` **retourne une liste vide** — il n'existe ni `PII`, ni
`GENERIC_PII`, ni `UNKNOWN`. Le `DEFAULT = "replace"` qu'on trouve dans
`presidio-anonymizer/anonymizer_engine.py` est un repli d'**anonymisation** appliqué à des entités
**déjà détectées**, pas un repli de catégorisation. Ce qui n'est pas modélisé n'est pas détecté,
**silencieusement** — formulation qui devrait résonner avec l'`Omission silencieuse` de `Casework`.

### 3.6 `presidio-structured` : la citation qui vaut le détour

C'est le module le plus proche de notre besoin
([doc](https://presidio.dataprivacystack.org/structured/)) : il « leverages the detection
capabilities of Presidio-Analyzer to identify **columns or keys containing** PII », et produit un
`StructuredAnalysis(entity_mapping={nom_de_colonne: entity_type})` — **une entité par colonne**,
exactement notre forme de sortie.

Mais il l'obtient en analysant **le contenu** de chaque colonne
(`PandasAnalysisBuilder._batch_analyze_df()`) puis en agrégeant par une stratégie déclarée —
`most_common` par défaut, ou `highest_confidence`, ou `mixed`. Et la section « Future work » de sa
propre documentation annonce :

> Add support for the detection of **sensitive column names**

**Ce que le projet range dans ses travaux futurs est le point de départ de la carte #122.** Côté
`BatchAnalyzerEngine.analyze_dict()`, la clé est bien injectée comme contexte
(`context=[key]`) — même mécanisme que le hotword Google et le keyword Macie : une colonne nommée
`ssn` remonte le score de valeurs qui matchent faiblement, mais une colonne `numero_secu` dont les
valeurs ne matchent rien **ne produit aucune détection**.

---

## 4. Google Cloud — Sensitive Data Protection (`infoTypes`)

### 4.1 Volumétrie et axes de classification

Référence officielle :
[InfoType detector reference](https://docs.cloud.google.com/sensitive-data-protection/docs/infotypes-reference)
(l'ancienne URL `cloud.google.com/sensitive-data-protection/…` redirige en 301). La page n'affiche
pas de total ; un décompte du tableau au 8 août 2026 donne **261 entrées** — **213 infoTypes
classiques** au format `NOM_MAJUSCULE`, plus **48 types documents/images** au format hiérarchique
`DOCUMENT_TYPE/…`, `OBJECT_TYPE/…`, `IMAGE_TYPE/…`. Listing programmatique :
[`infoTypes.list`](https://docs.cloud.google.com/sensitive-data-protection/docs/listing-infotypes).

**Google est le seul des trois à classer selon plusieurs axes orthogonaux**, exposés dans l'API par
`InfoTypeCategory` avec trois enums
([référence RPC](https://docs.cloud.google.com/sensitive-data-protection/docs/reference/rpc/google.privacy.dlp.v2)) :

| Axe | Valeurs |
|---|---|
| **Type** (8) | `PII`, `SPII`, `DEMOGRAPHIC`, `CREDENTIAL`, `GOVERNMENT_ID`, `DOCUMENT`, `CONTEXTUAL_INFORMATION`, `CUSTOM` |
| **Industry** (3) | `FINANCE`, `HEALTH`, `TELECOMMUNICATIONS` |
| **Location** (~51) | `GLOBAL`, `FRANCE`, `GERMANY`, `UNITED_STATES`, … |

S'y ajoute un **score de sensibilité par défaut** par infoType : `SENSITIVITY_LOW` /
`SENSITIVITY_MODERATE` / `SENSITIVITY_HIGH`.

C'est structurellement une **facettisation**, pas une taxonomie fermée à une valeur : un même
infoType porte simultanément un pays, un ou plusieurs types, éventuellement un secteur. Notre
contrainte « une catégorie par colonne » nous interdit ce modèle tel quel — mais elle nous laisse le
choix de **quel axe** devient notre taxonomie.

### 4.2 La France chez Google

Cinq infoTypes, tous `GOVERNMENT_ID, PII, SPII` et tous `SENSITIVITY_HIGH`
([référence](https://docs.cloud.google.com/sensitive-data-protection/docs/infotypes-reference)) :

| infoType | Description officielle (extrait) |
|---|---|
| `FRANCE_CNI` | « The French Carte Nationale d'Identité Sécurisée (CNI or CNIS) […] a 12-digit identification number » |
| `FRANCE_NIR` | « The French Numéro d'Inscription au Répertoire (NIR) is a permanent personal identification number that's also known as the French social security number » |
| `FRANCE_PASSPORT` | « A French passport number. » |
| `FRANCE_DRIVERS_LICENSE_NUMBER` | « A French driver's license number. » |
| `FRANCE_TAX_IDENTIFICATION_NUMBER` | « […] a government-issued ID for all individuals paying taxes in France » (porte en plus `FINANCE`) |

Il n'existe **ni `FRANCE_SSN` ni `FRANCE_TAX_ID`**, ni **aucun identifiant santé français** (pas
d'INS, pas de RPPS), ni **SIREN/SIRET**.

### 4.3 Google est le seul à énumérer l'article 9

C'est la divergence la plus nette avec Macie. Google fournit des détecteurs **lexicaux et
contextuels** pour presque tous les items de l'art. 9
([référence](https://docs.cloud.google.com/sensitive-data-protection/docs/infotypes-reference)) :

| Item art. 9 | infoType Google |
|---|---|
| Origine raciale ou ethnique | `ETHNIC_GROUP` — « A person's ethnic group. » |
| Opinions politiques | `POLITICAL_TERM` — « Terms that commonly refer to an association or membership to a political party. » |
| Convictions religieuses | `RELIGIOUS_TERM` |
| Appartenance syndicale | `TRADE_UNION` |
| Données génétiques | *aucun* |
| Données biométriques | *aucun côté texte* ; côté image `OBJECT_TYPE/PERSON/FACE`, `/SIGNATURE` |
| Santé | `MEDICAL_DATA`, `MEDICAL_TERM`, `MEDICAL_ID`, `MEDICAL_RECORD_NUMBER`, `BLOOD_TYPE`, `ICD9_CODE`, `ICD10_CODE`, `FDA_CODE`… |
| Vie sexuelle / orientation | `SEXUAL_ORIENTATION` |
| **Art. 10** — condamnations | *aucun* |

Il n'existe **aucun bundle nommé « sensitive »**. Les trois substituts sont la catégorie de type
`SPII`, le score `SENSITIVITY_HIGH`, et surtout l'infoType général **`DEMOGRAPHIC_DATA`**, qui
agrège `AGE, COUNTRY_DEMOGRAPHIC, DATE_OF_BIRTH, EMPLOYMENT_STATUS, ETHNIC_GROUP, GENDER,
IMMIGRATION_STATUS, MARITAL_STATUS, POLITICAL_TERM, RELIGIOUS_TERM, SEXUAL_ORIENTATION,
TRADE_UNION`.

**Ce regroupement est un contre-exemple instructif.** `DEMOGRAPHIC_DATA` mélange dans un même sac
quatre items de l'art. 9 et des attributs parfaitement ordinaires (âge, statut marital). Un
regroupement conçu pour la commodité de détection écrase exactement la frontière juridique qui
compte. Détail cohérent avec cette logique : ces détecteurs art. 9 sont notés
`SENSITIVITY_MODERATE` par défaut, pas `HIGH` — Google réserve `HIGH` aux identifiants d'État. **Sa
hiérarchie de sensibilité est celle du risque de ré-identification, pas celle du RGPD.**

### 4.4 Les « general infoTypes » — une hiérarchie assumée

Google documente des
[general infoTypes](https://docs.cloud.google.com/sensitive-data-protection/docs/concepts-infotypes),
sur-ensembles explicites d'infoTypes spécifiques, avec leur cardinalité :

`GOVERNMENT_ID` (113), `PASSPORT` (28), `SECURITY_DATA` (17), `DEMOGRAPHIC_DATA` (12),
`DRIVERS_LICENSE_NUMBER` (11), `MEDICAL_ID` (9), `FINANCIAL_ID` (8), `TECHNICAL_ID` (7),
`MEDICAL_DATA` (6), `CREDIT_CARD_DATA` (4), `GEOGRAPHIC_DATA` (4), `MAC_ADDRESS` (2),
`PHONE_NUMBER` (1), `VEHICLE_IDENTIFICATION_NUMBER` (1).

**C'est le référentiel qui documente le mieux ce que « la bonne granularité » veut dire** : Google
maintient les deux niveaux en parallèle et laisse l'appelant choisir lequel il demande. Une
taxonomie fermée à un seul niveau, comme la nôtre, doit trancher ce que Google refuse de trancher.

### 4.5 `GENERIC_ID` — le seul repli explicite de tout le corpus

C'est la trouvaille la plus directement utile de cette recherche. Google publie un infoType dont la
définition officielle est **exactement** notre question du « personnel mais non catégorisé »
([référence](https://docs.cloud.google.com/sensitive-data-protection/docs/infotypes-reference)) :

> **`GENERIC_ID`** — « Alphanumeric and special character strings that may be personally
> identifying but do not belong to a well-defined category. »

À ne pas confondre avec `PERSON_NAME`, qui n'est **pas** un repli mais un infoType spécifique
(`GLOBAL`/`PII`/`MODERATE`, avec un avertissement de latence dans sa propre description).

Aucun autre référentiel du corpus ne fournit un tel repli — la CNIL en fournit un d'une autre
nature (§ 2), Presidio et Macie n'en ont aucun.

### 4.6 Valeurs contre noms de colonnes

Comme Macie, Google classe **fondamentalement sur les valeurs**, le nom de colonne n'étant qu'un
signal contextuel — mais il le formalise plus explicitement. La référence
[`InspectConfig`](https://docs.cloud.google.com/sensitive-data-protection/docs/reference/rest/v2/InspectConfig)
écrit noir sur blanc :

> For record inspection of tables, **column names are considered hotwords.**

Conséquence documentée : pour qu'une `HotwordRule` ou une `ExclusionRule` porte sur l'en-tête de
colonne, il faut `proximity.windowBefore = 1` ; et `ExcludeByHotword` « lets you exclude an entire
column of data from the results »
([règles d'infoTypes personnalisés](https://docs.cloud.google.com/sensitive-data-protection/docs/creating-custom-infotypes-rules)).
Le nom de colonne peut donc **augmenter ou diminuer la vraisemblance** d'un résultat, jamais le
produire seul.

Le mécanisme le plus proche de notre livrable est le
[data profiling](https://docs.cloud.google.com/sensitive-data-protection/docs/data-profiles) :
« One column data profile for each column in the table », chaque profil portant les infoTypes
prédits, un `sensitivity level` et un `data risk level`. La forme de sortie — un verdict par
colonne — est exactement la nôtre ; l'entrée ne l'est pas, puisqu'elle lit les lignes.

---

## 5. Amazon Macie (`managed data identifiers`)

### 5.1 Structure du catalogue

Macie fournit des [`managed data identifiers`](https://docs.aws.amazon.com/macie/latest/user/managed-data-identifiers.html),
listés exhaustivement dans la
[quick reference « by type »](https://docs.aws.amazon.com/macie/latest/user/mdis-reference-quick.html).
AWS ne publie **aucun nombre total** dans la documentation — la formulation employée est « a large
and growing list ». La source faisant autorité pour un décompte est l'API
[`ListManagedDataIdentifiers`](https://docs.aws.amazon.com/macie/latest/APIReference/managed-data-identifiers-list.html).
Un dénombrement manuel de la table le 8 août 2026 donne **166 identifiants** sur environ
**44 « sensitive data types »** — chiffre de comptage, non affirmé par AWS.

Macie est le seul des trois outils à porter une **hiérarchie à deux niveaux explicite** :
catégorie → type de donnée sensible → identifiant.

| Catégorie (« sensitive data category ») | Page de référence |
|---|---|
| **Credentials** | [mdis-reference-credentials](https://docs.aws.amazon.com/macie/latest/user/mdis-reference-credentials.html) |
| **Financial information** | [mdis-reference-financial](https://docs.aws.amazon.com/macie/latest/user/mdis-reference-financial.html) |
| **Personal information : PHI** | [mdis-reference-phi](https://docs.aws.amazon.com/macie/latest/user/mdis-reference-phi.html) |
| **Personal information : PII** | [mdis-reference-pii](https://docs.aws.amazon.com/macie/latest/user/mdis-reference-pii.html) |

Détail de vocabulaire à ne pas répéter de travers : AWS écrit **« personal health information »**,
et non « protected health information ».

Les **17 types PII** :
`Birth date`, `Driver's license identification number`, `Electoral roll number`, `Full name`,
`GPS coordinates`, `HTTP cookie`, `Mailing address`, `National identification number`,
`National Insurance Number (NINO)`, `Passport number`, `Permanent residence number`,
`Phone number`, `Public transportation card number`, `Social Insurance Number (SIN)`,
`Social Security number (SSN)`, `Taxpayer identification or reference number`,
`Vehicle identification number (VIN)`
([source](https://docs.aws.amazon.com/macie/latest/user/mdis-reference-pii.html)).

Les **7 types PHI** :
`DEA Registration Number`, `Health Insurance Claim Number (HICN)`,
`Health insurance or medical identification number`, `HCPCS code`, `National Drug Code (NDC)`,
`National Provider Identifier (NPI)`, `Unique device identifier (UDI)`
([source](https://docs.aws.amazon.com/macie/latest/user/mdis-reference-phi.html)).

### 5.2 La France chez Macie

Sept identifiants préfixés `FRANCE_` ([quick reference](https://docs.aws.amazon.com/macie/latest/user/mdis-reference-quick.html)) :

| Identifiant | Catégorie | Mot-clé exigé |
|---|---|---|
| `FRANCE_NATIONAL_IDENTIFICATION_NUMBER` (codes **INSEE**, c.-à-d. le NIR) | PII | oui |
| `FRANCE_PASSPORT_NUMBER` | PII | oui |
| `FRANCE_DRIVERS_LICENSE` | PII | oui |
| `FRANCE_PHONE_NUMBER` | PII | variable |
| `FRANCE_TAX_IDENTIFICATION_NUMBER` | PII | oui |
| `FRANCE_BANK_ACCOUNT_NUMBER` (BBAN et IBAN) | Financial | BBAN oui, IBAN non |
| `FRANCE_HEALTH_INSURANCE_NUMBER` (carte Vitale) | **PHI** | oui |

Plus deux identifiants non préfixés qui couvrent la France : `ADDRESS` (8 pays dont la France) et
`EUROPEAN_HEALTH_INSURANCE_CARD_NUMBER`.

**Aucun identifiant SIREN ni SIRET** — cohérent avec le fait que ce sont des identifiants
d'entreprise, sauf pour l'entrepreneur individuel où le SIREN dérive du NIR.

### 5.3 Ce que Macie ne couvre pas

**Religion, origine raciale ou ethnique, orientation sexuelle, opinions politiques, appartenance
syndicale : absents du catalogue**, sur les pages PII comme PHI. Sur les huit items de l'art. 9,
Macie ne couvre que la **santé**, et uniquement sous forme d'**identifiants numériques** — numéro
d'assuré, code d'acte, code médicament, NPI, UDI — jamais un diagnostic ni une pathologie.
`UK_ELECTORAL_ROLL_NUMBER` est un numéro d'inscription sur les listes électorales, pas une opinion
politique. Pour ces catégories, AWS renvoie aux
[custom data identifiers](https://docs.aws.amazon.com/macie/latest/user/custom-data-identifiers.html)
(regex + mots-clés + mots à ignorer + distance de proximité).

### 5.4 Pas de valeur de repli

**Aucun mécanisme de repli documenté.** Macie ne remonte une occurrence que si un identifiant
managed ou custom **matche exactement**. Le seul concept voisin est l'
[`unclassifiable object`](https://docs.aws.amazon.com/macie/latest/user/discovery-supported-formats.html) —
un objet dont le format ou la classe de stockage n'est pas supporté, donc **sauté sans être lu** :
c'est une exclusion en amont, pas un « personnel mais non catégorisé ».

Piège d'exploitation à connaître : le
[jeu par défaut de l'automated sensitive data discovery](https://docs.aws.amazon.com/macie/latest/user/discovery-asdd-settings-defaults.html)
est un sous-ensemble restreint — **aucun identifiant PHI n'y est actif**, et côté France seuls
`FRANCE_NATIONAL_IDENTIFICATION_NUMBER`, `FRANCE_PASSPORT_NUMBER` et
`FRANCE_TAX_IDENTIFICATION_NUMBER` le sont.

### 5.5 Le point le plus utile pour nous : le nom de colonne comme mot-clé de contexte

Macie inspecte le **contenu** des objets S3
([formats supportés](https://docs.aws.amazon.com/macie/latest/user/discovery-supported-formats.html)),
donc les valeurs. Mais la page
[« Keyword requirements »](https://docs.aws.amazon.com/macie/latest/user/managed-data-identifiers-keywords.html)
décrit précisément l'usage qu'il fait des **noms de colonnes** :

> for structured, columnar data […] a keyword has to be part of the same value **or in the name of
> the column or field** that stores a value. […] if the name of a column contains *SSN*, Macie can
> detect each SSN in the column. Macie treats the values in that column as being in proximity of
> the keyword *SSN*.

Et pour les données structurées en enregistrements (Avro, Parquet, JSON) : le mot-clé peut être
« in the name of an element **in the path** to the field », l'exemple donné étant
`$.credentials.aws.key`.

**Conséquence pour la carte #122.** Macie confirme, en production et à grande échelle, que le
**nom de colonne porte du signal exploitable** — c'est même le mécanisme par lequel la majorité de
ses identifiants deviennent détectables. Mais Macie l'emploie comme **condition de contexte**, la
valeur restant le déclencheur : le nom seul ne produit **jamais** une détection. Notre service
retire précisément la moitié dont Macie ne se passe pas. C'est un argument mesuré en faveur du
lexique de noms de colonnes, et simultanément un avertissement sur son taux de faux positifs :
personne dans l'industrie ne fait tourner cette moitié-là toute seule.

Macie **localise** bien la colonne dans ses résultats
([schéma](https://docs.aws.amazon.com/macie/latest/user/findings-locate-sd-schema.html)) : tableau
`cells` avec numéro de colonne et de ligne pour CSV/TSV/Excel, `records` avec chemin de champ pour
Avro/Parquet.

---

## 6. Où les référentiels divergent, et pourquoi

C'est le résultat de ce ticket. La liste n'est pas l'apport ; la ligne de fracture l'est.

### 6.1 La fracture principale : on catégorise ce qu'on sait faire

**Les référentiels européens énumèrent un risque juridique. Les outils énumèrent un format
reconnaissable.** Tout le reste en découle.

Le RGPD énumère huit items parce que huit types d'information exposent une personne à la
discrimination — le [considérant 75](https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=CELEX:32016R0679)
le dit en toutes lettres. Aucun de ces huit items n'a de forme syntaxique. « Convictions
religieuses » n'a ni longueur, ni alphabet, ni clé de contrôle.

Macie et Presidio énumèrent l'inverse exact : ce qui porte un **motif vérifiable**. Un SSN a neuf
chiffres, un IBAN a une clé mod-97, un numéro DEA passe un Luhn. Et le catalogue s'arrête
précisément là où le motif s'arrête — **Macie n'a rien sur la religion, l'origine ethnique,
l'orientation sexuelle, les opinions politiques ni l'appartenance syndicale**, non par choix
doctrinal mais parce qu'aucune regex ne les attrape.

D'où le renversement complet des surfaces :

| | Couvre bien | Couvre mal ou pas |
|---|---|---|
| **RGPD / CNIL** | ce qui est juridiquement dangereux (art. 9, art. 10, NIR) | les identifiants techniques (cookie, IP, VIN, `TECHNICAL_ID`) — noyés dans « données de connexion » |
| **Macie / Presidio** | les identifiants d'État et financiers, par pays | l'art. 9 — Macie : rien hors santé ; Presidio : `NRP` bruité + bloc médical opt-in |
| **Google DLP** | les deux, **mais à des altitudes différentes** | le classement des deux dans un même barème |

**Google est l'exception, et c'est ce qui le rend le plus instructif.** Il est le seul à publier des
détecteurs pour presque tout l'art. 9 — `ETHNIC_GROUP`, `RELIGIOUS_TERM`, `POLITICAL_TERM`,
`TRADE_UNION`, `SEXUAL_ORIENTATION`. Mais il les note `SENSITIVITY_MODERATE` et les range dans
`DEMOGRAPHIC_DATA`, aux côtés de l'âge et du statut marital, tout en réservant `SENSITIVITY_HIGH`
aux passeports et aux NIR. **Sa hiérarchie de sensibilité est celle du risque de ré-identification,
pas celle du RGPD.** Il couvre les deux mondes et les classe selon un seul.

Corollaire pratique : **aucun catalogue d'outil ne peut servir de taxonomie RGPD par simple
renommage.** L'inverse non plus — l'art. 9 ne dit rien d'une colonne `session_token`.

### 6.2 La deuxième fracture : national contre universel

La France est traitée de façon radicalement inégale, et l'écart n'est pas explicable par la taille
du marché :

| Référentiel | Couverture France |
|---|---|
| **CNIL** | le NIR a sa **propre ligne** dans le registre, et son propre régime légal (décret cadre) |
| **Google DLP** | 5 infoTypes — `FRANCE_CNI`, `FRANCE_NIR`, `FRANCE_PASSPORT`, `FRANCE_DRIVERS_LICENSE_NUMBER`, `FRANCE_TAX_IDENTIFICATION_NUMBER` |
| **AWS Macie** | 7 identifiants `FRANCE_*`, dont `FRANCE_HEALTH_INSURANCE_NUMBER` (carte Vitale) — le seul des trois à couvrir la santé française |
| **Presidio** | **rien**. Aucun répertoire `france`, aucune entité `FR_*`. Seul le pattern IBAN FR existe, et il produit `IBAN_CODE` |

**Aucun des trois outils ne connaît le SIREN ni le SIRET**, alors que Presidio a des identifiants
d'entreprise pour l'Australie, Singapour, la Suède, l'Allemagne et l'Afrique du Sud. Ce n'est pas
qu'un oubli commercial : le SIREN/SIRET est un identifiant d'**entreprise**, donc hors RGPD — sauf
pour l'entrepreneur individuel, où le SIREN est dérivé du NIR et redevient une donnée personnelle.
Aucun référentiel n'exprime cette nuance, et une taxonomie qui l'ignore se trompera sur toutes les
bases de facturation françaises.

Enseignement pour la carte : **la couche française est à écrire, quel que soit le référentiel
retenu.** Aucun ne la fournit complète.

### 6.3 La troisième fracture : personne ne classe un schéma

Les trois outils convergent sur un point qu'aucun ne présente comme une limite, et qui est
pourtant central pour nous :

| Outil | Rôle du nom de colonne | Peut-il déclencher seul ? |
|---|---|---|
| **Macie** | « a keyword has to be part of the same value **or in the name of the column** » — le nom satisfait l'exigence de mot-clé | **non** |
| **Google DLP** | « For record inspection of tables, **column names are considered hotwords** » — module la vraisemblance | **non** |
| **Presidio** | `analyze_dict()` injecte la clé comme `context=[key]` — remonte le score | **non** |

**Les trois utilisent le nom de colonne exactement de la même façon : comme un modulateur de
confiance sur une détection déjà déclenchée par la valeur.** Aucun ne le traite comme signal
autonome. La documentation de `presidio-structured` le dit d'ailleurs sans détour, en rangeant
« Add support for the detection of sensitive column names » dans ses **travaux futurs**.

Deux lectures, et elles sont toutes deux valides :

- **Encourageante** — trois industriels ont indépendamment jugé le nom de colonne assez porteur de
  signal pour en faire une condition de détection. Le lexique de la carte #122 s'appuie sur un
  signal réel, pas sur un pari.
- **Avertissement** — aucun des trois ne le fait tourner seul. Ils gardent tous la valeur comme
  déclencheur. La carte #122 retire délibérément la moitié dont personne ne se passe, et il n'existe
  **aucun taux de faux positifs publié** pour le régime nom-seul. Le banc mesuré prévu par la carte
  n'est donc pas une précaution de confort : **c'est la seule source de chiffres qui existera.**

Note d'encouragement toutefois : la forme de sortie de `presidio-structured`
(`{nom_de_colonne: entity_type}`) et celle des
[column data profiles](https://docs.cloud.google.com/sensitive-data-protection/docs/data-profiles)
de Google sont **exactement** la nôtre — une catégorie par colonne. C'est l'entrée qui diffère, pas
la sortie.

---

## 7. Tableau de correspondance des grandes familles

Alignement des grandes familles. `—` signale une absence réelle, pas une simple différence de nom.

| Famille | CNIL (registre `.ods`) | Google DLP (Type / general infoType) | AWS Macie (catégorie / type) | Presidio |
|---|---|---|---|---|
| Identité, état civil | `État civil, identité, données d'identification, images…` | `PII` ; `PERSON_NAME`, `DATE_OF_BIRTH`, `GENDER` | PII : `Full name`, `Birth date` | `PERSON`, `DATE_TIME` |
| Coordonnées | *(dans « État civil » ou « Vie personnelle »)* | `PII` ; `EMAIL_ADDRESS`, `PHONE_NUMBER`, `STREET_ADDRESS` | PII : `Mailing address`, `Phone number` | `EMAIL_ADDRESS`, `PHONE_NUMBER`, `LOCATION` |
| Identifiants d'État | `Numéro de Sécurité Sociale (ou NIR)` *(ligne dédiée)* | `GOVERNMENT_ID` (113 infoTypes) | PII : `National identification number`, `Passport number`, `Driver's license…` | `US_SSN`, `UK_NINO`, `ES_NIF`, `DE_ID_CARD`… |
| Économique et financier | `Informations d'ordre économique et financier` | `FINANCE` ; `FINANCIAL_ID`, `CREDIT_CARD_DATA` | **Financial information** (catégorie de 1er niveau) | `CREDIT_CARD`, `IBAN_CODE`, `US_BANK_NUMBER`, `CRYPTO` |
| Connexion, technique | `Données de connexion (adress IP, logs, etc.)` | `TECHNICAL_ID` ; `IP_ADDRESS`, `MAC_ADDRESS`, `URL` | PII : `HTTP cookie` | `IP_ADDRESS`, `MAC_ADDRESS`, `URL`, `UUID` |
| Localisation | `Données de localisation (déplacements, GPS, GSM…)` | `GEOGRAPHIC_DATA` | PII : `GPS coordinates` | `LOCATION` |
| Vie personnelle | `Vie personnelle (habitudes de vie, situation familiale…)` | `DEMOGRAPHIC` ; `MARITAL_STATUS` | — | — |
| Vie professionnelle | *(seulement dans l'**ancien** modèle)* | `EMPLOYMENT_STATUS` | — | `ORGANIZATION` (via NER) |
| **Santé (art. 9)** | `Données concernant la santé` | `HEALTH` ; `MEDICAL_DATA`, `MEDICAL_ID`, `ICD10_CODE` | **PHI** (catégorie de 1er niveau, 12 identifiants) | bloc `MEDICAL_*` **opt-in** |
| **Origine ethnique (art. 9)** | ligne dédiée | `ETHNIC_GROUP` | **—** | `NRP` *(fusionné)* |
| **Opinions politiques (art. 9)** | ligne dédiée | `POLITICAL_TERM` | **—** | `NRP` *(fusionné)* |
| **Convictions religieuses (art. 9)** | ligne dédiée | `RELIGIOUS_TERM` | **—** | `NRP` *(fusionné)* |
| **Appartenance syndicale (art. 9)** | ligne dédiée | `TRADE_UNION` | **—** | **—** |
| **Vie sexuelle / orientation (art. 9)** | ligne dédiée | `SEXUAL_ORIENTATION` | **—** | **—** |
| **Génétique (art. 9)** | ligne dédiée | **—** | **—** | **—** |
| **Biométrie (art. 9)** | ligne dédiée | **—** *(texte)* ; `OBJECT_TYPE/PERSON/FACE` *(image)* | **—** | **—** |
| **Condamnations (art. 10)** | ligne dédiée | **—** | **—** *(`UK_ELECTORAL_ROLL_NUMBER` n'en est pas)* | **—** |
| Secrets, identifiants techniques | **—** | `CREDENTIAL` ; `SECURITY_DATA` | **Credentials** (catégorie de 1er niveau, 9 types) | `CRYPTO` *(partiel)* |
| **Repli** | `Autres catégories (précisez)` — **ancien modèle seul** | **`GENERIC_ID`** | **—** | **—** |

Deux colonnes de ce tableau sont presque vides et disent l'essentiel. Les lignes art. 9 sont vides
côté Macie et Presidio. La ligne « Credentials » est vide côté CNIL — **un mot de passe ou un jeton
de session n'a de catégorie dans aucun référentiel européen**, alors que c'est une catégorie de
premier niveau chez AWS et Google, et probablement l'une des colonnes les plus fréquentes d'un
schéma SQL réel. Notre taxonomie devra dire ce qu'elle fait de `password_hash` et `api_token` :
aucune source primaire ne le lui dictera.

---

## 8. La granularité de l'article 9 : une valeur ou huit ?

C'est la question la plus consultable du ticket. Voici ce que le corpus permet d'affirmer.

### Ce que le texte impose — et ce qu'il n'impose pas

**Le texte n'impose aucune granularité.** L'art. 9, § 1 est **une seule phrase portant une seule
interdiction**, dont les huit items sont les compléments d'objet. Il n'y a pas huit régimes ; il y
en a un. Les dérogations du § 2 s'appliquent à l'ensemble, à deux exceptions près : le § 2, h)
(médecine préventive, diagnostic, gestion des soins) et le § 2, i) (santé publique) ne concernent
en pratique que la santé, et le § 3 leur ajoute une condition de secret professionnel.

Ce que le texte impose, en revanche :

1. **Art. 9 et art. 10 ne se confondent pas.** Régimes distincts, articles distincts. Une taxonomie
   qui les fusionnerait sous « données sensibles » commettrait une erreur de droit — et la CNIL,
   qui les met pourtant dans le même bloc de tableur, prend soin de maintenir la distinction en
   commentaire.
2. **Les données biométriques ne sont art. 9 que « aux fins d'identifier une personne physique de
   manière unique »**, le [considérant 51](https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=CELEX:32016R0679)
   excluant explicitement les photographies traitées autrement. **Un détecteur de schéma ne peut
   pas connaître cette finalité.** Une catégorie `Biométrie` rendue depuis un nom de colonne
   affirmera donc systématiquement plus que ce que le service peut savoir.

### Ce que font les référentiels

| Référentiel | Granularité de l'art. 9 |
|---|---|
| CNIL, onglet « fiche de registre » | **8 lignes séparées** + 1 pour l'art. 10 |
| CNIL, onglet « liste des traitements » | **1 booléen** `Données sensibles ? Oui/non` |
| Google DLP | **détecteurs séparés**, mais regroupés dans `DEMOGRAPHIC_DATA` et notés `MODERATE` |
| AWS Macie | **1 seul item couvert** (santé, via PHI) ; les 7 autres absents |
| Presidio | **3 items fusionnés en 1** (`NRP`), santé opt-in, 4 absents |

**La CNIL emploie les deux granularités dans le même fichier**, et le critère qui les départage est
lisible : le grain fin sur la **fiche détaillée d'un traitement**, le booléen sur la **vue de
synthèse**. La granularité suit le grain du document, pas la nature du droit.

### Ce qui départage, pour notre cas

Arguments **pour huit valeurs séparées** :

- C'est la granularité de la fiche de registre CNIL, et notre rapport est du grain de la fiche
  détaillée, pas de la liste de synthèse.
- Le motif en prose exigé par la décision de cadrage 6 est bien plus utile s'il peut dire
  « `confession` → `ConvictionsReligieuses` » que « → `DonneesSensibles` ».
- L'arbitrage humain de la décision 7 est plus facile sur une proposition précise : une catégorie
  trop large ne se réfute pas.

Arguments **pour une valeur unique** :

- **Le service ne lit que des noms de colonnes.** Les cinq items « qui révèlent » ne sont pas des
  types de données mais un effet contextuel : ils ne sont pas énonçables depuis un identifiant.
  Sept des huit valeurs seraient **structurellement inatteignables** par le moteur — une taxonomie
  dont sept valeurs ne se déclenchent jamais est une taxonomie qui ment sur sa couverture.
- Presidio et Google, les deux référentiels qui ont *essayé* de détecter ces items, l'ont fait par
  **NER sur de la prose** — précisément ce que la décision de cadrage 1 exclut.
- Le noyau `DataSubjectRight` du dépôt vaut par sa **petitesse**, et la clause de gouvernance fait
  de tout ajout un événement de niveau ADR. Huit valeurs qui ne se déclenchent pas sont huit
  engagements publics gratuits.

**Une troisième option, non explorée par le ticket mais suggérée par la structure CNIL** : la CNIL
ne pose pas la sensibilité comme une *valeur* de sa liste de catégories, mais comme un **second
bloc coché en parallèle**. La question « une valeur ou huit ? » présuppose que l'art. 9 soit une
valeur de la taxonomie. Le seul référentiel européen du corpus ne le traite pas ainsi. Trancher
cela relève du ticket de taxonomie ; le signaler relève de celui-ci.

---

## 9. « Personnel mais non catégorisé » : le repli

Question décisive pour une taxonomie fermée à une valeur par colonne. Le corpus est étonnamment
tranché.

| Référentiel | Repli ? |
|---|---|
| **RGPD** | sans objet — il ne publie pas de nomenclature (§ 1.5) |
| **CNIL, modèle `.ods` actuel** | **pas de case « autres »** — mais un **champ « Description » libre** en regard de chaque ligne, et des feuilles non protégées : le remplisseur peut éditer et insérer |
| **CNIL, ancien modèle PDF** | **oui** — `☐ Autres catégories de données (précisez) :` |
| **Google DLP** | **oui**, et c'est le seul repli formel d'un outil : `GENERIC_ID` — « strings that **may be personally identifying but do not belong to a well-defined category** » |
| **AWS Macie** | **non**. Rien ne matche ⇒ rien n'est remonté. `unclassifiable object` est une exclusion de format, pas un repli |
| **Presidio** | **non**. `analyze()` retourne une liste vide. Le `DEFAULT = "replace"` est un repli d'anonymisation, sur des entités déjà détectées |

**Les référentiels qui n'ont pas de repli ne forcent pas un choix pour autant — ils rendent le
silence.** C'est la distinction que le ticket de taxonomie doit retenir : l'alternative n'est pas
« repli ou choix forcé », elle est **triple**.

1. **Une valeur de repli** — Google `GENERIC_ID`, ancien PDF CNIL.
2. **Le silence** — Macie, Presidio : la colonne n'apparaît simplement pas au rapport.
3. **Un choix forcé** — que **personne** ne pratique dans ce corpus.

Pour la carte #122, le silence est la branche la plus coûteuse, et le dépôt a déjà écrit pourquoi :
une colonne détectée comme personnelle mais tue **parce qu'aucune catégorie ne lui allait**
produirait un rapport plus court et donc plus rassurant — c'est la définition même de l'`Omission
silencieuse`, et la décision de cadrage 11 en fait une propriété de la réponse.

**Le précédent interne est déjà écrit, et il tranche dans le même sens.** `DataSubjectRight`
comporte `OutOfScope`, et sa documentation dit exactement ce qu'un repli doit être
(`src/MicroserviceRgpd.Core/SharedKernel/DataSubjectRight.cs`) :

> Ce n'est ni « inconnu », ni « non classé » : c'est un verdict.

Le dépôt a donc déjà résolu une fois la question posée ici, sur sa seule autre taxonomie fermée, et
la formulation de Google — « **may be** personally identifying but do not belong to a well-defined
category » — converge avec ce précédent : c'est une affirmation sur la donnée, pas un aveu
d'ignorance du moteur.

Reste ouverte, et elle n'appartient pas à ce ticket, la question de savoir si **un** repli suffit,
ou s'il en faut **deux** de nature différente : « personnelle, catégorie hors taxonomie » et « non
personnelle ». Aucun référentiel du corpus ne distingue les deux, parce qu'aucun ne rend un verdict
sur **toutes** les colonnes — ils ne parlent que de ce qu'ils trouvent. Notre service, lui, reçoit
un recensement exhaustif de colonnes et doit dire quelque chose de chacune. **C'est le point où
notre problème n'a aucun précédent dans le corpus.**

---

## 10. Points non vérifiés en source primaire

Ce document a été écrit contre les textes et les documentations officielles. Voici ce qu'il n'a
**pas** établi, et qu'il ne faut donc pas citer comme acquis.

**Décomptes.** Les nombres d'infoTypes Google (**261** entrées, dont 213 classiques et 48
document/image) et d'identifiants Macie (**166** sur ~44 types) sont des **comptages manuels des
tableaux HTML au 8 août 2026**. Ni Google ni AWS ne publient de total — AWS écrit « a large and
growing list ». Les sources d'autorité sont les API `infoTypes.list` et
`ListManagedDataIdentifiers`, **non appelées**. Ces catalogues évoluent ; tout chiffre repris
ailleurs doit être redaté.

**Légifrance.** Le contenu de l'[art. 30 de la loi Informatique et Libertés](https://www.legifrance.gouv.fr/loda/article_lc/LEGIARTI000038886929)
et du [décret n° 2019-341](https://www.legifrance.gouv.fr/jorf/id/JORFTEXT000038396526/) est
**résumé, non cité littéralement** : Légifrance renvoie 404 aux requêtes automatisées. Les URL sont
correctes, le texte exact n'a pas été lu. À revérifier avant toute citation dans un ADR.

**ISO/IEC 19944-1:2020.** Signalée comme portant une taxonomie de catégories de données, sur la
seule foi de sa [notice ISO](https://www.iso.org/standard/79573.html). **Norme payante, non
consultée.** Son contenu réel est inconnu de ce document. C'est la piste la plus sérieuse qui reste
ouverte, et la seule qui pourrait fournir une taxonomie de type *fermée et transversale* — ce
qu'aucune source gratuite du corpus ne fournit.

**Jurisprudence de la CJUE sur l'art. 9.** Non explorée. La question du périmètre exact des données
« qui révèlent » — combien de dérivation indirecte suffit à faire entrer une donnée dans l'art. 9 —
a fait l'objet de décisions qui n'ont pas été recherchées ici. Elle importerait si la taxonomie
devait qualifier des colonnes *indirectement* révélatrices.

**Versions.** Les catalogues Presidio ont été lus sur la branche `main` de
`github.com/microsoft/presidio`, sans numéro de version figé, et le projet **a changé
d'organisation** (§ 3.0) : la doc fait désormais autorité sous
`presidio.dataprivacystack.org`. Les listes d'entités par pays du § 3.2 proviennent en partie de
`conf/default_recognizers.yaml` et en partie de la page `supported_entities`, **qui divergent** —
plusieurs entités existent dans l'un et pas dans l'autre (`UUID`, `ABA_ROUTING_NUMBER`,
`PhPassportRecognizer`, plusieurs recognizers `Za*`). Le nombre exact d'entités Presidio n'est donc
pas une donnée stable.

**Taux de performance.** **Aucun chiffre de précision ou de rappel n'a été trouvé**, pour aucun des
trois outils, ni en régime nominal, ni — a fortiori — en régime « nom de colonne seul », que
personne ne pratique. Le banc mesuré prévu par la carte #122 ne pourra se comparer à aucune
référence publiée.

**Le modèle de registre CNIL comme source de taxonomie.** Le fichier `.ods` a bien été ouvert et son
`content.xml` analysé — les quinze libellés du § 2.1 et les constats de structure du § 2.2 sont
exacts au caractère près. Mais **rien dans la documentation de la CNIL ne présente ces six
catégories comme une nomenclature normative** : ce sont les lignes pré-remplies d'un modèle de
document, non protégées et librement éditables. Les traiter comme un référentiel serait leur prêter
une autorité que leur auteur ne leur donne pas.
