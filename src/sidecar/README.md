# Sidecar de qualification

La moitié Python du service : les moteurs qui qualifient un texte français au regard des droits que
le RGPD ouvre aux personnes concernées. Le service .NET les appelle en HTTP, un **point d'entrée par
moteur**.

Le sidecar n'est pas là parce que le lexique aurait besoin de Python — il n'importe que `re` et
`unicodedata`, que .NET fait aussi bien. La frontière est posée pour un motif unique et explicite :
**une évolution vers l'apprentissage automatique est anticipée**. Voir
[`docs/adr/0001`](../../docs/adr/0001-architecture-polyglotte-et-moteur-auto-heberge.md).

## Points d'entrée

| Verbe et chemin | Moteur | Codes |
| --- | --- | --- |
| `POST /opinions/lexicon` | le lexique déterministe, moteur **témoin** | `200`, `400`, `500` |
| `POST /opinions/llm` | le LLM local, qui rend le **verdict** | `200`, `400`, `502`, `503`, `504` |
| `GET /health` | — | `200` |

```jsonc
// requête — le texte, et rien d'autre, aux deux points d'entrée
{ "text": "Supprimez toutes les données que vous avez sur moi." }

// 200 de /opinions/lexicon
{ "rights": ["Erasure"], "engine": { "name": "lexicon", "version": "1.0.0" } }

// 200 de /opinions/llm
{
  "rights": ["Erasure"],
  "confidence": "High",
  "justification": "La demande porte sur la totalité des données sans finalité nommée : article 17.",
  "engine": { "name": "llm", "version": "qwen3:8b+prompt.1.0.0" }
}
```

La palette du lexique est **courte, et ce n'est pas un accident** : il n'a aucun amont, donc ni
`502`, ni `503`, ni `504` ne peuvent survenir. Celle du LLM est plus riche pour la raison inverse.
C'est ce qu'achètent deux points d'entrée séparés — un point d'entrée commun aurait imposé au
lexique la palette du LLM, et fait chercher un serveur de modèles là où il n'y en a jamais eu.

**Confiance obligatoire ici, interdite là-bas.** Le LLM déclare toujours son degré de doute sur
l'échelle ordinale `High` / `Medium` / `Low` et justifie toujours son verdict ; le lexique ne fait
ni l'un ni l'autre, n'ayant aucun avis sur sa propre fiabilité. Ce sont deux types distincts, et non
un type commun à champs optionnels : celui-ci rendrait exprimable un avis lexical assorti d'une
confiance, exactement ce que le domaine interdit.

**La justification reste en français.** C'est le seul texte du sidecar destiné à un humain, et
l'opérateur qui relit la qualification lit le français. Tout le reste du fil — noms de champs et
droits — est en anglais canonique.

**La version du moteur LLM nomme le modèle réellement servi** — celui que l'amont déclare avoir
servi, et non celui qu'on lui a demandé — suivi de la version de la consigne
(`qwen3:8b+prompt.1.0.0`). Sans le premier, personne ne saurait plus tard quelles qualifications
relèvent de quel modèle ; sans la seconde, deux verdicts du même modèle sous deux prompts différents
passeraient pour comparables.

> **Le tag, pas l'empreinte.** `docs/spec/qualification.md` § 5.7 illustre cette version par
> `qwen3:8b@sha256:…`. L'empreinte n'est pas lisible par le protocole compatible OpenAI : l'obtenir
> demanderait un appel à l'API propre d'Ollama, donc de graver le fournisseur dans le moteur —
> exactement ce que « basculer ne demande que `base_url` et nom de modèle » interdit. Le tag est
> retenu, et sa limite est assumée : deux jeux de poids repoussés sous le même tag porteront la même
> version.

**Aucun identifiant maison.** La corrélation avec l'appelant passe par l'en-tête `traceparent`, que
`ServiceDefaults` propage déjà. Les erreurs sortent en `application/problem+json`.

### Les trois échecs de l'amont ne partagent jamais un code

| Code | Ce qui s'est passé | Ce que l'exploitant doit faire |
| --- | --- | --- |
| `502` | l'amont a répondu, mais sa réponse n'est pas un avis | revoir le modèle ou la consigne |
| `503` | l'amont est injoignable, ou son modèle n'est pas chargé | démarrer ou réparer Ollama |
| `504` | l'échéance vers l'amont est passée | changer de modèle, ou de matériel |

À côté d'eux, un code qui ne nomme **aucune** panne : `501` dit que ce déploiement ne sert aucun
modèle, le moteur y étant éteint. Rien n'est à réparer, et rien n'est journalisé en erreur.

Le lexique, lui, sort une panne de moteur en `500` là où le LLM sort `502` : la panne est la même
vue du domaine — **un avis invalide n'est pas un avis faible** —, et les codes diffèrent par le seul
fait qui compte pour l'exploitant, *qui* est à réparer. Un bogue Python d'un côté, un modèle qui
déraille de l'autre.

