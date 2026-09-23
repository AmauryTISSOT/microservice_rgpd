#!/usr/bin/env bash
#
# Extrait les schémas amont vers le format pivot, PAR INTROSPECTION.
#
# ⚠️ La règle centrale, héritée de #125 : on ne lit JAMAIS le DDL pour produire le
# corpus. Le DDL est chargé dans un conteneur jetable, puis c'est le CATALOGUE du
# SGBD qu'on interroge, avec les mêmes requêtes que le service fournira à
# l'`Operator` — celles de `releves/`. Les 1 913 commentaires `--` de Dolibarr
# n'atteignent pas `information_schema` ; un corpus lu dans les fichiers donnerait
# au moteur un signal que la production ne lui donnera jamais.
#
# Prérequis : docker, sqlite3 ≥ 3.38, git, python3.
# Usage : ./extraire.sh [répertoire_de_travail]
set -uo pipefail

TRAVAIL="${1:-/tmp/corpus-schemas}"
ICI="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RACINE="$(dirname "$ICI")"
RELEVES="$(dirname "$(dirname "$RACINE")")/releves"
SRC="$TRAVAIL/src"
DDL="$TRAVAIL/ddl"
OUT="$RACINE/pivots"
mkdir -p "$SRC" "$DDL" "$OUT"

# --- 1. Les sources, avec le commit qui a servi -----------------------------
# ⚠️ Règle de provenance (#125) : rien n'entre sans clonage et lecture directe.
cloner() {  # <nom> <url>
  [ -d "$SRC/$1" ] && { echo "[$1] déjà cloné"; return; }
  git clone --depth 1 "$2" "$SRC/$1" >/dev/null 2>&1 && echo "[$1] cloné" || echo "[$1] ÉCHEC"
}
cloner dolibarr https://github.com/Dolibarr/dolibarr.git
cloner glpi     https://github.com/glpi-project/glpi.git
cloner openemr  https://github.com/openemr/openemr.git
cloner galette  https://github.com/galette/galette.git
cloner sacoche  https://forge.apps.education.fr/sesamath/sacoche.git
# Paheko : historique complet, trois états sont nécessaires.
[ -d "$SRC/paheko" ] || git clone https://github.com/paheko/paheko.git "$SRC/paheko" >/dev/null 2>&1

