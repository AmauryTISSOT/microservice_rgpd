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

---

## 3. L'appel

Une opération par `Capability`, en `POST`, sous l'adresse que le `Manifest` déclare pour le système.

```
POST <adresse déclarée>/<capability>?system_id=<votre identifiant de système>
X-RGPD-Secret: <le secret partagé>
Content-Type: application/json
```

`<capability>` vaut `locate`, `read`, `erase` ou `rectify`. Seul `locate` est appelé à ce jour ;
voir § 7.

Le corps porte le **sac de désignations**, et rien d'autre :

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
| `200` | Servi. | Ce que la `Capability` rend, en JSON. |
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
- `404` — **un désaccord `Manifest`/`Adapter`.** Le paysage déclaré côté service désigne une adresse
  qui ne connaît pas ce système. Quelqu'un a tort — la déclaration, ou votre `Adapter` — et un
  humain tranchera. Le service ne corrige **jamais** son `Manifest` en silence sur votre réponse.

**Refusez explicitement un `system_id` inconnu.** Ne servez pas « au mieux » un système que vous ne
connaissez pas : un `200` poli sur un identifiant inconnu est exactement l'omission silencieuse que
tout ce dispositif cherche à rendre impossible.

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
- **Tenir une connexion** en espérant votre réponse : répondez `202`.
- **Relancer tout seul.** Aucun processus de fond ne tourne ; tout se recalcule quand un opérateur
  regarde.
- **Corriger son `Manifest`** sur ce que vous répondez.
- **Vérifier ce que vous déclarez.** Le service est greffier, pas témoin : une réponse est une
  affirmation datée et attribuable, jamais un fait vérifié. Un travail déclaré fait prouve qu'on a
  déclaré l'avoir fait.
- **Parler à la personne concernée.** Elle n'atteint jamais le service, et le service ne lui écrit
  jamais.

---

## 7. Ce qui n'est pas encore là

Ce contrat est celui du **transport**, et il est complet : l'appel, le secret, le différé, les deux
refus. Ce que chaque `Capability` **rend** se fixe ticket par ticket.

- `locate` — la forme de sa réponse (noyau certain, réserves motivées) se décide avec l'écran
  d'arbitrage.
- `read` — portera, **en plus des désignations**, le droit **au titre duquel** on lit. Jamais la
  forme attendue : le périmètre matériel de l'art. 20 est plus étroit que celui de l'art. 15 et se
  décide ligne par ligne — vous seul pouvez le trancher, et vous tranchez du même geste le périmètre
  et la forme.
- `erase` et `rectify` — **déclarables dès aujourd'hui** dans le `Manifest`, exercées plus tard.
  Déclarer une capacité que personne n'appelle encore est une déclaration exacte ; ne pas pouvoir la
  déclarer serait un mensonge du catalogue.

Un système **sans aucune `Capability`** reste pleinement légitime : il est recensé, et traité à la
main. C'est le régime majoritaire, et celui dont le service tire le plus de valeur — il nomme ce
qu'il ne touche pas.

---

## 8. Pour le mainteneur

- Le contrat sur le fil : [`HttpAdapterCalls`](../../src/MicroserviceRgpd.Infrastructure/Casework/Adapters/HttpAdapterCalls.cs).
- Le vocabulaire des réponses : [`AdapterVerdict`](../../src/MicroserviceRgpd.Core/Casework/Adapters/AdapterVerdict.cs),
  [`AdapterAnswer`](../../src/MicroserviceRgpd.Core/Casework/Adapters/AdapterAnswer.cs).
- Ce qu'un refus laisse : [`AdapterCallsForCase`](../../src/MicroserviceRgpd.UseCases/Casework/CallAdapter/AdapterCallsForCase.cs).
- Le secret et le refus de démarrer : [`AdapterServiceExtensions`](../../src/MicroserviceRgpd.Infrastructure/Casework/Adapters/AdapterServiceExtensions.cs),
  clé `Casework:AdapterSecret`.

**La couture de test est posée sur le fil**, et c'est délibéré : un `HttpMessageHandler` injecté
dans le client de l'`Adapter`. L'en-tête de secret, le `system_id` en paramètre, le `202` et son
échéance **sont** le contrat — un port du domaine les aurait cachés au-dessus de la couture, et les
tests n'auraient plus prouvé que le comportement d'une doublure. Voir
[`HttpAdapterCallsTests`](../../tests/MicroserviceRgpd.UnitTests/Infrastructure/Casework/HttpAdapterCallsTests.cs).
