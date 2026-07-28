# API publique — `POST /qualifications`

Ce document est la référence de l'intégrateur : ce qu'on envoie, ce qu'on reçoit, ce qui peut mal
tourner, et ce qu'il ne faut surtout pas supposer. Il se lit seul, sans avoir suivi le projet.

La spec complète — avec le *pourquoi* de chaque décision — vit dans
[`docs/spec/qualification.md`](../spec/qualification.md) ; le vocabulaire du domaine dans
[`CONTEXT.md`](../../CONTEXT.md). Les deux s'adressent au mainteneur. Ce fichier-ci s'adresse à
l'appelant, et les deux dernières sections au mainteneur.

---

## 1. Ce que fait le service, et ce qu'il ne fait pas

Le service reçoit un **texte libre en français**, écrit par une personne concernée qui exerce — ou
croit exercer — un droit que le RGPD lui ouvre. Il rend, **dans le même échange**, l'ensemble des
droits que ce texte est jugé exercer.

> **C'est une aide à la décision, jamais une décision.** Le service propose une qualification ; un
> opérateur humain, côté application appelante, la valide ou la corrige. Rien dans ce contrat ne
> suppose qu'on puisse agir sur un verdict sans relecture.

Le service **ne traite pas** la demande : ni instruction, ni identification du demandeur, ni export
de portabilité, ni suivi du délai d'un mois. Il s'arrête à la qualification.

**Un `POST`, un `200`, et aucun `GET`.** La qualification n'est pas une ressource qu'on relit :
c'est un acte dont on repart avec le résultat. Il n'existe aucun moyen, pour un appelant, de relire
une qualification passée — le `qualificationId` ne se résout que par un canal humain.

**Authentification : aucune.** L'endpoint est supposé joignable en réseau de confiance. C'est une
décision de périmètre, pas un oubli.

---

## 2. Requête

```http
POST /qualifications
Content-Type: application/json
```

```json
{
  "text": "Je souhaite obtenir une copie de mes données puis les faire supprimer.",
  "callerReference": "DSAR-8871"
}
```

Deux champs, dont un seul obligatoire. **Aucun champ de langue** : le français est la seule option.

### 2.1 `text` — obligatoire

| Contrainte | Valeur |
| --- | --- |
| Type | chaîne UTF-8 |
| Non vide | après suppression des espaces de début et de fin |
| Plafond | **10 000 caractères**, mesurés **après** ce nettoyage |
| Plancher | **aucun** |

- **Le plafond se compte en unités UTF-16** (`🙂` vaut 2). Approximation assumée, pour ne pas faire
  entrer les éléments de texte Unicode dans un contrat public. Un appelant qui doit tronquer
  tronque sur cette base.
- **Second garde-fou, en amont du plafond : le corps de la requête est borné à 64 Ko.** Il agit au
  niveau du transport, avant toute désérialisation, et rend un `413` (§ 4). Un appelant qui envoie
  de longs textes doit donc respecter **les deux** bornes : 10 000 caractères *et* 64 Ko de corps.
- **Aucun plancher au-delà de la non-vacuité.** Un seul caractère est un texte valide : sur `🙂`
  seul, la réponse correcte est `OutOfScope` — un **verdict** —, pas un `400`. Rejeter un texte
  parce qu'il est court reviendrait à confondre « je n'y reconnais aucun droit » avec « ta requête
  est malformée ».
- **Aucune détection de langue, aucune détection de bruit.** Un texte hors sujet, dans une autre
  langue, ou incohérent n'est pas une erreur d'entrée : il ressort qualifié, généralement en
  `OutOfScope`, souvent avec un `reviewSignal` élevé.

### 2.2 `callerReference` — facultative

| Contrainte | Valeur |
| --- | --- |
| Longueur | **64 caractères au plus**, après nettoyage des bordures |
| Caractères | aucun caractère de contrôle |
| Traitement | espaces de bordure nettoyés ; **vide après nettoyage vaut absente** |
| Restitution | **verbatim**, dans la réponse `200` |

