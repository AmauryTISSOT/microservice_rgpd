#!/usr/bin/env bash
# Epreuve du garde de privilege (#286) : sait-on, AVANT de relever, distinguer un
# compte qui verra toute la base d'un compte qui l'amputera en silence ?
set -uo pipefail
cd "$(dirname "$0")"

IMAGE=${1:-mysql:8.4}
ETIQ=${2:-mysql84}
NOM=faits286-my

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
cli() { docker exec -i "$NOM" "$CLI" "$@"; }

echo "== version reelle du serveur"
cli -uroot -proot -N -B -e 'SELECT VERSION()'

cli -uroot -proot --force epreuve < fixture.sql

# Le role par defaut : la syntaxe diverge entre les deux moteurs, on tente les deux.
cli -uroot -proot --force -e "SET DEFAULT ROLE ALL TO 'parrole'@'%'" 2>&1 | sed 's/^/  [role mysql] /'
cli -uroot -proot --force -e "SET DEFAULT ROLE 'r_lecture' FOR 'parrole'@'%'" 2>&1 | sed 's/^/  [role mariadb] /'

for compte in plein global tableatable partiel partieltable parrole nu; do
  echo
  echo "############################################################"
  echo "### COMPTE : $compte"
  echo "############################################################"
  cli "-u$compte" "-p$compte" --table --force < sondes.sql 2>&1
done

echo
echo "############################################################"
echo "### PARROLE, role explicitement endosse (SHOW GRANTS ... USING)"
echo "############################################################"
cli -uparrole -pparrole --table --force -e "SHOW GRANTS FOR CURRENT_USER() USING 'r_lecture'" 2>&1
cli -uparrole -pparrole --table --force -e "SET ROLE 'r_lecture'; SELECT '--- apres SET ROLE' AS etape; SHOW GRANTS FOR CURRENT_USER(); SELECT COUNT(*) AS colonnes_au_catalogue FROM information_schema.COLUMNS WHERE TABLE_SCHEMA='epreuve'" 2>&1

echo
echo "############################################################"
echo "### CONTRE-EPREUVE : ce que chaque compte lit VRAIMENT"
echo "### (le garde a-t-il dit vrai ? on compare a la realite)"
echo "############################################################"
for compte in plein global tableatable partiel partieltable parrole nu; do
  echo "--- $compte"
  for t in adherents cotisations journaux v_adherents; do
    r=$(cli "-u$compte" "-p$compte" -N -B --force -e "SELECT COUNT(*) FROM epreuve.$t" 2>&1 | tr '\n' ' ')
    echo "    SELECT * FROM $t -> $r"
  done
done

docker rm -f "$NOM" >/dev/null
