#!/usr/bin/env bash
#
# Démarre la pile entière (Postgres, sidecar de qualification, service .NET, et Ollama si
# `Llm:Enabled` vaut « true ») via l'AppHost Aspire, attend que l'accueil réponde, puis ouvre le
# navigateur dans une NOUVELLE FENÊTRE sur l'accueil et le dashboard Aspire.
#
# Le script reste au premier plan : Ctrl+C arrête la pile. Les containers marqués persistants par
# l'AppHost (Postgres, Ollama) survivent volontairement à l'arrêt — c'est leur raison d'être.
#
# Usage :
#   scripts/run-project.sh              # démarre, attend, ouvre le navigateur
#   scripts/run-project.sh --sans-navigateur
#   PROFIL=http scripts/run-project.sh  # dashboard en http plutôt qu'en https

set -euo pipefail

RACINE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$RACINE"

PROFIL="${PROFIL:-https}"
# L'adresse de l'accueil est celle du profil `https` de MicroserviceRgpd.Web : c'est ce profil que
# l'AppHost applique au projet, et son `applicationUrl` est donc la seule source du port.
ACCUEIL="${ACCUEIL:-https://localhost:57679/}"
DELAI_DEMARRAGE="${DELAI_DEMARRAGE:-300}"

ouvrir_navigateur=1
if [[ "${1:-}" == "--sans-navigateur" ]]; then
  ouvrir_navigateur=0
fi

journal="$(mktemp -t microservice-rgpd-demarrage.XXXXXX.log)"
apphost_pid=""

info()   { printf '\033[1;34m▸\033[0m %s\n' "$*"; }
alerte() { printf '\033[1;33m!\033[0m %s\n' "$*"; }
echec()  { printf '\033[1;31m✗\033[0m %s\n' "$*" >&2; exit 1; }

nettoyer() {
  local code=$?
  # Désarmé d'entrée : `exit` plus bas redéclencherait sinon ce même piège.
  trap - EXIT INT TERM
  if [[ -n "$apphost_pid" ]] && kill -0 "$apphost_pid" 2>/dev/null; then
    info "Arrêt de la pile…"
    kill -TERM "$apphost_pid" 2>/dev/null || true
    wait "$apphost_pid" 2>/dev/null || true
  fi
  printf 'Journal de démarrage conservé : %s\n' "$journal"
  exit $code
}
trap nettoyer EXIT INT TERM

# ---------------------------------------------------------------------------
# Prérequis. Ils sont vérifiés AVANT de lancer quoi que ce soit : un manque annoncé en trois
# secondes vaut mieux qu'une pile à moitié montée qui échoue deux minutes plus tard.
# ---------------------------------------------------------------------------

manquants=()
for outil in dotnet docker uv curl; do
  command -v "$outil" >/dev/null 2>&1 || manquants+=("$outil")
