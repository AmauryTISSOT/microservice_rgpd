# Sources du corpus

Ce fichier recense la provenance de tout ce que contient `corpus/` : les applications dont les schémas
ont été relevés, leur version et leur licence.

## Schémas relevés (`schemas/pivots/`)

Chaque schéma a été relevé **par introspection du catalogue** d'une base créée à partir du DDL livré
par l'application, et non en lisant ses fichiers sources. Le corpus ne redistribue donc aucun code
amont : il ne contient que des noms de tables et de colonnes, leurs types et leurs commentaires. La
procédure est dans [`schemas/outils/extraire.sh`](./schemas/outils/extraire.sh), et la justification
juridique dans [`schemas/README.md`](./schemas/README.md#licence-du-corpus).

Les licences ont été lues dans le fichier de licence du dépôt cloné, et non dans le badge affiché par
la forge.

| Pivot | Application | Version | Dépôt | Commit | DDL source | Licence amont |
| --- | --- | --- | --- | --- | --- | --- |
| `dolibarr` | Dolibarr ERP/CRM | 25.0.0-alpha | [github.com/Dolibarr/dolibarr](https://github.com/Dolibarr/dolibarr/tree/fa08bb6559657b5031757b0306324ac5635582aa) | `fa08bb6` | `htdocs/install/mysql/tables/*.sql` | GPL-3.0 |
| `glpi` | GLPI | 11.0.9-dev | [github.com/glpi-project/glpi](https://github.com/glpi-project/glpi/tree/343a103) | `343a103` | `install/mysql/glpi-empty.sql` | GPL-3.0 |
| `openemr` | OpenEMR | 8.3.0-dev | [github.com/openemr/openemr](https://github.com/openemr/openemr/tree/2bf9abe) | `2bf9abe` | `sql/database.sql` | GPL-3.0 |
| `galette`, `galette-pg` | Galette | 1.3-dev | [github.com/galette/galette](https://github.com/galette/galette/tree/c8fb939) | `c8fb939` | `galette/install/scripts/mysql.sql`, `pgsql.sql` | GPL-3.0 |
| `sacoche` | SACoche | 2026-06-16 | [forge.apps.education.fr/sesamath/sacoche](https://forge.apps.education.fr/sesamath/sacoche/-/tree/13feaab) | `13feaab` | `_sql/structure/*.sql` | AGPL-3.0 |
| `paheko-0.8.0` | Paheko | 0.8.0 | [github.com/paheko/paheko](https://github.com/paheko/paheko/tree/eaf5710) | `eaf5710` | `archives/0.8.0_schema.sql` | AGPL-3.0 |
| `paheko-1.0.0` | Paheko | 1.0.0 | *idem* | `eaf5710` | `archives/1.0.0_schema.sql` | AGPL-3.0 |
| `paheko-head` | Paheko | 1.3.22 | *idem* | `eaf5710` | `src/include/data/schema.sql` | AGPL-3.0 |
| `temoin` | Brocanto (application témoin) | — | [`brocanto/`](../brocanto/README.md), dans ce dépôt | — | `brocanto/db/schema.sql` | MIT (ce dépôt) |

Les relevés datent du 8 août 2026. Le pivot `temoin` sert de test de fumée : il n'entre jamais dans
les mesures (voir [`schemas/README.md`](./schemas/README.md#volumétrie-mesurée)).

## Demandes RGPD (`demandes-rgpd.fr.jsonl`)

Les 120 demandes ont été rédigées pour ce projet. **Aucune n'est une donnée réelle** : les noms, les
sociétés et les références sont fictifs, les adresses électroniques utilisent le domaine réservé
`.invalid`, et les numéros de téléphone sont des suites de zéros (voir [`README.md`](./README.md)).

## Le corpus d'entraînement des 47 schémas

Le corpus décrit dans le mémoire (Partie 2, III, C : 47 schémas, 5 812 tables, 55 751 colonnes) ne se
trouve **pas** dans ce dépôt. Il a servi à entraîner et à comparer les cinq approches de détection (A0 à A4),
et il est tenu dans un dépôt de recherche distinct, `recherche_schema_v2`, qui liste la source et le
commit de chacun des schémas. Seul le modèle retenu, A2, est embarqué ici
([`src/MicroserviceRgpd.Infrastructure/Screenings/Embeddings/Artefact/`](../src/MicroserviceRgpd.Infrastructure/Screenings/Embeddings/Artefact/README.md)).

## Licence

Le contenu de `corpus/` est diffusé sous licence **CC BY-SA 4.0** (voir [`LICENSE`](./LICENSE)).
Les outils de `schemas/outils/` suivent la licence MIT du dépôt.
