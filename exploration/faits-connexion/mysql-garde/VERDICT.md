# Le garde de privilège MySQL / MariaDB — mesuré (#286)

Banc rejoué sur **MySQL 8.4.11** et **MariaDB 11.8.9-ubu2404**, mêmes images que
[#275](https://github.com/AmauryTISSOT/microservice_rgpd/issues/275).
Sept comptes, onze sondes, plus une seconde salve. Sorties brutes :
`sortie-mysql84.txt`, `sortie-mariadb11.txt`, `suite-mysql84.txt`, `suite-mariadb11.txt`.

La base d'épreuve porte **quatre objets** (`adherents` 9 colonnes, `cotisations` 3,
`journaux` 2, la vue `v_adherents` 2) — **16 colonnes** en tout.

## Le verdict en une phrase

**Le garde attrape le cas que #275 a mesuré, et rate le cas d'à côté.**
Un compte dont les droits sont au grain de la **colonne** est détectable ; un compte
dont les droits sont au grain de la **table** mais ne couvrent qu'une partie des tables
est **indétectable** — le catalogue qu'il lit est amputé *et cohérent avec lui-même*.
La comparaison « tables du catalogue vs tables sur lesquelles un droit existe » se
compare bien à elle-même, exactement comme le ticket le craignait.

## Le tableau qui décide

Garde candidat mesuré (sonde 9) : *nombre d'objets visibles non couverts par un droit
global, base ou table*. Verdict `0` = laisse passer.

| compte | droits | garde | catalogue vu | vérité | |
|---|---|---|---|---|---|
| `plein` | `SELECT ON epreuve.*` | 0 → passe | 4 obj / 16 col | voit tout | ✅ |
| `global` | `SELECT ON *.*` | 0 → passe | 4 obj / 16 col | voit tout | ✅ |
| `tableatable` | `SELECT` sur les 4 objets | 0 → passe | 4 obj / 16 col | voit tout | ✅ |
| `partiel` | `SELECT (courriel) ON adherents` | 1 → refuse | 1 obj / **1 col** | ampute | ✅ |
| **`partieltable`** | **`SELECT ON adherents` seul** | **0 → passe** | **1 obj / 9 col** | **ampute 3 objets sur 4** | ❌ |
| `parrole` | `SELECT ON epreuve.*` via rôle | 4 → refuse | 4 obj / 16 col | voit tout | ❌ |
| `nu` | aucun | 0 → passe | **0 obj / 0 col** | ne voit rien | ❌ rattrapable |

**Les deux moteurs rendent des chiffres identiques, colonne par colonne.**

## Les cinq faits

### 1. Le cas mortel : `partieltable` est indistinguable de `tableatable`

Le compte à droits table par table couvrant **toute** la base et le compte à droits
table par table n'en couvrant **qu'une partie** rendent le même verdict `0`.
Le second produira un pivot de 9 colonnes au lieu de 16, structurellement valide,
au compte de fin cohérent — le cas exact de #275, déplacé du grain colonne au grain table.

Et **rien nulle part** ne trahit les objets manquants. Pour `partieltable`, sur les
deux moteurs (sonde A) :

- `information_schema.TABLES` : `adherents` seule
- `KEY_COLUMN_USAGE` : `PRIMARY / adherents` seule — la clé étrangère `fk_cot_adh`
  qui nomme pourtant `adherents` **disparaît** avec `cotisations`
- `REFERENTIAL_CONSTRAINTS` : vide
- `STATISTICS` : `adherents` seule
- `VIEWS` : vide

Le catalogue ne présente **aucune incohérence** : c'est un monde à une table, et il
est parfaitement bien formé. Le garde pragmatique (`SELECT … LIMIT 0` par objet)
n'aide pas non plus : il ne teste que ce que le catalogue montre.

### 2. La voie `information_schema` est **strictement moins informée** que `SHOW GRANTS`

