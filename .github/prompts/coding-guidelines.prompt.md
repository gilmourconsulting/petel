---
agent: agent, ask, edit, plan
description: Reference the correct instruction files before making any code change in the Petel monorepo.
---

# Coding Guidelines Router

**Start here:** [AGENTS.md](../AGENTS.md) and [docs/agents/INDEX.md](../docs/agents/INDEX.md)

Before any change, read [docs/agents/core/pre-processing.md](../docs/agents/core/pre-processing.md).

## Instruction map

| File | Applies To | Canonical path |
|---|---|---|
| Core platform | All files | [docs/agents/core/](../docs/agents/core/) |
| Blazor | `**/*.razor` | [docs/agents/core/blazor-patterns.md](../docs/agents/core/blazor-patterns.md) |
| PetelATH | `PetelATH/**` | [docs/agents/apps/petel-ath.md](../docs/agents/apps/petel-ath.md) |
| PetelAssistants | `PetelAssistants/**` | [docs/agents/apps/petel-assistants.md](../docs/agents/apps/petel-assistants.md) |

## Playbooks

- New Blazor page: [docs/agents/playbooks/new-blazor-page.md](../docs/agents/playbooks/new-blazor-page.md)
- New entity + API: [docs/agents/playbooks/new-entity-and-api.md](../docs/agents/playbooks/new-entity-and-api.md)
