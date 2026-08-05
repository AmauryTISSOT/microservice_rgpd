# Le témoin : « Brocanto »

Le banc d'essai de la [carte #57](https://github.com/AmauryTISSOT/microservice_rgpd/issues/57).
Une application tierce **fictive mais ordinaire**, contre laquelle la branchabilité du service se
mesure en branchant — comme l'[ADR-0001](../docs/adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md)
s'est tranché sur un corpus annoté et non sur des intuitions.

> **Brocanto**, brocante en ligne entre particuliers, ouverte en 2019 sur la reprise d'une ancienne
> boutique PrestaShop. Quatre-vingt-dix mille annonces, deux personnes à la technique.

## La contrainte cardinale

**Cette application est écrite comme si le microservice n'existait pas.**

Rien dans `app.py`, `db/schema.sql` ni `db/seed.sql` ne connaît le RGPD, l'exercice des droits ou ce
dépôt. Pas de `deleted_at` bien placé, pas d'abstraction opportune, pas d'identifiant de personne
unique et propre. Toute complaisance ruinerait sa valeur de témoin : un témoin qui se laisse brancher
parce qu'on l'a écrit pour ça ne démontre rien.

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
légitime d'y toucher.

### 1. Un `Adapter` HTTP, servant deux systèmes

`adapter_rgpd.py` monte sous `/rgpd` la seule surface qui sache que le service existe ;
`rgpd_boutique.py` et `rgpd_journal.py` répondent à `locate` pour deux `system_id` :

| `system_id` | Nature du stockage | Ce qu'il compte |
| --- | --- | --- |
| `brocanto-boutique` | La base MariaDB | Des enregistrements, table par table |
| `brocanto-journal` | Le journal applicatif à plat, fichiers tournés compris | Des lignes, fichier par fichier |

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
compteur de sondes et le dossier de journal.

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
- **Quatre systèmes restent au niveau 0** — recensés, touchés à la main, sans `Capability` : la
  reprise de 2019 (`clients_ancienne_boutique`), l'export mensuel parti chez l'agence, les médias
  sur disque, et le prestataire de paiement. C'est le régime majoritaire, et celui dont le service
  tire le plus de valeur : il nomme ce qu'il ne touche pas.
- **Le texte libre n'est pas fouillé.** Les descriptions d'annonces et le corps des messages
  portent des numéros et des adresses écrits à la main (pièges 14 et 15) que les sondes ne trouvent
  pas. Un `0` sur `messages` veut dire « aucun message *écrit par* cette adresse », jamais « aucun
  message qui parle d'elle ». C'est une réserve à porter dans la réponse de `locate`, et elle
  appartient au ticket #92.

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
