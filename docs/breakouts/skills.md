# Skills

This page documents the five repository skills under `.github/skills/`. Skills
are reusable knowledge packs that Copilot can invoke when a request matches a
specific domain, convention, or verification workflow.

## Folder convention

Each skill lives at:

```text
.github/skills/<skill-name>/SKILL.md
```

`SKILL.md` starts with frontmatter containing at least `name` and
`description`. Some skills also declare `license` or `model`. The body provides
domain rules, examples, checklists, and output formats.

## How skills differ from agents and instructions

- **Skills** are topical references. They teach Copilot how to handle a
  specific kind of work, such as frontend generation or ML model review.
- **Agents** in `.github/agents/` are named personas with their own tools,
  prompts, and sometimes pinned models.
- **Instructions files** such as `.github/copilot-instructions.md` and
  `.github/copilot-review-instructions.md` are broad repository context that
  applies across many tasks.

## `brand-guidelines`

- **Path**: `.github/skills/brand-guidelines/SKILL.md`
- **Frontmatter name**: `brand-guidelines`
- **Frontmatter description**:
  "Guides AI agents when generating branded aviation and loyalty program
  websites. Use this skill when building marketing pages, product catalogs,
  hero sections, loyalty points displays, or any customer-facing UI for airline
  and frequent flyer contexts. Provides a complete visual design system with
  colors, typography, spacing, components, and layout patterns."
- **Other frontmatter**: `license: MIT`
- **Covers**: Colour tokens, typography, spacing, product cards, loyalty
  points displays, navigation, CTA buttons, hero sections, footers, and a brand
  compliance checklist.
- **When Copilot would invoke it**: When asked to build or review branded
  marketing pages, product catalogues, loyalty points UIs, hero sections, or
  customer-facing airline/frequent flyer experiences.

## `data-pipeline-conventions`

- **Path**: `.github/skills/data-pipeline-conventions/SKILL.md`
- **Frontmatter name**: `data-pipeline-conventions`
- **Frontmatter description**:
  "Enforces data pipeline coding conventions for retail analytics. Use this
  skill when writing or reviewing ETL code, data ingestion, transformation
  logic, or data quality checks. Covers schema management, idempotency,
  validation, partitioning, and observability patterns for production data
  pipelines."
- **Other frontmatter**: `license: MIT`
- **Covers**: Idempotent ETL, schema management, validation, dead-letter
  handling, partitioning, batching, observability, data quality checks, naming
  conventions, and pipeline tests.
- **When Copilot would invoke it**: When writing or reviewing ETL code, data
  ingestion, transformations, schema evolution, validation, or data quality
  checks for retail analytics.

## `demo-verifier`

- **Path**: `.github/skills/demo-verifier/SKILL.md`
- **Frontmatter name**: `demo-verifier`
- **Frontmatter description**:
  "**CRITICAL: Use this skill to verify demo capabilities.** Reads all demo
  scripts (docs/demos/demo-script*.md) and cross-references every claimed
  capability against current GitHub documentation. Reports what is confirmed,
  in preview, or deprecated. MUST be invoked when reviewing demo accuracy,
  checking feature availability, or preparing for a live demo."
- **Other frontmatter**: `license: MIT`
- **Covers**: Reading demo scripts, extracting claimed capabilities,
  cross-referencing them with current GitHub documentation, and reporting
  whether capabilities are confirmed, in preview, changed, or unavailable.
- **When Copilot would invoke it**: Before a live demo, after updating demo
  scripts, when checking demo accuracy, or when asked to verify feature
  availability.

## `frontend-conventions`

- **Path**: `.github/skills/frontend-conventions/SKILL.md`
- **Frontmatter name**: `frontend-conventions`
- **Frontmatter description**:
  "Guides AI agents when generating frontend websites from scratch. Use this
  skill when creating HTML pages, styling with CSS, adding JavaScript
  interactivity, or building static sites. Covers semantic HTML5, modern CSS
  patterns, accessibility, performance, and component patterns for
  production-ready frontends."
- **Other frontmatter**: `license: MIT`, `model: gpt-5.4`
- **Covers**: Semantic HTML5, landmarks, heading hierarchy, CSS custom
  properties, Grid, Flexbox, responsive typography, mobile-first breakpoints,
  component patterns, accessibility, performance, JavaScript patterns, and a
  frontend review checklist.
- **When Copilot would invoke it**: When asked to build a website, landing
  page, static frontend, HTML/CSS/JavaScript page, or when reviewing frontend
  code for accessibility or performance.

## `ml-model-review`

- **Path**: `.github/skills/ml-model-review/SKILL.md`
- **Frontmatter name**: `ml-model-review`
- **Frontmatter description**:
  "Reviews ML model code for correctness, fairness, and production readiness.
  Use this skill when writing or reviewing customer segmentation, prediction
  models, feature engineering, or model evaluation code. Checks for data
  leakage, bias, reproducibility, and deployment risks in retail analytics ML
  pipelines."
- **Other frontmatter**: `license: MIT`
- **Covers**: Data leakage, feature engineering, configurable thresholds,
  evaluation metrics, bias and fairness checks, reproducibility, production
  readiness, drift monitoring, and an ML review checklist.
- **When Copilot would invoke it**: When writing or reviewing customer
  segmentation, prediction models, feature engineering, model evaluation, or
  retail analytics ML pipeline code.

## Related

- [Custom agents](./custom-agents.md)
- [Architecture](./architecture.md)
- [Skills folder](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/tree/main/.github/skills)
- [Shared Copilot instructions](https://github.com/vicperdana/aigenius-copilotsdk-s5ep2/blob/main/.github/copilot-instructions.md)
- [Demo materials](../demos/)