**La lenteur doit arriver nommée.** L'échéance du sidecar vers l'amont est tenue *strictement plus
courte* que celle de l'appelant .NET, et le sidecar **refuse de démarrer** si la configuration ne
respecte pas cette inégalité : si l'appelant abandonnait le premier, il n'aurait qu'une échéance
anonyme à rapporter, là où le sidecar sait dire lequel des trois échecs il a subi. **Le moteur
éteint, cette vérification ne s'applique plus** : il n'y a pas d'échéance à tenir, et une paire
résiduelle laissée dans la configuration pour rallumer plus tard est ignorée sans bruit.

> **Ce que cette garde vaut aujourd'hui, et ce qui lui manque.** Les deux échéances viennent d'une
> même section de configuration de l'`AppHost` — `Llm:SidecarDeadlineSeconds` et
> `Llm:CallerDeadlineSeconds` —, et le client .NET, quand il naîtra, lira la seconde au même
> endroit. Tant qu'il n'existe pas, l'inégalité est **vérifiée mais pas encore exercée** : rien ne
> peut prouver ici que 150 s sont bien appliquées en face. La garde reste préférable au commentaire
> qu'elle remplace — elle transforme un chiffre qu'on aurait pu changer par distraction en un
> démarrage qui échoue.

**Aucune reprise n'est tentée.** Température à zéro et seed fixe font d'une requête rejouée une
opération nulle : elle rendrait le même avis en payant une seconde fois un GPU que 8 Go de VRAM
sérialisent déjà, et en dépassant l'échéance de l'appelant par-dessus le marché.

### Configuration du moteur LLM

**Le moteur LLM est éteint par défaut.** Un drapeau commande son existence, et sans lui le sidecar
démarre sans une seule des sept variables ci-dessous : il sert son lexique exactement comme
d'habitude, et rend `501` sur `POST /opinions/llm`. C'est ce qui permet de cloner le dépôt et de le
démarrer sans carte graphique — un poste de développement, un exécuteur d'intégration continue, une
démonstration.

| Variable | Rôle |
| --- | --- |
| `QUALIFICATION_LLM_ENABLED` | `true` allume le moteur ; **absente, il est éteint** |

Allumé, aucune valeur par défaut n'existe dans le code : **une variable absente empêche le sidecar
de démarrer**, exactement comme une divergence de taxonomie. Un `base_url` deviné ne rend pas des
avis un peu faux, il en rend d'inexploitables. Le drapeau est la **seule** variable du moteur à
disposer d'un repli, et l'écart est délibéré : le défaut sûr prime, et un déploiement qui ne dit
rien ne soumet aucun texte de personne concernée à un modèle génératif.

| Variable | Rôle, le moteur allumé |
| --- | --- |
| `QUALIFICATION_LLM_BASE_URL` | l'adresse du serveur compatible OpenAI |
| `QUALIFICATION_LLM_MODEL` | le nom du modèle demandé |
| `QUALIFICATION_LLM_API_KEY` | ignorée par Ollama, exigée par le protocole |
| `QUALIFICATION_LLM_TEMPERATURE` | `0` — le déterminisme est ce qui retire toute raison de reprendre |
| `QUALIFICATION_LLM_SEED` | la seed fixe, pour la même raison |
| `QUALIFICATION_LLM_DEADLINE_SECONDS` | l'échéance sidecar → amont |
| `QUALIFICATION_LLM_CALLER_DEADLINE_SECONDS` | celle que l'appelant .NET appliquera, dont la précédente doit rester strictement plus courte |

L'`AppHost` les fournit toutes depuis
[`src/MicroserviceRgpd.AspireHost/appsettings.json`](../MicroserviceRgpd.AspireHost/appsettings.json).
**Ces chiffres sont arbitraires et assumés** : rien n'a été mesuré, et ils devront l'être le jour où
`qwen3:8b` tournera sous charge.

**Basculer vers un autre fournisseur compatible OpenAI ne demande que `base_url` et le nom du
modèle** — aucune ligne de logique de qualification n'en dépend.

## Trois règles qui commandent le reste

1. **Jamais un avis à moitié valide.** Soit un avis satisfaisant tous les invariants du domaine —
   liste jamais vide, hors périmètre exclusif, appartenance stricte aux sept valeurs —, soit un
   non-2xx. L'exclusivité du hors périmètre n'étant pas exprimable en schéma JSON, elle est vérifiée
   en code, ici : **un avis invalide n'est pas un avis faible, c'est une panne du moteur**.
2. **Le fil parle anglais.** Les droits et les degrés de confiance circulent sous les noms
   canoniques du domaine .NET. Le français est une contrainte *locale aux moteurs* : le lexique
   raisonne en slugs français, le LLM raisonne et justifie en français, et chacun traduit juste
   avant de répondre. Seule la justification traverse la frontière en français, parce qu'elle est
   destinée à un humain.
