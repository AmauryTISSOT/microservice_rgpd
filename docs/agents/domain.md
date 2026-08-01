# Domain Docs

How the engineering skills should consume this repo's domain documentation when exploring the codebase.

## Before exploring, read these

**This repo is multi-context.** There is no root `CONTEXT.md` — it moved into its context when the
second one was created (ADR-0002).

- **`CONTEXT-MAP.md`** at the repo root — the index. It names the two contexts, states how they
  relate, and carries the one term that is true on both sides (`Aide à la décision`). Read it first.
- **`docs/contexts/<context>/CONTEXT.md`** — the glossary of each context. Read the one your topic
  belongs to. Read *both* only if you are genuinely working across the boundary.
- **`docs/adr/`** — system-wide decisions. Read the ones touching the area you're about to work in.
- **`docs/contexts/<context>/adr/`** — context-scoped decisions. None exist yet; create the folder
  lazily if one is ever needed.

If any of these files don't exist, **proceed silently**. Don't flag their absence; don't suggest
creating them upfront. The `/domain-modeling` skill (reached via `/grill-with-docs` and
`/improve-codebase-architecture`) creates them lazily when terms or decisions actually get resolved.

## File structure

This repo's actual layout:

```
/
├── CONTEXT-MAP.md                     ← index, relationships, system-wide language
├── docs/
│   ├── adr/                           ← system-wide decisions
│   │   ├── 0001-architecture-polyglotte-et-moteur-auto-heberge.md
│   │   └── 0002-deux-contextes-bornes-et-noyau-partage.md
│   └── contexts/
│       ├── qualification/CONTEXT.md   ← l'instant du verdict
│       └── casework/CONTEXT.md        ← la durée de l'instruction
└── src/                               ← DÉCOUPÉ PAR COUCHE, pas par contexte
    ├── MicroserviceRgpd.Core/
    │   ├── SharedKernel/              ← DataSubjectRight — n'appartient à aucun contexte
    │   ├── Qualifications/
    │   └── Casework/
    ├── MicroserviceRgpd.UseCases/
    ├── MicroserviceRgpd.Infrastructure/
    └── MicroserviceRgpd.Web/
```

⚠️ **Why `docs/contexts/` and not `src/<context>/`** — the layout a generic multi-context repo would
use. `src/` here is partitioned by **layer** (Clean Architecture), and each bounded context **cuts
across all four layers**. There is no single `src/<context>/` directory to host a `CONTEXT.md`.
Inside each layer, contexts are readable by folder name.

## Don't mix the two glossaries

A term means one thing **within a context**, not within the repo. Two homonyms on either side of the
boundary are legitimate. Before using a term, know which context you are in — and if your work
crosses the boundary, know that only `DataSubjectRight` and an opaque `qualificationId` cross it.

## Use the glossary's vocabulary

When your output names a domain concept (in an issue title, a refactor proposal, a hypothesis, a test name), use the term as defined in the `CONTEXT.md` **of the context you are in**. Don't drift to synonyms the glossary explicitly avoids.

If the concept you need isn't in the glossary yet, that's a signal — either you're inventing language the project doesn't use (reconsider) or there's a real gap (note it for `/domain-modeling`).

## Flag ADR conflicts

If your output contradicts an existing ADR, surface it explicitly rather than silently overriding:

> _Contradicts ADR-0007 (event-sourced orders) — but worth reopening because…_
