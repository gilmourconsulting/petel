---
name: ath-blazor-page
description: >-
  Scaffold or add a PetelATH Blazor page, route, menu item, or SecureButton.
  Use when creating a new ATH screen, adding a page under PetelATH.BlazorServer,
  inserting a menu_items row, or wiring a new page action. For editing an
  existing page layout, use ath-blazor-ui instead.
---

# ATH Blazor page

Read these before writing files:

1. `docs/agents/playbooks/new-blazor-page.md`
2. `docs/agents/core/blazor-patterns.md`
3. `docs/agents/apps/petel-ath.md` (page checklist)

ATH only. Do not create or edit `PetelAssistants/**`.

## Before scaffolding

Ask for any of these the user has not given: English page name (route), Hebrew title, menu sort order, main entity, API prefix, table fields with Hebrew labels, actions, and whether the page needs the selected school or year.

## Required shape

- `Components/Pages/{Page}.razor` with `@page`, `@layout MainLayout`, `@inherits SecurePageBase`
- `PageName` matches the route. Load data in `OnPageInitializedAsync()`, not `OnInitializedAsync()`
- Blazor DTOs in `PetelATH.BlazorServer/DTOs/`. API DTOs in `PetelATH.Api/DTOs/`
- Controller inherits `BaseController`. No `[Authorize]`. Call `GetCurrentSession()` in each endpoint. Filter by `session.EntityId`
- Calls go through `ApiService`. Navigation goes through `NavigationManager`
- Action buttons are `<SecureButton>` with `ActionName`, `ScreenName`, and `OnClick`
- Menu comes from the database. Insert `petel_schema.menu_items` (`name`, `reference`, Hebrew `text`, `sort_order`, `is_active`). Do not edit `NavMenu.razor` to add the item

## Done when

The page, DTOs, controller, and menu SQL exist, and a button without a granted action stays hidden.
