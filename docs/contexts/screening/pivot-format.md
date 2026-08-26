# `screening-pivot/1` — la forme du relevé, collé comme scanné

La forme exacte du `ColumnListing`. Elle est écrite ici parce qu'elle a **deux producteurs et un
seul consommateur** :

| producteur | qui l'écrit | quand |
| --- | --- | --- |
| **le relevé collé** | les trois requêtes de `releves/*.sql`, jouées par l'`Operator` dans son client SQL | il colle le résultat dans l'écran de dépôt |
| **le relevé scanné** | `PivotWriter`, en C#, sur les colonnes qu'un `IDialectScanner` vient de relever | le service a joint la base lui-même |

Le consommateur est unique : `ColumnListingIngestion`, que les **deux** chemins traversent. Un
troisième écrivain existe, qui n'est pas un producteur : l'extraction du corpus, qui rejoue les
mêmes requêtes hors du service.

⚠️ **Les deux chemins produisent le même objet, et c'est une contrainte, pas une coïncidence.** Un
relevé scanné qui différerait d'un relevé collé de la même base ferait deux formats sous un seul
nom de version, et l'ingestion — qui est le seul juge — cesserait de prouver quoi que ce soit du
second. C'est aussi pourquoi le scanner repasse par l'ingestion au lieu d'écrire directement un
`Screening`.

Les clés vivent en dur des deux côtés ; si elles ne vivent nulle part en toutes lettres, elles
divergeront.

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
| `base`      | `current_database()`, `DATABASE()`, ou le **nom** du fichier côté SQLite — jamais son chemin. |
| `genere_le` | L'instant de génération, ISO 8601 **avec décalage** — `…+02:00` ou `…Z`.             |

⚠️ **Le décalage n'est pas facultatif, et l'ingestion refuse un instant qui n'en porte pas.** Sans
lui, l'instant prendrait celui du **serveur** : le même collage lu à Tokyo puis en UTC donnerait deux
instants distants de neuf heures, et un relevé vieux d'une journée s'afficherait comme frais. C'est
la seule chose qui dise à l'`Operator` de quand date son relevé.

⚠️ **`base` est recopié, jamais vérifié** — mais il est **borné**. C'est un repère pour l'humain qui
relit un `Screening` trois jours plus tard, jamais une identité sur laquelle bâtir une comparaison de
rapports. Il passe le même garde que le domaine (100 caractères, sans caractère de contrôle ;
64 pour `dialecte`), sans quoi un refus lisible se changerait en erreur nue un cran plus loin.
⚠️ **Côté SQLite, on n'y écrit que le nom du fichier, jamais son chemin.**
[#278](https://github.com/AmauryTISSOT/microservice_rgpd/issues/278) tranche : `main.sqlite`, pas
`/var/lib/app/tenants/acme-corp/prod/main.sqlite`. Le motif est le secret, pas la longueur — sur ce
dialecte le chemin **est** la chaîne de connexion à peu de chose près, et ce champ, lui, est persisté
et **exporté hors du service**. Voir `Rien de réel ne reste`.
⚠️ **Cela referme la borne au lieu de la rouvrir.** La rédaction précédente prévoyait qu'un chemin de
conteneur trop long soit refusé au titre du cas n° 1, quitte à rouvrir les cent caractères du
domaine ; un nom de fichier y tient toujours, et la borne cesse d'être une gêne. ⚠️ **Sur le chemin
collé, la requête embarquée doit donc couper le chemin elle-même** — un `Operator` qui joue le
relevé à la main et colle le résultat obtient le même champ que le chemin connecté, la contrainte
voulant que les deux chemins produisent le même objet.

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
filtre de nullabilité sur toute la base, sans un mot, dans un rapport de détection qui se lit comme
complet.
Écrire `IS_NULLABLE = 'YES'` dans la requête, pas `IS_NULLABLE`.

⚠️ **Et MySQL n'est pas l'exception : SQLite pose le même piège par l'autre bout.** Il n'a **aucun**
type booléen, si bien qu'une comparaison — la solution qui marche sur MariaDB — y rend `1`. Vérifié
sur 3.46 :

```
sqlite> SELECT json_object('a',(1=1), 'b',TRUE, 'c',json('true'));
{"a":1,"b":1,"c":true}
```

`(1=1)` et `TRUE` rendent **`1`**, là où PostgreSQL, MySQL et MariaDB rendent `true`. `json('true')`
est la seule voie. Le relevé est refusé de la même façon (cas n° 4), pour la même raison, par un
chemin différent.

⚠️ **Il n'existe donc pas de forme portable de ce champ**, et c'est le fait à retenir avant d'écrire
un quatrième dialecte : `nullable` s'écrit `nullable` sur PostgreSQL (le catalogue rend déjà un
`boolean` — l'envelopper d'un `CASE … THEN 1 ELSE 0 END` le **dégraderait**),
`(IS_NULLABLE = 'YES')` sur MySQL et MariaDB, `json('true')`/`json('false')` sur SQLite. ⚠️
`CAST(… AS JSON)` n'est **pas** portable : syntaxe inconnue de MariaDB (`ERROR 1064`). Éprouvez la
sortie sur le moteur visé plutôt que de recopier la forme d'un voisin, et **écrivez le piège en tête
du fichier** : c'est ce qui empêche la « simplification » suivante de rebriser le champ.

