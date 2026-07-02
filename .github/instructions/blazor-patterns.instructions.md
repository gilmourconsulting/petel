---
applyTo: '**/*.razor'
---

# Blazor Server Patterns

**Read the full guide:** [docs/agents/core/blazor-patterns.md](../../docs/agents/core/blazor-patterns.md)

## Critical inline rules

- `@inherits SecurePageBase`; override `OnPageInitializedAsync()` — never `OnInitializedAsync()`
- Use `ApiService` for all HTTP; `SessionStateService` for session; `NavigationManager` for navigation
- No HTML SPA, sessionStorage, raw HttpClient, hardcoded API URLs, or emoji icons (use PNGs in `/images/`)