Renversement de la présomption du ticket. Pour `parrole` (droits hérités d'un rôle) :

- `SHOW GRANTS FOR CURRENT_USER()` sur **MySQL 8.4** rend `GRANT SELECT ON `epreuve`.*`
  — le rôle actif est **déplié**
- `information_schema.SCHEMA_PRIVILEGES` : **vide**. `USER_PRIVILEGES` : `USAGE` seul.

Un garde bâti sur `information_schema` seul **refuse à tort** tout compte dont les
droits passent par un rôle. Le texte du `SHOW`, réputé plus fragile à parser, est
la seule voie qui les porte.

### 3. Les rôles : deux moteurs, deux mécaniques, aucune commune

| | MySQL 8.4.11 | MariaDB 11.8.9 |
|---|---|---|
| `SHOW GRANTS FOR CURRENT_USER()` déplie le rôle | **oui** | **non** — rend `GRANT r_lecture TO parrole`, `USAGE`, `SET DEFAULT ROLE` |
| `SHOW GRANTS … USING '<role>'` | accepté | **ERROR 1064** — la syntaxe n'existe pas |
| après `SET ROLE` | déjà déplié | toujours pas déplié |
| `ENABLED_ROLES` / `APPLICABLE_ROLES` | nomme `r_lecture` | nomme `r_lecture` |
| `SHOW GRANTS FOR '<role>'` par un porteur du rôle | **ERROR 1142** (`mysql.user` refusé) | **réussit** → `GRANT SELECT ON epreuve.*` |
| `SET DEFAULT ROLE` | `ALL TO 'u'@'%'` | `'r' FOR 'u'@'%'` |

La voie MariaDB est donc : `ENABLED_ROLES`, puis **une passe `SHOW GRANTS FOR <rôle>`
par rôle**. La voie MySQL est : `SHOW GRANTS FOR CURRENT_USER()`, qui suffit.
Aucune requête n'est commune aux deux moteurs.

### 4. ⚠️ Sur MariaDB, `SHOW GRANTS` fuit le hash du mot de passe

```
GRANT USAGE ON *.* TO `parrole`@`%` IDENTIFIED BY PASSWORD '*63C9DAA8BE377DC5FC88C885A1D07E0841370F33'
```

Fait non demandé par le ticket, et il touche la **contrainte 2** de la carte (« la
chaîne de connexion n'est jamais persistée, jamais journalisée, jamais tracée, jamais
reprise dans un message d'erreur »). Si le garde lit `SHOW GRANTS` sur MariaDB, il
tient en mémoire une chaîne portant le condensat du secret d'accès. Elle ne doit
**jamais** atteindre un log, une trace, ni un message d'erreur — y compris le message
de refus du garde lui-même. MySQL 8.4 ne fuit rien (`GRANT USAGE ON *.* TO `parrole`@`%``).

### 5. Le coût, et la panne propre qui n'existe pas

- **Nombre de requêtes ajoutées avant le relevé** : 1 sur MySQL (le `SHOW GRANTS`
  suffit), 2 + *n* rôles sur MariaDB (`ENABLED_ROLES` puis un `SHOW GRANTS` par rôle).
  Aucune ne dépend du nombre de tables. Coût mesuré indistinct du bruit du
  `docker exec` (~0,44 s aller-retour conteneur compris, essais reproductibles).
- **La panne propre du garde n'existe pas.** `nu`, sans aucun privilège objet, lit
  ses propres droits sans erreur (`GRANT USAGE ON *.* TO `nu`@`%``) sur les deux
  moteurs. Aucun compte capable de se connecter ne peut être empêché de lire ses
  droits — le garde n'ajoute **aucune cinquième famille** aux quatre de #278.
- Le cas `nu` est en revanche le seul faux verdict **rattrapable** trivialement :
  son catalogue rend **0 objet** et la base `epreuve` **n'apparaît même pas dans
  `SCHEMATA`**. « La base est vide ou invisible » est un refus déjà nécessaire par
  ailleurs, indépendant du garde.

## Le SQL du garde, tel qu'il pourrait s'écrire

Il est donné pour mémoire — voir la conclusion sur ce qu'il vaut.

**MySQL 8.4** (une requête, plus le `SHOW` pour les rôles) :

```sql
SHOW GRANTS FOR CURRENT_USER();
-- puis, sur le texte rendu : chercher `GRANT …SELECT… ON *.*`
-- ou `GRANT …SELECT… ON `<base>`.*`. Sinon, descendre au grain table :
SELECT COUNT(*) FROM information_schema.TABLES t
WHERE t.TABLE_SCHEMA = @base
  AND NOT EXISTS (SELECT 1 FROM information_schema.TABLE_PRIVILEGES tp
                  WHERE tp.TABLE_SCHEMA = @base AND tp.TABLE_NAME = t.TABLE_NAME
                    AND tp.PRIVILEGE_TYPE = 'SELECT');
```

**MariaDB 11.8** : idem, précédé de

```sql
SELECT ROLE_NAME FROM information_schema.ENABLED_ROLES;
SHOW GRANTS FOR '<chaque rôle>';   -- le seul endroit où le droit hérité se lit
```

Ce qu'il **refuse** : `partiel` (grain colonne), et — si l'on omet la lecture des
rôles — `parrole` à tort.
Ce qu'il **laisse passer** : `plein`, `global`, `tableatable`, `parrole` (rôles lus),
et ⚠️ **`partieltable`, qui amputera**.

## Ce que cela impose à #278

Le fait est posé, comme le ticket le demandait :

> **un compte à droits table par table couvrant partiellement la base n'est pas
> distinguable d'un compte les couvrant toutes.**

Donc la formule de [#278](https://github.com/AmauryTISSOT/microservice_rgpd/issues/278)
— « le scan vérifie les droits avant de relever et **refuse** » — ne peut pas être
tenue telle quelle : elle promet une garantie que le moteur ne rend pas. Le garde
existe, il a un prix nul, et il attrape un cas réel ; il n'est simplement **pas une
preuve de complétude**. La clause à trancher est de savoir si l'on garde un filtre
qui ferme la porte mesurée par #275 tout en **disant** qu'il ne ferme pas toutes les
portes, ou si l'on renonce au garde et l'on porte la question à la clause
d'incomplétude. Cette décision n'appartient pas à ce ticket : elle rouvre #278.

## Comment rejouer

```sh
bash run.sh mysql:8.4       # puis lire sortie-mysql84.txt
bash run.sh mariadb:11.8    # puis lire sortie-mariadb11.txt
bash run-suite.sh mysql:8.4
bash run-suite.sh mariadb:11.8
```
