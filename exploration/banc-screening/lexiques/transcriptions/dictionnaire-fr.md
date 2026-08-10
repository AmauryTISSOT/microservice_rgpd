# Transcription de rédaction — `dictionnaire-fr.tsv`

Rédaction **en aveugle** exécutant [#155](https://github.com/AmauryTISSOT/microservice_rgpd/issues/155)
(gel amendé décidé par [#154](https://github.com/AmauryTISSOT/microservice_rgpd/issues/154), amendement
posté sur [#130](https://github.com/AmauryTISSOT/microservice_rgpd/issues/130)). Rédacteur : sous-agent
`general-purpose` (Claude Fable 5) **sans aucun accès au dépôt** — aucun outil invoqué (0 appel d'outil,
compté par le harnais). Le fichier `dictionnaire-fr.tsv` est l'extraction mécanique du bloc `tsv` de la
sortie ci-dessous ; aucune entrée n'a été éditée. Les deux seuls noms de colonnes présents dans
l'entrée, `arret_maladie` et `email_pro`, sont les exemples inventés de la documentation, déclarés
comme tels dans l'énoncé.

## Entrée — message intégral transmis au sous-agent

# Énoncé de tâche — rédaction en aveugle du dictionnaire FRANÇAIS du banc de dépistage

Tu es un rédacteur **en aveugle**. Tu n'as accès à **aucun outil** : pas de lecture de fichier, pas de shell, pas de recherche web, pas de dépôt de code. Ta **seule entrée** est le présent message. N'utilise aucun outil, ne tente aucune action ; réponds en un seul message. Commence ta réponse par la déclaration : « Je n'ai consulté aucune ressource extérieure ; ma seule entrée est le présent énoncé et la section verbatim qu'il contient. »

## Contexte

Un service de dépistage lit des **noms de colonnes** de schémas SQL (jamais les valeurs) et propose, colonne par colonne, une catégorie de données personnelles prise dans une taxonomie fermée. Un banc d'essai mesurera plusieurs montages ; l'un d'eux est un moteur **« règles + lexique »** dont le dictionnaire est ce que tu vas rédiger.

Le moteur fonctionnera ainsi : il découpe chaque identifiant de colonne en jetons (séparateurs `_`, chiffres, bascules de casse type camelCase, et découpe gloutonne par dictionnaire pour les mots collés), puis cherche chaque jeton dans le dictionnaire — par **correspondance exacte insensible à la casse**, puis par **rapprochement morphologique** (abréviations, préfixes, troncatures). Un jeton reconnu signale la colonne avec la valeur portée par l'entrée. Quand plusieurs entrées déclenchent sur une même colonne, c'est l'**ordre d'arbitrage** du tableau de la taxonomie (ci-dessous) qui tranche ; ton dictionnaire n'a pas à le gérer.

## Ce que tu produis

Un **dictionnaire français** : une liste d'entrées `terme → valeur`.

- **Format** : un bloc de code unique ouvert par ` ```tsv `, contenant une ligne d'en-tête `terme<TAB>valeur`, puis une entrée par ligne, séparateur tabulation. Rien d'autre dans le bloc.
- **`terme`** : un seul jeton, en minuscules, sans espace ni séparateur — un mot français entier (`maladie`, `naissance`) ou une abréviation plausible dans un schéma SQL, écrite de mémoire à partir de tes connaissances générales des conventions de nommage.
- **`valeur`** : un des noms canoniques du tableau de la taxonomie ci-dessous, **sauf `Unflagged`** — un dictionnaire ne contient que ce qui se signale. `PersonalDataUncategorised` est admis mais rare : réserve-le aux termes qui disent « personnel » sans dire quoi.
- **Un terme → une seule valeur.** Si un terme seul relève de plusieurs valeurs, choisis selon l'ordre d'arbitrage du tableau (du haut vers le bas, du plus au moins coûteux à omettre).
- **Couverture** : vise large — mots entiers ET abréviations usuelles des schémas SQL francophones, sur toutes les valeurs de la taxonomie que le français des schémas peut atteindre. Ordre de grandeur attendu : 150 à 400 entrées. La langue de ce dictionnaire est le **français** uniquement ; n'y mets pas de termes anglais (un dictionnaire anglais est rédigé séparément ; les termes identiques dans les deux langues, comme des emprunts, sont admis s'ils sont usuels dans des schémas francophones).

Exemple de forme (tiré des deux exemples inventés de la section) : `maladie<TAB>HealthData`.

⚠️ Les deux noms de colonnes cités dans la section ci-dessous, `arret_maladie` et `email_pro`, sont des **exemples inventés pour la documentation** ; ils ne proviennent d'aucun schéma réel.

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
infraction	CriminalOffenceData
condamnation	CriminalOffenceData
delit	CriminalOffenceData
crime	CriminalOffenceData
casier	CriminalOffenceData
penal	CriminalOffenceData
contravention	CriminalOffenceData
amende	CriminalOffenceData
recidive	CriminalOffenceData
poursuite	CriminalOffenceData
detention	CriminalOffenceData
incarceration	CriminalOffenceData
prison	CriminalOffenceData
ecrou	CriminalOffenceData
sursis	CriminalOffenceData
verbalisation	CriminalOffenceData
judiciaire	CriminalOffenceData
justice	CriminalOffenceData
victime	CriminalOffenceData
prevenu	CriminalOffenceData
sanction	CriminalOffenceData
sante	HealthData
maladie	HealthData
medical	HealthData
medicament	HealthData
pathologie	HealthData
diagnostic	HealthData
allergie	HealthData
allergene	HealthData
handicap	HealthData
invalidite	HealthData
hospitalisation	HealthData
hopital	HealthData
vaccin	HealthData
vaccination	HealthData
mutuelle	HealthData
grossesse	HealthData
sanguin	HealthData
sang	HealthData
ordonnance	HealthData
symptome	HealthData
antecedent	HealthData
psy	HealthData
psychiatrique	HealthData
imc	HealthData
depistage	HealthData
serologie	HealthData
vih	HealthData
covid	HealthData
cancer	HealthData
diabete	HealthData
soin	HealthData
infirmier	HealthData
medecin	HealthData
consultation	HealthData
ald	HealthData
rqth	HealthData
cpam	HealthData
prothese	HealthData
tabac	HealthData
alcool	HealthData
poids	HealthData
glycemie	HealthData
vitale	HealthData
religion	SpecialCategoryData
confession	SpecialCategoryData
croyance	SpecialCategoryData
conviction	SpecialCategoryData
culte	SpecialCategoryData
ethnie	SpecialCategoryData
ethnique	SpecialCategoryData
race	SpecialCategoryData
racial	SpecialCategoryData
syndicat	SpecialCategoryData
syndical	SpecialCategoryData
opinion	SpecialCategoryData
politique	SpecialCategoryData
vote	SpecialCategoryData
militant	SpecialCategoryData
orientation	SpecialCategoryData
sexualite	SpecialCategoryData
sexuel	SpecialCategoryData
biometrie	SpecialCategoryData
biometrique	SpecialCategoryData
empreinte	SpecialCategoryData
adn	SpecialCategoryData
genetique	SpecialCategoryData
genome	SpecialCategoryData
facial	SpecialCategoryData
philosophique	SpecialCategoryData
motdepasse	AuthenticationSecret
mdp	AuthenticationSecret
passe	AuthenticationSecret
secret	AuthenticationSecret
jeton	AuthenticationSecret
sel	AuthenticationSecret
hachage	AuthenticationSecret
cle	AuthenticationSecret
pin	AuthenticationSecret
otp	AuthenticationSecret
token	AuthenticationSecret
nir	NationalIdentifier
insee	NationalIdentifier
secu	NationalIdentifier
cni	NationalIdentifier
passeport	NationalIdentifier
permis	NationalIdentifier
niss	NationalIdentifier
avs	NationalIdentifier
iban	FinancialData
bic	FinancialData
rib	FinancialData
banque	FinancialData
bancaire	FinancialData
compte	FinancialData
salaire	FinancialData
remuneration	FinancialData
paie	FinancialData
paye	FinancialData
solde	FinancialData
revenu	FinancialData
impot	FinancialData
fiscal	FinancialData
credit	FinancialData
debit	FinancialData
dette	FinancialData
endettement	FinancialData
solvabilite	FinancialData
emprunt	FinancialData
pret	FinancialData
facture	FinancialData
facturation	FinancialData
paiement	FinancialData
virement	FinancialData
cheque	FinancialData
cb	FinancialData
patrimoine	FinancialData
quotient	FinancialData
allocation	FinancialData
caf	FinancialData
retraite	FinancialData
pension	FinancialData
bourse	FinancialData
loyer	FinancialData
cotisation	FinancialData
prime	FinancialData
transaction	FinancialData
achat	FinancialData
gps	LocationData
latitude	LocationData
longitude	LocationData
lat	LocationData
lon	LocationData
lng	LocationData
geolocalisation	LocationData
geoloc	LocationData
geo	LocationData
localisation	LocationData
position	LocationData
deplacement	LocationData
trajet	LocationData
itineraire	LocationData
imei	ConnectionData
ip	ConnectionData
mac	ConnectionData
log	ConnectionData
journalisation	ConnectionData
connexion	ConnectionData
session	ConnectionData
cookie	ConnectionData
navigateur	ConnectionData
appareil	ConnectionData
login	ConnectionData
pseudo	ConnectionData
pseudonyme	ConnectionData
trace	ConnectionData
nom	Identity
prenom	Identity
naissance	Identity
naiss	Identity
nationalite	Identity
sexe	Identity
genre	Identity
civilite	Identity
civil	Identity
age	Identity
surnom	Identity
identite	Identity
conjoint	Identity
marital	Identity
matrimonial	Identity
familial	Identity
famille	Identity
enfant	Identity
deces	Identity
anniversaire	Identity
photo	Identity
tutelle	Identity
email	ContactDetails
courriel	ContactDetails
mail	ContactDetails
mel	ContactDetails
telephone	ContactDetails
tel	ContactDetails
portable	ContactDetails
mobile	ContactDetails
gsm	ContactDetails
fax	ContactDetails
adresse	ContactDetails
adr	ContactDetails
rue	ContactDetails
voie	ContactDetails
ville	ContactDetails
commune	ContactDetails
postal	ContactDetails
cp	ContactDetails
pays	ContactDetails
domicile	ContactDetails
residence	ContactDetails
coordonnee	ContactDetails
coord	ContactDetails
contact	ContactDetails
emploi	ProfessionalLife
profession	ProfessionalLife
metier	ProfessionalLife
poste	ProfessionalLife
employeur	ProfessionalLife
salarie	ProfessionalLife
matricule	ProfessionalLife
fonction	ProfessionalLife
grade	ProfessionalLife
echelon	ProfessionalLife
anciennete	ProfessionalLife
conge	ProfessionalLife
demission	ProfessionalLife
licenciement	ProfessionalLife
embauche	ProfessionalLife
recrutement	ProfessionalLife
carriere	ProfessionalLife
diplome	ProfessionalLife
formation	ProfessionalLife
competence	ProfessionalLife
cv	ProfessionalLife
cdi	ProfessionalLife
cdd	ProfessionalLife
stage	ProfessionalLife
scolarite	ProfessionalLife
travail	ProfessionalLife
evaluation	ProfessionalLife
personnel	PersonalDataUncategorised
prive	PersonalDataUncategorised
confidentiel	PersonalDataUncategorised
sensible	PersonalDataUncategorised
dcp	PersonalDataUncategorised
```

**Note sur les choix d'arbitrage non évidents :**

- **Graphie sans accents** : les entrées sont écrites sans diacritiques (`telephone`, `prenom`, `deces`), conformément à l'usage dominant des identifiants SQL ; le rapprochement morphologique est supposé rattraper d'éventuelles formes accentuées.
- **Principe appliqué** : l'ordre d'arbitrage tranche quand le terme *seul* straddle réellement deux valeurs ; quand une lecture n'existe qu'avec un qualificatif (`numero_fiscal`, `adresse_ip`), le terme garde son sens dominant non qualifié et c'est le qualificatif — lui-même au dictionnaire — qui déclenche la valeur plus haute et gagne au niveau colonne.
- `adresse` → ContactDetails : la lecture « adresse IP » passe par le jeton `ip` (ConnectionData), qui gagne par l'ordre ; inutile de sacrifier l'adresse postale.
- `coordonnee`/`coord` → ContactDetails : en français de schéma, « coordonnées » signifie presque toujours les moyens de contact ; les coordonnées GPS passent par `gps`/`lat`/`lon`.
- `fiscal` → FinancialData : sens dominant fiscal/impôts ; la lecture « identifiant fiscal » exige `numero`, trop générique pour être une entrée.
- `matricule` → ProfessionalLife : sens dominant « matricule salarié » ; le matricule sécu passe par `secu`/`nir`.
- `antecedent` → HealthData : sens dominant médical ; la lecture pénale exige `judiciaire`, déjà au dictionnaire, qui gagne par l'ordre.
- `sanction` → CriminalOffenceData : straddle réel (pénal / disciplinaire) sans qualificatif dominant, donc ordre d'arbitrage appliqué, valeur la plus haute.
- `amende` → CriminalOffenceData plutôt que FinancialData : c'est la trace d'une infraction avant d'être un montant.
- `empreinte` → SpecialCategoryData : straddle réel (empreinte digitale / empreinte cryptographique) ; l'ordre place l'art. 9 au-dessus du secret d'authentification.
- `pseudo`/`pseudonyme` → ConnectionData : straddle identité/connexion ; ConnectionData est au-dessus d'Identity dans l'ordre.
- `compte` → FinancialData : straddle réel (compte bancaire / compte utilisateur) ; FinancialData est au-dessus de ConnectionData.
- `retraite`, `pension`, `prime`, `salaire` → FinancialData plutôt que ProfessionalLife : l'ordre place le financier au-dessus de la vie professionnelle.
- `mutuelle`, `vitale`, `cpam` → HealthData : la couverture maladie dit un état de santé assuré ; pour `vitale`, Health gagne de toute façon sur NationalIdentifier par l'ordre.
- `opinion`, `vote` → SpecialCategoryData : lecture « opinions politiques » retenue malgré le risque de faux positif sur des avis clients ; le coût d'omission de l'art. 9 l'emporte.
- `genre`, `sexe` → Identity : traités comme état civil ; la lecture « orientation/vie sexuelle » passe par `orientation`, `sexualite`, `sexuel`.
- `deces` → Identity : donnée d'état civil ; la cause du décès, médicale, passe par les jetons de santé voisins.
- `sensible`, `confidentiel` → PersonalDataUncategorised : ils disent « personnel/protégé » sans dire quoi ; affirmer l'art. 9 serait dire plus que le nom de colonne ne sait.
- `cle` → AuthenticationSecret : assumé malgré le sens « clé primaire/étrangère », rare comme nom de colonne nu dans les schémas francophones.
- **Écartés volontairement** (trop ambigus pour un jeton seul) : `traitement` (salaire / soin / traitement informatique), `arret` (arrêt maladie / arrêté / stop), `taille`, `tension`, `visite`, `origine`, `plainte`, `code`, `montant`, `carte`, `identifiant`, `historique`, `sejour`, `entretien`.
