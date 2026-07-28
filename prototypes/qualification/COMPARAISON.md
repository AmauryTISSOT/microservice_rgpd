# Deux moteurs de qualification mis en regard sur le corpus

**Ticket** : [Prototyper et comparer deux moteurs de qualification sur le corpus](https://github.com/AmauryTISSOT/microservice_rgpd/issues/6) — enfant de la carte [#1](https://github.com/AmauryTISSOT/microservice_rgpd/issues/1)
**Corpus** : `corpus/demandes-rgpd.fr.jsonl`, 120 exemples annotés ([#2](https://github.com/AmauryTISSOT/microservice_rgpd/issues/2))
**Date** : 27 juillet 2026

Les deux moteurs sont des **prototypes jetables**. Ils vivent hors de la
solution .NET, en Python, et n'ont aucune vocation à être portés tels quels.
Leur seul rôle est de mesurer un écart.

Le duel « lexiques contre LLM » est celui que la recherche
[#3](https://github.com/AmauryTISSOT/microservice_rgpd/issues/3) a désigné :
le supervisé classique y a été écarté (ML.NET sans multi-étiquettes natif,
modèle de base anglophone), et les embeddings exigent une calibration de sept
seuils qui suppose déjà un corpus.

---

## Le résultat en une ligne

Sur ce corpus, **le moteur LLM domine nettement** : 96,7 % de correspondance
exacte contre 78,3 %, et un macro-F₂ de 0,980 contre 0,899. Mais **le chiffre
du LLM est un plafond optimiste** obtenu dans un protocole dégradé (§ *Ce que
cette comparaison ne mesure pas*), et l'écart n'est pas le fait le plus
décisif : c'est la **nature** des erreurs qui l'est.

---

## 1. Exactitude

| | Moteur A — lexiques et règles | Moteur B — LLM à sortie structurée |
| --- | ---: | ---: |
| **Correspondance exacte** | 78,3 % (94/120) | **96,7 % (116/120)** |
| **Macro-F₂** (métrique de pilotage) | 0,899 | **0,980** |
| Macro-F₁ | 0,889 | 0,983 |
| Macro-précision | 0,878 | 0,989 |
| Macro-rappel | 0,906 | 0,979 |
| Hamming loss | 0,0417 | **0,0060** |
| Exemples en erreur | 26 | 4 |

Le **F₂** est la métrique de pilotage retenue par la recherche #3 : « Values of
β > 1 favor recall » (SLP3 ch. 4 §4.9). La sortie étant validée par un humain,
un droit oublié coûte plus qu'un droit en trop. La **macro**-moyenne prime sur
la micro : les sept droits comptent également, et limitation comme portabilité
sont structurellement sous-représentés dans le corpus.

### Par droit

| Droit | Support | A — F₂ | B — F₂ | A — rappel | B — rappel |
| --- | ---: | ---: | ---: | ---: | ---: |
| `acces` | 23 | 0,83 | 0,98 | 0,83 | 1,00 |
| `rectification` | 14 | 0,97 | 1,00 | 1,00 | 1,00 |
| `effacement` | 26 | 0,88 | 0,97 | 0,88 | 0,96 |
| `limitation` | 14 | **1,00** | 1,00 | 1,00 | 1,00 |
| `portabilite` | 13 | 0,87 | 0,94 | 0,85 | 0,92 |
| `opposition` | 20 | 0,83 | 1,00 | 0,85 | 1,00 |
| `hors-perimetre` | 30 | 0,91 | 0,97 | 0,93 | 0,97 |

**Le moteur lexical n'est pas uniformément faible.** Il atteint un F₂ parfait
sur la **limitation** — le droit dont la recherche #4 disait qu'il n'a aucun
modèle de courrier CNIL, donc aucune formulation officielle. La raison est
qu'il porte le marqueur le plus mécanique de toute la taxonomie : la double
instruction « ne plus utiliser / ne pas supprimer ». Un droit rare mais à
signature nette est *plus* facile à régler qu'un droit fréquent et diffus.

Ses deux points faibles sont l'`acces` et l'`opposition` : les deux droits dont
le vocabulaire est le plus recouvert par celui des autres — la CNIL emploie
elle-même « faire supprimer » pour décrire l'opposition.

---

## 2. Faux positifs sur les textes hors périmètre

C'est la classe où une erreur coûte le plus à l'opérateur humain : le corpus la
sur-représente délibérément (30 exemples sur 120).

| | Moteur A | Moteur B |
| --- | ---: | ---: |
| Textes classés hors périmètre à tort | 6 | **0** |
| Textes hors périmètre où un droit est inventé | 2 | **1** |
| Précision sur `hors-perimetre` | 0,82 | **1,00** |
| Rappel sur `hors-perimetre` | 0,93 | 0,97 |

Le moteur B n'a **jamais** rejeté à tort une vraie demande. Le moteur A l'a
fait 6 fois, dont deux cas instructifs :

- `edg-20` — « *Please delete my account and all the personal data associated
  with it.* » Un texte en anglais. Le lexique est monolingue par construction :
  il rejette. Le LLM qualifie correctement. La carte a acté le **français
  uniquement**, mais rien ne garantit qu'une application tierce n'enverra pas
  d'anglais — et le mode d'échec des deux moteurs y est opposé.
- `edg-10` — une demande d'accès noyée sous 60 mots de précautions oratoires.
  Le lexique dilue, le LLM va au verbe.

---

## 3. Les huit paires minimales

Les couples quasi identiques dont la qualification diffère : les exemples les
plus discriminants du corpus.

| Paire | Départage | A | B |
| --- | --- | :---: | :---: |
| `edg-13` / `edg-14` | valeur de remplacement | ❌ | ✅ |
| `edg-04` / `edg-05` | question sur un droit ou exercice | ❌ | ✅ |
| `eff-02` / `hop-03` | supprimer un compte ou résilier un abonnement | ✅ | ✅ |
| `eff-03` / `hop-24` | objet « données » ou objet « contrat » | ❌ | ✅ |
| `opp-02` / `mul-01` | l'enregistrement est préservé, ou visé | ❌ | ❌ |
| `edg-11` / `edg-12` | opposition ou limitation | ❌ | ✅ |
| `acc-01` / `hop-22` | RGPD ou législation sectorielle | ✅ | ✅ |
| `eff-05` / `opp-01` | retrait de consentement ou opposition | ✅ | ✅ |
| **Total départagé** | | **3/8** | **7/8** |

`opp-02` / `mul-01` résiste aux deux — c'est la seule paire que ni l'un ni
l'autre ne tranche, et elle mérite d'être regardée de près (§ 5).

---

## 4. Latence et coût

| | Moteur A | Moteur B |
| --- | --- | --- |
| **Latence médiane** | **~0,5 ms** (mesurée) | non mesurée — aucune clé d'API |
| Latence p95 | ~1 à 1,5 ms (mesurée) | — |
| Latence maximale | ~4 à 6 ms (mesurée) | — |
| **Coût par appel** | **nul** | 0,0002 à 0,011 $ (calculé) |
| Données envoyées à un tiers | **aucune** | le texte intégral, non caviardé |

La latence du moteur A est mesurée sur les 120 textes, motifs compilés, en
mémoire. Les valeurs varient d'une exécution à l'autre — d'où des ordres de
grandeur plutôt que des chiffres : la mesure est bruitée par l'ordonnanceur,
et la précision n'a aucun intérêt ici. Le fait qui compte est l'échelle :
**trois ordres de grandeur sous le moindre aller-retour HTTP.**

Pour le moteur B, **aucune latence n'a pu être mesurée** : l'environnement ne
dispose d'aucune clé d'API. La recherche #3 avait déjà établi qu'aucun
fournisseur ne publie de SLA ni de percentile — « All limits described here
represent maximum allowed usage, not guaranteed minimums ». Ce chiffre reste
donc **à mesurer avant toute décision**, et il conditionne la tenue d'un POST
synchrone.

### Coût par appel — calculé, non facturé

Profil mesuré sur ce prototype : prompt système de **6 668 caractères ≈ 1 800
tokens**, texte médian de 102 caractères ≈ 30 tokens, sortie ≈ 60 tokens
(droits + justification + confiance).

| Modèle | Sans cache | Pour 10 000 qualifications |
| --- | ---: | ---: |
| Mistral Small 4 ($0,15 / $0,60) | 0,00031 $ | 3,10 $ |
| Claude Haiku 4.5 ($1 / $5) | 0,00215 $ | 21,50 $ |
| Claude Sonnet 5 ($2 / $10) | 0,00430 $ | 43,00 $ |
| Claude Opus 5 ($5 / $25) | 0,01080 $ | 108,00 $ |

**Le piège du cache, confirmé par ce prototype.** Les minimums cachables sont
de 1 024 tokens sur Sonnet 5 et **4 096 sur Haiku 4.5**. Notre prompt système
fait ~1 800 tokens : il est cachable sur Sonnet, **il ne l'est pas sur
Haiku** — et « no error is returned ». Le modèle le moins cher est donc celui
qui ne bénéficie *pas* de l'économie de 75 %, ce qui resserre l'écart réel
entre les deux. Sur Sonnet 5 avec cache actif, le coût tombe à ~0,0010 $.

**Le coût n'est pas le facteur discriminant** — la recherche #3 le disait déjà,
ce prototype le confirme. Même à 108 $ pour 10 000 qualifications, le poste est
marginal devant le temps de l'opérateur humain qui valide.

---

## 5. Nature des erreurs

C'est la question que le ticket pose et à laquelle les métriques ne répondent
pas : **lesquelles seraient rattrapées par un humain, lesquelles passeraient
inaperçues ?**

### Moteur B — les 4 erreurs sont toutes signalées

| Confiance déclarée | Exemples | Erreurs | Taux |
| --- | ---: | ---: | ---: |
| haute | 111 | **0** | 0,0 % |
| moyenne | 6 | 3 | 50,0 % |
| basse | 3 | 1 | 33,3 % |

**Aucune erreur en confiance haute.** Relire les 9 textes de confiance non
haute — **8 % du corpus** — capte **4 erreurs sur 4**.

C'est le résultat le plus important de cette comparaison, et il déplace le
critère de choix. Pour une aide à la décision, une erreur *signalée* n'est pas
une erreur : c'est un aiguillage vers l'humain. Un moteur à 96,7 % dont les
3,3 % restants sont tous marqués « à relire » est opérationnellement plus
proche de 100 % que son taux brut ne le suggère.

Réserve à ne pas escamoter : ce chiffre repose sur **9 exemples**. Un taux de
détection de 4/4 sur un échantillon de cette taille n'est pas une garantie —
c'est un signal encourageant qui demande à être reproduit.

Les quatre erreurs, dans le détail :

- `lim-08` — « Suspendez tout usage jusqu'à ce que vous m'ayez répondu sur leur
  origine. » Le moteur ajoute `acces` à `limitation`. **Discutable des deux
  côtés** : le texte suppose une demande d'accès en cours sans la formuler.
- `mul-01` — « Désinscrivez-moi de votre newsletter et supprimez mes
  coordonnées de vos fichiers. » Le moteur retient `opposition` seul, le corpus
  attend aussi `effacement`. Le moteur a appliqué le modèle CNIL d'opposition
  à la prospection, où la suppression des coordonnées est l'*effet* de
  l'opposition. **Les deux lectures se défendent.**
- `mul-07` — « Envoyez-moi une copie de mes données. » Le corpus attend
  `acces` + `portabilite` (règle : sans indice, les deux) ; le moteur a retenu
  `acces` seul, car « copie » est le terme de l'art. 15 §3.
- `edg-02` — « Mes données ? » Deux mots. Le moteur qualifie `acces` en
  confiance basse ; le corpus attend `hors-perimetre`.

Aucune de ces quatre erreurs n'est absurde. Trois sur quatre relèvent d'un
désaccord d'annotation défendable plutôt que d'une méprise.

### Moteur A — 26 erreurs, dont une majorité silencieuse

Le moteur A **n'émet aucun signal de confiance**. Ses scores internes existent
mais ne sont pas calibrés : rien ne distingue une qualification sûre d'une
qualification de justesse. Toutes ses erreurs sont donc, par construction,
**silencieuses**. C'est la différence structurante entre les deux, plus encore
que l'écart d'exactitude.

Trois familles :

**a) Sur-génération (14 erreurs) — bruit, rattrapable.** Le moteur ajoute un
droit en trop : `edg-13` « Supprimez mon ancien numéro, le nouveau est le 07…
» → `effacement` + `rectification` là où seule la rectification est attendue.
Le droit correct est présent ; l'humain retire le surnuméraire. Coût : du temps.

**b) Sous-génération sur du multi-droits (7 erreurs) — silencieuse et
coûteuse.** `mul-11` « Communiquez-moi mes données et, tant que je n'ai pas
vérifié leur exactitude, cessez de les utiliser. » → `limitation` seul, `acces`
manquant. L'opérateur voit une qualification plausible, sans indice qu'elle est
incomplète. **C'est le mode d'échec le plus dangereux** : il ne se voit pas.

**c) Rejet à tort (6 erreurs) — le plus coûteux.** Une demande valide classée
`hors-perimetre` risque de ne jamais atteindre l'opérateur, selon ce que
l'application tierce fait de cette étiquette.

### Ce que le lexique a coûté à mettre au point

Le moteur A a été écrit **à partir des seules recherches #3 et #4**, sans
lecture des étiquettes. Première évaluation : **65,0 %** de correspondance
exacte. Après **une** passe de correction, il atteint **78,3 %** — et la passe
a porté sur deux bugs de mécanisme, pas sur des exemples :

1. **`\bavoir\b`** classait « je ne veux plus rien avoir à faire avec vous »
   comme un litige commercial.
2. **La négation, transposée telle quelle de l'anglais, inversait le sens des
   demandes les plus fréquentes.** La recette SLP3 préfixe `NOT_` après tout
   marqueur de négation. En français, « ne … plus » n'est pas une négation du
   marqueur : c'est *la* formulation de l'opposition et de la limitation.
   « Ne m'envoyez plus de publicité » était compté **contre** l'opposition.

Jurafsky & Martin annonçaient précisément ce piège — « rules can be fragile
[…] the example of negation » — et le prototype le vérifie. **Le point à
retenir n'est pas le chiffre après correction, mais que 13 points d'exactitude
tenaient à deux lignes de code qu'aucune relecture n'avait signalées.** C'est
la nature du coût de maintien d'un lexique : permanent, et invisible jusqu'à
la mesure.

---

## 6. Synthèse

| Critère | Moteur A — lexiques | Moteur B — LLM |
| --- | --- | --- |
| Correspondance exacte | 78,3 % | **96,7 %** |
| Macro-F₂ | 0,899 | **0,980** |
| Paires minimales départagées | 3/8 | **7/8** |
| Faux rejets de vraies demandes | 6 | **0** |
| Erreurs signalées à l'opérateur | **aucune** — toutes silencieuses | 4/4 signalées |
| Latence | **0,46 ms médiane** | non mesurée |
| Coût / 10 000 appels | **0 $** | 3 à 108 $ |
| Auditabilité de la décision | **code lisible, décision rejouable** | justification en langue naturelle, non vérifiable |
| Données envoyées à un tiers | **aucune** | **le texte intégral, non caviardé** |
| Déterminisme | **total** | non mesuré (un seul passage) |
| Robustesse hors français | nulle | bonne |
| Effort de mise au point | 2 passes, 13 points gagnés sur des bugs | prompt écrit une fois |
| Coût de maintien | permanent, dérive lexicale | dépendance fournisseur et prix |

**Ce que ce prototype tranche** : sur la qualité de qualification, l'écart est
franc et va au LLM. Le lexique ne rattrape pas 18 points de correspondance
exacte, et surtout il ne sait pas dire qu'il doute — ce qui, pour une aide à la
décision humaine, est une infirmité plus grave que son taux d'erreur.

**Ce que ce prototype ne tranche pas, et ne pouvait pas trancher** : le moteur B
envoie le texte intégral, non caviardé, à un tiers. La carte #1 a placé le
caviardage hors périmètre, et la recherche #3 a établi qu'Anthropic ne
documente aucune résidence UE là où Mistral l'offre par défaut. **C'est une
question de conformité, politique avant d'être technique, et elle appartient au
ticket « moteur retenu ».** Un moteur trois fois meilleur qui ne peut pas être
déployé ne vaut rien.

**Une piste que ces chiffres rendent concrète** : les deux moteurs échouent sur
des textes différents. Le lexique est parfait sur la limitation, gratuit,
instantané, auditable et souverain ; le LLM couvre le reste et sait dire qu'il
doute. Une passe déterministe en premier avis, un LLM en second — l'hybride que
la recherche #3 mentionnait sans l'instruire (SpamAssassin, SLP3 §4.3.1) — n'est
plus une hypothèse d'école après ces mesures. Elle mérite d'être posée comme
option devant le ticket « moteur retenu », au même rang que les deux moteurs
purs.

---

## Ce que cette comparaison ne mesure pas

À lire **avant** de citer le 96,7 %.

1. **Le moteur B n'a pas été exécuté via une API.** Aucune clé n'est disponible
   dans cet environnement. Les 120 qualifications ont été produites par un
   modèle Claude Opus 5 appliquant le prompt système figé de `prompt_llm.md`,
   sur le corpus **anonymisé et permuté** de `textes-aveugles.jsonl`. Le
   protocole est aveugle quant aux étiquettes, mais ce **n'est pas** un appel
   `output_config.format` réel.
2. **Opus 5 est le modèle le plus cher de la gamme.** Haiku 4.5, cinq fois
   moins coûteux, ferait vraisemblablement moins bien. Le 96,7 % est un
   **plafond**, pas la performance du moteur qu'on déploierait.
3. **Aucune latence, aucun coût réel, aucun taux d'erreur d'appel** (429,
   timeout, indisponibilité) n'a été observé.
4. **La variance n'a pas été mesurée** : un seul passage, aucune répétition. Le
   déterminisme d'un LLM sur cette tâche reste inconnu — alors que le moteur A
   est déterministe par construction.
5. **Il n'est pas vérifié que `GetResponseAsync<T>` sur
   `AnthropicClient.AsIChatClient()` produise un `output_config.format` natif**
   plutôt qu'une instruction de prompt. La recherche #3 l'avait déjà signalé
   comme à tester.
6. **Le corpus est petit et fait maison.** 120 exemples, tous inventés, dont 25
   textes hors périmètre seulement. Les intervalles de confiance sur des
   supports de 13 ou 14 exemples sont larges — un écart de 0,05 de F₂ sur la
   portabilité ne signifie rien.
7. **Le moteur A a vu le corpus une fois** (via ses erreurs agrégées) avant sa
   passe de correction ; le moteur B ne l'a jamais vu. À strictement parler,
   les 78,3 % sont légèrement optimistes eux aussi.

---

## Reproduire

```bash
py prototypes/qualification/extraire_aveugle.py   # regénère l'aveugle (déterministe)
py prototypes/qualification/evaluer.py            # tableaux ci-dessus
py prototypes/qualification/evaluer.py --detail   # + liste des erreurs
```

Sous Windows, préfixer par `PYTHONIOENCODING=utf-8` — la sortie contient des
indices Unicode (F₂).
