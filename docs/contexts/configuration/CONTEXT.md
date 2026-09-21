# Configuration

Ce contexte ne connaît que **ce qui vaut pour toutes les demandes à la fois** : le réglage du
service, sans date et sans demande. Il tient le `Settings` — à l'écran, le **Paramétrage** — qui
associe à chacun des six droits RGPD le **canal d'exercice** par lequel le service le fera
appliquer : une adresse HTTP, ou un routage RabbitMQ. **Un droit, un seul canal** (ADR-0027).

Il **configure, il n'appelle pas.** Enregistrer un canal est une écriture locale : aucune requête ne
part à la saisie, et **aucune connexion ne s'ouvre vers le broker** — enregistrer un routage ne
déclare ni ne vérifie l'exchange, la topologie du bus restant la propriété de l'exploitant. C'est
[Requests](../requests/CONTEXT.md) qui appelle, quand l'`Operator` exécute une demande.

Il est un **consommateur du noyau partagé**, aux côtés de
[Qualification](../qualification/CONTEXT.md) et de [Requests](../requests/CONTEXT.md) :
`DataSubjectRight` est la seule chose qu'il partage. Il est **fournisseur amont** de `Requests`, qui
lit le canal d'un droit pour exécuter une demande ; il ignore tout de cette lecture. [Screening](../screening/CONTEXT.md) ne communique avec lui en aucune façon. Voir
[`CONTEXT-MAP.md`](../../../CONTEXT-MAP.md).

Les identifiants du code sont en anglais (`Configuration`, `Settings`, `ExerciseChannel`,
`EndpointUrl`) ; les textes destinés à l'humain sont en français (« Paramétrage », « canal
d'exercice », « non configuré »).

## Language

### Le Paramétrage et ce qu'il porte

**Settings** :
La configuration applicative du service, **unique et propriété du service** : une seule instance,
une seule ligne en base (table `settings`, clé fixe). Elle associe chacun des six droits du
périmètre à un `ExerciseChannel` — une adresse, un routage, **ou rien**. L'écran la nomme
« Paramétrage ». Son état persistant est fait de **propriétés plates nullables** par droit, dont elle
**compose** le canal à la lecture et qu'elle **décompose** à l'écriture ; l'exclusivité « un droit,
un seul canal » est tenue **au seul endroit qui écrit**, sans valeur dormante ni colonne
discriminante.
Elle **naît paresseusement** : un service vierge n'a rien de persisté, et c'est un état complet —
les six droits s'y lisent « non configuré » sans que personne ait eu à « créer » la configuration.
La règle « il y a six droits » vit dans le code, jamais dans la base.
_Avoid_ : Manifest, Configuration, Catalogue, Registre, Profile, Preferences, Options

`Configuration` est le nom du contexte, et il nomme aussi, au registre du développeur,
`appsettings.json` et les options de déploiement. Le donner à l'agrégat aurait fait lire les deux
comme une seule chose. `Manifest` nommait le modèle que celui-ci remplace — un catalogue de
systèmes — et le reprendre ferait croire que le modèle a survécu sous un autre grain.

**EndpointUrl** :
L'adresse à laquelle le service fera appliquer un droit — une **espèce** de canal d'exercice, jamais
le genre. Impossible à construire invalide : **absolue**,
en `http` ou `https`, au plus 2 048 caractères, sans **userinfo**. Une adresse relative n'a pas
d'hôte ; une adresse qui porte `user:pw@` porterait un secret, que ce contexte ne détient pas. Le
`http` est admis au même titre que le `https` : exiger le chiffrement relève du déploiement.
**La construire n'appelle rien** — ni connexion, ni résolution de nom. Une adresse bien formée peut
désigner un hôte injoignable, et ce n'est pas à la saisie que cela se découvre.
_Avoid_ : AdapterAddress, Webhook, Callback, lien, route

**Canal d'exercice** (`ExerciseChannel`) :
Le **genre** : le moyen par lequel le service fera exercer un droit. Type **fermé à trois cas** —
une adresse HTTP, un routage RabbitMQ, et « non configuré ». `NotConfigured` est un **cas nommé, pas
un `null`** : « non configuré » est un état du glossaire, pas un trou, et le nommer rend exhaustive
toute lecture d'un canal, sans branche nulle. Un droit en porte **un seul** (ADR-0027).
_Avoid_ : transport, protocole, mode d'envoi, destination, cible, adaptateur

**Routage RabbitMQ** (`RabbitMqRouting`) :
Une **espèce** de canal : l'`ExchangeName` sur lequel publier et la `RoutingKey` avec laquelle
publier. Les deux sont rognés, non vides, et plafonnés à **255 octets UTF-8** — la limite d'un
_shortstr_ AMQP, comptée en octets et non en caractères. Le routage lui-même **ne valide rien** : il
n'a rien à vérifier que ses deux champs n'aient déjà refusé, et chaque message d'erreur se range
ainsi sous son champ de formulaire. **Le construire n'appelle rien** — ni connexion, ni résolution de
nom, ni déclaration d'exchange. Hôte, port, vhost et identifiants du broker relèvent du déploiement,
jamais de la base.
_Avoid_ : canal, bus, broker, queue, file, topic, binding

**RightChannel** :
Un droit et son canal — « non configuré » compris : ce que l'écran relit, droit par droit. Il ne
recopie ni le libellé ni l'article, qui se lisent sur le `DataSubjectRight`.
_Avoid_ : RightEndpoint, Binding, Mapping, Entry, ligne de configuration

### La connexion du déploiement au broker

