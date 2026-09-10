# Graph Report - frontend  (2026-09-11)

## Corpus Check
- 82 files · ~65,988 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 351 nodes · 519 edges · 26 communities (20 shown, 6 thin omitted)
- Extraction: 99% EXTRACTED · 1% INFERRED · 0% AMBIGUOUS · INFERRED: 7 edges (avg confidence: 0.84)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `62bbb4b6`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- package.json
- devDependencies
- Vue 3 Vite Frontend Template
- vue
- scripts
- .oxlintrc.json
- .prettierrc.json
- jsconfig.json
- local-adapters.js
- BEM CSS Naming
- Composition API
- Vite App Document Title
- VerifyEmailForm.vue
- SignUpForm.vue
- vue-router
- ResetPasswordForm.vue
- ProjectsPage.vue
- Stage 1 — Interface and Local Contract Data
- vitest
- Dionysus Frontend Foundation Design
- CreateProjectDialog.vue
- ProjectSearch.vue
- Task 4 evidence
- runtime.js
- task-3-report.md
- task-5-report.md

## God Nodes (most connected - your core abstractions)
1. `vue-router` - 18 edges
2. `vue` - 17 edges
3. `vitest` - 17 edges
4. `@vue/test-utils` - 14 edges
5. `validatePassword()` - 11 edges
6. `scripts` - 9 edges
7. `Dionysus Frontend Foundation Design` - 9 edges
8. `validateEmail()` - 8 edges
9. `Stage 1 — Interface and Local Contract Data` - 8 edges
10. `setSession()` - 7 edges

## Surprising Connections (you probably didn't know these)
- `Vite` --semantically_similar_to--> `Vue 3`  [INFERRED] [semantically similar]
  README.md → AGENTS.md
- `src/main.js Entry Script` --conceptually_related_to--> `Vite`  [INFERRED]
  index.html → README.md
- `submitForm()` --calls--> `verifyEmail()`  [EXTRACTED]
  src/features/verify-email/VerifyEmailForm.vue → src/entities/session/api.js
- `getProjects()` --calls--> `getLocalProjects()`  [EXTRACTED]
  src/entities/project/api.js → src/shared/api/local-adapters.js
- `loadProjects()` --calls--> `getProjects()`  [EXTRACTED]
  src/pages/projects/ProjectsPage.vue → src/entities/project/api.js

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Frontend Quality Workflow** — readme_npm_install, readme_npm_dev, readme_npm_build, readme_npm_lint [EXTRACTED 1.00]
- **Frontend Architecture Stack** — agents_vue_3, agents_composition_api, agents_javascript, readme_vite [INFERRED 0.85]

## Communities (26 total, 6 thin omitted)

### Community 0 - "package.json"
Cohesion: 0.08
Nodes (26): dependencies, pinia, vue, vue-router, engines, node, name, private (+18 more)

### Community 1 - "devDependencies"
Cohesion: 0.11
Nodes (19): devDependencies, eslint, eslint-config-prettier, @eslint/js, eslint-plugin-oxlint, eslint-plugin-vue, globals, jsdom (+11 more)

### Community 2 - "Vue 3 Vite Frontend Template"
Cohesion: 0.20
Nodes (11): Feature-Sliced Design, Single-File Components, Vue 3, Application Mount Point, src/main.js Entry Script, Vue 3 Vite Frontend Template, npm run build, npm run dev (+3 more)

### Community 3 - "vue"
Cohesion: 0.06
Nodes (23): pinia, vue, currentPassword, email, emailSaved, newPassword, passwordConfirmation, passwordSaved (+15 more)

### Community 4 - "scripts"
Cohesion: 0.22
Nodes (9): scripts, build, dev, format, lint, lint:eslint, lint:oxlint, preview (+1 more)

### Community 5 - ".oxlintrc.json"
Cohesion: 0.29
Nodes (6): categories, correctness, env, browser, plugins, $schema

### Community 6 - ".prettierrc.json"
Cohesion: 0.40
Nodes (4): printWidth, $schema, semi, singleQuote

### Community 7 - "jsconfig.json"
Cohesion: 0.50
Nodes (3): compilerOptions, paths, exclude

