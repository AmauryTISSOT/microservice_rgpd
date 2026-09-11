# ADR-0022 — Supprimer une demande ne laisse aucune trace

- **Statut** : accepté
- **Date** : 2026-09-11
- **Décidé par** : l'US « Consulter la liste des demandes RGPD »
- **Glossaires** : [`CONTEXT-MAP.md`](../../CONTEXT-MAP.md), [Requests](../contexts/requests/CONTEXT.md)
- **Ne supplante aucun ADR.** Il précise la langue de système : tout acte de l'`Operator` n'est pas
  un `Gesture`.

## Contexte

La langue de système définit le `Gesture` comme un acte posé par l'`Operator` ou par le service, qui
laisse des traces datées se tenant seules. Enregistrer une demande en est un.

L'`Operator` doit pouvoir retirer une demande du tableau pour de bon — une saisie en double, une
demande enregistrée par erreur —, et ce retrait ne se confond pas avec l'annulation, qui conserve la
demande sous le statut « Annulée ».

## Décision

**Supprimer une demande la retire définitivement**, quel que soit son statut : la ligne de
`data_subject_requests` disparaît, et rien d'autre n'est écrit — ni date, ni auteur, ni identifiant
opaque.

⚠️ **Ce n'est donc pas un `Gesture`.** Un `Gesture` laisse une trace ; la suppression efface celle
qu'avait laissée l'enregistrement. Le glossaire de `Requests` l'écrit sous « Supprimer une
demande », et la langue de système porte l'exception sous l'entrée `Gesture`.

**La suppression est un handler de la page, pas une API** — `POST /demandes?handler=Delete`, avec le
jeton anti-rejeu, comme la création. Une demande qui n'existe plus rend 404, et l'écran le lit comme
une réussite : ce que l'`Operator` voulait, que la demande ne soit plus là, est acquis. La ligne
disparaît au lieu de rester impossible à supprimer.

## Les options écartées

- **Laisser une trace de suppression** — quand, par qui, quelle demande sous un identifiant
  opaque, sans donnée personnelle. Elle servirait la responsabilité de l'art. 5.2 : prouver qu'une
  demande reçue n'a pas été escamotée. Écartée pour cette US, qui n'en demande pas ; elle pourra
  s'ajouter sans toucher au statut, et c'est pourquoi cette décision a son propre ADR.
- **Une suppression logique** — une colonne `deleted_at`, la demande cachée mais gardée. C'est une
  annulation sous un autre nom, et elle garderait les données personnelles que la suppression doit
  faire partir.
- **Ne permettre de supprimer qu'une demande annulée.** L'US veut la suppression pour tous les
  statuts, et une saisie en double n'a pas à être annulée avant d'être supprimée.

## Conséquences, y compris celles qui coûtent

⚠️ **Le service ne peut plus prouver qu'une demande supprimée a été reçue.** La preuve, s'il en
faut une, vit là où l'`Operator` l'a reçue — la boîte mail, le courrier.

⚠️ **Le mot « effacer » reste au droit à l'effacement** (art. 17). Supprimer une demande qui
invoque l'effacement ne l'exerce pas.
