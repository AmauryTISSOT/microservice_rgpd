# Captures authentiques des requêtes de relevé

Les fichiers de `captures/` sont les sorties **réellement produites** par les requêtes de
`releves/`, jouées sur de vrais SGBD en conteneur. Le filet de
[#284](https://github.com/AmauryTISSOT/microservice_rgpd/issues/284) les rejoue contre
`ColumnListingIngestion.Ingest` à chaque build — voir
`tests/MicroserviceRgpd.FunctionalTests/Screenings/ServedListingQueries.cs`.

⚠️ **Aucune de ces lignes n'a été écrite à la main.** C'est tout l'intérêt. Le défaut de #284 —
trois requêtes servies à l'`Operator` que le service refusait — a survécu parce que rien ne mettait
la sortie d'une requête face à l'ingestion. Une fixture rédigée à la main aurait le même angle mort :
elle dirait ce que son auteur croit que la requête produit.

C'est aussi pourquoi les captures de `exploration/faits-connexion/ingestion/captures-rapiecees/`
(branche `research/faits-connexion-conteneur`) ne sont **pas** réutilisées ici : elles ont été
fabriquées au `sed` par `rapiecer.sh` depuis des captures de requêtes cassées, pour isoler un défaut
à la fois. Aucune requête n'en est l'auteur.

## Ce qui a produit ces captures

| Capture             | Version exacte              | Requête                 |
| ------------------- | --------------------------- | ----------------------- |
| `postgresql.txt`    | PostgreSQL 17.11 (Alpine)   | `releves/postgresql.sql`|
| `mysql84.txt`       | MySQL 8.4.11                | `releves/mariadb.sql`   |
| `mariadb118.txt`    | MariaDB 11.8.9              | `releves/mariadb.sql`   |
| `sqlite.txt`        | SQLite 3.53.4               | `releves/sqlite.sql`    |

Les trois `*-fixture.sql` sont les bases d'épreuve : elles exercent ce que le pivot doit rendre —
les deux valeurs de nullabilité, une clé étrangère, un commentaire de table et un de colonne là où
le SGBD en rend, un `DROP COLUMN` qui creuse un trou dans la numérotation physique, et pour
PostgreSQL deux schémas portant la même table.

⚠️ **Le banc #275 avait relevé SQLite 3.49.1** ; l'image publique a avancé depuis. Le fait attesté ne
dépend pas de la version : SQLite n'a aucun type booléen, sur 3.46 comme sur 3.53.

## Réextraire

```sh
bash exploration/releves-authentiques/capter.sh          # les quatre
bash exploration/releves-authentiques/capter.sh sqlite   # un seul
```

⚠️ **Réextraire après avoir touché une requête, jamais pour « rafraîchir ».** C'est le seul moment
où le diff distingue « j'ai changé la requête » de « la sortie a changé ». Un `genere_le` qui bouge
seul est du bruit qui masquera le jour où autre chose bougera avec lui.

⚠️ **Ne pas confondre avec `corpus/schemas/pivots/`**, qui est un instrument de mesure **gelé** dans
une forme antérieure et qu'on ne réextrait jamais. Voir son `README.md`.