### Community 8 - "local-adapters.js"
Cohesion: 0.14
Nodes (24): createProject(), findProjects(), confirmCode(), email, emailError, errorMessage, password, router (+16 more)

### Community 12 - "VerifyEmailForm.vue"
Cohesion: 0.08
Nodes (11): code, digits, displayedEmail, inputRefs, props, router, serverError, state (+3 more)

### Community 13 - "SignUpForm.vue"
Cohesion: 0.15
Nodes (17): signUp(), verifyEmail(), clearSession(), getSession(), setSession(), router, signOut(), email (+9 more)

### Community 14 - "vue-router"
Cohesion: 0.13
Nodes (9): vue-router, router, routes, capabilities, steps, initials, session, projectName (+1 more)

### Community 15 - "ResetPasswordForm.vue"
Cohesion: 0.10
Nodes (15): code, codeStep, digits, displayedEmail, email, errorMessage, inputRefs, password (+7 more)

### Community 16 - "ProjectsPage.vue"
Cohesion: 0.14
Nodes (11): getProjects(), formatProjectDate(), projectStatuses, baseProjects, createDialogOpen, error, loading, loadProjects() (+3 more)

### Community 17 - "Stage 1 — Interface and Local Contract Data"
Cohesion: 0.11
Nodes (17): Dionysus Frontend Foundation Implementation Plan, Global Constraints, Plan Self-Review, Stage 1 — Interface and Local Contract Data, Stage 2 — Backend Integration, Target File Structure, Task 10: Integrate projects, search, and media upload, Task 11: Integrate project-detail context for the specification stub and perform release verification (+9 more)

### Community 18 - "vitest"
Cohesion: 0.16
Nodes (4): vitest, @vue/test-utils, push, push

### Community 19 - "Dionysus Frontend Foundation Design"
Cohesion: 0.14
Nodes (13): Confirmed backend contract, Dionysus Frontend Foundation Design, Error behaviour, Excluded from this initiative, Implementation order, Included Figma screens, Purpose, Scope and exclusions (+5 more)

### Community 20 - "CreateProjectDialog.vue"
Cohesion: 0.19
Nodes (10): emit, errorMessage(), fileError, media, name, nameError, props, state (+2 more)

### Community 21 - "ProjectSearch.vue"
Cohesion: 0.33
Nodes (6): emit, props, query, results, runSearch(), state

### Community 22 - "Task 4 evidence"
Cohesion: 0.33
Nodes (5): Covered behavior, Important review fixes, Scope, Task 4 evidence, TDD evidence

## Knowledge Gaps
- **175 isolated node(s):** `$schema`, `plugins`, `browser`, `correctness`, `$schema` (+170 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 209 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **6 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `vue` connect `vue` to `package.json`, `local-adapters.js`, `VerifyEmailForm.vue`, `SignUpForm.vue`, `vue-router`, `ResetPasswordForm.vue`, `ProjectsPage.vue`, `CreateProjectDialog.vue`, `ProjectSearch.vue`?**
  _High betweenness centrality (0.218) - this node is a cross-community bridge._
- **Why does `vue-router` connect `vue-router` to `package.json`, `vue`, `local-adapters.js`, `VerifyEmailForm.vue`, `SignUpForm.vue`, `ResetPasswordForm.vue`, `ProjectsPage.vue`?**
  _High betweenness centrality (0.118) - this node is a cross-community bridge._
- **Why does `devDependencies` connect `devDependencies` to `package.json`?**
  _High betweenness centrality (0.078) - this node is a cross-community bridge._
- **What connects `$schema`, `plugins`, `browser` to the rest of the system?**
  _175 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `package.json` be split into smaller, more focused modules?**
  _Cohesion score 0.08374384236453201 - nodes in this community are weakly interconnected._
- **Should `devDependencies` be split into smaller, more focused modules?**
  _Cohesion score 0.10526315789473684 - nodes in this community are weakly interconnected._
- **Should `vue` be split into smaller, more focused modules?**
  _Cohesion score 0.06282051282051282 - nodes in this community are weakly interconnected._