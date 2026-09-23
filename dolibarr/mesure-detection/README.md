# Mesure du temps de détection d'A2 sur Dolibarr (#467)

Le banc qui a produit les chiffres de la section « Mesure du temps de détection sur Dolibarr » de
l'[ADR-0025](../../docs/adr/0025-la-detection-passe-a-un-modele-a-plongements-servi-par-ollama-le-lexique-en-repli-de-deploiement.md).

⚠️ **Rien ici n'entre dans la suite de tests.** La mesure exige un encodeur servi et du matériel réel.
Elle se rejoue à la main.

## Dispositif

- **Pile** lancée par l'AppHost, drapeau `Screening:Embeddings:Enabled` allumé. Ollama tourne dans
  l'image `ollama/ollama:0.34.0`, la version lue dans le manifest de l'artefact. `bge-m3` a le digest
  `790764642607…`, celui du manifest. Le moteur envoie des lots de 64 textes.
- **CPU** : `Screening__Embeddings__Enabled=true` seul. Le conteneur naît sans GPU et le journal
  d'Ollama indique `library=cpu`.
- **GPU** : `Llm__Enabled=true` en plus. C'est le seul drapeau qui fait demander le GPU à l'AppHost.
  `qwen3:8b` était déjà dans le volume et n'a jamais été appelé. Le journal indique `library=CUDA` et
  `offloaded 25/25 layers to GPU`. ⚠️ Le conteneur étant persistant, il faut le supprimer
  (`docker rm -f microservice_rgpd_ollama`) pour qu'il renaisse avec le GPU.
- **Base** : une MariaDB 11.8 jetable, dont le schéma est reconstruit à partir de
  `corpus/schemas/pivots/dolibarr.jsonl` par `ddl.ps1` (413 tables, 5 382 colonnes). A2 ne lit que
  les noms : les index, les clés et les valeurs par défaut sont hors sujet.
- **Collage** : produit par la requête du service, `releves/mariadb.sql`, sur cette même base. Le
  collé et le scanné portent donc exactement les mêmes noms.
- **Pilote** : `mesure.ps1`. Il poste le formulaire de dépôt, ou lance le scan et interroge l'écran
  d'attente, comme le fait un navigateur, jeton anti-rejeu compris. Le temps de détection est la
  **somme des durées des appels `POST /api/embed`** relevées dans le journal d'Ollama. Le geste est
  la durée du POST de dépôt ou, pour le scan, celle du lancement jusqu'à la redirection vers le
  rapport.

## Rejouer

```powershell
docker run -d --name rgpd-dolibarr-467 -p 3307:3306 -e MARIADB_ROOT_PASSWORD=mesure -e MARIADB_DATABASE=dolibarr mariadb:11.8
./ddl.ps1   # écrit dolibarr.sql à côté
cmd /c "docker exec -i rgpd-dolibarr-467 mariadb -uroot -pmesure dolibarr < dolibarr.sql"
cmd /c "docker exec -i rgpd-dolibarr-467 mariadb -uroot -pmesure -N -B -r dolibarr < ..\..\releves\mariadb.sql > collage-dolibarr.jsonl"
# CPU — depuis la racine du dépôt : $env:Screening__Embeddings__Enabled='true'; dotnet run --project src/MicroserviceRgpd.AspireHost
./mesure.ps1 -Base https://localhost:57679 -Chemin colle -Materiel cpu -Runs 4
./mesure.ps1 -Base https://localhost:57679 -Chemin scanne -Materiel cpu -Runs 3
# GPU — arrêter la pile, docker rm -f microservice_rgpd_ollama, puis relancer avec en plus $env:Llm__Enabled='true'
./mesure.ps1 -Base https://localhost:57679 -Chemin colle -Materiel gpu -Runs 4
./mesure.ps1 -Base https://localhost:57679 -Chemin scanne -Materiel gpu -Runs 3
```

L'ADR retient les médianes des essais à chaud. Le premier essai d'une série, s'il est à froid, est
écarté.

## Résultats

Les fichiers `mesures-<matériel>-<chemin>.json` contiennent les résultats bruts. Pour le chemin
collé, le premier essai GPU est à froid : il inclut le chargement du modèle, et le lot le plus long
y dure 20 s.
