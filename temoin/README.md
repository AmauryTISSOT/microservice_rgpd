# Le témoin : « Brocanto »

Le banc d'essai de la [carte #57](https://github.com/AmauryTISSOT/microservice_rgpd/issues/57).
Une application tierce **fictive mais ordinaire**, contre laquelle la branchabilité du service se
mesure en branchant — comme l'[ADR-0001](../docs/adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md)
s'est tranché sur un corpus annoté et non sur des intuitions.

> **Brocanto**, brocante en ligne entre particuliers, ouverte en 2019 sur la reprise d'une ancienne
> boutique PrestaShop. Quatre-vingt-dix mille annonces, deux personnes à la technique.

## La contrainte cardinale

**Cette application est écrite comme si le microservice n'existait pas.**

Rien dans `db/schema.sql`, `db/seed.sql` ni dans aucune route de la brocante ne connaît le RGPD,
l'exercice des droits ou ce dépôt. Pas de `deleted_at` bien placé, pas d'abstraction opportune, pas
d'identifiant de personne unique et propre. Toute complaisance ruinerait sa valeur de témoin : un
témoin qui se laisse brancher parce qu'on l'a écrit pour ça ne démontre rien.

**La seule exception est l'adaptateur** (`adapter_rgpd.py`, `rgpd_boutique.py`, `rgpd_journal.py`),
et elle ne se cache pas : c'est le programme que le client écrit pour être appelé, et le contrat
suppose qu'il existe. `app.py` n'en connaît que le montage — un import et un `register_blueprint`,
en bas de fichier. Ce qui compte est que rien **au-dessus** ne s'y adapte : aucune route, aucune
colonne, aucune requête de la brocante n'a bougé pour lui.

**Modifier le témoin pour faciliter une intégration est donc interdit sans décision explicite.** Si un
ticket d'intégration bute sur le témoin, c'est un résultat, pas un obstacle : il se consigne. La seule
raison légitime de le toucher est de le rendre **plus** ordinaire, ou d'y ajouter un piège qu'une
application réelle aurait.

Ce fichier-ci est la seule pièce du dossier qui s'adresse à nous ; il est en dehors de l'application.

## Le stack, et pourquoi il n'est pas le nôtre

Python 3.12 / Flask / MariaDB 11, SQL écrit à la main, aucun ORM, aucune migration — un seul
`schema.sql` qu'on édite. Le service, lui, est en .NET sur PostgreSQL.