# --- 2. Concaténation du DDL amont -----------------------------------------
# Dolibarr : les tables d'abord, les clés ensuite. Un simple glob `*.sql`
# entremêlerait `llx_x.key.sql` (qui trie avant) et `llx_x.sql`.
T="$SRC/dolibarr/htdocs/install/mysql/tables"
echo "SET FOREIGN_KEY_CHECKS=0;" > "$DDL/dolibarr.sql"
ls "$T"/*.sql | grep -v '\.key\.sql$' | while read -r f; do
  cat "$f" >> "$DDL/dolibarr.sql"; echo ";" >> "$DDL/dolibarr.sql"
done
cat "$T"/*.key.sql >> "$DDL/dolibarr.sql"

for paire in "glpi:$SRC/glpi/install/mysql/glpi-empty.sql" \
             "openemr:$SRC/openemr/sql/database.sql" \
             "galette:$SRC/galette/galette/install/scripts/mysql.sql"; do
  n="${paire%%:*}"; f="${paire#*:}"
  echo "SET FOREIGN_KEY_CHECKS=0;" > "$DDL/$n.sql"; cat "$f" >> "$DDL/$n.sql"
done

# SACoche : un fichier par table. ⚠️ `_sql/structure/` seulement — `_sql/webmestre/`
# est une base DISTINCTE (l'administration du service hébergé), qu'un
# établissement client ne déploie pas.
echo "SET FOREIGN_KEY_CHECKS=0;" > "$DDL/sacoche.sql"
for f in "$SRC"/sacoche/_sql/structure/*.sql; do
  cat "$f" >> "$DDL/sacoche.sql"; echo ";" >> "$DDL/sacoche.sql"
done

# Galette en PostgreSQL : le même schéma dans un second dialecte. C'est la seule
# couverture de banc PostgreSQL du corpus, et #128 la notait absente.
cp "$SRC/galette/galette/install/scripts/pgsql.sql" "$DDL/galette-pg.sql"

# --- 3. Conteneurs jetables -------------------------------------------------
docker rm -f rgpd-maria rgpd-pg >/dev/null 2>&1
docker run -d --name rgpd-maria -e MARIADB_ROOT_PASSWORD=x -e MARIADB_DATABASE=banc mariadb:11.8 >/dev/null
docker run -d --name rgpd-pg    -e POSTGRES_PASSWORD=x     -e POSTGRES_DB=banc      postgres:16   >/dev/null
for i in $(seq 1 60); do docker exec rgpd-maria mariadb -uroot -px -e "SELECT 1" >/dev/null 2>&1 && break; sleep 1; done
for i in $(seq 1 60); do docker exec rgpd-pg pg_isready -U postgres >/dev/null 2>&1 && break; sleep 1; done

# --- 4. Chargement puis INTROSPECTION ---------------------------------------
extraire_mysql() {  # <nom> <ddl>
  docker exec rgpd-maria mariadb -uroot -px -e "DROP DATABASE IF EXISTS banc; CREATE DATABASE banc CHARACTER SET utf8mb4;"
  # ⚠️ Les commentaires `--` sont retirés avant chargement, comme le fait
  # l'installeur PHP amont : MariaDB n'accepte `--` que suivi d'un blanc, et
  # Dolibarr écrit `--ip used to create record`. Ils n'atteignent de toute façon
  # jamais information_schema — c'est tout l'objet de #125.
  sed -E 's/(^|[[:space:]])--.*$/\1/' "$2" > "$TRAVAIL/ddl_clean.sql"
  docker exec -i rgpd-maria mariadb -uroot -px --force banc < "$TRAVAIL/ddl_clean.sql" 2>"$TRAVAIL/err_$1.txt"
  docker exec -i rgpd-maria mariadb -uroot -px -N -B -r banc < "$RELEVES/mariadb.sql" > "$OUT/$1.jsonl"
  echo "[$1] $(grep -c '^ERROR' "$TRAVAIL/err_$1.txt") erreurs · $(tail -1 "$OUT/$1.jsonl")"
}

extraire_sqlite() {  # <nom> <schema.sql>
  rm -f "$TRAVAIL/$1.sqlite"
  sqlite3 "$TRAVAIL/$1.sqlite" < "$2" 2>"$TRAVAIL/err_$1.txt"
  sqlite3 -noheader -list "$TRAVAIL/$1.sqlite" < "$RELEVES/sqlite.sql" > "$OUT/$1.jsonl"
  echo "[$1] $(tail -1 "$OUT/$1.jsonl")"
}

for n in dolibarr glpi openemr galette sacoche; do extraire_mysql "$n" "$DDL/$n.sql"; done
extraire_mysql temoin "$(dirname "$(dirname "$RACINE")")/brocanto/db/schema.sql"

extraire_sqlite paheko-0.8.0 "$SRC/paheko/archives/0.8.0_schema.sql"
extraire_sqlite paheko-1.0.0 "$SRC/paheko/archives/1.0.0_schema.sql"
extraire_sqlite paheko-head  "$SRC/paheko/src/include/data/schema.sql"

docker exec rgpd-pg psql -U postgres -c "DROP DATABASE IF EXISTS banc2;" >/dev/null 2>&1
docker exec rgpd-pg psql -U postgres -c "CREATE DATABASE banc2;" >/dev/null 2>&1
docker exec -i rgpd-pg psql -U postgres -d banc2 < "$DDL/galette-pg.sql" >/dev/null 2>&1
docker exec -i rgpd-pg psql -U postgres -d banc2 -At < "$RELEVES/postgresql.sql" > "$OUT/galette-pg.jsonl"
echo "[galette-pg] $(tail -1 "$OUT/galette-pg.jsonl")"

docker rm -f rgpd-maria rgpd-pg >/dev/null 2>&1
echo "Pivots écrits dans $OUT — relancer outils/echantillonner.py pour le plan de sondage."
