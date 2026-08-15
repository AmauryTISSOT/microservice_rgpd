# Contrat d'`Adapter` — ce que le service appelle chez vous

Ce document est la référence du développeur qui **branche une application** sur le service : ce que
le service vous envoie, ce que vous devez lui répondre, et ce qu'il ne vous demandera jamais. Il se
lit seul, sans avoir suivi le projet.

Le vocabulaire du domaine vit dans
[`docs/contexts/casework/CONTEXT.md`](../contexts/casework/CONTEXT.md) ; il s'adresse au mainteneur.
Ce fichier-ci s'adresse à vous, sauf la section 8.

---

## 1. Ce qu'est un `Adapter`, et ce qu'il n'est pas

Un `Adapter` est **un programme HTTP que vous écrivez** et que le service appelle pour exercer une
`Capability` sur un ou plusieurs de vos systèmes.

**Le sens est unique.** Le service appelle toujours ; votre application ne rappelle **jamais**. Vous
n'avez besoin d'aucune adresse du service, d'aucun secret sortant, d'aucun client HTTP, d'aucune
file de reprise. Vous exposez des routes et vous répondez. C'était une décision de coût : un rappel
doublerait votre intégration, en la faisant passer d'« une requête derrière une route » à « un
client HTTP fiable ».

**Un seul `Adapter` peut servir plusieurs systèmes.** Le `system_id` voyage en **paramètre** de
chaque appel. Un système est une **unité de recensement** — « la boutique », « la messagerie
support », « la reprise de 2019 » — et non une unité de déploiement : trois systèmes déclarés
peuvent vivre derrière la même base et le même processus.

**Le service ne connaît aucun nom de table, de colonne ni de champ.** Il ne les connaîtra jamais.
Ce que vous rendez est écrit dans **votre** vocabulaire ; la précision de la réponse vous appartient.

**Ce que le service ne vous demandera jamais** : de le rappeler, de tenir une connexion ouverte, de
lui décrire votre schéma, de lui déclarer vos systèmes (c'est un humain qui les déclare, dans le
`Manifest` du service), ni de fusionner quoi que ce soit.

---

## 2. Périmètre et authentification — **la clause, en toutes lettres**

> **Le contrat ne dit pas « authentifiez-vous ». Il dit : un secret partagé _et_ un `Adapter` hors
> d'atteinte de l'extérieur — les deux ensemble, jamais l'un sans l'autre.**
>
> Le secret partagé, comparé en temps constant, est la moitié du dispositif. L'autre moitié est le
> **périmètre réseau** : votre `Adapter` ne doit être joignable que depuis le réseau où vit le
> service. Un `Adapter` exposé à l'Internet public et protégé par ce seul secret **n'est pas
> conforme à ce contrat**, quelle que soit la longueur du secret.
>
> La raison est ce que cette surface fait : une route qui rend des données personnelles est une
> fuite si on l'atteint, et une route qui les efface est une destruction. Ce n'est pas une surface
> qu'on protège par un seul mécanisme.
>
> **La confidentialité du transport est explicitement hors menace.** Le service et l'application
> sont auto-hébergés, dans le périmètre du même responsable de traitement : il n'y a pas de tiers
> sur le chemin dont on se protégerait. C'est cette hypothèse — et elle seule — qui autorise un
> secret en clair dans un en-tête. **Elle est aussi ce que la clause de périmètre paie.** Retirez le
> périmètre, et le secret ne suffit plus.

### Ce qui est explicitement écarté, et pourquoi

| Écarté | Motif |
| --- | --- |
| **mTLS** | Coûterait à chaque intégration une autorité de certification, une distribution de certificats, et leur renouvellement — c'est-à-dire une infrastructure entière — pour se protéger d'un attaquant réseau que la clause de périmètre suppose déjà absent. |
| **HMAC de la requête** | Vous demanderait de canoniser un corps et d'écrire une signature à vérifier octet par octet. Il achète l'intégrité contre un intermédiaire actif — le même attaquant, toujours hors menace — et ne protège de rien qu'un `Adapter` hors d'atteinte n'écarte déjà. |
| **JWT** | Apporte une expiration et des revendications dont il n'y a rien à faire : il n'y a qu'un appelant, il n'a qu'un droit, et vérifier une signature exigerait une bibliothèque de plus dans votre `requirements.txt`. |

