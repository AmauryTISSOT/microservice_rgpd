# Les 79 désaccords, classés en trois piles

*Classement rendu le 2026-08-09, selon la règle pré-enregistrée de [`ARBITRAGE.md`](./ARBITRAGE.md), commitée avant que la première ligne soit ouverte. L'ordre se vérifie dans `git log`.*

79 désaccords sur les 300 colonnes du double codage.

| Pile | Effectif | Criblage annoncé par #145 |
|---|---:|---:|
| **A** | 41 | ~20 |
| **B** | 29 | ~28 |
| **C** | 9 | ~31 |
| **Total** | **79** | **79** |

⚠️ **Les dimensionnements de #145 étaient des criblages lexicaux, pas des constats** ; ce tableau les remplace. L'écart se publie tel quel.

## Sur la Pile A, qui appliquait le protocole ?

⚠️ **Ce tableau se calcule**, il ne s'affirme pas : chaque ligne de Pile A porte la catégorie que le § invoqué **prescrit**, et la conformité de chaque passe en est déduite. C'est la seule pile où la question a un sens — ailleurs, aucun § ne prescrit rien.

| Passe | Conforme au § | sur |
|---|---:|---:|
| Étiquette machine | 39 | 41 |
| Référence humaine | 0 | 41 |

## Les quatre conditions de réfutation

Écrites dans `ARBITRAGE.md` **avant** les chiffres. Elles s'évaluent ici, et le verdict de chacune est mécanique.

1. **La Pile B est-elle majoritairement l'héritage du domaine par le nom de la table ?** — 25 sur 29. **Non déclenchée** : le § 3.10 tranche bien la famille dominante de B.
   ⚠️ Mais **4 ligne(s) de Pile B restent ouvertes, sans règle et hors de portée du § 3.10** — elles se nomment ci-dessous et ne sont fermées par aucun geste de ce ticket.
   - identifiant de compte nominatif (1)
   - contenu applicatif rattaché à une personne (1)
   - auteur d'un artefact logiciel (1)
   - désignation de personnes hors forme de clé étrangère (1)
2. **La Pile A est-elle vide ou quasi vide ?** — 41 sur 79, soit la pile la plus lourde. **Non déclenchée** : le diagnostic de #145 sur la Pile A est confirmé, et au-delà de ce qu'il annonçait.
3. **La Pile C domine-t-elle ?** — 9 sur 79. **Non déclenchée** : le pari de #145 porte sur un reste, et sur un reste trois fois plus petit que son criblage ne l'annonçait.
4. **Un désaccord n'entre-t-il dans aucune pile ?** — non : les 79 désaccords sont classés, et `piles.py` refuse de publier sinon. ⚠️ **Un cas limite se signale quand même** : `localtax1_type` et `localtax2_tx`, voisines dans la même table, tombent en A et en B — la première est nommée par la convention `type`, la seconde par rien. L'écart est le prix d'une règle appliquée à la lettre plutôt qu'au goût.

## Concentration par schéma

| Schéma | Désaccords | A | B | C |
|---|---:|---:|---:|---:|
| `openemr` | 33 | 10 | 22 | 1 |
| `dolibarr` | 13 | 9 | 2 | 2 |
| `sacoche` | 12 | 8 | 3 | 1 |
| `galette` | 9 | 5 | 1 | 3 |
| `glpi` | 4 | 3 | 0 | 1 |
| `paheko-head` | 3 | 2 | 1 | 0 |
| `paheko-0.8.0` | 2 | 2 | 0 | 0 |
| `temoin` | 2 | 1 | 0 | 1 |
| `paheko-1.0.0` | 1 | 1 | 0 | 0 |

## Pile A — le protocole tranche déjà, sans interprétation — 41 colonnes

### `dolibarr` · `llx_actioncomm.note`

- **§ 3.3 cas 2 prescrit `PersonalDataUncategorised`**
- machine ✓ `PersonalDataUncategorised` — « champ libre d'un objet rattaché à une personne par `fk_contact` »
- humain ✗ `Unflagged` — « — »
- champ libre nommé par le § 3.3 ; `llx_actioncomm` porte `fk_soc`, `fk_contact`, `fk_user_author` — objet rattaché à une personne, visible dans le pivot

### `dolibarr` · `llx_bank.label`

