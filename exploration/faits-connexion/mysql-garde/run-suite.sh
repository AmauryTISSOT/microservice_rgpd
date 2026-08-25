#!/usr/bin/env bash
# Seconde salve (#286) : la trace des objets invisibles, et la voie des roles.
set -uo pipefail
cd "$(dirname "$0")"

IMAGE=${1:-mysql:8.4}
NOM=faits286-suite

docker rm -f "$NOM" >/dev/null 2>&1
docker run -d --name "$NOM" \
  -e MYSQL_ROOT_PASSWORD=root -e MYSQL_DATABASE=epreuve \
  -e MARIADB_ROOT_PASSWORD=root -e MARIADB_DATABASE=epreuve \
  "$IMAGE" >/dev/null

echo "== attente du serveur ($IMAGE)"
for _ in $(seq 1 90); do
  docker exec "$NOM" sh -c 'mariadb -uroot -proot -e "select 1" 2>/dev/null || mysql -uroot -proot -e "select 1" 2>/dev/null' >/dev/null 2>&1 && break
  sleep 2
done

CLI=mysql
docker exec "$NOM" sh -c 'command -v mysql' >/dev/null 2>&1 || CLI=mariadb
cli() { docker exec -i "$NOM" "$CLI" "$@" 2>&1 | grep -v 'Using a password'; }

cli -uroot -proot --force epreuve < fixture.sql >/dev/null
cli -uroot -proot --force -e "SET DEFAULT ROLE ALL TO 'parrole'@'%'" >/dev/null
cli -uroot -proot --force -e "SET DEFAULT ROLE 'r_lecture' FOR 'parrole'@'%'" >/dev/null

for compte in plein tableatable partieltable parrole; do
  echo
  echo "############################################################"
  echo "### COMPTE : $compte"
  echo "############################################################"
  cli "-u$compte" "-p$compte" --table --force < sondes-suite.sql
done

echo
echo "############################################################"
echo "### COUT : combien de requetes, et en combien de temps"
echo "############################################################"
echo "-- une seule requete, celle de la sonde 9, chronometree en tant que 'plein'"
GARDE="SELECT COUNT(*) FROM information_schema.TABLES t WHERE t.TABLE_SCHEMA='epreuve' AND NOT EXISTS (SELECT 1 FROM information_schema.USER_PRIVILEGES up WHERE up.PRIVILEGE_TYPE='SELECT') AND NOT EXISTS (SELECT 1 FROM information_schema.SCHEMA_PRIVILEGES sp WHERE sp.TABLE_SCHEMA='epreuve' AND sp.PRIVILEGE_TYPE='SELECT') AND NOT EXISTS (SELECT 1 FROM information_schema.TABLE_PRIVILEGES tp WHERE tp.TABLE_SCHEMA='epreuve' AND tp.TABLE_NAME=t.TABLE_NAME AND tp.PRIVILEGE_TYPE='SELECT')"
for i in 1 2 3; do
  d=$( { TIMEFORMAT=%R; time cli -uplein -pplein -N -B --force -e "$GARDE" >/dev/null; } 2>&1 )
  echo "  essai $i : ${d}s (docker exec compris)"
done

echo
echo "-- le compte 'nu' peut-il lire ses propres droits ? (panne propre du garde)"
cli -unu -pnu --table --force -e "SHOW GRANTS FOR CURRENT_USER()"
cli -unu -pnu --table --force -e "SELECT COUNT(*) AS lignes_table_privileges FROM information_schema.TABLE_PRIVILEGES"
cli -unu -pnu --table --force -e "SELECT COUNT(*) AS objets FROM information_schema.TABLES WHERE TABLE_SCHEMA='epreuve'"

docker rm -f "$NOM" >/dev/null
