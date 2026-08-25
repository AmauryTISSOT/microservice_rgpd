#!/usr/bin/env bash
# Rapièce la ligne de fin des captures (`{"colonnes":N}` → `{"fin":true,"colonnes":N}`)
# pour isoler le défaut SUIVANT : le parseur s'arrête au premier cas rencontré.
set -uo pipefail
cd "$(dirname "$0")"
mkdir -p captures-rapiecees

for f in ../postgresql/capture-super.txt ../mysql/sortie-plein.txt ../sqlite-interrupt/capture-pivot.txt; do
  n=$(basename "$(dirname "$f")")
  grep '^{' "$f" | sed 's/^{"colonnes"\( *\):/{"fin":true,"colonnes"\1:/' > "captures-rapiecees/$n.txt"
  echo "-- $n : $(tail -1 "captures-rapiecees/$n.txt")"

  # Second rapiéçage, cumulatif : `nullable` en booléen JSON plutôt qu'en 0/1.
  sed 's/"nullable"\( *\): *0/"nullable"\1: false/; s/"nullable"\( *\): *1/"nullable"\1: true/' \
    "captures-rapiecees/$n.txt" > "captures-rapiecees/$n-booleen.txt"
done
