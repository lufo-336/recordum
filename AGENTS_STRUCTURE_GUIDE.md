# AGENTS.md Structure Guide

## Overview

This guide explains a generic hierarchical AGENTS.md system that can be reused in any repository.

The goal is to give AI agents and developers scoped guidance at each folder level while keeping rules consistent across the project.

---

## Core Principles

### 1. Hierarchical Scoping

- Root AGENTS.md: defines global project rules and architecture boundaries.
- Subdirectory AGENTS.md: adds local refinements for a specific folder.
- Cascading behavior: each level inherits parent guidance and narrows it.

### 2. Single Responsibility

- Each AGENTS.md should focus only on its folder domain.
- Keep global rules at root and local rules in the nearest scoped file.

### 3. Cross-Referencing

- Every child AGENTS.md should reference its parent (for example: "Vedi ../AGENTS.md").
- Parent AGENTS.md should list major child scopes it governs.

---

## Recommended Layout

### Root file

- Location: `./AGENTS.md`
- Purpose: project-wide governance

Suggested sections:

```markdown
# [Project]/[Root Scope]

## Ambito
- What this project is
- Scope boundaries

## Architettura da preservare
- Key modules and responsibilities
- Core relationships and constraints

## Regole operative
- Change rules
- Conventions
- Safety constraints

## Comandi utili
- Build/test/run commands

## Verifica
- How to validate changes

## Indice AGENTS.md
- List of child AGENTS.md files
```

### Subdirectory file

- Location: `./<subfolder>/AGENTS.md`
- Purpose: local guardrails and local verification

Suggested sections:

```markdown
# [Project]/[Subfolder]

## Ambito
- What belongs in this folder

## Regole operative
- Local rules and conventions
- Integration boundaries with other folders

## Verifica tipica
- Quick checks specific to this area

## Indice AGENTS.md
- Vedi `../AGENTS.md`
```

---

## Reading Order

1. Read root AGENTS.md.
2. Read each AGENTS.md along the path to the target folder.
3. Apply the most local rule when guidance overlaps.

---

## Example Generic Tree

```text
recordum/
├── AGENTS.md                          # Root: governance globale
├── CLAUDE.md                          # Contesto progetto dettagliato
├── AGENTS_STRUCTURE_GUIDE.md          # Questa guida
├── src/
│   ├── AGENTS.md                      # Progetti sorgente
│   ├── Recordum.Core/
│   ├── Recordum.Data/
│   ├── Recordum.Plugins/
│   ├── Recordum.Plugins.Contracts/
│   ├── Recordum.Plugins.ChartEngine/
│   ├── Recordum.Plugins.ExportEngine/
│   └── Recordum.WPF/
├── tests/
│   ├── AGENTS.md                      # Progetti di test
│   ├── Recordum.Core.Tests/
│   ├── Recordum.Data.Tests/
│   ├── Recordum.Plugins.Tests/
│   ├── Recordum.Plugins.ChartEngine.Tests/
│   ├── Recordum.Plugins.ExportEngine.Tests/
│   └── Recordum.WPF.Tests/
├── docs/
│   ├── AGENTS.md                      # Documentazione
│   ├── ARCHITECTURE.md
│   ├── MILESTONES.md
│   ├── phase-4-issues.md
│   ├── decisions/
│   │   └── 0001-ui-framework-wpf-over-avalonia.md
│   ├── design/
│   │   └── *.html (16 mockup UI)
│   └── superpowers/
│       ├── plans/ (47 file .md)
│       └── specs/ (39 file .md)
├── .claude/
│   ├── AGENTS.md                      # Configurazione Claude Code
│   ├── settings.json
│   ├── hooks/
│   │   ├── guard-main.js
│   │   ├── guard-generated.js
│   │   ├── guard-userdata.js
│   │   └── stop-dirty.js
│   └── skills/
│       └── run/SKILL.md
└── tools/
    └── plugin-api-ts/
```

---

## Implementation Checklist

1. Create root AGENTS.md with project scope and global rules.
2. Create AGENTS.md in each major folder.
3. Add parent-child cross-references.
4. Keep AGENTS files aligned with real folder structure.
5. Update AGENTS when architecture or responsibilities change.

---

## Best Practices

### Do

- Keep files concise and practical.
- Prefer concrete file and folder references.
- Separate global guidance from local guidance.
- Add simple verification steps that can be executed quickly.

### Do not

- Duplicate the same long rules in every folder.
- Keep outdated references after refactors.
- Mix unrelated domains in the same scoped file.

---

## Maintenance Rule

Whenever you add, remove, or rename a major folder, update:

1. Root AGENTS.md index.
2. Parent and child AGENTS cross-references.
3. Any structure guide or onboarding docs that describe AGENTS hierarchy.

---

## Version History

- v2.6 (2026-07-02): Conformata al progetto Recordum. Albero di esempio aggiornato con la struttura reale (src/, tests/, docs/, .claude/, tools/). Aggiunta `CLAUDE.md` e `AGENTS_STRUCTURE_GUIDE.md` all'albero. Created root AGENTS.md + subdirectory AGENTS.md per src, tests, docs, .claude.