C'est la clé de corrélation de l'appelant : elle lui appartient. Le service **ne l'interprète
jamais** — ni format, ni sens, ni unicité — et ne la normalise pas au-delà de ses bordures.

> ### ⚠️ La référence appelante n'est **pas** une clé d'idempotence
>
> Deux requêtes portant la même `callerReference` produisent **deux qualifications distinctes, deux
> `qualificationId`, deux traces d'audit**. Le service n'en impose pas l'unicité, ne la déduplique
> pas, et ne rejouera jamais une réponse précédente. Bâtir un mécanisme de déduplication sur cette
> référence serait s'appuyer sur une garantie qui n'existe pas.

> ### ⚠️ La référence appelante ne doit **pas** contenir de donnée personnelle
>
> Elle est conservée en clair dans la trace d'audit. C'est une **contrainte de documentation,
> explicitement inapplicable techniquement** : le service ne peut pas la vérifier et ne tentera pas
> de le faire. Elle pèse donc entièrement sur le **responsable de traitement**, c'est-à-dire sur
> l'appelant.

### 2.3 Trois métadonnées volontairement absentes

Un horodatage fourni par l'appelant doublonnerait l'heure de réception ; le canal d'origine
n'atteindrait aucun moteur et serait donc collecté pour être seulement stocké — exactement ce qu'un
service de conformité ne devrait pas faire ; le contexte de la relation n'entre pas dans cette
version. Les envoyer n'a aucun effet : ils sont ignorés.

---

## 3. Réponse `200`

```json
{
  "qualificationId": "0198f3a2-7c41-7b3e-9a2d-1f5c8e6b4d90",
  "callerReference": "DSAR-8871",
  "rights": ["Access", "Erasure"],
  "reviewSignal": "NeedsReview",
  "degraded": false,
  "justification": "Savoir ce qui est détenu puis tout supprimer : art. 15 puis art. 17."
}
```

| Champ | Type | Présence | Sens |
| --- | --- | --- | --- |
| `qualificationId` | `string` (UUID) | **toujours** | l'identité que le service donne à cette qualification, durable, clé de la trace d'audit |
| `callerReference` | `string` | **si fournie** | rendue verbatim ; absente sinon |
| `rights` | `string[]` | **toujours** | les droits reconnus, jamais vide |
| `reviewSignal` | `string` | **toujours** | l'urgence à relire |
| `degraded` | `boolean` | **toujours** | le service était-il entier au moment de rendre ce verdict |
| `justification` | `string` | **facultative** | la phrase, en français, qu'un opérateur lit pour comprendre le verdict |

**`qualificationId` est l'identifiant à citer dans un ticket de support** — jamais le `traceId`,
dont la rétention et l'échantillonnage échappent au service.

> **`justification` est facultative par contrat, et doit être typée comme pouvant manquer.** Elle
> est absente en mode dégradé, où le service se tait plutôt que d'inventer une raison. Un
> désérialiseur qui l'exige cassera le jour où le service tournera dégradé — c'est-à-dire un jour
> où il répond parfaitement bien.