- **conventions, famille `label` (§ 3.4) prescrit `Unflagged`**
- machine ✗ `PersonalDataUncategorised` — « libellé libre d'une écriture bancaire rattachée à un emetteur nommé ; contenu indéterminable depuis le schéma »
- humain ✗ `FinancialData` — « Car situé dans la table bank »
- `label` est nommé par les conventions communes, pas par la liste du § 3.3 ; `llx_bank` est une table d'objets

### `dolibarr` · `llx_bank.note`

- **§ 3.3 cas 2 prescrit `PersonalDataUncategorised`**
- machine ✗ `Unflagged` — « — »
- humain ✗ `FinancialData` — « Car situé dans la colonne bank »
- `note` est dans la liste du § 3.3 ; `llx_bank` porte `fk_user_author` et `fk_user_rappro`, personnes au sens du § 3.1

### `dolibarr` · `llx_c_accounting_category.rowid`

- **§ 3.1 prescrit `Unflagged`**
- machine ✓ `Unflagged` — « — »
- humain ✗ `PersonalDataUncategorised` — « nom de la table »
- `rowid` est nommé littéralement par le § 3.1 ; la table est en outre une nomenclature (§ 3.4)

### `dolibarr` · `llx_c_type_contact.libelle`

- **§ 3.4 prescrit `Unflagged`**
- machine ✓ `Unflagged` — « — »
- humain ✗ `ContactDetails` — « nom de la table »
- `type_contact` est cité nommément comme table de nomenclature par le § 3.2 ; le § 3.4 met toutes ses colonnes en `Unflagged`

### `dolibarr` · `llx_commande_fournisseurdet.label`

- **conventions, famille `label` (§ 3.4) prescrit `Unflagged`**
- machine ✓ `Unflagged` — « — »
- humain ✗ `PersonalDataUncategorised` — « nom de la table »
- ligne de commande fournisseur : table d'objets

### `dolibarr` · `llx_commande_fournisseurdet.localtax1_type`

- **conventions, famille `type` (§ 3.1) prescrit `Unflagged`**
- machine ✓ `Unflagged` — « — »
- humain ✗ `PersonalDataUncategorised` — « nom de la table »
- ⚠️ voisine de `localtax2_tx`, classée B : la première est nommée par la convention `type`, la seconde n'est nommée par rien. Écart assumé, la règle s'applique à la lettre

### `dolibarr` · `llx_product_customer_price_log.fk_soc`

- **§ 3.2 + § 3.6 prescrit `Identity`**
- machine ✓ `Identity` — « Clé étrangère nommée fk_soc, qui désigne une société cliente ; le paragraphe 3.6 fait entrer le professionnel indépendant, la société pouvant être une personne physique. »
- humain ✗ `Unflagged` — « — »
- `societe` figure nommément dans la liste des tables de personnes du § 3.2 ; le § 3.6 fait entrer l'indépendant

### `dolibarr` · `llx_societe_remise_supplier.fk_user_author`

- **§ 3.1, ligne `fk_user_creat` et assimilés prescrit `Identity`**
- machine ✓ `Identity` — « désigne l'utilisateur auteur de l'écriture ; § 3.1 fait entrer l'agent comme personne physique »
- humain ✗ `ConnectionData` — « foreign key »
- ⚠️ le motif humain dit « foreign key » alors que la catégorie saisie est `ConnectionData` — signature d'adjacence 8/9 relevée par le VERDICT

### `galette` · `galette_adherents.parent_id`

- **§ 3.2 prescrit `Identity`**
- machine ✓ `Identity` — « clé étrangère vers `galette_adherents`, table de personnes : désigne l'adhérent parent »
- humain ✗ `Unflagged` — « — »
- `table_referencee` est déclarée et vaut `galette_adherents`, table de personnes — le cas le plus mécanique du § 3.2

### `galette` · `galette_adherents.pays_adh`

- **conventions, famille adresse postale prescrit `ContactDetails`**
- machine ✓ `ContactDetails` — « pays de l'adresse postale de l'adhérent »
- humain ✗ `Unflagged` — « — »
- `pays` est nommé par la ligne d'arbitrage qui écarte explicitement `LocationData`

### `galette` · `galette_adherents.region_adh`

- **conventions, famille adresse postale prescrit `ContactDetails`**
- machine ✓ `ContactDetails` — « région de l'adresse postale de l'adhérent »
- humain ✗ `Unflagged` — « — »
- élément d'adresse déclarée dans une table de personnes, au même titre que `pays_adh` et `ville_adh` du même bloc

