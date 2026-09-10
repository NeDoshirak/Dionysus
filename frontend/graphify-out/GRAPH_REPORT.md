# Graph Report - frontend  (2026-09-10)

## Corpus Check
- Corpus is ~790 words - fits in a single context window. You may not need a graph.

## Summary
- 92 nodes · 95 edges · 12 communities (9 shown, 3 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 3 edges (avg confidence: 0.82)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Project Tooling
- Linting Dependencies
- Application Entry and Docs
- Vue Application Structure
- NPM Scripts
- Oxc Configuration
- Prettier Configuration
- Path Configuration
- Runtime Dependencies
- Styling Conventions
- Component Conventions
- HTML Metadata

## God Nodes (most connected - your core abstractions)
1. `scripts` - 8 edges
2. `Vue 3 Vite Frontend Template` - 5 edges
3. `pinia` - 3 edges
4. `vue` - 3 edges
5. `Vue 3` - 3 edges
6. `Vite` - 3 edges
7. `env` - 2 edges
8. `categories` - 2 edges
9. `compilerOptions` - 2 edges
10. `vue-router` - 2 edges

## Surprising Connections (you probably didn't know these)
- `Vite` --semantically_similar_to--> `Vue 3`  [INFERRED] [semantically similar]
  README.md → AGENTS.md
- `src/main.js Entry Script` --conceptually_related_to--> `Vite`  [INFERRED]
  index.html → README.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Frontend Architecture Stack** — agents_vue_3, agents_composition_api, agents_javascript, readme_vite [INFERRED 0.85]
- **Frontend Quality Workflow** — readme_npm_install, readme_npm_dev, readme_npm_build, readme_npm_lint [EXTRACTED 1.00]

## Communities (12 total, 3 thin omitted)

### Community 0 - "Project Tooling"
Cohesion: 0.11
Nodes (20): engines, node, name, private, type, version, eslint, eslint-config-prettier (+12 more)

### Community 1 - "Linting Dependencies"
Cohesion: 0.13
Nodes (15): devDependencies, eslint, eslint-config-prettier, @eslint/js, eslint-plugin-oxlint, eslint-plugin-vue, globals, npm-run-all2 (+7 more)

### Community 2 - "Application Entry and Docs"
Cohesion: 0.20
Nodes (11): Feature-Sliced Design, Single-File Components, Vue 3, Application Mount Point, src/main.js Entry Script, Vue 3 Vite Frontend Template, npm run build, npm run dev (+3 more)

### Community 3 - "Vue Application Structure"
Cohesion: 0.24
Nodes (6): pinia, vue, vue-router, app, router, useCounterStore

### Community 4 - "NPM Scripts"
Cohesion: 0.25
Nodes (8): scripts, build, dev, format, lint, lint:eslint, lint:oxlint, preview

### Community 5 - "Oxc Configuration"
Cohesion: 0.29
Nodes (6): categories, correctness, env, browser, plugins, $schema

### Community 6 - "Prettier Configuration"
Cohesion: 0.40
Nodes (4): printWidth, $schema, semi, singleQuote

### Community 7 - "Path Configuration"
Cohesion: 0.50
Nodes (3): compilerOptions, paths, exclude

### Community 8 - "Runtime Dependencies"
Cohesion: 0.50
Nodes (4): dependencies, pinia, vue, vue-router

## Knowledge Gaps
- **57 isolated node(s):** `$schema`, `plugins`, `browser`, `correctness`, `$schema` (+52 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 58 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **3 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `devDependencies` connect `Linting Dependencies` to `Project Tooling`?**
  _High betweenness centrality (0.176) - this node is a cross-community bridge._
- **Why does `scripts` connect `NPM Scripts` to `Project Tooling`?**
  _High betweenness centrality (0.094) - this node is a cross-community bridge._
- **Why does `dependencies` connect `Runtime Dependencies` to `Project Tooling`?**
  _High betweenness centrality (0.042) - this node is a cross-community bridge._
- **What connects `$schema`, `plugins`, `browser` to the rest of the system?**
  _57 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Project Tooling` be split into smaller, more focused modules?**
  _Cohesion score 0.11067193675889328 - nodes in this community are weakly interconnected._
- **Should `Linting Dependencies` be split into smaller, more focused modules?**
  _Cohesion score 0.13333333333333333 - nodes in this community are weakly interconnected._