Deux mises en garde sur la justification : elle est **persuasive indépendamment de sa justesse**
(une phrase bien tournée emporte l'adhésion), et elle **paraphrase le texte reçu**, donc son
contenu — sans conséquence vers l'appelant, qui vient d'envoyer ce texte.

### 3.1 `rights` — la taxonomie fermée de sept valeurs

`rights` **n'est jamais vide** : « aucun droit reconnu » s'écrit `OutOfScope`, qui est un verdict et
non une absence de verdict. Et **`OutOfScope` est exclusif** — il n'apparaît jamais accompagné d'un
autre droit. Plusieurs droits peuvent en revanche coexister : le multi-droits est fondé en droit,
un même message pouvant relever de l'accès et de l'effacement.

| Valeur | Droit | Article |
| --- | --- | --- |
| `Access` | obtenir communication des données traitées et des informations sur leur traitement | art. 15 |
| `Rectification` | faire corriger des données inexactes, compléter des données incomplètes | art. 16 |
| `Erasure` | faire supprimer des données — droit à l'oubli | art. 17 |
| `Restriction` | faire geler le traitement sans faire supprimer | art. 18 |
| `Portability` | recevoir ses données dans un format réutilisable, ou les faire transmettre | art. 20 |
| `Objection` | s'opposer à un traitement, notamment à la prospection | art. 21 |
| `OutOfScope` | aucun des six droits n'a été reconnu | — |

`OutOfScope` couvre aussi bien un texte étranger au RGPD qu'une demande relevant d'un droit resté
**hors de la taxonomie** — déréférencement, droit à l'information, décision individuelle
automatisée (art. 22). Ces demandes existent ; elles retombent ici, et la `justification` donne à
l'opérateur de quoi comprendre pourquoi.

L'ordre des valeurs dans `rights` est celui du tableau ci-dessus. Il est stable, mais le domaine
tient `rights` pour un **ensemble** : ne lui prêtez aucun sens.

### 3.2 `reviewSignal` — l'urgence à relire

Trois valeurs. Le signal **priorise** la relecture, il ne la déclenche pas : un humain valide de
toute façon chaque qualification, `Corroborated` compris.

| Valeur | Sens |
| --- | --- |
| `Corroborated` | le verdict a reçu un contrôle indépendant favorable, assorti d'une confiance haute |
| `NeedsReview` | le verdict n'a pas reçu ce contrôle favorable — soit la confiance n'était pas haute, soit le contrôle n'a pas pu avoir lieu |
| `Contested` | **le signal le plus fort** : deux évaluations indépendantes du même texte divergent |

Le service ne publie ni les évaluations qui produisent ce signal, ni la confiance dont elles
s'accompagnent : le `reviewSignal` est la conclusion, et publier ses prémisses inviterait chaque
appelant à recalculer sa propre règle hors de tout test.

### 3.3 `degraded` — le service était-il entier

`true` quand le service n'était pas entier au moment de rendre ce verdict : une des deux évaluations
n'a pas pu être produite. Le verdict rendu reste un verdict, il est simplement moins bien étayé.

**`degraded` est distinct de `reviewSignal`, et les deux doivent être lus.** L'un dit « avec quelle
attention relire ? », l'autre dit « le service était-il entier ? ». Quand `degraded` vaut `true`,
`reviewSignal` vaut nécessairement `NeedsReview` et `justification` peut manquer.

### 3.4 Ce que la réponse tait délibérément

Ni le texte en écho, ni horodatage, ni les évaluations brutes, ni la confiance déclarée, ni
l'identité ou la version des moteurs de qualification. Ces informations circulent à l'intérieur du
service sans franchir cette frontière : les publier graverait l'architecture interne dans le
contrat public.

---

## 4. Erreurs

**Toutes les erreurs de cette API sortent dans une forme unique** : `application/problem+json`
conforme à la **RFC 9457**. Sans exception — validation d'entrée, exception non gérée, et codes
rendus par la plateforme avant que l'application ne soit atteinte. Un appelant n'a donc qu'un seul
format d'erreur à savoir lire.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "La qualification est indisponible",
  "status": 503,
  "detail": "Aucun moteur n'a rendu d'avis. Le service ne peut rien qualifier pour l'instant.",
  "traceId": "00-8f3c1a9d2e4b7c60a1f5d8e3b9c04a72-3b1f9c7d2e408a15-01"
}
```

`title` et `detail` sont en **français** ; les noms de champs en anglais. **`traceId` est toujours
présent** : en erreur, aucune qualification n'a eu lieu, il n'y a donc aucun `qualificationId` à
citer et le `traceId` est la seule identité qui vaille pour un ticket de support.

| Code | Cas | Réaction attendue de l'appelant |
| --- | --- | --- |
| `400` | `text` absent, vide une fois nettoyé, ou au-delà de 10 000 caractères ; `callerReference` trop longue ou porteuse d'un caractère de contrôle ; JSON malformé | corriger la requête |
| `404` | route inconnue | corriger l'URL |
| `405` | méthode non supportée — il n'existe **aucun** `GET /qualifications` | corriger la méthode |
| `413` | corps au-delà de **64 Ko** — rendu par le serveur HTTP, avant l'application | tronquer avant d'appeler |
| `415` | `Content-Type` autre qu'`application/json` — rendu avant l'application | corriger l'en-tête |
| `500` | défaillance interne, **y compris l'échec d'écriture de la trace d'audit** | ouvrir un ticket avec le `traceId` |
| `503` | qualification indisponible : **aucune** évaluation n'a pu être produite | voir ci-dessous |
| `504` | même situation, l'évaluation principale ayant en outre dépassé son échéance | voir ci-dessous |

**`413` et `415` sont produits par la plateforme HTTP, pas par le code du service.** Ils sont
documentés parce qu'un appelant les rencontrera ; ils ne sont couverts par aucun test et ne font
l'objet d'aucune implémentation propre. Ils sortent néanmoins dans la forme unique ci-dessus.

> ### ⚠️ `503` et `504` sont quasi inatteignables, et signent une panne totale
>
> **La cause réaliste d'un `503` n'est pas « le serveur de modèles est éteint »** — ce cas rend un
> `200` avec `degraded: true`, et c'est tout le sens du mode dégradé. Un `503` signifie que **le
> composant de qualification entier est mort** : aucune des deux évaluations n'a pu être produite.
> C'est une panne d'exploitation, pas une variation de charge.
>
> **Un `504` ne doit pas être réessayé en boucle.** Il dit que le travail a commencé sans aboutir,
> et la génération étant déterministe — température nulle, seed fixe —, **un rejeu immédiat de la
> même requête reproduira le même dépassement à l'identique**. Réessayer ajoute de la file d'attente
> sur un composant déjà saturé, sans aucune chance de succès. Attendre, alerter l'exploitation, et
> ne rejouer qu'une fois la cause traitée.
>
> Un `503` peut, lui, être réessayé plus tard — c'est la différence que les deux codes portent.

**Aucune de ces erreurs ne laisse de trace d'audit** : la trace enregistre les verdicts, jamais les
tentatives (§ 6.3).

---

## 5. Conventions de nommage

Une seule convention, sur tout le fil, en requête comme en réponse comme en erreur :

- **`camelCase` pour les noms de champs** — `callerReference`, `qualificationId`, `reviewSignal`,
  `traceId`.
- **`PascalCase` pour les valeurs** de `rights` et de `reviewSignal` — `Access`, `OutOfScope`,
  `NeedsReview`. Ce sont des **identifiants**, pas des textes destinés à l'humain : ils ne sont ni
  traduits, ni localisés, ni destinés à être affichés tels quels.
- **Les textes destinés à l'humain sont en français** — `title`, `detail`, `justification`.

Les valeurs sont **sensibles à la casse**.

---

## 6. Pour le mainteneur

### 6.1 Règle d'évolution — ce qui est une rupture

**Rétro-compatible :**

- ajouter un champ **facultatif** à la réponse ;
- assouplir une contrainte d'entrée.

**Rupture — un événement de niveau ADR, pas une extension de routine :**

- retirer un champ, en renommer un, changer la sémantique d'un champ existant ;
- durcir une contrainte d'entrée ;
- **ajouter une valeur à `rights`** — la taxonomie des sept `DataSubjectRight` est **fermée par
  décision** ;
- **ajouter une valeur à `reviewSignal`**.

Les deux derniers points sont ceux qu'on croit anodins, et ils ne le sont pas : un appelant ayant
écrit un `switch` exhaustif sur sept droits casse à la huitième valeur, et rien dans le code du
service ne le lui dira. La projection sur le fil vit dans
[`data-subject-rights.wire.json`](../../data-subject-rights.wire.json), gardée par un test dans les
deux sens — ce test n'est pas là pour être fait taire.

### 6.2 Dette : il n'y a pas de segment de version, et sa porte de sortie

**Le chemin est `POST /qualifications`, sans `/v1`.** C'est la seule décision du contrat prise
contre la recommandation, et **la seule qu'on ne pouvait pas différer sans coût** : tous les autres
champs s'ajoutent de façon rétro-compatible, mais introduire un segment de version plus tard casse
**tous** les appelants d'un coup.

**La dette est réelle, assumée, et sa porte de sortie est décidée d'avance** : le jour où une
seconde version arrive, **`POST /qualifications` devient l'alias permanent de la première**, et le
segment de version n'est exigé que des nouveaux appelants. Cette atténuation fait partie de la
décision initiale ; ce n'est pas un rattrapage à improviser le jour venu.

### 6.3 Trois avertissements d'exploitation

> **1. Le texte reçu est conservé intégral, en clair, sans aucune purge.** La trace d'audit garde le
> `text` tel quel, ainsi que la `callerReference` et la `justification` qui paraphrase le texte. Il
> n'existe ni durée de rétention déclarée, ni mécanisme de purge, ni minimisation. **Aucune donnée
> réelle ne doit atteindre ce service avant que la rétention ne soit traitée** : c'est une condition
> de sortie de POC, pas une amélioration souhaitable. Ce qui rend l'état actuel défendable est
> exactement, et uniquement, le fait qu'aucune donnée réelle ne transite.

> **2. Le service sort sans aucun critère disant si la qualification est bonne.** Il n'existe ni
> métrique faisant foi, ni seuil d'acceptation, ni taux de faux positifs toléré sur le hors
> périmètre. La barre de qualité du dépôt porte sur **le code**, pas sur la justesse des verdicts.
> Le seul chiffre jamais mesuré l'a été sur un autre moteur que celui qui est servi : le reprendre
> serait emprunter un seuil. Cette question est ouverte et **sans propriétaire assigné**.

> **3. Les pannes sont invisibles à l'audit.** Un `503`, un `504` ou une annulation de l'appelant ne
> laisse **aucune ligne** dans la trace : celle-ci enregistre les verdicts, jamais les tentatives.
> Un appelant qui contesterait « je vous ai envoyé un texte, vous n'en avez aucune trace » aurait
> raison, et le service ne peut lui opposer que sa télémétrie — dont la rétention et
> l'échantillonnage lui échappent. Le trou est assumé en POC, et à réexaminer avec le bloc
> rétention.

### 6.4 Le bruit adverse : ce qu'il peut, et ce qu'il ne peut pas

Le service accepte n'importe quel texte, y compris un texte écrit pour détourner le moteur. La
question a une réponse bornée, et elle est écrite ici pour ne pas revenir sous forme d'inquiétude
sans réponse.

> **Le pire résultat atteignable par un texte adverse est un verdict faux parmi sept valeurs.** Ni
> exécution arbitraire, ni fuite de données, ni évasion du format : la sortie du moteur est validée
> contre la taxonomie fermée — sept valeurs, non vide, `OutOfScope` exclusif — avant d'atteindre
> quoi que ce soit d'autre. Une réponse hors taxonomie est refusée, pas transmise.

Deux amortisseurs s'ajoutent : la seconde évaluation est **lexicale, donc insensible à toute
injection de prompt** — elle divergera, et le verdict sortira en `Contested` ; et **un humain valide
chaque qualification**. L'authentification étant hors périmètre et le réseau supposé de confiance,
rien de plus n'est fait dans cette version.

---

## 7. Exemples

Des requêtes exécutables — succès, référence absente, texte d'un caractère, texte refusé — vivent
dans [`src/MicroserviceRgpd.Web/api.http`](../../src/MicroserviceRgpd.Web/api.http). Le service
expose par ailleurs son OpenAPI, explorable via Scalar quand il tourne en développement.
