# Spec — Qualification RGPD d'un texte reçu d'une application tierce

Statut : **prête à implémenter**. Version 1 (POC).

Ce document rassemble les décisions de la carte [#1](https://github.com/AmauryTISSOT/microservice_rgpd/issues/1) en un seul texte suffisant pour écrire le code. Chaque section indique le ticket qui l'a tranchée ; le commentaire de résolution de ce ticket reste la source de vérité pour le *pourquoi*, ce document pour le *quoi*. Aucune question ouverte ne subsiste dans le périmètre couvert ici : ce qui reste ouvert est rassemblé au § 13, hors périmètre d'implémentation.

---

## 1. Objet et périmètre

Un texte libre **en français** arrive en `POST` HTTP depuis une application tierce. Le service rend la ou les qualifications RGPD parmi une taxonomie fermée de sept valeurs, **en aide à la décision** — un humain valide côté application tierce — et conserve une **trace d'audit** interne.

**Dans le périmètre**

- Qualification d'un texte vers `DataSubjectRight`, multi-droits.
- Un signal de relecture indépendant, issu de l'entrecontrôle de deux moteurs.
- Un mode dégradé explicite et signalé.
- Une trace d'audit en écriture seule.

**Hors périmètre, décidé** (§ 13.3 pour le détail et les motifs)

Traitement effectif de la demande · authentification de l'application tierce · détection et caviardage des données personnelles · interface de relecture · droits hors des sept valeurs · recours à un LLM en API tierce.

**Posture produit.** POC. Une erreur de qualification coûte peu, puisqu'un humain valide. **La latence n'est pas un critère de rejet.** Aucune donnée réelle ne transite — c'est cette hypothèse, et elle seule, qui rend défendables l'absence de rétention (§ 8.2) et la conservation du texte en clair.

---

## 2. Vocabulaire et taxonomie

> Tranché par [Fixer le vocabulaire du domaine de la qualification](https://github.com/AmauryTISSOT/microservice_rgpd/issues/5) · glossaire dans [`CONTEXT.md`](../contexts/qualification/CONTEXT.md)

**Langue du code : anglais** pour tous les identifiants — types, membres, dossiers, noms de champs sur le fil. Le français reste dans les `Summary` Swagger, les libellés destinés à l'opérateur, les messages d'erreur, `CONTEXT.md` et ce document.

### 2.1 Concepts

| Type | Rôle | Forme |
| --- | --- | --- |
| `RightsRequestText` | le texte reçu, *présumé* être une demande | value object **Vogen** |
| `Qualification` | le verdict : l'ensemble des droits reconnus | type de `Core` |
| `DataSubjectRight` | la taxonomie fermée | **`SmartEnum<DataSubjectRight>`** scellé |
| `QualificationOpinion` | l'avis d'un moteur | type de `Core` |
| `WitnessOpinion` | l'avis témoin du lexique, en marche nominale | rôle, glosé dans `CONTEXT.md` |
| `ReviewSignal` | le signal de relecture rendu à l'appelant | énumération à trois valeurs |

### 2.2 La taxonomie

Sept membres, chacun portant `int? Article` et `string FrenchLabel` **comme données attachées** — une seule source de vérité, aucune table de correspondance parallèle.

| Membre | Article | Libellé français |
| --- | --- | --- |
| `Access` | 15 | droit d'accès |
| `Rectification` | 16 | droit de rectification |
| `Erasure` | 17 | droit à l'effacement |
| `Restriction` | 18 | droit à la limitation du traitement |
| `Portability` | 20 | droit à la portabilité |
| `Objection` | 21 | droit d'opposition |
| `OutOfScope` | — | hors périmètre |

La valeur entière du `SmartEnum` est un **ordinal stable, jamais le numéro d'article** — `OutOfScope` n'en a aucun. Coût assumé : un convertisseur JSON et un convertisseur EF Core, écrits une fois.

### 2.3 Invariants du domaine

- **I1** — `Qualification.Rights` n'est **jamais vide**. « Aucun droit reconnu » est la valeur nommée `OutOfScope`, pas une absence à interpréter : un verdict ne peut donc pas se confondre avec une panne de moteur.
- **I2** — **`OutOfScope` est exclusif** : il ne se combine jamais avec un droit.
- **I3** — `OutOfScope` couvre aussi les droits laissés hors taxonomie (déréférencement, droit à l'information, art. 22).

Ces trois invariants sont validés **à la frontière du sidecar** (§ 5.4) et re-portés par le type de `Core`.

### 2.4 Emplacement du code

```
src/MicroserviceRgpd.Core/            DataSubjectRight, RightsRequestText, Qualification,
                                      QualificationOpinion, ReviewSignal,
                                      la règle de corroboration, IQualificationEngine,
                                      IQualificationAuditTrail
src/MicroserviceRgpd.UseCases/Qualifications/Qualify/
                                      QualifyCommand.cs   (RightsRequestText Text, string? CallerReference)
                                      QualifyHandler.cs   -> Result<...>
src/MicroserviceRgpd.Infrastructure/  les deux clients HTTP du sidecar,
                                      l'entité EF Core de la trace + sa configuration
src/MicroserviceRgpd.Web/Qualifications/Qualify.cs        POST /qualifications
```

Regroupement par concept au pluriel puis par opération, suivant la convention du gabarit Ardalis en place.

---

## 3. Architecture retenue

> Tranché par [Trancher le moteur de qualification retenu pour la v1](https://github.com/AmauryTISSOT/microservice_rgpd/issues/7)

**Un hybride à deux moteurs qui s'entrecontrôlent, entièrement auto-hébergé.**

Un **LLM open-weights local** — `qwen3:8b` servi par Ollama en container Aspire — rend le **verdict**. Un **lexique déterministe** rend un avis **témoin**. Leur divergence devient un signal d'attention adressé à l'opérateur humain.

```
   .NET Web  (contrat public § 4, trace d'audit § 8)
        |
        v
   Core :  la règle de corroboration (§ 6)          <- règle du domaine,
           avis LLM     = le verdict                   testable sans réseau
           avis lexique = témoin                       ni container
              |                        |
        IQualificationEngine     IQualificationEngine
              |                        |
   Infra :  client HTTP              client HTTP     <- simple transport
              |                        |
              +-----------+------------+
                          v
        SIDECAR PYTHON  (Aspire.Hosting.Python 13.4.6)
          POST /opinions/llm       -> moteur LLM
          POST /opinions/lexicon   -> moteur lexical
                          |
                          v
                 container Ollama (qwen3:8b)
```

**Pourquoi l'auto-hébergement.** Le caviardage étant hors périmètre, une demande réelle part avec l'identité du demandeur : tout appel à un tiers serait une sous-traitance au sens de l'art. 28. L'auto-hébergement supprime le sous-traitant, l'engagement contractuel et la question de la localisation **par construction**.

**Pourquoi deux endpoints et non un.** L'indépendance des deux avis devient *structurelle* : un endpoint unique pourrait un jour faire dépendre un avis de l'autre sans que .NET le sache, et la corroboration deviendrait un théâtre. Bénéfice second : deux modes de panne séparés (§ 7).

**Pourquoi la règle vit dans `Core`.** Comparer deux avis et en déduire s'il faut un humain, c'est la posture « aide à la décision » rendue exécutable — et cela se teste sans container ni réseau.

**Pourquoi `IQualificationEngine` et non un appel direct.** Elle garde **deux implémentations réelles** ; le sidecar n'est qu'un détail de transport. Si un moteur revenait en C#, ou si un troisième apparaissait, `Core` ne bouge pas.

**Pourquoi un sidecar Python alors que rien ne l'impose.** Le lexique n'a aucune dépendance tierce et se porterait en C#. La frontière est posée pour un motif unique et explicite : **une évolution vers l'apprentissage automatique est anticipée** (plongements, classifieur entraîné, *fine-tuning*). Mieux vaut la poser tant qu'elle est bon marché. **Si ce motif tombait, la décision serait à revoir.**

**Portabilité du modèle.** Elle vit **côté Python** : le client compatible OpenAI parle indifféremment à Ollama et à Mistral. Changer de modèle local ou basculer vers une API se réduit à un `base_url` et un nom de modèle en configuration — zéro ligne de logique de qualification touchée. `Microsoft.Extensions.AI` / `IChatClient` est **écarté** : l'appel LLM vivant en Python, cette abstraction n'aurait plus rien à abstraire.

**Matériel.** NVIDIA RTX 3070 Laptop, **8 Go de VRAM**. `qwen3:8b` en Q4 pèse ~5 Go et tient avec de la marge pour le contexte. 8 Go est un **plafond réel** — un 14B en Q4 (~9 Go) ne tiendrait pas ; la classe 7–8B est la bonne cible. Le GPU **sérialise de fait les appels**, contrainte qui traverse les § 4.2, § 7.5 et § 12.

---

## 4. Contrat public

> Tranché par [Fixer le contrat de l'endpoint de qualification](https://github.com/AmauryTISSOT/microservice_rgpd/issues/8), complété par [Définir les comportements dégradés](https://github.com/AmauryTISSOT/microservice_rgpd/issues/10) pour `degraded`

**Ligne directrice.** Sur un contrat **public**, ajouter un champ est rétro-compatible, en retirer un ne l'est pas. Le défaut prudent est donc l'**inverse** de celui du contrat interne (§ 5), où les deux extrémités sont à nous. Cette asymétrie est délibérée.

### 4.1 Forme générale

**`POST /qualifications`, calcul synchrone, `200 OK`. Aucun `GET` public.**

Un `201 Created` avec `Location` est écarté : il ferait de la trace d'audit une ressource métier exposée — précisément l'agrégat que la carte a refusé — et un `Location` qui ne mène nulle part est un mensonge de contrat.

**Pas de segment de version.** Le chemin reste `POST /qualifications` — voir § 4.6, la seule décision prise contre la recommandation.

### 4.2 Requête

```jsonc
POST /qualifications
Content-Type: application/json

{
  "text": "Je souhaite obtenir une copie de mes données puis les faire supprimer.",
  "callerReference": "DSAR-8871"
}
```

**`text`** — obligatoire, UTF-8, **non vide après suppression des espaces de début et de fin**, **10 000 caractères au maximum**. Pas de champ de langue : le français est la seule option.

- **Aucun plancher au-delà de la non-vacuité.** Le corpus donne une médiane de 102 caractères, un p95 à 210, un maximum de 1 195 — et un minimum de **1 caractère**, l'emoji `🙂` versé délibérément, suivi de `Mes données ?` (13), `Droit d'accès.` (14), `Supprimez tout.` (15). Sur `🙂`, la bonne réponse est **`OutOfScope`**, pas un `400` : rejeter un texte parce qu'il est court, c'est confondre « je n'y reconnais aucun droit » avec « ta requête est malformée ».
- **Plafond à 10 000 caractères** — huit fois le plus long exemple du corpus, soit ~2 500–3 500 tokens de français, qui tiennent sans peine dans le contexte de `qwen3:8b` à côté du prompt système de ~1 800 tokens. Chiffre **arbitraire et assumé**. Il ne protège pas d'un attaquant — l'authentification est hors périmètre — mais de l'usage normal : sur un GPU unique qui sérialise, un seul texte démesuré immobilise le service.
- La borne se compte en **unités UTF-16** (.NET), où `🙂` vaut 2. Approximation assumée plutôt que d'introduire les éléments de texte Unicode dans un contrat public.
- **Second garde-fou : corps borné à 64 Ko** au niveau du transport. C'est celui qui protège réellement, agissant avant toute désérialisation.

**`callerReference`** — facultative, **64 caractères au plus**, sans caractère de contrôle, espaces de bordure nettoyés ; vide après nettoyage vaut absente. Le service ne l'interprète jamais, ne la normalise pas, n'en impose pas l'unicité, et la **renvoie verbatim**.

> **Ce n'est pas une clé d'idempotence, et la documentation d'API doit l'écrire.** Deux requêtes portant la même référence produisent deux qualifications distinctes, deux `qualificationId`, deux traces.

> **Elle ne doit pas contenir de donnée personnelle** — contrainte de documentation, **explicitement inapplicable techniquement** : le service ne peut pas la vérifier. Elle pèse sur le responsable de traitement (§ 8.3).

**Trois métadonnées écartées.** L'horodatage fourni par l'appelant doublonnerait l'heure de réception. Le canal d'origine **n'atteint aucun moteur** — il serait collecté pour être stocké, exactement ce qu'un service de conformité ne devrait pas faire. Le **contexte de la relation** attendu par le CEPD n'entre pas dans la v1 : le faire entrer supposerait de rouvrir le contrat interne, le sidecar ne recevant que `text` (§ 13.1).

### 4.3 Réponse `200`

```jsonc
{
  "qualificationId": "0198f3a2-7c41-7b3e-9a2d-1f5c8e6b4d90",
  "callerReference": "DSAR-8871",
  "rights": ["Access", "Erasure"],
  "reviewSignal": "NeedsReview",
  "degraded": false,
  "justification": "Savoir ce qui est détenu puis tout supprimer : art. 15 puis art. 17."
}
```

| Champ | Présence | Sens |
| --- | --- | --- |
| `qualificationId` | **toujours** | forgé par le service, **clé primaire de la trace d'audit** |
| `callerReference` | si fournie | renvoyée verbatim |
| `rights` | **toujours** | la `Qualification` — jamais vide, `OutOfScope` exclusif, noms canoniques |
| `reviewSignal` | **toujours** | `Corroborated` / `NeedsReview` / `Contested` (§ 6) |
| `degraded` | **toujours** | booléen, `false` en marche normale (§ 7.2) |
| `justification` | **facultative** | phrase produite par le seul LLM |

**`qualificationId` n'est pas le `traceId`.** Un identifiant de trace vit dans le backend de télémétrie, dont la rétention se compte en jours et dont l'échantillonnage est décidé en aval — `ServiceDefaults` ne fixe aucun échantillonneur. Un `traceId` échantillonné hors du lot ne référence plus rien. Exposer les deux serait offrir un choix, donc garantir qu'un ticket de support cite l'un des deux sans qu'on sache lequel. *Implémentation, hors contrat :* `Guid.CreateVersion7()`, natif depuis .NET 9 — ordonné dans le temps, donc sans fragmentation d'index côté PostgreSQL.

**`justification` est facultative par le domaine, pas par concession.** Si le LLM tombe et que seul le lexique répond, il n'y a aucune justification à rendre ; un champ obligatoire forcerait à en inventer une. La rendre conditionnelle au `reviewSignal` est écartée : cela ferait dépendre la forme de la réponse de son contenu, et « on ne justifie que le douteux » s'effondre devant un fait acté — **un humain valide chaque qualification**, `Corroborated` compris.

*Deux coûts assumés sur la justification.* Le **biais d'automatisation** : une phrase bien tournée est persuasive indépendamment de sa justesse. Et elle **paraphrase le texte reçu**, donc des données personnelles — sans conséquence vers l'appelant, qui vient d'envoyer ce texte, mais lourd pour la trace (§ 8.3).

### 4.4 Ce que la réponse tait délibérément

Ni le texte en écho, ni horodatage. Et **trois informations qui circulent à l'intérieur sans franchir la frontière** :

- **Les deux avis bruts.** Le lexique est un **détecteur, jamais contributeur** en marche nominale (§ 6) ; les exposer inviterait l'application tierce à le faire voter hors de toute règle, et graverait le sidecar à deux moteurs dans le contrat public.
- **La `DeclaredConfidence`.** Elle est déjà contenue dans le `reviewSignal` — `NeedsReview` *signifie* « confiance non haute ». Publier une conclusion et sa prémisse invite l'appelant à recalculer sa propre règle, hors de `Core` et hors de tout test — exactement la règle fondée sur l'auto-évaluation d'un petit modèle que l'architecture a refusée. S'y ajoute qu'elle **n'est pas calibrée**.
- **L'identité et la version des moteurs.** Publier `"llm"` et `"lexicon"` graverait l'architecture dans le contrat public par une autre porte. Un digest de modèle ferait de chaque montée de version un événement de contrat, alors que le changement de modèle est explicitement anticipé.

> **Objection nommée.** Le `qualificationId` ne permet de retrouver l'identité du moteur que **par un canal humain**, aucun `GET` public n'existant. Un appelant qui voudrait, seul, distinguer deux verdicts rendus par deux versions du système n'a rien. Une chaîne opaque unique — un `qualifierVersion` sans nom de moteur ni digest — répondrait à ce besoin sans rien révéler, et redeviendra le bon choix le jour où plusieurs consommateurs existeront. Écartée pour un POC à appelant unique et connu.

### 4.5 Erreurs

**`application/problem+json`, RFC 9457.** Cela suppose un réglage explicite : **`Errors.UseProblemDetails()`** dans FastEndpoints, faute de quoi les échecs de validation sortiraient dans le format maison `ErrorResponse` et **deux formes d'erreur coexisteraient** dans la même API.

**Chaque situation reçoit l'identité qui lui correspond.** En succès, le `qualificationId`, durable, adossé à l'audit. En erreur, aucune qualification n'a eu lieu et il n'y a rien à référencer dans l'audit : c'est le **`traceId`**, qu'ASP.NET ajoute déjà en extension de `ProblemDetails`. L'objection de rétention opposée au `traceId` en § 4.3 ne mord pas ici, où l'on parle de diagnostic et non d'audit.

`title` et `detail` en français, noms de champs en anglais.

| Code | Cas | `ResultStatus` |
| --- | --- | --- |
| `200` | qualification rendue, éventuellement `degraded` | `Ok` |
| `400` | `text` absent, vide ou trop long ; `callerReference` invalide ; JSON malformé | `Invalid` |
| `500` | défaillance interne — **y compris l'échec d'écriture de la trace** (§ 8.5) | `Error` / `CriticalError` |
| `503` | qualification indisponible — **double panne**, hors délai LLM | `Unavailable` |
| `504` | double panne **avec** délai LLM dépassé | branche explicite |

Deux codes produits par la plateforme **avant** l'application, à documenter sans les implémenter : **`413`** (corps au-delà de 64 Ko, rendu par Kestrel) et **`415`** (`Content-Type` autre que `application/json`).

> **Le `504` distinct du `503` est une entorse assumée à Ardalis.Result.** La bibliothèque n'a aucun statut de délai dépassé — ses onze valeurs vont de `Ok` à `Unavailable` sans passer par là. Le `504` se produit donc **en sortant du mapping standard, par une branche explicite dans l'endpoint**. Ce coût est payé parce que la réaction attendue diffère : `503` invite à réessayer plus tard, `504` dit que le travail a commencé sans aboutir — et qu'à `temperature: 0` avec seed fixe, un simple rejeu redonnera le même dépassement.

> **`503` et `504` publics sont quasi inatteignables** (§ 7.4). La cause réaliste d'un `503` n'est **pas** « Ollama est éteint » — ce cas rend un `200 degraded` — mais « le sidecar entier est mort ». **Cette phrase doit figurer dans la documentation d'API**, sans quoi un exploitant lira ces codes de travers le jour où ils tomberont.

### 4.6 Versionnement — la seule décision prise contre la recommandation

**Pas de segment de version.**

Le versionnement dans le chemin — `/api/v1/qualifications`, via le support natif de FastEndpoints 7.1 — était recommandé au motif que **c'est la seule décision du contrat qu'on ne peut pas différer sans coût** : tous les autres champs s'ajoutent de façon rétro-compatible, mais introduire un segment de version plus tard casse **tous** les appelants d'un coup.

**La dette est réelle et nommée ici pour ne pas être découverte plus tard.** Elle est atténuable, à condition de le décider **le jour où** la version arrive : garder `POST /qualifications` comme **alias permanent de `v1`** et n'exiger le segment que des nouveaux appelants. Cette porte de sortie fait partie de la décision, pas d'un rattrapage improvisé.

**Règle d'évolution, applicable dès maintenant :**

- **Rétro-compatible** — ajouter un champ optionnel à la réponse, assouplir une contrainte d'entrée.
- **Rupture** — retirer ou renommer un champ, durcir une contrainte d'entrée, changer la sémantique d'un champ existant, et **ajouter une valeur à `DataSubjectRight` ou à `reviewSignal`**. Un appelant ayant écrit un `switch` exhaustif sur sept droits casse à la huitième valeur. La taxonomie est **fermée par décision** ; son extension est un événement de niveau ADR.

**Nommage sur le fil** : `camelCase` pour les champs, `PascalCase` pour les valeurs de `rights` et de `reviewSignal`.

---

## 5. Contrat interne — .NET ↔ sidecar Python

> Tranché par [Fixer le contrat interne entre le service .NET et le sidecar de qualification](https://github.com/AmauryTISSOT/microservice_rgpd/issues/16)

**Le sidecar ne rend jamais d'avis à moitié valide.** Contrat *interne* : le contrat public (§ 4) en reste entièrement libre.

### 5.1 Vocabulaire du fil — noms canoniques anglais

`["Access", "Erasure"]`, exactement les membres du `SmartEnum`. Le sidecar traduit **en interne** : le prompt reste en français avec ses slugs (`acces`, `hors-perimetre`), chaque moteur mappe vers le nom canonique juste avant de répondre.

Trois raisons de ne pas laisser passer les slugs français : un droit sur un fil JSON entre deux composants est un **identifiant**, pas un texte humain ; la contrainte du français est **locale au prompt**, la faire remonter jusqu'à `Core` la ferait payer à toute l'architecture, y compris au lexique qui n'a aucun prompt ; et cela **réduit** la surface de dérive au lieu de la déplacer — le garde-fou n'a plus qu'un ensemble de sept valeurs à vérifier, non une bijection entre deux alphabets.

### 5.2 Anti-dérive — un fichier de projection, gardé par un test

La taxonomie existe désormais des deux côtés de la frontière, et une divergence serait silencieuse.

**Un fichier JSON versionné à la racine liste les sept noms canoniques. Python le lit à l'import ; un test unitaire .NET assure qu'il correspond exactement aux sept membres du `SmartEnum`.**

- **La dérive devient unidirectionnelle.** Python n'a plus de copie, il lit la projection — il ne *peut* plus dériver. Un seul écart reste possible, `SmartEnum` contre fichier, et un seul test le garde.
- **Le garde-fou ne coûte rien à exécuter** : lire un fichier et comparer sept chaînes, sans container ni réseau. Un test de contrat contre un sidecar vivant n'existerait que quand Ollama tourne — donc pas sur la machine du développeur pressé, exactement quand on en a besoin.
- **La génération de code est disproportionnée** : la taxonomie est fermée par décision, un générateur ne s'amortirait sur rien — et il corrigerait la dérive *en silence*, là où un test la **nomme**.

**Partage des rôles.** `Core` reste la source de vérité du domaine — c'est le `SmartEnum` qui porte l'article et le libellé français. Le fichier n'est que la **projection sur le fil** : les sept noms, rien d'autre. **Un commentaire en tête de fichier doit dire lequel des deux commande**, sans quoi quelqu'un fera taire le test en éditant le fichier.

### 5.3 Confiance déclarée

**Le LLM rend une échelle ordinale à trois degrés : `High` / `Medium` / `Low`.**

Le contrat n'est pas la règle : la corroboration n'en lit qu'un bit (haute ou non), mais aplatir l'échelle à la frontière détruirait l'information avant qu'elle n'atteigne la trace d'audit — et c'est exactement la donnée dont a besoin la mesure de calibration restée ouverte (§ 13.1). Un **score numérique est refusé** : `0,73` rendu par un modèle de 8 milliards de paramètres n'est pas une probabilité, c'est un mot choisi qui ressemble à un nombre, et un nombre invite à poser des seuils, donc à régler des seuils sur rien. C'est enfin **l'échelle qui a été mesurée** au prototypage.

**Le lexique ne rend pas ce champ — il est absent, jamais une constante.** Une confiance constante serait un **mensonge typé** : elle donnerait au lexique l'apparence d'un avis sur sa propre fiabilité, et surtout elle serait *lisible* — quelqu'un finirait par écrire une règle qui la consomme, et l'indépendance du signal deviendrait le théâtre qu'on voulait éviter.

**Conséquence : `DeclaredConfidence` est optionnelle dans `QualificationOpinion`, et `Core` traite le cas absent.** Ce n'est pas un trou du modèle, c'est le domaine.

### 5.4 Le sidecar ne rend jamais un avis à moitié valide

Soit un `200` portant un avis qui satisfait **toutes** les invariantes du domaine — I1, I2, I3 du § 2.3, plus l'appartenance stricte aux sept valeurs de la projection —, soit un code non-2xx. **Aucun intermédiaire.**

Le schéma JSON ne peut pas tout exprimer : **l'exclusivité d'`OutOfScope` n'est pas exprimable** et doit être vérifiée par du code. Ce code vit **dans le sidecar**, pour la raison même qui a mis la corroboration dans `Core` : cette règle doit demander « les deux avis s'accordent-ils ? », jamais « cet avis est-il bien formé ? ». **Un avis invalide n'est pas un avis faible, c'est une panne du moteur**, et il se présente comme telle.

### 5.5 Aucune reprise, ni dans le sidecar, ni dans .NET

À `temperature: 0` avec seed fixe, rejouer le même texte rend **exactement la même sortie** — une reprise sur réponse malformée est littéralement une opération nulle. Il faudrait faire varier le seed pour qu'elle ait un sens, c'est-à-dire sacrifier le déterminisme. Et une reprise masquerait dans les métriques un problème de qualité du modèle qui doit rester visible. Depuis le repli lexical (§ 7.1), la reprise n'est plus seulement inutile : elle est **dominée**.

### 5.6 Codes de retour du sidecar

`POST /opinions/llm` :

| Code | Cas |
| --- | --- |
| `200` | avis valide |
| `400` | requête malformée — texte absent ou vide |
| `501` | ce déploiement ne sert **aucun** modèle : le moteur y est éteint, et rien n'est en panne |
| `502` | Ollama a répondu mais inexploitable : JSON illisible, valeur hors taxonomie, exclusivité violée |
| `503` | Ollama injoignable ou modèle non chargé |
| `504` | Ollama a dépassé le délai interne du sidecar |

**Le `501` est le seul de cette palette à ne nommer aucune panne.** Le moteur LLM est éteint par défaut (§ 9) : rien n'est à réparer, rien n'est à redémarrer, et rien n'est journalisé en erreur. Ni `404`, qui ferait chercher une faute de frappe dans l'URL, ni `503`, qui ferait redémarrer un serveur de modèles qu'on n'a jamais voulu.

`POST /opinions/lexicon` : **`200` et `400` seulement** (plus `500` sur bug). Le lexique n'a aucun amont — ni `502`, ni `503`, ni `504` ne peuvent survenir. **Cette asymétrie n'est pas un accident** : c'est ce que deux endpoints séparés achètent, et ce qui rend les comportements dégradés presque gratuits.

### 5.7 Forme concrète

```
POST /opinions/llm          POST /opinions/lexicon
Content-Type: application/json

{ "text": "…" }
```

Rien d'autre dans la requête : pas d'identifiant, la corrélation passe par l'en-tête **`traceparent`** que `ServiceDefaults` propage déjà via OpenTelemetry. Pas de langue non plus.

```jsonc
// 200 — /opinions/llm
{
  "rights": ["Access", "Erasure"],
  "confidence": "High",
  "justification": "Savoir ce qui est détenu puis tout supprimer : art. 15 puis art. 17.",
  "engine": { "name": "llm", "version": "qwen3:8b@sha256:…" }
}

// 200 — /opinions/lexicon
{
  "rights": ["Erasure"],
  "engine": { "name": "lexicon", "version": "1.0.0" }
}
```

**Un seul type, deux schémas qui le resserrent.** `QualificationOpinion` porte une confiance optionnelle ; le schéma de `/opinions/llm` la rend **obligatoire**, celui de `/opinions/lexicon` l'**interdit**. Deux types distincts feraient de `IQualificationEngine` un mensonge ; un schéma unique et permissif priverait la validation du § 5.4 de tout mordant côté LLM.

**La `justification` fait partie du contrat, du LLM seulement.** Le lexique n'en rend pas : sa « raison » est une table de scores, qui est du diagnostic, pas de l'aide à la décision.

**Chaque avis porte l'identité de son moteur : `engine { name, version }`.** C'est ce qui rend une régression interprétable — un taux de `Contested` qui grimpe n'a pas le même sens selon qu'on a changé de modèle ou non.

Erreurs en **`application/problem+json`**. Nommage `camelCase` sur le fil, valeurs de taxonomie en `PascalCase`.

### 5.8 Échéances et parallélisme

**Deux échéances imbriquées, celle du sidecar strictement plus courte.** Le sidecar porte sa propre échéance vers Ollama et rend `504` ; .NET porte la sienne vers le sidecar, plus longue. **La lenteur d'Ollama arrive ainsi à `Core` nommée**, au lieu d'apparaître comme une annulation muette. Valeurs : **120 s côté sidecar, 150 s côté .NET** — chiffres franchement arbitraires, à réviser le jour où `qwen3:8b` sera mesuré.

**`Core` appelle les deux moteurs en parallèle.** Ils sont indépendants par construction, le lexique coûte ~0,5 ms et ne touche pas le GPU : l'échéance globale est celle du seul appel LLM.

> **Contrainte d'implémentation, invisible depuis le contrat.** Les deux points d'entrée vivent dans le **même sidecar Python**. S'il sert ses requêtes de façon bloquante, un appel LLM en cours — jusqu'à 120 s — **affamerait** le point d'entrée du lexique, dont l'échéance de 5 s se déclencherait massivement, non parce que le lexique est lent mais parce qu'il attend derrière le LLM. L'entrecontrôle s'éteindrait précisément quand le service travaille. **Le sidecar doit donc servir ses deux points d'entrée concurremment**, et la suite `pytest` doit le vérifier.

---

## 6. La règle de corroboration et le `reviewSignal`

> Tranché par [#7](https://github.com/AmauryTISSOT/microservice_rgpd/issues/7), arbitrage précisé par [#16](https://github.com/AmauryTISSOT/microservice_rgpd/issues/16), définition publique par [#8](https://github.com/AmauryTISSOT/microservice_rgpd/issues/8)

### 6.1 Le verdict et la place du lexique

**Le verdict rendu est celui du LLM. Le lexique est un détecteur, jamais un contributeur** — en marche nominale. Deux raisons :

1. **L'union des deux avis n'est pas définissable.** `OutOfScope` étant exclusif, « `OutOfScope` ∪ `Erasure` » n'existe pas ; il faudrait une règle de priorité, qui retombe sur « le LLM tranche ».
2. **L'union importerait le bruit du moteur faible.** Laisser le lexique ajouter des droits dégrade la précision plus qu'elle n'améliore le rappel.

**Cette règle est révisée, pas abandonnée, par le repli lexical** (§ 7.1) : le lexique devient contributeur **de dernier recours** quand le moteur principal est absent. **Les deux rôles ne coexistent jamais dans une même réponse.**

### 6.2 La règle, dans `Core`

Entrées : l'avis LLM (peut être absent), l'avis lexical (peut être absent), la `DeclaredConfidence` du LLM (optionnelle par § 5.3).

**Ordre d'évaluation : divergence d'abord, confiance ensuite.**

| Condition | `ReviewSignal` |
| --- | --- |
| les deux avis présents **et** ensembles de droits différents | **`Contested`** |
| les deux avis présents, ensembles identiques, confiance **`High`** | `Corroborated` |
| les deux avis présents, ensembles identiques, confiance non haute ou absente | `NeedsReview` |
| un seul avis présent (mode dégradé, § 7) | `NeedsReview` |

> **`Contested` l'emporte sur `NeedsReview`** quand les deux conditions sont réunies — divergence *et* confiance basse, cas courant et non cas d'école. C'est l'argument central de l'entrecontrôle : quand les deux signaux parlent, on écoute celui **qui ne dépend pas de l'auto-évaluation** d'un petit modèle réputé sur-confiant.

La comparaison des ensembles de droits est une **égalité d'ensembles**, insensible à l'ordre.

### 6.3 Sens public du signal

| Valeur | Sens **public** |
| --- | --- |
| `Corroborated` | le verdict a reçu un contrôle indépendant favorable **et** la confiance était haute |
| `NeedsReview` | ni l'un ni l'autre pleinement |
| `Contested` | le contrôle indépendant est en désaccord |

**La définition est sémantique, jamais mécanique** — et c'est ce qui fait tenir la décision. Elle ne parle ni de LLM, ni de lexique, ni de deux moteurs : un troisième moteur, ou l'abandon du lexique, laisse le contrat public intact. Elle a deux corollaires directs :

- Un **lexique injoignable ne remplit pas la condition de `Corroborated`**, donc `NeedsReview` le dit exactement, sans mentir.
- **Aucun mode dégradé ne peut se présenter comme `Corroborated`.** C'est une contrainte de contrat, pas une consigne d'implémentation.

**Le signal est toujours présent.** Un signal de tri facultatif ne trie plus : toute sa valeur est qu'un appelant puisse écrire `if (reviewSignal != "Corroborated")`. Le rendre optionnel obligerait chaque consommateur à traiter l'absence — dont le traitement prudent *est* `NeedsReview`. Une quatrième valeur `Uncorroborated` est écartée pour la raison symétrique : « la confiance est basse » et « la corroboration n'a pas eu lieu » appellent la **même action**, regarder ; une valeur d'échelle se paie chez tous les consommateurs et doit acheter une décision différente.

**Le signal priorise la relecture, il ne la déclenche pas** — un humain valide chaque qualification de toute façon. Un faux positif y coûte donc peu, ce qui autorise le taux de déclenchement de ~25 % extrapolé au prototypage.

> ⚠️ **Ce taux n'a jamais été mesuré sur `qwen3:8b`.** Le contrat public promet ici un signal **non calibré** — risque assumé au titre du POC (§ 13.2).

---

## 7. Comportements dégradés

> Tranché par [Définir les comportements dégradés de la qualification](https://github.com/AmauryTISSOT/microservice_rgpd/issues/10)

**Le service ne refuse presque jamais. Il rend un verdict, et il dit quand il l'a rendu diminué.**

### 7.1 Le repli lexical

**Quand le LLM ne rend pas d'avis, le lexique produit le verdict, et l'appelant en est informé.**

Trois faits l'emportent sur la règle « détecteur, jamais contributeur » :

- **L'avis lexical est déjà en main, gratuitement** — les appels sont parallèles et le lexique coûte ~0,5 ms. Au moment où le LLM échoue, à la première seconde comme à la 120ᵉ, le verdict de repli est disponible depuis longtemps. Refuser de s'en servir, c'est jeter une réponse qu'on détient pour rendre une erreur.
- **Le contrat public avait déjà écrit ce scénario** — l'optionalité de `justification` est justifiée mot pour mot par « si le LLM tombe et que seul le lexique répond ».
- **Un humain valide de toute façon.** Un verdict lexical signalé entre dans la file de validation d'un opérateur averti ; un `503` l'oblige à revenir plus tard sans rien avoir appris.

> **Le coût est réel et nommé.** Le lexique commet des erreurs **silencieuses**, faute de tout signal de confiance, et produit des faux rejets que le LLM ne produit pas. **Le mode de repli est le mode où le service se trompe le plus, et le moins visiblement.** C'est ce qui rend le champ `degraded` non négociable : sans lui, le repli serait indéfendable.

**Le repli couvre les trois modes de panne du LLM — `502`, `503` et `504`.** Sur le `502` : un avis invalide *est* une panne du moteur, le traiter à part le requalifierait en avis faible par la porte de derrière. Sur le `504` : la latence n'est pas un critère de rejet, et un appelant qui a déjà attendu deux minutes préfère un verdict imparfait et signalé à un `504` qui l'oblige à tout rejouer — sachant qu'un rejeu redonnera le même dépassement.

### 7.2 Le champ `degraded`

**Un booléen `degraded`, toujours présent dans le corps, `false` en marche normale.**

- **Un champ, et non une valeur de `reviewSignal`** : il y a **deux axes, pas un**. `reviewSignal` répond à « avec quelle attention dois-je relire ce verdict ? » ; `degraded` répond à « le service était-il entier quand il l'a produit ? ». Les fondre forcerait l'appelant à perdre l'un pour lire l'autre. S'y ajoute qu'ajouter une valeur à `reviewSignal` est une **rupture** (§ 4.6), là où ajouter un champ est rétro-compatible.
- **Dans le corps, pas ailleurs.** Un `203 Non-Authoritative Information` serait lu comme un `2xx` par toute bibliothèque testant `IsSuccessStatusCode`, et l'en-tête `Warning` est déprécié depuis la RFC 9111. Une information que le contrat juge digne d'être émise ne doit pas voyager dans le canal que personne ne lit.
- **Toujours présent.** Un champ facultatif obligerait chaque consommateur à traiter **trois** états — `true`, `false`, absent — alors que le domaine n'en a que deux. Côté client typé, c'est un `bool`, non un `bool?`.

**`degraded` signifie « le service n'était pas entier ».** Il se lève donc **aussi quand le lexique manque** alors que le LLM répond. Le champ ne dit pas *quelle* pièce manquait — cette distinction reste interne, portée par la télémétrie et par la trace d'audit (§ 8.4).

> **Objection enregistrée.** Un même booléen recouvre deux situations aux consignes contraires : « méfie-toi du contenu de ce verdict » en repli lexical, « ce verdict est de qualité normale, simplement non recoupé » en lexique absent. Une énumération les aurait séparées. Le booléen est préféré pour sa simplicité, et le gain est réel : **un lexique mort ne s'éteint plus en silence**.

> **Conséquence désagréable et cohérente** : en repli lexical, `degraded: true` s'accompagne d'**aucune `justification`**. L'opérateur reçoit un verdict **muet** au moment précis où il aurait le plus besoin de lire quelque chose. Lui en fabriquer une serait lui mentir.

### 7.3 Ce que voit l'appelant, cas par cas

| Situation | Statut | `rights` | `reviewSignal` | `degraded` | `justification` |
| --- | --- | --- | --- | --- | --- |
| Accord des deux moteurs, confiance haute | `200` | LLM | `Corroborated` | `false` | présente |
| Confiance non haute, pas de divergence | `200` | LLM | `NeedsReview` | `false` | présente |
| Divergence des deux moteurs | `200` | LLM | `Contested` | `false` | présente |
| **LLM en panne (`502`/`503`/`504`), lexique répond** | `200` | **lexique** | `NeedsReview` | **`true`** | **absente** |
| **Lexique absent (échéance ou `500`), LLM répond** | `200` | LLM | `NeedsReview` | **`true`** | présente |
| Les deux en panne, LLM en délai dépassé | `504` | `problem+json` + `traceId` | — | — | — |
| Les deux en panne, autre cause | `503` | `problem+json` + `traceId` | — | — | — |
| `text` absent, vide, > 10 000 car. ; `callerReference` invalide | `400` | `problem+json` + `traceId` | — | — | — |
| **Échec d'écriture de la trace d'audit** | `500` | `problem+json` + `traceId` | — | — | — |
| Appelant déconnecté | aucune réponse, appel abandonné | — | — | — | — |

**Deux lignes sont dérivées, non décidées.** En repli lexical, `Corroborated` est **interdit** par le contrat public et `Contested` est **impossible** faute d'un second avis avec lequel diverger : `NeedsReview` est donc **forcé**.

**Double panne : `504` si et seulement si le LLM a dépassé l'échéance, `503` sinon.** Le code public suit le mode de panne du **moteur principal**, dont l'absence explique qu'aucun verdict n'ait pu être rendu ; celle du lexique n'a fait que priver le service de son filet.

**Frontière avec la persistance.** Ce chapitre traite la défaillance des **moteurs**, jamais celle de la persistance. Corollaire à écrire dans le code : **un repli lexical dont la trace ne s'écrit pas rend `500`, pas `200 degraded`** — la dégradation du moteur ne survit pas à une panne de base.

### 7.4 `503` et `504` publics sont quasi inatteignables

Ils exigent une double panne dont la seconde branche ne peut venir que d'un bogue du lexique ou de la famine du sidecar (§ 5.8). **Rien dans l'usage normal ne les atteindra jamais** — ces branches doivent donc être **testées délibérément** (§ 10.2), et la documentation d'API doit porter la phrase du § 4.5.

### 7.5 Résilience — le gestionnaire standard est écarté

**Le fait dans le dépôt.** `src/MicroserviceRgpd.ServiceDefaults/Extensions.cs` applique `AddStandardResilienceHandler()` à **tous** les `HttpClient`. Ses valeurs d'usine contredisent le contrat interne sur trois points : délai total **30 s** (contre 150 s), délai par tentative **10 s**, et **3 reprises** avec attente exponentielle (contre aucune).

Laissé tel quel, le service abandonnerait Ollama au bout de 30 s après l'avoir rejoué trois fois, et le `504` construit pour arriver **nommé** ne serait jamais émis, l'échéance du sidecar (120 s) étant quatre fois plus longue que celle du client qui l'appelle. Pire, sur un GPU qui **sérialise**, chaque rejeu lance une génération supplémentaire : une seule requête entrante en met quatre à la queue leu leu, dont trois sont abandonnées sans être lues.

**Décision : un pipeline de résilience explicite sur les deux clients de moteur**, en remplacement du gestionnaire hérité. Le défaut global de `ServiceDefaults` **reste en place** pour tout autre client — le retirer priverait silencieusement de résilience tout client créé plus tard.

| Réglage | Client LLM | Client lexique |
| --- | --- | --- |
| Reprises | **aucune** | **aucune** |
| Délai total | **150 s** | **5 s** |
| Délai par tentative | sans objet | sans objet |
| Disjoncteur | **aucun** | **aucun** |

- **Aucune reprise** : dominée par le repli (§ 5.5, § 7.1).
- **150 s côté LLM**, strictement plus long que les 120 s du sidecar, conformément à l'ordre du § 5.8.
- **5 s côté lexique** — chiffre arbitraire et assumé. Au-delà, l'avis est traité comme **absent**. La propriété recherchée : le lexique, franchement plus rapide et franchement plus court en échéance, **ne peut jamais rallonger le temps de réponse du service**.
- **Pas de disjoncteur en v1.** Il n'achèterait que de la latence pendant une panne *lente* d'Ollama — or la latence n'est pas un critère de rejet, et sans lui le comportement reste **juste**, seulement lent. C'est de surcroît un seuil, et personne n'a mesuré la fréquence ni la forme des échecs d'Ollama ; sa forme standard exige un débit minimal qu'un POC à appelant unique n'atteindra jamais. Il rendrait enfin le service **non reproductible du point de vue de l'appelant** et creuserait l'ambiguïté de `degraded`, qui recouvrirait aussi « on n'a même pas essayé ». Reporté (§ 13.1).

**L'annulation de l'appelant est propagée** jusqu'au sidecar. La raison est matérielle : le GPU sérialise, donc une génération poursuivie pour un appelant parti ne gaspille pas seulement, elle **bloque la file**. Techniquement le coût est nul — c'est le comportement d'ASP.NET Core dès lors que le jeton d'annulation atteint les appels sortants — mais ne pas l'écrire donnerait ce résultat par accident, ou son contraire par étourderie. **Une annulation ne laisse aucune trace d'audit**, cohérent avec « la trace enregistre l'acte d'avoir qualifié ».

### 7.6 Les cas de contenu : il n'y a rien à détecter

**Aucun classifieur en amont du classifieur.**

**Pas de détection de langue.** Placer un détecteur non mesuré et sans témoin devant un moteur choisi, mesuré et contrôlé, c'est protéger le mesuré par le non mesuré — et ses erreurs seraient **définitives**, un texte rejeté ne rencontrant jamais le moteur qui aurait su le qualifier. Les détecteurs de langue sont de surcroît instables sous une vingtaine de caractères : ils **rétabliraient par la porte de derrière le plancher de longueur que le § 4.2 a refusé**. Ce que produit l'absence de détection est heureux : sur un texte anglais, le LLM — multilingue — reconnaîtra probablement le droit tandis que le lexique, bâti sur des mots-clés français, rendra `OutOfScope` ; les avis divergent, le verdict sort en **`Contested`**, et le texte étranger se signale seul. *Deux coûts assumés* : le comportement de `qwen3:8b` hors français n'a jamais été mesuré, et « français uniquement » devient une note de documentation, non une règle exécutée.

**Pas de détection de bruit.** **`OutOfScope` est la réponse au bruit.** Un `400` reviendrait à dire qu'on n'a pas su qualifier un texte qu'on vient de qualifier correctement.

*Le bruit peut être adverse* — un texte cherchant à détourner le prompt. À écrire dans la doc pour que la question ne revienne pas sous forme d'inquiétude sans réponse : le sidecar validant taxonomie, non-vacuité et exclusivité (§ 5.4), le pire résultat atteignable est **un verdict faux parmi sept valeurs** — ni exécution arbitraire, ni fuite ; le lexique, insensible à toute injection, divergera et le verdict sortira en `Contested` ; et un humain valide. L'authentification étant hors périmètre et le réseau supposé de confiance, rien de plus n'est fait en v1.

**Aucun seuil de confiance.** La prémisse est dissoute : l'échelle est ordinale, la règle n'en lit qu'un bit, le contrat public interdit de l'exposer. Remplacer un verdict peu assuré par `OutOfScope` **détruirait le sens de cette valeur** — le glossaire lui interdit explicitement d'être « inconnu, non classé, None, Unknown » —, et l'opérateur lirait un verdict **affirmatif et faux** au lieu d'un verdict incertain et signalé. Rendre une erreur serait pire : les erreurs du LLM portent une confiance non haute, ce qui les rend **captables par relecture** ; les transformer en pannes reviendrait à jeter précisément les qualifications que le dispositif a été bâti pour attraper.

**La liste vide est inexprimable** — I1 du § 2.3, validée par le sidecar.

---

## 8. Trace d'audit

> Tranché par [Définir ce que conserve la trace d'audit et sa rétention](https://github.com/AmauryTISSOT/microservice_rgpd/issues/9)

### 8.1 Finalité

**Redevabilité technique seule, le débogage en effet de bord borné.**

**L'amélioration du moteur est écartée** : conserver des textes réels pour mesurer ou entraîner serait une finalité *propre*, qui ferait basculer le service de sous-traitant à **responsable de traitement** — base légale propre, information des personnes, art. 28.3.

> **Conséquence assumée : jamais de requalification de demandes réelles lors d'une montée de version du modèle.** La non-régression reste adossée au **corpus synthétique** de `corpus/demandes-rgpd.fr.jsonl`.

### 8.2 Ce qui est conservé, et pendant combien de temps

**Le texte est conservé intégral, en clair. Aucune rétention : ni durée déclarée, ni purge.**

Le chemin explique la forme. Une **empreinte HMAC-SHA256 à clé** avec texte en clair éphémère avait d'abord été retenue — à clé, et non un SHA-256 nu, que le corpus (`🙂` seul, `Mes données ?`) rend cassable au dictionnaire en secondes. La décision de ne pas implémenter de purge en POC l'a vidée de son sens : sans purge, l'empreinte cohabite indéfiniment avec l'original qu'elle devait remplacer. **Elle est retirée**, avec `TextLength`. Le texte tronqué avait été écarté d'emblée : toujours de la donnée en clair, mais amputée — inexploitable pour déboguer, insuffisante pour prouver.

> ⚠️ **Ce qui rend cela défendable est nommé : en POC, aucune donnée réelle ne transite.**
> **Rétention, purge et minimisation forment une condition de sortie de POC** — le service ne doit pas recevoir de donnée réelle avant que ce bloc ne soit traité (§ 13.1).

### 8.3 Les champs

| Colonne | Type PostgreSQL | Note |
| --- | --- | --- |
| `qualification_id` | `uuid` | **clé primaire**, GUID v7 généré par le code |
| `occurred_at` | `timestamptz` | UTC |
| `text` | `varchar(10000)` | miroir du plafond du § 4.2 |
| `rights` | `text[]` | **non nullable** — le verdict rendu |
| `review_signal` | `text` | par son **nom**, jamais un entier |
| `verdict_rights` | `text[]` | **nullable** |
| `verdict_declared_confidence` | `text` | **nullable**, par son nom |
| `verdict_engine_name` | `text` | **nullable** |
| `verdict_engine_version` | `text` | **nullable** |
| `witness_rights` | `text[]` | **nullable** |
| `witness_declared_confidence` | `text` | **nullable**, par son nom |
| `witness_engine_name` | `text` | **nullable** |
| `witness_engine_version` | `text` | **nullable** |
| `justification` | `text` | **nullable** |
| `caller_reference` | `varchar(64)` | **nullable** |
| `trace_id` | `varchar(64)` | **nullable** |
| `total_latency_ms` | `integer` | |
| `verdict_latency_ms` | `integer` | **nullable** |
| `witness_latency_ms` | `integer` | **nullable** |

Trois de ces choix portent plus que de la technique :

- **Les deux avis bruts sont stockés, alors que le contrat public les refuse à l'appelant.** La logique s'inverse parce que la trace est **interne** : les avis sont ce qui *explique* le `review_signal` ; sans eux la trace enregistrerait une conclusion sans ses prémisses.
- **Nom et version des deux moteurs figurent sur chaque ligne.** La trace permet donc de savoir quelles qualifications relèvent de quelle version de `qwen3:8b` ou du lexique.
- **Les colonnes d'avis nomment un rôle, jamais une technologie** — révision de la première rédaction, qui les nommait `llm_*` et `lexicon_*`. Le rôle tenu par chaque moteur est une décision d'enregistrement de services ([#26](https://github.com/AmauryTISSOT/microservice_rgpd/issues/26)) : un nommage par technologie ferait mentir la table le jour où les rôles s'échangent. Rien n'est perdu, puisque `verdict_engine_name` et `witness_engine_name` disent sur chaque ligne *qui* a parlé.
- **`caller_reference` est conservée en clair.** C'est la clé de corrélation de l'appelant ; sans elle il ne retrouve pas son propre appel et la redevabilité tombe. Mais elle est *opaque* : rien n'empêche l'application tierce d'y placer un identifiant de personne. **Contrainte de documentation, explicitement inapplicable techniquement** (§ 4.2) — elle pèse sur le responsable de traitement.

La `justification` paraphrase le texte en le citant ; elle avait été rangée en éphémère avec lui. Sans purge, la question tombe : elle est conservée comme le reste.

### 8.4 La nullité *est* l'enregistrement du mode dégradé

**Aucun champ « mode dégradé » n'existe dans le schéma**, et c'est délibéré : la nullité des colonnes d'avis suffit et **dit davantage que le booléen public**.

| Configuration | Ce qu'elle signe |
| --- | --- |
| `verdict_*` renseigné, `witness_*` renseigné | marche nominale |
| `verdict_*` **nul**, `witness_*` renseigné | **repli lexical** |
| `verdict_*` renseigné, `witness_*` **nul** | **lexique absent** |

C'est exactement la distinction que le booléen `degraded` abandonne (§ 7.2) : elle existe, au bon endroit — **à l'intérieur**.

### 8.5 Le port, l'entité, la table

**Port `IQualificationAuditTrail` en `Core`, une seule méthode, écriture seule.**

**L'entité EF Core est confinée à `Infrastructure` et n'est jamais marquée `IAggregateRoot`.** `EfRepository<T>` du gabarit est contraint à `where T : class, IAggregateRoot` : passer par le dépôt générique aurait déclaré la trace **agrégat racine**, exactement ce que la carte (« pas d'agrégat métier de demande ») et le refus du `201 Created` ont chassé. Un port d'écriture sans lecture dit la vérité du domaine : **il n'existe aucun `GET`, la trace est inatteignable en lecture**, et un dépôt offrirait des capacités de requête que rien n'utilise. `EfRepository` et `IAggregateRoot` restent intacts pour un vrai agrégat le jour où il en apparaît un. **Le commentaire d'`AppDbContext` (« déclarer ici un `DbSet` par agrégat ») est à reformuler**, plutôt que le modèle à tordre.

**La table.** Unique, **`qualification_audit_entries`**.

- Nommage `snake_case` **explicite dans un `IEntityTypeConfiguration`, sans ajouter `EFCore.NamingConventions`** — pour une seule table, un fichier vérifiable et local vaut mieux qu'une dépendance dont la compatibilité EF Core 10 n'est pas établie.
- Les trois ensembles de droits en **`text[]` portant les noms canoniques anglais** du fichier de projection (§ 5.2) : **la base n'introduit aucun troisième vocabulaire**. Table fille écartée (surdimensionnée pour un non-agrégat), `jsonb` écarté (moins typé, sans gain), chaîne jointe écartée (toute requête deviendrait textuelle).
- `review_signal` et `llm_declared_confidence` stockés **par leur nom, jamais par un entier** — un entier rendrait la table illisible et la lierait à l'ordre de déclaration.
- `text` en **`varchar(10000)`**, miroir du plafond public : en PostgreSQL cela ne coûte rien de plus que `text`, et la base cesse d'accepter ce que le contrat interdit — au prix d'une migration si le plafond bouge.
- **Aucun index secondaire** : rien ne lit cette table, la purge est écartée, et un index sur `occurred_at` coûterait à chaque écriture sur le chemin synchrone.
- **Cette fonctionnalité produit la première migration du dépôt** — `Migrations/` n'existe pas encore, et elle ne contiendra que cette table.

### 8.6 Écriture synchrone, échec → `500`

**Ordre : qualifier, écrire, répondre.** On ne trace pas un verdict qu'on n'a pas, et la latence n'est pas un critère de rejet.

Sur échec d'écriture, l'option « répondre `200` quand même » est écartée : le `qualificationId` est **défini comme la clé de la trace**, et rendre un identifiant creux ferait mentir le contrat **précisément dans le cas où la trace sert** — une fois qu'un `qualificationId` peut être vide, aucun ne prouve plus rien, faute de pouvoir distinguer les deux. L'argument adverse (aide à la décision, une erreur coûte peu) est rejeté parce qu'**une base indisponible est une panne du service, pas une qualification dégradée** — ce qui garde nette la frontière du § 7.3.

### 8.7 La trace enregistre les verdicts, jamais les tentatives

`rights` étant **non nullable**, aucune ligne n'est possible pour un appel sans verdict : un `503`, un `504` ou une annulation **ne laisse aucune ligne**.

**Décision : pas d'autre artefact en v1. Le trou est assumé.** La finalité est la redevabilité, et la trace est l'écrit de « l'acte de l'avoir qualifiée » ; une double panne n'a rien qualifié, il n'y a aucun verdict dont répondre. Enregistrer les tentatives ferait de l'audit un **registre de non-événements**. Ce qui manque après ce refus n'est pas une lacune d'audit mais une lacune d'**exploitation** — savoir combien de fois le service a échoué —, et cette matière a déjà son canal : OpenTelemetry est configuré par `ServiceDefaults` et les deux appels sortants sont instrumentés.

> **Deux limites nommées.** Un `503` reste **invisible à l'audit** : un appelant contestant « je vous ai envoyé un texte, vous n'en avez aucune trace » a raison, et le service ne peut lui opposer que sa télémétrie — dont la rétention et l'échantillonnage lui échappent. Une annulation ne laisse rien non plus. Acceptable en POC parce qu'aucune obligation de conservation ne porte sur des demandes **jamais qualifiées** — **à réexaminer avec le bloc « rétention, purge et minimisation »** (§ 13.1).

---

## 9. Configuration

Tous les chiffres ci-dessous sont **arbitraires et assumés comme tels**, à réviser le jour où `qwen3:8b` sera mesuré. Ils doivent vivre en configuration, non en dur.

| Réglage | Valeur | Où | § |
| --- | --- | --- | --- |
| Existence du moteur LLM | **éteint par défaut** — `Llm:Enabled` côté .NET, `QUALIFICATION_LLM_ENABLED` côté sidecar | `Web` / `Infrastructure`, et sidecar | 5.6 |
| Modèle local | `qwen3:8b` | Ollama / sidecar | 3 |
| `temperature` | `0`, seed fixe | sidecar | 5.5 |
| `base_url` du client compatible OpenAI | Ollama | sidecar — **point de bascule vers Mistral** | 3 |
| Échéance sidecar → Ollama | **120 s** | sidecar | 5.8 |
| Échéance .NET → `/opinions/llm` | **150 s** | `Infrastructure` | 5.8, 7.5 |
| Échéance .NET → `/opinions/lexicon` | **5 s** | `Infrastructure` | 7.5 |
| Reprises sur les deux clients | **0** | `Infrastructure` | 5.5, 7.5 |
| Disjoncteur | **aucun** | — | 7.5 |
| Longueur maximale de `text` | **10 000** caractères | `Web` + base | 4.2, 8.5 |
| Taille maximale du corps | **64 Ko** | Kestrel | 4.2 |
| Longueur maximale de `callerReference` | **64** caractères | `Web` + base | 4.2 |

**`AppHost`.** Le sidecar Python se déclare comme Postgres l'est déjà, via `Aspire.Hosting.Python` **13.4.6** — exactement la version des autres paquets Aspire du dépôt. **Le paquet n'est pas encore dans `Directory.Packages.props`** : il est à ajouter. Le container Ollama y entre également, avec le volume de modèles nécessaire.

---

## 10. Stratégie de test et barre de qualité

> Tranché par [Fixer la barre de qualité et la stratégie de test](https://github.com/AmauryTISSOT/microservice_rgpd/issues/11)

### 10.1 Ce que la barre couvre — et ce qu'elle ne couvre pas

**La barre porte sur le code, pas sur la qualification.**

> ⚠️ **Cette spec sort sans aucun critère disant si la qualification est bonne.** La métrique qui fait foi, le seuil d'acceptation et le taux de faux positifs toléré sur le hors périmètre sont **explicitement refusés** : le seul chiffre disponible (96,7 % de correspondance exacte au prototypage) est celui d'**Opus 5, pas de `qwen3:8b`** — un seuil posé maintenant serait un seuil emprunté à un autre moteur. La question part au § 13.1, **sans propriétaire**.

### 10.2 La frontière du moteur, et le découpage des projets

**Aucun test de la suite .NET n'appelle Ollama, jamais.** La doublure se pose sur **`IQualificationEngine` avec NSubstitute** — pas sur le fil HTTP, pas sur des réponses enregistrées, pas sur un vrai sidecar marqué et exclu. Les enregistrements pourrissent en silence et rendent indiscernable une régression de code d'une montée de modèle ; un test qui exige un GPU en CI ne tourne jamais, **et un test qui ne tourne jamais ment**.

| Projet | Rôle | Changement à faire |
| --- | --- | --- |
| **`UnitTests`** | `Core`, `UseCases`, **et les adaptateurs HTTP du sidecar** avec un `HttpMessageHandler` moqué | **ajouter une référence à `Infrastructure`** (absente aujourd'hui : le projet ne référence que `Core` et `UseCases`) |
| **`IntegrationTests`** | **un seul rôle : la persistance de la trace d'audit**, sur un vrai PostgreSQL en Testcontainer | **supprimer `Microsoft.EntityFrameworkCore.InMemory`** |
| **`FunctionalTests`** | l'endpoint public de bout en bout, moteur substitué | rien — tourne déjà sur PostgreSQL 18 via Testcontainers avec `Migrate()` |
| **`AspireTests`** | — | **à vider** |

- **`UnitTests` est l'endroit où la palette asymétrique du § 5.6 est vérifiée** : réponse conforme, taxonomie inconnue, `rights` vide, `OutOfScope` accompagné, chacun des `400`/`502`/`503`/`504`, dépassement d'échéance. **Sans cela, les comportements dégradés du § 7 ne seraient déclenchés par aucun test.** C'est aussi là que les branches `503`/`504` publiques, inatteignables en usage normal (§ 7.4), sont atteintes délibérément.
- **Ce qui sauve `IntegrationTests` de la suppression** est une conséquence du contrat public : **sans `GET`, la trace est inatteignable depuis l'endpoint**, donc un test fonctionnel ne peut pas la voir. Et `InMemory` ne partage ni les types, ni les contraintes, ni `text[]`, ni la génération de clés de Npgsql — précisément ce que le § 8.5 décide.
- **`AspireTests` est à vider** : il ne contient aucun test aujourd'hui, et deviendra un **piège** dès que le sidecar et Ollama entreront dans l'`AppHost` — tout test démarrant l'`AppHost` démarrerait Ollama, en contradiction frontale avec la frontière ci-dessus. Une éventuelle vérification d'orchestration complète sera un **dispositif manuel séparé**, et une décision à part.

*Note de ménage :* `TESTCONTAINERS_IMPLEMENTATION.md` est périmé — il décrit SQL Server.

### 10.3 Côté Python

Suite **`pytest`** propre au sidecar, couvrant quatre choses :

1. **Les règles de refus du § 5.4** — taxonomie inconnue, `rights` vide, `OutOfScope` accompagné produisent un non-2xx.
2. **Le fichier JSON de projection vu du côté Python** — les sept noms lus à l'import sont ceux que le moteur peut émettre. Symétrique du test unitaire .NET du § 5.2.
3. **Le lexique rejoué sur le corpus, en fichier témoin.** Le lexique est déterministe : sa sortie est figée exemple par exemple et toute divergence est une alerte. **Ce n'est pas une mesure de qualité de qualification** — aucun seuil, aucun F2, aucune métrique — c'est de la non-régression de code, qui dit « ce commit a changé le comportement du lexique sur N exemples, était-ce voulu ? ». C'est le seul moteur entièrement testable sans GPU ; le laisser hors tests en ferait un angle mort complet.
4. **La concurrence des deux points d'entrée** — la contrainte du § 5.8 : un appel LLM en cours ne doit pas affamer `/opinions/lexicon`.

### 10.4 La porte, et la méthode

**Pas de CI.** Une **commande unique documentée** — `dotnet test` sur la solution plus `pytest` sur le sidecar — à passer au vert avant d'ouvrir une PR, consignée dans le `README` ou `CONTEXT.md`. **Pas de hook git** : les tests à container coûtent une dizaine de secondes de démarrage, un `pre-commit` qui les lance sera désactivé dans la semaine, et **un garde-fou désactivé est pire qu'absent** — il donne l'illusion d'une protection.

**Méthode : TDD strict**, rouge-vert-refactor, via `/tdd`.

> **Réserve consignée** : le TDD garantit qu'un test précède chaque comportement écrit, **pas qu'on pense aux invariants posés ailleurs**. D'où le § 11.

---

## 11. Les invariants transverses

Ce sont les invariants qui **traversent la carte** : chacun a été posé par un ticket et doit être vérifié par du code écrit dans un autre. Aucun n'est garanti par le TDD seul. **Cette liste est la contribution propre de ce document** — elle n'existe nulle part ailleurs.

| # | Invariant | Posé par | Vérifié où |
| --- | --- | --- | --- |
| **T1** | `rights` n'est **jamais vide** | § 2.3 (I1) | `UnitTests` (`Core` + adaptateurs), `pytest` |
| **T2** | **`OutOfScope` est exclusif** — jamais combiné | § 2.3 (I2) | `UnitTests`, `pytest` (règle de refus) |
| **T3** | Le **lexique ne contribue jamais au verdict** en marche nominale — et jamais détecteur et contributeur dans la même réponse | § 6.1, § 7.1 | `UnitTests` sur la règle de corroboration |
| **T4** | **`Contested` l'emporte sur `NeedsReview`** — divergence évaluée avant confiance | § 6.2 | `UnitTests` sur la règle |
| **T5** | **Aucun mode dégradé ne rend `Corroborated`** | § 6.3, § 7.3 | `UnitTests` + `FunctionalTests` |
| **T6** | En repli lexical, **`NeedsReview` forcé et aucune `justification`** | § 7.3 | `UnitTests` + `FunctionalTests` |
| **T7** | **`degraded` est toujours présent** dans le corps `200` | § 7.2 | `FunctionalTests` |
| **T8** | **`reviewSignal` est toujours présent** dans le corps `200` | § 6.3 | `FunctionalTests` |
| **T9** | **`🙂` seul → `200` avec `OutOfScope`, jamais `400`** — aucun plancher de longueur | § 4.2 | `FunctionalTests` |
| **T10** | **`504` ssi le LLM a dépassé l'échéance** ; `503` pour toute autre double panne | § 7.3 | `UnitTests` (branches délibérées) |
| **T11** | Le **`SmartEnum` et le fichier de projection coïncident** exactement | § 5.2 | `UnitTests` .NET **et** `pytest` |
| **T12** | Un **échec d'écriture de la trace rend `500`**, jamais `200 degraded` | § 7.3, § 8.6 | `IntegrationTests` + `FunctionalTests` |
| **T13** | Le **sidecar sert ses deux points d'entrée concurremment** | § 5.8 | `pytest` |
| **T14** | Le **pipeline de résilience explicite remplace bien `AddStandardResilienceHandler()`** sur les deux clients de moteur — 0 reprise, 150 s / 5 s | § 7.5 | `UnitTests` sur les adaptateurs |
| **T15** | L'**annulation de l'appelant est propagée** jusqu'au sidecar, et ne laisse aucune trace | § 7.5, § 8.7 | `UnitTests` + `FunctionalTests` |
| **T16** | La trace **n'est jamais `IAggregateRoot`** et ne passe jamais par `EfRepository<T>` | § 8.5 | revue + `IntegrationTests` |
| **T17** | La base **n'introduit aucun troisième vocabulaire** : `text[]` aux noms canoniques | § 8.5 | `IntegrationTests` |

---

## 12. Plan d'implémentation

Ordre proposé, chaque étape laissant la solution compilable et verte. **TDD strict** à chaque étape.

1. **Le domaine, sans dépendance.** `DataSubjectRight` (`SmartEnum`, 7 membres, article + libellé), `RightsRequestText` (Vogen), `Qualification`, `QualificationOpinion`, `ReviewSignal`, les convertisseurs JSON et EF Core. → T1, T2.
2. **Le fichier de projection** à la racine + le test unitaire .NET qui le confronte au `SmartEnum`, commentaire d'autorité en tête. → T11.
3. **La règle de corroboration** dans `Core`, plus `IQualificationEngine`. Testable sans réseau. → T3, T4, T5, T6.
4. **Le sidecar Python** : les deux endpoints, la validation du § 5.4, les tables de correspondance slugs → noms canoniques, la lecture de la projection, le service concurrent. Suite `pytest`, dont le fichier témoin du lexique sur le corpus. → T1, T2, T11, T13.
5. **Les adaptateurs HTTP** dans `Infrastructure`, avec le pipeline de résilience explicite et la propagation d'annulation. `UnitTests` gagne sa référence à `Infrastructure` ; toute la palette `400`/`502`/`503`/`504` y est couverte. → T10, T14, T15.
6. **La trace d'audit** : `IQualificationAuditTrail` en `Core`, entité + `IEntityTypeConfiguration` en `Infrastructure`, **première migration du dépôt**. `IntegrationTests` perd `InMemory`. → T12, T16, T17.
7. **Le use case** `Qualifications/Qualify` : appels parallèles, corroboration, repli, écriture de la trace, `Result<...>`.
8. **L'endpoint** `POST /qualifications` : validation d'entrée, `Errors.UseProblemDetails()`, mapping Ardalis.Result **plus la branche explicite du `504`**, borne de corps à 64 Ko. `FunctionalTests` de bout en bout. → T7, T8, T9.
9. **L'`AppHost`** : ajout d'`Aspire.Hosting.Python` à `Directory.Packages.props`, déclaration du sidecar et du container Ollama. **Vider `AspireTests`.**
10. **La documentation** : commande de test unique, phrases obligatoires de la doc d'API (§ 4.2 idempotence, § 4.2 donnée personnelle dans `callerReference`, § 4.5 rareté des `503`/`504`, § 7.6 bruit adverse), reformulation du commentaire d'`AppDbContext`, mise à jour de `TESTCONTAINERS_IMPLEMENTATION.md`.

**Préalables hors code.** Fusionner le brouillon [#17](https://github.com/AmauryTISSOT/microservice_rgpd/pull/17), qui porte l'extension du glossaire (`QualificationOpinion`, `DeclaredConfidence`, `WitnessOpinion`, `ReviewSignal`) **et la révision de la règle « détecteur, jamais contributeur »** — cette phrase corrigée n'existe **nulle part ailleurs**. Le brouillon [#15](https://github.com/AmauryTISSOT/microservice_rgpd/pull/15) porte les prototypes, dont `moteur_lexique.py`, `prompt_llm.md` et `evaluer.py` : c'est le **matériau de départ du sidecar**, à fusionner ou à reprendre avant l'étape 4.

---

## 13. Ce que cette spec ne couvre pas

### 13.1 Questions ouvertes, hors périmètre d'implémentation

Reprises de la section *Not yet specified* de la carte. Aucune ne bloque l'écriture du code.

- **Rétention, purge et minimisation de la trace** — **condition de sortie de POC** : le service ne doit pas recevoir de donnée réelle avant que ce bloc ne soit traité. Deux acquis : le schéma peut accueillir une purge sans migration si l'on rétablit la distinction éphémère / durable, et les durées avancées en séance (6 mois – 1 an par analogie avec la journalisation, 5 ans si l'on se cale sur l'art. 2224 du Code civil) l'ont été **de mémoire** — il faudra les sourcer.
- **Critères d'acceptation du modèle local** — exactitude, calibration réelle, taux de désaccord effectif n'ont jamais été mesurés sur `qwen3:8b`. **Sans propriétaire assigné.**
- **Disjoncteur sur le client LLM** — écarté en v1 ; le fait qui manque pour le régler est nommé : le taux et la forme des échecs d'Ollama n'ont jamais été mesurés.
- **Intégration continue** — deux contraintes déjà acquises : les runners Linux de GitHub Actions embarquent Docker, donc les Testcontainers PostgreSQL y tournent tels quels ; et tout dispositif démarrant l'`AppHost` démarrerait Ollama, ce qui reste interdit.
- **Exploitation du sidecar** — image et packaging, dépendances, montée de version du modèle, démarrage à froid.
- **Montée de version du modèle** — comment comparer les verdicts avant et après, et décider d'un seuil d'acceptation. Contrainte durcie par le § 8.1 : cette comparaison ne pourra **jamais** s'appuyer sur des demandes réelles, seulement sur le corpus synthétique.
- **Observabilité** — quelles métriques émettre via OpenTelemetry ; **le taux de `Contested` est une métrique de dérive évidente**.
- **Comportement en charge** — volumétrie, appels concurrents, traitement par lots, sur un GPU qui sérialise.
- **Non-déterminisme du moteur** — fortement réduit par `temperature: 0` et seed fixe, reste à confirmer en pratique.
- **Contexte de la relation attendu par le CEPD** — écarté de la v1 sans être tranché ; le faire entrer suppose de **rouvrir aussi le contrat interne**, le sidecar ne recevant que `text`.
- **Retour de la question du tiers en cas de bascule vers Mistral** — la portabilité rend le basculement facile, ce qui rouvrirait d'un coup le dossier sous-traitant / DPA / localisation.

### 13.2 Risques assumés, à lire avant d'implémenter

1. **Le moteur retenu n'a jamais été mesuré.** Les chiffres du prototypage sont ceux d'Opus 5 ; ils ne s'appliquent pas à `qwen3:8b`. Si le modèle s'avérait nettement plus faible, le verdict se dégraderait et le taux de `Contested` grimperait bien au-delà des ~25 % extrapolés. **Le montage à deux moteurs amortit ce risque — il ne le supprime pas.**
2. **Le service sort sans critère de qualité de qualification** (§ 10.1).
3. **Le mode de repli est le mode où le service se trompe le plus, et le moins visiblement** (§ 7.1).
4. **Le contrat public n'a pas de segment de version** — dette nommée, atténuable par un alias permanent (§ 4.6).
5. **Le texte est conservé en clair, sans échéance** — tenable **uniquement** parce qu'aucune donnée réelle ne transite (§ 8.2).
6. **`503`, `504` et les annulations sont invisibles à l'audit** (§ 8.7).

### 13.3 Hors périmètre, décidé

Ces sujets ne reviendront pas dans cette carte. Ils supposeraient une destination redessinée.

- **Traitement effectif de la demande** — instruction, identification du demandeur, export de portabilité, délai d'un mois. Le service s'arrête à la qualification.
- **Authentification de l'application tierce** — clé d'API, OAuth, mTLS. L'endpoint est supposé joignable en réseau de confiance.
- **Détection et caviardage des données personnelles** dans le texte reçu.
- **Interface d'administration ou de relecture** — la validation humaine se fait côté application tierce.
- **Droits hors des sept valeurs** — déréférencement, droit à l'information, art. 22. Écartés en connaissance de cause : la taxonomie reste fermée, ces demandes retombent en `OutOfScope`, et la `justification` du LLM donne à l'opérateur de quoi comprendre pourquoi.
- **Recours à un LLM en API tierce pour la v1** — le code reste conçu pour rendre la bascule facile, mais elle n'est pas au programme.

---

## 14. Traçabilité

| Ticket | Ce qu'il a tranché | Section |
| --- | --- | --- |
| [État de l'art : qualifier un texte libre vers une taxonomie fermée](https://github.com/AmauryTISSOT/microservice_rgpd/issues/3) | supervisé classique écarté ; le duel est lexiques vs LLM ; confidentialité discriminante | 3 |
| [Formulations réelles des six droits RGPD et pièges de confusion](https://github.com/AmauryTISSOT/microservice_rgpd/issues/4) | aucun formalisme exigible ; multi-droits fondé en droit ; repli par défaut sur l'opposition proscrit | 1, 2 |
| [Constituer un corpus annoté de demandes RGPD en français](https://github.com/AmauryTISSOT/microservice_rgpd/issues/2) | 120 exemples JSONL dans `corpus/`, aucune donnée réelle | 4.2, 8.1, 10.3 |
| [Fixer le vocabulaire du domaine de la qualification](https://github.com/AmauryTISSOT/microservice_rgpd/issues/5) | langue, types, taxonomie, invariants, slice | 2 |
| [Prototyper et comparer deux moteurs de qualification sur le corpus](https://github.com/AmauryTISSOT/microservice_rgpd/issues/6) | le LLM domine, mais le fait décisif est la **nature** des erreurs | 3, 6.3, 7.1 |
| [Trancher le moteur de qualification retenu pour la v1](https://github.com/AmauryTISSOT/microservice_rgpd/issues/7) | hybride auto-hébergé, sidecar Python, `ReviewSignal` | 3, 6 |
| [Fixer le contrat interne entre le service .NET et le sidecar](https://github.com/AmauryTISSOT/microservice_rgpd/issues/16) | deux endpoints, projection, confiance ordinale, aucun avis à moitié valide | 5 |
| [Fixer le contrat de l'endpoint de qualification](https://github.com/AmauryTISSOT/microservice_rgpd/issues/8) | `POST /qualifications`, bornes, quatre champs, palette d'erreurs, versionnement | 4 |
| [Définir ce que conserve la trace d'audit et sa rétention](https://github.com/AmauryTISSOT/microservice_rgpd/issues/9) | finalité, champs, schéma, écriture synchrone | 8 |
| [Définir les comportements dégradés de la qualification](https://github.com/AmauryTISSOT/microservice_rgpd/issues/10) | repli lexical, `degraded`, résilience explicite, aucun classifieur en amont | 7 |
| [Fixer la barre de qualité et la stratégie de test](https://github.com/AmauryTISSOT/microservice_rgpd/issues/11) | barre sur le code seul, découpage des projets, TDD strict | 10 |
| [Assembler la spec finale de la fonctionnalité](https://github.com/AmauryTISSOT/microservice_rgpd/issues/12) | ce document, les invariants transverses (§ 11), l'ouverture de `docs/adr/` | 11, 12 |

Le commentaire de résolution de chaque ticket reste la source de vérité du *pourquoi*. Un **ADR** consigne la décision d'architecture à la plus longue portée : [`docs/adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md`](../adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md).
