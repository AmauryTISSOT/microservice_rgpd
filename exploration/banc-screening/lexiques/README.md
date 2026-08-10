# Lexiques gelés du banc de `Screening`

Les trois lexiques du banc d'essai du moteur de dépistage
([#134](https://github.com/AmauryTISSOT/microservice_rgpd/issues/134)), rédigés **en aveugle** et
gelés par le commit qui les introduit, en exécution de
[#155](https://github.com/AmauryTISSOT/microservice_rgpd/issues/155) — le gel amendé décidé par
[#154](https://github.com/AmauryTISSOT/microservice_rgpd/issues/154) et posté sur
[#130](https://github.com/AmauryTISSOT/microservice_rgpd/issues/130).

| Fichier | Rôle | Montage |
| --- | --- | --- |
| [`dictionnaire-fr.tsv`](./dictionnaire-fr.tsv) | Dictionnaire français, un jeton par entrée. | règles + lexique, FR |
| [`dictionnaire-en.tsv`](./dictionnaire-en.tsv) | Dictionnaire anglais, un jeton par entrée. | règles + lexique, EN |
| [`ligne-de-base.tsv`](./ligne-de-base.tsv) | 30–50 noms de colonnes entiers, bilingues. | ligne de base triviale |

Le montage **FR+EN** est l'**union mécanique** des deux dictionnaires ; il n'a pas de fichier
propre, et en avoir un serait une occasion d'éditer. Deux termes portent des valeurs différentes
dans les deux langues — `conviction` (`SpecialCategoryData` en FR, `CriminalOffenceData` en EN) et
`coord` (`ContactDetails` en FR, `LocationData` en EN). Ce n'est pas un défaut à corriger : dans
l'union, les deux entrées déclenchent, et c'est l'**ordre d'arbitrage** de la taxonomie — hérité,
jamais redécidé — qui tranche, comme pour tout double déclenchement.

## La règle de rédaction, et sa preuve

Tout lexique du banc — dictionnaires des montages **et** ligne de base — a été rédigé par un
**sous-agent sans aucun accès au dépôt**. Seule entrée : la section `PersonalDataCategory` de
[`docs/contexts/screening/CONTEXT.md`](../../../docs/contexts/screening/CONTEXT.md), extraite
verbatim, plus l'énoncé de tâche. Le § 3 du protocole d'annotation était **interdit d'entrée** : son
§ 3.9 cite des noms du corpus, la fuite aurait été dans l'entrée même. Les deux seuls noms de
colonnes cités dans l'entrée, `arret_maladie` et `email_pro`, sont les exemples inventés de la
documentation, déclarés comme tels dans chaque transcription.

La preuve est dans [`transcriptions/`](./transcriptions/) : une transcription par lexique, **entrée
comprise** (l'énoncé intégral et la section verbatim), versionnée **dans le même commit** que les
fichiers. Chaque fichier `.tsv` est l'extraction mécanique du bloc `tsv` de la sortie du rédacteur ;
**aucune entrée n'a été éditée** après rédaction.

## Le gel

Prédicat de la cause ③ amendée de #130 : lexiques et transcriptions commités **avant le premier
commit d'exécution du banc**, aucune édition ultérieure — constaté dans `git log`. Toute édition
d'une entrée après le commit d'introduction rend le banc **non concluant**. Dès le premier chiffre
de moteur, la liste des causes closes est inamendable.