Ce que le contrat retenu vous coûte, à l'inverse : **trois lignes de bibliothèque standard**.

```python
import hmac

def autorise(requete) -> bool:
    presente = requete.headers.get("X-RGPD-Secret", "")
    return hmac.compare_digest(presente, SECRET)
```

`compare_digest` — et non `==` — parce qu'une comparaison qui s'arrête au premier octet différent
laisse mesurer combien d'octets étaient bons. C'est la seule exigence de forme que ce contrat pose
sur votre code.

### Le secret

- **Un seul secret, valide à la fois.** Il n'existe aucun recouvrement : la rotation est un
  **redémarrage coordonné** des deux côtés. Un second secret « encore accepté » aurait une date de
  retrait que personne n'aurait à prouver.
- **Un secret par `Adapter`, jamais par système.** Le `system_id` est une unité de recensement ; un
  secret est de la topologie. Les deux ne se découpent pas de la même façon.
- **Il ne vit jamais dans le `Manifest`** du service, qui se relit à l'écran, et l'adresse déclarée
  ne peut pas en porter un — les identifiants d'usager d'une URL (`https://jean:mot@…`) sont refusés
  à la saisie. Il vit dans la configuration de déploiement des deux côtés.
- **Côté service, son absence arrête le démarrage.** Il n'existe aucun mode « sans ».

### La sonde de vérification, et pourquoi vous la verrez dans vos journaux

Le `Manifest` du service est **déclaratif** : un humain y écrit vos systèmes et ce que votre
`Adapter` sait y faire. Il vieillit, et rien d'autre qu'une confrontation ne dira qu'il ment. Le
service en tient donc une, à la demande d'un exploitant — jamais par une minuterie — et elle vous
adresse **deux requêtes par système**, toutes deux inoffensives :

```
POST <adresse déclarée>/locate?system_id=<votre système>
X-RGPD-Secret: sonde-de-verification-du-manifest-secret-deliberement-faux
Content-Type: application/json

{ "designations": [] }
```

puis — **sauf si vous avez servi la première**, auquel cas il n'y en a pas de seconde — la même
requête **sous le vrai secret**.

- **Le secret de la première est délibérément faux, et il est public** — le voici en toutes lettres.
  Il n'ouvre rien : il est fait pour se faire refuser. Le voir dans vos journaux n'est pas une
  attaque, c'est le service qui vérifie que votre porte est fermée. Il n'a aucun rapport avec le
  vôtre : un faux dérivé du vrai vous livrerait le vrai, octet par octet.
  ⚠️ Sa publicité a un coût assumé : un `Adapter` qui refuserait **cette chaîne-là** et servirait
  tout le reste passerait la sonde en restant grand ouvert. Écrire ce cas-là, c'est mentir à son
  propre exploitant ; la sonde ne prétend pas s'en protéger, elle constate ce qu'on lui répond.
- **Si vous répondez `200` ou `202` à cette première requête, votre `Adapter` est rapporté comme
  nu** : vous avez travaillé pour un appelant que le contrat vous demandait de refuser. C'est le
  seul résultat que la sonde tienne pour une preuve, et il remonte à votre exploitant.
  Le service ne vous rappelle alors pas sous le vrai secret, et **ne conclut rien de votre
  catalogue** : les réponses d'un `Adapter` qui sert n'importe qui ne prouvent plus rien de ce qu'il
  sert.
- **Le sac de désignations est vide.** Aucune personne réelle ne part chez vous au titre de cette
  vérification, et il n'y a rien à chercher — le sac vide est prévu par le contrat (§ 3).
