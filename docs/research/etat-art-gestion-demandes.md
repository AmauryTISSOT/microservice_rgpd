# État de l'art : comment les outils de gestion de demandes se branchent sur l'existant

> Recherche du ticket [#59](https://github.com/AmauryTISSOT/microservice_rgpd/issues/59).
> **Ce document donne la matière d'un choix ; il ne le fait pas.** Il ne prescrit ni mécanisme de branchement, ni partage de responsabilité pour notre service. Il relève ce que six éditeurs et quatre briques ouvertes documentent publiquement, et en dégage les points où ils convergent — donc où le problème contraint — et ceux où ils divergent — donc où notre décision se joue.

## Ce que la question demande

Le ticket pose six axes : le mécanisme de branchement, le partage de responsabilité, le modèle de découverte des données, la vérification d'identité, le format d'export, le modèle de déploiement. Deux d'entre eux portent la décision du projet et sont traités en priorité dans les Parties 2 et 3 : **qui exécute réellement l'effacement**, et **comment l'outil sait où vivent les données**. Le reste est instruit, mais subordonné.

Terrain couvert : OneTrust, Transcend, DataGrail, Didomi, Osano, Ketch, plus les briques ouvertes trouvées par recherche active (Partie 7).

---

# Partie 0 — Ce que la documentation permet et ne permet pas d'affirmer

Une contrainte de méthode structure tout ce document : **la disponibilité de documentation technique de première main varie d'un facteur considérable d'un éditeur à l'autre, et cette variation est elle-même un résultat.**

| Éditeur | Documentation technique publique | Ce qu'on peut affirmer |
| --- | --- | --- |
| **Transcend** | Centre d'aide et référence d'API entièrement ouverts, schémas de charge utile publiés, dépôts GitHub actifs | Presque tout |
| **DataGrail** | `docs.datagrail.io` ouvert, spécification REST et schéma de webhook publiés | Presque tout, sauf le format d'export |
| **Ethyca / Fides** | Code source Apache-2.0 intégral | Tout, y compris ce que le marketing tait |
| **Didomi** | `developers.didomi.io` ouvert mais mince sur le sujet | Assez pour conclure que le produit ne fait presque rien |
| **OneTrust** | `developer.onetrust.com` public ; **`my.onetrust.com` derrière authentification** | La façade d'API ; pas le contrat de bout en bout |
| **Osano** | `developers.osano.com` ouvert et riche ; base de connaissances `docs.osano.com` inaccessible (HTTP 502) | Le contrat de satisfaction, entièrement ; le reste par extraits indexés |
| **Ketch** | **`docs.ketch.com` entièrement derrière Auth0** ; en revanche dépôts publics et spécification OpenAPI ouverts | Le contrat de satisfaction, entièrement ; le catalogue de connecteurs, pas du tout |

**Conséquence pratique.** Le portail produit de OneTrust (`my.onetrust.com`) est une application Salesforce Experience Cloud qui ne rend rien à un client non authentifié : seuls les titres d'articles et les extraits de recherche sont accessibles. Aucune affirmation de ce document sur le comportement interne de OneTrust ne s'appuie donc sur son manuel utilisateur ; tout vient de `developer.onetrust.com`, qui est bien une source de première main mais ne décrit que la surface d'API.

Même situation chez **Ketch** : toute page de contenu de `docs.ketch.com` redirige en 302 vers `https://global.ketchapi.com/harbormaster/oauth/readmecom/init` puis vers `ketch.us.auth0.com/authorize`. Seule la page d'accueil est publique. Tout ce que ce document affirme de Ketch vient de ses dépôts GitHub publics et de sa spécification OpenAPI publiée — qui, elles, sont substantielles. Chez **Osano**, `docs.osano.com` a répondu 502 sur chaque tentative ; les affirmations issues de cette base sont signalées par la mention **[extrait indexé]** et n'ont pas la même valeur probante que le reste.

Il y a là une inversion notable : **les deux éditeurs dont le manuel utilisateur est le moins accessible publient le contrat de satisfaction le plus rigoureusement spécifié.** Ketch livre un OpenAPI complet ; Osano livre une implémentation de référence exécutable. Ce que l'on ne peut pas lire chez eux, c'est le catalogue de connecteurs — c'est-à-dire la partie commerciale.

**Deux affirmations couramment répétées sont écartées d'emblée**, faute de source de première main :

- le nombre de « 500 connecteurs préconstruits » de OneTrust n'est corroboré nulle part sur son portail développeur ;
- le terme **« silent connector »**, très présent dans les comparatifs tiers, **n'apparaît dans la documentation d'aucun des six éditeurs**. Ce n'est pas un terme d'éditeur.

Chaque fois que ce document écrit **« non documenté publiquement »**, c'est une conclusion, pas un aveu de recherche incomplète : elle signifie qu'une intégration décidée sur cette base engagerait le projet sur une promesse invérifiable.

---

# Partie 1 — Le mécanisme de branchement

## Transcend — le plus complètement spécifié

Transcend appelle **Data Silo** tout système connecté et documente cinq façons de brancher un système interne ([Connecting internal data systems](https://docs.transcend.io/docs/articles/dsr-automation/connecting-data-systems/internal-data-systems)) :

| Type | Ce que la documentation en dit |
| --- | --- |
| **Custom Functions** | « Serverless functions that run in Transcend. Specifically, Custom Functions execute on Sombra. » |
| **Server Webhook** | Le client héberge un serveur, Transcend y publie une tâche par demande. |
| **Cron Job** | « A script that interacts with our DSR API. Each time the script is run, it will retrieve all pending requests, run the corresponding internal workflow(s)…then notify our API of each job's completion. » Modèle **tiré**, sans écouteur HTTP. |
| **Direct Database Connection** | « Transcend can integrate directly with your database. » Requêtes SQL rédigées dans la console. |
| **Automated Vendor Coordination** | Pour les systèmes non programmables : « an AVC integration automatically notifies (via email or in-app) the assignees. » |

La configuration est décrite comme du code : fichier `transcend.yml` et interface en ligne de commande ([`transcend-io/tools`](https://github.com/transcend-io/tools/tree/main/packages/cli)), plus un fournisseur Terraform officiel ([`terraform-provider-transcend`](https://github.com/transcend-io/terraform-provider-transcend), [ressource `data_silo`](https://registry.terraform.io/providers/transcend-io/transcend/latest/docs/resources/data_silo)).

## DataGrail — trois chemins nommés pour les systèmes propres au client

DataGrail distingue explicitement trois familles ([Integrations overview](https://docs.datagrail.io/docs/integrations/overview/)) : **API Integrations** (catalogue de partenaires SaaS), **Internal Systems Integrations** — « allow you to programmatically include first-party data systems on your Privacy Requests » — et **Direct Contact Integrations**, « an integrated email workflow ».

Pour les systèmes internes, trois mécanismes ([Internal systems overview](https://docs.datagrail.io/docs/integrations/internal-systems-integrations/overview/)) :

1. **Request Manager Agent**, recommandé : « The Agent allows you to define the desired business logic in your systems while maintaining a standardized interface with DataGrail. » Distribué en **image Docker**, « enabling deployment in virtually any containerized environment », et surtout **sortant uniquement** : « it only makes outbound connections and never accepts inbound traffic » et « polls DataGrail for work » ([Request Manager Agent](https://docs.datagrail.io/docs/integrations/internal-systems-integrations/request-manager-agent/overview)).
2. **Webhooks**, adaptés aux « systems that can process requests asynchronously and do not require immediate feedback to DataGrail ».
3. **Direct Contact**, formulaire hébergé par DataGrail et journal d'audit, pour un faible volume.

## OneTrust — une façade d'API, un agent de découverte, et un trou

Trois mécanismes apparaissent dans les sources de première main :

- **Connecteurs préconstruits exécutés par OneTrust** : attesté pour le chemin Salesforce, qui exige une application connectée OAuth 2.0 et un réglage « Enable Data Subject Deletion ». L'API correspondante existe : [`DELETE` Data Subjects](https://developer.onetrust.com/onetrust/reference/deletedatasubjectprofilesusingdelete), « Requests will be processed asynchronously and can be monitored in the View Activity option ».
- **Webhooks** : la page [Integrating with webhooks](https://developer.onetrust.com/onetrust/reference/integrating-with-webhooks) définit ce qu'est un webhook en termes génériques — « Webhooks are HTTP callbacks that get triggered in response to a specific event within a system » — et **ne contient ni catalogue d'événements, ni schéma de charge utile, ni spécification d'authentification, ni contrat de réponse**.
- **Agent sur site : le Data Discovery Worker Node.** C'est la seule brique OneTrust qui tourne chez le client. Sa documentation est nette sur sa portée : « these endpoints interact directly with the worker node », et l'API « does not communicate with the OneTrust application » ([Custom scan using worker node APIs](https://developer.onetrust.com/onetrust/reference/custom-scan-using-worker-node-apis)). Un **SDK Java** permet d'écrire des scanneurs personnalisés contre ce nœud ([Adding SDK to app](https://developer.onetrust.com/onetrust/docs/adding-sdk-to-app)).

**Point dur : le schéma JSON que OneTrust envoie à un webhook client quand une demande doit être satisfaite n'est pas documenté publiquement.** C'est le contraste le plus vif avec Transcend et DataGrail, qui publient tous deux le leur.

## Didomi — pas de branchement du tout

Le produit *Privacy Requests* de Didomi est une API REST CRUD sur un objet de type ticket : `POST /dsar/requests`, `PATCH /dsar/requests/{id}`, `GET /dsar/requests` ([Privacy Requests API](https://developers.didomi.io/api-and-platform/privacy-requests/requests)).

Les webhooks de Didomi **ne couvrent pas les demandes d'exercice de droits**. La liste publiée est `event.created`, `event.updated`, `event.deleted`, `user.created`, `user.updated`, `user.deleted` ([Webhooks](https://developers.didomi.io/integrations/generic-integrations/webhooks)) — soit des événements de consentement et de cycle de vie utilisateur, rien d'autre. Les autres chemins d'intégration documentés (envoi temps réel, API Consents en lecture, export quotidien NDJSON gzippé vers S3 ou GCS) portent tous sur le **consentement**, pas sur l'acheminement d'une demande.

Le dépôt public [`github.com/didomi`](https://github.com/didomi) ne contient **que** des SDK de plateforme de gestion du consentement et des exemples (`react-native`, `flutter`, `unity`, `consent-string`, `iabtcf-es`…) : **aucun dépôt lié aux demandes d'exercice de droits**. Cela corrobore l'absence de surface d'intégration.

## Osano — un catalogue dont les capacités réelles sont publiées

Osano documente trois familles d'intégrations ([developers.osano.com/integrations](https://developers.osano.com/integrations/)) : **Data Discovery Integrations** (155 entrées), **Source Discovery Integrations** (12), **Privacy Signal Integrations** (2).

Le document le plus utile de tout le corpus pour évaluer un catalogue est sa [liste des capacités par connecteur](https://developers.osano.com/integrations/data-discovery-integrations/list-of-capabilities), qui distingue deux colonnes : **Summarization** et **User Deletion**. Tous les connecteurs listés savent résumer ; **environ la moitié seulement savent supprimer**. Hubspot, Salesforce, Mailchimp, Zendesk, Dropbox, Freshdesk et ServiceNow font les deux ; Stripe, Square, Braintree, Chargebee, GitHub, Jira, Slack et Shopify **ne savent que résumer**.

C'est le seul éditeur du terrain qui publie l'aveu ligne à ligne que « connecté » ne veut pas dire « effaçable ». Voir Partie 8, patron P9.

L'accès direct aux bases passe par le nuage d'Osano avec des identifiants fournis par le client : Aurora RDS, BigQuery, Cosmos DB, Redshift, Snowflake ([Source discovery integrations](https://developers.osano.com/integrations/source-discovery-integrations/)). La [page RDS](https://developers.osano.com/integrations/source-discovery-integrations/RDS) exige clé et secret AWS, ARN de grappe, ARN de secret, et déclare un accès « read-only access to establish a connection » — **alors que la politique IAM demandée accorde `BatchExecuteStatement` et `ExecuteStatement`**, c'est-à-dire l'exécution d'instructions arbitraires. La tension entre l'intention déclarée et le privilège accordé mérite d'être relevée.

Pour tout ce qui n'est pas au catalogue : **webhooks et API REST** ([Webhooks](https://developers.osano.com/webhooks/), [Workflows](https://developers.osano.com/webhooks/workflows)). Aucun agent n'est déployé chez le client pour la satisfaction des demandes.

## Ketch — la documentation est fermée, le contrat est ouvert

Trois mécanismes, tous établis par des dépôts publics.

**Le Ketch Forwarder** — un webhook sortant spécifié en OpenAPI ([`ketch-com/ketch-forwarder`](https://github.com/ketch-com/ketch-forwarder), [`forwarder_gen.yaml`](https://raw.githubusercontent.com/ketch-com/ketch-forwarder/main/openapi/forwarder_gen.yaml)). La spécification s'annonce ainsi : « This specification defines a web protocol encoding a set of standardized request/response data flows such that Data Subjects can exercise Personal Data Rights and express Consent choices. » Implémentations de référence en Go et en Java ; celle en Express/Node est archivée.

**Le Ketch Agent** — [`ketch-sdk/ketch-agent`](https://github.com/ketch-sdk/ketch-agent), licence **MIT**, TypeScript, maintenu. Le fichier de présentation est explicite :

> « Ketch Agent is a containerized application that is deployed inside your VPC as part the transponder deployment. When the transponder is connected and authenticated to Ketch, the agent periodically checks for any DSR Right activity that needs to be executed. When a user invokes their DSR Right, the right is forwarded to the agent which then **downloads your nodejs module and executes it dynamically**. »

Le contrat d'entrée est une seule fonction :

```ts
HandleRequest = (request: DSRRequest, connectionConfig: ConnectionConfig) => Promise<DSRResponse>
```

où `ConnectionConfig` est « the key value pair of the connection parameters provided in the transponder UI » — les identifiants d'accès sont donc saisis dans la console Ketch puis injectés dans du code écrit par le client. L'exemple livré ([`examples/mongo.ts`](https://raw.githubusercontent.com/ketch-sdk/ketch-agent/main/examples/mongo.ts)) ouvre lui-même la connexion et exécute `restaurants.deleteOne({ email: req.request.subject.email })`.

**Le catalogue de connecteurs préconstruits** est revendiqué sur [ketch.com/developers](https://www.ketch.com/developers) mais **n'est documenté nulle part publiquement**. Il n'existe pas de SDK ouvert pour écrire une entrée de ce catalogue : le SDK ouvert de Ketch, c'est l'agent (écrire un module Node) et le forwarder (implémenter un webhook).

## Fides (Ethyca) — la brique ouverte, traitée en Partie 7

Fides est traité à part parce qu'il est le seul outil du terrain dont le code est intégralement lisible, ce qui en fait la source la plus fiable du document. Voir Partie 7.

---

# Partie 2 — Qui exécute réellement l'effacement

C'est le premier des deux points sur lesquels la décision du projet se joue. La réponse n'est pas binaire : **tous les outils qui font quelque chose font les deux, et la ligne de partage passe entre systèmes tiers connus et systèmes propres au client.**

## La règle générale

| | Systèmes SaaS tiers du catalogue | Systèmes propres au client |
| --- | --- | --- |
| **Transcend** | Transcend exécute, via Sombra | Au choix : Transcend exécute (base de données, fonction), ou le client exécute (webhook, cron) |
| **DataGrail** | DataGrail exécute | Le client exécute et rend compte |
| **Fides** | Fides exécute (appel HTTP `update`/`delete`) | Fides exécute (écriture SQL directe) |
| **Osano** | Osano exécute, **mais seulement pour la moitié des connecteurs** | Le client exécute et rend compte |
| **Ketch** | Non documenté publiquement | **Le client exécute**, sur les deux chemins publics |
| **OneTrust** | Exécute pour un sous-ensemble ; sinon notifie | Notifie ; le client rend compte |
| **Didomi** | Rien | Rien |

## Transcend : la double voie, explicitement documentée

**L'outil exécute.** Pour les connecteurs et les bases, Transcend « make[s] the corresponding API call to act on the data (download, delete, opt out, etc.) » ([DSR automation overview](https://docs.transcend.io/docs/articles/dsr-automation/overview)). L'appel transite par la passerelle auto-hébergée : « Transcend cannot directly query data from your business system—Transcend can only request that Sombra make the request on Transcend's behalf, and Sombra only responds with encrypted data » ([Sombra introduction](https://docs.transcend.io/docs/articles/sombra/introduction)).

**Le client exécute.** Le silo *Server Webhook* ([documentation](https://docs.transcend.io/docs/silo-server)) impose au serveur client quatre obligations : valider l'en-tête `x-sombra-token`, faire le travail — « returning or removing rows from a database, returning or removing file from a filesystem » —, répondre `200` pour accuser réception, puis, hors bande, « send a POST request to Transcend that indicates that processing has been completed ».

La charge utile est publiée intégralement ([New privacy request job](https://docs.transcend.io/docs/api-reference/webhook/new-privacy-request-job)) :

```json
{
  "type": "ACCESS",
  "requestIdentifier": { "value": "mikebrook@transcend.io" },
  "coreIdentifier": { "value": "some.user.id" },
  "dataSubject": { "type": "customer" },
  "isTest": false,
  "extras": { "profile": {}, "dataSilo": {}, "request": {}, "organization": {} }
}
```

Authentification par `x-sombra-token`, « a token used for webhook authentication, containing a JSON Web Token (JWT) asymmetrically signed with the ES384 algorithm », plus un `x-transcend-nonce` que le client doit renvoyer au moment de rendre compte.

L'énumération de `type` est large et couvre les six droits du RGPD plus les opt-out américains : `ACCESS`, `ERASURE`, `RECTIFICATION`, `RESTRICTION`, `BUSINESS_PURPOSE`, `PLACE_ON_LEGAL_HOLD`, `AUTOMATED_DECISION_MAKING_OPT_OUT`, `SALE_OPT_OUT`, `TRACKING_OPT_OUT`, `CUSTOM_OPT_OUT` et leurs variantes `_OPT_IN`.

Contrat de réponse : `200` = mis en file, résultat rendu plus tard ; `204` = rien à faire, tâche marquée `SKIPPED` ; `4xx` = Transcend réessaie **cinq fois, à raison d'une fois par heure**. Le compte rendu se fait par `POST /v1/data-silo` (accès) ou `PUT /v1/data-silo` (effacement, opposition) ([Responding to DSRs](https://docs.transcend.io/docs/articles/dsr-automation/api-integration/responding-to-dsrs)), avec un statut dans `READY | RESOLVED | SKIPPED | SKIPPED_DUE_TO_EXCEPTION | ACTION_REQUIRED | WAITING`. Absence de donnée : renvoyer `null`, `[]` ou `{}` pour l'accès, `204` pour l'effacement. Des implémentations de référence en JavaScript, TypeScript, Python et Ruby sont publiées ([`transcend-io/examples`](https://github.com/transcend-io/examples)).

## DataGrail : deux contrats de fil, publiés

**Webhook** ([documentation](https://docs.datagrail.io/docs/integrations/webhooks/)). DataGrail publie, le client exécute, le client rend compte par `PUT` sur `webhook_callback_url`. Signature `X-Webhook-Signature` : « HMAC-SHA256 signature to verify the request originated from DataGrail », calculée sur la concaténation de `X-Webhook-Timestamp` et du corps. Quatre types de notification : demande de confidentialité, **health check**, **readiness check**, **reminders**. Le corps d'un contrôle de santé :

```json
{
  "request_type": "health_check",
  "integration_name": "Webhook Integration",
  "integration_kind": "webhook",
  "webhook_request_id": "3aa32ca6-e60d-4b71-981a-d1ba258fd712"
}
```

Les résultats d'accès sont acceptés en base64 en ligne ou par référence à un fichier distant, « Max upload limit of all files is 200 MB ».

**API implémentée par le client** ([spécification](https://docs.datagrail.io/docs/integrations/internal-systems-integrations/legacy-solutions/api-specification)) — le client expose une API REST au format imposé :

```
GET  /api/v1/hc
GET  /api/v1/connections/list
POST /api/v1/privacy/identifiers/:connection-uuid
POST /api/v1/privacy/access/:connection-uuid
POST /api/v1/privacy/delete/:connection-uuid
POST /api/v1/privacy/optout/:connection-uuid
POST /api/v1/results/retrieve
```

DataGrail interroge `/api/v1/results/retrieve` « every 15 minutes for up to 3 days ». Ce chemin est classé sous **`legacy-solutions`** : l'agent Docker l'a remplacé comme recommandation.

**Un garde-fou humain est documenté dans le cycle d'effacement** : les *Privacy Managers* « review retrieved data in the Pending Action state and select what to delete », après quoi seulement « all selected API integrations will initiate deletion » ([Deletion lifecycle](https://docs.datagrail.io/docs/request-manager/request-processing/privacy-right/deletion)). L'effacement n'est donc pas déclenché sans validation humaine.

## Osano : le modèle des *action items*, et une implémentation de référence publiée

Osano structure tout autour d'un objet nommé **action item**, et sépare explicitement deux natures de source de données [extrait indexé] : les **automated data stores**, connectés au catalogue, où « searches of these data sources will begin automatically » une fois l'identité confirmée ; et les **manual data stores**, « used for data sources not directly connected to Osano », où « an action item is automatically assigned to a Subject Rights Assignee to perform a manual search for personal data in systems outside the Osano Platform ».

Par défaut, un humain valide : « Once the search is complete, an action item is automatically assigned to a Subject Rights Assignee to validate the completeness and accuracy of the search results. » Avec la satisfaction automatique activée, « action items are no longer assigned to assignees; instead, the Assignee field will display as "Automated" ».

**Pour une application propre au client, le contrat est public et exécutable** : Osano publie une implémentation de référence complète, [`osano/webhook-sample-apps`](https://github.com/osano/webhook-sample-apps), dossier `sample-dsar-handler/`. C'est l'artefact le plus proche de la forme de notre projet dans tout le corpus.

Charge utile entrante — trois champs, rien de plus :

```json
{ "email": "user@example.com", "dsarActionItemId": 123, "requestedAction": "DELETE" }
```

Le code d'aiguillage est un `switch (requestedAction)` sur `SUMMARIZE`, `DELETE`, avec un commentaire par défaut prévoyant « SUMMARIZE, DELETE, OPT_OUT, etc. ». Le compte rendu se fait en deux appels :

```
POST  https://api.osano.com/v1/subject-rights/action-items/{actionItemId}/summaries   (en-tête x-osano-api-key, corps = le fichier)
PATCH https://api.osano.com/v1/subject-rights/action-items/{actionItemId}             { "status": "COMPLETED" }
```

Le fichier `integration.js` de l'exemple énonce la séquence sans ambiguïté : chercher les données du demandeur dans **votre** base par courriel, supprimer l'utilisateur de **votre** base, joindre le résumé, marquer terminé. **Osano notifie, l'application efface, l'application rend compte.**

Deux limites notables. L'exemple appelle `markActionItemCompleted` même quand aucun utilisateur n'est trouvé : **le cas « pas de donnée » n'est pas modélisé** comme un statut distinct. Et **aucune vérification de signature du webhook n'est documentée** — seulement un en-tête `Authorization: Bearer` que le client configure lui-même, avec des reprises automatiques (« Osano also retries failed webhook calls automatically », [Workflows](https://developers.osano.com/webhooks/workflows)).

Les événements déclencheurs documentés ([Webhook variables](https://developers.osano.com/webhooks/variables)) éclairent le cycle de vie : *Email Verified*, *Identity Verified*, *Action Item Generated*, *Manual Action Item Generated*, *Action Items Completed*, *Request Rejected*, *Request Completed*, *New Message from Requester*.

## Ketch : le client exécute, sur les deux chemins publics

Le forwarder et l'agent partagent la même forme d'enveloppe, `apiVersion: dsr/v1`, avec un `kind` dans `AccessRequest | CorrectionRequest | DeleteRequest | RestrictProcessingRequest` — quatre des six droits du RGPD, ni portabilité ni opposition. La demande porte `identities[]`, `subject`, `context`, `submittedTimestamp`, `dueTimestamp` et `callbacks[]` (chacun `{url, headers}`).

La réponse porte `status` — `unknown | pending | in_progress | completed`, augmenté de `cancelled` et `denied` côté agent — un `reason`, un `expectedCompletionTimestamp` et un `requestID` décrit comme « the request ID known in the destination system ». La complétion asynchrone remonte par un `Event` de type `AccessStatusEvent`, `CorrectionStatusEvent`, `DeleteStatusEvent` ou `RestrictProcessingStatusEvent`.

**L'énumération des motifs de refus est la plus riche du corpus** : `need_user_verification`, `requested`, `insufficient_identification`, `executed`, `suspected_fraud`, `insufficient_verification`, `no_match`, `claim_not_covered`, `outside_jurisdiction`, `too_many_requests`, `other`. On y reconnaîtra, presque mot pour mot, l'énumération de refus du Data Rights Protocol (Partie 7) — Ketch a aligné son vocabulaire sur la spécification ouverte.

L'exemple MongoDB livré rend `status: completed, reason: executed` **après avoir fait la suppression lui-même**. C'est le contrat en miniature : **Ketch ne touche jamais à l'enregistrement.**

Pour le chemin des connecteurs préconstruits — celui où Ketch exécuterait vraisemblablement l'effacement contre des API SaaS — **le contrat de satisfaction n'est pas documenté publiquement.**

## OneTrust : orchestrateur, pas exécutant

La surface d'API documentée est celle d'un suivi de travail : files de demandes, étapes, sous-tâches, codes de résolution. L'indice le plus net est l'endpoint [Add Targeted Data Discovery Results Summary to Request](https://developer.onetrust.com/onetrust/reference/datadiscoveryupdatesusingpost) :

```
POST https://{hostname}/api/datasubject/v2/datadiscovery/requestqueues/{requestQueueRefId}
```

Le corps exige un champ `system` — « Name or identifier of the system from which this data is sourced » — et transporte `results`, `unstructuredResults` et `attachments`. C'est une **poussée entrante de constats dans le dossier de demande** : le contrat classique « un système externe a fait le travail, il vient le déclarer ».

## Didomi : le contrat de satisfaction tient en une chaîne de caractères

Le client fait tout — découverte, effacement, export — hors de Didomi, qui suit un statut et envoie des courriels. Statuts : `unverified`, `verified`, `work_in_progress`, `fulfilled`, `archived`, `refused`. Sur un `PATCH`, « Only `status`, `metadata`, and `extra_message_variables` are editable ».

Pour livrer les données, le client héberge l'export et donne une URL à Didomi : « Provide your end-user download URL in: `extra_message_variables.download_link` » lors du passage à `fulfilled`, afin que « the email includes a button/link that redirects users to access/download their data » ([Privacy Requests API](https://developers.didomi.io/api-and-platform/privacy-requests/requests)).

**Didomi ne touche jamais aux données personnelles.** Pas d'exécution d'effacement, pas de schéma de rappel, pas de contrat structuré de remise de travail : un statut et un champ d'URL.

## Fides : l'outil exécute, et le code le montre

Fides est le seul cas où l'affirmation est vérifiable dans le code source. Pour une base de données, il génère et exécute lui-même les instructions de mise à jour, en appliquant une **stratégie de masquage** par champ — `null_rewrite`, `string_rewrite`, `hash`, `hmac`, chiffrement AES ([Masking strategies](https://docs.ethyca.com/docs/dev-docs/privacy-requests/masking-strategies)). Le masquage plutôt que la suppression est justifié par le maintien de l'intégrité référentielle.

Pour un système SaaS, le connecteur est un fichier YAML déclarant les appels HTTP que Fides émettra. Extrait verbatim de [`data/saas/config/mailchimp_config.yml`](https://github.com/ethyca/fides/blob/main/data/saas/config/mailchimp_config.yml) :

```yaml
    - name: member
      requests:
        read:
          method: GET
          path: /3.0/search-members
          query_params:
            - name: query
              value: <email>
          param_values:
            - name: email
              identity: email
          data_path: exact_matches.members
        update:
          method: PUT
          path: /3.0/lists/<list_id>/members/<subscriber_hash>
          ...
          body: |
            {
              <masked_object_fields>
            }
```

C'est bien Fides qui émet le `PUT` porteur des champs masqués.

Mais Fides documente aussi ses deux voies de repli, et elles sont instructives :

- **Les *policy webhooks*** ([documentation fidesops](https://ethyca.github.io/fidesops/guides/policy_webhooks/)) : « HTTPS Callback that you've defined on a Policy to call an external REST API endpoint *before* or *after* a Privacy Request executes ». La charge utile est publiée, et surtout la réponse permet au client de **renvoyer une identité dérivée** et de **suspendre l'exécution** :

```json
{
  "derived_identity": { "email": "customer-1@gmail.com", "phone_number": "555-5555" },
  "halt": "true | false"
}
```

  « Derived identity is optional: a returned email or phone number will replace currently known emails or phone numbers. »

- **Le connecteur *Generic Erasure Email*** : quand Fides ne peut pas atteindre un système, il **envoie un courriel hebdomadaire** à une adresse générique contenant les identités de toutes les personnes ayant demandé un effacement dans la semaine, « to notify a third-party vendor of their obligations to erase any personal data associated with the provided identities » ([Generic erasure email integration](https://www.ethyca.com/docs/user-guides/integrations/email-integrations/generic-erasure-emails)).

Ce dernier point est la donnée la plus honnête du corpus : **l'outil open source le plus automatisé du terrain assume qu'une partie de la couverture se termine en courriel.** Voir Partie 8.

---

# Partie 3 — Comment l'outil sait où vivent les données

Second point décisif. Trois modèles coexistent, et **aucun outil n'en retient un seul**.

## Modèle A — la cartographie déclarative, exigée partout

C'est le socle commun : un inventaire de systèmes, de champs et de catégories de données, tenu par des humains ou par du code d'infrastructure. Transcend l'appelle **Data Inventory / Data Map** ([documentation](https://docs.transcend.io/docs/the-data-map)) et y définit la notion de **datapoint** : « A *data system* contains a set of *datapoints*, which are categories of data on your server. » OneTrust l'appelle **Data Mapping Automation**, exposé par [`GET /api/inventory/v2/inventories/{schemaName}`](https://developer.onetrust.com/onetrust/reference/getlistofinventoriesusingget). DataGrail l'appelle **Live Data Map**, alimenté notamment par saisie manuelle ([LDM overview](https://docs.datagrail.io/docs/ldm/ldm-overview/)). Chez Osano, l'équivalent est la liste des *data stores*, où la distinction entre **automated** et **manual** est elle-même une déclaration humaine : c'est un humain qui décide quels systèmes l'outil peut atteindre et lesquels resteront des tâches assignées.

Fides pousse cette exigence le plus loin, jusqu'à en faire une condition de validité de la configuration : voir Modèle C.

## Modèle B — la découverte automatique, et ce qu'elle découvre vraiment

Le mot « découverte » recouvre deux choses très différentes, qu'il faut séparer.

**Découverte de *systèmes*** — trouver quelles applications existent. DataGrail le fait par **introspection du fournisseur d'identité** : « DataGrail integrates with popular SAML/SSO providers (i.e. Okta, Entra ID, Google Apps) », croisé avec des catalogues tiers ([LDM overview](https://docs.datagrail.io/docs/ldm/ldm-overview/)). Transcend fait de même avec **Silo Discovery**, qui « leverages pre-built plugins to scan, discover, and automatically catalog all data systems » en scrutant les fournisseurs SSO, AWS et les manifestes de paquets ([Silo discovery](https://docs.transcend.io/docs/silo-discovery)).

Il n'existe **aucune documentation de première main** attestant une découverte par analyse du trafic réseau, du DNS ou d'une extension de navigateur chez DataGrail. Ce que le marketing appelle « AI-powered system detection » se réduit, dans les documents, à : lecture de l'annuaire d'identité + agent conteneurisé + saisie manuelle.

Osano fait exactement la même chose et l'instrumente jusque dans ses webhooks : les variables `{{numAppsDiscovered}}`, `{{numAppsDeactivated}}`, `{{ssoConnectionName}}` et les événements *SSO Applications Discovered* / *SSO Applications Deactivated* ([Webhook variables](https://developers.osano.com/webhooks/variables)) montrent que la découverte de systèmes est, littéralement, une lecture de l'annuaire SSO — complétée par les plateformes de données clients (Segment, RudderStack).

**Le cas Ketch invite à la prudence.** Son produit de découverte, *Data Sentry*, ne dispose d'**aucune documentation développeur publique**. Tout ce qui est public — page produit et communiqué de lancement — le décrit comme un **plan de données de façade** : analyse en temps réel des paquets quittant le site, l'application ou la télévision connectée. **Rien ne permet d'affirmer qu'il découvre des enregistrements dans une base.** Un nom de produit contenant « data map » ne dit pas de quel côté du système il regarde.

**Découverte de *champs*** — trouver, dans un système donné, quelles colonnes portent des données personnelles. Osano décrit un balayage qui « searches for pre-identified fields that likely contain personal information, tags those fields, and suggests appropriate classifications » [extrait indexé], avec un compteur d'*Unclassified Fields* par fournisseur synchronisé, et **une limite explicitement assumée** sur sa page produit : « Osano Data Discovery supports querying structured data, but not unstructured data » ([Data discovery](https://www.osano.com/updates/data-discovery)). Transcend est le seul à publier sa cascade de classification, avec le **lieu d'exécution de chaque étage** ([Developing classifications](https://docs.transcend.io/docs/structured-discovery/developing-classifications)) : correspondance de noms de propriétés puis correspondance d'expressions régulières, toutes deux dans Sombra ; puis un **modèle de langue exécuté dans l'environnement du client** — « Runs within your environment for maximum data security », « Analyzes up to 200 characters of sample data » ; en repli, un modèle hébergé par Transcend qui « analyzes only metadata (table and column names, descriptions) » et « does not process actual data content ». DataGrail a l'équivalent sous le nom **Responsible Data Discovery** : « DataGrail utilizes a containerized API agent to locate and identify structured, semi-structured, and unstructured data ». OneTrust a ses **worker nodes** et son SDK de connecteur de découverte.

## Modèle C — la résolution d'identifiants, et son absence de graphe

C'est le point le plus contre-intuitif du corpus.

Une demande arrive avec un identifiant — un courriel. Les données de la personne vivent sous d'autres identifiants ailleurs — identifiant client interne, identifiant publicitaire. Comment fait-on le pont ?

**Transcend : à la volée, à chaque demande, pas dans un graphe persistant.** Le mécanisme s'appelle **Preflight / Identity Enrichment** : « Preflight checks take in a single identifier (e.g. an email) and can return many identifiers » ([Preflight checks](https://docs.transcend.io/docs/dsr-automation/configuring-requests/preflight-checks), [Identity enrichment](https://docs.transcend.io/docs/identity-enrichment)). Trois types d'enrichisseurs : webhook, base de données, entrepôt via Looker. Pour le webhook, le client renvoie à Sombra sur `/v1/enrich-identifiers` ([documentation](https://docs.transcend.io/docs/articles/dsr-automation/preflight-and-enrichment/preflight-check-post-to-a-server-webhook)) :

```json
{ "enrichedIdentifiers": {
    "googleAnalyticsInternalId": [ { "value": "SOME-GOOGLE-ANALYTICS-ID" } ],
    "gpadvid": [ { "value": "b20f48d8-9f50-447a-a446-1d398a9f9b1b" } ] } }
```

**Aucune documentation de graphe d'identité persistant chez Transcend.** La résolution est refaite à chaque demande. Cela n'a rien d'un défaut : un graphe d'identité persistant serait lui-même un traitement de données personnelles à justifier.

**Fides : un graphe, mais un graphe de *schéma*, pas d'identités.** C'est le modèle le plus intéressant du corpus parce qu'il est entièrement lisible. Un **Dataset** annote un schéma de base ; deux clés de `fides_meta` portent tout ([Datasets](https://ethyca.github.io/fidesops/guides/datasets/)) :

- **`identity`** : « Signifies that this field is an identity value that can be used as the root for a traversal ».
- **`references`** + **`direction`** : « If the direction is `to`, fidesops will only use data in the *source* collection to discover data in the *referenced* collection. If the direction is `from`, fidesops will only use data in the *referenced* collection to discover data in the *source* collection. »

À l'exécution, Fides « uses your Datasets to generate a *graph* of the resources » et parcourt : « Identify the collections that contain the identity data that belong to the user. Find all related records. Use the data to find all connected data. Continue until we've found all related data » ([Query execution](https://ethyca.github.io/fidesops/guides/query_execution/)).

Le garde-fou est net et vaut d'être noté : **« It's an error to specify a collection in your Dataset can't be reached through the relations you've specified. »** Un système déclaré mais non atteignable depuis une racine d'identité fait échouer la configuration. C'est le seul dispositif du corpus qui **refuse une carte incomplète** au lieu de la subir.

**Ketch : le seul dont le contrat lui-même est en forme de graphe d'identités.** Là où les autres transportent un identifiant, l'enveloppe `dsr/v1` de Ketch transporte un **tableau** d'objets `Identity = {identitySpace, identityFormat, identityValue}`, où `identityFormat` vaut `raw`, `md5` ou `sha1`. Ketch résout donc la personne dans plusieurs espaces d'identité **en amont**, et transmet l'ensemble à chaque système. S'y ajoute une carte `context` décrite comme « additional non-identity context that have been added via identity verification or other augmentation methods ». La résolution est faite par la plateforme, jamais demandée au système receveur.

**OneTrust : non documenté publiquement.** Le marketing décrit un balayage « enterprise-wide for requestor information such as email, phone, and logins ». Aucun composant de résolution d'identifiants n'est documenté sur le portail développeur.

**Didomi : rien.** Didomi n'a aucun modèle de découverte : son *Data Manager* décrit des finalités de consentement et une taxonomie de droits (`cpra_access_my_data`, `cpra_delete_my_data`, `cpra_opt_out` — [User rights](https://developers.didomi.io/api-and-platform/data-manager/user-rights)), pas un inventaire de données personnelles.

**DSRHub** (Partie 7) nomme explicitement le problème et en fait une étape de flux de travail : un pas nommé **« Exchange Identity »** sert « to enrich the context of an identity, which helps services that are not tied to the orignal identity format ».

---

# Partie 4 — La vérification d'identité

| Outil | Portée | Mécanismes documentés |
| --- | --- | --- |
| **Transcend** | Déléguée à l'authentification du client, médiée par la passerelle | Lien magique par courriel (préconfiguré), **JWT** (« If you maintain your own authentication code for account login, we recommend the JWT strategy »), **OAuth 2** — « OAuth 2 verification is performed with Sombra…meaning you don't have to trust Transcend to authenticate the user » ([Identity verification](https://docs.transcend.io/docs/articles/privacy-center/identity-verification-concept)) ; code SMS Twilio ; pièce d'identité via [Stripe Identity](https://docs.transcend.io/docs/articles/dsr-automation/preflight-and-enrichment/preflight-check-government-id-verification-via-stripe-identity) |
| **DataGrail** | Portée par l'outil | Lien courriel valable 7 jours avec relance à 24 h ; étape SMS optionnelle ; flux de mandataire avec double vérification et dépôt de preuve ; *Smart Verification* breveté, à points, s'appuyant sur « additional data points from supported systems of record (Salesforce, Shopify, Marketo) » ([Verification](https://docs.datagrail.io/docs/request-manager/request-intake/verification/)) |
| **Didomi** | Courriel seul | Statut initial `unverified`, lien de confirmation dont « link lifetime is 900 seconds (15 min) » |
| **Osano** | Portée par l'outil, à deux étages | Lien magique par courriel ; puis, pour les types de demande sensibles, **dépôt d'une pièce d'identité relue manuellement** par le gestionnaire avant poursuite [extrait indexé]. Endpoint réel : `POST /v1/subject-rights/requests/{dsarId}/identification` ([API REST](https://developers.osano.com/customer-rest-api)). Deux événements distincts, *Email Verified* et *Identity Verified*. **Les recherches automatiques ne démarrent qu'après confirmation** |
| **Ketch** | Portée par Ketch, en amont, avec droit de refus en aval | Les méthodes ne sont **pas documentées publiquement**, mais l'énumération de refus du contrat prouve le partage : le système receveur peut répondre `need_user_verification`, `insufficient_identification`, `insufficient_verification`, `suspected_fraud` |
| **Fides** | Portée par l'outil, optionnelle | Code à six chiffres envoyé par courriel, à ressaisir dans le Privacy Center |
| **OneTrust** | **Non documenté publiquement** | Un endpoint [Get list of verification methods](https://developer.onetrust.com/onetrust/reference/getallv2verificationmethodsusingget) existe et « retrieve[s] a list of all verification methods for the specified request » — mais **le schéma de réponse et l'énumération des méthodes ne sont pas rendus publics** |

**Un réglage par défaut de DataGrail mérite d'être relevé** parce qu'il révèle une décision de conception partagée mais rarement dite : « **Only requests submitted through the Privacy Request Center require verification by default. Requests submitted through email, phone, and API are automatically considered verified.** » Autrement dit, une demande arrivant par API est réputée déjà vérifiée par l'appelant. La vérification est une propriété du **canal d'entrée**, pas de la demande.

**Un second point, plus gênant.** Dans le contrat d'Osano comme dans celui de Ketch, **le système qui exécute ne reçoit aucune preuve de la vérification** : Osano transmet `{email, dsarActionItemId, requestedAction}`, rien d'autre. L'application cliente doit faire confiance à l'outil sans élément vérifiable. Notons aussi qu'**aucun des deux ne documente de signature de webhook** (pas de HMAC) : Osano propose un jeton porteur que le client configure, Ketch un couple en-tête/valeur libre. Comparé au JWT ES384 de Transcend et au HMAC-SHA256 de DataGrail, c'est un écart de rigueur qu'on ne devinerait pas depuis les pages produit.

**Un rappel de prudence.** Fides a publié en 2026 un avis de sécurité sur un **contournement de la vérification d'identité** : « An unauthenticated attacker who knows a target's email address and can reach the public Privacy Center can cause an erasure privacy request to be approved by an administrator and processed without identity verification » ([CVE-2026-42303](https://github.com/advisories/GHSA-qx5f-ghc2-7g5c), corrigé en 2.83.2). Un second avis, [CVE-2023-47114](https://github.com/advisories/GHSA-3vpf-mcj7-5h38), portait sur une injection HTML dans les paquets d'export au format HTML. La vérification d'identité et le format d'export sont l'un et l'autre des surfaces d'attaque réelles, pas des formalités.

---

# Partie 5 — Le format d'export

**Il n'existe aucun schéma d'export universel.** C'est un constat net, et il porte.

| Outil | Ce qui est documenté |
| --- | --- |
| **Transcend** | Archive **ZIP** téléchargée depuis le Privacy Center, plus un rapport **HTML hors ligne** ([Receiving DSRs](https://docs.transcend.io/docs/articles/privacy-center/receiving-data-subject-requests)). Le contenu est du **JSON indexé par les *datapoints* déclarés** : « The keys of the JSON should match the keys you defined for this integration ». Donc **schéma déclaré par le client**, pas schéma universel. Récupération en ligne de commande : `transcend request download-files` |
| **Fides** | Configurable par destination de stockage : **JSON, CSV, ou HTML compressé en ZIP** — ce dernier étant le plus courant ([avis CVE-2023-47114](https://github.com/advisories/GHSA-3vpf-mcj7-5h38), qui précise que seuls les déploiements configurés en HTML étaient concernés) |
| **DataGrail** | **Non documenté publiquement.** La documentation dit seulement que la personne reçoit « an email with a download link » et que « Any retrieved data will be uploaded to your cloud storage bucket » ([Access lifecycle](https://docs.datagrail.io/docs/request-manager/request-processing/privacy-right/access/)). Le conteneur concret n'est jamais nommé. Les rappels de webhook acceptent des fichiers arbitraires, ce qui implique l'absence de schéma imposé |
| **OneTrust** | Rapport de synthèse et pièces jointes remises via un portail chiffré, avec un module de **caviardage** (*DSAR Redaction*) appliqué aux fichiers avant publication. **Aucun schéma JSON d'export universel documenté** : le livrable est un rapport composé, pas un export lisible par machine |
| **Osano** | Le seul dont la chaîne complète est décrite. De l'application vers Osano : **octets arbitraires**, aucun schéma imposé (l'exemple envoie un `JSON.stringify(user)`). Des connecteurs automatiques : **un CSV par magasin de données** [extrait indexé]. Vers la personne : **une archive ZIP** contenant un **PDF** généré (résultats de découverte en tableaux, groupés par magasin) plus les fichiers joints, remise par le portail de messagerie sécurisée [extrait indexé] |
| **Ketch** | **Aucun format imposé.** Deux enveloppes agnostiques : côté forwarder, `AccessResponse.results` est un tableau de `{url, headers}` — on rend des URL que Ketch ira chercher ; côté agent, `AccessResponseBody.documents` accepte un champ `data` en ligne. « The Document object can look like a Callback object which allows Ketch to download the document using a simple HTTP GET. » Ni schéma, ni type MIME, ni format de portabilité |
| **Didomi** | Sans objet. Le client produit l'artefact et fournit `download_link` |

**Le fait notable est le stockage.** DataGrail dépose les résultats dans le seau de stockage **du client**, pas dans le sien. C'est une décision d'architecture qui vaut d'être retenue : le résultat d'une demande d'accès est le document le plus sensible de tout le processus, et deux éditeurs sur six évitent délibérément de le détenir.

---

# Partie 6 — Le modèle de déploiement et l'auto-hébergement

Contrainte cardinale du projet. Le résultat est tranché : **un seul outil du terrain commercial est auto-hébergeable de bout en bout, et il est open source.**

| Outil | Plan de contrôle | Composant chez le client | Auto-hébergeable ? |
| --- | --- | --- | --- |
| **Fides / Ethyca** | Auto-hébergé ou Fides Cloud | Tout | **Oui, intégralement** |
| **Transcend** | SaaS, jamais auto-hébergeable | **Sombra**, plan de données complet | **Partiellement — le plan de données, oui** |
| **Ketch** | SaaS (`app.ketch.com`, `global.ketchapi.com`) | **Transponder** conteneurisé dans le VPC du client, hébergeant le Ketch Agent | Non |
| **DataGrail** | SaaS (`https://<client>.datagrail.io`) | Request Manager Agent, agent RDD (Docker, sortants uniquement) | Non |
| **OneTrust** | SaaS multi-locataire | Data Discovery Worker Node (découverte seule) | Non |
| **Osano** | SaaS (`my.osano.com`, `api.osano.com`) | Aucun pour les demandes de droits | Non |
| **Didomi** | SaaS (`api.didomi.io`) | Aucun | Non |

## Sombra, le cas le plus instructif

Transcend a fait le choix d'architecture le plus net du terrain : **plan de contrôle SaaS, plan de données chez le client**.

- « Sombra is the part of Transcend which actually connects to your data—it's a containerized application which scans your data and operates on it » ([Sombra](https://docs.transcend.io/docs/articles/sombra)).
- Sans état : « a simple Node.js server that proxies requests, and has no storage requirements » ; « designed to scale horizontally… Each Sombra node is a worker… It does not store any persistent state, and it is fault tolerant » ([Architecture](https://docs.transcend.io/docs/articles/sombra/architecture)).
- Pas d'exposition entrante : un **tunnel inverse** « lets Transcend Cloud securely open a connection to Sombra in your private network or Virtual Private Cloud (VPC), without exposing it to the internet », ou AWS PrivateLink.
- Garde des clés : « Sombra manages the access keys to your business systems ». En auto-hébergé, « no unencrypted data is ever available to Transcend, and you keep your keys in-house » ; en Sombra hébergé par Transcend, « the keys are stored by Transcend's KMS ».
- Déploiement par Helm ([`helm-charts`](https://github.com/transcend-io/helm-charts)) ou Terraform ([`terraform-aws-sombra`](https://github.com/transcend-io/terraform-aws-sombra)), avec un **coût de référence publié** : « ~$170-$180/month » pour le module AWS livré tel quel ([Sombra self-hosting costs](https://docs.transcend.io/docs/articles/security/end-to-end-encryption/sombra-self-hosting-costs)).

Transcend publie donc, chiffres à l'appui, ce que coûte le fait de ne pas lui confier ses données. C'est la documentation la plus honnête du corpus sur ce point.

## Le Transponder de Ketch, et ce qu'on n'en sait pas

Ketch a fait le même choix d'architecture que Transcend — plan de contrôle SaaS, plan de données chez le client — mais **sans en publier la documentation**. L'existence du Transponder et sa localisation « inside your VPC » ne sont établies que par le fichier de présentation du dépôt `ketch-agent`. Aucun guide d'installation, aucun graphique Helm, aucune référence d'image, aucun schéma d'architecture n'est public. Une comparaison publiée par Ketch affirme que le Transponder classe Snowflake, Postgres et S3 dans l'environnement du client sans que les enregistrements en sortent : **c'est une page marketing, à traiter comme non vérifié.**

Deux éditeurs ont donc la même architecture ; l'un en publie le coût mensuel de référence, l'autre n'en publie même pas l'image Docker.

**Un cas non corroboré côté Osano.** Une page de mise à jour produit affirme que la découverte de données est disponible « as both a SaaS offering, as well as a self-hosted Kubernetes container for use on-premise or behind the firewall ». La page [Data discovery](https://www.osano.com/updates/data-discovery) elle-même ne mentionne aucun conteneur Kubernetes, et aucune documentation développeur ne le corrobore. **Non vérifié.**

## Fides, seul auto-hébergement intégral — avec une réserve

Le dépôt [`ethyca/fides`](https://github.com/ethyca/fides) est sous **Apache Software License 2.0**, le langage `fideslang` sous CC BY 4.0. Le déploiement se fait par Docker (`fides deploy up`). Ethyca documente explicitement les deux modèles : « Fides Self Hosted is deployed in your cloud, supported by Ethyca. Self hosted is recommended for organizations requiring additional security controls or where you do not wish to have data stored in a third party vendor system » ([Fides Cloud vs Self Hosted](https://www.ethyca.com/docs/user-guides/security/fides-cloud-vs-self-hosted)).

**La réserve.** Ethyca commercialise des paliers nommés *Fides True*, *Fides Team*, *Fides Plus* et *Fides Enterprise*, et le dépôt public contient des workflows d'intégration continue nommés `trigger_fidesplus_builds.yml` — la preuve d'un composant `fidesplus` fermé construit à partir du dépôt ouvert. **La ligne exacte entre ce qui est Apache-2.0 et ce qui est réservé aux paliers commerciaux n'est pas documentée publiquement** : aucune matrice de fonctionnalités n'est publiée. C'est le principal angle mort de cette recherche sur la seule brique qui satisfait notre contrainte d'auto-hébergement.

---

# Partie 7 — Les briques ouvertes et les protocoles

La recherche active a produit un résultat en deux temps : **une seule implémentation ouverte sérieuse, mais deux protocoles ouverts qui, eux, décrivent exactement le contrat qui nous intéresse.**

## L'implémentation : Fides (Ethyca)

Traitée tout au long du document. À retenir : Apache-2.0, auto-hébergeable, plus d'une centaine de connecteurs SaaS déclarés en YAML, exécution effective de l'effacement par masquage, découverte par graphe de schéma déclaré, webhooks de politique pour rendre la main au client. C'est le seul outil du terrain dont on peut vérifier chaque affirmation dans le code. Le prédécesseur [`ethyca/fidesops`](https://github.com/ethyca/fidesops) reste la meilleure source de documentation verbatim sur les mécanismes internes, car ses pages sont statiques et lisibles.

## Les briques ouvertes publiées par des éditeurs propriétaires

Trois éditeurs du terrain ouvrent, non pas leur produit, mais **le côté client de leur contrat**. C'est une catégorie à part, et la plus directement utile à notre projet, puisque c'est exactement la place que nous occupons.

- [`ketch-sdk/ketch-agent`](https://github.com/ketch-sdk/ketch-agent) — **MIT**, TypeScript, maintenu. Le conteneur qui exécute, dans le VPC du client, un module Node écrit par le client. Documentation d'API générée publiée ([ketch-sdk.github.io/ketch-agent](https://ketch-sdk.github.io/ketch-agent/index.html)). Le fichier `src/index.ts` résume la posture : « Agent provides the ability to run custom nodejs scripts within your environment as part of a Data Subject Rights (DSR) request orchestrated by Ketch. »
- [`ketch-com/ketch-forwarder`](https://github.com/ketch-com/ketch-forwarder) et ses implémentations de référence en [Go](https://github.com/ketch-com/go-ketch-forwarder) et [Java](https://github.com/ketch-com/java-ketch-forwarder) — la spécification OpenAPI du webhook, une seule opération `/webhook`, « This receives events forwarded from Ketch ».
- [`osano/webhook-sample-apps`](https://github.com/osano/webhook-sample-apps) — l'implémentation de référence du gestionnaire de demandes, décrite en Partie 2.
- [`transcend-io/examples`](https://github.com/transcend-io/examples) — implémentations de référence en JavaScript, TypeScript, Python et Ruby, couvrant la vérification du JWT, l'accès, l'effacement et l'enrichissement d'identité.

**Ce que ce corpus enseigne** : le contrat entre un orchestrateur et un système propriétaire tient, chez tous, en une page. Une charge utile de trois à six champs, un accusé de réception, un compte rendu asynchrone, une énumération de statuts. Aucune de ces implémentations ne dépasse quelques centaines de lignes.

## Le protocole côté processeur : OpenDSR

[`opengdpr/OpenDSR`](https://github.com/opengdpr/OpenDSR), anciennement OpenGDPR, renommé début 2020 pour couvrir la CCPA. Il « defines a common approach for data Controllers and Processors to build interoperable systems for tracking and fulfilling Data Subject requests ». C'est le contrat responsable de traitement → sous-traitant.

Champs du corps ([spécification](https://github.com/opengdpr/OpenDSR/blob/master/specification.md)) : `regulation` (`gdpr` ou `ccpa`), `subject_request_id` (UUID v4), `subject_request_type`, `submitted_time` (RFC 3339), `subject_identities`, `status_callback_urls`, `api_version`, `extensions`.

Trois types de demande seulement : **`access`, `portability`, `erasure`**. Les identités sont encodables en `raw`, `sha1`, `md5`, `sha256` — le hachage permettant d'interroger un sous-traitant sans lui révéler l'identifiant en clair.

Le rappel de statut est **obligatoire** : « All included callbacks **MUST** be invoked by the Processor on request state change ». Statuts : `pending`, `in_progress`, `completed`, `cancelled`. Et la spécification tranche la question de l'exécution : « The Data Processor receives data subject requests via RESTful endpoints and is **responsible for fulfilling requests**. »

## Le protocole côté demandeur : Data Rights Protocol

[Data Rights Protocol](https://github.com/consumer-reports-innovation-lab/data-rights-protocol), porté par le Innovation Lab de Consumer Reports, Apache-2.0, **version 1.0 publiée** après plus de trois ans de travaux. Le consortium inclut DataGrail, Ethyca, OneTrust et Transcend — c'est-à-dire quatre des six éditeurs de notre terrain.

Trois rôles : **Authorized Agent** (« an entity […] that a User has authorized to act on their behalf »), **Covered Business**, et **Privacy Infrastructure Provider**, « responsible for providing endpoints for processing data rights requests, either as a third-party service or implemented directly by the Covered Business ».

Endpoints :

```
POST   /v1/data-rights-request
GET    /v1/data-rights-request/{request_id}
POST   $status_callback
DELETE /v1/data-rights-request/{request_id}
POST   /v1/agent/{agent-id}
GET    /v1/agent/{agent-id}
```

Statuts : `open`, `in_progress`, `fulfilled`, `revoked`, `denied`, `expired`. Le refus est motivé par une énumération fermée : `suspected_fraud`, `insuf_verification`, `no_match`, `claim_not_covered`, `outside_jurisdiction`, `too_many_requests`, `other`.

**Deux enseignements pour nous.**

D'abord, l'identité : les revendications suivent le schéma `schema.org/Person` (nom, courriel, téléphone, adresse), **chacune assortie d'un booléen `_verified`**. La vérification n'est donc pas un état global de la demande mais un attribut par revendication.

Ensuite, l'aveu de conception, qui est le plus utile : le protocole assume de ne pas résoudre la confiance techniquement — « Implementers will work with minimal technical trust mechanisms and instead rely on an operating agreement between implementers ». Et le réseau est fermé : « The model of the current DRP is a closed network », avec des annuaires de service remplaçant les points `.well-known` publics envisagés à l'origine. Les requêtes sont signées cryptographiquement par le mandataire émetteur.

## Les tentatives mineures, et ce que leur rareté signifie

- [`WithSecureOpenSource/gdpr-subject-rights-api`](https://github.com/WithSecureOpenSource/gdpr-subject-rights-api) — spécification OpenAPI v3, Apache-2.0, publiée par F-Secure. C'est **la forme la plus proche de notre projet** : elle standardise la façon dont les services de back-office répondent aux demandes d'export et d'effacement, déplaçant la charge du support client vers les propriétaires de services. Deux choix explicites méritent d'être notés. Elle reste « agnostic to the context in which the personal data is being processed », parce que « Some back-end services might identify their data subjects with an email address, some others by a customer or telephone number, some with a national ID ». Et elle prévoit le repli humain : pour les systèmes hérités incapables d'implémenter l'API, un enrobage central peut « send the request forward for manual processing. This could mean opening a ticket for the back-end service's administrators. » Elle n'impose **aucun format d'export**.
- [`dsrhub/dsrhub`](https://github.com/dsrhub/dsrhub) — Apache-2.0, en Go, « an open platform for DSR (Data Subject Requests) » orchestrant le flux entre microservices. Construit sur le moteur de flux **uTask** (BSD 3-Clause). Le partage de responsabilité est explicite : « We then let individual services handle their own implementation of the dsrhub API specification », les services exposant du gRPC ou du HTTP. Étape **Exchange Identity** pour l'enrichissement d'identifiants. **19 étoiles, 96 commits** : preuve de concept, pas dépendance.
- Le reste du sujet GitHub [`dsr`](https://github.com/topics/dsr) est du bruit : `oneint` (9 étoiles), `ndpr-toolkit` (6 étoiles, droit nigérian). **Rien d'exploitable.**

La conclusion est structurelle : **le logiciel libre de gestion de demandes se réduit à un projet financé par capital-risque (Fides), à quelques implémentations du côté client publiées par des éditeurs propriétaires, et à deux spécifications de contrat.** Aucune communauté indépendante n'a produit d'alternative. Notre projet n'a donc pas de plateforme existante à reprendre — mais il a **plusieurs contrats déjà écrits** à ne pas réinventer, et le fait que Ketch ait aligné son énumération de refus sur celle du Data Rights Protocol montre que la convergence est possible.

## Une note sur le Data Transfer Initiative

Le [Data Transfer Project](https://dtinit.org/docs/dtp-what-is-it), lancé en 2018 par Google, Microsoft, Facebook et Twitter, aujourd'hui porté par la [Data Transfer Initiative](https://dtinit.org/), est **hors sujet pour l'exercice des droits mais dans le sujet pour la portabilité**. Son objet est le transfert direct de service à service, sans téléchargement intermédiaire, par des modèles de données communs et une bibliothèque d'adaptateurs vers les API des services. C'est la seule réponse existante à l'article 20 § 2 — la transmission directe à un autre responsable de traitement. **Aucun des six éditeurs du terrain ne s'y branche** : tous s'arrêtent au téléchargement par la personne.

---

# Partie 8 — Patrons récurrents et divergences

## Ce sur quoi tout le monde converge — donc ce que le problème impose

**P1. Un catalogue de connecteurs pour les tiers, un contrat de rappel pour le reste.** Tous les outils qui font quelque chose ont exactement cette structure à deux étages : des connecteurs préécrits pour les SaaS connus, et un mécanisme générique — webhook, agent, API implémentée par le client — pour ce que l'éditeur ne peut pas connaître. Personne n'a trouvé de troisième voie. Notre projet est, par construction, **du côté du second étage** : nous sommes le système propre au client.

**P2. Aucun outil n'accède directement aux bases sans une carte déclarée.** L'accès direct existe (Transcend, Fides), mais toujours **après** une annotation humaine du schéma. Aucun outil ne prétend deviner la structure de données d'une application sans qu'on la lui décrive. La découverte automatique complète la carte, elle ne la remplace jamais.

**P3. La découverte automatique est bien plus modeste que le marketing ne le dit.** Ramenée aux sources primaires, elle se réduit à trois techniques : lire l'annuaire d'identité pour trouver les applications, faire tourner un agent conteneurisé chez le client pour scruter les bases, faire correspondre des noms de colonnes puis des expressions régulières puis, éventuellement, un modèle de langue. Rien de magique, et rien qui découvre un système que personne n'a déclaré ni relié à l'annuaire SSO.

**P4. L'agent conteneurisé sortant est la réponse standard à la sensibilité des données.** DataGrail, Transcend, OneTrust et Ketch ont convergé sur la même forme : une image Docker chez le client, qui n'accepte aucun trafic entrant, détient les identifiants d'accès et **tire** le travail. Quatre éditeurs sur six, indépendamment, ont abouti à cette architecture. C'est la seule façon connue de concilier un plan de contrôle SaaS avec des données qu'on ne veut pas exporter. **Notre projet est déjà, structurellement, cet agent — sans plan de contrôle distant.**

**P5. La vérification d'identité est déléguée dès qu'un compte existe.** Le lien ou le code par courriel est le socle universel, mais tous ceux qui documentent sérieusement le sujet renvoient à l'authentification du client dès qu'elle existe : JWT ou OAuth 2 chez Transcend, systèmes de référence chez DataGrail. Corollaire non dit mais partagé : **une demande arrivant par API est réputée déjà vérifiée par l'appelant** (formulé explicitement par DataGrail).

**P6. Il n'existe aucun schéma d'export universel, et personne n'essaie d'en imposer un.** Transcend indexe l'export sur les *datapoints* déclarés par le client, Fides laisse choisir entre JSON, CSV et HTML, DataGrail ne documente rien, OneTrust livre un rapport composé et caviardé, la spécification de WithSecure s'abstient explicitement. Le format est une **conséquence de la carte**, pas un standard.

**P7. Tout le monde a un repli humain, et l'assume.** DataGrail a *Direct Contact*, Transcend a *Automated Vendor Coordination*, Osano a ses **manual data stores** avec assignation à un humain, Fides envoie un **courriel hebdomadaire** listant les identités à effacer, OneTrust ouvre des sous-tâches ServiceNow, WithSecure prévoit l'ouverture d'un ticket. Aucune plateforme n'atteint 100 % d'automatisation, et les documentations sérieuses le disent sans détour là où les pages produit le taisent. **Un service qui prévoit explicitement sa part non automatisable est conforme à l'état de l'art, pas en retard sur lui.**

**P8. L'effacement passe par une porte humaine.** DataGrail exige une revue en état *Pending Action* avant tout déclenchement. Osano assigne par défaut la validation des résultats à un *Subject Rights Assignee*, l'automatisation intégrale étant une option à activer. Fides masque plutôt que de supprimer, pour préserver l'intégrité référentielle. OneTrust est un moteur de flux avec étapes et codes de résolution. Personne ne fait de l'effacement un appel synchrone sans garde-fou.

**P9. Être connecté ne veut pas dire être effaçable — et un seul éditeur le publie.** Osano distingue, connecteur par connecteur, la capacité de *Summarization* de la capacité de *User Deletion*, et **environ la moitié de son catalogue de 155 intégrations ne sait que lire** ([liste des capacités](https://developers.osano.com/integrations/data-discovery-integrations/list-of-capabilities)). Il n'y a aucune raison de croire les autres catalogues différents : la limite vient des API des éditeurs tiers, pas de l'outil. Tout décompte de connecteurs présenté sans cette ventilation surestime mécaniquement la couverture d'effacement d'un facteur proche de deux.

**P10. Le contrat côté client tient en une page, et plusieurs éditeurs le publient en clair.** Trois à six champs entrants, un accusé de réception, un compte rendu asynchrone, une énumération de statuts. Osano, Ketch, Transcend et DataGrail publient chacun le leur, en implémentation exécutable ou en OpenAPI. Personne n'a inventé de protocole complexe — parce que le problème ne l'est pas. La difficulté est ailleurs : dans la carte, pas dans le fil.

## Ce sur quoi ils divergent — donc là où notre décision se joue

**D1. Qui exécute — et la ligne ne passe pas où on l'attend.** Elle ne sépare pas les outils entre eux, elle passe **à l'intérieur de chaque outil**, entre les SaaS tiers (l'outil exécute) et les systèmes propres au client (le client exécute). Aucun éditeur n'exécute l'effacement dans un système propriétaire sans que son propriétaire l'ait implémenté. Le cas de Ketch pousse la logique à son terme : même son agent, qui tourne dans le VPC du client et ouvre lui-même la connexion à la base, **exécute du code écrit par le client** — l'outil fournit l'ordonnanceur et le conteneur, jamais la requête. Pour un microservice déployé dans le périmètre d'une application tierce, cela signifie que **la question « exécutons-nous l'effacement ? » n'a pas de réponse imposée par l'état de l'art** : le seul cas où l'outil écrit vraiment dans la base du client est Fides, et il le fait au prix d'une annotation exhaustive du schéma par le client lui-même.

**D2. Le modèle de découverte : trois réponses incompatibles au même problème.** Fides fait déclarer un graphe de relations entre collections et le parcourt depuis une racine `identity` — approche **statique, vérifiable, et qui refuse une carte incomplète** (« It's an error to specify a collection […] can't be reached through the relations you've specified »). Transcend ne déclare aucun graphe et **résout les identifiants à chaque demande** par un appel d'enrichissement au client. Ketch prend une troisième voie : il **résout les identités en amont** et transmet à chaque système un tableau `identities[]` couvrant plusieurs espaces d'identité, en clair ou haché. Le système receveur n'a rien à résoudre : il lui suffit de reconnaître un des identifiants qu'on lui tend.

Les trois exigences sont différentes en nature. Fides exige une connaissance complète du schéma en amont. Transcend exige seulement que le client sache répondre « voici les autres identifiants de cette personne ». Ketch exige que la plateforme, elle, tienne les correspondances. **Aucun des trois ne maintient de graphe d'identités persistant chez le client. C'est le choix d'architecture le plus lourd du document.**

**D3. Le degré d'auto-hébergement, et il est quaternaire.** Intégral (Fides, Apache-2.0). Plan de données complet (Transcend/Sombra, avec coût publié et garde des clés chez le client ; Ketch/Transponder, sur le papier, mais sans aucune documentation publique). Agent de découverte seulement (DataGrail, OneTrust). Aucun (Didomi, Osano). **Notre contrainte cardinale n'a qu'un précédent complet, et c'est un logiciel libre financé par capital-risque dont la frontière ouvert/fermé n'est pas publiée.**

**D4. Le niveau de spécification publique du contrat de satisfaction, et il ne suit pas la taille de l'éditeur.** Transcend publie tout — charge utile, JWT ES384, nonce, énumération de statuts, politique de reprise. DataGrail publie tout — HMAC-SHA256, cadence de scrutation, plafond de 200 Mo. Ketch publie un OpenAPI complet et un paquet TypeScript typé, alors même que son manuel est fermé. Osano publie une application de référence exécutable. **OneTrust ne publie rien** sur ce point précis, et Didomi n'a rien à publier. Deux sous-divergences valent d'être notées : l'**authentification** du webhook, signée cryptographiquement chez Transcend et DataGrail, réduite à un jeton porteur configurable chez Osano et Ketch ; et l'**énumération des motifs de refus**, riche et normalisée chez Ketch, absente ailleurs.

**D5. Où résident les résultats.** DataGrail dépose dans le seau du **client**. Transcend chiffre de bout en bout et n'a pas les clés en auto-hébergement. Osano et OneTrust hébergent dans leur propre portail de messagerie chiffrée. Ketch va chercher les documents par `GET` sur des URL que le client lui donne. Didomi ne détient qu'une URL. **Le document le plus sensible du processus — l'export d'accès — fait l'objet d'une décision différente chez presque chaque éditeur.**

**D6. La granularité de la vérification.** État global de la demande chez tous les éditeurs ; **attribut par revendication** (`_verified` par champ `schema.org/Person`) dans le Data Rights Protocol. La seconde forme est plus fine et vient du seul acteur non commercial. Elle mérite d'être regardée.

**D7. La taxonomie des droits, et personne ne couvre les six.** OpenDSR n'en connaît que trois — `access`, `portability`, `erasure`. Ketch en connaît quatre — `AccessRequest`, `CorrectionRequest`, `DeleteRequest`, `RestrictProcessingRequest` — donc **ni portabilité ni opposition**. Osano en modélise trois dans son exemple (`SUMMARIZE`, `DELETE`, `OPT_OUT`). Transcend en énumère plus de quinze, en mêlant droits RGPD et opt-out américains. Le Data Rights Protocol structure par régime puis par droit, et son énumération CCPA ignore la rectification. **L'ambition des six droits est plus rare qu'on ne le croirait.**

Surtout : **notre taxonomie fermée à sept valeurs, dont un `OutOfScope` exclusif, n'a d'équivalent chez personne.** Aucun outil du terrain ne nomme le cas « ce texte n'exerce aucun droit ». Tous reçoivent une demande dont le type est déjà décidé, par un formulaire ou par un appelant. Le seul endroit du corpus où l'incertitude sur la nature de la demande est représentée, c'est le motif de refus `claim_not_covered` du Data Rights Protocol, repris par Ketch — et c'est un refus, pas une qualification. **C'est précisément l'espace que notre service occupe, et il est vide.**

## Ce qui reste opaque

1. Le schéma de charge utile du webhook de satisfaction de OneTrust — **inexistant publiquement**.
2. L'énumération des méthodes de vérification d'identité de OneTrust — endpoint documenté, schéma de réponse non rendu.
3. La liste canonique des états du cycle de vie chez OneTrust — apparemment configurable par locataire (l'API filtre par *nom d'étape*), donc pas d'énumération publique.
4. Le format d'export concret de DataGrail — jamais nommé, seulement « download link » et « your cloud storage bucket ».
5. La frontière exacte entre Fides Apache-2.0 et les paliers `fidesplus` fermés — aucune matrice publiée.
6. Le corps intégral de `PUT /v1/data-silo` chez Transcend (réponse d'effacement) — l'endpoint est référencé, son schéma complet ne l'est pas.
7. L'ensemble des articles de `my.onetrust.com` — portail authentifié.
8. **L'ensemble de `docs.ketch.com`** — portail Auth0. Conséquences : le catalogue de connecteurs de Ketch et ses capacités par connecteur, le contrat de satisfaction du chemin « connecteur préconstruit », les méthodes de vérification d'identité, et toute documentation du Transponder (installation, image, architecture) sont **inaccessibles**.
9. **Ketch Data Sentry** — aucune documentation développeur publique. Tout ce qui est public le décrit comme un plan de données de **façade** (trafic réseau sortant), non comme une découverte en base.
10. **Le format d'export de Ketch** — aucun schéma, aucun type MIME, aucun format de portabilité imposé nulle part publiquement.
11. **Le conteneur Kubernetes auto-hébergé de la découverte Osano** — affirmé sur une page de mise à jour produit, non corroboré par la documentation développeur ni par la page produit elle-même.
12. **La base de connaissances `docs.osano.com`** — HTTP 502 sur chaque tentative. Les affirmations marquées [extrait indexé] reposent sur des extraits de moteur de recherche, pas sur une lecture directe.
13. **Le schéma canonique du webhook sortant d'Osano** — connu seulement par les trois champs de l'exemple et la liste de variables ; aucun OpenAPI publié.
</content>