done
if ((${#manquants[@]})); then
  echec "Outils absents du PATH : ${manquants[*]}. Voir la section « Démarrer » du README."
fi

# `docker info` échoue pour deux raisons opposées : le démon est arrêté, ou il tourne mais refuse
# son socket à cet utilisateur. Le geste correctif n'est pas le même, d'où le tri ci-dessous — et
# la sortie réelle est conservée, parce qu'un troisième cas finira bien par arriver.
if ! sortie_docker="$(docker info 2>&1)"; then
  socket="${DOCKER_HOST:-unix:///var/run/docker.sock}"
  if grep -qi 'permission denied' <<<"$sortie_docker"; then
    echec "$(printf '%s\n' \
      "Docker tourne mais refuse son socket à cet utilisateur ($socket)." \
      "    Il manque l'appartenance au groupe « docker » — inutile de redémarrer le démon." \
      "    Corriger  : sudo usermod -aG docker \"\$USER\"  puis rouvrir la session (« newgrp docker » ne vaut que pour ce terminal)." \
      "    Vérifier  : id -nG | grep docker && docker info" \
      "    À savoir  : le groupe « docker » équivaut à un accès root sur la machine.")"
  elif grep -qiE 'cannot connect to the docker daemon|is the docker daemon running' <<<"$sortie_docker"; then
    echec "$(printf '%s\n' \
      "Le démon Docker ne tourne pas ($socket). Postgres et le sidecar en dépendent." \
      "    Corriger  : sudo systemctl start docker" \
      "    Au boot   : sudo systemctl enable --now docker.socket")"
  else
    echec "$(printf '%s\n' \
      "Docker ne répond pas. Postgres et le sidecar en dépendent." \
      "    Sortie de « docker info » :" \
      "$(sed 's/^/        /' <<<"$sortie_docker")")"
  fi
fi

# Sans certificat de développement approuvé, l'accueil s'ouvre sur un avertissement de sécurité et
# l'attente ci-dessous passerait quand même : l'alerte est donc préventive, pas bloquante.
if ! dotnet dev-certs https --check --trust >/dev/null 2>&1; then
  alerte "Certificat HTTPS de développement non approuvé — l'accueil s'ouvrira sur un avertissement."
  alerte "Pour le régler une fois pour toutes : dotnet dev-certs https --trust"
fi

if grep -q '"Enabled"[[:space:]]*:[[:space:]]*"true"' src/MicroserviceRgpd.AspireHost/appsettings.json 2>/dev/null; then
  alerte "Le moteur LLM est allumé : Ollama réclame un GPU NVIDIA, et le premier démarrage tire plusieurs gigaoctets."
fi

# ---------------------------------------------------------------------------
# Démarrage
# ---------------------------------------------------------------------------

info "Démarrage de la pile Aspire (profil « $PROFIL »)…"
# La sortie est dupliquée dans un journal parce que le lien du dashboard n'existe QUE là : il porte
# un jeton de connexion, imprimé au démarrage et impossible à deviner.
# Substitution de processus plutôt qu'un tube : dans un tube, `$!` désignerait `tee`, et le Ctrl+C
# n'arrêterait donc pas la pile.
dotnet run --project src/MicroserviceRgpd.AspireHost --launch-profile "$PROFIL" \
  > >(tee "$journal") 2>&1 &
apphost_pid=$!

# ---------------------------------------------------------------------------
# Attente. Deux conditions distinctes, parce qu'elles échouent pour des raisons différentes.
# ---------------------------------------------------------------------------

attendre_accueil() {
  local echeance=$((SECONDS + DELAI_DEMARRAGE))
  while ((SECONDS < echeance)); do
    kill -0 "$apphost_pid" 2>/dev/null \
      || echec "L'AppHost s'est arrêté avant que l'accueil ne réponde. Journal : $journal"
    # -k : le certificat de développement n'est pas forcément approuvé, et ce n'est pas ce qu'on teste ici.
    if curl -ksf --max-time 3 "${ACCUEIL%/}/health" >/dev/null 2>&1; then
      return 0
    fi
    sleep 2
  done
  return 1
}

url_dashboard() {
  # Aspire imprime « Login to the dashboard at https://localhost:17143/login?t=… ».
  grep -oE 'https?://localhost:[0-9]+/login\?t=[A-Za-z0-9]+' "$journal" | tail -n 1
}

info "Attente de l'accueil ($ACCUEIL) — jusqu'à ${DELAI_DEMARRAGE}s…"
attendre_accueil \
  || echec "L'accueil n'a pas répondu en ${DELAI_DEMARRAGE}s. Journal : $journal"

dashboard="$(url_dashboard || true)"
if [[ -z "$dashboard" ]]; then
  # Attrapé tard : le lien du dashboard sort tôt, mais l'ordre des lignes n'est pas contractuel.
  for _ in 1 2 3 4 5; do
    sleep 1
    dashboard="$(url_dashboard || true)"
    [[ -n "$dashboard" ]] && break
  done
fi

if [[ -z "$dashboard" ]]; then
  alerte "Lien du dashboard introuvable dans la sortie d'Aspire — seul l'accueil sera ouvert."
fi

# ---------------------------------------------------------------------------
# Navigateur, dans une nouvelle fenêtre
# ---------------------------------------------------------------------------

ouvrir() {
  local -a pages=("$@")

  # `$BROWSER` d'abord : c'est la seule déclaration explicite de l'utilisateur.
  local candidats=()
  [[ -n "${BROWSER:-}" ]] && candidats+=("$BROWSER")
  candidats+=(google-chrome google-chrome-stable chromium chromium-browser brave-browser microsoft-edge firefox)

  local navigateur=""
  for c in "${candidats[@]}"; do
    if command -v "$c" >/dev/null 2>&1; then
      navigateur="$c"
      break
    fi
  done

  if [[ -z "$navigateur" ]]; then
    # `xdg-open` ne sait pas ouvrir une fenêtre : c'est un repli, et il est annoncé comme tel.
    if command -v xdg-open >/dev/null 2>&1; then
      alerte "Aucun navigateur connu — repli sur xdg-open (onglets, pas une nouvelle fenêtre)."
      for page in "${pages[@]}"; do xdg-open "$page" >/dev/null 2>&1 || true; done
      return 0
    fi
    alerte "Aucun navigateur trouvé. À ouvrir à la main :"
    for page in "${pages[@]}"; do printf '    %s\n' "$page"; done
    return 0
  fi

  # Sur le nom de base : `$BROWSER` peut être un chemin absolu, et « /usr/bin/firefox » ne
  # ressemble pas à « firefox* ».
  case "$(basename -- "$navigateur")" in
    firefox*)
      # Firefox n'accepte qu'une URL par `--new-window` ; les suivantes rejoignent cette fenêtre.
      "$navigateur" --new-window "${pages[0]}" >/dev/null 2>&1 &
      for page in "${pages[@]:1}"; do
        sleep 1
        "$navigateur" --new-tab "$page" >/dev/null 2>&1 &
      done
      ;;
    *)
      # Chromium et dérivés : toutes les pages atterrissent dans la même nouvelle fenêtre.
      "$navigateur" --new-window "${pages[@]}" >/dev/null 2>&1 &
      ;;
  esac
  info "Navigateur ouvert ($navigateur)."
}

pages=("$ACCUEIL")
[[ -n "$dashboard" ]] && pages+=("$dashboard")

if ((ouvrir_navigateur)); then
  ouvrir "${pages[@]}"
else
  info "Navigateur non ouvert (--sans-navigateur). Adresses :"
  for page in "${pages[@]}"; do printf '    %s\n' "$page"; done
fi

printf '\n'
info "Pile démarrée. Ctrl+C pour tout arrêter."
printf '    Accueil   : %s\n' "$ACCUEIL"
[[ -n "$dashboard" ]] && printf '    Dashboard : %s\n' "$dashboard"
printf '\n'

# Le script rend la main au flux de logs d'Aspire : la fenêtre reste la console de la pile.
wait "$apphost_pid"
