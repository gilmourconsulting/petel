nstruction Files — Final Status
All JS SPA patterns have been removed from all instruction files. Here's what to use going forward:

File	applyTo	Purpose
copilot-instructions.md	All files	Backend patterns: DB/EF, auth, deployment, shared architecture
petelath.instructions.md	PetelATH/**	ATH-specific guide including Blazor page structure and component hierarchy
petelassistants.instructions.md	PetelAssistants/**	Assistants-specific scaffolding and roadmap
blazor-patterns.instructions.md	**/*.razor	NEW — Canonical Blazor patterns: SecurePageBase, ApiService, SessionStateService, modals, tables, SecureButton, anti-patterns
new-blazor-page.prompt.md	N/A	NEW — Agent mode scaffold prompt for new Blazor pages
Recommendation: When creating or modifying .razor files, Copilot will automatically pick up blazor-patterns.instructions.md. Use the new-blazor-page.prompt.md prompt when scaffolding a complete new page with its API controller and DTOs.