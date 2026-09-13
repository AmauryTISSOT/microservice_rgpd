#!/usr/bin/env bash
#
# Plante cent demandes fictives dans le Tableau des demandes de la base LOCALE, ou les retire.
#
# ⚠️ Ce n'est pas une migration : rien ne lance ce script au démarrage du service. Il joue
# `scripts/seed-requests.sql` dans le conteneur Postgres d'Aspire, et dans lui seul — aucune chaîne
# de connexion n'est acceptée, pour qu'aucune autre base ne puisse être visée par erreur.
#
# Rejouable : les demandes plantées vivent dans une plage d'identifiants réservée, que le script
# efface avant de la replanter. Les demandes saisies à la main ne sont jamais touchées.
#
# Usage :
#   scripts/seed-requests.sh           # (re)plante les cent demandes
#   scripts/seed-requests.sh --reset   # retire les demandes plantées, sans en replanter

set -euo pipefail

RACINE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Les deux noms sont ceux que l'AppHost déclare (`src/MicroserviceRgpd.AspireHost/AppHost.cs`).
CONTENEUR="microservice_rgpd_bdd"
BASE="cleanarchitecture"
SCRIPT="$RACINE/scripts/seed-requests.sql"

info()  { printf '\033[1;34m▸\033[0m %s\n' "$*"; }
echec() { printf '\033[1;31m✗\033[0m %s\n' "$*" >&2; exit 1; }

variables=()
case "${1:-}" in
  "") ;;
  --reset) variables+=(-v reset=1) ;;
  -h|--help) sed -n '3,14p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
  *) echec "Option inconnue : $1. Voir « $0 --help »." ;;
esac

command -v docker >/dev/null 2>&1 || echec "docker est absent du PATH."

# Un conteneur arrêté répond « false », un conteneur absent ne répond pas : les deux se corrigent
# de la même façon, en démarrant la pile.
if [[ "$(docker inspect --format '{{.State.Running}}' "$CONTENEUR" 2>/dev/null)" != "true" ]]; then
  echec "$(printf '%s\n' \
    "Le conteneur Postgres local « $CONTENEUR » ne tourne pas." \
    "    Démarrer la pile : scripts/run-project.sh")"
fi

# Aspire exige un mot de passe, même sur le socket local, et le génère : il n'est lu que dans
# l'environnement du conteneur lui-même, sans jamais passer par cette machine.
psql_local() {
  docker exec -i "$CONTENEUR" sh -c \
    'PGPASSWORD="$POSTGRES_PASSWORD" exec psql --username "$POSTGRES_USER" "$@"' psql \
    --dbname "$BASE" --no-psqlrc --quiet "$@"
}

# La table n'existe qu'une fois les migrations jouées, c'est-à-dire après un premier démarrage du
# service : le dire vaut mieux que la « relation does not exist » de psql.
# Un psql qui échoue (authentification, base absente) n'est pas une table absente : sa sortie est
# rendue telle quelle.
if ! table="$(psql_local --tuples-only --no-align --command "SELECT to_regclass('data_subject_requests')" 2>&1)"; then
  echec "$(printf '%s\n' "psql ne répond pas dans « $CONTENEUR » :" "$(sed 's/^/    /' <<<"$table")")"
fi
if [[ "$table" != "data_subject_requests" ]]; then
  echec "$(printf '%s\n' \
    "La base « $BASE » n'a pas encore la table data_subject_requests." \
    "    Les migrations se jouent au démarrage du service : scripts/run-project.sh, une fois.")"
fi

if ((${#variables[@]})); then
  info "Retrait des demandes plantées…"
else
  info "Plantation des cent demandes de démonstration…"
fi

psql_local "${variables[@]}" --file - < "$SCRIPT"

plantees="$(psql_local --tuples-only --no-align --command \
  "SELECT count(*) FROM data_subject_requests
   WHERE id BETWEEN '5eed0000-0000-7000-8000-000000000000' AND '5eed0000-0000-7000-8000-ffffffffffff'")"
info "Demandes plantées dans la base : $plantees. Tableau : https://localhost:57679/demandes"
