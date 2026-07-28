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
| `GET /health` | — | `200` |

```jsonc
// requête — le texte, et rien d'autre
{ "text": "Supprimez toutes les données que vous avez sur moi." }

// 200
{ "rights": ["Erasure"], "engine": { "name": "lexicon", "version": "1.0.0" } }
```

La palette du lexique est **courte, et ce n'est pas un accident** : il n'a aucun amont, donc ni
`502`, ni `503`, ni `504` ne peuvent survenir. C'est ce qu'achètent deux points d'entrée séparés.

**Aucune confiance déclarée.** Le lexique n'a pas d'avis sur sa propre fiabilité ; une constante lui
en donnerait l'apparence, et quelqu'un finirait par écrire une règle qui la consomme.

**Aucun identifiant maison.** La corrélation avec l'appelant passe par l'en-tête `traceparent`, que
`ServiceDefaults` propage déjà. Les erreurs sortent en `application/problem+json`.

## Trois règles qui commandent le reste

1. **Jamais un avis à moitié valide.** Soit un avis satisfaisant tous les invariants du domaine —
   liste jamais vide, hors périmètre exclusif, appartenance stricte aux sept valeurs —, soit un
   non-2xx. L'exclusivité du hors périmètre n'étant pas exprimable en schéma JSON, elle est vérifiée
   en code, ici : **un avis invalide n'est pas un avis faible, c'est une panne du moteur**.
2. **Le fil parle anglais.** Les droits circulent sous les noms canoniques du domaine .NET. Le
   français est une contrainte *locale aux moteurs* : le lexique raisonne en slugs français et
   traduit juste avant de répondre.
3. **La taxonomie n'est jamais recopiée.** Le sidecar lit
   [`data-subject-rights.wire.json`](../../data-subject-rights.wire.json) à l'import et **refuse de
   démarrer** si ce qu'il connaît en diverge. Le domaine commande, le fichier suit, Python lit.

## Développer

```sh
uv sync                                    # environnement virtuel et dépendances verrouillées
uv run pytest                              # la suite complète
uv run uvicorn qualification_sidecar.app:app --reload
```

Depuis la racine du dépôt, la porte à passer avant une PR est `uv run --project src/sidecar pytest`.

Aspire lance le sidecar avec le reste de la pile : `dotnet run --project src/MicroserviceRgpd.AspireHost`.

## Ce que la suite teste, et ce qu'elle ne teste pas

Elle tourne **sans réseau sortant, sans GPU et sans clé d'API** — c'est ce qui la rend exécutable
sur la machine du développeur pressé, exactement quand on en a besoin.

- Les **règles de refus** de la frontière, une par une.
- Le **fichier de projection** confronté à la taxonomie vue du côté Python, symétrique du test
  unitaire .NET `WireTaxonomyProjectionTests`.
- Le **lexique rejoué sur le corpus témoin du dépôt, exemple par exemple**
  ([`tests/witness/`](tests/witness/)).

Ce dernier point est de la **non-régression de code, pas une mesure de qualité** : aucun seuil,
aucun F₂, aucune métrique agrégée. Il dit « ce commit a changé le comportement du lexique sur cet
exemple, était-ce voulu ? ». Quand l'écart est voulu :

```sh
uv run python tests/witness/regenerate.py   # puis incrémenter lexicon.ENGINE_VERSION
```

La qualité de la qualification est **explicitement hors périmètre** : le seul chiffre disponible
serait emprunté à un autre moteur que celui qui sera servi.
