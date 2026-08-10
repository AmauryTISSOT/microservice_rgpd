# Transcription de rédaction — `ligne-de-base.tsv`

Rédaction **en aveugle** exécutant [#155](https://github.com/AmauryTISSOT/microservice_rgpd/issues/155)
(gel amendé décidé par [#154](https://github.com/AmauryTISSOT/microservice_rgpd/issues/154), amendement
posté sur [#130](https://github.com/AmauryTISSOT/microservice_rgpd/issues/130)). Rédacteur : sous-agent
`general-purpose` (Claude Fable 5) **sans aucun accès au dépôt** — aucun outil invoqué (0 appel d'outil,
compté par le harnais). Le fichier `ligne-de-base.tsv` est l'extraction mécanique du bloc `tsv` de la
sortie ci-dessous ; aucune entrée n'a été éditée. Les deux seuls noms de colonnes présents dans
l'entrée, `arret_maladie` et `email_pro`, sont les exemples inventés de la documentation, déclarés
comme tels dans l'énoncé.

## Entrée — message intégral transmis au sous-agent

# Énoncé de tâche — rédaction en aveugle de la LIGNE DE BASE TRIVIALE du banc de dépistage

Tu es un rédacteur **en aveugle**. Tu n'as accès à **aucun outil** : pas de lecture de fichier, pas de shell, pas de recherche web, pas de dépôt de code. Ta **seule entrée** est le présent message. N'utilise aucun outil, ne tente aucune action ; réponds en un seul message. Commence ta réponse par la déclaration : « Je n'ai consulté aucune ressource extérieure ; ma seule entrée est le présent énoncé et la section verbatim qu'il contient. »

## Contexte

Un service de dépistage lit des **noms de colonnes** de schémas SQL (jamais les valeurs) et propose, colonne par colonne, une catégorie de données personnelles prise dans une taxonomie fermée. Un banc d'essai mesurera plusieurs montages contre une **ligne de base triviale**, dont le lexique est ce que tu vas rédiger. C'est le seul point de comparaison que le banc possédera : elle doit rester **réellement naïve**.

La ligne de base fonctionnera ainsi, et c'est toute sa spécification : **correspondance exacte, insensible à la casse, du nom de colonne entier** contre ta liste. Elle ne lit que le nom de la colonne — ni type, ni commentaire, ni table. Elle ne fait **aucune** découpe d'identifiant, aucun préfixe, aucune racinisation : `nom_client` ne correspond **pas** à une entrée `nom`.

## Ce que tu produis

Une liste de **30 à 50 entrées au total, bilingue** (noms français et anglais mélangés dans la même liste) : les noms de colonnes **entiers** les plus évidents qu'on s'attend à croiser tels quels dans une base de données, écrits **de mémoire**, avec pour chacun sa valeur de taxonomie.

- **Format** : un bloc de code unique ouvert par ` ```tsv `, contenant une ligne d'en-tête `colonne<TAB>valeur`, puis une entrée par ligne, séparateur tabulation. Rien d'autre dans le bloc.
- **`colonne`** : un nom de colonne complet, en minuscules, tel qu'attendu en base (par exemple `email`) — puisque seule la correspondance du nom entier compte, une entrée qui n'est pas un nom de colonne plausible en entier ne servira jamais.
- **`valeur`** : un des noms canoniques du tableau de la taxonomie ci-dessous, **sauf `Unflagged`**.
- **30 à 50 entrées, pas davantage** : la trivialité est la propriété recherchée. Prends les noms les plus courants des deux langues, pas les cas rares.
- **Un nom → une seule valeur.** Si un nom relève de plusieurs valeurs, choisis selon l'ordre d'arbitrage du tableau (du haut vers le bas).

⚠️ Les deux noms de colonnes cités dans la section ci-dessous, `arret_maladie` et `email_pro`, sont des **exemples inventés pour la documentation** ; ils ne proviennent d'aucun schéma réel.

Après le bloc, ajoute une courte note sur les choix non évidents.

## Entrée : section `PersonalDataCategory` de `docs/contexts/screening/CONTEXT.md` (verbatim)

**PersonalDataCategory** :
La taxonomie fermée dans laquelle une `ScreenedColumn` puise. Elle reconnaît toujours une valeur —
l'absence de signalement est elle-même une valeur nommée, `Unflagged`, jamais une ligne absente.
⚠️ **Elle n'est pas `DataSubjectRight` et ne s'y raccroche jamais.** Une colonne « courriel » ne
relève pas d'un droit plutôt qu'un autre, elle relève de tous ; l'y rattacher ferait peser une
troisième dépendance sur le noyau partagé sans rien apporter. Elle est **propre à ce contexte**, et
son auteur n'est pas le RGPD.
⚠️ Le mot **catégorie** est sur la liste _Avoid_ de `DataSubjectRight` et le reste : le nom complet
est porté ici précisément pour que cette interdiction n'ait pas d'exception à gérer.

**Treize valeurs**, chacune portant son **nom canonique anglais** et son **libellé français attaché**
— décalque exact de `DataSubjectRight`, une seule source de vérité et aucune table de correspondance
parallèle. Le motif, lui, reste en prose française : il est écrit pour l'humain qui arbitre.

| Valeur | Libellé | Origine |
|---|---|---|
| `CriminalOffenceData` | données relatives aux infractions | RGPD art. 10 |
| `HealthData` | données concernant la santé | RGPD art. 9 |
| `SpecialCategoryData` | autre catégorie particulière | RGPD art. 9 |
| `AuthenticationSecret` | secret d'authentification | doctrinal |
| `NationalIdentifier` | identifiant national | CNIL, registre simplifié |
| `FinancialData` | données économiques et financières | CNIL, registre simplifié |
| `LocationData` | données de localisation | CNIL, registre simplifié |
| `ConnectionData` | données de connexion | CNIL, registre simplifié |
| `Identity` | état civil et identité | CNIL, registre simplifié |
| `ContactDetails` | coordonnées | CNIL, registre simplifié |
| `ProfessionalLife` | vie professionnelle | CNIL, ancien modèle |
| `PersonalDataUncategorised` | donnée personnelle sans catégorie | repli |
| `Unflagged` | rien signalé | repli |

⚠️ **La sensibilité est une valeur, pas une seconde dimension.** La CNIL coche ses neuf items
sensibles dans un **bloc parallèle** à ses six catégories ordinaires, et on pourrait croire qu'il
faut l'imiter — une colonne serait alors une catégorie **plus** un drapeau. C'est un artefact de son
**grain** : une fiche de registre décrit un traitement entier, où « identité **et** santé » coexistent
forcément. Notre grain est **la colonne**, et à ce grain la coexistence s'effondre : `confession` est
une conviction religieuse, elle n'est pas *aussi* de l'état civil. Un drapeau qui vaudrait vrai
exactement quand la catégorie est déjà l'une des trois valeurs de droit est un champ redondant — et
deux champs qu'un chemin d'écriture peut dissocier finissent par se dissocier. « Montre-moi les
colonnes sensibles » est donc un **calcul** sur la catégorie, comme « courant » est un calcul sur le
`Screening`.

⚠️ **L'art. 9 tient en deux valeurs, et l'énumération n'y porte pas l'item : le motif le porte.**
Ni une valeur unique, ni les huit du règlement. La santé se détache parce qu'elle cumule trois
raisons — c'est le seul des huit items fréquent dans un schéma réel, le seul dont le texte singularise
le régime (art. 9 § 2 h et i, § 3), et la première question d'un DPO. Les sept autres tiennent dans
`SpecialCategoryData`, et le motif nomme lequel : « `confession` → catégorie particulière (art. 9),
motif : convictions religieuses » dit à l'humain exactement ce que sept valeurs lui auraient dit,
sans que la taxonomie porte six engagements qui ne se déclenchent jamais. Le cas qui tranche est la
**biométrie** : le [considérant 51](https://eur-lex.europa.eu/legal-content/FR/TXT/HTML/?uri=CELEX:32016R0679)
ne la range à l'art. 9 que « aux fins d'identifier une personne de manière unique » — une **finalité**
qu'aucun lecteur de schéma ne connaît. Une valeur `Biometrics` affirmerait donc toujours plus que le
service ne peut savoir ; un motif peut dire que le régime dépend d'une finalité illisible ici.

⚠️ **L'art. 10 ne se replie pas dans l'art. 9**, alors même qu'il ne se déclenchera presque jamais.
Articles distincts, régimes distincts ; la CNIL maintient la distinction jusque dans un commentaire de
cellule de tableur — « font **également** l'objet de règles particulières », donc à côté et non
dedans. Les fusionner pour économiser une valeur serait une **erreur de droit** dans l'outil dont
c'est le métier de ne pas en commettre. Sa rareté n'est pas un argument contre son existence : c'est
un fait que la clause d'incomplétude a charge de dire.

**L'exclusivité est un invariant, et l'arbitrage est l'ordre de ce tableau, figé.** Une
`ScreenedColumn` porte **une** valeur. Quand plusieurs règles déclenchent — `arret_maladie` est santé
*et* vie professionnelle, `email_pro` est coordonnées *et* vie professionnelle — c'est l'ordre
ci-dessus qui tranche, du plus au moins coûteux à omettre. ⚠️ **Jamais la `RuleStrength`** :
comparer deux degrés pour désigner un gagnant serait un score qui produit une issue, ce que l'`Aide
à la décision` interdit. Le motif, lui, peut dire ce qui a été écarté — « la règle *vie
professionnelle* a aussi déclenché ». Les moteurs **héritent** cet ordre ; aucun ne le redécide.

**Sa gouvernance a deux étages, parce que ses valeurs n'ont pas toutes le même auteur.**
`DataSubjectRight` peut écrire qu'ajouter une valeur est une rupture de niveau ADR : il y a six droits
parce que le RGPD en ouvre six, et la clause emprunte sa solennité au règlement. **Cette taxonomie-ci
n'a pas cet appui** — le RGPD n'énumère nulle part les catégories *ordinaires*, l'art. 30 impose
l'exercice sans fournir le vocabulaire, et **toute** nomenclature ordinaire est donc doctrinale, celle
de la CNIL comprise. Se donner la même gravité sans le même fondement serait une posture.
- Les **trois valeurs de droit** — `CriminalOffenceData`, `HealthData`, `SpecialCategoryData` — sont
  fermées par le texte et ne bougent que s'il bouge.
- Les **valeurs ordinaires** sont un découpage de travail, révisable et sans autorité empruntée. En
  ajouter une n'est pas un ADR : c'est une PR dont le corps répond à trois questions — quelles
  colonnes réelles, dans quel schéma réel, ne trouvaient pas de valeur ; pourquoi
  `PersonalDataUncategorised` ne suffisait pas ; et où la valeur entre dans l'ordre d'arbitrage.
- ⚠️ **Retirer ou renommer une valeur reste un ADR**, et pour une raison qui n'est pas la même que
  chez `DataSubjectRight` : il n'y a pas d'appelant à casser ici, mais il y a des **arbitrages humains
  signés et datés** qui vivent plusieurs jours, et qu'un re-scan ne reprend pas. Ce geste-là ne périme
  pas un contrat, il périme du travail humain.

_Avoid_ : DataCategory, catégorie, Label, Class, Tag, Type ⚠️ le raccourci `DataCategory` viendrait
frotter contre la liste de `DataSubjectRight` pour économiser huit caractères.

## Sortie — réponse intégrale du sous-agent

Je n'ai consulté aucune ressource extérieure ; ma seule entrée est le présent énoncé et la section verbatim qu'il contient.

```tsv
colonne	valeur
casier_judiciaire	CriminalOffenceData
criminal_record	CriminalOffenceData
groupe_sanguin	HealthData
blood_type	HealthData
religion	SpecialCategoryData
ethnicity	SpecialCategoryData
password	AuthenticationSecret
mot_de_passe	AuthenticationSecret
password_hash	AuthenticationSecret
api_key	AuthenticationSecret
ssn	NationalIdentifier
nir	NationalIdentifier
numero_securite_sociale	NationalIdentifier
social_security_number	NationalIdentifier
iban	FinancialData
salaire	FinancialData
salary	FinancialData
credit_card_number	FinancialData
latitude	LocationData
longitude	LocationData
ip_address	ConnectionData
adresse_ip	ConnectionData
user_agent	ConnectionData
last_login	ConnectionData
nom	Identity
prenom	Identity
first_name	Identity
last_name	Identity
full_name	Identity
date_naissance	Identity
date_of_birth	Identity
sexe	Identity
gender	Identity
nationalite	Identity
nationality	Identity
email	ContactDetails
courriel	ContactDetails
telephone	ContactDetails
phone	ContactDetails
phone_number	ContactDetails
mobile	ContactDetails
adresse	ContactDetails
address	ContactDetails
ville	ContactDetails
city	ContactDetails
code_postal	ContactDetails
zip_code	ContactDetails
postal_code	ContactDetails
profession	ProfessionalLife
job_title	ProfessionalLife
```

**Notes sur les choix non évidents**

- **`adresse` / `address` / `ville` / `code_postal` → `ContactDetails`, pas `LocationData`.** L'adresse postale relève des « coordonnées » au sens du registre CNIL ; `LocationData` est réservé à la géolocalisation (`latitude`, `longitude`). Il n'y a pas de double déclenchement ici, donc l'ordre d'arbitrage ne joue pas.
- **`salaire` / `salary` → `FinancialData`.** Le nom déclenche à la fois données financières et vie professionnelle ; l'ordre du tableau fait gagner `FinancialData`.
- **`sexe` / `gender` → `Identity`.** Le sexe à l'état civil n'est pas l'orientation sexuelle ; sans indice supplémentaire dans le nom, le classer en catégorie particulière affirmerait plus que le nom ne dit.
- **`nationalite` / `nationality` → `Identity`.** La nationalité est de l'état civil ; c'est l'origine ethnique (`ethnicity`), pas la nationalité, qui relève de l'art. 9.
- **`last_login` / `user_agent` → `ConnectionData`** : traces techniques de connexion au sens du registre CNIL, malgré leur allure anodine.
- **`ssn`** est retenu comme abréviation figée courante en base ; conformément à la règle de correspondance entière, aucune variante composée (`ssn_number`, `user_ssn`) n'est tentée.
- La liste s'arrête à 50 entrées ; les métiers rares (`confession`, `diagnostic`, `siret`…) sont volontairement laissés de côté : la trivialité est la propriété recherchée.