- **Le service ne lit jamais le corps de vos réponses à ces deux requêtes.** Le statut lui suffit, et
  le corps d'un `200` rendu à un secret faux est précisément la fuite qu'il vient constater.
- **Seul `locate` est envoyé. Jamais `read`, jamais `erase`, jamais `rectify`** — quel que soit ce
  que le `Manifest` déclare, et quoi que la sonde ait appris. Une vérification ne détruit pas des
  données pour savoir si vous savez les détruire : `erase` et `rectify` sont rapportées à votre
  exploitant comme **non vérifiables**, nommément, plutôt que passées sous silence. `read` non plus
  n'est pas sondé, et pour une autre raison : sa forme d'appel est fixée (§ 4 ter), mais le sonder
  ferait rapatrier des données personnelles au titre d'une vérification de topologie — un séjour de
  plus, pour n'apprendre qu'un statut que `locate` donne déjà.
- **Le service ne corrige jamais son `Manifest` sur ce que vous répondez.** Un `404` sur un système
  qu'il croyait vôtre est rapporté comme un écart ; un humain tranchera lequel des deux avait tort.

---

## 3. L'appel

Une opération par `Capability`, en `POST`, sous l'adresse que le `Manifest` déclare pour le système.

```
POST <adresse déclarée>/<capability>?system_id=<votre identifiant de système>
X-RGPD-Secret: <le secret partagé>
Content-Type: application/json
```

`<capability>` vaut `locate`, `read`, `erase` ou `rectify`. `locate` et `read` sont appelées à ce
jour ; `erase` et `rectify` sont déclarables et pas encore exercées (§ 7).

Le corps porte le **sac de désignations** — et, pour `read` seulement, le **droit** au titre duquel
on lit (§ 4 ter). Rien d'autre :

```json
{
  "designations": [
    { "kind": "email", "value": "helene.petit@example.fr" },
    { "kind": "name",  "value": "Hélène Petit" },
    { "kind": "phone", "value": "+33 6 12 34 56 78" },
    { "kind": "reference", "value": "1203" }
  ]
}
```

`kind` appartient à un vocabulaire **fermé** de quatre mots : `email`, `name`, `phone`, `reference`.

**Une `Designation` ne prétend ni à l'unicité ni à l'exactitude.** La personne n'a pas
d'identifiant : `email` n'est pas une clé, et l'identifiant natif de votre application arrive en
`reference` — comme une désignation parmi d'autres, sans autorité particulière. Deux désignations
peuvent viser la même personne, une seule peut en viser deux. C'est ce doute qu'un humain arbitrera
côté service ; vous n'avez pas à le lever.

**Le sac peut être vide**, et ce n'est pas une erreur : c'est une recherche qui ne trouvera rien, et
vous le direz mieux que nous.

**Rien du dossier ne vous parvient** : ni identifiant de dossier, ni date, ni nom d'opérateur, ni
motif. Ce que vous ne recevez pas ne peut pas finir dans vos journaux.

---

## 4. Ce que vous répondez

| Statut | Sens | Corps |
| --- | --- | --- |
| `200` | Servi. | Ce que la `Capability` rend : du JSON pour `locate` (§ 4 bis), **des octets bruts** pour `read` (§ 4 ter). |
| `202` | **Différé** : le travail est long, vous ne tenez pas la connexion. | `{"deadline": "<ISO 8601 avec décalage>"}` |
| `401` | Le secret n'est pas celui que vous attendez — ou il manquait. | Libre, non lu. |
| `404` | Vous ne servez pas ce `system_id`. | Libre, non lu. |

**Tout autre statut est une panne**, du point de vue du service : ni réponse, ni refus. Il n'entre
dans aucun dossier et ne se distingue pas d'un serveur muet.

### Le `202` et son échéance

```json
{ "deadline": "2026-08-05T09:00:00+02:00" }
```

