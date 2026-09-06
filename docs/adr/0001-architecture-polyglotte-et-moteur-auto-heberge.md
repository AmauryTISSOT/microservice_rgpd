# ADR-0001 — Architecture polyglotte et moteur de qualification auto-hébergé

- **Statut** : accepté
- **Date** : 2026-07-28
- **Décidé par** : [Trancher le moteur de qualification retenu pour la v1](https://github.com/AmauryTISSOT/microservice_rgpd/issues/7), consigné ici par [Assembler la spec finale de la fonctionnalité](https://github.com/AmauryTISSOT/microservice_rgpd/issues/12)
- **Détail d'implémentation** : [`docs/spec/qualification.md`](../spec/qualification.md)

## Contexte

Le service qualifie un texte libre français vers une taxonomie fermée de sept `DataSubjectRight`, en aide à la décision. Le caviardage des données personnelles est hors périmètre : une demande réelle arrive donc avec l'identité du demandeur, et souvent davantage.

Un prototypage comparé sur un corpus annoté de 120 exemples a opposé un lexique déterministe à un LLM. Le LLM domine nettement, mais le fait décisif n'est pas l'écart de score : les erreurs du LLM portent toutes une confiance non haute, donc une relecture ciblée les capte, là où les erreurs du lexique sont silencieuses, faute de tout signal de confiance.

Le dépôt était jusqu'ici entièrement .NET (Clean Architecture, FastEndpoints, EF Core, Aspire).

## Décision

Un hybride à deux moteurs qui s'entrecontrôlent, entièrement auto-hébergé, porté par un sidecar Python.

1. Un LLM open-weights local — `qwen3:8b` servi par Ollama en container Aspire — rend le **verdict**.
2. Le lexique déterministe rend un avis **témoin**. Il est détecteur, jamais contributeur en marche nominale ; contributeur de dernier recours quand le LLM est absent.
3. Leur divergence devient un `ReviewSignal` à trois valeurs, rendu à l'appelant.
4. Les deux moteurs vivent dans un sidecar Python (`Aspire.Hosting.Python`) derrière deux endpoints HTTP séparés. `IQualificationEngine` a deux implémentations en `Infrastructure` ; la règle de corroboration vit dans `Core`.
5. Aucun tiers. Le texte ne quitte pas le périmètre technique.

## Justification

**L'auto-hébergement plutôt qu'une API tierce.** Tout appel à un tiers serait une sous-traitance au sens de l'art. 28 du RGPD, sur un texte non caviardé. L'auto-hébergement supprime le sous-traitant, l'engagement contractuel et la question de la localisation par construction. Un microservice dont l'unique métier est la conformité RGPD ne peut pas être celui qui expédie des demandes d'exercice de droits chez un tiers.

**L'hybride plutôt qu'un moteur seul.** L'alarme cesse de reposer sur l'auto-évaluation du modèle. Les petits modèles sont réputés sur-confiants ; le lexique déclenche l'alarme de l'extérieur, sans rien demander au LLM. C'est un signal indépendant, structurellement plus solide qu'une introspection.

**Deux endpoints plutôt qu'un.** L'indépendance des deux avis devient structurelle. Un endpoint unique pourrait un jour faire dépendre un avis de l'autre sans que .NET le sache, et la corroboration deviendrait un théâtre. Bénéfice second : deux modes de panne séparés, d'où des comportements dégradés presque gratuits.

**Le sidecar Python, qui est le point le plus discutable.** Rien ne l'impose techniquement : le lexique n'importe que `re` et `unicodedata`, que .NET fait aussi bien. La frontière est posée pour un motif unique : une évolution vers l'apprentissage automatique est anticipée (plongements, classifieur entraîné, éventuel *fine-tuning*). Mieux vaut la poser maintenant, tant qu'elle est bon marché. Si ce motif tombait, cette décision serait à revoir. Bénéfice immédiat : le harnais d'évaluation qui a produit les chiffres du prototypage évalue désormais le code de production, et non une maquette.

## Conséquences

**Acquis**

- Le volet sous-traitant / DPA / localisation tombe par construction.
- Un signal de relecture indépendant, et un mode dégradé qui s'écrit presque gratuitement.
- `Core` ne bouge pas si un troisième moteur apparaît, ou si un moteur revient en C#.
- Portabilité du modèle réduite à un `base_url` et un nom de modèle, côté Python — Ollama comme Mistral, protocole compatible OpenAI identique.

**Coûts et contraintes**

- Le dépôt devient polyglotte : deux écosystèmes, deux suites de tests, deux chaînes de dépendances. L'exploitation du sidecar (image, packaging, démarrage à froid) reste une question ouverte.
- La taxonomie existe des deux côtés d'une frontière. Traitée par un fichier JSON de projection lu par Python et gardé par un test unitaire .NET, `Core` restant la source de vérité.
- Le GPU sérialise de fait les appels (8 Go de VRAM, classe 7–8B en plafond réel). Cela contraint le plafond de longueur d'entrée, interdit les reprises, et impose la propagation de l'annulation.
- `Microsoft.Extensions.AI` / `IChatClient` est écarté : l'appel LLM vivant en Python, cette abstraction n'aurait plus rien à abstraire.
- Tout test qui démarre l'`AppHost` démarrerait Ollama. `AspireTests` est vidé en conséquence, et aucun test .NET n'appelle jamais Ollama.

**Risque assumé**

⚠️ **La décision a été prise sans mesure de `qwen3:8b`.** Les chiffres du prototypage sont ceux d'un grand modèle propriétaire et ne s'appliquent pas à un modèle de 8 milliards de paramètres. Ni l'exactitude, ni la calibration réelle, ni le taux de désaccord effectif avec le lexique n'ont été mesurés sur le moteur retenu. Si `qwen3:8b` s'avérait nettement plus faible qu'espéré, le verdict se dégraderait et le taux de `Contested` grimperait bien au-delà des ~25 % extrapolés. Le montage à deux moteurs amortit ce risque ; il ne le supprime pas.

## Alternatives écartées

| Alternative | Motif du rejet |
| --- | --- |
| **LLM en API tierce** (Anthropic, Mistral, API majeure en région UE) | sous-traitance au sens de l'art. 28 sur un texte non caviardé |
| **Lexique seul** | ses erreurs sont silencieuses ; pour une aide à la décision, ne pas savoir dire qu'on doute est plus grave qu'un taux d'erreur |
| **LLM seul** | l'alarme reposerait sur l'auto-évaluation d'un petit modèle réputé sur-confiant |
| **Union des deux avis** | indéfinissable — `OutOfScope` étant exclusif, « `OutOfScope` ∪ `Erasure` » n'existe pas ; et l'union importerait le bruit du moteur faible |
| **Port du lexique en C#** | techniquement viable et même plus simple ; écarté au seul motif de la frontière anticipée vers l'apprentissage automatique |
| **Supervisé classique** (ML.NET) | pas de multi-étiquettes natif, modèle de base anglophone |

## Portée de cet ADR

Cet ADR ouvre la série, et il est le seul ouvert pour cette fonctionnalité. Les autres décisions de la carte — vocabulaire, contrat public, contrat interne, trace d'audit, comportements dégradés, stratégie de test — sont consignées dans [`docs/spec/qualification.md`](../spec/qualification.md) et dans les commentaires de résolution de leurs tickets, qui font foi sur le *pourquoi*. Elles n'ont pas la portée architecturale qui justifie un ADR : elles décrivent ce que fait ce service, là où celle-ci décide de quoi il est fait, et elle est la seule dont le renversement invaliderait les autres.
