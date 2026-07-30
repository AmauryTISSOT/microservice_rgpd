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
