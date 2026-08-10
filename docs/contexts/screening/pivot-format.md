# `screening-pivot/1` — la forme du relevé collé

La forme exacte du `ColumnListing` que l'`Operator` colle. Elle est écrite ici parce qu'elle a
**deux producteurs et un seul consommateur** : les trois requêtes par dialecte que
[#129](https://github.com/AmauryTISSOT/microservice_rgpd/issues/129) écrira, l'extraction du corpus
qui les rejoue, et l'ingestion de `ColumnListingIngestion`. Les clés vivent en dur des deux côtés ;
si elles ne vivent nulle part en toutes lettres, elles divergeront.

⚠️ **Ce fichier ne remplace pas le code.** Les noms de clés font foi dans
`src/MicroserviceRgpd.Core/Screenings/ColumnListingIngestion.cs`, la version de format et le plafond
dans `ColumnListing`. Ce qui est écrit ici est ce qu'un auteur de requête doit savoir avant d'écrire
son `json_object()`.

## La forme

Du **JSONL** : une ligne par colonne de la base, encadrée d'une ligne d'en-tête et d'une ligne de
fin. Le JSON parce que l'échappement des textes libres n'est alors **pas écrit par nous** —
`JSON_OBJECT()` (MariaDB), `json_build_object()` (PostgreSQL), `json_object()` (SQLite) — et parce
que `commentaire_colonne`, `commentaire_table` et `type` portent de la prose avec virgules,
apostrophes et parenthèses.

⚠️ **Une ligne par colonne, jamais une cellule de texte unique.** DBeaver, phpMyAdmin et consorts
tronquent l'affichage d'une cellule volumineuse : l'`Operator` copierait une troncature sans le
savoir, et on aurait reconstruit la troncature silencieuse dans le geste même censé la rendre
détectable.

Contrainte imposée aux trois requêtes : un `ORDER BY` **déterministe**, en-tête en premier, ligne de
fin en dernier, et les colonnes d'une même table contiguës dans leur ordre de schéma.

## L'en-tête

```json
{"format":"screening-pivot/1","dialecte":"postgresql","base":"galette_prod","genere_le":"2026-08-10T09:30:00.0000000+00:00"}
```

| clé         | ce que c'est                                                                       |
| ----------- | ---------------------------------------------------------------------------------- |
| `format`    | La version de format, en dur dans la requête. Toute autre valeur est refusée.       |
| `dialecte`  | Le SGBD, **en dur dans la requête** : le pivot le déclare seul, l'écran ne le tape pas. |
| `base`      | `current_database()`, `DATABASE()`, ou le chemin du fichier côté SQLite.            |
| `genere_le` | L'instant de génération, ISO 8601 **avec décalage** — `…+02:00` ou `…Z`.             |

⚠️ **Le décalage n'est pas facultatif, et l'ingestion refuse un instant qui n'en porte pas.** Sans
lui, l'instant prendrait celui du **serveur** : le même collage lu à Tokyo puis en UTC donnerait deux
instants distants de neuf heures, et un relevé vieux d'une journée s'afficherait comme frais. C'est
la seule chose qui dise à l'`Operator` de quand date son relevé.

⚠️ **`base` est recopié, jamais vérifié** — mais il est **borné**. C'est un repère pour l'humain qui
relit un `Screening` trois jours plus tard, jamais une identité sur laquelle bâtir une comparaison de
rapports. Il passe le même garde que le domaine (100 caractères, sans caractère de contrôle ;
64 pour `dialecte`), sans quoi un refus lisible se changerait en erreur nue un cran plus loin.
⚠️ **Côté SQLite, cela borne le chemin du fichier** : un chemin plus long est refusé au titre du cas
n° 1. Si un parc client dépasse, c'est la borne du domaine qu'il faut rouvrir, pas la frontière.

⚠️ **`dialecte` n'est pas cosmétique.** Sans lui, « cette colonne n'a pas de commentaire » et
« SQLite ne rend aucun commentaire » se lisent exactement pareil — l'`Omission silencieuse` déplacée
d'un cran, dans le contexte qui existe pour ne pas l'avoir.

## Les lignes de colonne — les neuf champs

```json
{"schema":"public","table":"adherents","colonne":"adr_l1","position":2,"type":"varchar(255)","nullable":true,"commentaire_colonne":null,"commentaire_table":"les adhérents","table_referencee":null}
```

| clé                   | type         | remarque                                                              |
| --------------------- | ------------ | --------------------------------------------------------------------- |
| `schema`              | texte        | Pour PostgreSQL, qui a un vrai multi-schéma. Constant ailleurs.        |
| `table`               | texte        |                                                                       |
| `colonne`             | texte        |                                                                       |
| `position`            | entier ≥ 0   | Le rang dans le schéma. PostgreSQL part de 1, SQLite de 0.             |
| `type`                | texte / null | **Déclaré en entier**, longueur comprise : `varchar(320)`.             |
| `nullable`            | booléen / null |                                                                     |
| `commentaire_colonne` | texte / null | `null` sur SQLite, qui n'en rend aucun.                                |
| `commentaire_table`   | texte / null | Recopié sur **chacune** des colonnes de la table.                      |
| `table_referencee`    | texte / null | La table pointée par la clé étrangère. **La colonne pointée n'entre pas.** |

⚠️ **Les neuf clés sont exigées, valeurs vides comprises.** Une clé absente n'est pas une valeur
absente : c'est une ligne qui ne vient pas de la requête. C'est ce qui distingue « SQLite ne rend
aucun commentaire », qui écrit `null`, de « cette ligne a été retouchée à la main », qui n'écrit
rien.

⚠️ **Le _type_ de chaque valeur est exigé avec la clé**, et c'est le piège le plus concret pour un
auteur de requête : l'`information_schema` de MariaDB rend la nullabilité en `'YES'`/`'NO'`, si bien
qu'un `JSON_OBJECT('nullable', IS_NULLABLE, …)` naïf émet `"nullable":"YES"` — une chaîne, pas un
booléen. Le relevé est alors **refusé** (cas n° 4). Il doit l'être : accepté, il désactiverait le
filtre de nullabilité sur toute la base, sans un mot, dans un rapport qui se lit comme complet.
Écrire `IS_NULLABLE = 'YES'` dans la requête, pas `IS_NULLABLE`.

⚠️ **`type`, `nullable` et `table_referencee` sont collectés comme _filtre_, jamais comme _signal_.**
Un `boolean`, un `decimal(10,2)`, une clé vers une table de référence **écartent** des catégories
plutôt qu'ils n'en désignent une. Le premier banc qui mesurera leur pouvoir prédictif isolé conclura
« inutiles » ; sans cette phrase, quelqu'un les retirera du pivot et supprimera le filtre du même
geste.

⚠️ **`position` n'est pas un signal non plus** : il entre parce qu'il rend détectable la troncature
**au milieu**, que la ligne de fin ne voit pas.

## La ligne de fin

```json
{"fin":true,"colonnes":412}
```

⚠️ **`colonnes` se calcule dans la même requête que les lignes**, jamais par un second passage sur le
catalogue. Deux lectures d'un catalogue vivant peuvent légitimement diverger si un `ALTER TABLE`
passe entre elles, et le service refuserait alors un pivot sincère. C'est un contrôle d'intégrité
**du collage**, pas de la base.

## Ce que l'ingestion refuse, et dans quel ordre

Les neuf cas sont `RefusalCause`, numérotés comme dans la résolution de
[#128](https://github.com/AmauryTISSOT/microservice_rgpd/issues/128). L'ingestion les évalue dans
l'ordre suivant, et deux points de cet ordre sont **délibérés** :

1. `MissingHeader` (n° 1) — la première ligne n'est pas un en-tête lisible.
2. `UnknownFormatVersion` (n° 8) — l'en-tête déclare un autre format.
3. `MissingClosingLine` (n° 2) — la dernière ligne n'est pas une ligne de fin lisible.
4. `CeilingExceeded` (n° 9) — ⚠️ **lu sur le compte _annoncé_ avant qu'aucune ligne ne soit
   analysée**, pour que « ton relevé annonce 31 000 colonnes, le plafond est 20 000 » sorte sans
   qu'on ait payé l'analyse de 31 000 lignes.
5. `CountMismatch` (n° 3) — ⚠️ **avant le relevé vide**, sans quoi un collage annonçant 400 colonnes
   et n'en portant aucune se lirait « relevé vide » là où il est une troncature.
6. `EmptyListing` (n° 7).
7. `UnreadableColumnLine` (n° 4).
8. `DuplicateColumn` (n° 6).
9. `RankGap` (n° 5).

⚠️ **Le plafond est de 20 000 colonnes, borne incluse, et au-delà on refuse — on ne tronque
jamais.** Une contrainte d'ordonnancement le suit, et elle n'est pas un détail d'implémentation :
la limite d'octets du corps de la requête HTTP doit être réglée **au-dessus** de ce que 20 000
colonnes produisent, sans quoi le refus muet du serveur sur la taille du corps sortirait avant le
refus lisible.

## Ce qui n'est pas ici

- **Les trois fichiers de requête** — ils appartiennent à #129, qui les exécutera et donc les
  prouvera.
- **Le rendu des messages de refus à l'écran** — un `RefusalCause` porte le cas, sa
  `FrenchLabel` et son `Expectation` ; la phrase qui les assemble vient avec l'écran.
- **La provenance du relevé.** Un relevé sincère, entier et bien formé, mais tiré de la base de
  recette, est **indiscernable du bon**. Aucun mécanisme n'attrape ce cas, et aucun ne doit prétendre
  l'attraper.
