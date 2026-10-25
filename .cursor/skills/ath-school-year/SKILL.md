---
name: ath-school-year
description: >-
  Change PetelATH school-year setup: copy definitions from the previous Hebrew
  year, open a school year for a network, or edit school_year_attributes. Use
  when the user mentions copying a year, opening a school year, year
  definitions, tracks, pricing for a year, or year attributes.
---

# ATH school year

Read the school-year sections of `docs/agents/apps/petel-ath.md` before editing: School Year Attributes, Copy school year definitions, and Open a school year for a network.

ATH only. Do not edit `PetelAssistants/**`.

## Copy definitions

`SchoolYearConfig` copies from the previous `hebrew_years` row only when the target year has no rows in the five tabs (document types, pricing elements, tracks, study-program pricing, school attribute types).

Copied with new ids and remapped foreign keys: document types, special-needs pricing, tracks and levels and pricing, additional-study pricing, school attribute types and values, and `school_year_attributes`.

Not copied: per-school `school_attributes`, school track assignments, documents, students, `school_years` rows.

Do not clone a year through export/import. After a successful copy, refresh `SchoolAttributeCache`. Action: `schoolyearconfig_copyFromPreviousYear`.

## Open a year

`/openschoolyear` opens the session year for schools that existed in the previous Hebrew year. Confirm writes schools in one transaction. A school that already has a `school_years` row for the target year stays disabled.

Block confirm when `school_attributes_types` are missing on the target year. The user must copy definitions first.

Created per checked school: `school_years`, schools version 1, `school_classes` from the submitted list, and last-version `school_attributes` rematched by name.

Not copied: students, tracks, additional study programs, sign-language translators, documents. A list attribute with no matching option on the target year is saved empty and returned in `skippedAttributes`.

Actions: `openschoolyear`, `schoollist_openSchoolYear`, `openschoolyear_confirm`.
