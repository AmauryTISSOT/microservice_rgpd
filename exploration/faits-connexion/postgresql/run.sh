#!/usr/bin/env bash
# Épreuve PostgreSQL : le relevé par pg_catalog tient-il pour un rôle sans aucun
# privilège objet, là où information_schema le rend muet ?
set -uo pipefail
cd "$(dirname "$0")"

IMAGE=${IMAGE:-postgres:17-alpine}
NOM=faits275-pg

docker rm -f "$NOM" >/dev/null 2>&1
docker run -d --name "$NOM" -e POSTGRES_PASSWORD=root -e POSTGRES_DB=epreuve "$IMAGE" >/dev/null

echo "== attente du serveur ($IMAGE)"
for _ in $(seq 1 60); do
  docker exec "$NOM" pg_isready -U postgres -d epreuve >/dev/null 2>&1 && break
  sleep 1
done

docker exec -i "$NOM" psql -v ON_ERROR_STOP=1 -U postgres -d epreuve < fixture.sql

echo
echo "== version"
docker exec "$NOM" psql -At -U postgres -d epreuve -c 'select version()'

echo
echo "===================== SONDES SECONDAIRES (superutilisateur)"
docker exec -i "$NOM" psql -U postgres -d epreuve < sondes.sql 2>&1
docker exec -i "$NOM" psql -U postgres -d epreuve < sondes-suite.sql 2>&1

echo
echo "===================== RELEVÉ pg_catalog — superutilisateur"
REL=../../../releves/postgresql.sql
docker exec -i "$NOM" psql -At -U postgres -d epreuve < "$REL" > capture-super.txt 2>&1
head -3 capture-super.txt; echo '...'; tail -2 capture-super.txt
echo "lignes: $(wc -l < capture-super.txt)"

echo
echo "===================== RELEVÉ pg_catalog — rôle sans_droits"
docker exec -e PGPASSWORD=sans_droits -i "$NOM" psql -At -U sans_droits -h 127.0.0.1 -d epreuve < "$REL" > capture-sans.txt 2>&1
head -3 capture-sans.txt; echo '...'; tail -2 capture-sans.txt
echo "lignes: $(wc -l < capture-sans.txt)"

echo
echo "===================== COMPARAISON des deux relevés (hors ligne d'en-tête horodatée)"
if [ "$(wc -l < capture-sans.txt)" -le 5 ] || [ "$(wc -l < capture-super.txt)" -le 5 ]; then
  echo "RELEVÉ VIDE OU EN ÉCHEC — comparaison sans objet"
elif diff <(tail -n +2 capture-super.txt) <(tail -n +2 capture-sans.txt) >/dev/null; then
  echo "IDENTIQUES — 100 % des colonnes relevées sans aucun privilège objet"
else
  echo "DIFFÉRENTS :"
  diff <(tail -n +2 capture-super.txt) <(tail -n +2 capture-sans.txt) | head -20
fi

echo
echo "===================== information_schema — rôle sans_droits"
docker exec -e PGPASSWORD=sans_droits -i "$NOM" psql -U sans_droits -h 127.0.0.1 -d epreuve < sondes-suite.sql 2>&1

docker rm -f "$NOM" >/dev/null