L'échéance est **déclarée par vous**, jamais négociée : vous dites quand vous aurez fini, et le
service repassera de lui-même — à l'ouverture du dossier par un opérateur, jamais par une minuterie.

**Le décalage horaire est obligatoire.** `2026-08-05 09:00` est refusé : sans fuseau, cette date
vaudrait deux heures de moins sur un serveur et deux de plus sur un autre, et le service ne devine
pas le vôtre.

**Un `202` sans échéance relisible est traité comme une panne**, jamais comme un différé par défaut.
Le service n'invente pas de date : une date qu'il aurait fabriquée deviendrait, une heure plus tard,
une preuve que personne n'a déclarée.

⚠️ **Il n'existe ni compteur de tentatives, ni temporisation, ni abandon automatique, ni escalade.**
Un `202` répété trente-cinq fois ne déclenche rien : c'est un humain qui décide, et lui seul.

### Les deux refus, et pourquoi ils ne se confondent pas

`401` et `404` **ne se répondent pas au hasard** : le service les traite différemment parce qu'on ne
les répare pas au même endroit.

- `401` — **un fait de topologie.** Il ne dit rien du système appelé : le secret des deux côtés n'est
  plus le même, et la réparation est un redémarrage coordonné.
- `404` — **un désaccord `Manifest`/`Adapter`.** Le catalogue déclaré côté service désigne une adresse
  qui ne connaît pas ce système. Quelqu'un a tort — la déclaration, ou votre `Adapter` — et un
  humain tranchera. Le service ne corrige **jamais** son `Manifest` en silence sur votre réponse.

**Refusez explicitement un `system_id` inconnu.** Ne servez pas « au mieux » un système que vous ne
connaissez pas : un `200` poli sur un identifiant inconnu est exactement l'omission silencieuse que
tout ce dispositif cherche à rendre impossible.

---

## 4 bis. Le corps d'un `200` à `locate`

C'est la seule `Capability` dont la **réponse** ait une forme fixée — `read` n'en a délibérément
aucune (§ 4 ter).

```json
{
  "certain": ["clients#1203"],
  "reserved": [
    {
      "reference": "clients#4417",
      "reason": "Deux comptes portent le nom « Jean Dupont ». Celui-ci a été créé en 2019 et n'a jamais commandé.",
      "designations": [{ "kind": "email", "value": "j.dupont1954@example.fr" }]
    }
  ]
}
```

**Deux listes, et la différence entre les deux est tout le sujet.**

- `certain` — ce que vous **rattachez** à la personne cherchée. Vous en répondez.
- `reserved` — ce que vous avez trouvé **sans pouvoir trancher**. Le service n'en tranchera pas non
  plus : un humain le fera, à l'écran, nommément et à une date.

**Une réserve n'est pas un demi-rattachement.** Tant que personne ne l'a arbitrée, elle ne compte
pour aucun rattachement, et un `Step` déclaré fait sur un système qui n'en porte aucun réclamera un
constat écrit.

### Les champs

| Champ | Obligatoire | Ce que c'est |
| --- | --- | --- |
| `certain[]` | non — l'absence vaut liste vide | Une **référence opaque**, écrite dans votre vocabulaire. |
| `reserved[].reference` | **oui** | La même chose, pour une ligne dont vous doutez. |
| `reserved[].reason` | **oui** | Le motif du doute, **en français**, lu tel quel par l'opérateur. |
| `reserved[].designations` | non | Ce que cette ligne-là propose comme désignation de la personne. |

**Les références sont opaques de bout en bout.** `clients#1203`, `/var/log/app-2026-03.log:88`,
`ligne 412 du fichier de reprise` : le service ne les découpe pas, ne les compte pas, ne les
compare pas entre systèmes, et ne vous les redemandera que telles qu'elles. Elles reviennent à
l'écran, mot pour mot, pour un humain qui saura les lire chez vous. **200 caractères** au plus.