### `galette` · `galette_adherents.tel_adh`

- **conventions, famille `tel` prescrit `ContactDetails`**
- machine ✓ `ContactDetails` — « `tel` = numéro de téléphone de l'adhérent »
- humain ✗ `Unflagged` — « — »
- cas cité par #145 comme exemple de Pile A ; confirmé

### `galette` · `galette_transactions.trans_amount`

- **§ 3.9 prescrit `FinancialData`**
- machine ✓ `FinancialData` — « Montant d'une transaction rattaché à une personne par `id_adh`, qui identifie l'adhérent concerné. »
- humain ✗ `Unflagged` — « — »
- `amount` est nommé par le § 3.9 et la table porte `id_adh` : rattachement visible dans le schéma

### `glpi` · `glpi_reminders.users_id`

- **conventions, famille `users_id` (§ 3.2) prescrit `Identity`**
- machine ✓ `Identity` — « suffixe `users_id` désignant la table des utilisateurs `glpi_users` : clé étrangère vers une personne (l'auteur du rappel), tranchée sur le nom faute de contrainte déclarée. »
- humain ✗ `ConnectionData` — « nom de la colonne »
- ⚠️ humain à `ConnectionData` avec pour motif « nom de la colonne » — signature d'adjacence 8/9

### `glpi` · `glpi_softwares.locations_id`

- **§ 3.2, branche nomenclature prescrit `Unflagged`**
- machine ✓ `Unflagged` — « — »
- humain ✗ `LocationData` — « nom de la colonne »
- `glpi_locations` est une nomenclature de lieux d'équipement, pas une personne

### `glpi` · `glpi_transfers.keep_dc_phone`

- **§ 3.4 prescrit `Unflagged`**
- machine ✓ `Unflagged` — « — »
- humain ✗ `ContactDetails` — « nom de la colonne »
- `glpi_transfers` est une table de configuration — 38 colonnes `keep_*` / `clean_*` ; le § 3.4 met toute la table en `Unflagged`

### `openemr` · `amendments.modified_by`

- **§ 3.1, ligne `user_modif` et assimilés prescrit `Identity`**
- machine ✓ `Identity` — « le commentaire dit `references users.id` : désigne l'utilisateur auteur de la modification (§ 3.1). »
- humain ✗ `Unflagged` — « — »
- le commentaire de colonne dit `references users.id`, donc le pivot porte lui-même la cible

### `openemr` · `api_refresh_token.revoked`

- **conventions, famille `statut` / `is_*` (§ 3.1) prescrit `Unflagged`**
- machine ✓ `Unflagged` — « — »
- humain ✗ `ConnectionData` — « commentaire »
- drapeau applicatif, commentaire `1=revoked,0=not revoked`

### `openemr` · `form_eye_mag_impplan.pid`

- **conventions, famille `pid` (§ 3.2) prescrit `Identity`**
- machine ✓ `Identity` — « colonne `pid` : identifiant du patient, clé étrangère désignant une personne (convention `pid` → Identity). »
- humain ✗ `Unflagged` — « — »
- seule colonne `form_eye_*` du tirage que le protocole tranchait déjà

### `openemr` · `insurance_data.subscriber_employer_country`

- **conventions, famille adresse postale prescrit `ContactDetails`**
- machine ✓ `ContactDetails` — « pays d'une adresse postale rattachée au souscripteur via son employeur ; ProfessionalLife écarté par l'ordre d'arbitrage. »
- humain ✗ `LocationData` — « Nom de la colonne »
- ⚠️ humain à `LocationData` — signature d'adjacence 7/10, et la ligne d'arbitrage écarte nommément `LocationData` pour l'adresse déclarée

### `openemr` · `insurance_data.subscriber_mname`

- **conventions, famille `name` en table de personnes (§ 3.2) prescrit `Identity`**
- machine ✓ `Identity` — « `mname` nomme le second prénom du souscripteur. »
- humain ✗ `ConnectionData` — « Nom de la colonne »
- cas cité par #145 comme exemple de Pile A ; confirmé. Encadré par `subscriber_lname` et `subscriber_fname` dans le même pivot

### `openemr` · `insurance_data.type`

- **conventions, famille `type` (§ 3.1) prescrit `Unflagged`**
- machine ✓ `Unflagged` — « — »
- humain ✗ `PersonalDataUncategorised` — « Nom de la colonne »
- énumération `primary/secondary/tertiary` — mécanique applicative

### `openemr` · `patient_care_experience_preferences.value_code_system`

- **§ 3.2, branche nomenclature prescrit `Unflagged`**
- machine ✓ `Unflagged` — « — »
- humain ✗ `HealthData` — « Nom de la table »
- le commentaire dit `fk to preference_value_sets.answer_system` : le pivot porte la cible, et c'est une nomenclature. ⚠️ l'humain a inversé l'héritage du domaine, motif « Nom de la table »

### `openemr` · `person_patient_link.id`

- **§ 3.1, clé primaire prescrit `Unflagged`**
- machine ✓ `Unflagged` — « — »
- humain ✗ `HealthData` — « Nom de la table »
- ⚠️ l'humain a étiqueté `HealthData` sur une clé primaire, motif « Nom de la table » — héritage du domaine appliqué là où le § 3.1 l'interdit déjà

### `openemr` · `phone_numbers.id`

- **§ 3.1, clé primaire prescrit `Unflagged`**
- machine ✓ `Unflagged` — « — »
- humain ✗ `ContactDetails` — « Nom de la table »
- même cas que `person_patient_link.id`

### `openemr` · `procedure_order_relationships.created_by`

- **§ 3.1, ligne `fk_user_creat` et assimilés prescrit `Identity`**
- machine ✓ `Identity` — « le commentaire dit `User who created this link` : désigne l'utilisateur auteur de l'écriture (§ 3.1). »
- humain ✗ `ConnectionData` — « Nom de la table »
- le commentaire dit `User who created this link`

### `paheko-0.8.0` · `compta_exercices.cloture`

- **§ 3.4 et § 3.9 branche 2 prescrit `Unflagged`**
- machine ✓ `Unflagged` — « — »
- humain ✗ `FinancialData` — « Nom de la colonne »
- table d'exercices comptables sans aucune personne ; `cloture` est un drapeau de clôture

### `paheko-0.8.0` · `rappels_envoyes.id_membre`

- **§ 3.2 prescrit `Identity`**
- machine ✓ `Identity` — « clé étrangère déclarée vers `membres`, table de personnes. »
- humain ✗ `Unflagged` — « — »
- `table_referencee` déclarée vers `membres`

### `paheko-1.0.0` · `membres_sessions.hash`

- **§ 3.8 prescrit `AuthenticationSecret`**
- machine ✓ `AuthenticationSecret` — « Condensat du jeton de session d'un membre : empreinte d'un secret d'authentification. »
- humain ✗ `Unflagged` — « — »
- la source de l'empreinte se lit dans le nom de la table, `membres_sessions` — exactement la clause du § 3.8

### `paheko-head` · `files_shares.id_user`

- **§ 3.2 prescrit `Identity`**
- machine ✓ `Identity` — « clé étrangère déclarée vers `users`, table de personnes. »
- humain ✗ `Unflagged` — « — »
- `table_referencee` déclarée vers `users`

### `paheko-head` · `mailings.sender_email`

- **conventions, famille `email` prescrit `ContactDetails`**
- machine ✓ `ContactDetails` — « `sender_email` porte l'adresse de courriel de l'expéditeur. »
- humain ✗ `Unflagged` — « — »
- coordonnée nommée par la convention

### `sacoche` · `sacoche_image.user_id`

- **conventions, famille `user_id` prescrit `Identity`**
- machine ✓ `Identity` — « `user_id` désigne l'utilisateur auquel l'image est rattachée. »
- humain ✗ `Unflagged` — « — »
- clé étrangère nommée, contrainte non déclarée chez SACoche — le § 3.2 prévoit exactement ce repli

### `sacoche` · `sacoche_jointure_devoir_eleve.jointure_memo_autoeval`

- **§ 3.3 cas 2 prescrit `PersonalDataUncategorised`**
- machine ✓ `PersonalDataUncategorised` — « mémo d'auto-évaluation rattaché à un élève identifié ; contenu indéterminable depuis le schéma. »
- humain ✗ `Unflagged` — « — »
- `memo` est dans la liste du § 3.3 ; la table porte `eleve_id`

### `sacoche` · `sacoche_livret_jointure_ap_prof.prof_id`

- **conventions, famille `prof_id` prescrit `Identity`**
- machine ✓ `Identity` — « le nom `prof_id` désigne un enseignant, donc une personne physique ; clé étrangère vers une table de personnes au sens du § 3.2, la contrainte n'étant pas déclarée chez SACoche. »
- humain ✗ `Unflagged` — « — »
- nommé littéralement par la convention

### `sacoche` · `sacoche_livret_jointure_modaccomp_eleve.eleve_id`

- **§ 3.2 prescrit `Identity`**
- machine ✓ `Identity` — « le préfixe `eleve_` désigne un élève, donc une personne physique. »
- humain ✗ `Unflagged` — « — »
- `eleve` figure nommément dans la liste des tables de personnes du § 3.2

### `sacoche` · `sacoche_officiel_archive.user_id`

- **conventions, famille `user_id` prescrit `Identity`**
- machine ✓ `Identity` — « `user_id` désigne un utilisateur du logiciel, donc une personne physique. »
- humain ✗ `Unflagged` — « — »
- nommé littéralement par la convention

### `sacoche` · `sacoche_officiel_saisie.prof_id`

- **conventions, famille `prof_id` prescrit `Identity`**
- machine ✓ `Identity` — « le nom `prof_id` désigne un enseignant, donc une personne physique ; clé étrangère vers une table de personnes au sens du § 3.2, la contrainte n'étant pas déclarée chez SACoche. »
- humain ✗ `Unflagged` — « — »
- nommé littéralement par la convention

### `sacoche` · `sacoche_parent_adresse.adresse_postal_code`

- **conventions, famille adresse postale prescrit `ContactDetails`**
- machine ✓ `ContactDetails` — « Code postal d'une adresse de parent, confirmé par le commentaire renvoyant à la notice « Code postal » ; élément d'adresse déclarée. »
- humain ✗ `LocationData` — « Nom de la colonne »
- ⚠️ humain à `LocationData` — signature d'adjacence 7/10, contre une ligne d'arbitrage qui écarte nommément `LocationData`

### `sacoche` · `sacoche_user.user_email_refus`

- **conventions, famille `is_*` / drapeau (§ 3.1) prescrit `Unflagged`**
- machine ✓ `Unflagged` — « — »
- humain ✗ `ProfessionalLife` — « erreur ici$ »
- ⚠️ c'est la ligne dont le motif humain dit « erreur ici$ » — la saisie se déclare elle-même fausse

### `temoin` · `adresses.client_id`

- **§ 3.2 prescrit `Identity`**
- machine ✓ `Identity` — « Clé étrangère déclarée vers `clients`, table de personnes : la colonne désigne le client, elle ne stocke qu'un entier. »
- humain ✗ `Unflagged` — « — »
- `table_referencee` déclarée vers `clients`

## Pile B — aucune règle n'existe — 29 colonnes

### `dolibarr` · `llx_adherent.login`

- famille absente du protocole : **identifiant de compte nominatif**
- machine `Identity` — « Identifiant de compte nominatif de l'adhérent ; écarte ConnectionData, qui vise les traces de connexion (IP, session, dernière connexion) et non le nom du compte. »
- humain `PersonalDataUncategorised` — « Colonne située dans la table login »
- aucun § ne nomme `login` ; ce n'est ni un secret (journal, `token`/`password`) ni une trace de connexion (`last_login`). ⚠️ B hors héritage du domaine

### `dolibarr` · `llx_commande_fournisseurdet.localtax2_tx`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `Unflagged` — « — »
- humain `PersonalDataUncategorised` — « nom de la table »
- taux de taxe d'une ligne de commande, nommé par aucun § ; l'humain a étiqueté `PersonalDataUncategorised` avec pour motif « nom de la table »

### `galette` · `galette_searches.parameters`

- famille absente du protocole : **contenu applicatif rattaché à une personne**
- machine `PersonalDataUncategorised` — « critères de recherche enregistrés au compte de l'adhérent référencé par `id_adh` ; contenu indéterminable depuis le schéma »
- humain `Unflagged` — « — »
- critères de recherche enregistrés sous `id_adh` ; aucun § ne dit si un paramétrage stocké au compte d'une personne est une donnée sur elle. ⚠️ B hors héritage du domaine

### `openemr` · `benefit_eligibility.percent`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `FinancialData` — « la table `benefit_eligibility` décrit les droits et la couverture d'assurance d'un assuré ; la colonne porte un élément de sa prise en charge financière. »
- humain `Unflagged` — « — »
- `percent` n'est pas dans la famille nommée par le § 3.9 ; la machine a conclu depuis le domaine que `benefit_eligibility` nomme

### `openemr` · `billing.encounter`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « `encounter` nomme la rencontre de soins du patient ; l'existence d'une consultation rattachée à `pid` est une donnée de santé. »
- humain `Unflagged` — « — »
- aucun § ne nomme `encounter` ; la machine a conclu `HealthData` depuis le domaine médical du schéma

### `openemr` · `facility_user_ids.field_value`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `ProfessionalLife` — « valeur d'un identifiant de l'utilisateur auprès d'un établissement, que le nom de la table `facility_user_ids` qualifie comme professionnel. »
- humain `ConnectionData` — « Nom de la table »
- couple générique `field_id` / `field_value` ; la machine a conclu depuis le nom de la table

### `openemr` · `form_eye_antseg.OSSCHIRMER1`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « la table `form_eye_antseg` est un formulaire d'examen du segment antérieur de l'œil rattaché à un patient (`pid`) ; la colonne porte un relevé clinique. »
- humain `Unflagged` — « — »
- famille `form_eye_*` — le nom de colonne est illisible, le nom de table nomme le domaine

### `openemr` · `form_eye_hpi.CC3`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « la table `form_eye_hpi` est un formulaire d'anamnèse oculaire (HPI, histoire de la maladie actuelle) rattaché à un patient (`pid`) ; la colonne porte un élément de la plainte et de son histoire. »
- humain `Unflagged` — « — »
- famille `form_eye_*`

### `openemr` · `form_eye_hpi.CONTEXT1`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « la table `form_eye_hpi` est un formulaire d'anamnèse oculaire (HPI, histoire de la maladie actuelle) rattaché à un patient (`pid`) ; la colonne porte un élément de la plainte et de son histoire. »
- humain `Unflagged` — « — »
- famille `form_eye_*`

### `openemr` · `form_eye_hpi.DURATION3`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « la table `form_eye_hpi` est un formulaire d'anamnèse oculaire (HPI, histoire de la maladie actuelle) rattaché à un patient (`pid`) ; la colonne porte un élément de la plainte et de son histoire. »
- humain `Unflagged` — « — »
- famille `form_eye_*`

### `openemr` · `form_eye_hpi.TIMING2`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « la table `form_eye_hpi` est un formulaire d'anamnèse oculaire (HPI, histoire de la maladie actuelle) rattaché à un patient (`pid`) ; la colonne porte un élément de la plainte et de son histoire. »
- humain `Unflagged` — « — »
- famille `form_eye_*`

### `openemr` · `form_eye_mag_impplan.codetext`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « la table `form_eye_mag_impplan` est un formulaire d'impression diagnostique et de plan de soins rattaché à un patient (`pid`). »
- humain `Unflagged` — « — »
- famille `form_eye_*`

### `openemr` · `form_eye_mag_impplan.codetype`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « la table `form_eye_mag_impplan` est un formulaire d'impression diagnostique et de plan de soins rattaché à un patient (`pid`). »
- humain `Unflagged` — « — »
- famille `form_eye_*` ; ⚠️ `codetype` serait `Unflagged` par la convention `type` si le § 3.10 ne primait pas — cas limite signalé

### `openemr` · `form_eye_mag_wearing.LENS_TREATMENTS`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « la table `form_eye_mag_wearing` est un formulaire de correction optique portée rattaché à un patient (`PID`) ; la colonne porte une mesure de réfraction ou un élément de prescription. »
- humain `Unflagged` — « — »
- famille `form_eye_*`

### `openemr` · `form_eye_mag_wearing.ODSPH`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « la table `form_eye_mag_wearing` est un formulaire de correction optique portée rattaché à un patient (`PID`) ; la colonne porte une mesure de réfraction ou un élément de prescription. »
- humain `Unflagged` — « — »
- famille `form_eye_*` — le cas nommé par #145

### `openemr` · `form_eye_mag_wearing.OSMPDD`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « la table `form_eye_mag_wearing` est un formulaire de correction optique portée rattaché à un patient (`PID`) ; la colonne porte une mesure de réfraction ou un élément de prescription. »
- humain `Unflagged` — « — »
- famille `form_eye_*`

### `openemr` · `form_eye_mag_wearing.OSVPD`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « la table `form_eye_mag_wearing` est un formulaire de correction optique portée rattaché à un patient (`PID`) ; la colonne porte une mesure de réfraction ou un élément de prescription. »
- humain `Unflagged` — « — »
- famille `form_eye_*`

### `openemr` · `form_group_attendance.encounter_id`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « désigne la rencontre clinique (encounter) rattachée à la séance : l'existence d'une consultation est une donnée de santé »
- humain `Unflagged` — « — »
- `encounter_id` désigne une table qui n'est ni une personne ni une nomenclature : le § 3.2 ne conclut pas, et c'est le domaine qui a tranché

### `openemr` · `forms.issue_id`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « le commentaire dit qu'elle référence `lists.id` pour identifier un cas : elle désigne un problème médical du dossier »
- humain `Unflagged` — « — »
- le commentaire dit `references lists.id` — ni personne ni nomenclature ; la machine a conclu depuis le domaine

### `openemr` · `history_data.dc_siblings`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `PersonalDataUncategorised` — « l'abréviation `dc` n'est pas lisible depuis le seul schéma ; le suffixe rattache la colonne à la fratrie du patient, donc à une personne, sans que la nature du fait soit déterminable »
- humain `Unflagged` — « — »
- abréviation `dc` illisible depuis le pivot ; la machine a conclu au rattachement depuis le nom de la table

### `openemr` · `history_data.hazardous_activities`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « la table `history_data` recense les antécédents d'un patient (`pid`) et la colonne nomme les activités à risque, relevé comme anamnèse médicale »
- humain `Unflagged` — « — »
- `history_data` nomme les antécédents d'un patient

### `openemr` · `history_data.history_offspring`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « le nom désigne les antécédents de les enfants du patient : un antécédent familial porté au dossier est une donnée de santé »
- humain `Unflagged` — « — »
- antécédent familial ; nommé par aucun §

### `openemr` · `history_data.value_1`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `PersonalDataUncategorised` — « valeur d'un champ personnalisable apparié à `name_1`, rattaché au dossier d'un patient ; contenu indéterminable depuis le schéma (§ 3.3) »
- humain `Unflagged` — « — »
- champ personnalisable apparié à `name_1` ; `value_1` n'est pas dans la liste des champs libres du § 3.3

### `openemr` · `procedure_specimen.procedure_order_id`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « le commentaire indique qu'elle renvoie à `procedure_order` : elle désigne la prescription d'un acte médical, et une clé étrangère s'annote sur ce qu'elle désigne (§ 3.2) »
- humain `Unflagged` — « — »
- clé étrangère vers une table d'objet ; le § 3.2 ne conclut pas et le domaine a tranché

### `openemr` · `syndromic_surveillance.lists_id`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `HealthData` — « la table nomme une surveillance syndromique, déclaration sanitaire de cas, et la colonne désigne l'entrée de `lists` qui porte le cas déclaré »
- humain `Unflagged` — « — »
- `lists` est un fourre-tout OpenEMR ; la machine a conclu depuis le nom de la table porteuse

### `paheko-head` · `modules.author`

- famille absente du protocole : **auteur d'un artefact logiciel**
- machine `Identity` — « `author` nomme l'auteur du greffon ou du module ; le nom d'un auteur peut être celui d'une personne physique (§ 3.6). »
- humain `ConnectionData` — « Nom de la colonne »
- aucun § ne dit si l'auteur d'un greffon est une personne concernée ; `modules` ne porte aucune personne. ⚠️ B hors héritage du domaine

### `sacoche` · `sacoche_acces_historique.acces_mode`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `ConnectionData` — « mode d'accès : caractérise la façon dont l'utilisateur s'est connecté. »
- humain `Unflagged` — « — »
- `acces_mode` n'est pas dans la liste de la convention `ConnectionData` ; c'est la table qui nomme le domaine

### `sacoche` · `sacoche_courriel_erreur.erreur_users`

- famille absente du protocole : **désignation de personnes hors forme de clé étrangère**
- machine `Identity` — « liste des utilisateurs concernés par l'erreur d'envoi ; la colonne désigne des personnes. »
- humain `Unflagged` — « — »
- `varchar(500)` portant une liste d'utilisateurs ; le § 3.2 ne nomme que les formes de clé étrangère. ⚠️ B hors héritage du domaine

### `sacoche` · `sacoche_jointure_plan_eleve.jointure_rangee`

- famille absente du protocole : **héritage du domaine par le nom de la table**
- machine `PersonalDataUncategorised` — « rangée occupée par un élève identifié dans le plan de classe : fait sur la personne, et non géolocalisation. »
- humain `Unflagged` — « — »
- rangée d'un plan de classe : fait sur l'élève désigné par `eleve_id`, nommé par aucun §

## Pile C — un § s'applique mais ne conclut pas seul — 9 colonnes

### `dolibarr` · `llx_bank.origin_id`

- **§ 3.2 sans conclusion**
- machine `Unflagged` — « — »
- humain `FinancialData` — « car situé dans la table bank »
- clé étrangère polymorphe — `origin_type` la qualifie à l'exécution ; ni personne ni nomenclature ne se lisent dans le pivot

### `dolibarr` · `llx_commande_fournisseurdet.fk_commande`

- **§ 3.2 sans conclusion**
- machine `Unflagged` — « — »
- humain `PersonalDataUncategorised` — « nom de la table »
- clé étrangère vers une table d'objet — le § 3.2 n'a que deux branches, personne et nomenclature, et aucune ne couvre celle-ci

### `galette` · `galette_adherents.societe_adh`

- **§ 3.6 sans conclusion de catégorie**
- machine `ProfessionalLife` — « raison sociale rattachée à l'adhérent ; personnelle au titre du § 3.6 car elle peut désigner un professionnel indépendant, mais elle relève de la vie professionnelle et non de l'état civil »
- humain `Unflagged` — « — »
- le § 3.6 impose d'étiqueter comme personnel mais ne nomme aucune catégorie ; `ProfessionalLife`, `Identity` et `ContactDetails` restent en concurrence

### `galette` · `galette_cotisations.date_debut_cotis`

- **conventions, famille `date` — clause « la qualification prime » sans catégorie nommée**
- machine `PersonalDataUncategorised` — « début de la période d'adhésion : fait daté sur l'adhérent référencé par `id_adh`, et non horodatage d'écriture »
- humain `Unflagged` — « — »
- la convention dit que `date_naissance` échappe à `Unflagged`, mais ne dit pas quelle catégorie prend une date qualifiée qui n'est pas une naissance

### `galette` · `galette_cotisations.info_cotis`

- **§ 3.3, famille énumérée**
- machine `PersonalDataUncategorised` — « champ libre dans une table qui référence l'adhérent ; contenu indéterminable depuis le schéma »
- humain `Unflagged` — « — »
- `info` ne figure ni dans la liste du § 3.3 ni dans celle des conventions ; décider que c'en est un champ libre, c'est lire la colonne

### `glpi` · `glpi_printers.contact_num`

- **conventions, famille coordonnées**
- machine `ContactDetails` — « suffixe `_num` accolé à `contact` : numéro de la personne de contact, donc une coordonnée »
- humain `Unflagged` — « — »
- `contact_num` n'est nommé par aucune ligne ; conclure au numéro de téléphone demande de lire le suffixe `_num`

### `openemr` · `addresses.plus_four`

- **conventions, famille adresse postale**
- machine `ContactDetails` — « complément à quatre chiffres du code postal américain (ZIP+4), qui affine l'adresse postale ; LocationData est écarté car il vise la géolocalisation, pas l'adresse déclarée (conventions communes) »
- humain `PersonalDataUncategorised` — « Nom de la table et de la colonne »
- `plus_four` n'est nommé par rien ; les deux passes s'accordent sur le caractère personnel et divergent sur la catégorie

### `sacoche` · `sacoche_user.user_sconet_elenoet`

- **conventions, famille `code` / `ref` et sa clause « sauf si le nom qualifie une personne »**
- machine `Identity` — « Numéro d'élève dans l'établissement (ELENOET, dit n° GEP d'après le commentaire) : identifiant local d'une personne, pas un identifiant national. »
- humain `Unflagged` — « — »
- la clause existe mais exige de lire le nom ; le commentaire dit ELENOET, numéro d'élève — identifiant local, et le § ne dit pas s'il vaut `Identity` ou `NationalIdentifier`

### `temoin` · `adresses.destinataire`

- **§ 2, ordre d'arbitrage entre deux § applicables**
- machine `Identity` — « Dans un bloc d'adresse postale, le destinataire est le nom de la personne à qui le pli est adressé ; `varchar(160)` est une longueur de nom complet. »
- humain `LocationData` — « Nom de la colonne »
- `destinataire` dans un bloc d'adresse : nom de personne (`Identity`, rang 9) ou élément d'adresse (`ContactDetails`, rang 10) ; départager, c'est lire la colonne

