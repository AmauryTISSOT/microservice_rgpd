# ADR-0002 — Deux contextes bornés, séparés par le temps, et un noyau partagé d'un seul type

- **Statut** : accepté
- **Date** : 2026-08-01
- **Décidé par** : [Nommer le second contexte borné et poser son langage](https://github.com/AmauryTISSOT/microservice_rgpd/issues/67)
- **Carte** : [Brancher l'exercice des droits sur une application tierce](https://github.com/AmauryTISSOT/microservice_rgpd/issues/57)
- **Glossaires** : [`CONTEXT-MAP.md`](../../CONTEXT-MAP.md), [Qualification](../contexts/qualification/CONTEXT.md), [Casework](../contexts/casework/CONTEXT.md)

## Contexte

Le dépôt n'a longtemps su faire qu'une chose : **qualifier** un texte libre français vers la
taxonomie fermée des sept `DataSubjectRight`. Son `CONTEXT.md` était explicite — *« il n'existe
aucune entité de demande instruite dans le temps »*.

La carte #57 a renversé cette posture. Accompagner l'exercice des droits suppose un objet qui vit
dans la durée : un dossier qui s'ouvre, se traîne, se clôt, avec des acteurs concurrents et des
invariants à tenir. [#64](https://github.com/AmauryTISSOT/microservice_rgpd/issues/64) a établi que
ce dossier est un agrégat. L'argument décisif : la règle « lire avant d'effacer » traverse deux
droits d'une même demande, et deux écritures valides isolément suffiraient à effacer avant d'avoir
lu, sans que rien ne « plante ».

Deux natures cohabitent donc désormais dans le même service. La qualification est un instant sans
invariant à tenir — c'est pourquoi la carte #1 lui avait refusé tout agrégat. L'instruction est une
durée. Les faire vivre sous un seul glossaire obligerait des termes à signifier deux choses ; les
séparer sans règle d'échange les ferait dériver.

## Décision

Deux contextes bornés, séparés par le temps, et un noyau partagé restreint à un seul type.

1. **`Qualification`** — l'instant du verdict. Inchangé.
2. **`Casework`** — la durée de l'instruction. Son agrégat est le `Case` ; le contexte porte donc le
   nom de son agrégat.
3. **Noyau partagé : `DataSubjectRight`, et rien d'autre.** Il quitte `Core/Qualifications/` pour
   `Core/SharedKernel/`.
4. **`Qualification` est un fournisseur amont optionnel** de `Casework`. Seul un `qualificationId`
   opaque traverse, jamais déréférencé.
5. **Aucune dépendance de compilation `Casework` → `Qualification`**, gardée par un test au niveau
   de l'IL.
6. **`Separate Ways`** pour tout le reste, sans couche anticorruption.

## Justification

**Pourquoi la frontière passe par le temps.** C'est la seule ligne où les invariants changent de
nature. À gauche, aucun invariant à tenir, donc aucun agrégat. À droite, un invariant qui traverse
deux droits, donc un agrégat obligatoire. Une frontière posée ailleurs couperait cet invariant en
deux.

**Pourquoi le noyau partagé, et pas un langage publié.** La taxonomie n'est le modèle d'aucun des
deux contextes : son auteur est le RGPD, articles 15 à 21. Les deux s'y conforment, aucun ne la
possède — c'est la définition même du noyau partagé, un sous-ensemble petit que personne ne modifie
unilatéralement. Sa clause de gouvernance est déjà écrite en majuscules dans
`data-subject-rights.wire.json` depuis l'ADR-0001.

La projection sur le fil existe parce que le sidecar est un processus séparé en Python, où il faut
un second exemplaire de la taxonomie. Recréer une projection entre deux contextes de la même
solution .NET paierait le prix d'une frontière de déploiement qu'on n'a pas, contre un risque de
dérive que le type unique rend inexistant.

**Pourquoi le noyau doit rester minuscule.** Un noyau partagé qui grossit recouple deux contextes en
douce. Le critère est écrit : n'y entre que ce qui est vrai des deux côtés sans exception.
`Capability` échoue au test et reste dans `Casework`.

**Pourquoi l'optionnalité doit être mécanique.** Une demande peut arriver déjà qualifiée. Si
`Casework` pouvait référencer `Qualification`, rien n'empêcherait un appel au moteur d'apparaître
dans un corps de méthode — et la promesse « le second contexte se démontre sans GPU » deviendrait
invérifiable, avec un lot 1 traînant Ollama derrière lui.

**Pourquoi un test du code compilé, et pas des signatures.** Un test par réflexion ne lit que des
signatures. Il attraperait un `Case` portant un `ReviewSignal` en propriété, mais passerait au vert
sur un gestionnaire à la signature propre qui appelle le moteur dans son corps — le mode de fuite
exact que l'on craint. La carte a refusé plusieurs gardes pour ce motif : le manifeste qui « pourrit
en silence », la case recopiée qui « vaut zéro en affichant vert ». Une garde qui ment est pire
qu'une garde absente.

**Pourquoi pas de couche anticorruption.** Il n'y a rien à traduire : le noyau partagé n'en a pas
besoin, et on ne traduit pas un identifiant opaque.

## Conséquences

**Acquis**

- Deux glossaires qui peuvent diverger sans se contredire. Deux régimes d'erreur opposés —
  `Erreur relue` et `Omission silencieuse` — sont enfin dicibles, là où un glossaire unique les
  forçait sous un seul nom et rendait la justification de l'un fausse pour l'autre.
- `Casework` se construit et se démontre sans GPU, sans Ollama, sans sidecar.
- Le layout multi-contexte rend le dépôt lisible par les agents : chacun lit le glossaire du
  contexte qu'il touche, pas les deux.

**Coûts et contraintes**

- `DataSubjectRight.cs` doit bouger. S'il reste dans `Core/Qualifications/`, la décision est morte :
  référencer les droits reviendrait à référencer le contexte entier.
- `System` est un nom interdit pour le type de domaine correspondant : c'est `DeclaredSystem`.
- Une dépendance de test nouvelle (`NetArchTest.Rules`), sur un paquet de test uniquement.
  *Mise en œuvre, [#84](https://github.com/AmauryTISSOT/microservice_rgpd/issues/84) :* le paquet
  retenu est `Mono.Cecil`, non `NetArchTest.Rules`. La décision ne change pas — c'est bien l'IL qui
  est lu — mais Cecil le lit directement, là où `NetArchTest` intercale ses propres prédicats sur
  ce que la règle sait exprimer. Le garde vit dans `tests/MicroserviceRgpd.ArchitectureTests/`.
- La frontière est détectable, pas impossible. Le test la rend bruyante ; seuls des assemblages
  séparés la rendraient infranchissable. C'est le troc assumé, « bruyant » étant le critère que la
  carte a retenu ailleurs. L'option des projets séparés reste ouverte au lot 1.
- `docs/agents/domain.md` s'écarte de son propre layout générique : les `CONTEXT.md` vivent sous
  `docs/contexts/<contexte>/` et non `src/<contexte>/`, parce que `src/` est découpé par couche et
  que chaque contexte traverse les quatre.

**Risque assumé**

⚠️ **Le glossaire de `Casework` a été écrit avant que la moindre ligne de `Casework` n'existe.**
Il est entièrement dérivé des résolutions de la carte #57, sans confrontation au code. Le lot 1 de
[#68](https://github.com/AmauryTISSOT/microservice_rgpd/issues/68) est le premier moment où il
rencontrera la réalité, et il faut s'attendre à ce qu'il bouge — c'est un glossaire, pas un contrat.
Le noyau partagé, lui, ne bouge pas : sa liste est celle du législateur.

## Alternatives écartées

| Alternative | Motif du rejet |
| --- | --- |
| **Un seul contexte étendu** | forcerait `Aide à la décision` et le régime d'erreur à signifier deux choses ; la justification « une erreur coûte peu » est fausse dans la durée |
| **Contexte nommé `Fulfilment`** (le mot du terrain) | promet l'accomplissement dans le nom même, contredisant « enregistré, jamais vérifié » dans chaque `using` — la raison exacte qui a fait refuser un état `Honoré` |
| **Langage publié** entre les deux contextes | paie une frontière de déploiement inexistante ; deux types à tenir contre une dérive que le type unique rend impossible |
| **Noyau partagé élargi** (`Capability`, taxonomie des états) | recouplerait les contextes en douce ; échoue au critère « vrai des deux côtés sans exception » |
| **Conformist** (`Casework` prend le modèle de `Qualification` tel quel) | tuerait l'optionnalité : plus de `Casework` sans qualification, donc plus de démonstration sans GPU |
| **Test d'architecture par réflexion** | affiche vert sur un appel en corps de méthode — la fuite même que l'on craint |
| **Projets .NET séparés** | seule garantie infranchissable, mais restructure la solution ; laissé ouvert au lot 1 |
| **`CONTEXT.md` sous `src/<contexte>/`** | il n'existe aucun `src/<contexte>/` : `src/` est découpé par couche, chaque contexte traverse les quatre |

## Portée de cet ADR

Cet ADR décide de quoi le service est fait — le critère posé par la « Portée » de l'ADR-0001. Le
reste des décisions de #67 — les identifiants `Case`, `Claim`, `Step`, `EvidenceLog`,
`RetrievedData` et les autres — décrit ce que fait le service : c'est du glossaire, et il vit dans
les `CONTEXT.md` et dans le commentaire de résolution de #67, qui fait foi sur le *pourquoi*.

⚠️ **L'ADR-0001 précède ce découpage et n'a pas été réécrit** : on supplante un ADR, on ne l'édite
pas. Il se lit comme un ADR de système, sa clause porteuse pour `Casework` étant l'auto-hébergement
intégral et le refus de toute sous-traitance au sens de l'art. 28. Sa phrase *« il est le seul
ouvert pour cette fonctionnalité »* reste vraie : celui-ci porte sur un autre contexte.