**Le motif est lu par un humain, pas par une machine.** Écrivez la phrase que vous diriez à un
collègue : « deux comptes portent ce nom, celui-ci a commandé en mars ». Le service ne l'analyse
jamais — il l'affiche. Une réserve **sans motif** est une panne, pas une réserve : c'est un doute
qu'on demanderait à quelqu'un de trancher sans lui dire lequel.

**`designations` est le seul champ que le service interprète**, et seulement **après** qu'un humain
a rattaché la réserve. Il entre alors au sac, et l'appel suivant le porte — c'est ainsi qu'une
adresse trouvée dans votre base ouvre le journal du voisin. Son `kind` appartient au même
vocabulaire fermé de quatre mots qu'au § 3. Une réserve **sans** `designations` reste locale et
opaque : elle s'arbitre, mais elle n'apprend rien à personne d'autre.

### Le zéro

```json
{ "certain": [], "reserved": [] }
```

**Il n'a qu'une forme, et c'est délibéré.** Le service ne vous demande pas de distinguer « rien
trouvé » de « rien à trouver », ni de rendre un compte : un zéro est un zéro. Ce que vous auriez
mis dans la nuance, mettez-le dans une réserve motivée — c'est là qu'un humain la lira.

Un corps **vide**, ou sans aucune des deux clés, vaut le même zéro. En revanche une réserve sans
`reference`, sans `reason`, ou portant un `kind` hors des quatre mots, est une **panne** : le
service préfère ne rien apprendre plutôt qu'apprendre à moitié en silence.

---

## 4 ter. `read` : le droit part, la forme jamais

### Le corps de l'appel

`read` est la seule `Capability` dont le corps porte autre chose que le sac : le **droit au titre
duquel** on lit.

```json
{
  "designations": [{ "kind": "email", "value": "helene.petit@example.fr" }],
  "right": "Access"
}
```

`right` appartient à un vocabulaire fermé, sous ses noms canoniques **anglais** : `Access`,
`Rectification`, `Erasure`, `Portability`, `Restriction`, `Objection`.

**Pourquoi il part, et pourquoi il est la seule chose qui parte.** Le périmètre matériel de
l'art. 20 est plus étroit que celui de l'art. 15, et il se décide **ligne par ligne, chez vous** :
« ce que la personne nous a fourni » n'a de réponse que dans vos tables. Le service ne peut pas
trancher cela et ne le tentera jamais ; il vous dit sous quel article on lit, et vous en tirez ce
que vous seul savez en tirer.

Un `read` appelé pour deux droits différents part **deux fois**. C'est délibéré : rendre une seule
pièce pour deux périmètres vous obligerait à retenir le plus large, c'est-à-dire à rapatrier au
titre de l'art. 20 ce que seul l'art. 15 justifiait.

### Ce que vous répondez

**Rien n'est imposé. Des octets, et de quoi les nommer.**

```
200 OK
Content-Type: text/csv; charset=utf-8
Content-Disposition: attachment; filename="export-2026-04.csv"

<vos octets, dans votre forme à vous>
```

Un CSV, un ZIP, un PDF, du JSON qui n'obéit à aucun schéma du service, un dump SQL : **le service
n'ouvre pas le corps.** Il ne le désérialise pas, ne le valide pas, ne compte rien dedans, et n'en
tire aucune ligne de preuve. Un export que vous savez déjà écrire coûte trois lignes ; un schéma
commun vous aurait demandé de traduire vos tables dans le vocabulaire de quelqu'un d'autre — et
c'est ce coût-là, plus que tout autre, qui fait qu'une capacité n'est jamais déclarée.

**Les deux en-têtes sont recopiés sans interprétation :**

| En-tête | Ce que le service en fait |
| --- | --- |
| `Content-Type` | Recopié **tel quel**, et montré à un humain. Absent → `application/octet-stream`. |
| `Content-Disposition: filename=` | Seul le **dernier segment** est gardé — `/var/exports/2026/a.csv` devient `a.csv`. Absent, vide, ou illisible → le nom **dégrade sur votre `system_id`**. |

