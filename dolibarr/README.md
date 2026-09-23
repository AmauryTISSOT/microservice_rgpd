# Dolibarr : le second contexte d'intégration

Mémoire, Partie 3, IV — *Test de généricité : application à un second contexte*.

[Brocanto](../brocanto/README.md) a été écrite par l'auteur du mémoire, et donc peut-être avec les
capacités du microservice en tête. Pour écarter ce biais de conception, le microservice a été intégré
à une seconde application dont nous n'avons pas écrit le code : [Dolibarr](https://github.com/Dolibarr/dolibarr),
un ERP/CRM libre utilisé par les petites entreprises et les associations.

## Ce qui change par rapport à Brocanto

|                          | Brocanto        | Dolibarr        |
| ------------------------ | --------------- | --------------- |
| Langage de l'application | Python (Flask)  | PHP             |
| Base de données          | MariaDB         | MariaDB         |
| Colonnes analysées       | 69              | 5 382           |
| Mode de communication    | HTTP            | Bus RabbitMQ    |

Aucune ligne du microservice n'a changé entre les deux intégrations. Le passage de Brocanto à
Dolibarr, et du mode HTTP au mode RabbitMQ, s'est fait uniquement dans l'écran de paramétrage
(voir l'[ADR-0027](../docs/adr/0027-un-droit-un-seul-canal-une-adresse-http-ou-un-routage-rabbitmq.md)
et l'[ADR-0028](../docs/adr/0028-l-aboutissement-d-une-execution-cesse-d-etre-un-2xx-le-broker-accuse-reception.md)).

## Contenu du dossier

| Chemin | Contenu |
| --- | --- |
| [`mesure-detection/`](./mesure-detection/README.md) | Le banc qui mesure le temps de détection du modèle A2 sur les 5 382 colonnes de Dolibarr, sur CPU et sur GPU, avec les mesures brutes. |

Le schéma de Dolibarr lui-même se trouve dans le corpus annoté :
[`corpus/schemas/pivots/dolibarr.jsonl`](../corpus/schemas/pivots/dolibarr.jsonl). Sa source et sa
licence sont indiquées dans [`corpus/SOURCES.md`](../corpus/SOURCES.md).