3. **La taxonomie n'est jamais recopiée.** Le sidecar lit
   [`data-subject-rights.wire.json`](../../data-subject-rights.wire.json) à l'import et **refuse de
   démarrer** si ce qu'il connaît en diverge. Le domaine commande, le fichier suit, Python lit.

## Développer

```sh
uv sync                                    # environnement virtuel et dépendances verrouillées
uv run pytest                              # la suite complète
uv run uvicorn qualification_sidecar.app:app --reload
```

Ainsi lancé, le sidecar démarre **sans modèle** : le lexique répond, le point d'entrée LLM rend son
refus nommé, et aucune variable n'est à poser. Allumer le moteur exige alors toutes celles du
tableau ci-dessus — c'est le prix du refus des valeurs par défaut. Le plus court est de laisser
Aspire les fournir ; sinon, sous PowerShell :

```powershell
$env:QUALIFICATION_LLM_ENABLED = "true"
$env:QUALIFICATION_LLM_BASE_URL = "http://localhost:11434/v1"
$env:QUALIFICATION_LLM_MODEL = "qwen3:8b"
$env:QUALIFICATION_LLM_API_KEY = "ollama-ne-lit-pas-cette-cle"
$env:QUALIFICATION_LLM_TEMPERATURE = "0"
$env:QUALIFICATION_LLM_SEED = "20180525"
$env:QUALIFICATION_LLM_DEADLINE_SECONDS = "120"
$env:QUALIFICATION_LLM_CALLER_DEADLINE_SECONDS = "150"
```

Depuis la racine du dépôt, la porte à passer avant une PR est `uv run --project src/sidecar pytest`.

Aspire lance le sidecar avec le reste de la pile — Postgres, Ollama et son volume de modèles
compris — d'une seule commande :

```sh
dotnet run --project src/MicroserviceRgpd.AspireHost
```

Le premier démarrage tire `qwen3:8b`, ce qui prend le temps que prend un modèle de plusieurs
gigaoctets ; le volume nommé fait que les suivants n'y reviennent pas. Une fois le tableau de bord
vert, un appel manuel rend un avis réel :

```sh
curl -X POST http://localhost:<port>/opinions/llm \
  -H "Content-Type: application/json" \
  -d '{"text":"Je change de banque, envoyez-moi mes relevés en CSV."}'
```

## Ce que la suite teste, et ce qu'elle ne teste pas

Elle tourne **sans réseau sortant, sans GPU et sans clé d'API** — c'est ce qui la rend exécutable
sur la machine du développeur pressé, exactement quand on en a besoin. **Aucun test n'appelle
Ollama** : un test qui exige un GPU est un test qui ne tourne jamais, et un test qui ne tourne
jamais ment. L'amont y est toujours un faux, ce que rend possible l'étroitesse du contrat que le
moteur LLM exige de lui — une consigne, un texte, une réponse.

- Les **règles de refus** de la frontière, une par une.
- Le **fichier de projection** confronté à la taxonomie vue du côté Python, symétrique du test
  unitaire .NET `WireTaxonomyProjectionTests`.
- Le **lexique rejoué sur le corpus témoin du dépôt, exemple par exemple**
  ([`tests/witness/`](tests/witness/)).
- La **configuration du moteur LLM** : chaque variable absente refusée par son nom, et l'inégalité
  stricte des deux échéances.
- Ce que le sidecar **accepte du modèle** : tout ce qui n'est pas un avis complet — JSON malformé,
  droits vides, confiance hors échelle, justification absente — est une panne, jamais un avis faible.
- La **traduction** des slugs français et des trois degrés de confiance vers les noms du fil, et le
  refus de replier un degré inconnu sur un degré connu.
- La **palette de codes**, et notamment que `502`, `503` et `504` ne se confondent jamais.
- Que le **lexique reste servi pendant qu'un appel LLM est en vol** — la propriété que le ticket
  précédent avait laissée à couvrir, faute d'un second point d'entrée pour la mettre à l'épreuve.

Le rejeu du corpus est de la **non-régression de code, pas une mesure de qualité** : aucun seuil,
aucun F₂, aucune métrique agrégée. Il dit « ce commit a changé le comportement du lexique sur cet
exemple, était-ce voulu ? ». Quand l'écart est voulu :

```sh
uv run python tests/witness/regenerate.py   # puis incrémenter lexicon.ENGINE_VERSION
```

La qualité de la qualification est **explicitement hors périmètre** : le seul chiffre disponible
serait emprunté à un autre moteur que celui qui sera servi. La remarque vaut doublement pour le
LLM — `qwen3:8b` n'a jamais été mesuré, et les chiffres du prototype viennent d'un tout autre
modèle.
