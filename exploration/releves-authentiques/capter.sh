#!/usr/bin/env bash
# Réextraction des captures authentiques de `captures/`. Voir README.md.
#
# Monte les quatre SGBD, exécute les requêtes de `releves/` TELLES QU'ELLES SONT
# dans l'arbre de travail, et écrase les captures.
#
# ⚠️ Réextraire change les fixtures que le filet rejoue. Ne le faites qu'après
# avoir modifié une requête, et relisez le diff : c'est le seul moment où la
# différence entre « la requête a changé » et « la sortie a changé » est visible.
#
# Docker Desktop doit tourner.
# Usage : bash exploration/releves-authentiques/capter.sh [pg|mysql|mariadb|sqlite]...
set -uo pipefail
cd "$(dirname "$0")"

RELEVES=../../releves
OUT=captures
mkdir -p "$OUT"

cibles=("$@")
[ ${#cibles[@]} -eq 0 ] && cibles=(pg mysql mariadb sqlite)

veut() { for c in "${cibles[@]}"; do [ "$c" = "$1" ] && return 0; done; return 1; }

# ---------------------------------------------------------------- PostgreSQL
if veut pg; then
  NOM=b284-pg
  docker rm -f "$NOM" >/dev/null 2>&1
  docker run -d --name "$NOM" -e POSTGRES_PASSWORD=root -e POSTGRES_DB=epreuve \
    postgres:17-alpine >/dev/null
  echo "== PostgreSQL : attente"
  for _ in $(seq 1 60); do
    docker exec "$NOM" pg_isready -U postgres -d epreuve >/dev/null 2>&1 && break
    sleep 1
  done
  docker exec -i "$NOM" psql -q -v ON_ERROR_STOP=1 -U postgres -d epreuve < pg-fixture.sql >/dev/null
  docker exec "$NOM" psql -At -U postgres -d epreuve -c 'select version()' | head -1
  docker exec -i "$NOM" psql -At -U postgres -d epreuve < "$RELEVES/postgresql.sql" \
    > "$OUT/postgresql.txt" 2>&1
  echo "   -> $OUT/postgresql.txt ($(wc -l < "$OUT/postgresql.txt") lignes)"
  docker rm -f "$NOM" >/dev/null
fi

# --------------------------------------------------------- MySQL et MariaDB
capter_mysql() {
  local image=$1 nom=$2 sortie=$3
  docker rm -f "$nom" >/dev/null 2>&1
  docker run -d --name "$nom" -e MYSQL_ROOT_PASSWORD=root -e MYSQL_DATABASE=epreuve \
    -e MARIADB_ROOT_PASSWORD=root -e MARIADB_DATABASE=epreuve "$image" >/dev/null
  echo "== $image : attente"

  # MariaDB ≥ 11 n'embarque plus le binaire `mysql` : le client s'appelle `mariadb`.
  local cli=mysql
  docker exec "$nom" sh -c 'command -v mysql' >/dev/null 2>&1 || cli=mariadb

  # ⚠️ Détection par TCP, jamais par le socket : MySQL comme MariaDB lancent un
  # serveur TEMPORAIRE sur le socket le temps d'initialiser la base, puis le
  # coupent. Sonder le socket rend « prêt » pendant cette fenêtre, et la fixture
  # part alors dans un serveur qui va disparaître. Le port 3306 n'est ouvert qu'à
  # la fin de l'initialisation.
  local pret=non
  for _ in $(seq 1 180); do
    if docker exec "$nom" sh -c \
      "$cli --protocol=tcp -h 127.0.0.1 -uroot -proot -e 'select 1'" >/dev/null 2>&1; then
      pret=oui; break
    fi
    sleep 1
  done
  if [ "$pret" = non ]; then
    echo "   ÉCHEC : $image n'a pas démarré"; docker rm -f "$nom" >/dev/null; return 1
  fi

  docker exec -i "$nom" sh -c "$cli -uroot -proot epreuve" < mysql-fixture.sql 2>&1 | grep -v Warning
  docker exec "$nom" sh -c "$cli -N -B -uroot -proot -e 'select version()'" 2>/dev/null
  docker exec -i "$nom" sh -c "$cli -N -B -r -uroot -proot epreuve" \
    < "$RELEVES/mariadb.sql" 2>/dev/null > "$OUT/$sortie"
  echo "   -> $OUT/$sortie ($(wc -l < "$OUT/$sortie") lignes)"
  docker rm -f "$nom" >/dev/null
}

veut mysql   && capter_mysql mysql:8.4    b284-mysql   mysql84.txt
veut mariadb && capter_mysql mariadb:11.8 b284-mariadb mariadb118.txt

# -------------------------------------------------------------------- SQLite
if veut sqlite; then
  echo "== SQLite"
  RACINE=$(cd ../.. && (pwd -W 2>/dev/null || pwd))
  # MSYS_NO_PATHCONV : sans lui, Git Bash traduit `/r/banc-284` en `R:/banc-284`.
  MSYS_NO_PATHCONV=1 \
  docker run --rm -v "$RACINE:/r" -w /r/exploration/releves-authentiques keinos/sqlite3:latest \
    sh -c 'rm -f /tmp/epreuve.sqlite;
           sqlite3 /tmp/epreuve.sqlite < sqlite-fixture.sql;
           sqlite3 --version;
           sqlite3 -noheader -list /tmp/epreuve.sqlite < /r/releves/sqlite.sql' \
    > "$OUT/sqlite.raw" 2>&1
  head -1 "$OUT/sqlite.raw"
  tail -n +2 "$OUT/sqlite.raw" > "$OUT/sqlite.txt"
  rm -f "$OUT/sqlite.raw"
  echo "   -> $OUT/sqlite.txt ($(wc -l < "$OUT/sqlite.txt") lignes)"
fi

# ⚠️ On ne garde que les lignes du pivot. Les clients en ligne de commande mêlent
# leurs avertissements à la sortie, et une fixture qu'il faudrait nettoyer avant de
# l'ingérer ne dirait plus « la sortie réelle passe l'ingestion » : le test doit
# pouvoir lire le fichier tel quel.
for f in "$OUT"/*.txt; do
  grep '^[[:space:]]*{' "$f" > "$f.propre" && mv "$f.propre" "$f"
done

echo
echo "captures : $(ls "$OUT")"