L'écart est délibéré : un témoin en .NET aurait laissé passer des facilités invisibles — partager un
`DbContext`, une migration EF, un type. Une base MariaDB derrière une application Python force chaque
chemin d'accès aux données à être **explicite**, ce que le ticket
[#63](https://github.com/AmauryTISSOT/microservice_rgpd/issues/63) devra trancher.

## Faire tourner

```sh
docker compose up --build          # http://localhost:8080
docker compose down -v             # remet la base à zéro
```

La base est aussi jointe directement sur `localhost:3307` (`brocanto` / `brocanto`), ce qui permet à
un ticket d'intégration d'essayer la voie « accès direct à la base » sans passer par l'application.

L'administration est derrière une clé en clair dans l'URL :
`http://localhost:8080/admin/clients?cle=brocanto2019`.

La recette de l'adaptateur (voir plus bas) se joue sans base ni conteneur :

```sh
pip install -r requirements-dev.txt   # `requirements.txt` n'a pas bougé : pytest est en plus, à côté
pytest                                # depuis `temoin/`
```

## Le branchement — ce qui a été fait, et ce qui ne l'a pas été

Ticket [#88](https://github.com/AmauryTISSOT/microservice_rgpd/issues/88). Deux choses ont été
ajoutées au témoin, et deux seulement, chacune le rendant **plus** ordinaire — la seule raison
légitime d'y toucher. Les sections 3 et 4 ne sont pas des ajouts au témoin : ce sont les capacités
que l'adaptateur sert, ce qu'elles lui ont coûté, et ce qu'elles ont fait découvrir de Brocanto.

### 1. Un `Adapter` HTTP, servant deux systèmes

`adapter_rgpd.py` monte sous `/rgpd` la seule surface qui sache que le service existe ;
`rgpd_boutique.py` et `rgpd_journal.py` répondent à `locate` — et, depuis le § 4, à `read` — pour
deux `system_id` :

| `system_id` | Nature du stockage | Ce qu'il rend |
| --- | --- | --- |
| `brocanto-boutique` | La base MariaDB | Des références de lignes, table par table — et des **réserves** là où un nom ne tranche pas |
| `brocanto-journal` | Le journal applicatif à plat, fichiers tournés compris | Un nom de fichier par fichier portant la personne |

**Deux natures plutôt qu'une** : un contrat qui ne saurait dire que « SELECT » n'aurait pas été
éprouvé. Le journal apporte aussi le `202` — au-delà de cinq mégaoctets à relire ligne à ligne, il
déclare l'échéance de la passe de nuit au lieu de tenir la connexion.

Le secret arrive en en-tête `X-RGPD-Secret` et se compare avec `hmac.compare_digest` ; il vient de
`RGPD_ADAPTER_SECRET`, et un déploiement qui l'oublie refuse tous les appels. **`requirements.txt`
n'a pas bougé** : `hmac`, `re` et `datetime` sont de la bibliothèque standard.

L'adaptateur est ainsi **plus rigoureux que son hôte** — `compare_digest` ici, un `==` sur une clé
de query string pour l'administration deux cents lignes plus haut. Ce n'est pas une incohérence à
corriger : c'est le prix de la branchabilité, et il se paie dans l'adaptateur, pas dans le témoin.

Ses tests vivent ici (`tests/`, `pytest`, sur le modèle de `src/sidecar/tests/`) et n'entrent jamais
dans la solution .NET. Aucun ne joint MariaDB ni ne lit le vrai journal : les coutures sont le
**lecteur** de sondes et le dossier de journal.

### 2. Une porte d'entrée — un défaut de conformité **antérieur**

`/contact` n'existait pas. Ni route, ni `mailto`, ni la moindre adresse écrite quelque part : la
table `messages` était alimentée par on ne sait quoi et exposée par rien. Une personne concernée
n'avait donc **aucun moyen d'adresser une demande** à Brocanto — ce que l'art. 12 exige de faciliter.

**Ce défaut est antérieur au branchement, et il est consigné comme tel** : il n'a pas été découvert
en branchant, il a été découvert en cherchant par où une demande entrerait. La porte ajoutée est
celle qu'une application ordinaire aurait — un formulaire de contact qui écrit dans `messages` —
et non une porte « RGPD » : rien dans `/contact` ne parle de droits, de demande d'exercice ni du
service. Le témoin reste écrit comme si le microservice n'existait pas.

### Ce qui n'a délibérément pas été fait

- **Aucune iframe, aucun lien de navigation vers la GUI du service.** Une ligne de HTML qui ne rend
  le témoin ni plus ni moins ordinaire est du confort d'intégration pur.
- **Aucune facilité de schéma** : pas une colonne, pas un index, pas une vue. `db/schema.sql` est
  intact.
- **`localhost:3307` reste inutilisé.** La voie « accès direct à la base » était ouverte et
  documentée ci-dessus ; le branchement ne l'a pas empruntée, et **c'est un résultat, pas un
  oubli** : tout passe par l'`Adapter`, donc par du code que le client écrit, relit et déploie.
- **Quatre systèmes restent au niveau 0** — recensés, touchés à la main, sans `Capability` :
  l'export mensuel parti chez l'agence, les médias sur disque, le prestataire de paiement, et les
  archives compressées du journal, que l'adaptateur laisse fermées. C'est le régime majoritaire, et
  celui dont le service tire le plus de valeur : il nomme ce qu'il ne touche pas.

  La reprise de 2019 (`clients_ancienne_boutique`), elle, **n'est pas** un cinquième système : elle
  vit dans la même base et se compte avec `brocanto-boutique`. Un système est une unité de
  recensement, et rien n'obligeait à la découper — mais le jour où on la découperait, l'adaptateur
  la servirait déjà.

Deux réserves valent d'être écrites, parce qu'une réponse servie sans elles se lirait comme un fait
complet :

- **Le texte libre n'est pas fouillé.** Les descriptions d'annonces et le corps des messages
  portent des numéros et des adresses écrits à la main (pièges 14 et 15) que les sondes ne trouvent
  pas. L'absence de `messages` veut dire « aucun message *écrit par* cette adresse », jamais « aucun
  message qui parle d'elle ».
- **Le journal ne se cherche que par adresse.** Une ligne ne porte que celle de la session : y
  chercher un nom ne trouverait rien, et y chercher une référence comme « 1203 » rattacherait des
  horaires et des identifiants d'annonce. Un sac sans adresse ne fait donc ouvrir aucun fichier.

### 3. Ce que la forme de `locate` a coûté, et ce qu'elle a révélé

Ticket [#92](https://github.com/AmauryTISSOT/microservice_rgpd/issues/92). Les deux systèmes
rendaient un **dénombrement** — « 3 enregistrements sur 6 tables ». Ils rendent désormais ce que le
contrat demande : des **références opaques**, et des **réserves motivées** là où Brocanto ne sait pas
trancher. Trois choses en sont sorties, dont deux qu'on ne cherchait pas.

**Le compte est perdu, et c'était le prix.** « 412 passages dans le journal » disait quelque chose de
l'intensité d'un usage ; `brocanto.log` ne le dit pas. Le service n'en faisait rien de bon — il ne
peut ni le vérifier ni le comparer d'un système à l'autre — mais la perte est réelle, et elle est
consignée ici plutôt que passée sous silence.

**Deux sondes par table, là où le dénombrement en imposait une.** Tant qu'on comptait, deux requêtes
sur `clients` auraient compté deux fois la personne trouvée par son nom *et* par son adresse. Une
référence, elle, se dédoublonne. C'est ce changement de forme qui a permis de séparer la question
qui **identifie** — adresse, téléphone, référence — de celle qui ne fait que **nommer** ; et c'est
cette séparation, et non une intention, qui a rendu le doute visible.

**Un nom ne rattache rien, jamais.** `clients` ne pose aucune unicité — le support crée un second
compte quand quelqu'un dit « je n'arrive plus à me connecter » (piège n° 2) —,
`adresses.destinataire` et `paiements.porteur_nom` nomment couramment un tiers (piège n° 5), et la
reprise de 2019 n'a jamais été rapprochée des comptes actuels (piège n° 4). Chercher « Jean Dupont »
rend donc des lignes
que rien ici ne départage : elles partent en réserves motivées, chacune proposant l'adresse de sa
ligne, et un humain tranche côté service.

⚠️ **La découverte, celle qu'on ne cherchait pas : la base et le journal ne comparent pas pareil.**
MariaDB ignore la casse ; un fichier plat n'a pas de collation, et `rgpd_journal` ne normalise rien —
parce que rien de ce qui lit ce journal ne normalise, ni `grep`, ni l'astreinte à trois heures du
matin. La conséquence est qu'une même désignation ouvre l'un et **rate l'autre** : chercher
`jean.dupont@example.fr` trouve les deux comptes de la base et zéro ligne du journal, où la session
s'est écrite `Jean.Dupont@Example.fr` (piège n° 2). Deux systèmes du même client, la même personne,
deux réponses opposées.

Personne chez Brocanto ne l'avait vu, et rien dans Brocanto ne pouvait le voir : il a fallu qu'un
tiers pose la même question aux deux d'affilée. Ce qui répare le trou n'est pas une ligne de code —
c'est la réserve que la base rend sur son homonyme, l'adresse qu'elle propose **dans la casse où
elle la stocke**, l'humain qui la rattache, et l'appel suivant qui la porte. Le dispositif entier
sert à ça, et ce cas-là est la raison pour laquelle il a cette forme.

### 4. Ce que `read` a coûté, et pourquoi presque rien

Ticket [#93](https://github.com/AmauryTISSOT/microservice_rgpd/issues/93). Les deux systèmes servent
désormais `read` en plus de `locate`. **Le coût a été dérisoire, et c'est le fait le plus important
de cette section** : le contrat n'impose **aucune forme** à la réponse.

| `system_id` | Ce que `read` rend | Ce que ça a coûté |
| --- | --- | --- |
| `brocanto-boutique` | Un CSV, une section par table | Les mêmes emplacements, `SELECT *` au lieu de `SELECT clé`, et le module `csv` de la bibliothèque standard |
| `brocanto-journal` | Les lignes portant l'adresse, en texte brut | La même lecture que `locate`, poussée jusqu'au bout au lieu de s'arrêter à la première ligne |

**`requirements.txt` n'a toujours pas bougé.** Aucun schéma à apprendre, aucun vocabulaire commun à
adopter, aucune traduction de nos tables dans les mots de quelqu'un d'autre : on rend l'export que la
brocante sait déjà écrire — `/admin/export/ventes.csv` en écrit un depuis 2019 — et le service le
recopie sans l'ouvrir. C'est cette gratuité qui fait qu'une capacité est déclarable ; l'inverse est
la raison pour laquelle, sur le terrain, elles ne le sont pas.

**Le droit part, et il ne change qu'une chose — chez nous.** L'appel porte `right`, et
`FOURNIES_PAR_LA_PERSONNE` en tire le seul découpage qui existe : une portabilité (art. 20) laisse
dehors `factures` et `paiements` — que la personne ne nous a pas *fournies*, nous les avons produites
en la facturant — et `clients_ancienne_boutique`, dont la reprise de 2019 n'a jamais été rapprochée
des comptes actuels. Ce découpage ne remonte nulle part et n'a pas à remonter : le service ne connaît
aucun nom de table et n'aurait pas su l'arbitrer. Le jour où l'on nous dira que `paiements` est
portable, **une seule ligne change**.

⚠️ **Une réserve non tranchée n'ouvre aucune lecture.** `sondes_de_lecture` ne pose que les sondes
**certaines** : exporter une ligne trouvée sous un nom livrerait au demandeur les données d'un
homonyme — une violation dans l'autre sens, et le piège n° 2 s'y prête exactement. Les deux moitiés
du dispositif disent la même chose sans s'être concertées : le service ne lit que là où il a
rattaché, et nous ne rendons que ce que nous rattachons.

**Le corps vide est gratuit, et c'est délibéré.** Une personne qu'aucune table ne porte rend un `200`
sans octets — pas un `204`, pas une absence de réponse. S'il avait fallu produire un export d'une
forme convenue pour dire « on a regardé, il n'y a rien », on aurait été tenté de ne rien dire du
tout.

⚠️ **Ce que le droit ne change pas dans le journal, et pourquoi.** Une ligne de journal est un seul
objet : elle n'a pas de colonnes dont on pourrait dire que la personne les a fournies et d'autres
non. `Portability` et `Access` y rendent donc la même chose. Ce n'est pas un oubli — c'est un fait de
cette nature de stockage, et il est écrit ici pour que personne ne le prenne pour un défaut.

### La clause de périmètre, et ce que la recette en fait

Le contrat exige **un secret partagé _et_ un `Adapter` hors d'atteinte de l'extérieur**, les deux
ensemble. Ici, l'adaptateur vit dans le même processus Flask que la boutique et sort donc par le
même port — `8080`, publié sur l'hôte par `compose.yaml`. C'est exactement ce qu'un client ferait,
et c'est ce qui rend l'intégration bon marché ; **ce n'est pas conforme en production** pour autant.
Un déploiement réel doit fermer `/rgpd` à tout ce qui ne vient pas du réseau du service — un filtre
devant l'application, pas une ligne de Python de plus. En recette, `8080` n'est joignable que depuis
la machine de développement : la clause tient par l'endroit où tourne le conteneur, et c'est écrit
ici pour que personne ne croie qu'elle tient par le secret seul.

## Les pièges effectivement posés

C'est cette liste que les tickets d'intégration devront affronter. Chaque piège est réel : il se
rencontre dans les applications qu'on brancherait pour de vrai.

### L'identité de la personne

1. **La personne n'a pas d'identifiant unique.** `clients.id` n'est la clé qu'à l'intérieur de
   `clients`. Ailleurs on la désigne par son adresse électronique : `newsletter.courriel` (clé
   primaire), `messages.auteur_courriel`, `commandes.courriel_acheteur`,
   `clients_ancienne_boutique.mail`. Aucune de ces tables ne porte de clé étrangère vers `clients`.
2. **Aucune unicité sur `clients.email`.** Les comptes 1 et 8 sont la même personne :
   `jean.dupont@example.fr` et `Jean.Dupont@Example.fr`, créés à dix-huit mois d'écart par le support.
   Le rapprochement suppose une normalisation de casse — donc une décision, pas une jointure.
3. **Des personnes sans compte.** La commande `BRC-2026-0043` a `client_id` à `NULL` : Hélène Petit
   n'existe dans la base que par son adresse, répartie sur `commandes`, `factures`, `paiements`,
   `newsletter` et `messages`.
4. **Une table de reprise jamais rapprochée.** `clients_ancienne_boutique` garde depuis 2019 quatre
   personnes, dont deux (`patrick.roussel@`, `jean.dupont@`) qui ont aussi un compte moderne, et deux
   qui n'en ont jamais eu.
5. **Des tiers qui ne sont pas clients.** `adresses.destinataire` peut nommer quelqu'un d'autre —
   « Yvette Dupont » n'a jamais rien commandé et figure pourtant dans la base et sur une facture.

### Ce qu'on ne peut pas effacer

6. **Les factures sont scellées.** `factures` gèle nom, adresse et courriel du destinataire, sous
   conservation légale de dix ans (art. L123-22 c. com.). Le droit à l'effacement s'y arrête et doit
   savoir le dire.
7. **La donnée de paiement est chez un tiers.** `paiements` ne garde qu'une `reference_psp` chez
   Stripe ou PayPal. Effacer localement ne touche pas le sous-traitant.

### La donnée dupliquée et dérivée

8. **Le courriel est recopié au moment de la commande.** `commandes.courriel_acheteur` est un
   instantané : il peut différer de `clients.email` si la personne a changé d'adresse depuis.
9. **L'adresse de livraison est dénormalisée en texte libre.** `commandes.adresse_livraison` est un
   `TEXT` multiligne, recopié, non structuré.
10. **Les statistiques ne se rattachent que par jointure.** `stats_annonces_jour` ne porte aucune
    identité ; il faut passer par `annonces.vendeur_id` pour savoir de quel vendeur relèvent ces
    agrégats — et décider si un agrégat est une donnée personnelle.

### La donnée hors de la base

11. **Les journaux applicatifs portent adresses et IP.** `logs/brocanto.log.1` (le fichier tourné par
    logrotate) associe une adresse électronique à une adresse IP et à un horodatage, ligne à ligne.
    Le fichier courant n'est même pas versionné.
12. **Un export est déjà parti ailleurs.** `exports/ventes-2026-05.csv` — noms, adresses, montants —
    est envoyé chaque mois à l'agence qui fait les campagnes. Il est reproductible à volonté par
    `/admin/export/ventes.csv`.
13. **Les photos sont des fichiers sur disque.** `annonce_photos.fichier` pointe dans `medias/`, hors
    de la base et hors de toute transaction.

### La donnée personnelle dans du texte libre

14. **Les annonces contiennent des coordonnées.** Les descriptions des annonces 1, 2 et 4 donnent un
    numéro de téléphone ou une adresse postale en clair, écrits par le vendeur.
15. **La messagerie support en contient encore plus.** `messages.corps` livre le numéro de portable de
    Luc Moreau, l'identité et l'adresse de sa sœur — une personne qui n'a aucun compte — et l'adresse
    postale complète d'Hélène Petit.

### Le geste RGPD à moitié fait

16. **La désinscription existe, et ne supprime rien.** `/newsletter/desinscription` pose une date dans
    `newsletter.desinscrit_le` et conserve la ligne indéfiniment. C'est le seul geste de l'application
    qui ressemble à un droit exercé — une `Objection` honorée à moitié — et le lien ne porte aucun
    jeton : l'adresse est dans l'URL, n'importe qui peut désinscrire n'importe qui.

### Ce que l'application ne sait pas faire du tout

17. **Aucune suppression de compte.** Aucun écran, aucune route, aucune colonne.
18. **Aucun export de ses propres données.** Le seul export qui existe est celui de l'administration,
    et il est orienté ventes, pas personne.
19. **Aucune trace de qui a consulté quoi côté administration**, sinon les lignes du journal — et
    l'administration n'a pas de compte nommé, juste une clé partagée.
20. **Aucune porte d'entrée — défaut de conformité antérieur au branchement, consigné puis
    refermé.** Jusqu'au ticket #88, l'application n'exposait ni route de contact, ni `mailto`, ni
    adresse écrite nulle part ; la table `messages` n'était atteignable par aucune route. Une
    personne concernée n'avait donc aucun moyen d'adresser quoi que ce soit à Brocanto. Le défaut
    n'est pas né du branchement et ne s'est pas révélé en branchant : il s'est révélé en cherchant
    par où une demande entrerait. `/contact` l'a refermé, sous la forme qu'une application ordinaire
    aurait — voir « Le branchement » plus haut.