**`send_file()` seul doit suffire.** Ne rien écrire du tout ne perd pas la pièce : c'est le cas
prévu, pas le cas dégradé qu'on tolère.

### Les trois cas, et ce qui les distingue

L'enveloppe **seule** les distingue — vous n'avez aucun champ à remplir pour le dire :

| Ce que vous voulez dire | Ce que vous répondez |
| --- | --- |
| **Pièce pleine** — voici ses données. | `200`, avec des octets. |
| **Corps vide** — j'ai regardé, il n'y a rien. | `200`, **zéro octet**. |
| **Pièce absente** — je n'ai pas répondu à la question. | `202`, `401`, `404` — ou vous n'êtes pas appelé. |

⚠️ **Un `204` n'est pas une pièce vide** : c'est une panne, comme tout statut hors du tableau du
§ 4. La pièce vide est un `200` sans octets, et le contrat n'a pas deux façons de la dire.

**Dire « rien » ne doit rien coûter.** C'est pour cela que le corps vide est gratuit : s'il fallait
produire un export d'une forme convenue pour déclarer qu'on n'a rien, on serait tenté de ne rien
déclarer du tout — et l'omission silencieuse est très exactement ce que ce dispositif cherche à
rendre impossible.

### Quand vous êtes appelé, et quand vous ne l'êtes pas

- **Seulement là où le service a rattaché quelqu'un**, c'est-à-dire là où votre `locate` a rendu du
  `certain`, ou là où un humain a arbitré une de vos réserves. Vous demander les données d'une
  personne que vous venez de dire ne pas connaître ferait remonter une pièce dont personne ne
  saurait de qui elle parle.
  ⚠️ **Une réserve en attente n'est pas un rattachement.** Ce qui manque est un regard, et lire
  avant qu'un humain n'ait tranché rapatrierait les données d'un homonyme.
- **Une pièce déjà obtenue n'est jamais redemandée.** Elle répond à la question posée sous le sac
  d'aujourd'hui ; repasser rapatrierait une seconde fois les données de quelqu'un, c'est-à-dire
  allongerait le séjour que tout ce dispositif cherche à raccourcir.
- **Un sac enrichi fait repartir l'appel**, et lui seul : un arbitrage a élargi la question, et la
  pièce d'hier répondait à une plus étroite.
- **Un `202` fait repasser après l'échéance que vous avez déclarée** — à l'ouverture du dossier par
  un opérateur, jamais par une minuterie.

### Ce que la pièce devient côté service

- Elle est gardée **hors du dossier**, avec une durée de vie qui n'est pas la sienne : la remise à
  la personne la détruira sans réécrire le dossier.
- **La matière de preuve n'en porte rien** — ni le type, ni le nom du fichier, ni sa taille, ni un
  octet de son contenu. Elle consigne qu'on a lu, chez qui, quel jour, au titre de quel droit, et
  sous **combien** de désignations. Le contrôle lit l'ampleur d'une recherche ; il n'y trouve jamais
  les données de la personne.

---

## 5. Ce que le service fait d'un refus

- **Rien ne bouge dans le dossier.** Aucune étape ne change d'état : votre refus n'apprend rien sur
  le travail dû, il dit que nos deux moitiés ne sont pas d'accord.
- **La tentative est datée dans la matière de preuve**, dans le dossier au titre duquel l'appel est
  parti — le fait, sa date, le système, et « l'application » comme signataire, aucun humain n'ayant
  prononcé ce refus.
- **Le désaccord est signalé une seule fois, au grain du déploiement.** Une panne unique n'est pas N
  pannes : votre secret périmé vaut pour tous les dossiers à la fois, et le crier une fois par
  dossier ferait dépendre le bruit du nombre de demandes en cours.

---

## 6. Ce que le service ne fera jamais

