#!/usr/bin/env bash
# Épreuve MySQL / MariaDB : l'asymétrie des droits ampute-t-elle le relevé, en silence ?
set -uo pipefail
cd "$(dirname "$0")"

IMAGE=${1:-mysql:8.4}
NOM=faits275-my
REL=../../../releves/mariadb.sql

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

# Le binaire client diffère : `mysql` chez Oracle, `mariadb` chez MariaDB.
CLI=mysql
docker exec "$NOM" sh -c 'command -v mysql' >/dev/null 2>&1 || CLI=mariadb

cli() { docker exec -i "$NOM" "$CLI" "$@"; }

cli -uroot -proot epreuve < fixture.sql

echo
echo "===================== SONDES SECONDAIRES (root)"
cli -uroot -proot --table --force epreuve < sondes.sql 2>&1

for compte in root plein partiel nu; do
  mdp=$compte; [ "$compte" = root ] && mdp=root
  echo
  echo "===================== RELEVÉ — compte '$compte'"
  cli "-u$compte" "-p$mdp" -N -B -r epreuve < "$REL" > "sortie-$compte.txt" 2>&1
  lignes=$(grep -c '^{' "sortie-$compte.txt" 2>/dev/null || echo 0)
  echo "lignes JSON : $lignes"
  head -2 "sortie-$compte.txt"
  echo '...'
  tail -2 "sortie-$compte.txt"
done

echo
echo "===================== VERDICT : le compte 'partiel' voit-il autant que 'plein' ?"
# JSON_OBJECT ne garantit pas l'ordre des clés : on compare les comptes déclarés
# par la ligne de fin du pivot, pas les lignes octet pour octet.
compte() { grep -o '"colonnes": *[0-9]*' "$1" | grep -o '[0-9]*' | tail -1; }
echo "colonnes déclarées — plein : $(compte sortie-plein.txt), partiel : $(compte sortie-partiel.txt)"
if [ "$(compte sortie-plein.txt)" = "$(compte sortie-partiel.txt)" ]; then
  echo "IDENTIQUES"
else
  echo "AMPUTÉ ET MUET — le pivot du compte 'partiel' est structurellement valide,"
  echo "son compte de fin est cohérent avec ce qu'il a vu, et rien ne dit qu'il manque des colonnes :"
  grep '^{' sortie-partiel.txt
fi

docker rm -f "$NOM" >/dev/null
