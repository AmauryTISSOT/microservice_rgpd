# Transcription de rédaction — `dictionnaire-en.tsv`

Rédaction **en aveugle** exécutant [#155](https://github.com/AmauryTISSOT/microservice_rgpd/issues/155)
(gel amendé décidé par [#154](https://github.com/AmauryTISSOT/microservice_rgpd/issues/154), amendement
posté sur [#130](https://github.com/AmauryTISSOT/microservice_rgpd/issues/130)). Rédacteur : sous-agent
`general-purpose` (Claude Fable 5) **sans aucun accès au dépôt** — aucun outil invoqué (0 appel d'outil,
compté par le harnais). Le fichier `dictionnaire-en.tsv` est l'extraction mécanique du bloc `tsv` de la
sortie ci-dessous ; aucune entrée n'a été éditée. Les deux seuls noms de colonnes présents dans
l'entrée, `arret_maladie` et `email_pro`, sont les exemples inventés de la documentation, déclarés
comme tels dans l'énoncé.

## Entrée — message intégral transmis au sous-agent

# Énoncé de tâche — rédaction en aveugle du dictionnaire ANGLAIS du banc de dépistage

Tu es un rédacteur **en aveugle**. Tu n'as accès à **aucun outil** : pas de lecture de fichier, pas de shell, pas de recherche web, pas de dépôt de code. Ta **seule entrée** est le présent message. N'utilise aucun outil, ne tente aucune action ; réponds en un seul message. Commence ta réponse par la déclaration : « Je n'ai consulté aucune ressource extérieure ; ma seule entrée est le présent énoncé et la section verbatim qu'il contient. »

## Contexte

Un service de dépistage lit des **noms de colonnes** de schémas SQL (jamais les valeurs) et propose, colonne par colonne, une catégorie de données personnelles prise dans une taxonomie fermée. Un banc d'essai mesurera plusieurs montages ; l'un d'eux est un moteur **« règles + lexique »** dont le dictionnaire est ce que tu vas rédiger.

Le moteur fonctionnera ainsi : il découpe chaque identifiant de colonne en jetons (séparateurs `_`, chiffres, bascules de casse type camelCase, et découpe gloutonne par dictionnaire pour les mots collés), puis cherche chaque jeton dans le dictionnaire — par **correspondance exacte insensible à la casse**, puis par **rapprochement morphologique** (abréviations, préfixes, troncatures). Un jeton reconnu signale la colonne avec la valeur portée par l'entrée. Quand plusieurs entrées déclenchent sur une même colonne, c'est l'**ordre d'arbitrage** du tableau de la taxonomie (ci-dessous) qui tranche ; ton dictionnaire n'a pas à le gérer.

## Ce que tu produis

Un **dictionnaire anglais** : une liste d'entrées `terme → valeur`.

- **Format** : un bloc de code unique ouvert par ` ```tsv `, contenant une ligne d'en-tête `terme<TAB>valeur`, puis une entrée par ligne, séparateur tabulation. Rien d'autre dans le bloc.
- **`terme`** : un seul jeton, en minuscules, sans espace ni séparateur — un mot anglais entier (`birth`, `salary`) ou une abréviation plausible dans un schéma SQL, écrite de mémoire à partir de tes connaissances générales des conventions de nommage.
- **`valeur`** : un des noms canoniques du tableau de la taxonomie ci-dessous, **sauf `Unflagged`** — un dictionnaire ne contient que ce qui se signale. `PersonalDataUncategorised` est admis mais rare : réserve-le aux termes qui disent « personnel » sans dire quoi.
- **Un terme → une seule valeur.** Si un terme seul relève de plusieurs valeurs, choisis selon l'ordre d'arbitrage du tableau (du haut vers le bas, du plus au moins coûteux à omettre).
- **Couverture** : vise large — mots entiers ET abréviations usuelles des schémas SQL anglophones, sur toutes les valeurs de la taxonomie que l'anglais des schémas peut atteindre. Ordre de grandeur attendu : 150 à 400 entrées. La langue de ce dictionnaire est l'**anglais** uniquement ; n'y mets pas de termes français (un dictionnaire français est rédigé séparément).

⚠️ Les deux noms de colonnes cités dans la section ci-dessous, `arret_maladie` et `email_pro`, sont des **exemples inventés pour la documentation** ; ils ne proviennent d'aucun schéma réel. La section est en français parce que le service est documenté en français ; ton dictionnaire, lui, est anglais.

Après le bloc, ajoute une courte note listant les choix d'arbitrage non évidents que tu as faits (termes ambigus et la valeur retenue).

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
terme	valeur
conviction	CriminalOffenceData
convictions	CriminalOffenceData
offence	CriminalOffenceData
offense	CriminalOffenceData
felony	CriminalOffenceData
misdemeanor	CriminalOffenceData
crime	CriminalOffenceData
criminal	CriminalOffenceData
arrest	CriminalOffenceData
parole	CriminalOffenceData
probation	CriminalOffenceData
prosecution	CriminalOffenceData
indictment	CriminalOffenceData
incarceration	CriminalOffenceData
warrant	CriminalOffenceData
penal	CriminalOffenceData
health	HealthData
medical	HealthData
med	HealthData
meds	HealthData
diagnosis	HealthData
dx	HealthData
disease	HealthData
illness	HealthData
sick	HealthData
sickness	HealthData
injury	HealthData
treatment	HealthData
therapy	HealthData
medication	HealthData
prescription	HealthData
rx	HealthData
dosage	HealthData
allergy	HealthData
allergies	HealthData
disability	HealthData
handicap	HealthData
blood	HealthData
vaccine	HealthData
vaccination	HealthData
immunization	HealthData
surgery	HealthData
symptom	HealthData
pathology	HealthData
psychiatric	HealthData
pregnancy	HealthData
pregnant	HealthData
hiv	HealthData
cancer	HealthData
diabetes	HealthData
bmi	HealthData
patient	HealthData
mrn	HealthData
hospital	HealthData
physician	HealthData
clinical	HealthData
religion	SpecialCategoryData
religious	SpecialCategoryData
faith	SpecialCategoryData
belief	SpecialCategoryData
ethnicity	SpecialCategoryData
ethnic	SpecialCategoryData
race	SpecialCategoryData
racial	SpecialCategoryData
sexuality	SpecialCategoryData
sexual	SpecialCategoryData
orientation	SpecialCategoryData
political	SpecialCategoryData
ideology	SpecialCategoryData
philosophical	SpecialCategoryData
genetic	SpecialCategoryData
dna	SpecialCategoryData
biometric	SpecialCategoryData
fingerprint	SpecialCategoryData
retina	SpecialCategoryData
iris	SpecialCategoryData
faceprint	SpecialCategoryData
password	AuthenticationSecret
passwd	AuthenticationSecret
pwd	AuthenticationSecret
pass	AuthenticationSecret
passphrase	AuthenticationSecret
secret	AuthenticationSecret
token	AuthenticationSecret
apikey	AuthenticationSecret
credential	AuthenticationSecret
credentials	AuthenticationSecret
hash	AuthenticationSecret
salt	AuthenticationSecret
otp	AuthenticationSecret
totp	AuthenticationSecret
mfa	AuthenticationSecret
pin	AuthenticationSecret
jwt	AuthenticationSecret
oauth	AuthenticationSecret
auth	AuthenticationSecret
ssn	NationalIdentifier
sin	NationalIdentifier
nino	NationalIdentifier
nhs	NationalIdentifier
tin	NationalIdentifier
itin	NationalIdentifier
passport	NationalIdentifier
nid	NationalIdentifier
dni	NationalIdentifier
salary	FinancialData
wage	FinancialData
wages	FinancialData
income	FinancialData
earnings	FinancialData
compensation	FinancialData
remuneration	FinancialData
payroll	FinancialData
bonus	FinancialData
iban	FinancialData
bic	FinancialData
swift	FinancialData
account	FinancialData
acct	FinancialData
bank	FinancialData
banking	FinancialData
credit	FinancialData
debit	FinancialData
card	FinancialData
pan	FinancialData
cvv	FinancialData
cvc	FinancialData
visa	FinancialData
payment	FinancialData
invoice	FinancialData
billing	FinancialData
tax	FinancialData
loan	FinancialData
mortgage	FinancialData
debt	FinancialData
balance	FinancialData
pension	FinancialData
insurance	FinancialData
routing	FinancialData
sortcode	FinancialData
bankruptcy	FinancialData
wealth	FinancialData
gps	LocationData
latitude	LocationData
longitude	LocationData
lat	LocationData
lng	LocationData
lon	LocationData
geo	LocationData
geolocation	LocationData
location	LocationData
loc	LocationData
position	LocationData
coord	LocationData
coords	LocationData
coordinates	LocationData
altitude	LocationData
waypoint	LocationData
ip	ConnectionData
mac	ConnectionData
imei	ConnectionData
imsi	ConnectionData
cookie	ConnectionData
session	ConnectionData
device	ConnectionData
hostname	ConnectionData
browser	ConnectionData
useragent	ConnectionData
ua	ConnectionData
login	ConnectionData
logon	ConnectionData
username	ConnectionData
connection	ConnectionData
name	Identity
firstname	Identity
lastname	Identity
surname	Identity
forename	Identity
fname	Identity
lname	Identity
middlename	Identity
fullname	Identity
given	Identity
maiden	Identity
nickname	Identity
alias	Identity
initials	Identity
birth	Identity
birthday	Identity
birthdate	Identity
dob	Identity
born	Identity
age	Identity
gender	Identity
sex	Identity
title	Identity
nationality	Identity
citizenship	Identity
marital	Identity
spouse	Identity
photo	Identity
picture	Identity
avatar	Identity
signature	Identity
death	Identity
deceased	Identity
email	ContactDetails
mail	ContactDetails
phone	ContactDetails
telephone	ContactDetails
tel	ContactDetails
mobile	ContactDetails
cell	ContactDetails
cellphone	ContactDetails
fax	ContactDetails
msisdn	ContactDetails
address	ContactDetails
addr	ContactDetails
street	ContactDetails
city	ContactDetails
town	ContactDetails
zip	ContactDetails
zipcode	ContactDetails
postcode	ContactDetails
postal	ContactDetails
country	ContactDetails
province	ContactDetails
residence	ContactDetails
contact	ContactDetails
skype	ContactDetails
whatsapp	ContactDetails
job	ProfessionalLife
occupation	ProfessionalLife
profession	ProfessionalLife
professional	ProfessionalLife
employer	ProfessionalLife
employee	ProfessionalLife
employment	ProfessionalLife
company	ProfessionalLife
department	ProfessionalLife
dept	ProfessionalLife
manager	ProfessionalLife
grade	ProfessionalLife
seniority	ProfessionalLife
hire	ProfessionalLife
hired	ProfessionalLife
resume	ProfessionalLife
cv	ProfessionalLife
diploma	ProfessionalLife
degree	ProfessionalLife
education	ProfessionalLife
qualification	ProfessionalLife
career	ProfessionalLife
work	ProfessionalLife
staff	ProfessionalLife
rank	ProfessionalLife
promotion	ProfessionalLife
internship	ProfessionalLife
apprentice	ProfessionalLife
retirement	ProfessionalLife
personal	PersonalDataUncategorised
pii	PersonalDataUncategorised
private	PersonalDataUncategorised
```

**Choix d'arbitrage non évidents :**

- **`sex` → Identity, `sexual`/`orientation` → SpecialCategoryData** : `sex` seul est presque toujours le sexe d'état civil (M/F) ; l'orientation sexuelle passe par d'autres jetons.
- **`biometric`, `fingerprint`, `dna`, etc. → SpecialCategoryData** : le considérant 51 conditionne la biométrie à une finalité illisible ici, mais l'ordre « du plus coûteux à omettre » plaide pour signaler haut et laisser le motif nuancer. `genetic`/`dna` sont, eux, art. 9 sans condition.
- **`position` → LocationData** (et non vie professionnelle) : ambigu GPS/poste ; tranché par l'ordre du tableau.
- **`title` → Identity** : civilité vs intitulé de poste ; Identity précède ProfessionalLife dans l'ordre.
- **`account`, `card`, `pan`, `visa` → FinancialData** : chacun a une lecture non financière (compte utilisateur, carte d'identité, PAN indien, visa d'immigration), mais l'usage dominant en schéma est bancaire/paiement.
- **`login`, `username` → ConnectionData** (identifiant/journal de connexion) et non AuthenticationSecret : un identifiant n'est pas un secret.
- **`pass`, `auth`, `token` → AuthenticationSecret** : `pass` (titre de transport) et `token` (jeton métier) ont des lectures bénignes ; le coût d'omission d'un secret l'emporte.
- **`address`, `city`, `zip`, `country` → ContactDetails** : les coordonnées postales relèvent des « coordonnées » CNIL ; LocationData est réservé au positionnement (GPS, lat/lng).
- **`insurance` → FinancialData** : l'assurance santé existe, mais le terme seul est d'abord contractuel/financier.
- **`mrn`, `patient` → HealthData** : un numéro de dossier médical révèle la qualité de patient.
- **`nhs` → NationalIdentifier** : le NHS number est un identifiant national, malgré la connotation santé.
- **`death`, `deceased` → Identity** : fait d'état civil plutôt que donnée de santé.
- **`hash`, `salt` → AuthenticationSecret** : presque toujours des dérivés de mot de passe dans une table utilisateur.