- **Vous rappeler**, ou attendre que vous le rappeliez.
- **Tenir une connexion** en espérant votre réponse : répondez `202`. Le service coupe l'appel au
  bout de **30 secondes**, et traite ce dépassement comme une panne — ni servi, ni différé, ni
  refusé. Ce chiffre est arbitraire et assumé : il ne se règle pas par déploiement, parce que le
  `202` existe précisément pour qu'un travail long n'ait pas besoin d'une échéance longue.
- **Reprendre un appel.** Un appel part **une fois**, et une seule : ni reprise sur `5xx`, ni
  reprise sur échéance. Vous ne recevrez jamais deux fois la même demande parce que le service a
  trouvé la première trop lente — ce qui compte surtout le jour où `erase` sera exercé.
- **Relancer tout seul.** Aucun processus de fond ne tourne ; tout se recalcule quand un opérateur
  regarde. Le service repasse après l'échéance que vous avez déclarée, mais seulement quand un
  opérateur ouvre le dossier.
- **Corriger son `Manifest`** sur ce que vous répondez.
- **Vérifier ce que vous déclarez.** `Enregistré, jamais vérifié` : une réponse est une affirmation
  datée et attribuable, jamais un fait vérifié. Un travail déclaré fait prouve qu'on a déclaré
  l'avoir fait.
- **Parler à la personne concernée.** Elle n'atteint jamais le service, et le service ne lui écrit
  jamais.

---

## 7. Ce qui n'est pas encore là

Ce contrat est celui du **transport**, et il est complet : l'appel, le secret, le différé, les deux
refus. Ce que chaque `Capability` **rend** se fixe ticket par ticket ; `locate` est fixée (§ 4 bis),
et `read` l'est aussi — en ceci qu'elle ne fixe **rien** de la réponse (§ 4 ter).

- `erase` et `rectify` — **déclarables dès aujourd'hui** dans le `Manifest`, exercées plus tard.
  Déclarer une capacité que personne n'appelle encore est une déclaration exacte ; ne pas pouvoir la
  déclarer serait un mensonge du catalogue.

Un système **sans aucune `Capability`** reste pleinement légitime : il est recensé, et traité à la
main. C'est le régime majoritaire, et celui dont le service tire le plus de valeur — il nomme ce
qu'il ne touche pas.

---

## 8. Pour le mainteneur

- Le contrat sur le fil : [`HttpAdapterCalls`](../../src/MicroserviceRgpd.Infrastructure/Casework/Adapters/HttpAdapterCalls.cs).
- Le vocabulaire des réponses : [`AdapterOutcome`](../../src/MicroserviceRgpd.Core/Casework/Adapters/AdapterOutcome.cs),
  [`AdapterAnswer`](../../src/MicroserviceRgpd.Core/Casework/Adapters/AdapterAnswer.cs).
- Le corps d'un `locate` servi, en deux types et non un :
  [`LocateOnTheWire`](../../src/MicroserviceRgpd.Core/Casework/Adapters/LocateOnTheWire.cs) est ce que
  le fil rend — tout y est nullable, parce qu'un client peut tout omettre —, et
  [`LocateFindings`](../../src/MicroserviceRgpd.Core/Casework/Adapters/LocateFindings.cs) est ce que
  le domaine accepte d'en croire. La frontière entre les deux est le seul endroit où un corps mal
  formé devient une panne plutôt qu'un zéro.
- Tout ce que le service sait d'une pièce, et rien de plus :
  [`TransportEnvelope`](../../src/MicroserviceRgpd.Core/Casework/Adapters/TransportEnvelope.cs) — la
  recopie du `Content-Type`, le dernier segment du `filename=`, et le repli sur le `system_id` —,
  [`RetrievedPiece`](../../src/MicroserviceRgpd.Core/Casework/Adapters/RetrievedPiece.cs) pour les
  trois cas (pleine, vide, absente).