**Connexion du déploiement** (`BrokerConnection`) :
Ce qui dit « **ce déploiement sait publier** ». Type **fermé à deux cas** — `Configured` et
`Absent` —, et non un booléen nu : la question se pose à plusieurs endroits — le bandeau de la face
RabbitMQ, et l'exécution d'une demande routée —, et elle doit s'y lire sous le même nom, avec la
même réponse (ADR-0028). **`Absent` est un état légal**, jamais une erreur de configuration : un
service sans bus démarre normalement, et un routage reste enregistrable avant que le broker existe.
Elle dit ce que le déploiement **déclare**, jamais ce qu'il **atteint** : aucun test réseau ne se
cache derrière elle, et une connexion déclarée devant un broker éteint est un cas prévu, dont seule
l'exécution découvrira l'échec.
_Avoid_ : broker, bus, client AMQP, connexion ouverte, disponibilité, santé

**Son état** (`IBrokerConnectionState`) :
Le seul endroit où la question se pose : une propriété, qui se lit. Elle n'ouvre rien, ne teste rien
et ne ferme rien. Ce qu'**une clé vide ou faite d'espaces** vaut — une clé absente — est écrit sur la
connexion elle-même, **dans le noyau et à ce seul endroit** : recopiée chez chaque lecteur, la règle
aurait divergé, et deux publics auraient lu deux vérités de la même configuration.
_Avoid_ : health check, ping, sonde, `IsBrokerAvailable`

**Ce que le déploiement déclare** (`RabbitMqOptions`, section `RabbitMq`) :
Hôte, port, vhost, identifiants, TLS, et le délai d'attente d'une confirmation de publication —
**10 secondes par défaut**. Cela vit dans la **configuration de déploiement**, jamais en base ni à
l'écran : aucun secret ne transite par le Paramétrage. **Pas d'hôte, pas de connexion** — état
légal ; un hôte présent avec un port ou un délai **aberrant** arrête le démarrage, comme
`HostSystem:TimeoutSeconds`, pour qu'une faute de frappe ne passe pas pour un réglage. **Aucune
chaîne de connexion URI** : les identifiants n'entrent pas dans une URL, pour le motif qui interdit
déjà l'`userinfo` dans une `EndpointUrl`.
_Avoid_ : connection string, URI AMQP, credentials en base, réglage d'écran

### Les droits, et celui qui n'en est pas un

**Les six droits configurables** :
`DataSubjectRight.List` moins `OutOfScope`, rangés dans l'ordre des articles : accès (15),
rectification (16), effacement (17), limitation (18), portabilité (20), opposition (21). L'article
19 est absent délibérément : il n'ouvre pas de droit que la personne exerce. Libellé français et
article viennent du type partagé (`FrenchLabel`, `Article`), jamais d'une seconde table.

`OutOfScope` est le **verdict** qu'aucun droit n'est exercé, pas un droit : il n'a pas de canal, et
le demander au `Settings` est une programmation fautive, refusée comme telle — à la lecture comme à
l'écriture.
_Avoid_ : sept droits, tous les droits, les droits RGPD (sans nombre)

**Non configuré** :
L'état d'un droit **sans adresse ni routage**. C'est un état **valide et normal** — celui d'un
service qu'on vient d'installer —, jamais un manque à combler, et c'est un **cas du canal**, pas un
`null`. Il se dit en toutes lettres à l'écran. Effacer le canal d'un droit le ramène à cet état —
**un seul geste pour les deux canaux** —, et n'a d'effet sur aucun autre.
_Avoid_ : manquant, incomplet, à compléter, vide, désactivé

**Les deux faces du Paramétrage** :
L'écran se tient sur **deux pages** reliées par des **onglets rendus par le serveur** : « Adresse
HTTP » (`/parametrage`) et « Routage RabbitMQ » (`/parametrage/rabbitmq`). Elles portent le
**même titre** et se lisent comme un seul écran à deux faces. Chacune se lit **en liste et
détail** : les six droits d'un côté, chacun avec son réglage en une ligne, et le **droit ouvert** de
l'autre, avec son réglage actuel et sa mini-form. Le droit ouvert est porté par l'adresse
(`?droit=Erasure`), et les onglets le gardent d'une face à l'autre. Chaque face ne **configure** que son
espèce de canal, mais les deux **racontent la même histoire** : un droit réglé sur l'autre canal y
montre ce réglage, accompagné, **avant le bouton d'enregistrement**, de l'avertissement qui
**nomme** ce qu'un enregistrement ici remplacerait. « Non configuré » ne se dit que d'un droit
**sans adresse ni routage**, et le mot garde donc le même sens sur les deux. Les onglets ne sont pas
un point d'entrée : le panneau
latéral garde ses quatre entrées, et « Paramétrage » y reste marqué courant sur les deux faces.
_Avoid_ : sous-écran, cinquième entrée, onglet JavaScript

### Ce que le contexte ne fait pas

**Il énumère, il ne compte pas.** L'écran liste les six droits et leur état ; il n'affiche ni
total, ni taux, ni ratio. Un « 4/6 configurés » se lirait comme une mesure d'avancement, et un
« 6/6 » comme une configuration **complète** — or un canal enregistré ne dit pas qu'un appel ou une
publication y aboutira.

**Il ne détient aucun secret.** Le userinfo est refusé, les identifiants du broker n'entrent pas
davantage, et l'authentification de l'appel est hors du périmètre de ce contexte.

**Il ne date pas.** Le `Settings` ne sait pas quand un canal a été posé ni par qui. La datation
par droit est hors périmètre, et son absence est assumée : un réglage n'est pas une preuve.
