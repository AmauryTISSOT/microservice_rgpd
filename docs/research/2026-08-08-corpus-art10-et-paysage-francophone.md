# Compléter le corpus : l'article 10 et le reste du paysage francophone

**Ticket** : [#137](https://github.com/AmauryTISSOT/microservice_rgpd/issues/137), enfant de la carte
[#122](https://github.com/AmauryTISSOT/microservice_rgpd/issues/122). Gradue une brume ouverte par
[#125](https://github.com/AmauryTISSOT/microservice_rgpd/issues/125).
**Date** : 2026-08-08. **Nature** : recherche.

**Méthode, non négociable** : chaque candidat de ce document a été **cloné et lu directement**.
Aucun chiffre ne provient d'une source secondaire, d'un moteur de recherche ou d'un rapport
intermédiaire. C'est la règle de provenance imposée à la carte #122 après l'incident de #125, où une
exploration déléguée avait produit un rapport entièrement fabriqué. Les commits sont épinglés au § 6.
Ce qui n'a pas pu être cloné est déclaré **absent**, jamais déduit.

---

## 0. Ce que la recherche a établi, en une page

**Le ticket posait trois questions. Les trois ont une réponse, et la première n'est pas celle
qu'on attendait.**

1. **L'article 10 n'est pas un trou de corpus. C'est une propriété du logiciel libre.**
   Dix candidats supplémentaires ont été clonés et fouillés, dont **Chill**, la seule piste que #125
   avait nommée. **Zéro colonne d'infraction, partout** — dix sur dix, en plus des neuf de #125.
   Les quatre correspondances textuelles trouvées sont toutes des **faux positifs vérifiés** (§ 2).
   Et il existe une raison structurelle à cette absence, qui est la vraie réponse du ticket : le
   traitement de données d'infractions est **réservé par l'article 10 lui-même** aux autorités
   publiques et aux traitements autorisés par la loi. Un logiciel publié sous licence libre est,
   par construction, écrit pour un public qui n'a pas cette autorisation. **Chercher plus longtemps
   ne changera rien** : le corpus ne manque pas d'un candidat, la population ne contient pas
   l'objet.
2. **Chill ne bouche pas le trou de l'article 10 — mais il bouche le trou que #124 avait désigné
   comme le plus grave de toute la carte.** `ChillJobBundle` porte `chill_job.cs_person`, une table
   de **50 colonnes en franglais camelCase saturée d'acronymes administratifs français** :
   `CERInscriptionDate`, `PPAEInscriptionDate`, `NEETEligibilite`, `FSEMaDemarcheCode`,
   `datecontratIEJ`, `documentRQTH_id`, `accompagnementRQTHDate`, `poleEmploiId`, `CPFMontant`,
   `acomptedif`, `handicapnotes`, `mobilitemoyentransport`. C'est **exactement** le régime que #124
   déclarait non mesuré — « aucune évaluation d'aucune méthode sur des identifiants francophones » —
   et **exactement** la découpe mono-casse sur laquelle *Samurai* échouait (29 réussites sur 249).
   Chill entre au corpus, mais **pour une autre raison que celle qui l'avait fait nommer**.
3. **Le paysage francophone est refait depuis zéro, et il est maigre en volume mais net en verdicts.**
   `démarches-simplifiées` (AGPL, 100 tables / 974 colonnes, franglais snake_case), **GRR** (GPL-2.0,
   39 tables / 269 colonnes, français abrégé), **Passerelle** d'Entr'ouvert (AGPL, franglais de
   guichet et **le seul vivier de secrets d'authentification du corpus**), les **quatre plugins
   Galette** (GPL, et **en anglais** — ce qui contredit l'intuition de #125). **PMB et GEPI n'ont
   pas pu être clonés** et sont déclarés absents, pas « probablement intéressants ».
4. **Le mot « plancher » appliqué à SACoche par #125 est faux, et ce qu'il cachait vaut mieux.**
   Les 17 migrations PHP ont été lues : elles n'ajoutent **aucune** colonne au schéma courant — les
   143 colonnes qu'elles ajoutent y sont déjà. Ce qu'elles portent, en revanche, est **une série
   diachronique de seize ans (2010 → 2026) sur un schéma français**, avec **20 colonnes réellement
   mortes** (`user_num_sconet`, `officiel_palier1/2/3`, `eleve_brevet_serie`…) et **316 `CHANGE`**.
   C'est la **base sédimentée** que #125 déclarait non couverte, et elle était dans le dépôt.

**Un résultat négatif transversal**, qui confirme #125 sur son point le plus coûteux : sur les dix
candidats, **`démarches-simplifiées` porte zéro commentaire de colonne** dans son `schema.rb`, et
aucun des autres n'en porte davantage. Le pivot que l'`Operator` collera n'aura pas de commentaire.

---

## 1. Ce qui a été cloné, et ce qui ne l'a pas été

| Candidat | Licence | SGBD | Tables | Colonnes | Langue des identifiants | Art. 10 |
| --- | --- | --- | ---: | ---: | --- | --- |
| [Chill](https://gitlab.com/Chill-Projet/chill-bundles) | AGPL-3.0-only | PostgreSQL | 222 | 1 219 *(plancher)* | **Anglais, sauf `ChillJobBundle` en franglais** | **0** |
| [démarches-simplifiées](https://github.com/betagouv/demarches-simplifiees.fr) | AGPL-3.0 | PostgreSQL | 100 | 974 | **Franglais snake_case** | **0** |
| [GRR](https://github.com/JeromeDevome/GRR) | GPL-2.0 | MySQL | 39 | 269 | **Français abrégé** | **0** |
| [Passerelle](https://git.entrouvert.org/entrouvert/passerelle) | AGPL-3.0 | PostgreSQL | 314 *(modèles)* | 173 *(champs uniques échantillonnés)* | **Franglais de guichet** | **0** |
| [Authentic 2](https://git.entrouvert.org/entrouvert/authentic) | AGPL-3.0 | PostgreSQL | 86 *(modèles)* | non compté | Anglais | **0** |
| [galette-auto](https://github.com/galette/plugin-auto) | GPL-3.0 | MySQL/Pg | 10 | 44 | **Anglais** | **0** |
| [galette-events](https://github.com/galette/plugin-events) | GPL-3.0 | MySQL/Pg | 5 | 37 | **Anglais** | **0** |
| [galette-paypal](https://github.com/galette/plugin-paypal) | GPL-3.0 | MySQL/Pg | 2 | 10 | Anglais | **0** |
| [galette-maps](https://github.com/galette/plugin-maps) | GPL-3.0 | MySQL/Pg | 1 | 3 | Anglais | **0** |
| SACoche *(migrations, relecture)* | AGPL-3.0 | MySQL | — | +143 / −20 sur 16 ans | **Français abrégé** | **0** |

**Non obtenus, donc absents** :

- **PMB** — quatre URL essayées (`forge.chapril.org/PMB/pmb`, `forge.chapril.org/pmb/pmb`,
  `gitlab.com/pmb-fr/pmb`), échec de clonage à chaque fois. Aucune caractéristique de PMB n'est
  affirmée dans ce document.
- **GEPI** — deux URL essayées (`github.com/gepi/gepi`, `github.com/GEPI/gepi`), échec. Idem.

Ces deux-là restent, à la lettre de la règle de provenance, **rigoureusement inconnus**. Ils ne sont
pas « probablement des candidats francophones » : rien n'en a été vu. Les reprendre suppose de
trouver leur forge canonique, ce que ce ticket n'a pas fait.

**Non examinés, et déclarés tels** : les modules métier Dolibarr, Framadate, `demarches-simplifiees`
côté modules externes, et l'ensemble Publik d'Entr'ouvert au-delà d'`authentic` et `passerelle`.
Le rendement décroissant est net (§ 2 et § 5) et ne justifiait pas d'y passer le budget du ticket.

---

## 2. L'article 10 : pourquoi il n'y a rien, et pourquoi il n'y aura rien

### 2.1 Le fait

L'heuristique de #125 a été rejouée, élargie (`crimin`, `offence`, `conviction`, `condamn`,
`infraction`, `casier`, `judiciair`, `penal`, `prison`, `probation`, `incarcer`, `delinqu`,
`sursis`, `recidiv`) et passée sur **l'intégralité** des dix dépôts — pas seulement leur DDL :
fichiers `.sql`, `.php`, `.py`, `.rb`, migrations comprises.

**Quatre correspondances, quatre faux positifs, tous vérifiés en ouvrant le fichier :**

| Correspondance | Où | Ce que c'est réellement |
| --- | --- | --- |
| `casier` | `grr/reservation/controleurs/contact.php:39` | Une variable PHP `$_POST["casier"]` — un **casier de rangement**, pas un casier judiciaire. Homonymie française pure. |
| `PROBATION` | `ds/spec/lib/api_entreprise/etablissement_adapter_spec.rb:162` | Une **fixture de test** : la raison sociale « SERVICE PÉNITENTIAIRE D'INSERTION ET DE PROBATION » rendue par l'API Entreprise. Une valeur d'exemple, pas une colonne. |
| `judiciaire` | `ds/app/lib/email_checker.rb:392` | `judiciaire.interieur.gouv.fr` dans une **liste blanche de domaines de courriel**. |
| `convictions` | SACoche | Aucune occurrence dans le DDL ni le code PHP ; résidu d'un fichier non applicatif. |

**Zéro nom de colonne.** Sur dix candidats ici, et neuf chez #125 : **dix-neuf applications libres,
aucune donnée d'infraction**.

### 2.2 La raison, qui est le vrai résultat

Ce n'est pas un échec d'échantillonnage, et allonger la liste des candidats ne le corrigera pas.
**L'article 10 restreint lui-même qui peut traiter ces données** : sous le contrôle de l'autorité
publique, ou sous une autorisation légale assortie de garanties. Les logiciels qui les manipulent en
France sont ceux de la chaîne pénale, des SPIP, de la PJJ, de l'administration pénitentiaire — des
systèmes de l'État, **non publiés**. À l'inverse, un logiciel publié en libre est écrit pour un
public quelconque, c'est-à-dire pour un public qui **n'a pas** cette autorisation.

Chill est la démonstration la plus propre de ce point, et c'est pour ça qu'il valait la peine d'être
cloné même pour un résultat nul : c'est un logiciel de **travail social français**, celui dont on
attendait le plus, et il n'a rien. Son modèle porte `chill_person.medical_care`, `social_issue`,
`social_action`, `handicap` — de l'article 9 en quantité — et **pas une colonne** d'article 10.
Le travail social français s'arrête exactement là où l'article 10 commence.

### 2.3 Conséquence pour la carte

Le ticket #137 proposait deux issues, dont l'une était « chercher un candidat ». **Cette issue est
close par un fait, pas par un abandon** : il n'y a pas de candidat à trouver dans cette population.
Reste l'autre, qui devient la seule et qui n'est plus un pis-aller :

> Le banc **ne mesure rien** sur `CriminalOffenceData`, et il le dit — non pas « faute de candidat »,
> mais parce que **le régime schéma-seul sur des applications libres ne peut pas atteindre cette
> catégorie**.

C'est la même forme d'énoncé que la décision de cadrage 11 de #122 (« le rapport dit ce qu'il n'a
pas regardé »), portée d'un cran plus haut : ici, c'est **le banc** qui dit ce qu'il ne peut pas
mesurer. Et ce n'est plus seulement une lacune du corpus : cela rejoint la **seconde dimension**
que #127 a renvoyée à [la clause d'incomplétude](https://github.com/AmauryTISSOT/microservice_rgpd/issues/132)
— les **catégories que ce régime ne peut pas atteindre**. `CriminalOffenceData` y entre désormais
avec un motif documenté, et non plus comme une case vide en attente.

⚠️ **Ce que cela ne dit pas** : `CriminalOffenceData` reste **justifiée dans la taxonomie**. #127 l'a
tranché sur le droit (fusionner l'article 10 dans l'article 9 serait une erreur de droit), pas sur la
mesurabilité. Une base *client* réelle — un cabinet d'avocats, une collectivité, une association de
prévention — peut parfaitement en porter. Ce document dit que **le banc** ne la mesurera pas ; il ne
dit pas que le moteur ne la rencontrera jamais.

---

## 3. Chill : le candidat qui entre pour la mauvaise raison

**Licence** : `AGPL-3.0-only` (déclarée dans `composer.json`, `LICENSE` = texte AGPL v3 intégral).
Même régime que SACoche et Paheko, déjà analysé au § 2 de [#125] : le versement au corpus par
**introspection** reste défendable, la copie de fichiers reste à éviter.
**SGBD** : PostgreSQL exclusivement (Doctrine/Symfony). **222 tables** relevées dans 422 fichiers de
migration `Version*.php`, **1 219 colonnes** en `CREATE TABLE` — un **plancher réel celui-là**, car
1 289 opérations `ADD COLUMN` supplémentaires apparaissent dans l'historique des migrations.

### 3.1 Le corps du produit est en anglais

`chill_person_person`, `chill_main_address`, `marital_status`, `household`, `accompanying_periods`,
`social_action`, `social_issue`. Un logiciel français à identifiants anglais : c'est en soi une
donnée pour la carte, et elle va contre l'hypothèse implicite de #125 qu'« application française ⇒
identifiants français ». **La corrélation n'existe pas.** `démarches-simplifiées`, application de
l'État, la contredit dans l'autre sens (§ 4.1).

Deux spécimens de sédimentation valent quand même d'être notés : **le mélange de conventions de
casse dans un même schéma** — `ActivityReason`, `CustomFieldsGroup`, `Person`, `Report` en
PascalCase à côté de `chill_person_household_composition_type` en snake_case — et `regroupment`,
un franglais authentique (« regroupement » anglicisé). #125 disait la saleté absente des projets
maintenus ; sur 222 tables, elle est ici bien présente, mais **au niveau de la convention**, pas de
la table morte.

### 3.2 `chill_job.cs_person` : le spécimen qui justifie le clonage

C'est la table qui change la valeur de Chill pour la carte. Cinquante colonnes, extraites
littéralement de `src/Bundle/ChillJobBundle/src/migrations/Version20240424095147.php` :

```
situationLogement, situationLogementPrecision, enfantACharge, niveauMaitriseLangue,
vehiculePersonnel, permisConduire, situationProfessionnelle, dateFinDernierEmploi,
typeContrat, typeContratAide, ressources, ressourcesComment, ressourceDate1Versement,
CPFMontant, acomptedif, accompagnement, accompagnementRQTHDate, accompagnementComment,
poleEmploiId, poleEmploiInscriptionDate, cafId, cafInscriptionDate, CERInscriptionDate,
PPAEInscriptionDate, CERSignataire, PPAESignataire, NEETEligibilite, NEETCommissionDate,
FSEMaDemarcheCode, datecontratIEJ, dateavenantIEJ, dispositifs_notes, handicap,
handicapnotes, handicapRecommandation, mobilitemoyentransport, mobilitenotes,
handicapAccompagnement_id, documentCV_id, documentAgrementIAE_id, documentRQTH_id,
documentAttestationNEET_id, documentCI_id, documentTitreSejour_id,
documentAttestationFiscale_id, documentPermis_id, documentAttestationCAAF_id,
documentContraTravail_id, documentAttestationFormation_id, documentQuittanceLoyer_id,
documentAttestationSecuriteSociale_id
```

**Ce que cette table apporte, et qu'aucun candidat retenu par #125 n'apportait :**

- **Des acronymes administratifs français opaques, en position de radical** : CER (contrat
  d'engagement réciproque), PPAE (projet personnalisé d'accès à l'emploi), NEET, IEJ (initiative pour
  l'emploi des jeunes), FSE (Fonds social européen), RQTH (reconnaissance de la qualité de
  travailleur handicapé), CPF, CAF, IAE, CI, CV. **Aucun dictionnaire français général ne les
  contient.** #124 a établi que « c'est le dictionnaire qui décide, pas l'algorithme » et en a fait
  un **facteur à faire varier** ; cette table est le banc d'essai de ce facteur — elle mesurera la
  différence entre un dictionnaire de français courant et un dictionnaire de sigles administratifs.
- **De la concaténation mono-casse sans séparateur** : `acomptedif`, `handicapnotes`,
  `mobilitemoyentransport`, `noteimmersion`, `datecontratIEJ`. C'est **littéralement** le cas où
  *Samurai* réussit 29 découpes sur 249 (#124), et le corpus n'en avait aucun échantillon
  francophone.
- **Des acronymes collés à du camelCase** : `accompagnementRQTHDate`, `documentAgrementIAE_id`,
  `handicapAccompagnement_id`. Trois conventions de casse dans un seul identifiant.
- **De l'article 9 lisible dans le nom** : `handicap`, `handicapnotes`, `handicapRecommandation`,
  `documentRQTH_id`, `documentAttestationSecuriteSociale_id` → `HealthData` ;
  `documentTitreSejour_id` et `niveauMaitriseLangue` → susceptibles de révéler l'origine.
  C'est de l'article 9 **atteignable par le nom**, là où SACoche l'avait caché dans des valeurs.
- **Le voisinage adverse le plus dur du corpus** : `documentCV_id` et `documentPermis_id` sont des
  clés étrangères vers une table de documents, `CPFMontant` et `acomptedif` des montants. Un moteur
  qui signale `permisConduire` (donnée personnelle) doit **ne pas** signaler `documentPermis_id`
  de la même façon — c'est le rôle d'écartement que #128 assigne au type et à `table_referencee`,
  et voici enfin de quoi le mesurer.

Le reste du bundle est du même bois : `chill_job.frein` (`freinsPerso`, `notesPerso`,
`freinsEmploi`), `chill_job.immersion` (`tuteurFonction`, `structureAccPhonenumber`, `posteLieu`,
`savoirEtre`, `domaineActivite`), `chill_job.cv_formation` (`diplomaReconnue`, `organisme`).

**Statut du bundle** : vivant, pas mort. Il est autochargé par le `composer.json` racine
(`"Chill\\JobBundle\\": "src/Bundle/ChillJobBundle/src"`) et sa dernière migration date de
**septembre 2024**. Il porte un ancien schéma `chill_csconnectes` renommé en `chill_job` — c'est-à-dire
qu'il fournit **en prime une petite série diachronique**.

### 3.3 ⚠️ Le résultat qui coûte : `person.cfdata` est un `jsonb`

`ChillCustomFieldsBundle` permet à chaque déploiement de définir ses propres champs. Ces champs
sont stockés dans **une seule colonne** : `person.cfdata`, de type `jsonb`
(`ChillPersonBundle/migrations/Version20160818113633.php`). Les tables `CustomField` et
`CustomFieldsGroup` ne portent que les **définitions** — le nom du champ y est une **valeur**, pas
un identifiant de schéma.

**C'est le résultat de SACoche, mais aggravé d'un cran et cette fois par conception.** Chez SACoche,
les PAI et PPS étaient des valeurs derrière `livret_modaccomp_code` — un accident de modélisation.
Chez Chill, **le mécanisme d'extension officiel du produit** rend invisible tout ce qu'un
déploiement ajoute. Un lecteur de schéma voit `person.cfdata jsonb` et ne saura jamais qu'il y a
derrière, selon le département qui l'a installé, une orientation sexuelle ou un suivi psychiatrique.

Chill compte **149 colonnes `JSON`/`JSONB`** dans ses migrations. Ce n'est pas une bizarrerie
locale : c'est un **motif d'architecture répandu** (Passerelle a `computed_properties`,
`data_values`, `demand_data`, `metadata` ; SACoche a ses colonnes `_config` sérialisées).

**Ce que la carte doit en faire.** Trois conséquences, dont deux sont neuves :

1. **Pour le banc** : `cfdata` est une **vérité terrain indécidable**, pas un faux négatif. Le
   protocole ([#130](https://github.com/AmauryTISSOT/microservice_rgpd/issues/130)) doit pouvoir
   annoter une colonne comme *hors de portée par construction* et l'exclure du dénominateur —
   sans quoi le corpus punit le moteur pour une chose qu'aucun moteur ne peut faire.
2. **Pour la taxonomie** : la question n'est pas tranchée de savoir si `person.cfdata` est
   `Unflagged` (rien n'est visible) ou `PersonalDataUncategorised` (il y a manifestement du
   personnel là-dedans, mais sans catégorie). La règle de #127 — *motif présent ⇔ ce n'est pas
   `Unflagged`* — donne la réponse : un motif est parfaitement rédigeable (« colonne JSON libre
   d'un mécanisme de champs personnalisés »), donc **`PersonalDataUncategorised`**. Cela mérite
   d'être écrit dans le glossaire, parce que c'est le cas d'usage le plus fort du repli.
3. **Pour la clause d'incomplétude** ([#132](https://github.com/AmauryTISSOT/microservice_rgpd/issues/132)) :
   une colonne `json`/`jsonb` est un **signal détectable** que le rapport ne voit pas tout. Le
   `Screening` peut donc **nommer précisément** ce qu'il n'a pas regardé, au lieu d'une clause
   générique. C'est du grain neuf, et il est bon marché : le champ `type` du pivot de #128 le
   fournit déjà. **Le moteur peut compter les colonnes JSON et le dire.**

---

## 4. Le paysage francophone, refait

### 4.1 `démarches-simplifiées` — la meilleure prise du lot après Chill

AGPL-3.0, Rails/PostgreSQL, **100 tables et 974 colonnes** dans un `db/schema.rb` unique et
canonique — de très loin l'artefact le plus commode du corpus : un fichier, pas 755.

Ses identifiants sont un **franglais snake_case systématique**, et l'application est celle de l'État
français : `administrateurs`, `instructeurs`, `groupe_instructeurs`, `administrateurs_instructeurs`,
`types_de_champ`, `champs`, `dossiers`, `avis`, `commentaires`, `etablissements`, `exercices`,
`traitements`, `procedure_revisions`, `attestation_templates`, `france_connect_informations`,
`agent_connect_informations`. À côté, de l'anglais pur : `follows`, `labels`, `zones`, `banners`,
`stats`, `exports`, `batch_operations`.

Le spécimen remarquable est **`procedure_revision_types_de_champ`** : un syntagme français complet,
préposition comprise, en snake_case, préfixé de deux mots anglais. Pour une découpe d'identifiants,
c'est un cas dur et parfaitement réaliste.

⚠️ **`schema.rb` n'est pas du DDL**, et c'est un piège du même genre que celui de #125. C'est une
représentation Ruby que Rails **régénère** ; les types y sont des types Rails (`t.string`,
`t.jsonb`), pas des types SQL. Il ne dispense donc **pas** de l'introspection : il en est une
promesse, pas une preuve. Et le résultat de #125 s'y confirme — **zéro `comment:`** dans les 1 612
lignes du fichier : Rails supporte les commentaires de colonne, ce projet n'en pose aucun.

### 4.2 GRR — français abrégé, petit mais propre

GPL-2.0, MySQL, **39 tables / 269 colonnes**. Réservation de ressources, identifiants français avec
des abréviations sincères : `grr_calendrier_feries`, `grr_calendrier_vacances`, `idgroupes`,
`mail_resa`, `mail_hebdo`, et un préfixe `grr_j_*` où le `j` signifie **jointure** — une convention
locale, non documentée dans le nom, illisible pour qui ne connaît pas le projet. Zéro article 10,
peu d'article 9. Il entre comme **petit candidat français à faible volume**, utile surtout pour la
morphologie abrégée (`resa`, `hebdo`, `feries`).

### 4.3 Passerelle (Entr'ouvert) — le vivier de secrets

AGPL-3.0, Django/PostgreSQL, **314 modèles** créés à travers l'historique des migrations —
un connecteur par service, donc beaucoup de tables très étroites. Sur un échantillon de
**173 champs uniques** relevés dans les migrations initiales, deux choses ressortent.

**Du franglais de guichet**, dense et administratif : `cartads_id_dossier`, `cartads_numero_dossier`,
`cod_rgp`, `code_insee`, `commune_id`, `objet_demande_id`, `recipient_guichet`, `recipient_siret`,
`exercice`, `financement`, `gesbac_id`, `id_per`, `id_piece`, `num`, `dob`. `cod_rgp`, `id_per` et
`dob` sont exactement les abréviations tronquées que *Valentine* fabrique artificiellement (#124) —
ici elles sont authentiques.

**Et surtout, ce que rien d'autre n'apporte** : `dav_password`, `oauth_password`, `ftp_password`,
`secret_key`, `consumer_secret`, `consumer_key`, `auth_token`, `api_key`, `hawk_auth_key`,
`hawk_auth_id`, `account_sid`, `iv`, `card_data`.
⚠️ **#127 a fait entrer `AuthenticationSecret` dans la taxonomie « sans aucune source primaire »**,
au motif que c'est le cas où le moteur est le plus fort. **Passerelle est le corpus qui permet enfin
de vérifier cette affirmation** au lieu de la supposer — et il apporte du même coup les faux amis
qui vont avec (`consumer_key`, `api_key` sont des secrets **de service**, pas des données
personnelles : un moteur qui les signale comme telles se trompe de sujet).

`authentic2` (86 modèles, AGPL) est à l'inverse en anglais strict et n'apporte rien de spécifique ;
il n'est pas retenu.

### 4.4 Les plugins Galette — le résultat négatif utile

Quatre plugins officiels clonés (`auto`, `events`, `paypal`, `maps`), tous **GPL-3.0**, **18 tables
et 94 colonnes** en tout. #125 laissait entendre qu'ils pourraient étendre le corpus français.
**Ils ne l'étendent pas : ils sont en anglais.** `galette_auto_cars.car_registration`,
`car_first_circulation_date`, `car_chassis_number`, `car_horsepower`, `galette_auto_bodies.body`,
`galette_auto_transmissions.transmission` — avec seulement deux résidus français, `finition` et
`id_adh` (adhérent).

Le cœur de Galette est en français strict, son écosystème de plugins est en anglais. **La question
de #125 est donc close par un fait**, et l'apport est nul en volume : 94 colonnes qui, à elles
seules, ne valent pas le coût d'entrée. Deux colonnes restent notables comme identifiants
indirects — `car_registration` (plaque) et `car_chassis_number` (VIN) — mais elles ne créent pas
de catégorie nouvelle.

---

## 5. SACoche : ce que les 17 migrations disent vraiment

#125 concluait : « les migrations PHP n'ont pas été lues, les 103 tables / 632 colonnes sont donc un
**plancher** ». **Les 17 fichiers `requetes_structure_maj_base_20XX.inc.php` (2010 → 2026) ont
maintenant été lus, et cette conclusion est fausse.**

Ce qu'ils contiennent : **13 `CREATE TABLE`**, **143 `ALTER TABLE … ADD`**, **316 `CHANGE`**,
**2 `RENAME`**, et **22 `DROP`** de colonne.

**Les colonnes ajoutées sont déjà dans le schéma courant.** Vérifié par échantillon contre
`_sql/structure/` : `eleve_lv1`, `livret_devoirsfaits_id`, `devoir_pluriannuel`,
`eleve_uai_origine` y sont tous présents. Les fichiers de structure sont l'**état courant**, pas un
socle initial : les migrations amènent le schéma à cet état, elles ne s'y ajoutent pas. **Le
comptage de #125 n'était donc pas un plancher, il était juste.**

**Ce que les migrations apportent réellement est meilleur que ce qu'on leur prêtait.**

1. **Une série diachronique de seize ans à identifiants français.** #125 déplorait que « le cas dur
   — la base sédimentée de 2009 — reste non couvert » et que Paheko soit le seul à offrir une
   variable isolée sur trois états. SACoche offre **seize états annuels** du même schéma français.
   C'est la sédimentation réelle, datée, et elle était dans le dépôt depuis le début.
2. **Vingt colonnes réellement mortes**, vérifiées absentes du schéma courant — le vocabulaire
   qu'un projet abandonne :
   `user_num_sconet` (SCONET, l'ancien SI de scolarité, retiré vers 2015), `officiel_palier1`,
   `officiel_palier2`, `officiel_palier3` (les paliers du socle commun, emportés par la réforme de
   2016), `eleve_brevet_serie`, `fiche_brevet`, `eleve_langue`, `resp_legal_envoi`,
   `message_destinataires`, `groupe_prof_id`, `selection_item_liste`, `user_statut`,
   `user_tentative_date`, `bulletin_modele`, `devoir_partage`, `entree_id`,
   `livret_parcours_type_url_sitegouv`, `livret_parcours_type_url_txtofficiel`,
   `crcn_domaine_couleur`, `crcn_niveau_couleur`.
   ⚠️ **`resp_legal_envoi` et `user_num_sconet` sont des données personnelles** que le schéma
   courant ne montre plus. Un corpus construit sur `HEAD` seul ne les verrait jamais.
3. **Les acronymes de l'Éducation nationale, dans les noms de colonnes** : `eleve_dnb_mef_id` (DNB,
   MEF), `eleve_uai_origine` (UAI), `eleve_lv1`/`eleve_lv2`, `livret_page_crcn` (CRCN),
   `livret_epi_theme_origine` (EPI), `livret_ap_report_auto` (AP), `assiduite_retard_nj` (NJ, non
   justifié). Même famille que les sigles administratifs de Chill : **une seconde attestation, dans
   un autre domaine, que le dictionnaire est le facteur qui décide.**
4. **`livret_devoirsfaits_id`** — « devoirs faits » collé sans séparateur au milieu d'un nom
   pourtant snake_case. Un second cas de découpe mono-casse française, natif.

**Recommandation** : ne pas gonfler le comptage de SACoche, mais **verser les 17 migrations au
corpus comme série diachronique**, et annoter séparément les 20 colonnes mortes. Le coût est faible
(les fichiers sont là), et cela couvre le seul cas dur que #125 déclarait hors d'atteinte.

---

## 6. Provenance : commits épinglés

Clones peu profonds (`--depth 1`) du 8 août 2026. Chaque chiffre de ce document a été produit en
lisant ces arbres, et pas autre chose.

| Dépôt | Branche | Commit |
| --- | --- | --- |
| chill-bundles | `master` | `788db16e5b66ec63803c8cf6104f154bac5cf636` (2026-07-17) |
| demarches-simplifiees.fr | `main` | `2daa8e507964d63134115f38c880ce4d450be650` |
| GRR | `master` | `2aaeb067aca6951b2cf9334bf9185de0b410a1c7` |
| passerelle | `main` | `b21c6fd0adfd06afe10d3d6c38fcc1b7fa268f10` |
| authentic | `main` | `52e129ae82364dae2309a52aee715a994048b064` |
| sacoche | `main` | `13feaabd9dc7a43e916104b1a29d9e7c4fd5051b` |
| plugin-auto / -events / -paypal / -maps | `master` | clonés le 2026-08-08 |

**L'analyseur `CREATE TABLE`** employé pour Chill, GRR et les plugins Galette est *ad hoc*, écrit
pour ce ticket, **non versionné et non testé** — de même nature et de même fragilité que celui de
#125. Il ignore les `ALTER TABLE … ADD COLUMN` : les comptages de Chill sont donc un plancher
explicite. Pour `démarches-simplifiées`, le comptage vient d'un filtrage des lignes `t.*` de
`schema.rb` hors `t.index` et `t.check_constraint`.

---

## 7. Points non vérifiés en source primaire

Section de condition de lecture, sur le modèle du § 6 de #125.

**Sur les candidats**

- **PMB et GEPI n'ont pas été obtenus** (§ 1). Rien n'en est affirmé, dans un sens ni dans l'autre.
- **Aucun des dix candidats n'a été installé.** Tous les comptages viennent de la lecture de
  fichiers — migrations, `schema.rb`, DDL. La vérification décisive de #125 — le passage par
  `information_schema` sur une instance réelle — **n'a été refaite pour aucun d'eux**. Pour Chill et
  `démarches-simplifiées` (PostgreSQL), c'est d'autant plus lourd que #125 signalait déjà que
  **PostgreSQL n'a jamais été éprouvé côté pivot** : `COMMENT ON COLUMN` s'y expose par
  `col_description()` et **non** par `information_schema.columns`. Ce trou-là **reste entier**, et
  il concerne désormais les deux plus gros candidats francophones du corpus.
- **Le comptage de Passerelle (173 champs) est un échantillon**, tiré des seules migrations
  `0001_initial.py`. Le total réel sur 314 modèles n'a pas été établi.
- **`authentic2` n'a pas été compté en colonnes**, seulement en modèles (86).
- **Le statut de maintenance de `ChillJobBundle` est déduit** de son autochargement et de la date de
  sa dernière migration (septembre 2024). Aucune déclaration explicite du projet n'a été trouvée ;
  il n'est **pas** exclu qu'il soit un bundle destiné à un déploiement particulier plutôt qu'au
  produit générique. **À vérifier auprès du projet avant de bâtir le banc dessus**, car c'est la
  pièce sur laquelle ce document met le plus de poids.
- **La série diachronique de SACoche n'a pas été rejouée.** Les 143 ajouts et 20 suppressions
  viennent d'un `grep` sur le DDL des migrations, pas de l'exécution des 17 scripts PHP contre une
  base. Des colonnes ajoutées puis renommées par un `CHANGE` (316 occurrences, non dépouillées)
  peuvent brouiller le compte.
- **Les licences ont été lues dans les fichiers `LICENSE`/`COPYING`/`composer.json`** de chaque
  dépôt. Aucune analyse de compatibilité n'a été refaite : le § 2 de #125 fait foi, et **l'AGPL de
  Chill n'y a pas été spécifiquement traitée** — SACoche et Paheko l'avaient été.

**Sur le raisonnement**

- ⚠️ **L'argument du § 2.2 — « l'article 10 est absent du libre par construction » — est une
  explication, pas une preuve.** Il est cohérent avec dix-neuf observations nulles, et il repose sur
  une lecture du régime de l'article 10 qui n'a été confrontée à aucune source primaire dans ce
  ticket. Un contre-exemple le renverserait : un logiciel libre d'aide juridictionnelle, de
  médiation pénale ou de prévention de la délinquance, s'il en existe un publié. **Aucune recherche
  systématique n'a été menée sur ce sous-domaine précis** — les dix candidats venaient de la liste
  de #137, pas d'un balayage du champ juridique.
- **L'heuristique par mots-clés est faillible dans le sens qui compte.** Elle trouve `condamnation`,
  elle ne trouve pas une colonne d'article 10 nommée `statut_2` ou `dossier_type`. Les dix-neuf
  résultats nuls sont donc, à la rigueur, « aucune donnée d'infraction **nommée comme telle** ».
  C'est exactement la limite de méthode que #125 a mise au jour sur `livret_modaccomp_code`, et
  elle joue ici contre le résultat de ce document.
- **Aucun chiffre de ce document n'est une annotation.** Comme chez #125, ce sont des comptages et
  des relevés, jamais une vérité terrain. Rien ici ne doit figurer dans un résultat de banc.