- La pièce gardée **hors de l'agrégat**, avec sa durée de vie propre :
  [`RetrievedData`](../../src/MicroserviceRgpd.Core/Casework/RetrievedData.cs),
  [`IRetrievedData`](../../src/MicroserviceRgpd.Core/Casework/IRetrievedData.cs),
  [`RetrievedDataStore`](../../src/MicroserviceRgpd.Infrastructure/Data/Casework/RetrievedDataStore.cs).
  Le **fait** de la lecture, lui, reste dans le dossier :
  [`Reading`](../../src/MicroserviceRgpd.Core/Casework/Reading.cs), qui porte l'échéance d'un `202`.
- L'appel de `read` de bout en bout, et les quatre raisons de repasser :
  [`ReadHandler`](../../src/MicroserviceRgpd.UseCases/Casework/Read/ReadHandler.cs).
- Ce qu'un refus laisse : [`AdapterCallsForCase`](../../src/MicroserviceRgpd.UseCases/Casework/CallAdapter/AdapterCallsForCase.cs).
- L'appel de `locate` de bout en bout, et la relance d'un `202` à l'ouverture du dossier :
  [`LocateHandler`](../../src/MicroserviceRgpd.UseCases/Casework/Locate/LocateHandler.cs).
- L'arbitrage d'une réserve par un humain nommé, et le sac qu'il enrichit :
  [`ArbitrateReservationHandler`](../../src/MicroserviceRgpd.UseCases/Casework/ArbitrateReservation/ArbitrateReservationHandler.cs),
  [`Case.Arbitrate`](../../src/MicroserviceRgpd.Core/Casework/Case.cs).
- La vérification du `Manifest` et l'appel au faux secret :
  [`VerifyManifestHandler`](../../src/MicroserviceRgpd.UseCases/Casework/VerifyManifest/VerifyManifestHandler.cs),
  [`IAdapterProbes`](../../src/MicroserviceRgpd.Core/Casework/Adapters/IAdapterProbes.cs),
  [`HttpAdapterProbes`](../../src/MicroserviceRgpd.Infrastructure/Casework/Adapters/HttpAdapterProbes.cs).
  Le port ne reçoit **aucune** `Capability` : c'est par la forme, et non par la vigilance de
  l'appelant, qu'aucune sonde ne peut exercer `Erase` ni `Rectify`.
- Le secret et le refus de démarrer : [`AdapterServiceExtensions`](../../src/MicroserviceRgpd.Infrastructure/Casework/Adapters/AdapterServiceExtensions.cs),
  clé `Casework:AdapterSecret`.
- L'autre bout du fil, écrit comme un client l'écrirait : l'`Adapter` du témoin,
  [`temoin/adapter_rgpd.py`](../../temoin/adapter_rgpd.py), qui sert `locate` **et** `read` pour deux
  `system_id` de deux natures — une base MariaDB et un journal à fichiers plats. Les deux rendent
  des pièces qu'aucun schéma ne rapproche : un CSV pour la base
  ([`rgpd_boutique.lire`](../../temoin/rgpd_boutique.py)), des lignes de journal en texte brut pour
  l'autre ([`rgpd_journal.lire`](../../temoin/rgpd_journal.py)). C'est là que ce contrat se vérifie
  contre autre chose que lui-même ; ce qu'il a coûté au témoin est consigné dans
  [`temoin/README.md`](../../temoin/README.md).

**La couture de test est posée sur le fil**, et c'est délibéré : un `HttpMessageHandler` injecté
dans le client de l'`Adapter`. L'en-tête de secret, le `system_id` en paramètre, le `202` et son
échéance **sont** le contrat — un port du domaine les aurait cachés au-dessus de la couture, et les
tests n'auraient plus prouvé que le comportement d'une doublure. Voir
[`HttpAdapterCallsTests`](../../tests/MicroserviceRgpd.UnitTests/Infrastructure/Casework/HttpAdapterCallsTests.cs).