⚠️ **Le même piège vaut pour la ligne de fin**, dont le `"fin":true` est un booléen aussi — voir
plus bas.

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

⚠️ **`fin` est un booléen JSON, et il se heurte au même piège que `nullable`** : `true` nu sur
PostgreSQL, `(1 = 1)` sur MySQL et MariaDB, `json('true')` sur SQLite. Sans ce marqueur, l'ingestion
refuse (cas n° 2) — et elle doit : « relevé complet » et « copier-coller tronqué » se liraient sinon
exactement pareil.

⚠️ **`colonnes` se calcule dans la même requête que les lignes**, jamais par un second passage sur le
catalogue. Deux lectures d'un catalogue vivant peuvent légitimement diverger si un `ALTER TABLE`
passe entre elles, et le service refuserait alors un pivot sincère. C'est un contrôle d'intégrité
**du collage**, pas de la base.

⚠️ **Et sur le relevé scanné, ce contrôle n'a aucun équivalent — c'est une divergence, pas une
nuance.** Au collage, le compte vient d'ailleurs que du texte qu'il accompagne : une troncature le
contredit, et `CountMismatch` la prend. Au scan, c'est `PivotWriter` qui écrit ce compte, sur la
liste qu'il vient de sérialiser : il ne peut pas se contredire, et ne peut donc rien attraper non
plus. La clause « il est entier ou il n'existe pas » y est tenue **un cran plus haut**, par le
dialecte, qui ne rend un relevé que s'il a lu le catalogue en entier ; en dessous, elle est vraie
par construction et ne prouve rien. « Entier » veut dire *non interrompu*, jamais *complet* — un
catalogue que le compte de connexion n'a montré qu'à moitié produit un pivot valide, et rien ici ne
rougit.

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

### Le plafond d'octets, qui n'est pas un dixième cas

Le geste de dépôt borne aussi le **poids** du collage — `DepositListingCommand.MaxPasteBytes`, 8 Mo,
soit deux fois ce que pèse le pire relevé autorisé. Il est lu **avant l'ingestion**, et il refuse en
français comme les neuf autres.

⚠️ **Il ne prend pas de numéro de cas, et c'est délibéré.** Les neuf cas sont ceux du contrat de
_format_ ; le poids est une borne du _geste_, qui peut bouger sans que le format bouge. Un dixième
numéro ferait mentir toute la documentation qui en compte neuf.

⚠️ **Le plafond du transport est posé au-dessus du plafond du geste, jamais au même octet.** Posés au
même niveau, les deux se déclencheraient ensemble et celui qui sortirait serait le `413` nu — sans
phrase, sans écran, sans « aucune colonne n'a été ingérée ». L'écart doit en outre couvrir l'enflure
de l'**encodage de formulaire**, où les accolades et guillemets du pivot pèsent trois octets chacun
(~1,45× mesuré sur une ligne réelle). ⚠️ Le `TestServer` des tests fonctionnels **n'applique pas**
`RequestSizeLimit` : aucun test de surface ne peut voir ce `413`, et la propriété est donc épinglée
sur les deux constantes elles-mêmes.

## Ce qui n'est pas ici

- **Les trois fichiers de requête** — ils appartiennent à #129, qui les exécutera et donc les
  prouvera.
- **Le rendu des messages de refus à l'écran** — un `RefusalCause` porte le cas, sa
  `FrenchLabel` et son `Expectation`, et rien de plus. La phrase qui les assemble vit avec le geste,
  dans `DepositListingHandler` ; l'écran de dépôt la redit sans en rédiger aucune, parce que deux
  rédactions d'une même règle finiraient par ne plus dire la même chose.
- **La provenance du relevé.** Un relevé sincère, entier et bien formé, mais tiré de la base de
  recette, est **indiscernable du bon**. Aucun mécanisme n'attrape ce cas, et aucun ne doit prétendre
  l'attraper.
