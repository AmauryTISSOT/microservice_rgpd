# Divergences d'étiquetage entre lots

3254 colonnes annotées. Un lot est un annotateur ; ce rapport cherche les colonnes de même nom qu'ils ont tranchées différemment.

⚠️ **Une divergence n'est pas forcément une erreur** : le § 3.3 et le § 3.4 font légitimement dépendre l'étiquette de la table. Ce rapport classe, il ne corrige pas.

## Intra-schéma — même logiciel — 25 nom(s) de colonne

### `paheko-1.0.0` · `id` — 21 occurrence(s)

- **Unflagged** (20) — `acc_accounts`, `acc_charts`, `acc_transactions`, `acc_transactions_lines` +16
- **Identity** (1) — `fichiers_membres`
  - motif : « malgré son nom, la colonne porte une clé étrangère déclarée vers `membres` ; § 3.2 (ce qu'elle désigne) prime sur § 3.1 (clé primaire). »

### `paheko-0.8.0` · `id` — 20 occurrence(s)

- **Unflagged** (19) — `compta_categories`, `compta_comptes`, `compta_comptes_bancaires`, `compta_exercices` +15
- **Identity** (1) — `fichiers_membres`
  - motif : « malgré son nom, la colonne porte une clé étrangère déclarée vers `membres` ; § 3.2 (ce qu'elle désigne) prime sur § 3.1 (clé primaire). »

### `sacoche` · `user_id` — 15 occurrence(s)

- **Identity** (14) — `sacoche_acces_historique`, `sacoche_catalogue_appreciation`, `sacoche_catalogue_categorie`, `sacoche_image` +10
  - motif : « `user_id` désigne l'utilisateur dont l'accès est historisé ; clé étrangère vers une table de personnes. »
- **Unflagged** (1) — `sacoche_user`

### `dolibarr` · `label` — 14 occurrence(s)

- **Unflagged** (11) — `llx_webhook_target`, `llx_tva`, `llx_accounting_transaction_template`, `llx_c_product_nature` +7
- **PersonalDataUncategorised** (2) — `llx_actioncomm`, `llx_bank`
  - motif : « intitulé libre d'un événement rattaché par clé étrangère à un contact ou à un utilisateur ; contenu indéterminable depuis le schéma »
- **Identity** (1) — `llx_establishment`
  - motif : « dénomination d'un établissement ; § 3.6 la fait entrer car elle peut être celle d'un professionnel indépendant »

### `glpi` · `items_id` — 11 occurrence(s)

- **Unflagged** (10) — `glpi_agents`, `glpi_itemtranslations_itemtranslations`, `glpi_certificates_items`, `glpi_lockedfields` +6
- **Identity** (1) — `glpi_projectteams`
  - motif : « la table est nommée « projectteams », équipe d'un projet ; la référence polymorphe items_id y désigne les membres de cette équipe, donc des personnes physiques »

### `galette` · `id_adh` — 9 occurrence(s)

- **Identity** (8) — `galette_cotisations`, `galette_groups_managers`, `galette_groups_members`, `galette_pictures` +4
  - motif : « clé étrangère vers `galette_adherents`, table de personnes »
- **Unflagged** (1) — `galette_adherents`

### `paheko-head` · `content` — 7 occurrence(s)

- **Unflagged** (6) — `files_contents`, `files_search`, `searches`, `web_pages` +2
- **PersonalDataUncategorised** (1) — `emails_queue`
  - motif : « contenu d'un message adressé à la personne désignée par `recipient` sur la même ligne ; correspondance dont le contenu est indéterminable depuis le schéma (§ 3.3.1). »

### `dolibarr` · `note` — 5 occurrence(s)

- **PersonalDataUncategorised** (3) — `llx_actioncomm`, `llx_bank`, `llx_societe_remise_supplier`
  - motif : « champ libre rattaché à une personne par les clés `fk_contact` et `fk_soc` de la table ; contenu indéterminable depuis le schéma »
- **Unflagged** (2) — `llx_tva`, `llx_payment_donation`

### `dolibarr` · `name` — 4 occurrence(s)

- **Identity** (2) — `llx_onlinesignature`, `llx_establishment`
  - motif : « dans une table de signatures en ligne, `name` porte le nom du signataire, nécessairement une personne physique »
- **Unflagged** (2) — `llx_c_format_cards`, `llx_printer_receipt_template`

### `dolibarr` · `amount` — 4 occurrence(s)

- **Unflagged** (3) — `llx_tva`, `llx_budget_lines`, `llx_payment_donation`
- **FinancialData** (1) — `llx_bank`
  - motif : « montant d'une opération bancaire, dans une table qui nommé par ailleurs un emetteur »

### `paheko-1.0.0` · `hash` — 4 occurrence(s)

- **Unflagged** (2) — `acc_transactions`, `fichiers_contenu`
- **AuthenticationSecret** (2) — `compromised_passwords_cache`, `membres_sessions`
  - motif : « `hash` dans une table nommée `compromised_passwords_cache` : la colonne porte des empreintes de mots de passe, même si le schéma n'établit aucun rattachement à une personne. »

### `paheko-head` · `hash` — 4 occurrence(s)

- **AuthenticationSecret** (2) — `compromised_passwords_cache`, `users_sessions`
  - motif : « `hash` dans une table nommée `compromised_passwords_cache` : la colonne porte des empreintes de mots de passe, même si le schéma n'établit aucun rattachement à une personne. »
- **Unflagged** (1) — `acc_transactions`
- **ContactDetails** (1) — `emails`
  - motif : « empreinte de l'adresse de courriel dans la table `emails` : la colonne porte une coordonnée pseudonymisée et non un secret vérifié à l'authentification. »

### `dolibarr` · `note_public` — 3 occurrence(s)

- **Unflagged** (2) — `llx_webhook_target`, `llx_product_lot`
- **PersonalDataUncategorised** (1) — `llx_adherent`
  - motif : « Champ libre rattaché à une personne ; contenu indéterminable depuis le schéma. »

### `dolibarr` · `note_private` — 3 occurrence(s)

- **Unflagged** (2) — `llx_webhook_target`, `llx_product_lot`
- **PersonalDataUncategorised** (1) — `llx_adherent`
  - motif : « Champ libre rattaché à une personne ; contenu indéterminable depuis le schéma. »

### `openemr` · `comments` — 3 occurrence(s)

- **PersonalDataUncategorised** (2) — `procedure_specimen`, `log`
  - motif : « champ libre rattaché au prélèvement d'une personne ; aucune qualification explicite dans le nom ni dans un commentaire, contenu indéterminable depuis le schéma (§ 3.3) »
- **HealthData** (1) — `form_eye_mag_wearing`
  - motif : « champ libre d'un formulaire de prescription optique rattaché au patient (`PID`) ; la table qualifie le champ comme commentaire clinique. »

### `openemr` · `encounter` — 3 occurrence(s)

- **HealthData** (2) — `forms`, `billing`
  - motif : « désigne la rencontre clinique (encounter) d'un patient : l'existence d'une consultation est déjà une donnée de santé »
- **Unflagged** (1) — `form_eye_mag_wearing`

### `paheko-head` · `subject` — 3 occurrence(s)

- **Unflagged** (2) — `mailings`, `services_reminders`
- **PersonalDataUncategorised** (1) — `emails_queue`
  - motif : « contenu d'un message adressé à la personne désignée par `recipient` sur la même ligne ; correspondance dont le contenu est indéterminable depuis le schéma (§ 3.3.1). »

### `dolibarr` · `url` — 2 occurrence(s)

- **Unflagged** (1) — `llx_webhook_target`
- **ContactDetails** (1) — `llx_adherent`
  - motif : « Adresse web déclarée par l'adhérent, coordonnée de contact. »

### `galette` · `comment` — 2 occurrence(s)

- **Unflagged** (1) — `galette_documents`
- **PersonalDataUncategorised** (1) — `galette_payments_schedules`
  - motif : « champ libre d'un échéancier rattaché par `id_cotis` à la cotisation d'un adhérent ; contenu indéterminable depuis le schéma »

### `galette` · `amount` — 2 occurrence(s)

- **FinancialData** (1) — `galette_payments_schedules`
  - motif : « montant en `decimal(15,2)` d'un échéancier de paiement rattaché par `id_cotis` à la cotisation d'un adhérent »
- **Unflagged** (1) — `galette_types_cotisation`

### `openemr` · `field_value` — 2 occurrence(s)

- **PersonalDataUncategorised** (1) — `audit_details`
  - motif : « le commentaire dit qu'elle recopie la valeur d'un champ d'une table OpenEMR quelconque, y compris des tables de personnes : le contenu est personnel mais indéterminable depuis le schéma »
- **ProfessionalLife** (1) — `facility_user_ids`
  - motif : « valeur d'un identifiant de l'utilisateur auprès d'un établissement, que le nom de la table `facility_user_ids` qualifie comme professionnel. »

### `openemr` · `title` — 2 occurrence(s)

- **Unflagged** (1) — `background_services`
- **HealthData** (1) — `form_eye_mag_impplan`
  - motif : « la table `form_eye_mag_impplan` est un formulaire d'impression diagnostique et de plan de soins rattaché à un patient (`pid`). »

### `openemr` · `code` — 2 occurrence(s)

- **Unflagged** (1) — `billing`
- **HealthData** (1) — `form_eye_mag_impplan`
  - motif : « la table `form_eye_mag_impplan` est un formulaire d'impression diagnostique et de plan de soins rattaché à un patient (`pid`). »

### `paheko-0.8.0` · `hash` — 2 occurrence(s)

- **Unflagged** (1) — `fichiers_contenu`
- **AuthenticationSecret** (1) — `membres_sessions`
  - motif : « empreinte du jeton de session, à côté du sélecteur : c'est le secret vérifié à l'authentification. »

### `sacoche` · `crcn_niveau_numero` — 2 occurrence(s)

- **Unflagged** (1) — `sacoche_crcn_niveau`
- **PersonalDataUncategorised** (1) — `sacoche_crcn_saisie`
  - motif : « niveau de maîtrise saisi pour un élève identifié : résultat d'évaluation, donnée personnelle qu'aucune autre valeur de la taxonomie ne nomme. »

## Inter-schémas — logiciels différents — 32 nom(s) de colonne

### `id` — 173 occurrence(s)

- **Unflagged** (171) — `llx_actioncomm`, `llx_c_propalst`, `galette_tmplinks`, `glpi_devicefirmwaretypes` +167
- **Identity** (2) — `fichiers_membres`, `fichiers_membres`
  - motif : « malgré son nom, la colonne porte une clé étrangère déclarée vers `membres` ; § 3.2 (ce qu'elle désigne) prime sur § 3.1 (clé primaire). »

### `name` — 42 occurrence(s)

- **Unflagged** (40) — `llx_c_format_cards`, `llx_printer_receipt_template`, `galette_searches`, `glpi_devicefirmwaretypes` +36
- **Identity** (2) — `llx_onlinesignature`, `llx_establishment`
  - motif : « dans une table de signatures en ligne, `name` porte le nom du signataire, nécessairement une personne physique »

### `label` — 34 occurrence(s)

- **Unflagged** (31) — `llx_webhook_target`, `llx_tva`, `llx_accounting_transaction_template`, `llx_c_product_nature` +27
- **PersonalDataUncategorised** (2) — `llx_actioncomm`, `llx_bank`
  - motif : « intitulé libre d'un événement rattaché par clé étrangère à un contact ou à un utilisateur ; contenu indéterminable depuis le schéma »
- **Identity** (1) — `llx_establishment`
  - motif : « dénomination d'un établissement ; § 3.6 la fait entrer car elle peut être celle d'un professionnel indépendant »

### `comment` — 26 occurrence(s)

- **Unflagged** (25) — `galette_documents`, `glpi_devicefirmwaretypes`, `glpi_networkequipmentmodels`, `glpi_solutiontypes` +21
- **PersonalDataUncategorised** (1) — `galette_payments_schedules`
  - motif : « champ libre d'un échéancier rattaché par `id_cotis` à la cotisation d'un adhérent ; contenu indéterminable depuis le schéma »

### `code` — 21 occurrence(s)

- **Unflagged** (20) — `llx_actioncomm`, `llx_c_format_cards`, `llx_accounting_transaction_template`, `llx_c_type_contact` +16
- **HealthData** (1) — `form_eye_mag_impplan`
  - motif : « la table `form_eye_mag_impplan` est un formulaire d'impression diagnostique et de plan de soins rattaché à un patient (`pid`). »

### `user_id` — 19 occurrence(s)

- **Identity** (18) — `api_refresh_token`, `api_log`, `login_mfa_registrations`, `ccda_table_mapping` +14
  - motif : « désigne l'utilisateur porteur du jeton (§ 3.2) »
- **Unflagged** (1) — `sacoche_user`

### `hash` — 12 occurrence(s)

- **AuthenticationSecret** (6) — `galette_tmplinks`, `membres_sessions`, `compromised_passwords_cache`, `membres_sessions` +2
  - motif : « `hash` d'un lien temporaire : jeton dont la détention vaut preuve d'accès »
- **Unflagged** (5) — `glpi_itemtranslations_itemtranslations`, `fichiers_contenu`, `acc_transactions`, `fichiers_contenu` +1
- **ContactDetails** (1) — `emails`
  - motif : « empreinte de l'adresse de courriel dans la table `emails` : la colonne porte une coordonnée pseudonymisée et non un secret vérifié à l'authentification. »

### `items_id` — 11 occurrence(s)

- **Unflagged** (10) — `glpi_agents`, `glpi_itemtranslations_itemtranslations`, `glpi_certificates_items`, `glpi_lockedfields` +6
- **Identity** (1) — `glpi_projectteams`
  - motif : « la table est nommée « projectteams », équipe d'un projet ; la référence polymorphe items_id y désigne les membres de cette équipe, donc des personnes physiques »

### `amount` — 10 occurrence(s)

- **Unflagged** (7) — `llx_tva`, `llx_budget_lines`, `llx_payment_donation`, `galette_types_cotisation` +3
- **FinancialData** (3) — `llx_bank`, `galette_payments_schedules`, `benefit_eligibility`
  - motif : « montant d'une opération bancaire, dans une table qui nommé par ailleurs un emetteur »

### `nom` — 10 occurrence(s)

- **Unflagged** (9) — `llx_c_regions`, `llx_document_model`, `compta_moyens_paiement`, `fichiers` +5
- **Identity** (1) — `clients`
  - motif : « Nom de famille de la personne dans la table `clients` : élément d'état civil. »

### `content` — 9 occurrence(s)

- **Unflagged** (8) — `llx_website_page`, `llx_c_subtotals_texts`, `files_contents`, `files_search` +4
- **PersonalDataUncategorised** (1) — `emails_queue`
  - motif : « contenu d'un message adressé à la personne désignée par `recipient` sur la même ligne ; correspondance dont le contenu est indéterminable depuis le schéma (§ 3.3.1). »

### `id_adh` — 9 occurrence(s)

- **Identity** (8) — `galette_cotisations`, `galette_groups_managers`, `galette_groups_members`, `galette_pictures` +4
  - motif : « clé étrangère vers `galette_adherents`, table de personnes »
- **Unflagged** (1) — `galette_adherents`

### `user` — 8 occurrence(s)

- **Identity** (6) — `forms`, `form_group_attendance`, `form_eye_base`, `billing` +2
  - motif : « désigne l'utilisateur auteur de la saisie, personne physique (§ 3.1) »
- **Unflagged** (2) — `acc_accounts`, `acc_accounts`

### `title` — 7 occurrence(s)

- **Unflagged** (6) — `llx_website_page`, `llx_inventory`, `llx_c_price_expression`, `background_services` +2
- **HealthData** (1) — `form_eye_mag_impplan`
  - motif : « la table `form_eye_mag_impplan` est un formulaire d'impression diagnostique et de plan de soins rattaché à un patient (`pid`). »

### `note` — 6 occurrence(s)

- **PersonalDataUncategorised** (4) — `llx_actioncomm`, `llx_bank`, `llx_societe_remise_supplier`, `patient_care_experience_preferences`
  - motif : « champ libre rattaché à une personne par les clés `fk_contact` et `fk_soc` de la table ; contenu indéterminable depuis le schéma »
- **Unflagged** (2) — `llx_tva`, `llx_payment_donation`

### `url` — 5 occurrence(s)

- **Unflagged** (3) — `llx_webhook_target`, `plugins`, `plugins`
- **ContactDetails** (2) — `llx_adherent`, `galette_socials`
  - motif : « Adresse web déclarée par l'adhérent, coordonnée de contact. »

### `subject` — 5 occurrence(s)

- **Unflagged** (3) — `services_reminders`, `mailings`, `services_reminders`
- **PersonalDataUncategorised** (2) — `glpi_notimportedemails`, `emails_queue`
  - motif : « champ libre `subject` d'un message dont l'expéditeur et le destinataire figurent sur la même ligne ; contenu indéterminable depuis le schéma. »

### `country` — 4 occurrence(s)

- **ContactDetails** (2) — `llx_adherent`, `addresses`
  - motif : « Pays du bloc d'adresse postale, en position immédiate après `state_id`. »
- **Unflagged** (2) — `acc_charts`, `acc_charts`

### `body` — 4 occurrence(s)

- **Unflagged** (3) — `services_reminders`, `mailings`, `services_reminders`
- **PersonalDataUncategorised** (1) — `onotes`
  - motif : « corps d'une note libre rattachée à l'utilisateur nommé par `user` ; contenu indéterminable depuis le schéma (§ 3.3-1). »

### `note_public` — 3 occurrence(s)

- **Unflagged** (2) — `llx_webhook_target`, `llx_product_lot`
- **PersonalDataUncategorised** (1) — `llx_adherent`
  - motif : « Champ libre rattaché à une personne ; contenu indéterminable depuis le schéma. »

### `note_private` — 3 occurrence(s)

- **Unflagged** (2) — `llx_webhook_target`, `llx_product_lot`
- **PersonalDataUncategorised** (1) — `llx_adherent`
  - motif : « Champ libre rattaché à une personne ; contenu indéterminable depuis le schéma. »

### `source` — 3 occurrence(s)

- **Unflagged** (2) — `llx_c_type_contact`, `newsletter`
- **Identity** (1) — `procedure_report`
  - motif : « le commentaire dit `references users.id, who entered this data` : désigne l'utilisateur auteur de la saisie (§ 3.1). »

### `comments` — 3 occurrence(s)

- **PersonalDataUncategorised** (2) — `procedure_specimen`, `log`
  - motif : « champ libre rattaché au prélèvement d'une personne ; aucune qualification explicite dans le nom ni dans un commentaire, contenu indéterminable depuis le schéma (§ 3.3) »
- **HealthData** (1) — `form_eye_mag_wearing`
  - motif : « champ libre d'un formulaire de prescription optique rattaché au patient (`PID`) ; la table qualifie le champ comme commentaire clinique. »

### `client_id` — 3 occurrence(s)

- **Identity** (2) — `adresses`, `commandes`
  - motif : « Clé étrangère déclarée vers `clients`, table de personnes : la colonne désigne le client, elle ne stocke qu'un entier. »
- **Unflagged** (1) — `api_refresh_token`

### `encounter` — 3 occurrence(s)

- **HealthData** (2) — `forms`, `billing`
  - motif : « désigne la rencontre clinique (encounter) d'un patient : l'existence d'une consultation est déjà une donnée de santé »
- **Unflagged** (1) — `form_eye_mag_wearing`

### `notes` — 3 occurrence(s)

- **Unflagged** (2) — `acc_transactions`, `acc_transactions`
- **PersonalDataUncategorised** (1) — `person_patient_link`
  - motif : « champ libre rattaché à une personne (la table lie une personne à un patient) ; contenu indéterminable depuis le schéma (§ 3.3-1). »

### `prefix` — 3 occurrence(s)

- **AuthenticationSecret** (2) — `compromised_passwords_cache_ranges`, `compromised_passwords_cache_ranges`
  - motif : « préfixe d'empreinte de mot de passe dans le cache de mots de passe compromis ; même lecture que la colonne `hash` de la table jumelle. »
- **ContactDetails** (1) — `phone_numbers`
  - motif : « préfixe, composante d'un numéro de téléphone dans la table `phone_numbers`. »

### `percent` — 2 occurrence(s)

- **Unflagged** (1) — `llx_actioncomm`
- **FinancialData** (1) — `benefit_eligibility`
  - motif : « la table `benefit_eligibility` décrit les droits et la couverture d'assurance d'un assuré ; la colonne porte un élément de sa prise en charge financière. »

### `banque` — 2 occurrence(s)

- **FinancialData** (1) — `llx_bank`
  - motif : « nom de l'établissement bancaire associé à l'opération d'un emetteur, donnée bancaire »
- **Unflagged** (1) — `compta_comptes_bancaires`

### `state` — 2 occurrence(s)

- **Unflagged** (1) — `glpi_reminders`
- **ContactDetails** (1) — `addresses`
  - motif : « état ou région d'une adresse postale ; LocationData est écarté car il vise la géolocalisation, pas l'adresse déclarée (conventions communes) »

### `field_value` — 2 occurrence(s)

- **PersonalDataUncategorised** (1) — `audit_details`
  - motif : « le commentaire dit qu'elle recopie la valeur d'un champ d'une table OpenEMR quelconque, y compris des tables de personnes : le contenu est personnel mais indéterminable depuis le schéma »
- **ProfessionalLife** (1) — `facility_user_ids`
  - motif : « valeur d'un identifiant de l'utilisateur auprès d'un établissement, que le nom de la table `facility_user_ids` qualifie comme professionnel. »

### `crcn_niveau_numero` — 2 occurrence(s)

- **Unflagged** (1) — `sacoche_crcn_niveau`
- **PersonalDataUncategorised** (1) — `sacoche_crcn_saisie`
  - motif : « niveau de maîtrise saisi pour un élève identifié : résultat d'évaluation, donnée personnelle qu'aucune autre valeur de la taxonomie ne nomme. »

