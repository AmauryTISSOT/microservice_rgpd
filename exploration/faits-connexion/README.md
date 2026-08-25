# Épreuve sur conteneur des faits du workflow connecté

Banc de vérification pour [#275](https://github.com/AmauryTISSOT/microservice_rgpd/issues/275),
carte [#261](https://github.com/AmauryTISSOT/microservice_rgpd/issues/261). La
[recherche #266](https://github.com/AmauryTISSOT/microservice_rgpd/issues/266) a nommé les faits
qu'aucune page de documentation ne déclare en toutes lettres ; ici on les fait tourner.

⚠️ **Ce banc ne décide de rien.** Il fournit des observations aux tickets qu'il débloque.

## Ce qui est éprouvé, et où

| Dossier            | Question                                                                     |
| ------------------ | ---------------------------------------------------------------------------- |
| `postgresql/`      | Un rôle sans aucun privilège objet relève-t-il 100 % par `pg_catalog`, 0 % par `information_schema` ? |
| `mysql/`           | Un `GRANT SELECT (colonne)` ampute-t-il le relevé ? `JSON_OBJECT` produit-il un booléen ? |
| `sqlite-interrupt/`| `sqlite3_interrupt` via `SqliteConnection.Handle` fonctionne-t-il, et sur quel code d'erreur ? |
| `ingestion/`       | Le service accepte-t-il les relevés que ses **propres** requêtes viennent de produire ? |

Les trois premiers dossiers écrivent des captures (`capture-*.txt`, `sortie-*.txt`) que le
quatrième relit. `ingestion/` ne conclut pas par lecture du parseur : il passe les octets réellement
sortis des conteneurs dans `ColumnListingIngestion.Ingest`.

## Comment relancer

Docker Desktop doit tourner. Depuis ce dossier :

```sh
bash postgresql/run.sh                 # postgres:17-alpine
bash mysql/run.sh mysql:8.4
bash mysql/run.sh mariadb:11
dotnet run --project sqlite-interrupt/Sonde.csproj -c Release

bash ingestion/rapiecer.sh             # isole le défaut suivant en corrigeant le précédent
dotnet run --project ingestion/Verdict.csproj -c Release
```

Les deux projets `.csproj` sont **délibérément hors de la solution** et hors de la gestion
centralisée des versions : chacun porte un `Directory.Build.props` et un `Directory.Packages.props`
vides qui coupent l'héritage de la racine. Ce sont des bancs jetables, pas du code de production.

## Ce que le banc a trouvé

Le compte rendu complet est le commentaire de résolution de
[#275](https://github.com/AmauryTISSOT/microservice_rgpd/issues/275). En deux lignes :

- Le fait qui tient la **contrainte 6** tient côté PostgreSQL — relevé identique octet pour octet
  entre superutilisateur et rôle nu — et **ne tient nulle part ailleurs** : sur MySQL comme sur
  MariaDB, un droit partiel produit un pivot amputé, structurellement valide, que l'ingestion
  **accepte**.
- `releves/*.sql` produit aujourd'hui, sur les **trois** dialectes, un pivot que le service refuse :
  ligne de fin sans `"fin":true`, et `nullable` en `0`/`1` au lieu d'un booléen JSON.
