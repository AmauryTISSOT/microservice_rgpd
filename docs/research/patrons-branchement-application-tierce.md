# Patrons de branchement sur une application tierce

> Recherche du ticket [#60](https://github.com/AmauryTISSOT/microservice_rgpd/issues/60).
> **Ce document donne la matière d'un choix ; il ne le fait pas.** Aucun patron n'est ici retenu ni écarté pour le service. Le périmètre actuel du service — une `Qualification` rendue en aide à la décision, sans exécution du droit qualifié ([CONTEXT.md](../../CONTEXT.md)) — n'est pas élargi par ce document : il en est le point de comparaison.

## Ce que la question demande

Le ticket demande quels patrons permettent à un service déployé aux côtés d'une application de **lire et modifier les données de cette application sans la réécrire**, et ce que chacun coûte. Cinq patrons sont nommés : l'adaptateur implémenté par l'application, l'accès direct à la base, le renversement par événements ou *webhooks*, le *strangler* / passerelle, l'introspection de schéma et la cartographie déclarative.

Chacun est instruit selon la même grille en quatre colonnes :

| Colonne | Ce qu'elle demande |
| --- | --- |
| **Exige** | Ce que l'application tierce doit faire, écrire, ou concéder. C'est le coût d'intégration, celui qu'on facture au client. |
| **Garantit** | Ce que le service peut promettre indépendamment de la bonne volonté du client. |
| **Échoue** | Le mode de panne caractéristique, et surtout : est-il **bruyant** ou **silencieux** ? |
| **Tenable quand l'application évolue sans prévenir** | La colonne décisive. Un schéma, une API, une table changent sans que personne nous en informe. Le patron s'en aperçoit-il, et à quel prix ? |

Le vocabulaire de la quatrième colonne est celui que l'[ADR-0001](../adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md) a déjà posé pour arbitrer entre les deux moteurs : *« les erreurs du LLM portent toutes une confiance non haute, donc une relecture ciblée les capte, là où les erreurs du lexique sont silencieuses »*. **Le même critère s'applique aux patrons de branchement : ce qui compte n'est pas le taux de panne, c'est de savoir si la panne se déclare.**

---

# Partie 0 — Le fait qui commande tout

Avant les patrons, un fait que trois sources primaires indépendantes énoncent chacune dans son domaine, et qui décide de la quatrième colonne dans tous les cas.

**Le schéma d'un tiers n'est notifié à personne.** PostgreSQL le dit sans ambiguïté pour sa propre réplication logique : *« The database schema and DDL commands are not replicated. The initial schema can be copied by hand using `pg_dump --schema-only`. Subsequent schema changes would need to be kept in sync manually. »* ([PostgreSQL, *Logical Replication — Restrictions*](https://www.postgresql.org/docs/current/logical-replication-restrictions.html)). Debezium hérite mécaniquement de cette limite : *« Logical decoding does not support DDL changes. This means that the connector is unable to report DDL change events back to consumers. »* ([Debezium, connecteur PostgreSQL](https://raw.githubusercontent.com/debezium/debezium/main/documentation/modules/ROOT/pages/connectors/postgresql.adoc)). Et l'outil de conformité qui a le plus formalisé la cartographie du schéma d'autrui le dit en une phrase : *« fidesops does not do automated schema discovery. It is only aware of the fields you declare. »* ([Fidesops, *Define Datasets*](https://ethyca.github.io/fidesops/guides/datasets/)).

Trois conséquences, valables pour tous les patrons ci-dessous :

1. **Aucun patron ne détecte un changement de schéma « gratuitement ».** La détection est toujours un dispositif ajouté — une validation, un test de contrat, un *diff*.
2. **Le mode de panne par défaut d'un changement de schéma est silencieux**, sauf contrainte explicite. PostgreSQL décrit précisément la seule situation où il devient bruyant : *« When the schema is changed on the publisher and replicated data starts arriving at the subscriber but does not fit into the table schema, replication will error until the schema is updated. »* — l'erreur naît de l'incompatibilité de la donnée, pas de la connaissance du DDL.
3. **La direction du changement compte.** Toujours PostgreSQL : *« In many cases, intermittent errors can be avoided by applying additive schema changes to the subscriber first. »* Un ajout de colonne est absorbable ; un renommage ou une suppression ne l'est pas.

Le RGPD ajoute deux contraintes de délai et de portée qui pèsent sur le choix : l'article 12.3 impose de répondre *« dans les meilleurs délais et en tout état de cause dans un délai d'un mois à compter de la réception de la demande »*, et l'article 19 impose de notifier *« à chaque destinataire auquel les données à caractère personnel ont été communiquées toute rectification ou tout effacement de données à caractère personnel ou toute limitation du traitement »* ([CNIL, texte du RGPD, chapitre III](https://www.cnil.fr/fr/reglement-europeen-protection-donnees/chapitre3)). **Un mois est un budget confortable : il autorise l'asynchrone, et il interdit d'exiger la synchronicité comme justification d'un patron.** L'article 19, lui, disqualifie tout patron qui ne saurait pas énumérer les destinataires.

---

# Partie 1 — Les cinq patrons

## 1. L'adaptateur implémenté par l'application

Le service définit une interface — HTTP, gRPC, ou un paquet à référencer — et l'application l'implémente. C'est le patron du **port sortant** : le service déclare ce dont il a besoin, le client fournit la traduction.

### Ce qu'il exige de l'application tierce

Du **code applicatif écrit par le client**, et c'est le coût le plus visible des cinq. Rien ne peut être fait sans lui. En contrepartie, il ne demande **aucun accès à la base**, aucune modification d'infrastructure, aucun privilège de réplication.

La littérature primaire nomme la couche qui en résulte : c'est l'**anti-corruption layer** d'Eric Evans, repris tel quel par Microsoft — *« Implement a facade or adapter layer between different subsystems that don't share the same semantics. This layer translates requests that one subsystem makes to the other subsystem. Use this pattern to ensure that dependencies on outside subsystems don't limit an application's design. »* ([Microsoft, *Anti-Corruption Layer Pattern*](https://learn.microsoft.com/en-us/azure/architecture/patterns/anti-corruption-layer)). Le motif du patron est explicitement défensif : *« When these legacy features have quality problems, this support corrupts what might otherwise be a cleanly designed modern application. »* Autrement dit, l'adaptateur existe pour que **le schéma du tiers ne remonte pas dans le domaine du service**.

Microsoft énumère les coûts de la couche, et ils ne sont pas nuls : *« The anti-corruption layer adds latency to calls between the two systems »*, *« adds an extra service that you must manage and maintain »*, et *« If the anti-corruption layer is part of an application migration strategy, consider whether it's permanent or whether you plan to retire it after you migrate all legacy functionality. »* Deux avertissements plus fins, souvent négligés : *« Because the anti-corruption layer mediates systems that might have different trust levels, consider enforcing input validation and sanitization at this boundary »*, et *« Plan for observability, including correlation IDs and structured logging, to diagnose translation failures. »* — **une traduction rate silencieusement si on ne l'instrumente pas.**

Contre-indication explicite : *« The new and legacy systems have no significant semantic differences. In this scenario, it's important to focus the anti-corruption layer on translation logic. Avoid placing business rules or orchestration in the layer. »*

### Ce qu'il permet de garantir

**Une garantie de forme, jamais de contenu.** Le service peut garantir que ce qu'il reçoit est bien typé et que ce qu'il demande est bien formulé. Il ne peut pas garantir que l'implémentation du client est complète — qu'un `erase(subjectId)` efface réellement toutes les occurrences.

C'est là que la littérature des contrats devient pertinente. Ian Robinson pose le problème de l'évolution d'un fournisseur vis-à-vis de ses consommateurs : *« If we remove a required component from our extensible schema, we will break existing consumers […] provider and consumers must all jump at the same time. »* ([Martin Fowler / Ian Robinson, *Consumer-Driven Contracts*](https://martinfowler.com/articles/consumerDrivenContracts.html)). Le renversement proposé — le contrat piloté par le consommateur — se décrit ainsi : *« Providers are subject to an obligation that originates from outside their boundaries […] services depend for success on their being consumed. »*

Ici, **les rôles sont inversés par rapport au cas habituel** : c'est le service RGPD qui est *consommateur* d'une capacité fournie par l'application tierce. Le contrat piloté par le consommateur est donc, littéralement, la forme dont ce patron a besoin — le service publie ses attentes sous forme de suite exécutable, l'application les fait passer. C'est **le seul mécanisme identifié dans les sources primaires qui rende bruyante la rupture d'un adaptateur.**

### Comment il échoue

- **Silencieusement, par implémentation partielle.** Un adaptateur qui compile et répond `200 OK` sans avoir tout effacé est indiscernable d'un adaptateur correct, vu du service.
- **Bruyamment, à la compilation ou au *handshake*,** si le contrat est versionné et vérifié. C'est le mode souhaitable, et il ne s'obtient qu'en payant la suite de contrat.
- **Par dérive de traduction** : le mode de panne que Microsoft demande d'instrumenter avec des identifiants de corrélation.

### Tenable quand l'application évolue sans prévenir ?

**Oui, et c'est le seul des cinq dont c'est la propriété structurelle.** Le service ne connaît pas le schéma du client ; le schéma peut donc changer autant qu'il veut. Ce qui doit rester stable, c'est **l'interface**, qui est une surface petite, écrite, et versionnable — pas le schéma, qui est une surface large, implicite et mouvante.

Condition de tenabilité, et elle est dure : **l'adaptateur devient une dépendance de la chaîne de livraison du client.** Il n'est tenable que si le client accepte d'exécuter la suite de contrat du service dans son intégration continue. Sans cela, on retombe sur le mode de panne silencieux, et la garantie disparaît sans que rien ne change en apparence.

---

## 2. L'accès direct à la base de données

Le service se connecte au schéma du client, en lecture et en écriture.

### Ce qu'il exige de l'application tierce

**Des identifiants, des privilèges, et un accès réseau.** Rien d'autre — c'est le seul des cinq qui n'exige *aucune ligne de code* du client, et c'est exactement pourquoi il séduit.

Le prix se paie en couplage, et Martin Fowler l'a nommé il y a longtemps : *« An integration database is a database which acts as the data store for multiple applications, and thus integrates data across these applications »*, et le diagnostic est sans appel — *« Integration databases lead to serious problems becaue the database becomes a point of coupling between the applications that access it. This is usually a deep coupling that significantly increases the risk involved in changing those applications and making it harder to evolve them. »* Il conclut : *« As a result most software architects that I respect take the view that integration databases should be avoided. »* ([Martin Fowler, *IntegrationDatabase*](https://martinfowler.com/bliki/IntegrationDatabase.html)).

Fowler nomme aussi l'effet sur le schéma lui-même : *« The resulting schema is either more general, more complex or both - because it has to unify what should be separate BoundedContexts. »* Ce n'est pas un coût abstrait : **brancher un service RGPD en direct sur la base d'un client, c'est lui interdire de faire évoluer son schéma librement, et il ne le saura pas avant de casser quelque chose.**

### Ce qu'il permet de garantir

Beaucoup, et c'est son attrait réel :

- **L'atomicité.** Une transaction couvre plusieurs tables ; l'effacement est tout ou rien.
- **L'exhaustivité relative** au périmètre connu, si le graphe des clés étrangères est complet.
- **L'observabilité** : on peut relire pour vérifier ce qu'on a écrit.

Et beaucoup de garanties **négatives** peuvent être imposées par le SGBD lui-même plutôt que par le code. PostgreSQL fournit la sécurité au niveau ligne, avec un défaut sain : *« When row security is enabled […] all normal access to the table for selecting rows or modifying rows must be allowed by a row security policy. If no policy exists for the table, a default-deny policy is used, meaning that no rows are visible or can be modified. »* Mais la même page nomme les échappatoires : *« Superusers and roles with the `BYPASSRLS` attribute always bypass the row security system »*, et *« Table owners normally bypass row security as well, though a table owner can choose to be subject to row security with `ALTER TABLE … FORCE ROW LEVEL SECURITY` »* ([PostgreSQL, *Row Security Policies*](https://www.postgresql.org/docs/current/ddl-rowsecurity.html)). **Un compte de service propriétaire des tables n'est pas contraint par la RLS : la garantie de moindre privilège est fausse si l'on ne vérifie pas ce point.**

### Comment il échoue

- **Silencieusement, par angle mort.** Une table ajoutée après le branchement n'est jamais visitée. Rien ne le signale.
- **Bruyamment, par violation de contrainte,** quand une clé étrangère `ON DELETE RESTRICT` bloque un effacement — c'est le meilleur mode de panne du patron, et il faut y voir un allié plutôt qu'un obstacle.
- **Par sur-privilège.** PostgreSQL prévient sur le versant réplication, et l'avertissement vaut plus généralement : *« There are currently no privileges on publications. Any subscription (that is able to connect) can access any publication. Thus, if you intend to hide some information from particular subscribers, such as by using row filters or column lists, or by not adding the whole table to the publication, be aware that other publications in the same database could expose the same information. »* ([PostgreSQL, *Logical Replication Security*](https://www.postgresql.org/docs/current/logical-replication-security.html)).
- **Par dérive de modèle,** si le service reflète le schéma dans du code. EF Core est explicite sur ce que la rétro-ingénierie ne saura jamais faire : *« Not everything about a model can be represented using a database schema. For example, information about inheritance hierarchies, owned types, and table splitting are not present in the database schema. Because of this, these constructs will never be scaffolded. »*, et *« some column types may not be supported by the EF Core provider. These columns won't be included in the model. »* ([Microsoft, *Reverse Engineering — EF Core*](https://learn.microsoft.com/en-us/ef/core/managing-schemas/scaffolding/)). **Une colonne non supportée est absente du modèle sans erreur : c'est un angle mort silencieux, et c'est précisément une colonne susceptible de contenir des données personnelles (type applicatif, extension, JSON exotique).**

### Tenable quand l'application évolue sans prévenir ?

**Non par construction ; oui seulement sous surveillance active.** C'est la conclusion la plus nette de cette recherche, et elle est étayée par la manière dont les outils qui vivent de ce patron s'en sortent — voir la Partie 2.

Trois conditions cumulatives ressortent des sources :

1. **Un inventaire déclaré**, et non deviné, de ce que le service touche (Partie 1 § 5).
2. **Une comparaison de cet inventaire au schéma réel, à chaque exécution.** C'est exactement ce que Greenmask appelle *schema diff* : *« The validate command offers a way to assess the impact on both schema (validation warnings) and data (transformation and displaying differences) »*, avec le motif énoncé sans détour — *« Schema diff helps to avoid data leakage when schema changed. »* ([Greenmask, *Architecture*](https://docs.greenmask.io/latest/architecture/) et [présentation](https://docs.greenmask.io/latest/)).
3. **Le choix d'échouer plutôt que de continuer** quand le *diff* n'est pas vide. Sans cette troisième condition, les deux premières ne servent à rien.

Le mode de gestion du modèle, côté EF Core, se ramène à un dilemme que Microsoft formule textuellement. Soit on *scaffold* une fois — *« the scaffolded code provides a starting point for code-based mapping going forward »* — et il faut alors *« Manually update the entity types and EF configuration when the database changes […] with the only real challenge being a process to make sure that database changes are recorded or detected in some way »*. Soit on re-*scaffold* à chaque changement, et *« This will overwrite any previously scaffolded code, meaning any changes made to entity types or EF configuration in that code will be lost. »* **Les deux branches supposent qu'on sache que la base a changé.** Personne ne le dit au service : c'est là le trou.

---

## 3. Le renversement par événements et *webhooks*

Le service **demande** ; l'application **exécute** dans son propre code, avec ses propres règles, et **rend compte** de façon asynchrone.

### Ce qu'il exige de l'application tierce

Un consommateur d'ordres et un producteur de comptes rendus. **Du code, comme le patron 1 — mais de la logique métier, pas de la traduction.** C'est une différence de nature : l'adaptateur du patron 1 traduit une demande du service en opérations sur le modèle du client ; ici le client décide lui-même de ce qu'effacer signifie chez lui.

Le budget d'un mois de l'article 12.3 rend le va-et-vient asynchrone acceptable. Le patron canonique est décrit par Microsoft : réponse `HTTP 202 (Accepted)` immédiate, en-têtes `Location` et `Retry-After`, point d'état interrogeable, `303 (See Other)` à l'achèvement ([Microsoft, *Asynchronous Request-Reply Pattern*](https://learn.microsoft.com/en-us/azure/architecture/patterns/asynchronous-request-reply)). Deux considérations comptent ici plus que le mécanisme :

- **L'idempotence côté serveur est nécessaire, pas optionnelle** : *« You can require clients to supply an idempotency key […] If the back end receives a duplicate key, it should return the existing status resource instead of enqueuing a second work item. This approach protects against network failures that cause the client to retry a POST that the server already accepted. It's especially important in this pattern because the client can't distinguish between a lost response and a request that was never received. »*
- **Le choix entre interrogation et rappel dépend du réseau du client, pas de l'élégance.** Microsoft indique d'utiliser l'interrogation quand *« You call a service that uses only the HTTP protocol and the return service can't send callbacks because of firewall restrictions on the client side »*, et de ne pas l'utiliser quand *« The network design supports open ports to receive asynchronous callbacks or webhooks. »* Dans un déploiement dans le périmètre d'un tiers, **c'est l'interrogation qui est le pari sûr**, parce qu'elle ne demande rien à la topologie réseau du client.

Le versant *webhook* a aujourd'hui une spécification. Standard Webhooks impose la vérification de signature et la protection contre le rejeu : *« the message's: ID, timestamp and body are concatenated (delimited by full-stops) and then signed »*, avec *« Make sure to verify the `webhook-timestamp` header has a timestamp that is within some allowable tolerance of the current timestamp to prevent replay attacks. »* Et l'identifiant sert de clé d'idempotence : *« The ID is often used as an idempotency key, which lets a consumer ensure that they only process a specific event once, even if sent multiple times maliciously, in error, or due to networking issues. »* La spécification recommande enfin *« to retry delivery following a retry schedule spanning multiple days, with an exponential backoff »* ([Standard Webhooks, spécification](https://raw.githubusercontent.com/standard-webhooks/standard-webhooks/main/spec/standard-webhooks.md)). Le motif d'existence de la spécification est en soi une donnée sur le coût du patron : *« every webhooks provider implements them differently and with varying quality »* ([standardwebhooks.com](https://www.standardwebhooks.com/)).

### Ce qu'il permet de garantir

**La traçabilité de l'ordre et de son accusé — et rien de plus.** Le service peut prouver qu'il a demandé, quand, et ce que le client a répondu. Il ne peut rien prouver de l'effet réel.

Ce n'est pas une faiblesse dans tous les cadres : c'est **exactement le partage de responsabilité que l'Open Policy Agent assume**. OPA *« decouples policy decision-making from policy enforcement »* ; l'application interroge OPA — *« When your software needs to make policy decisions it queries OPA and supplies structured data (e.g., JSON) as input »* — et **OPA n'applique rien lui-même**, l'exécution restant à la charge de l'appelant ([Open Policy Agent, documentation](https://www.openpolicyagent.org/docs/latest/)). Un service qui décide et un tiers qui exécute est un montage établi, à condition que le partage soit assumé et écrit, pas subi.

Il y a en revanche une **incompatibilité dure avec la garantie transactionnelle** : demander à l'application d'écrire en base *et* de publier un message, ce sont deux écritures sans transaction commune. C'est le problème que Debezium nomme et résout par le patron *outbox* : *« The outbox pattern is a way to safely and reliably exchange data between multiple (micro) services »*, qui évite *« inconsistencies between a service's internal state (as typically persisted in its database) and state in events consumed by services that need the same data »* ([Debezium, *Outbox Event Router*](https://raw.githubusercontent.com/debezium/debezium/main/documentation/modules/ROOT/pages/transformations/outbox-event-router.adoc)). **Mais l'*outbox* est du travail pour le client** : il lui faut une table dédiée avec des colonnes imposées — `id` (uuid), `aggregatetype`, `aggregateid`, `type`, `payload` — et l'obligation d'y écrire dans la même transaction que la donnée métier. C'est le patron 3 qui vient réclamer du patron 2.

### Comment il échoue

- **Par silence.** L'ordre part, rien ne revient. C'est le mode dominant, et il est **bruyant seulement si l'on pose une échéance** : sans horloge côté service, un compte rendu qui n'arrive jamais ressemble à un compte rendu qui tarde.
- **Par double exécution**, si l'idempotence n'a pas été payée des deux côtés.
- **Par accusé mensonger.** Le client répond « fait » sans avoir fait. Indétectable de l'extérieur — c'est le même angle mort que le patron 1, aggravé par le fait qu'aucune suite de contrat ne peut vérifier un effet asynchrone sur des données réelles.
- **Par perte de la file**, si l'ordre n'est pas persisté avant d'être émis.

Microsoft rappelle enfin un coût d'exploitation qu'on oublie : *« The status resource and any stored results consume storage and compute. Define a retention policy to clean them up after a reasonable period. »*

### Tenable quand l'application évolue sans prévenir ?

**Oui, autant que le patron 1, et pour la même raison : le service ne connaît pas le schéma.** Le contrat porte sur un vocabulaire d'ordres, pas sur des tables.

La condition de tenabilité est différente et plus légère : il faut **un versionnement du format d'événement et une échéance**. Le versionnement rend bruyante une rupture de format ; l'échéance rend bruyant un silence. Sans échéance, le patron 3 est **le plus silencieux des cinq** — un client qui cesse simplement de traiter les ordres est indistinguable d'un client qui n'a rien reçu à traiter.

---

## 4. Le patron *strangler fig* / la passerelle

S'interposer devant l'application, intercepter le trafic, et router.

### Ce qu'il exige de l'application tierce

**La maîtrise du point d'entrée**, ce qui est un pouvoir considérable et rarement concédé. Martin Fowler donne l'analogie fondatrice : le figuier étrangleur *« begins with small additions, often new features, that are built on top of, yet separate to the legacy code base. As we do this we move bits of behavior from the legacy system into the new code base. »*, et l'essentiel du travail est ailleurs — *« there's considerable work in figuring out how to break it down into manageable pieces, This will involve identifying seams that we can insert into the system to allow it to be split. »* ([Martin Fowler, *StranglerFigApplication*](https://martinfowler.com/bliki/StranglerFigApplication.html)). Il nomme aussi la résistance qu'il rencontre : *« people often balk at the necessity of building transitional architecture to allow the new and legacy system to coexist, code that will go away once the modernization is complete. »*

**Les contre-indications de Microsoft sont ici décisives** et disqualifient le patron dans la plupart des déploiements en périmètre tiers. Le patron ne convient pas quand *« Requests to the back-end system can't be intercepted »* et quand *« You can't access the legacy system's source code. To disable migrated features and redirect internal calls, you need to be able to modify the legacy system's source code. »* ([Microsoft, *Strangler Fig Pattern*](https://learn.microsoft.com/en-us/azure/architecture/patterns/strangler-fig)). **La seconde condition est fatale : un service « facilement branchable » suppose par définition qu'on ne modifie pas le code du client.**

Le patron impose en outre des obligations d'exploitation : *« Make sure that the façade doesn't become a single point of failure or a performance bottleneck »*, *« Make sure that the façade keeps up with the migration »*, et *« Consider how to handle services and data stores that both the new system and the legacy system might use. Make sure that both systems can access these resources at the same time. »*

### Ce qu'il permet de garantir

**L'interception exhaustive de ce qui passe par la passerelle — et strictement rien de ce qui n'y passe pas.** C'est la garantie la plus forte des cinq sur son périmètre, et la plus étroite. Un traitement par lot, un administrateur en SQL direct, un connecteur tiers : autant de chemins qui contournent la passerelle sans que rien ne l'indique.

La forme atténuée du patron est celle que Fowler, Cartwright, Horn et Lewis nomment **Event Interception** — *« Intercept any updates to system state and route some of them to a new component »* — accompagnée du **Legacy Mimic** — *« New system interacts with legacy system in such a way that the old system is not aware of any changes. »* ([Martin Fowler *et al.*, *Patterns of Legacy Displacement*](https://martinfowler.com/articles/patterns-legacy-displacement/)). Les auteurs demandent d'être *« explicit and transparent in this value of risk mitigation vs cost of transitional architecture trade-off »* : l'architecture transitoire est un investissement, pas un déchet, mais c'en est un.

### Comment il échoue

- **Par contournement silencieux.** Le mode dominant, et le plus grave.
- **Bruyamment, par panne de la passerelle** : elle est sur le chemin critique de l'application entière. C'est le point que Microsoft demande explicitement de prévenir.
- **Par pourrissement de l'architecture transitoire**, quand la passerelle censée disparaître devient permanente. Microsoft laisse la porte ouverte — *« Alternatively, you can maintain the façade as an adapter for legacy clients »* — mais demande de le décider : *« Conceptualize this as transitional architecture, and balance this architecture's risk mitigation benefits against its temporary infrastructural costs. »*

### Tenable quand l'application évolue sans prévenir ?

**Non.** La passerelle doit connaître les routes ; une route ajoutée sans qu'on le sache la contourne. Microsoft l'énonce comme une exigence d'entretien continu : *« Make sure that the façade keeps up with the migration. »* Un tiers qui évolue sans prévenir garantit précisément que la passerelle ne suivra pas.

**Note importante sur la lecture du patron.** Le *strangler fig* est un patron de **remplacement**, pas de branchement. Sa finalité est la mise hors service du système existant : *« After you migrate all of the functionality and there are no dependencies on the legacy system, you can decommission the legacy system. »* Employé comme patron d'intégration permanente, il est détourné de son intention — Fowler comme Microsoft le décrivent tous deux comme temporaire. La partie récupérable pour un service branchable est **Event Interception seul**, sans la trajectoire de remplacement.

---

## 5. L'introspection de schéma et la cartographie déclarative

Deux stratégies opposées pour un même besoin — savoir où vivent les données personnelles chez le client — et il faut absolument les distinguer.

**L'introspection** *devine* : le service lit le catalogue système, cherche des noms de colonnes plausibles, suit les clés étrangères. **La cartographie déclarative** *fait dire* : le client écrit un document qui déclare, table par table, colonne par colonne, ce qui est personnel.

### Ce que la cartographie exige de l'application tierce

**Un document, pas du code.** C'est son grand mérite : le coût d'intégration est celui d'un fichier, versionnable, relisable par un juriste, et non celui d'un développement.

Le seul dispositif de première main abouti pour la finalité RGPD précise est le *Dataset* de Fides : *« A Dataset takes a database schema (tables and columns) and adds Fides privacy categorizations »*, c'est-à-dire *« a database-agnostic way to annotate privacy declarations »*. Sa structure est *« a set of "collections" (tables) that contain "fields" (columns) »*, et *« At each level — Dataset, collection, and field, you can assign one or more Data Categories, and the categories declared at each child level are additive. »* ([Ethyca, *Data Mapping Annotations*](https://www.ethyca.com/docs/data-mapping/guides/data-mapping-annotations) et [*Dataset*, Fides Language](https://ethyca.github.io/fideslang/resources/dataset/)).

Ce que le document doit contenir est étonnamment précis, et c'est là que le coût réel apparaît. Les annotations `fides_meta` sont documentées une à une ([Fidesops, *Define Datasets*](https://ethyca.github.io/fidesops/guides/datasets/)) :

| Clé | Définition citée |
| --- | --- |
| `identity` | *« Signifies that this field is an identity value that can be used as the root for a traversal »* |
| `references` | *« A declaration of relationships between collections »*, avec la syntaxe *« `[dataset name].[collection name].[field name]` »* |
| `direction` | *« Accepted values are `from` or `to`. This determines how fidesops uses the relationships to discover data. If the direction is `to`, fidesops will only use data in the source collection to discover data in the referenced collection. If the direction is `from`, fidesops will only use data in the referenced collection to discover data in the source collection. »* |
| `primary_key` | *« A boolean value that means that fidesops will treat this field as a unique row identifier for generating update statements »* |
| `data_type` | *« An indication of the type of data held by this field. Data types are used to convert values to the appropriate type when those values are used in queries »* |

**Le point crucial : ces relations ne sont pas les clés étrangères.** Elles sont déclarées à part, avec une direction, parce que le graphe utile à une demande d'exercice de droits n'est pas le graphe d'intégrité référentielle du schéma. C'est un graphe **orienté depuis une identité** — l'adresse électronique, l'identifiant utilisateur — vers tout ce qui s'y rattache, et il traverse des liens que le SGBD ne connaît pas.

### Ce qu'elle permet de garantir

**Beaucoup, à condition de refuser d'exécuter ce qu'on n'a pas compris.** Fides fait exactement cela : la cartographie sert à construire un graphe de parcours — *« we are creating a linked graph using the connections you've specified between your collections to retrieve your data »* — et **l'échec est explicite** : *« It's an error to specify a collection in your Dataset can't be reached through the relations you've specified. »* ([Fidesops, *Query Execution*](https://github.com/ethyca/fidesops/blob/main/docs/fidesops/docs/guides/query_execution.md)).

C'est la propriété la plus précieuse trouvée dans cette recherche : **un patron dont le mode de panne par défaut est bruyant.** Une collection déclarée mais inatteignable arrête l'exécution au lieu de produire un résultat partiel qui aurait l'air complet.

La même page nomme un piège subtil : *« If your Dataset has multiple identity values, you can create a situation where the query behavior depends on the values you provide. »* Une demande instruite avec l'adresse électronique seule n'atteint pas les mêmes collections qu'une demande instruite avec l'adresse et l'identifiant interne. **Le périmètre effacé dépend de l'identité fournie, et cela ne se voit pas dans le rapport.**

### Ce que l'introspection permet de garantir

**Rien.** C'est la conclusion la plus nette de cette partie, et elle est étayée directement. Le seul outil du corpus qui aurait un intérêt évident à faire de la découverte automatique la refuse explicitement : *« fidesops does not do automated schema discovery. It is only aware of the fields you declare. »*

L'introspection reste utile pour **une seule chose** : produire l'ébauche que l'humain complétera, et détecter la dérive entre le déclaré et le réel. C'est exactement l'usage qu'en font les outils de la Partie 2 — Greenmask lit le catalogue *pour comparer*, pas *pour deviner*.

Un argument de conception vient renforcer la position déclarative, formulé par PostgreSQL Anonymizer : *« masking rules should be written by the people who develop the application because they have the best knowledge of how the data model works »*, et par conséquent ces règles doivent être *« implemented directly inside the database schema »* ([PostgreSQL Anonymizer, documentation](https://access.crunchydata.com/documentation/postgresql-anonymizer/latest/)). **Le service RGPD n'est pas la bonne entité pour savoir ce qui est personnel dans le schéma d'un tiers ; le client l'est.**

### Comment elle échoue

- **La cartographie échoue par obsolescence** : elle décrit un schéma d'hier. Le mode de panne est silencieux **si personne ne la confronte au schéma réel**, bruyant si on le fait.
- **Elle échoue par incomplétude assumée** : ce qui n'y figure pas n'est pas traité, et personne ne s'en aperçoit. C'est la panne la plus dangereuse du patron, car elle n'a pas de signature.
- **L'introspection échoue par faux positifs et faux négatifs**, sans hiérarchie entre eux et sans signal de confiance.

### Tenable quand l'application évolue sans prévenir ?

**Oui, mais seulement si la cartographie est *validée contre le schéma réel* à chaque exécution, et si l'écart interrompt le traitement.** C'est le seul patron dont les sources primaires décrivent un mécanisme de détection de dérive déjà implémenté et déjà nommé (le graphe inatteignable de Fides, le *schema diff* de Greenmask).

Sans validation, la cartographie déclarative est **le patron le plus dangereux des cinq**, parce qu'il produit un rapport d'exécution complet et rassurant sur un périmètre devenu faux.

---

# Partie 2 — Ce que font les outils qui touchent aux données d'autrui

L'angle complémentaire demandé par le ticket. Ces outils ont tous le même problème : accéder au schéma d'un tiers qu'ils n'ont pas écrit. Leurs solutions convergent de façon frappante.

## Debezium — l'accès en lecture le plus abouti, et ce qu'il facture

Debezium est la référence pour le patron 2 en lecture seule. Son coût d'intégration réel, tel que sa propre documentation l'énonce, est considérable ([Debezium, connecteur PostgreSQL](https://raw.githubusercontent.com/debezium/debezium/main/documentation/modules/ROOT/pages/connectors/postgresql.adoc)) :

**Configuration serveur.** `wal_level = logical`, paramètre qui n'est pas modifiable à chaud — sur RDS, la documentation note qu'*« If the `wal_level` is not set to `logical` after you make the preceding change, it is probably because the instance has to be restarted after the parameter group change. »* **Redémarrer la base d'un client pour brancher un service est un coût d'intégration, pas un détail de configuration.**

**Privilèges.** *« To provide a user with replication permissions, define a PostgreSQL role that has at least the `REPLICATION` and `LOGIN` permissions »*. Pour créer une publication : *« Replication privileges in the database to add the table to a publication »*, *« `CREATE` privileges on the database to add publications »*, *« `SELECT` privileges on the tables to copy the initial table data »*. Et surtout — *« To add tables to a publication, the user must be an owner of the table. But because the source table already exists, you need a mechanism to share ownership with the original owner. »* La procédure documentée consiste à **transférer la propriété des tables du client à un rôle partagé** (`ALTER TABLE <table_name> OWNER TO REPLICATION_GROUP`). C'est une intrusion majeure dans la gouvernance du schéma d'autrui. À défaut : *« The connector user must have superuser permissions to create this publication, so it is usually preferable to create the publication before starting the connector for the first time. »*

**Fidélité de la donnée, à la charge du client.** *« REPLICA IDENTITY is a PostgreSQL-specific table-level setting that determines the amount of information that is available to the logical decoding plug-in for UPDATE and DELETE events. »* Avec le défaut, les événements ne portent que *« the previous values for the primary key columns of a table if that table has a primary key »* ; il faut `FULL` pour obtenir *« the previous values of all columns in the table »*. **Un service RGPD qui veut savoir *ce qui a été effacé* a besoin de `FULL` — donc d'une modification de chaque table du client, avec son coût en volume de WAL.** Effet de bord documenté sur les colonnes TOAST : *« any unchanged TOAST column value that is not part of the replica identity is not contained in the event […] the connector returns a placeholder value as defined by the connector configuration property, `unavailable.value.placeholder`. »*

**Un mode de panne qui frappe le client, pas le service.** *« In certain cases, it is possible for PostgreSQL disk space consumed by WAL files to spike or increase out of usual proportions. »* Le cas le plus vicieux est décrit précisément : une base peu active surveillée à côté d'une base très active sur la même instance — *« As WAL is shared by all databases, the amount used tends to grow until an event is emitted by the database for which Debezium is capturing changes. »* La parade est un dispositif à installer, `heartbeat.interval.ms`, éventuellement avec écriture périodique par `heartbeat.action.query`. **Brancher un consommateur de réplication logique, c'est se rendre capable de remplir le disque du client.**

**Et un avertissement sur la panne silencieuse** qui vaut d'être cité en entier : *« If you permit multiple connectors to capture from a replication slot, you risk data loss, because a replication slot can emit each change only once. When multiple connectors share a slot, PostgreSQL delivers each change event to only one of the competing consumers. The other connectors never receive those events, resulting in incomplete data capture. PostgreSQL provides no warnings when this misconfiguration occurs, making the data loss silent and difficult to detect. »*

Enfin, la limite qui décide de la quatrième colonne : *« Logical decoding does not support DDL changes. This means that the connector is unable to report DDL change events back to consumers. »* Debezium rafraîchit son schéma en mémoire quand une donnée incompatible arrive — *« all plug-ins will refresh schema metadata from the database upon detection of a schema change during streaming »* — et concède que la synchronisation n'est pas garantie : *« the default value present in the Kafka schema is not guaranteed to always be in-sync with the default value in the database schema »*, avec des valeurs qui *« may appear 'late' »* ou *« 'early' »*. **Le maître du CDC ne détecte pas les changements de schéma : il en subit les conséquences et se rattrape.**

## Greenmask — la validation avant exécution

Greenmask illustre la troisième condition de tenabilité du patron 2. Il ne cherche pas à deviner : il compare le déclaré au réel et refuse de partir en cas d'écart. *« The validate command offers a way to assess the impact on both schema (validation warnings) and data (transformation and displaying differences), allowing you to validate the schema and data transformations »*, et le motif explicite est **la fuite de données** : *« Schema diff helps to avoid data leakage when schema changed. »* ([Greenmask](https://docs.greenmask.io/latest/) et [*Architecture*](https://docs.greenmask.io/latest/architecture/)).

Il délègue par ailleurs la partie qu'il ne veut pas réimplémenter : *« Greenmask delegates schema dumping and restoration to PostgreSQL's native utilities (`pg_dump` and `pg_restore`) »*. **Ne pas réécrire ce que l'écosystème du SGBD sait déjà faire est un choix récurrent chez ces outils.**

## PostgreSQL Anonymizer — la déclaration dans le schéma lui-même

L'approche la plus radicale sur la localisation de la cartographie : *« a declarative approach of anonymization, meaning you can declare the masking rules using the PostgreSQL Data Definition Language (DDL) and specify your anonymization policy inside the table definition itself »*, sous forme d'étiquettes de sécurité — `SECURITY LABEL FOR anon ON COLUMN player.name IS 'MASKED WITH FUNCTION anon.fake_last_name()'` ([PostgreSQL Anonymizer](https://access.crunchydata.com/documentation/postgresql-anonymizer/latest/)).

**L'intérêt architectural pour la quatrième colonne est direct :** une règle attachée à la colonne par `SECURITY LABEL` disparaît avec la colonne. Il n'y a pas de cartographie externe à réconcilier, donc pas de dérive possible entre les deux. Le prix est symétrique : la cartographie vit chez le client, dans son schéma, et il faut qu'il l'écrive et l'entretienne. La justification donnée est celle citée plus haut — *« masking rules should be written by the people who develop the application »* — et la seconde raison invoquée est la surface d'exposition : garder le masquage *« directly inside the PostgreSQL instance without using an external tool is crucial to limit the exposure and the risks of data leak. »* Cet argument-là résonne mot pour mot avec le motif d'auto-hébergement de l'[ADR-0001](../adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md).

## Flyway — comment on adopte un schéma préexistant

Le problème « prendre en charge une base qu'on n'a pas créée » a une réponse canonique côté migrations : le *baseline*. Le réglage `baselineOnMigrate` *« automatically calls baseline when migrate is executed against a non-empty schema with no schema history table »*, et il est présenté comme *« a convenience instead of running a separate baseline step for initial Flyway production deployments on projects with an existing DB »*. L'avertissement qui l'accompagne est exactement du registre de cette recherche : *« Be careful when enabling this as it removes the safety net that ensures Flyway does not migrate the wrong database in case of a configuration mistake! »* ([Flyway, *Baseline On Migrate Setting*](https://github.com/flyway/flyway/blob/main/documentation/Reference/Configuration/Flyway%20Namespace/Flyway%20Baseline%20On%20Migrate%20Setting.md)).

**La leçon transposable :** l'automatisme qui rend le branchement facile est le même qui supprime le garde-fou. C'est le compromis central de tout ce document.

## Fides — le seul à traiter la finalité exacte

Fides est le seul outil du corpus dont la finalité est la nôtre : exécuter une demande d'exercice de droits sur la base d'autrui. Sa réponse est **entièrement déclarative, jamais introspective**, et son échec est **bruyant par conception** (voir Partie 1 § 5). Les *Datasets* annotés *« will be automatically traversed when Fides executes a privacy request, and will either return or update the requested data according to the associated privacy request policy »* ([Ethyca, *Datasets and databases*](https://www.ethyca.com/docs/dev-docs/configuration/integrations/database-integrations/datasets)).

**Le fait le plus instructif de toute cette recherche est ce que Fides a choisi de ne pas faire.** Un outil commercial de conformité, dont l'argument de vente serait « branchez-le, il trouve tout seul », déclare explicitement l'inverse : *« fidesops does not do automated schema discovery. It is only aware of the fields you declare. »*

---

# Partie 3 — Comparaison synthétique

## La grille resserrée

| Patron | Exige de l'application | Garantit | Mode de panne dominant | Bruyant ? | Tenable si le tiers évolue sans prévenir |
| --- | --- | --- | --- | --- | --- |
| **1. Adaptateur implémenté par le client** | du **code de traduction** ; aucun accès base | la **forme** des échanges, jamais l'effet | implémentation partielle | 🔇 silencieux — 🔊 bruyant **si** suite de contrat en intégration continue | **Oui, structurellement.** Le service ignore le schéma. Condition : le contrat tourne chez le client. |
| **2. Accès direct à la base** | des **privilèges** ; aucun code | atomicité, exhaustivité **du périmètre connu** | table ou colonne ajoutée jamais visitée | 🔇 silencieux par défaut | **Non par construction.** Tenable seulement avec inventaire déclaré + *diff* de schéma + arrêt sur écart. |
| **3. Événements / *webhooks*** | du **code métier** + idempotence | la **traçabilité de l'ordre** et de l'accusé | silence, ou accusé mensonger | 🔇 le plus silencieux des cinq — 🔊 **si** échéance côté service | **Oui**, aux mêmes conditions que 1, plus une échéance et un format versionné. |
| **4. *Strangler* / passerelle** | la **maîtrise du point d'entrée**, et l'accès au code du client | interception **exhaustive de ce qui y passe** | contournement par un chemin non intercepté | 🔇 silencieux (contournement) — 🔊 bruyant (panne de passerelle) | **Non.** Une route ajoutée la contourne. Patron de *remplacement*, pas de branchement. |
| **5. Cartographie déclarative** | un **document**, versionnable, relu par un humain | l'**exhaustivité du déclaré**, et le refus d'exécuter l'incompris | cartographie obsolète, ou incomplète dès l'origine | 🔊 **bruyant si validé** (graphe inatteignable, *diff*) — 🔇 sinon, le plus dangereux des cinq | **Oui, à la condition unique de valider contre le schéma réel et d'échouer sur écart.** |
| *5 bis.* **Introspection seule** | rien | **rien** | faux positifs et faux négatifs indiscernables | 🔇 | **Non.** Utile pour ébaucher et pour détecter la dérive ; jamais comme source de vérité. |

## Quel patron pour quelle exigence

| Exigence | Patron qui la sert |
| --- | --- |
| Effort d'intégration **nul en code** chez le client | 2 (privilèges seuls) — au prix de la garantie |
| Effort d'intégration **relisable par un non-développeur** | 5 (un document) |
| **Atomicité** d'un effacement multi-tables | 2 seul |
| **Preuve écrite** de ce qui a été demandé et répondu | 3 |
| **Découplage du schéma** du client | 1 et 3 |
| **Détection de la dérive** de schéma | 5 validé (le seul), 2 outillé d'un *diff* |
| **Exhaustivité** vérifiable | 5 validé — et rien d'autre |
| Respect de l'**article 19** (énumérer les destinataires) | 5 (la cartographie *est* l'inventaire des destinataires) |

## Les incompatibilités

C'est la partie la moins souvent écrite, et la plus utile.

**1 et 2 s'excluent sur la garantie, pas sur la technique.** Rien n'interdit techniquement de faire les deux. Mais la raison d'être du patron 1 est l'*anti-corruption layer* — *« ensure that dependencies on outside subsystems don't limit an application's design »* ([Microsoft](https://learn.microsoft.com/en-us/azure/architecture/patterns/anti-corruption-layer)) — et un accès direct à la base réintroduit précisément la dépendance que l'adaptateur existait pour supprimer. **Faire les deux, c'est payer l'adaptateur et n'en garder aucun bénéfice.** L'*integration database* de Fowler décrit l'état terminal de cette confusion.

**2 et 3 s'excluent sur la propriété de l'écriture.** Si le service écrit en base *et* demande à l'application d'écrire, deux auteurs modifient les mêmes lignes sans transaction commune ni ordre garanti. Le patron *outbox* n'est pas une réconciliation des deux : il est **la subordination de 3 à 2 chez le client** — le client écrit sa donnée et son événement dans la même transaction locale ([Debezium](https://raw.githubusercontent.com/debezium/debezium/main/documentation/modules/ROOT/pages/transformations/outbox-event-router.adoc)). Il faut choisir qui écrit ; il n'y a pas de version où les deux écrivent sans arbitre.

**4 est incompatible avec l'hypothèse de départ du ticket.** Microsoft énonce la contre-indication frontalement : le patron ne convient pas quand *« You can't access the legacy system's source code »* ([Microsoft, *Strangler Fig*](https://learn.microsoft.com/en-us/azure/architecture/patterns/strangler-fig)). Un service « facilement branchable » sur une application déjà développée est, par définition, dans ce cas. Seule **Event Interception** survit à l'amputation de la trajectoire de remplacement.

**5 est compatible avec tout — et n'est suffisant avec rien.** La cartographie déclare *où* sont les données ; elle ne les lit ni ne les écrit. Elle sert d'entrée au patron 2 (que lire, que modifier), au patron 1 (quels ordres émettre), au patron 3 (quel périmètre annoncer). **Elle est le seul des cinq qui soit un prérequis plutôt qu'une alternative.** Corollaire : la question « 1, 2 ou 3 ? » et la question « 5 ou pas ? » ne sont pas la même question, et les confondre est l'erreur la plus coûteuse identifiée ici.

**5 et 5 bis s'excluent frontalement.** Une cartographie qu'on complète par de la découverte automatique n'est plus une cartographie : c'est une liste dont on ne sait plus si l'absence d'une entrée signifie « absent du schéma » ou « non déclaré ». **La propriété qui fait toute la valeur du patron 5 — l'échec bruyant sur l'inatteignable, cité de [Fides](https://github.com/ethyca/fidesops/blob/main/docs/fidesops/docs/guides/query_execution.md) — disparaît dès qu'un mécanisme de repli devine à sa place.**

## Le seul invariant qui traverse les cinq

Aucun patron ne rend un branchement à la fois **peu coûteux pour le client** et **garanti pour le service**. Les cinq répartissent ce compromis différemment, et le curseur est toujours le même : **ce que le client accepte d'écrire**.

- Il n'écrit rien → patron 2 → aucune garantie de tenue dans le temps.
- Il écrit un document → patron 5 → garantie proportionnelle à la fraîcheur du document, avec un mécanisme de détection.
- Il écrit du code → patrons 1 ou 3 → découplage du schéma, garantie proportionnelle à la discipline de contrat.
- Il ouvre son point d'entrée et son code → patron 4 → garantie forte sur un périmètre étroit, non tenable dans la durée.

Et un second invariant, plus dur : **tous les modes de panne par défaut sont silencieux, sauf un.** Le seul mécanisme trouvé dans les sources primaires qui échoue bruyamment sans dispositif supplémentaire est l'erreur de collection inatteignable de Fides. Partout ailleurs, le bruit s'achète — suite de contrat, échéance, *schema diff*, `heartbeat`.

---

# Partie 4 — Ce qui reste incertain

Points relevés comme non instruits, à ne pas confondre avec des conclusions.

1. **Aucune mesure d'effort d'intégration réel.** Le ticket demande « l'effort d'intégration réel » du patron 1. Les sources primaires décrivent des mécanismes, jamais des coûts en jours-homme. Cette recherche n'en produit aucun chiffre et aucune source ne permettrait d'en produire un honnêtement.

2. **La cartographie déclarative dans un dépôt .NET n'a pas d'implémentation de référence.** Fides est en Python, PostgreSQL Anonymizer est une extension C. Le format du document (YAML à la Fides, attributs EF Core, `SECURITY LABEL` en base) et son emplacement (chez le client, chez le service, dans le schéma) restent ouverts. Aucune source primaire ne tranche.

3. **La détection de dérive n'a pas été instruite dans le détail.** On sait que Greenmask fait un *schema diff* et que Fides échoue sur graphe inatteignable ; on n'a pas lu leurs implémentations. La question « que compare-t-on exactement, et à quelle fréquence » n'est pas résolue.

4. **L'écriture par CDC n'existe pas.** Debezium capture ; il n'écrit pas dans la base source. Le patron 2 en **écriture** n'a donc pas d'outil de référence dans le corpus étudié — tous les outils lus sont soit en lecture (Debezium, `pg_dump`), soit sur une copie (Greenmask, masquage statique). **C'est un vide notable : l'écriture dans le schéma d'un tiers n'a pas d'outillage mûr documenté.**

5. **Le rapport de ces patrons au périmètre actuel du service n'est pas tranché** — et ne l'est pas ici volontairement. Le service rend aujourd'hui une `Qualification` en **aide à la décision**, *« sans jamais la trancher »* ([CONTEXT.md](../../CONTEXT.md)). Rien dans ce document ne dit qu'il doive exécuter le droit qualifié. La question « faut-il seulement lire et modifier les données du client ? » précède le choix d'un patron, et elle appartient à un autre ticket.

6. **La cohabitation avec un référentiel de politiques n'a pas été instruite.** OPA a été lu pour son partage décision/exécution, pas comme candidat. Savoir si une cartographie déclarative gagnerait à être exprimée en politiques évaluables reste ouvert.

---

## Sources primaires consultées

**Littérature d'architecture**

- Martin Fowler, [*StranglerFigApplication*](https://martinfowler.com/bliki/StranglerFigApplication.html)
- Martin Fowler, [*IntegrationDatabase*](https://martinfowler.com/bliki/IntegrationDatabase.html)
- Ian Cartwright, Rob Horn, James Lewis, [*Patterns of Legacy Displacement*](https://martinfowler.com/articles/patterns-legacy-displacement/) (martinfowler.com)
- Ian Robinson, [*Consumer-Driven Contracts*](https://martinfowler.com/articles/consumerDrivenContracts.html) (martinfowler.com)
- Microsoft, [*Anti-Corruption Layer Pattern*](https://learn.microsoft.com/en-us/azure/architecture/patterns/anti-corruption-layer)
- Microsoft, [*Strangler Fig Pattern*](https://learn.microsoft.com/en-us/azure/architecture/patterns/strangler-fig)
- Microsoft, [*Asynchronous Request-Reply Pattern*](https://learn.microsoft.com/en-us/azure/architecture/patterns/async-request-reply)

**Documentation de projets et spécifications**

- Debezium, [connecteur PostgreSQL](https://raw.githubusercontent.com/debezium/debezium/main/documentation/modules/ROOT/pages/connectors/postgresql.adoc) (source AsciiDoc du dépôt `debezium/debezium`)
- Debezium, [*Outbox Event Router*](https://raw.githubusercontent.com/debezium/debezium/main/documentation/modules/ROOT/pages/transformations/outbox-event-router.adoc)
- PostgreSQL, [*Logical Replication — Restrictions*](https://www.postgresql.org/docs/current/logical-replication-restrictions.html)
- PostgreSQL, [*Logical Replication — Security*](https://www.postgresql.org/docs/current/logical-replication-security.html)
- PostgreSQL, [*Row Security Policies*](https://www.postgresql.org/docs/current/ddl-rowsecurity.html)
- Greenmask, [présentation](https://docs.greenmask.io/latest/) et [*Architecture*](https://docs.greenmask.io/latest/architecture/)
- PostgreSQL Anonymizer, [documentation](https://access.crunchydata.com/documentation/postgresql-anonymizer/latest/)
- Flyway, [*Baseline On Migrate Setting*](https://github.com/flyway/flyway/blob/main/documentation/Reference/Configuration/Flyway%20Namespace/Flyway%20Baseline%20On%20Migrate%20Setting.md) (dépôt `flyway/flyway`)
- Microsoft, [*Reverse Engineering — EF Core*](https://learn.microsoft.com/en-us/ef/core/managing-schemas/scaffolding/)
- Ethyca / Fides, [*Data Mapping Annotations*](https://www.ethyca.com/docs/data-mapping/guides/data-mapping-annotations), [*Datasets and databases*](https://www.ethyca.com/docs/dev-docs/configuration/integrations/database-integrations/datasets), [*Dataset* (Fides Language)](https://ethyca.github.io/fideslang/resources/dataset/)
- Fidesops, [*Define Datasets*](https://ethyca.github.io/fidesops/guides/datasets/) et [*Query Execution*](https://github.com/ethyca/fidesops/blob/main/docs/fidesops/docs/guides/query_execution.md)
- Open Policy Agent, [documentation](https://www.openpolicyagent.org/docs/latest/)
- Standard Webhooks, [spécification](https://raw.githubusercontent.com/standard-webhooks/standard-webhooks/main/spec/standard-webhooks.md) et [présentation](https://www.standardwebhooks.com/)

**Texte réglementaire**

- RGPD, articles 12 et 19, [texte du chapitre III](https://www.cnil.fr/fr/reglement-europeen-protection-donnees/chapitre3) (CNIL)
</content>
</invoke>
