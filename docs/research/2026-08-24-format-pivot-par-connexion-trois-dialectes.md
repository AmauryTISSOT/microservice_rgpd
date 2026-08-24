# Produire le format pivot `screening-pivot/1` par connexion, sur les trois dialectes

Findings de [#266](https://github.com/AmauryTISSOT/microservice_rgpd/issues/266), carte
[#261](https://github.com/AmauryTISSOT/microservice_rgpd/issues/261). Rédigé le 2026-08-24.

Sources : documentation officielle PostgreSQL 17, MySQL 8.4, MariaDB 11.x, SQLite 3.4x, npgsql.org,
mysqlconnector.net, learn.microsoft.com, nuget.org. Chaque affirmation porte son lien. Ce qui n'a pas
pu être vérifié à la source est signalé **⚠ non vérifié** à l'endroit où il est écrit, et repris en
fin de document.

---

## Résumé exécutif

1. **La voie connectée doit lire des colonnes brutes et sérialiser le pivot en C#**, et non demander
   au SGBD de construire le JSON comme le fait le chemin collé. Trois raisons cumulées : le pivot
   exige `nullable` en **booléen JSON**, or MariaDB n'a pas de type JSON natif (`JSON` y est un alias
   de `LONGTEXT`) et ne sait pas en émettre un ; `ColumnListing` **n'a aucun constructeur public**, le
   seul chemin vers le domaine est `ColumnListingIngestion.Ingest`, donc la contrainte 1 est tenue
   *par construction* si l'on repasse par le texte pivot ; et l'avancement réel exigé par la
   contrainte 9 se compte en lisant les lignes, pas en recevant un bloc.
2. **Écart trouvé dans le dépôt, à corriger avant toute construction** : les trois requêtes de
   `releves/` émettent `"nullable":1` (un nombre) et une ligne de fin `{"colonnes":N}` sans
   `"fin":true`. `ColumnListingIngestion` **refuse les deux**. Les requêtes de référence, telles
   qu'elles sont sur `main`, ne produisent pas un pivot ingérable. Détail et preuve au § 0.
3. **PostgreSQL : lire `pg_catalog`, jamais `information_schema`** — et le motif n'est pas seulement
   les commentaires. `information_schema` est **filtré par privilège**, ligne à ligne, ce qui rendrait
   un relevé **silencieusement partiel** : la contrainte 6 (« schéma entier ou rien ») tomberait sans
   un mot. `pg_catalog` n'est pas filtré.
4. **MySQL/MariaDB n'offrent pas cette échappatoire** : `information_schema` y est filtré et il n'y a
   pas de second catalogue. Il n'existe **aucun privilège « voir la structure sans les données »**. Le
   minimum fiable est `GRANT SELECT ON base.*`. Un `GRANT` table par table ou colonne par colonne
   produit un relevé faux et muet : à proscrire dans la documentation client.
5. **Microsoft.Data.Sqlite n'a pas d'async** : « Async ADO.NET methods will execute synchronously ».
   `SqliteCommand.Cancel()` est documenté « **Does nothing.** », et `CommandTimeout` n'y borne que
   l'attente **d'un verrou**, jamais la durée d'un scan. L'interruption passe obligatoirement par
   `sqlite3_interrupt()` sur `SqliteConnection.Handle`, depuis un autre fil.
6. **Pilotes** : `Npgsql` 10.0.3 (licence PostgreSQL) est **déjà là**, tiré par
   `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3. `MySqlConnector` 2.6.2, **MIT**, à préférer à
   `MySql.Data` 26.7.0 dont la licence est **GPL-2.0-only WITH Universal-FOSS-exception-1.0** et dont
   l'async est faux. `Microsoft.Data.Sqlite` 10.0.11, MIT.
7. **Troncature** : PostgreSQL exige `left(col::text, N)` — sans le cast, `left(entier, N)` **lève**.
   MySQL/MariaDB castent implicitement, SQLite exige un filtre `typeof()` parce que le typage y est
   dynamique et que `CAST(blob AS TEXT)` ne valide rien.
8. **Coût** : le schéma tient en **une requête** par dialecte. Le prélèvement demande **une requête
   par table** — 300 allers-retours, soit ~9 s sur un lien à 30 ms. Le regroupement en lot est
   possible partout, mais un lot Npgsql est enveloppé dans une transaction implicite où **une
   instruction en échec annule tout le lot** : c'est l'exact opposé de la contrainte 6, qui veut
   « 299 aperçus et une raison nommée ». Recommandation : lots de 20 à 50 tables avec repli
   individuel, jamais un lot unique.
9. **Surprise de sécurité** : MySqlConnector active `CLIENT_MULTI_STATEMENTS` **inconditionnellement**.
   Or le prélèvement interpole des noms de table et de colonne venus du catalogue d'une base tierce.
   Le quoting d'identifiants devient un point de revue, pas une formalité. Et `AllowLoadLocalInfile`
   doit rester à `false` : à `true`, un serveur hostile fait lire au microservice des fichiers de son
   propre disque.

---

## 0. Ce que le dépôt dit déjà, et l'écart à corriger

### 0.1 Les trois requêtes de référence

Elles sont sur `main`, à la racine, dans `releves/` : `postgresql.sql`, `mariadb.sql`, `sqlite.sql`.
Elles sont embarquées comme ressources par `src/MicroserviceRgpd.Web/Pages/Screenings/ListingQuery.cs`
et servies à l'`Operator` à l'écran de dépôt. Ce sont les **mêmes** que celles qui ont extrait le
corpus (`corpus/schemas/outils/extraire.sh`).

Trois faits confirmés, comme demandé :

**(a) `pg_catalog` et non `information_schema`, pour les commentaires.** Vérifié à la source :
[`information_schema.columns`](https://www.postgresql.org/docs/17/infoschema-columns.html) n'a aucune
colonne de commentaire, et [`information_schema.tables`](https://www.postgresql.org/docs/17/infoschema-tables.html)
non plus. `COMMENT` est une extension PostgreSQL hors norme SQL. Les commentaires ne se lisent que par
`col_description(table oid, column integer)` et `obj_description(object oid, catalog name)`, décrites
en [9.27.6 Comment Information Functions](https://www.postgresql.org/docs/17/functions-info.html) —
et `col_description` y porte la note qui règle la question : « `obj_description` cannot be used for
table columns, since columns do not have OIDs of their own. » La forme à un seul argument de
`obj_description` est dépréciée (« there is no guarantee that OIDs are unique across different system
catalogs ») : `releves/postgresql.sql` passe bien `'pg_class'`, c'est correct.

**Ce fait ne vaut pas pour MySQL/MariaDB.** Leur `information_schema` porte les commentaires :
`COLUMNS.COLUMN_COMMENT` — « Any comment included in the column definition. »
([MySQL 8.4](https://dev.mysql.com/doc/refman/8.4/en/information-schema-columns-table.html)) — et
`TABLES.TABLE_COMMENT` — « The comment used when creating the table (or information as to why MySQL
could not access the table information). »
([MySQL 8.4](https://dev.mysql.com/doc/refman/8.4/en/information-schema-tables-table.html),
[MariaDB KB](https://mariadb.com/kb/en/information-schema-tables-table/)). Longueurs maximales
documentées : 2048 caractères pour le commentaire de table, 1024 pour celui de colonne
([CREATE TABLE, MySQL 8.4](https://dev.mysql.com/doc/refman/8.4/en/create-table.html)) — utile à
savoir puisque `ScreeningText` borne ailleurs à 100.

**(b) `position` recalculé par `row_number()`, jamais `attnum`.** La doc de
[`pg_attribute`](https://www.postgresql.org/docs/17/catalog-pg-attribute.html) dit : `attnum` « The
number of the column. Ordinary columns are numbered from 1 up. » et `attisdropped` « This column has
been dropped and is no longer valid. **A dropped column is still physically present in the table**,
but is ignored by the parser ». [`ALTER TABLE`](https://www.postgresql.org/docs/17/sql-altertable.html)
confirme : « The `DROP COLUMN` form does not physically remove the column, but simply makes it
invisible to SQL operations. »
⚠ **non vérifié à la lettre** : aucune page n'écrit « les `attnum` ne sont jamais réutilisés ». Ce qui
est documenté, c'est que la ligne du tombstone **reste** dans `pg_attribute` avec son `attnum`. La
conséquence est mécanique : dès qu'on filtre `attisdropped = false`, la suite des `attnum` porte des
trous, et un trou est le cas de refus n° 5 (`RankGap`).

**Conséquences directes pour la voie connectée**, et elles vont plus loin que PostgreSQL :

- Le rang doit être **recalculé** dans tous les cas. Ne jamais transporter le rang natif du catalogue.
- Le calcul peut se faire côté SGBD (`ROW_NUMBER()`, disponible PostgreSQL, MySQL 8.0+, MariaDB
  10.2+, SQLite 3.25+) **ou côté C#**. La voie connectée lisant des colonnes brutes, le faire en C#
  est plus simple, uniforme sur les trois dialectes, et testable sans base — c'est ce que je
  recommande. Il suffit d'ordonner par le rang natif et de numéroter à 1.
- Cela vaut aussi ailleurs : `INFORMATION_SCHEMA.COLUMNS.ORDINAL_POSITION` n'est **pas** documenté
  comme partant de 1 ni comme contigu (la phrase « Column positions are numbered beginning with 1 »
  n'apparaît que pour [`KEY_COLUMN_USAGE`](https://dev.mysql.com/doc/refman/8.4/en/information-schema-key-column-usage-table.html),
  où elle désigne d'ailleurs la position *dans la contrainte*). Et côté SQLite,
  [`PRAGMA table_info`](https://www.sqlite.org/pragma.html) avertit explicitement : « The "cid" column
  should not be taken to mean more than "rank within the current result set". »
  Un seul code de renumérotation en C# règle les trois cas.

**(c) L'écart entre `releves/*.sql` et `pivot-format.md` — et c'est le code qui a raison.**

`ColumnListingIngestion.ReadColumn` exige, pour chaque ligne :

```csharp
!HasKind(fields, NullableKey, JsonValueKind.True, JsonValueKind.False, JsonValueKind.Null)
```

et pour la ligne de fin :

```csharp
ReadBoolean(closingFields, ClosingMarkerKey) is not true   // la clé "fin"
```

Or les trois requêtes de `releves/` émettent :

- `'nullable', CASE WHEN nullable THEN 1 ELSE 0 END` → `"nullable":1`, de kind `Number` ;
- une ligne de fin `json_object('colonnes', …)` → `{"colonnes":194}`, **sans `"fin":true`**.

Les pivots du corpus le montrent en clair
(`corpus/schemas/pivots/galette-pg.jsonl`, première ligne de colonne et dernière ligne). Et le test
`tests/MicroserviceRgpd.UnitTests/Infrastructure/Screenings/ScreeningOnTheTemoinPivotTests.cs` le
**convertit explicitement** avant d'ingérer :

```csharp
.Replace("\"nullable\": 0", "\"nullable\": false", …)
.Replace("\"nullable\": 1", "\"nullable\": true", …)
lines[^1] = lines[^1].Replace("{\"colonnes\"", "{\"fin\":true,\"colonnes\"", …);
```

avec le commentaire « le pivot du corpus a été produit **avant** que `pivot-format.md` ne fige la
forme ». Ce commentaire est exact pour le corpus — mais **les requêtes n'ont pas été mises à jour
depuis** (`git log -- releves/` ne rend qu'un seul commit, `ab8ce57`, celui de l'extraction du
corpus).

**Verdict, à trancher explicitement :**

| champ | ce que l'ingestion accepte | ce que `releves/*.sql` émet | verdict |
| --- | --- | --- | --- |
| `nullable` | `true`, `false`, `null` **uniquement** | `1` / `0` | **refusé** — cas n° 4 `UnreadableColumnLine` |
| ligne de fin | `{"fin":true,"colonnes":N}` | `{"colonnes":N}` | **refusé** — cas n° 2 `MissingClosingLine` |
| `type`, commentaires, `table_referencee` | `String` ou `Null` ; `""` est un `String`, normalisé en `null` par `ReadText`/`OrAbsent` | `''` | **accepté** — les deux formes passent |
| `position` | `Number` entier ≥ 0 | entier | accepté |
| `genere_le` | `yyyy-MM-ddTHH:mm:ss(.FFFFFFF)zzz`, décalage **obligatoire**, `Z` toléré | `…Z` | accepté |

Donc : **la documentation n'est pas en retard sur le code, ce sont les requêtes qui le sont.**
`pivot-format.md` et `ColumnListingIngestion` disent la même chose ; `releves/*.sql` dit autre chose.
Deux corrections à faire dans `releves/`, et elles ne sont pas cosmétiques — en l'état, un `Operator`
qui suit la procédure documentée reçoit un refus.

⚠ **Et la correction n'est pas triviale sur MariaDB** : voir § 1.4. C'est cet obstacle-là qui commande
la recommandation d'architecture.

### 0.2 Ce que l'ingestion impose au sérialiseur de la voie connectée

Pour que le `ColumnListing` produit par connexion soit *le même objet* que le collé, le sérialiseur
doit émettre exactement :

- en-tête : `{"format":"screening-pivot/1","dialecte":"…","base":"…","genere_le":"…"}` — `dialecte`
  ≤ 64 caractères, `base` ≤ 100 caractères, sans caractère de contrôle
  (`Screening.MaxDialectLength`, `Screening.MaxDatabaseNameLength`, `ScreeningText.OrThrow`).
  ⚠ **Côté SQLite, `base` est le chemin du fichier** : un chemin de plus de 100 caractères fait
  refuser un scan sincère au titre du cas n° 1. C'est déjà écrit dans `pivot-format.md` pour le
  chemin collé ; la voie connectée hérite du problème, et elle le rencontrera plus souvent (chemins
  de conteneur, montages).
- `genere_le` : `DateTimeOffset.UtcNow.ToString("O")` convient (`…+00:00`). Le prendre de l'horloge
  **du service**, pas du serveur de base : c'est le seul moyen d'avoir un décalage sûr, et cela
  supprime la divergence `to_char` / `DATE_FORMAT` / `strftime`.
- lignes : les neuf clés, toutes présentes, `nullable` en booléen ou `null`, `position` entier.
- ligne de fin : `{"fin":true,"colonnes":N}` où **N est compté sur la même lecture** que les lignes.
- plafond : 20 000 colonnes, borne incluse (`ColumnListing.MaxColumns`). Le corpus donne l'échelle
  réelle : Dolibarr 413 tables / 5 382 colonnes, GLPI 442 / 4 519, OpenEMR 283 / 3 780
  (`corpus/schemas/README.md`). Une base de 300 tables est le cas nominal, pas le cas extrême.

Les noms de schéma, table et colonne passent `ColumnIdentity.Of` → `ScreeningText.OrThrow`, borné à
**100 caractères** et **sans caractère de contrôle**. Un nom plus long fait refuser tout le relevé.
C'est atteignable : PostgreSQL borne les identifiants à 63 octets, MySQL à 64, mais SQLite ne borne
rien.

---

## 1. Les requêtes catalogue par dialecte

### 1.1 PostgreSQL 16/17 — `pg_catalog`

```sql
SELECT n.nspname                                   AS schema_name,
       c.relname                                   AS table_name,
       a.attname                                   AS column_name,
       a.attnum                                    AS native_rank,   -- renuméroté en C#
       format_type(a.atttypid, a.atttypmod)        AS declared_type,
       NOT a.attnotnull                            AS is_nullable,   -- vrai booléen
       col_description(c.oid, a.attnum)            AS column_comment,
       obj_description(c.oid, 'pg_class')          AS table_comment,
       fc.relname                                  AS referenced_table
FROM pg_catalog.pg_class c
JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
JOIN pg_catalog.pg_attribute a ON a.attrelid = c.oid
LEFT JOIN LATERAL (
  SELECT MIN(k.confrelid) AS confrelid
  FROM pg_catalog.pg_constraint k
  WHERE k.conrelid = c.oid AND k.contype = 'f' AND a.attnum = ANY (k.conkey)
) fk ON TRUE
LEFT JOIN pg_catalog.pg_class fc ON fc.oid = fk.confrelid
WHERE c.relkind = 'r'
  AND a.attnum > 0
  AND NOT a.attisdropped
  AND n.nspname NOT IN ('pg_catalog', 'information_schema')
  AND n.nspname !~ '^pg_toast'
ORDER BY n.nspname, c.relname, a.attnum;
```

**Ce qu'elle rend.** Les neuf champs, commentaires compris. `is_nullable` est un **vrai booléen
PostgreSQL**, ce qui est le cas le plus simple des trois.

**Le type déclaré en entier** : `format_type(type oid, typemod integer)` — « Returns the SQL name for
a data type that is identified by its type OID and possibly a type modifier »
([9.27.4](https://www.postgresql.org/docs/17/functions-info.html)) — combiné à `pg_attribute.atttypmod`,
« records type-specific data supplied at table creation time (for example, the maximum length of a
`varchar` column) ». ⚠ **non vérifié à la lettre** : la doc ne dit pas « rend la longueur », c'est
l'assemblage des deux phrases. Attention aussi : `format_type` rend le **nom SQL canonique**
(`character varying(255)`), pas l'orthographe du DDL (`varchar(255)`) — le pivot du corpus le montre.
C'est un écart d'affichage à assumer, pas un défaut.

**Ce qu'elle ne rend pas.** Rien du domaine des neuf champs. En revanche `relkind = 'r'` écarte
délibérément vues, vues matérialisées, tables partitionnées et tables distantes — le corpus a été
extrait ainsi, la voie connectée doit faire pareil pour rester comparable, mais c'est une décision à
reconfirmer (voir « Ce qui reste ouvert »).

**Clé étrangère.** `pg_constraint` : `contype = 'f'`, `conrelid` la table portante, `confrelid` la
table pointée, `conkey` les colonnes contraintes
([doc](https://www.postgresql.org/docs/17/catalog-pg-constraint.html)). Le pivot ne veut **qu'une**
table référencée par colonne — d'où le `MIN(...)`, comme dans `releves/postgresql.sql` : sans lui,
deux FK sur la même colonne dupliqueraient la ligne et déclencheraient le cas n° 6
(`DuplicateColumn`). Ce piège est réel et déjà résolu dans le chemin collé ; la voie connectée doit le
reprendre tel quel.

### 1.2 MariaDB 11.x / MySQL 8.x — `information_schema`

```sql
SELECT c.TABLE_SCHEMA, c.TABLE_NAME, c.COLUMN_NAME,
       c.ORDINAL_POSITION            AS native_rank,   -- renuméroté en C#
       c.COLUMN_TYPE                 AS declared_type,
       c.IS_NULLABLE,                                  -- 'YES' / 'NO', chaîne
       c.COLUMN_COMMENT,
       t.TABLE_COMMENT,
       fk.REFERENCED_TABLE_NAME
FROM information_schema.COLUMNS c
JOIN information_schema.TABLES t
  ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME
 AND t.TABLE_TYPE = 'BASE TABLE'
LEFT JOIN (
  SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME,
         MIN(REFERENCED_TABLE_NAME) AS REFERENCED_TABLE_NAME
  FROM information_schema.KEY_COLUMN_USAGE
  WHERE REFERENCED_TABLE_NAME IS NOT NULL
  GROUP BY TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME
) fk ON fk.TABLE_SCHEMA = c.TABLE_SCHEMA
    AND fk.TABLE_NAME   = c.TABLE_NAME
    AND fk.COLUMN_NAME  = c.COLUMN_NAME
WHERE c.TABLE_SCHEMA = DATABASE()
ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION;
```

**Le type déclaré en entier : `COLUMN_TYPE`, jamais `DATA_TYPE`.** Verbatim : « The `DATA_TYPE` value
is the type name only with no other information. The `COLUMN_TYPE` value contains the type name and
possibly other information such as the precision or length. »
([MySQL 8.4](https://dev.mysql.com/doc/refman/8.4/en/information-schema-columns-table.html)) — c'est
lui qui rend `int(10) unsigned`, `enum('a','b')`, `decimal(10,2)`.

**Le piège `IS_NULLABLE`, confirmé à la source.** « The column nullability. The value is `YES` if
`NULL` values can be stored in the column, `NO` if not. » — une **chaîne**, jamais un booléen. Côté
C#, comparer la chaîne (`string.Equals(value, "YES", StringComparison.Ordinal)`), ne jamais caster.
⚠ **non vérifié côté MariaDB** : la KB se borne à « Whether the column can contain NULL values » sans
donner les valeurs littérales. MariaDB suit la norme SQL (domaine `yes_or_no`) et rend `'YES'`/`'NO'`
en pratique, mais la phrase n'est pas écrite. Un contrôle défensif (valeur inattendue → refus, pas
`false` par défaut) est donc justifié.

**Ce qu'elle ne rend pas.** `TABLE_COMMENT` peut porter, à la place du commentaire, « information as
to why MySQL could not access the table information » — c'est-à-dire un message d'erreur, documenté.
⚠ **non vérifié** : le suffixe historique `InnoDB free: N kB` n'apparaît dans aucune doc consultée
(MySQL 8.4, 5.7, KB MariaDB). Il existait sur les branches 5.x et paraît avoir disparu avec le data
dictionary de 8.0, mais je ne peux pas l'attester. Ne pas le parser ; le journaliser si on le voit.

### 1.3 SQLite 3.4x — pragmas et `sqlite_master`

```sql
SELECT m.name                                   AS table_name,
       ti.name                                  AS column_name,
       ti.cid                                   AS native_rank,   -- renuméroté en C#
       ti.type                                  AS declared_type, -- '' si non déclaré
       ti."notnull"                             AS not_null,
       (SELECT MIN(fk."table")
        FROM pragma_foreign_key_list(m.name) fk
        WHERE fk."from" = ti.name)              AS referenced_table
FROM sqlite_master m
JOIN pragma_table_info(m.name) ti
WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite_%'
ORDER BY m.name, ti.cid;
```

**Aucun commentaire, et c'est structurel.** SQLite n'a ni `information_schema` ni clause `COMMENT`.
[`lang_comment`](https://www.sqlite.org/lang_comment.html) : « Comments are not SQL commands […]
**Comments are treated as whitespace by the parser.** » ⚠ **non vérifié (preuve d'absence)** : aucune
page ne dit « SQLite ne stocke pas de commentaire d'objet » ; c'est l'absence de clause dans la
grammaire de `CREATE TABLE` qui l'établit. Nuance exploitable et à écarter délibérément :
`sqlite_schema.sql` conserve le **texte du DDL**, donc un `--` écrit dans le `CREATE TABLE` y survit
et serait extractible par parsing. **Ne pas le faire** : ce serait un signal que le chemin collé ne
donne pas, et les deux chemins doivent rendre le même objet (contrainte 1).

**Le type déclaré.** `table_info.type` = « data type if given, else '' ». Corroboré par
[`sqlite3_column_decltype`](https://www.sqlite.org/c3ref/column_decltype.html), qui rend `"VARIANT"`
pour `CREATE TABLE t1(c1 VARIANT)`. Et
[Datatypes In SQLite](https://www.sqlite.org/datatype3.html) : les « numeric arguments in parentheses
that follow the type name (ex: "VARCHAR(255)") are **ignored by SQLite** ». ⚠ **Conséquence métier à
écrire quelque part** : en SQLite, `varchar(255)` **n'est pas une contrainte**. Le champ `type` du
pivot y est déclaratif, pas garanti — ce que le moteur de détection utilise déjà comme *filtre* et
non comme *signal*, donc sans dégât, mais il ne faut pas l'oublier en lisant un rapport SQLite.

**`table_info` ou `table_xinfo` ?** `table_info` « does not show information about generated columns
or hidden columns » ; `table_xinfo` les rend, avec une colonne `hidden` supplémentaire
(0 normale, 1 cachée de table virtuelle, 2 ou 3 générée). Les colonnes **générées** contiennent des
données réelles et devraient entrer dans un relevé RGPD. Le chemin collé emploie `table_info` : les
deux chemins doivent employer le même. C'est une décision, pas un détail — voir « Ce qui reste
ouvert ».

**⚠ Point d'attention réel** : la documentation SQLite **ne nomme nulle part** les colonnes rendues
par `pragma_foreign_key_list` (`id, seq, table, from, to, on_update, on_delete, match`). Ces noms sont
ceux de l'implémentation, stables de fait mais non contractuels. Y accéder **par nom** (`"table"` et
`"from"` sont des mots-clés, à échapper) et prévoir un chemin d'erreur nommé plutôt qu'un accès par
index. Même remarque pour `cid`, dont la doc dit de ne rien lui prêter d'autre qu'un rang.

Le chemin du fichier : `pragma_database_list`, troisième colonne (`file`), « the name of the database
file itself, or an empty string if the database is not associated with a file ». Côté ADO.NET,
`SqliteConnection.DataSource` donne la même chose plus simplement.

Versions minimales : pragmas comme fonctions de table depuis **3.16.0 (2017-01-02)**
([pragma.html](https://www.sqlite.org/pragma.html)) ; `json_object()` intégré par défaut depuis
**3.38.0 (2022-02-22)** ([json1](https://www.sqlite.org/json1.html)). Sur la voie connectée c'est
**notre** SQLite qui s'exécute (SQLitePCLRaw `e_sqlite3` embarqué), donc ces bornes sont acquises —
argument secondaire mais réel en faveur de la voie connectée pour SQLite.

### 1.4 Chemin collé (JSON par le SGBD) contre chemin connecté (colonnes brutes + C#) — la recommandation

**Recommandation : sur la voie connectée, lire des colonnes brutes et sérialiser le pivot en C#.**

Cinq motifs, du plus contraignant au plus confortable.

**(1) MariaDB ne sait pas émettre un booléen JSON.** Le pivot exige `nullable` en `true`/`false`. Or
le type `JSON` de MariaDB « is an alias for LONGTEXT COLLATE utf8mb4_bin introduced for compatibility
reasons with MySQL's JSON data type » ([MariaDB KB](https://mariadb.com/kb/en/json-data-type/)) : il
n'y a pas de type JSON natif, donc pas de valeur JSON booléenne. `JSON_OBJECT('nullable',
IS_NULLABLE = 'YES')` y produit `1`, pas `true`. Côté MySQL 8, l'exemple documenté
`JSON_ARRAY(1, "abc", NULL, TRUE, CURTIME())` → `[1, "abc", null, true, …]`
([Creation Functions](https://dev.mysql.com/doc/refman/8.4/en/json-creation-functions.html)) suggère
que le littéral booléen y passe bien — **et c'est exactement le problème** : une seule requête sert
les deux SGBD sous le dialecte déclaré `mariadb`, et elle produirait un pivot accepté sur MySQL et
refusé sur MariaDB. ⚠ **non vérifié par exécution** — à éprouver sur conteneur avant de corriger
`releves/mariadb.sql`.
Côté PostgreSQL le problème n'existe pas (`json_build_object` sur un booléen rend `true`), et côté
SQLite il se contourne (`json_object('nullable', json('true'))`, la doc json1 précisant qu'un argument
« comes directly from another JSON function » est traité comme du JSON) — mais au prix d'une
gymnastique par dialecte, pour un champ que C# écrit en un mot.

**(2) `ColumnListing` n'a aucun constructeur public.** Son seul chemin d'entrée est
`ColumnListingIngestion.Ingest(string)`. Faire produire un `ColumnListing` à la voie connectée sans
passer par le texte pivot exigerait d'ouvrir une seconde porte dans `Core` — et cette seconde porte
**contournerait les neuf contrôles**. Or trois d'entre eux gardent précisément ce que la contrainte 6
exige du scan : `CountMismatch` attrape une lecture interrompue au milieu, `DuplicateColumn` attrape
la duplication de ligne par double clé étrangère, `RankGap` attrape le trou de position. Repasser par
le texte, c'est réutiliser gratuitement le seul endroit du dépôt qui sait dire « entier ou rien ».

**(3) L'avancement réel de la contrainte 9.** Un pivot construit par le SGBD arrive en un bloc : on ne
peut pas afficher « 1 240 colonnes sur 4 519 ». En lisant les colonnes brutes avec un
`DbDataReader`, le compteur est gratuit et honnête.

**(4) Les particularités de dialecte deviennent testables sans base.** Le `'YES'`/`'NO'`, la
renumérotation, la déduplication de FK, la normalisation des chaînes vides — tout cela vit dans une
classe C# éprouvable en tests unitaires, au lieu de vivre dans trois textes SQL dont le seul banc
d'essai est un conteneur.

**(5) La requête cesse d'exiger des fonctions JSON du serveur du client.** `json_build_object` demande
PostgreSQL ≥ 9.4, `JSON_OBJECT` MariaDB ≥ 10.2 / MySQL ≥ 5.7. Un `SELECT` de colonnes ordinaires ne
demande rien.

**Le prix, et il est modeste.** Double sérialisation : 20 000 lignes × ~200 octets ≈ 4 Mo de chaîne
construite puis reparcourue par `JsonDocument`. C'est de l'ordre de la centaine de millisecondes,
face à un scan qui dure des dizaines de secondes. Si ce coût devenait gênant, la sortie propre est
d'**extraire les trois contrôles d'intégrité** de `ColumnListingIngestion` vers un garde partagé, et
d'ouvrir un second constructeur interne — mais c'est un refactoring de `Core`, à décider séparément,
pas à improviser dans la livraison.

**Corollaire à ne pas manquer** : les trois fichiers de `releves/` restent la référence du chemin
**collé** et doivent être corrigés (§ 0.1c) même si la voie connectée ne les exécute pas. Et le
sérialiseur C# doit être épinglé par un test qui compare, sur un même schéma, le pivot produit par la
requête et celui produit par le code : c'est le seul contrôle mécanique de la contrainte 1.

---

## 2. Les pilotes .NET

### 2.1 Versions, licences, compatibilité avec `Directory.Packages.props`

| paquet | version stable | licence | cibles | statut dans le dépôt |
| --- | --- | --- | --- | --- |
| [`Npgsql`](https://www.nuget.org/packages/Npgsql) | **10.0.3** (2026-05-27) | **PostgreSQL License** | net8.0, net9.0, **net10.0** | **déjà tiré transitivement** par `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, qui dépend de `Npgsql >= 10.0.3` |
| [`MySqlConnector`](https://www.nuget.org/packages/MySqlConnector) | **2.6.2** (2026-08-11) | **MIT** | net6.0, net8.0, net9.0, **net10.0**, netstandard2.0/2.1, net462+ | à ajouter |
| [`MySql.Data`](https://www.nuget.org/packages/MySql.Data) | 26.7.0 (2026-07-29) | **GPL-2.0-only WITH Universal-FOSS-exception-1.0** | net8.0, net9.0, net10.0, netstandard2.0/2.1 | **à ne pas ajouter** |
| [`Microsoft.Data.Sqlite`](https://www.nuget.org/packages/Microsoft.Data.Sqlite) | **10.0.11** | **MIT** | métapaquet ; `Microsoft.Data.Sqlite.Core` 10.0.11 ne porte que des groupes `net8.0` et `netstandard2.0` — **compatible** net10.0, sans TFM dédié | à ajouter |

`Microsoft.Data.Sqlite` tire `SQLitePCLRaw.bundle_e_sqlite3` (binaire SQLite natif embarqué) et
`SQLitePCLRaw.core` 2.1.12. C'est ce qui rend `SQLitePCL.raw.sqlite3_interrupt` disponible sans
référence supplémentaire (§ 2.3), et ce qui garantit une version de SQLite récente quel que soit
l'hôte. Des préversions `11.0.0-preview.*` existent : ne pas les prendre.

**Npgsql : à épingler explicitement.** Il est déjà là, mais **transitivement**. Le dépôt utilise la
gestion centralisée des versions et pin déjà des transitifs (`SSH.NET`,
`System.Security.Cryptography.Xml`). Ajouter `<PackageVersion Include="Npgsql" Version="10.0.3" />`
et une `PackageReference` explicite dans `Infrastructure` : sans cela, la voie connectée dépendrait
d'un paquet qu'aucun fichier de projet ne nomme, et une montée de version d'EF Core la déplacerait
sans qu'on le voie.

**MySql.Data : écarté, et le motif est la licence.** GPL-2.0-only avec l'exception FOSS universelle
d'Oracle : l'exception n'affranchit le lien que pour des applications **elles-mêmes distribuées sous
une licence libre reconnue**. Pour un service qui n'est pas publié sous une telle licence, la GPL
s'applique par lien. MySqlConnector est MIT, « a clean-room reimplementation of the MySQL Protocol
[…] not based on Oracle's MySQL Connector/NET » ([mysqlconnector.net](https://mysqlconnector.net/)),
et fonctionne avec MariaDB comme avec MySQL. Le choix est sans hésitation.

Le texte de l'exception, chez Oracle, est explicite
([Universal FOSS Exception 1.0](https://oss.oracle.com/licenses/universal-foss-exception/)) :

> « Nothing in this additional permission grants any right to distribute any portion of the Software
> on terms other than those of the Software License or grants any additional permission […] for use
> or distribution of the Software in conjunction with software other than **Other FOSS**. »

L'exception ne joue que si l'application liée est **elle-même** sous une licence libre reconnue. Pour
un service qui ne l'est pas, il ne reste que la licence commerciale MySQL ou le renoncement.
⚠ **hors périmètre technique** : la question « la GPL se propage-t-elle à un service jamais
*distribué* (SaaS) ? » relève d'un avis juridique, pas d'une source technique. Le risque est signalé,
il n'est pas qualifié ici. Ce qui est vérifié, c'est que l'exception FOSS ne couvre pas le
propriétaire.

**Versions recommandées à épingler** : `Npgsql` 10.0.3, `MySqlConnector` 2.6.2,
`Microsoft.Data.Sqlite` 10.0.11.

**Testcontainers.** Le dépôt a `Testcontainers` 4.13.0 et `Testcontainers.PostgreSql` 4.13.0. Pour
éprouver les trois dialectes il faudra `Testcontainers.MariaDb` (ou `Testcontainers.MySql`) en 4.13.0 ;
SQLite n'a pas besoin de conteneur. ⚠ **non vérifié** : l'existence et la version exacte du module
MariaDb en 4.13.0.

### 2.2 Timeouts

**Npgsql** ([Connection String Parameters](https://www.npgsql.org/doc/connection-string-parameters.html)) :

| paramètre | défaut | description |
| --- | --- | --- |
| `Timeout` | **15 s** | « The time to wait (in seconds) while trying to establish a connection before terminating the attempt and generating an error. » |
| `Command Timeout` | **30 s** | « The time to wait (in seconds) while trying to execute a command before terminating the attempt and generating an error. Set to zero for infinity. » |
| `Cancellation Timeout` | **2000 ms** | « The time to wait (in milliseconds) while trying to read a response for a cancellation request for a timed out or cancelled query, before terminating the attempt and generating an error. -1 skips the wait, 0 means infinite wait. **Introduced in 5.0.** » Le code ajoute : après ce délai, Npgsql **rompt la connexion physique**. |
| `Keepalive` | 0 (désactivé) | secondes d'inactivité avant envoi d'un keepalive |
| `Internal Command Timeout` | -1 | **obsolète** — marqué `[Obsolete("…no longer needed and does nothing")]` dans le code. À ignorer. |

⚠ Un `TODO` subsiste dans `NpgsqlConnectionStringBuilder.cs` : « according to docs, we treat 0 timeout
as infinite, yet we do not change the actual value ». `Cancellation Timeout=0` ne se comporte donc
peut-être pas comme documenté — **ne pas l'employer**.

**MySqlConnector** ([Connection Options](https://mysqlconnector.net/connection-options/)) :

| option | défaut | description |
| --- | --- | --- |
| `Connection Timeout` | **15 s** | « The length of time (in seconds) to wait for a connection to the server before terminating the attempt » |
| `Default Command Timeout` | **30 s** | « The length of time (in seconds) each command can execute before the query is cancelled on the server » |
| `Cancellation Timeout` | **2 s** | « The length of time (in seconds) to wait for a query to be canceled when `MySqlCommand.CommandTimeout` expires » |
| `Pooling` | `true` | « When pooling is enabled, `MySqlConnection.Open`/`OpenAsync` retrieves an open connection from the pool » |

Les deux pilotes ont donc la **même** trame : 15 s à l'ouverture, 30 s par commande, 2 s pour obtenir
la confirmation d'annulation. C'est heureux : le service peut exposer un seul jeu de bornes à
l'`Operator`.

**Microsoft.Data.Sqlite** — et c'est un piège, pas un détail. Le mot-clé de chaîne de connexion
`Default Timeout` (alias `Command Timeout`, ajouté en 6.0) et `SqliteCommand.CommandTimeout` valent
**30 s** par défaut, mais la remarque de l'API est sans ambiguïté :

> « Gets or sets the number of seconds to wait before terminating the attempt to execute the command.
> Defaults to 30. […] **The timeout is used when the command is waiting to obtain a lock on the
> table.** »
> ([SqliteCommand.CommandTimeout](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite.sqlitecommand.commandtimeout))

Le code le confirme : c'est une boucle d'attente active autour de `sqlite3_prepare_v2` et de
`sqlite3_step`, qui ne se déclenche que sur `SQLITE_BUSY` et dort 150 ms entre deux essais
([`SqliteCommand.cs`](https://github.com/dotnet/efcore/blob/main/src/Microsoft.Data.Sqlite.Core/SqliteCommand.cs)).
**Une requête longue mais non bloquée — un balayage de table — n'est jamais interrompue par
`CommandTimeout`.** Il n'existe donc, sur SQLite, aucune borne de durée : c'est au service de la
poser.

### 2.3 Annulation — le point qui décide de l'architecture du scan

**Npgsql : honoré aux deux endroits, et vérifié dans le code source.**

- **`OpenAsync`** : `NpgsqlConnector.ConnectAsync` construit un
  `CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)` combinant le token de
  l'appelant et le `Timeout`, et appelle `ThrowIfCancellationRequested()` autour de l'ouverture du
  socket
  ([`NpgsqlConnector.cs`](https://github.com/npgsql/npgsql/blob/main/src/Npgsql/Internal/NpgsqlConnector.cs)).
- **`ExecuteReaderAsync` / `ReadAsync`** : oui, et l'implémentation est bien celle du protocole —
  `PerformPostgresCancellation()` **ouvre une seconde connexion physique** (`new NpgsqlConnector(this)`)
  et lui fait envoyer un `CancelRequest` portant le PID backend et la clé secrète. Le commentaire du
  code prévient : « This does not indicate whether the cancellation attempt was successful on the
  PostgreSQL side — only if the request was delivered. » Puis
  `PerformUserCancellationUnsynchronized` attend la réponse pendant `Cancellation Timeout` et, faute
  de réponse, **rompt la connexion physique**. Les notes de version 5.0 le disent en clair : « If
  PostgreSQL cancellation isn't successful within a short time window, the network is likely down.
  Npgsql forcibly closes the physical connection and raises an exception. »
  ([release notes 5.0](https://www.npgsql.org/doc/release-notes/5.0.html))

**Deux conséquences pratiques à retenir** : l'annulation coûte une **connexion supplémentaire** — le
compte de connexion doit pouvoir en ouvrir deux, ce qui interdit un `Maximum Pool Size=1` — et elle
lève `OperationCanceledException` sur annulation par token, `NpgsqlException` encapsulant une
`TimeoutException` sur `Command Timeout`.

**MySqlConnector : honoré, par `KILL QUERY` sur une seconde connexion.** La page dédiée
([Command Cancellation](https://mysqlconnector.net/overview/command-cancellation/)) est explicite :
la *soft cancellation* envoie un `KILL QUERY` au serveur, et « because the MySQL protocol doesn't
allow multiplexing commands, the `KILL QUERY` command **must be sent over a different network
connection** to the same MySQL Server ». Si elle n'aboutit pas dans `Cancellation Timeout`, le pilote
bascule en *hard cancellation* : fermeture du socket, la connexion devient inutilisable.

Deux réserves documentées, l'une et l'autre pertinentes ici :

- « **Using a proxy or a Layer 4 load balancer may interfere with the ability to send the `KILL QUERY`
  command to the same server and prevent cancellation from occurring.** » Une base cliente derrière
  ProxySQL, RDS Proxy ou un répartiteur TCP peut donc être **inannulable** en douceur.
- « the timeout is reset at the beginning of each call to a public API method » : `CommandTimeout`
  borne **chaque appel** (`ExecuteReaderAsync`, chaque `ReadAsync`), pas la durée totale de lecture
  d'un reader. Une borne de durée globale du scan doit donc venir du service, pas du pilote.

**MySql.Data : écarté, et cette fois sur sources Oracle.** Contre toute attente, le tracker officiel
documente le problème :

- [Bug #70111](https://bugs.mysql.com/bug.php?id=70111) « asynchronous functions not asynchronous »,
  ouvert en 2013, **corrigé seulement en Connector/NET 8.0.33** (janvier 2023) : « Asynchronous
  methods used with classic MySQL protocol connections now are implemented to ensure asynchronous
  behavior at the level of I/O operations. **Previously, some asynchronous methods were executed in a
  synchronous context.** »
- [Bug #94760](https://bugs.mysql.com/bug.php?id=94760) « `OpenAsync(CancellationToken)` doesn't use
  the token » — statut **Verified**, c'est-à-dire reconnu par Oracle et non marqué corrigé.
- [#110790](https://bugs.mysql.com/bug.php?id=110790) « `ExecuteReaderAsync` hangs instead of
  cancelling query after `CommandTimeout` » et
  [#110791](https://bugs.mysql.com/bug.php?id=110791) « `OpenAsync(CancellationToken)` doesn't throw
  for cancelled token ».

⚠ **non vérifié** : le statut *actuel* de #94760 / #110790 / #110791 contre la version 26.7.0. Sans
importance ici : le paquet est déjà écarté sur sa licence, et son historique d'annulation suffirait à
le faire écarter une seconde fois.

**Microsoft.Data.Sqlite : pas d'async du tout, et `Cancel()` ne fait rien.** Deux citations
décisives, toutes deux de learn.microsoft.com :

> « **SQLite doesn't support asynchronous I/O. Async ADO.NET methods will execute synchronously in
> Microsoft.Data.Sqlite. Avoid calling them.** »
> ([Async limitations](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async))

> `SqliteCommand.Cancel()` : « Attempts to cancel the execution of the command. **Does nothing.** »
> ([API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite.sqlitecommand.cancel))

Donc, sur SQLite : `OpenAsync`, `ExecuteReaderAsync` et `ReadAsync` s'exécutent **de façon
synchrone**, bloquent le fil appelant, et le `CancellationToken` n'est vérifié — au mieux — qu'à
l'entrée. Un scan SQLite lancé sur le fil d'une requête ASP.NET bloquerait un fil du pool pendant
toute sa durée, et l'écran d'attente en `<meta refresh>` de la contrainte 9 ne serait pas servi.

Le code le confirme au mot près
([`SqliteCommand.cs`](https://github.com/dotnet/efcore/blob/main/src/Microsoft.Data.Sqlite.Core/SqliteCommand.cs)) :

```csharp
public new virtual Task<SqliteDataReader> ExecuteReaderAsync(
    CommandBehavior behavior, CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();
    return Task.FromResult(ExecuteReader(behavior));
}
```

Le token n'est vérifié **qu'une fois, avant** l'exécution ; `ExecuteReader` bloque ensuite jusqu'au
bout. Et `SqliteDataReader` **ne surcharge aucune méthode `*Async`** : `ReadAsync`, `NextResultAsync`
et `GetFieldValueAsync` tombent sur l'implémentation par défaut de `DbDataReader`, qui exécute le
pendant synchrone et rend une `Task` déjà complétée. Aucune occurrence de `interrupt` ni de
`progress_handler` dans `SqliteCommand.cs` ou `SqliteConnection.cs` : **le pilote n'expose aucun
mécanisme d'interruption dans son API publique.**

**La façon documentée d'interrompre** est `sqlite3_interrupt()`
([c3ref/interrupt](https://sqlite.org/c3ref/interrupt.html)) : « **It is safe to call this routine
from a thread different from the thread that is currently running the database operation.** » et « An
SQL operation that is interrupted will return `SQLITE_INTERRUPT` ». Microsoft.Data.Sqlite l'expose
indirectement : `SqliteConnection.Handle` rend un `SQLitePCL.sqlite3`
([API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlite.sqliteconnection.handle)),
que l'on passe à `SQLitePCL.raw.sqlite3_interrupt`.

**Ce qu'il faut donc construire pour SQLite :**

1. exécuter tout le scan SQLite sur un fil dédié (`Task.Run` ou un `BackgroundService`), jamais sur le
   fil d'une requête HTTP ;
2. enregistrer l'annulation : `using var reg = token.Register(() => raw.sqlite3_interrupt(connection.Handle));`
   — en veillant à ce que la connexion ne soit pas fermée pendant l'appel (la doc l'exige) ;
3. traduire `SQLITE_INTERRUPT` en `OperationCanceledException` pour que le reste du code ne connaisse
   qu'un seul vocabulaire d'annulation.

Palliatif à granularité grossière, si l'on veut éviter le P/Invoke : découper le scan (par table, ce
qu'on fait déjà) et tester le token **entre** les requêtes. L'interruption est alors au grain de la
table, ce qui est probablement acceptable — une requête `LIMIT 5` sur une table SQLite locale est
courte. C'est le compromis à considérer en premier ; le `sqlite3_interrupt` est le filet pour la
table pathologique.

⚠ **non vérifié** : Microsoft ne documente **nulle part** l'usage de `sqlite3_interrupt` via
`SqliteConnection.Handle`. Le montage repose sur deux faits vérifiés (le `Handle` est public et typé
`SQLitePCL.sqlite3` ; `SQLitePCLRaw.core` est une dépendance du paquet), pas sur une recommandation
de l'éditeur. Le comportement exact de `SqliteException` sur `SQLITE_INTERRUPT` n'est pas vérifié non
plus. **À prototyper avant de s'y engager.**

**Sur PostgreSQL et MySQL, la même règle vaut pour une autre raison** : le scan doit tourner hors du
fil de la requête HTTP parce qu'il dure des dizaines de secondes et que la contrainte 10 le veut
asynchrone. Mais là, l'`await` rend le fil, et l'annulation est réelle.

### 2.4 Pooling et ouverture

| pilote | défauts | `OpenAsync` bloque-t-il sur pool épuisé ? |
| --- | --- | --- |
| Npgsql | `Pooling=true`, `Maximum Pool Size=100`, `Connection Idle Lifetime=300 s` | **oui**, jusqu'à `Timeout` (15 s), puis `NpgsqlException` : « The connection pool has been exhausted, either raise 'Max Pool Size' […] or 'Timeout' […] », avec une `TimeoutException` en cause interne (vérifié dans `PoolingDataSource.cs`) |
| MySqlConnector | `Pooling=true`, `MaximumPoolSize=100`, `ConnectionIdleTimeout=180 s` | **oui**, borné par `ConnectionTimeout` (15 s). ⚠ type et message d'exception non vérifiés |
| Microsoft.Data.Sqlite | `Pooling=true` (mot-clé ajouté en 6.0) | **non** — aucun mot-clé de taille maximale de pool n'est documenté, donc ni file d'attente ni épuisement. ⚠ déduit de la liste des mots-clés documentés, pas d'une affirmation |

**Le pooling est indexé par chaîne de connexion.** Pour un service qui se connecte à des bases
**clientes** arbitraires, cela veut dire : N bases scannées → N pools de 100 connexions maximum,
chacun gardant ses connexions oisives 3 à 5 minutes vers un serveur tiers. Deux conséquences :

- **désactiver le pooling pour les connexions de scan** (`Pooling=false`), ou du moins n'en garder
  aucune ouverte après le scan. La contrainte 2 dit que la chaîne de connexion ne survit pas au scan ;
  une connexion **poolée** survivrait, elle, dans le pool — avec sa chaîne comme clé de pool. C'est
  une fuite discrète de la promesse « aucun secret d'accès durable », et elle mérite d'être tranchée
  explicitement ;
- prévoir que Npgsql aura besoin d'**une seconde connexion** pour émettre le `CancelRequest` : avec
  `Pooling=false`, cela reste possible, mais le compte doit pouvoir ouvrir deux connexions.

### 2.5 Batch et multi-résultats

**Npgsql** ([Basic Usage](https://www.npgsql.org/doc/basic-usage.html)) : `NpgsqlBatch` est la voie
recommandée — « An `NpgsqlBatch` simply contains a list of `NpgsqlBatchCommands` […] **All statements
and parameters are efficiently packed into a single packet — when possible — and sent to
PostgreSQL.** » Le multi-instruction par point-virgule dans un seul `NpgsqlCommand` marche encore,
mais est déconseillé : « legacy batching is generally discouraged since it isn't natively supported by
PostgreSQL, **forcing Npgsql to parse the SQL to find semicolons** » — analyse côté client qui coûte
et se comporte mal sur du SQL exotique (dollar-quoting, littéraux contenant `;`). En outre, toutes les
instructions y partagent une seule collection `Parameters`, là où `NpgsqlBatch` donne un jeu par
instruction.

⚠ **Le piège qui décide du § 6.2, et il est documenté** : « If you haven't started an explicit
transaction with `BeginTransaction()`, a batch is automatically wrapped in an implicit transaction.
That is, **if a statement within the batch fails, all later statements are skipped and the entire
batch is rolled back.** » Un lot de 300 prélèvements dont un échoue ne rend donc **aucun** aperçu.

**MySqlConnector** : `MySqlBatch` existe (implémentation de `DbBatch`), avec `NextResultAsync()` — la
doc la signale **expérimentale et susceptible de changer**, et note que le gain réel est surtout sur
**MariaDB ≥ 10.2**. Le multi-instruction par `;` est, lui, **toujours disponible sans option** :
`MySqlConnector` demande inconditionnellement `CLIENT_MULTI_STATEMENTS` et `CLIENT_MULTI_RESULTS` dans
son handshake
([`HandshakeResponse41Payload.cs`](https://github.com/mysql-net/MySqlConnector/blob/master/src/MySqlConnector/Protocol/Payloads/HandshakeResponse41Payload.cs)),
d'où la mention « MySqlConnector always allows batch statements » dans la liste des options
d'Oracle non reprises.

⚠ **Corollaire de sécurité à ne pas laisser passer** : le multi-instruction étant **toujours actif**,
une injection sur ce pilote permet d'empiler des instructions. Or le scan construit ses requêtes de
prélèvement en **interpolant des noms de schéma, de table et de colonne** relevés dans le catalogue du
client — qui ne sont pas paramétrables et qui viennent d'une base tierce. Le quoting d'identifiants
doit être strict et éprouvé, table par table.
Deux options à ne pas confondre avec cela : `Allow User Variables` (défaut `false`) concerne les
variables `@nom` — inutile ici, et à `false` un `@nom` est lu comme un paramètre, ce qui est ce qu'on
veut. `AllowLoadLocalInfile` (défaut `false`) doit **impérativement rester à `false`** : à `true`, un
serveur MySQL hostile ou compromis peut demander au client de lui envoyer des fichiers arbitraires du
système de fichiers du microservice. Pour un service qui se connecte à des bases tierces sur simple
saisie d'une chaîne, ce n'est pas négociable.

**Microsoft.Data.Sqlite** ([Batching](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/batching)) :
le batching est émulé, et sa sémantique est contre-intuitive — « When calling `DbCommand.ExecuteReader`,
statements are executed **up to the first one that returns results**. Calling `DbDataReader.NextResult`
**continues executing** statements […]. Calling `DbDataReader.Dispose` or `Close` **executes any
remaining statements that haven't been consumed by `NextResult()`**. » Autrement dit, abandonner un
reader n'annule pas le reste du lot : le `Dispose` l'exécute. Pour un scan annulable, cela tranche :
**une instruction par commande côté SQLite**, jamais de `;`. Sans coût, la base étant locale.

---

## 3. Les droits minimaux, et ce que voit un compte sans droits sur les données

C'est le point où la contrainte 6 se joue, et les trois dialectes n'y répondent pas pareil.

### 3.1 PostgreSQL — deux voies, une seule tenable

**`information_schema` est filtré par privilège, à la ligne près.** Verbatim :

> « **Only those tables and views are shown that the current user has access to (by way of being the
> owner or having some privilege).** »
> ([35.53 tables](https://www.postgresql.org/docs/17/infoschema-tables.html))

> « **Only those columns are shown that the current user has access to (by way of being the owner or
> having some privilege).** »
> ([35.17 columns](https://www.postgresql.org/docs/17/infoschema-columns.html))

**C'est disqualifiant pour la contrainte 6.** Un compte à droits partiels rendrait un relevé
**complet en apparence** — en-tête correct, ligne de fin cohérente, aucun trou de position, puisque
les tables absentes sont absentes *proprement*. Les neuf contrôles d'intégrité **ne l'attraperaient
pas** : ils vérifient la cohérence du pivot avec lui-même, pas sa complétude par rapport à la base.
C'est exactement l'`Omission silencieuse` que ce contexte existe pour ne pas avoir, déplacée d'un cran
en amont.

**`pg_catalog` n'est pas filtré.** La preuve primaire la plus nette est dans la description du
privilège `USAGE` sur un schéma ([5.7 Privileges](https://www.postgresql.org/docs/17/ddl-priv.html)) :

> « For schemas, allows access to objects contained in the schema […]. **Without this permission, it
> is still possible to see the object names, e.g., by querying system catalogs.** »

Confirmé *a contrario* par [`pg_authid`](https://www.postgresql.org/docs/17/catalog-pg-authid.html) :
« **Since this catalog contains passwords, it must not be publicly readable.** `pg_roles` is a publicly
readable view on `pg_authid` […] » — la restriction est signalée comme l'exception, ce qui suppose la
lisibilité comme règle.
⚠ **non vérifié** : aucune page ne déclare en toutes lettres que tous les catalogues de `pg_catalog`
sont lisibles par `PUBLIC`. Les catalogues réputés restreints (`pg_authid`, `pg_statistic`,
`pg_subscription`, `pg_user_mapping`) n'entrent pas dans la requête du § 1.1, donc sans effet ici —
mais **c'est le fait le plus structurant de ce rapport et il mérite d'être éprouvé sur conteneur**
avant d'être écrit dans une spec : créer un rôle sans aucun privilège objet et vérifier qu'il relève
bien 100 % des colonnes par `pg_catalog` et 0 % par `information_schema`.

**Privilèges à demander :**

- « schéma seulement » : `CREATE ROLE svc LOGIN PASSWORD '…'; GRANT CONNECT ON DATABASE cible TO svc;`
  et **rien d'autre**. `CONNECT` est accordé à `PUBLIC` par défaut, mais beaucoup d'installations
  durcies le révoquent ; le demander explicitement. `USAGE` sur les schémas n'est **pas** nécessaire
  pour lire `pg_catalog`.
- « schéma + aperçu » : ajouter `GRANT pg_read_all_data TO svc;` — rôle prédéfini décrit comme « Read
  all data (tables, views, sequences), as if having `SELECT` rights on those objects, and `USAGE`
  rights on all schemas, even without having it explicitly »
  ([22.5 Predefined Roles](https://www.postgresql.org/docs/17/predefined-roles.html)). Avantage
  décisif : il couvre les tables **futures**, donc aucun entretien.
  ⚠ **Piège documenté à dire à l'écran** : « This role does not have the role attribute `BYPASSRLS`
  set. If RLS is being used, an administrator may wish to set `BYPASSRLS` on roles which this role is
  GRANTed to. » Sous *row-level security*, un aperçu peut donc revenir **vide sans erreur**. C'est
  exactement le cas que le glossaire interdit de laisser muet : un aperçu vide est indiscernable d'une
  colonne vide. Il faut soit distinguer « 0 ligne lue » de « colonne vide », soit nommer la raison.

`pg_monitor` est hors sujet (statistiques, pas structure) : ne pas le demander, ce serait du privilège
gratuit.

### 3.2 MySQL / MariaDB — filtré, et sans échappatoire

> « For most `INFORMATION_SCHEMA` tables, each MySQL user has the right to access them, but **can see
> only the rows in the tables that correspond to objects for which the user has the proper access
> privileges**. » et « **In either case, you must have some privilege on an object to see information
> about it.** »
> ([28.1 INFORMATION_SCHEMA introduction](https://dev.mysql.com/doc/refman/8.4/en/information-schema-introduction.html))

Et le filtrage **descend au niveau colonne** : « `SHOW COLUMNS` displays information only for those
columns for which you have some privilege. »
([SHOW COLUMNS](https://dev.mysql.com/doc/refman/8.4/en/show-columns.html)), la doc précisant par
ailleurs que « the same privileges apply to selecting information from `INFORMATION_SCHEMA` and
viewing the same information through `SHOW` statements ».
⚠ **inférence forte, non littérale** : un `GRANT SELECT (col_a) ON base.t` rendrait donc un
`information_schema.COLUMNS` **amputé** des autres colonnes de `t`. La doc de `I_S.COLUMNS` ne le dit
pas ; c'est la combinaison des deux phrases. À éprouver — mais la conclusion pratique ne change pas :
**proscrire les GRANT colonne par colonne**, ils produisent un relevé faux et muet.

**Il n'existe pas de privilège « voir la structure ».** `SHOW VIEW` est grantable au niveau « Views »
seulement, donc inapplicable aux tables de base
([Privileges Provided](https://dev.mysql.com/doc/refman/8.4/en/privileges-provided.html)).
`REFERENCES` est le seul candidat théorique (il ne donne aucune lecture de données) mais ⚠ **non
vérifié** qu'il suffise à faire apparaître une table dans `I_S` — et **MariaDB le documente comme
« Unused »** ([GRANT, MariaDB](https://mariadb.com/docs/server/reference/sql-statements/account-management-sql-statements/grant)),
donc il n'y faut pas compter. `PROCESS` ne concerne que les tables `INNODB_*` : hors sujet.

**Le minimum fiable est donc `GRANT SELECT ON base.* TO 'svc'@'…';`** — au niveau **base**, jamais
table par table. C'est un compromis imposé par le moteur, pas un choix : sur MySQL/MariaDB, le service
ne *peut pas* relever le schéma entier sans obtenir aussi la lecture des données. Cela doit être dit à
l'`Operator` en toutes lettres, et c'est une différence de fond avec PostgreSQL qu'il faut rendre
lisible à l'écran de saisie de la connexion.

Phrase proposée pour la documentation client :
> « Le moteur MySQL/MariaDB ne propose aucun privilège permettant de lire la structure sans lire les
> données. Le minimum technique est `SELECT` au niveau de la base. Un `SELECT` restreint à certaines
> tables ou colonnes produirait un relevé incomplet **sans nous en avertir**, ce qui est incompatible
> avec un usage de conformité. »

### 3.3 SQLite — pas de droits SQL, seulement le système de fichiers

Trois leviers, tous documentés :

**Permissions fichier *et* répertoire.** Le répertoire compte à cause du WAL.
[wal.html](https://www.sqlite.org/wal.html) : « It is not possible to open read-only WAL databases.
The opening process must have **write privileges for "-shm" wal-index shared memory file** associated
with the database, if that file exists, **or else write access on the directory** containing the
database file if the "-shm" file does not exist. » Depuis **3.22.0** la contrainte est relâchée : une
base WAL s'ouvre en lecture seule si (1) les `-shm`/`-wal` existent et sont lisibles, **ou** (2) le
répertoire est inscriptible, **ou** (3) la connexion emploie `immutable`.

**Le mode de panne concret à afficher à l'`Operator`** : base en `journal_mode=WAL`, répertoire non
inscriptible, fichiers `-shm`/`-wal` absents parce que la base a été proprement fermée. L'ouverture
échoue, **même en lecture seule**. C'est un cas fréquent (montage en lecture seule, volume Docker
`:ro`) et son message d'erreur brut n'aide personne.

**Lecture seule par URI** : `file:/chemin/base.sqlite?mode=ro`
([uri.html](https://www.sqlite.org/uri.html)) — « The **mode** query parameter determines if the new
database is opened read-only, read-write, read-write and created if it does not exist, or […] pure
in-memory ». Côté .NET, `Microsoft.Data.Sqlite` expose `Mode=ReadOnly` dans la chaîne de connexion,
ce qui évite de manipuler l'URI. ⚠ **non vérifié à la source** (hors périmètre de la doc SQLite).

**`immutable=1`** : dernier recours, et **dangereux**. « If this query parameter […] asserts that a
database file is immutable **and that file changes anyhow, then SQLite might return incorrect query
results and/or `SQLITE_CORRUPT` errors** ». À n'employer que sur une copie dont on garantit
l'immobilité — donc probablement jamais, la contrainte 11 disant que le fichier doit être atteignable
par le service, pas copié dans le service.

### 3.4 Tableau récapitulatif

| dialecte | « schéma seulement » | « schéma + aperçu » | ce que voit un compte catalogue sans données |
| --- | --- | --- | --- |
| **PostgreSQL 16/17** | `GRANT CONNECT ON DATABASE cible TO svc;` — rien d'autre | + `GRANT pg_read_all_data TO svc;` (couvre les tables futures ; ⚠ pas de `BYPASSRLS`) | **schéma entier** par `pg_catalog` ; **schéma amputé et muet** par `information_schema` |
| **MySQL 8.x** | aucun privilège documenté ne le permet ; `REFERENCES ON base.*` ⚠ à éprouver | `GRANT SELECT ON base.* TO 'svc'@'…';` (+ `SHOW VIEW` si vues) | **schéma amputé et muet**, jusqu'au niveau colonne |
| **MariaDB 11.x** | idem, et `REFERENCES` y est « Unused » → sans espoir | `GRANT SELECT ON base.* TO 'svc'@'…';` | idem |
| **SQLite 3.4x** | lecture sur le fichier ; si WAL, `-wal`/`-shm` lisibles **ou** répertoire inscriptible | identique — SQLite ne sépare pas structure et données | sans objet |

**Conséquence à porter dans la spec** : la contrainte 6 (« schéma entier ou rien ») est **tenable sur
PostgreSQL** par le seul choix de `pg_catalog`, et **indémontrable sur MySQL/MariaDB** : le service ne
peut jamais savoir qu'il a tout vu. Il ne peut que documenter le privilège requis et faire confiance.
C'est une asymétrie réelle entre les dialectes, et il vaut mieux l'écrire que la découvrir.

---

## 4. La troncature côté SGBD (contrainte 5)

### 4.1 PostgreSQL — le cast est obligatoire

`left(string text, n integer) → text` — « Returns first `n` characters in the string, or when `n` is
negative, returns all but last |`n`| characters. »
([9.4 String Functions](https://www.postgresql.org/docs/17/functions-string.html)).

**Sur un type non textuel, ça lève.** La doc de résolution de fonctions
([10.3](https://www.postgresql.org/docs/17/typeconv-func.html)) donne l'exemple à la lettre :

```
SELECT substr(1234, 3);
ERROR:  function substr(integer, integer) does not exist
```
« This does not work because `integer` does not have an implicit cast to `text`. An explicit cast will
work, however: `substr(CAST (1234 AS text), 3)` ». Donc `left(col, 64)` sur un `integer`, une `date`,
un `uuid`, un `numeric`, un `boolean` ou un `jsonb` **échoue**. La forme non robuste est à proscrire.

**`::text` fonctionne sur tout type**, via les conversions d'entrée/sortie automatiques
([CREATE CAST](https://www.postgresql.org/docs/17/sql-createcast.html) : « PostgreSQL provides
automatic I/O conversion casts […] The automatic casts **to** string types are treated as assignment
casts »). Ce qu'il produit : `bytea::text` → la représentation hexadécimale `\xdeadbeef` (paramètre
`bytea_output`, défaut `hex`,
[8.4](https://www.postgresql.org/docs/17/datatype-binary.html)) ; `enum::text` → le libellé ;
`array::text` → `{a,b}` ; `composite::text` → `(a,b)` ; `json`/`jsonb::text` → le document sérialisé.

**Négatif** : `left(col, -3)` ne lève pas, il **retire les 3 derniers caractères**. Un N mal calculé
ne casse pas la requête, il change de sens en silence : valider `N > 0` côté service.

**NULL** : `left(NULL, 64)` rend NULL. ⚠ **non vérifié à la lettre** sur la page des fonctions ; c'est
le comportement STRICT usuel. Le service doit de toute façon distinguer « valeur NULL » de « chaîne
vide » à l'écran, sans quoi il réintroduit une ambiguïté que le glossaire refuse ailleurs.

**Caractères, pas octets**, et l'encodage est validé par le serveur (« will check that incoming data is
valid for that encoding », [24.3](https://www.postgresql.org/docs/17/multibyte.html)). Aucun risque de
couper un point de code en deux **sur les types texte**. Le risque n'apparaît que si l'on tronque du
`bytea` — d'où l'exclusion.

> **Forme retenue : `left(col::text, 64)`.**

### 4.2 MariaDB / MySQL — cast implicite, mais attention au charset

`LEFT(str, len)` : « Returns the leftmost `len` characters from the string `str`, or `NULL` if any
argument is `NULL` » + « **This function is multibyte safe.** »
([MySQL 8.4 String Functions](https://dev.mysql.com/doc/refman/8.4/en/string-functions.html)).
⚠ **non vérifié côté MariaDB** : la mention « multibyte safe » n'apparaît pas sur les pages MariaDB
correspondantes. Le comportement est aligné en pratique ; la garantie n'est pas écrite.

**Cast implicite documenté** : « MySQL automatically converts strings to numbers as necessary, and
vice versa » ; « Implicit conversion of a numeric or temporal value to string produces a value that
has a character set and collation determined by the `character_set_connection` and
`collation_connection` system variables »
([Type Conversion](https://dev.mysql.com/doc/refman/8.4/en/type-conversion.html)). Donc
`LEFT(int_col, 64)` marche — mais le résultat dépend de la connexion. **Fixer
`character_set_connection` à `utf8mb4` à l'ouverture** fait partie de la troncature correcte.

**Sur une colonne binaire, `LEFT()` compte des octets.** Les `BINARY`/`VARBINARY` « store byte strings
rather than character strings. This means they have the `binary` character set and collation »
([doc](https://dev.mysql.com/doc/refman/8.4/en/binary-varbinary.html)), et pour ce jeu de caractères
« **For multibyte characters stored as binary strings, character and byte boundaries differ.
Character boundaries are lost** »
([binary charset](https://dev.mysql.com/doc/refman/8.4/en/charset-binary-set.html)). Le « multibyte
safe » de `LEFT()` est donc **vide de sens sur un BLOB** : la troncature tombe au milieu d'un point de
code et produit de l'UTF-8 invalide.

**`CAST(col AS CHAR(64))`** est à la fois cast et troncature : « If the optional length `N` is given,
`CHAR(N)` causes the cast to use **no more than N characters** of the argument. No padding occurs »
([CAST functions](https://dev.mysql.com/doc/refman/8.4/en/cast-functions.html)).
`CONVERT(col USING utf8mb4)` transcode explicitement — la doc le recommande pour traiter une chaîne
binaire comme du texte.

> **Formes retenues : `LEFT(CONVERT(col USING utf8mb4), 64)`** pour `CHAR/VARCHAR/TEXT/JSON`, et
> **`CAST(col AS CHAR(64))`** pour tout le reste (numérique, date, `ENUM`, `YEAR`). Les colonnes
> binaires sont exclues en amont, ce qui neutralise le risque d'UTF-8 invalide.

### 4.3 SQLite — filtrer à la valeur

`substr(X,Y,Z)` : « **If X is a BLOB then the indices count bytes.** » ;
`typeof(X)` rend « 'null', 'integer', 'real', 'text', or 'blob' » ; `length(X)` sur un blob « returns
the number of bytes » ([Core Functions](https://sqlite.org/lang_corefunc.html)).

`CAST(X AS TEXT)` : « **When casting a BLOB value to TEXT, the sequence of bytes that make up the BLOB
is interpreted as text encoded using the database encoding.** »
([CAST expressions](https://sqlite.org/lang_expr.html)) — **aucune validation**. C'est le risque
d'UTF-8 invalide le plus net des trois dialectes.

⚠ **non vérifié à la lettre** : le comportement de `substr()` sur un INTEGER/REAL (conversion usuelle
en texte) et sur NULL n'est pas énoncé sur la page des fonctions ; le `CAST` explicite lève
l'ambiguïté.

> **Forme retenue :**
> ```sql
> CASE WHEN typeof(col) = 'blob' THEN NULL
>      ELSE substr(CAST(col AS TEXT), 1, 64) END
> ```
> Le `CASE` fait double emploi : il exclut le binaire **à la valeur** (le typage étant dynamique,
> c'est le seul filtre sûr) et il garantit que `CAST … AS TEXT` ne s'applique jamais à des octets
> bruts.

⚠ **Et il crée un problème que la spec doit trancher** : un NULL produit par le `CASE` est
indiscernable d'un vrai NULL. Or le glossaire exige qu'un aperçu soit « soit des valeurs, soit une
raison nommée », jamais une absence muette. Il faut donc **remonter aussi `typeof(col)`** par valeur
prélevée, pour que l'écran puisse dire « 2 des 5 valeurs sont binaires, non affichées » au lieu
d'afficher des vides. Sans ce second champ, la voie connectée réintroduit l'`Omission silencieuse` à
l'échelle de la valeur.

### 4.4 Récapitulatif

| dialecte | forme retenue | compte | risque UTF-8 invalide |
| --- | --- | --- | --- |
| PostgreSQL 16/17 | `left(col::text, 64)` | caractères | nul sur texte (encodage validé par le serveur) |
| MariaDB 11 / MySQL 8 | `LEFT(CONVERT(col USING utf8mb4), 64)` / `CAST(col AS CHAR(64))` | caractères, **octets** sur charset `binary` | réel si l'on tronque du binaire → exclure |
| SQLite 3.4x | `CASE WHEN typeof(col)='blob' THEN NULL ELSE substr(CAST(col AS TEXT),1,64) END` | caractères sur TEXT, **octets** sur BLOB | réel : `CAST(blob AS TEXT)` ne valide rien → filtrer par `typeof()` |

---

## 5. Les types binaires et exotiques : exclure ou tronquer

Rappel de la clause : l'exclusion doit être **nommée** sur la ligne — « valeurs non prélevées — type
binaire » — et jamais confondue avec « rien vu ». Le glossaire distingue deux raisons de nature
différente : **exclu** (décidé d'avance, sur le type) et **échoué** (droits, délai, panne). Un type
binaire relève du premier.

### 5.1 PostgreSQL — détecter par catégorie, pas par nom

[`pg_type`](https://www.postgresql.org/docs/17/catalog-pg-type.html) donne deux colonnes de décision :
`typtype` (`b` base, `c` composite, `d` domaine, `e` enum, `p` pseudo, `r` range, `m` multirange) et
`typcategory` (`A` Array, `B` Boolean, `C` Composite, `D` Date/time, `E` Enum, `G` Geometric,
`I` Network address, `N` Numeric, `P` Pseudo, `R` Range, `S` String, `T` Timespan, `U` User-defined,
`V` Bit-string, `X` unknown, `Z` Internal).

Résoudre d'abord les **domaines** (`typtype = 'd'` → suivre `typbasetype`).

| critère | décision | motif |
| --- | --- | --- |
| `typcategory` ∈ {`S`} — `text`, `varchar`, `char` | **tronquer** | la cible |
| `typcategory` ∈ {`N`,`B`,`D`,`T`} + `uuid` | tronquer (inoffensif) | sortie courte et lisible |
| `typcategory = 'E'` (enum) | tronquer | libellé lisible |
| `typcategory = 'I'` — `inet`, `cidr`, `macaddr` | tronquer | court ; **et souvent donnée personnelle**, donc à prélever |
| `typcategory = 'A'` (tableaux) | **exclure** | volume non borné ; `{…}` tronqué est un littéral cassé, trompeur |
| `typcategory = 'C'` / `typtype = 'c'` | **exclure** | structure imbriquée, illisible tronquée |
| `typcategory = 'G'` — `point`, `line`, `lseg`, `box`, `path`, `polygon`, `circle` | **exclure** | aucune lecture humaine utile, `path`/`polygon` non bornés |
| `typcategory = 'V'` — `bit`, `bit varying` | **exclure** | flux de bits |
| `typcategory` ∈ {`P`,`Z`,`X`} | **exclure** | garde-fou ; ne devrait pas apparaître comme type de colonne |
| `bytea` | **exclure** | `::text` rend `\x…`, deux caractères par octet : illisible et volumineux |
| `json` / `jsonb` | **exclure** (ou tronquer en le nommant « fragment ») | jusqu'à 1 Go ; un JSON tronqué n'est plus du JSON |
| `xml` | **exclure** | idem |
| `tsvector` / `tsquery` | **exclure** | représentation interne de recherche |
| `oid` de large object, type `lo` | **exclure** | ne porte qu'un identifiant, la donnée est ailleurs |
| `pg_lsn` | **exclure** | interne au moteur |
| PostGIS `geometry`, `geography`, `raster` | **exclure** | sortie EWKB hexadécimal, volume non borné |

⚠ **non vérifié** : la doc donne la **table des codes** de `typcategory`, pas l'affectation type par
type. Que `bytea`, `uuid`, `json`, `xml` valent bien `'U'` est une déduction, à confirmer par un
`SELECT typname, typtype, typcategory FROM pg_type` sur une instance de référence. Conséquence
pratique : la catégorie `'U'` étant un fourre-tout, il faut une **liste blanche par `typname` à
l'intérieur de `'U'`** (`uuid` dedans, `bytea` dehors), pas une règle par catégorie seule.

**Détecter PostGIS sans dépendre de l'extension** : ne jamais coder d'OID en dur (ils diffèrent par
base) ni supposer le schéma. Le critère portable est le **nom** :
`t.typname IN ('geometry','geography','raster','box2d','box3d')`. Si l'extension est absente, le
prédicat ne matche rien — aucune dépendance créée.

### 5.2 MariaDB / MySQL — décider sur `DATA_TYPE`

| `DATA_TYPE` | décision | motif |
| --- | --- | --- |
| `char`, `varchar` | tronquer | la cible |
| `tinytext`, `text`, `mediumtext`, `longtext` | **tronquer** (obligatoire) | jusqu'à 4 Go, jamais rapatrier en entier |
| entiers, `decimal`, `float`, `double`, dates, `year` | tronquer (inoffensif) | cast implicite documenté |
| `enum`, `set` | tronquer | chaînes lisibles |
| `binary`, `varbinary` | **exclure** | charset `binary`, octets bruts |
| `tinyblob`, `blob`, `mediumblob`, `longblob` | **exclure** | idem + volume |
| `bit` | **exclure** | flux de bits |
| `json` | **exclure** (ou tronquer en le nommant) | un fragment n'est plus du JSON |
| types spatiaux (`geometry`, `point`, `polygon`, …) | **exclure** | « Internally, MySQL stores geometry values in a format that is not identical to either WKT or WKB […] like WKB but with an initial 4 bytes to indicate the SRID » : un `SELECT` direct rend du binaire. Lisible seulement via `ST_AsText()`, hors périmètre d'un prélèvement générique |
| `uuid` (MariaDB 10.7+) | **tronquer** | « Data retrieved by this type is presented as a string […] defined in RFC4122 » |
| `inet4` / `inet6` (MariaDB 10.5+) | **tronquer** | stocké en `BINARY(16)` mais « Clients see `INET6` as `CHAR(39)` and get text representation on retrieval ». **Contre-exemple utile : le stockage binaire n'implique pas l'exclusion.** |

**`CHARACTER_SET_NAME IS NULL` comme marqueur de colonne binaire** : vérifié **chez MariaDB** —
« Character set if a non-binary string data type, **otherwise NULL** »
([MariaDB KB](https://mariadb.com/kb/en/information-schema-columns-table/)). ⚠ **non confirmé chez
MySQL**, dont la page se borne à « For character string columns, the character set name ». Donc :
**décider sur `DATA_TYPE` comme critère principal**, `CHARACTER_SET_NAME IS NULL` seulement comme
filet de sécurité.

`uuid`, `inet4` et `inet6` **n'existent pas en MySQL 8.x** ; les types spatiaux existent des deux
côtés. La liste doit être l'union, appliquée selon le serveur réellement joint.

### 5.3 SQLite — l'affinité ne suffit pas

Le type déclaré est arbitraire. Les cinq règles d'affinité
([Datatypes In SQLite, §3.1](https://sqlite.org/datatype3.html)) :

1. type contenant `'INT'` → INTEGER ;
2. contenant `'CHAR'`, `'CLOB'` ou `'TEXT'` → TEXT ;
3. contenant `'BLOB'` **ou aucun type déclaré** → BLOB ;
4. contenant `'REAL'`, `'FLOA'` ou `'DOUB'` → REAL ;
5. sinon → NUMERIC.

**La règle 3 est décisive et contre-intuitive : une colonne sans type déclaré a l'affinité BLOB.**
Exclure sur l'affinité BLOB écarterait donc toutes les colonnes non typées — très fréquentes dans les
bases SQLite réelles — alors qu'elles contiennent le plus souvent du texte. Symétriquement, une
colonne déclarée `TEXT` peut parfaitement contenir un BLOB.

> **Recommandation SQLite : ne pas exclure sur le type déclaré seul, filtrer à la valeur** avec
> `typeof(col)`, comme au § 4.3 — **plus** une exclusion *a priori* des colonnes dont le type déclaré
> contient littéralement `BLOB`, qui sont les seules nommables avant la requête.

Ce partage a une conséquence directe sur la clause « l'exclusion est nommée » : sur PostgreSQL et
MySQL/MariaDB, l'exclusion est décidée **par colonne, avant la requête**, et se nomme naturellement
(« colonne `photo` : type `bytea`, exclue »). Sur SQLite, elle est **par valeur**, connue seulement
après lecture. La spec devra donc admettre **deux formes d'exclusion nommée** — une par colonne, une
par valeur — ou en normaliser une seule au prix d'une perte d'information.

---

## 6. Le coût

### 6.1 Combien de requêtes pour 300 tables

| étape | PostgreSQL | MariaDB/MySQL | SQLite |
| --- | --- | --- | --- |
| relevé du schéma | **1** requête (`pg_catalog`) | **1** requête (`information_schema`) | **1** requête (`sqlite_master` × `pragma_table_info`) |
| prélèvement | **1 par table** = 300, ou groupé | 300, ou groupé | 300, mais locales |

Le schéma tient toujours en une requête : c'est acquis et déjà éprouvé par le corpus (le pivot de
Dolibarr, 5 382 colonnes, sort d'un seul `SELECT`).

Le prélèvement, lui, ne se factorise pas naturellement : la contrainte 5 impose une requête **par
table, aux colonnes nommées explicitement**, avec `LIMIT 5`.

### 6.2 Grouper, et pourquoi il ne faut pas trop grouper

**Ce que chaque pilote permet :**

- **Npgsql** : `NpgsqlBatch` — « All statements and parameters are efficiently packed into a single
  packet — when possible — and sent to PostgreSQL. » Le multi-instruction par `;` marche mais oblige
  Npgsql à analyser le SQL pour trouver les points-virgules : déconseillé par la doc.
- **MySqlConnector** : `MySqlBatch`, et « MySqlConnector always allows batch statements ». Ne pas
  confondre avec `Allow User Variables` (variables `@nom`) ni avec `AllowLoadLocalInfile`, qui doit
  rester `false`.
- **Microsoft.Data.Sqlite** : sans objet, la base est locale.

**Ce qui plaide contre le lot unique — et c'est une contrainte du domaine, confirmée par la
technique.** La contrainte 6 tolère l'échec d'un prélèvement **à condition qu'il soit nommé** table
par table. Or la doc de Npgsql est formelle : « If you haven't started an explicit transaction […] a
batch is automatically wrapped in an implicit transaction. That is, **if a statement within the batch
fails, all later statements are skipped and the entire batch is rolled back.** » Un lot de 300
prélèvements dont un échoue — une table verrouillée, un type inattendu, un droit manquant sur une
seule table — rendrait donc **zéro aperçu**, là où la contrainte veut « 299 aperçus et une raison
nommée ». C'est l'exact opposé du comportement voulu.

> **Recommandation : lots de 20 à 50 tables**, avec **repli en requêtes individuelles sur le lot qui
> échoue**. On garde l'essentiel du gain de latence (300 allers-retours → 6 à 15) et l'on ne paie le
> prix d'un lot perdu que sur le lot concerné, dont on retrouve la granularité en le rejouant table
> par table. Côté SQLite : pas de lot du tout (§ 2.5), la base est locale.

### 6.3 Ordre de grandeur en latence

Hypothèses : 300 tables, 5 lignes par table, une requête par table.

| lien | RTT | prélèvement individuel (300 A/R) | lots de 25 (12 A/R) |
| --- | --- | --- | --- |
| même hôte / conteneur local | ~0,3 ms | ~0,1 s | négligeable |
| réseau local | ~1 ms | ~0,3 s | négligeable |
| VPN / site distant | ~30 ms | **~9 s** | ~0,4 s |
| lien intercontinental | ~150 ms | **~45 s** | ~2 s |

À quoi s'ajoute le temps d'exécution réel. Un `SELECT col1, …, colN FROM t LIMIT 5` sans `ORDER BY`
lit cinq lignes et s'arrête : c'est négligeable sur une table indexée, mais ⚠ **pas garanti** si la
table est très fragmentée ou si un filtre RLS s'applique. La borne `Command Timeout` à 30 s par défaut
protège du cas pathologique, et l'échec sera **toléré et nommé** — le comportement voulu.

Conclusion : sur un lien local ou LAN, le regroupement est inutile ; il ne devient décisif qu'au-delà
de ~20 ms de RTT. Comme le service ne sait pas d'avance où il se connecte, la stratégie par lots de 25
est le bon défaut : elle ne coûte rien en local et sauve 30 secondes à distance.

### 6.4 Limites de taille de requête

Aucune n'est bloquante pour une requête nommant 200 colonnes, mais deux méritent d'être connues :

- **PostgreSQL** ([Appendix K](https://www.postgresql.org/docs/17/limits.html)) : 1 600 colonnes par
  table, **1 664 colonnes dans un jeu de résultats**, 65 535 paramètres. Aucune limite documentée sur
  la longueur du texte d'une requête.
- **MySQL/MariaDB** : 4 096 colonnes par table (InnoDB : 1 017,
  [Column Count Limits](https://dev.mysql.com/doc/refman/8.4/en/column-count-limit.html)) ;
  **61 tables maximum dans une même jointure**
  ([Join limits](https://dev.mysql.com/doc/refman/8.4/en/join.html)) — sans effet sur un `UNION ALL`,
  mais bloquant pour toute stratégie qui joindrait les tables ; longueur de requête bornée par
  `max_allowed_packet`, « limits the size of a communication packet, which includes a single SQL
  statement sent to the server », défaut serveur 64 Mo, maximum 1 Go
  ([Packet Too Large](https://dev.mysql.com/doc/refman/8.4/en/packet-too-large.html)). Ce plafond
  s'applique **aussi au sens retour** : argument de plus pour tronquer côté serveur.
- **SQLite** ([Limits](https://sqlite.org/limits.html)) : `SQLITE_MAX_COLUMN` 2 000 par défaut (la plus
  basse des trois, et elle s'applique aussi au nombre de termes de la liste `SELECT`) ;
  `SQLITE_MAX_SQL_LENGTH` 1 Go ; **`SQLITE_MAX_COMPOUND_SELECT` = 500** — la vraie contrainte si l'on
  groupe en `UNION ALL` : au-delà de 500 branches, la requête est refusée. Ces valeurs sont des
  **défauts de compilation** ; sur la voie connectée c'est notre `e_sqlite3` qui s'exécute, donc elles
  sont connues.

---

## Ce qui reste ouvert

Questions que cette recherche **ne tranche pas**, et qui doivent devenir des décisions.

1. **Corriger `releves/*.sql`, et comment sur MariaDB.** Les deux défauts (§ 0.1c) sont certains ;
   la correction de `nullable` en booléen JSON sur MariaDB est **non résolue** — le type JSON y étant
   un alias de `LONGTEXT`. Trois issues possibles : trouver une forme qui marche sur les deux serveurs,
   scinder le fichier en `mysql.sql` et `mariadb.sql` (ce qui change le dialecte déclaré et donc le
   pivot), ou assouplir l'ingestion pour accepter `0`/`1` (ce qui affaiblirait le cas n° 4, dont
   `pivot-format.md` explique longuement pourquoi il existe). **À éprouver sur conteneur avant de
   décider.**
2. **Le corpus est-il à réextraire ?** Les huit pivots de `corpus/schemas/pivots/` portent l'ancienne
   forme, et le test témoin la convertit à la volée. Si les requêtes sont corrigées, les pivots du
   corpus divergent d'elles. Or le corpus est un instrument de mesure gelé, et le banc est clos. Ne
   rien toucher est probablement juste — mais alors il faut **écrire** que le corpus est figé dans une
   forme antérieure, plutôt que le laisser en commentaire d'un test.
3. **Passer par le texte pivot, ou ouvrir une seconde porte dans `Core` ?** La recommandation du § 1.4
   est de repasser par `ColumnListingIngestion.Ingest`. L'alternative — extraire les contrôles
   d'intégrité vers un garde partagé et ouvrir un constructeur interne — est plus propre à terme et
   plus coûteuse à court terme. C'est une décision de conception, pas un fait technique.
4. **Vues, tables partitionnées, colonnes générées.** Le chemin collé prend `relkind = 'r'`,
   `TABLE_TYPE = 'BASE TABLE'` et `pragma_table_info`. Une vue peut exposer des données personnelles ;
   une colonne générée SQLite en contient. La voie connectée doit faire **exactement pareil** que le
   collé (contrainte 1) — donc la question est : élargit-on **les deux** chemins, ou aucun ?
5. **Le pooling et la contrainte 2.** Une connexion poolée survit au scan, indexée par la chaîne de
   connexion. Est-ce une violation de « aucun secret d'accès durable » ? `Pooling=false` est le choix
   sûr ; il faut le décider, pas le subir.
6. **`pg_read_all_data` sous RLS.** Le rôle n'a pas `BYPASSRLS` : un aperçu peut revenir vide sans
   erreur. Faut-il détecter le cas (comparer le nombre de lignes lues à `reltuples` ? tester
   `row_security` ?) et le nommer, ou l'accepter comme un aperçu vide ? Le glossaire interdit l'absence
   muette : il faudra trancher.
7. **Deux formes d'exclusion nommée, ou une seule ?** Sur SQLite, l'exclusion binaire est décidée **à
   la valeur** et non à la colonne (§ 5.3). Faut-il un aperçu qui puisse dire « 2 valeurs sur 5 sont
   binaires » — donc une raison par valeur — ou normaliser sur « colonne exclue » au prix d'une perte ?
8. **La borne de 100 caractères sur `base` et les chemins SQLite.** Elle vient du domaine et fera
   refuser des scans sincères sur des chemins de conteneur. Rouvrir la borne, ou tronquer le chemin en
   le disant ?
9. **`genere_le` : horloge du service ou du serveur de base ?** La recommandation du § 0.2 est
   l'horloge du service (décalage sûr, uniformité des trois dialectes). Mais cela dissocie l'instant du
   relevé de l'instant du serveur lu, ce qui n'est pas rien pour un pivot dont le seul repère temporel
   est ce champ.
10. **L'interruption d'un scan SQLite : `sqlite3_interrupt` ou grain de la table ?** Le P/Invoke via
    `SqliteConnection.Handle` n'est documenté par personne (§ 2.3) et demande un prototype. Tester le
    token entre les requêtes suffit peut-être, la base étant locale et chaque requête courte. La
    seconde voie est bien moins chère ; il faut décider si le risque de la table pathologique la
    disqualifie.
11. **Le quoting des identifiants**, sachant que MySqlConnector accepte toujours le multi-instruction
    (§ 2.5). Ce n'est pas une question ouverte au sens strict — il faut le faire — mais c'est un point
    de revue à inscrire explicitement, parce que les noms interpolés viennent d'une base tierce.
12. **Éprouver les faits structurants sur conteneur**, avant la spec : (a) un rôle PostgreSQL sans
    privilège objet relève-t-il bien 100 % des colonnes par `pg_catalog` et 0 % par
    `information_schema` ? (b) un `GRANT SELECT (col)` MySQL ampute-t-il bien `I_S.COLUMNS` ?
    (c) `JSON_OBJECT` produit-il un booléen JSON sur MySQL 8 et pas sur MariaDB 11 ?

### Récapitulatif des points non vérifiés

- **PostgreSQL** : aucune page ne déclare `pg_catalog` lisible par `PUBLIC` (déduit de la note sur
  `USAGE` et de l'exception `pg_authid`) ; « les `attnum` ne sont pas réutilisés » n'est pas écrit tel
  quel ; `format_type` documenté comme rendant « the SQL name », l'inclusion de la longueur est
  déduite d'`atttypmod` ; `typcategory` par type concret non attesté ; `bytea::text` en hexadécimal
  déduit ; NULL-propagation de `left()` non énoncée. Le mécanisme `CancelRequest` de Npgsql, en
  revanche, est **vérifié** dans le code source.
- **MySQL/MariaDB** : `COLUMNS.ORDINAL_POSITION` n'est documenté ni comme partant de 1 ni comme
  contigu ; le suffixe `InnoDB free:` dans `TABLE_COMMENT` n'apparaît dans aucune doc consultée ;
  valeurs `'YES'`/`'NO'` d'`IS_NULLABLE` non explicitées côté MariaDB ; effet d'un `GRANT SELECT (col)`
  sur `I_S.COLUMNS` déduit de `SHOW COLUMNS` ; `REFERENCES` comme privilège minimal de visibilité non
  documenté (et « Unused » chez MariaDB) ; `max_allowed_packet` par défaut chez MariaDB non lu ;
  « multibyte safe » de `LEFT()` non écrit côté MariaDB ; nombre maximal de branches d'`UNION ALL` non
  documenté ; production d'un booléen JSON par `JSON_OBJECT` non éprouvée par exécution.
- **SQLite** : les colonnes de `pragma_foreign_key_list` ne sont documentées nulle part ; `cid` part
  de 0 n'est pas documenté (et la doc avertit de ne rien lui prêter) ; l'absence de mécanisme de
  commentaire est un constat, pas une citation ; `Mode=ReadOnly` de Microsoft.Data.Sqlite non vérifié
  à la source ; code d'erreur exposé sur `SQLITE_INTERRUPT` non vérifié.
- **Pilotes** : l'usage de `sqlite3_interrupt` via `SqliteConnection.Handle` n'est documenté nulle
  part par Microsoft — c'est une déduction à prototyper, ainsi que le code d'erreur exposé sur
  `SQLITE_INTERRUPT` ; type et message de l'exception de MySqlConnector sur pool épuisé non vérifiés ;
  absence d'un mot-clé « max pool size » chez Microsoft.Data.Sqlite déduite de la liste documentée des
  mots-clés ; statut actuel des bugs Oracle #94760 / #110790 / #110791 contre MySql.Data 26.7.0 non
  vérifié ; portée de la GPL sur un service non redistribué (SaaS) hors périmètre technique ;
  existence et version de `Testcontainers.MariaDb` 4.13.0 non vérifiées.
  En revanche, la sémantique « attente de verrou » de `CommandTimeout` chez Microsoft.Data.Sqlite, sa
  façade `*Async` synchrone, l'annulation par `CancelRequest` de Npgsql, l'annulation par `KILL QUERY`
  de MySqlConnector et la transaction implicite d'un `NpgsqlBatch` sont **vérifiées** — doc éditeur ou
  code source.
