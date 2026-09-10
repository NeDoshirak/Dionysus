# Specification Workspace Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the technical-specification placeholder with an authenticated, editable evidence-review workspace that connects requirement cards to waveform playback and the project transcript.

**Architecture:** `entities/specification` owns the immutable backend contract and pure presentation helpers. `features` own forms, confirmation UI, and the intentionally local clarification marker. `widgets/specification-workspace` composes the cards, transcript, and protected waveform player while `SpecificationPage` performs route-level loading and refreshes the server state after every successful mutation.

**Tech Stack:** Vue 3 Composition API with JavaScript, Vue Router, Vitest and Vue Test Utils, indented Sass, `@meersagor/wavesurfer-vue`.

**Spec:** `docs/superpowers/specs/2026-09-11-specification-workspace-design.md`

## Global Constraints

- Modify frontend files only. Do not modify backend source code, configuration, migrations, deployment, data, or tasks.
- Use Vue 3 Composition API and `<script setup>`; do not introduce TypeScript, React, Tailwind, or JSX.
- Use FSD dependencies and public `index.js` APIs only; do not import between sibling slices.
- Keep component styles local with `<style scoped lang="sass">` and BEM class names.
- Consume only documented backend fields. Every authenticated request sends the existing Bearer access token and credentials.
- Use the already installed `@meersagor/wavesurfer-vue`; do not add a second audio-player library.
- Use full transcript segments returned by `GET /api/projects/{id}` for local filtering; do not call the optional transcription-search endpoint.
- Do not poll. In-progress analyses refresh only through an explicit user action.
- The clarification toggle is visibly local to the page session and never sends a request.
- After frontend changes run `npm run lint` and `npm run build`. Never run `git push`.

---

## File structure

```text
src/
  entities/specification/
    api.js                       # Authenticated specification CRUD and retry calls
    api.test.js                  # Contract paths, methods, and JSON bodies
    model.js                     # Status/type labels and pure sorting helpers
    model.test.js                # Mapping and sorting behaviour
    index.js                     # Slice public API
  features/manage-specification/
    SpecificationCardForm.vue    # Create/edit form with kind-aware fields
    SpecificationCardForm.test.js
    SpecificationFunctionForm.vue
    SpecificationFunctionForm.test.js
    SpecificationConfirmDialog.vue
    index.js
  features/mark-requirement/
    ClarificationToggle.vue      # Session-only marker control
    ClarificationToggle.test.js
    index.js
  widgets/specification-workspace/
    SpecificationWorkspace.vue   # Workspace orchestration and mutation wiring
    SpecificationWorkspace.test.js
    SpecificationFunctionSection.vue
    SpecificationCard.vue
    SpecificationTranscript.vue
    SpecificationPlayer.vue
    SpecificationPlayer.test.js
    index.js
  pages/specification/
    SpecificationPage.vue        # Route loading, result refresh, error classification
    SpecificationPage.test.js
  shared/api/client.js           # Export protected media fetch options without duplicating auth logic
  shared/api/client.test.js
```

### Task 1: Create the specification entity contract

**Files:**
- Create: `src/entities/specification/api.js`
- Create: `src/entities/specification/api.test.js`
- Create: `src/entities/specification/model.js`
- Create: `src/entities/specification/model.test.js`
- Create: `src/entities/specification/index.js`

**Interfaces:**
- Consumes: `apiRequest(path, options)` from `@/shared/api/client`.
- Produces: `getSpecification`, `retrySpecification`, `createSpecificationFunction`, `updateSpecificationFunction`, `deleteSpecificationFunction`, `createSpecificationItem`, `updateSpecificationItem`, `deleteSpecificationItem`, `analysisStatusLabels`, `itemKindLabels`, `isSpecificationEditable`, `sortItems`, and `getTranscriptSegments`.

- [ ] **Step 1: Write failing entity tests**

```js
import { apiRequest } from '@/shared/api/client'
import {
  createSpecificationItem,
  getSpecification,
  retrySpecification,
} from './api'
import { getTranscriptSegments, isSpecificationEditable, sortItems } from './model'

vi.mock('@/shared/api/client', () => ({ apiRequest: vi.fn() }))

it('uses the completed specification endpoint with authentication', async () => {
  apiRequest.mockResolvedValue({ id: 'analysis-1', status: 'completed' })
  await getSpecification('project 1')
  expect(apiRequest).toHaveBeenCalledWith('/api/projects/project%201/specification', { authenticated: true })
})

it('sends only contract field names when creating a card', async () => {
  apiRequest.mockResolvedValue({ id: 'item-1' })
  await createSpecificationItem('project-1', 'function-1', {
    kind: 'functionalRequirement', title: 'SSO', description: 'Use corporate sign-in',
    priority: 'required', sourceStatementIds: [],
  })
  expect(apiRequest).toHaveBeenCalledWith(
    '/api/projects/project-1/specification/functions/function-1/items',
    expect.objectContaining({ method: 'POST', authenticated: true, body: {
      kind: 'functionalRequirement', title: 'SSO', description: 'Use corporate sign-in', priority: 'required', sourceStatementIds: [],
    } }),
  )
})

it('retries only through the documented retry path', async () => {
  apiRequest.mockResolvedValue({ analysisId: 'analysis-1', runId: 'run-2' })
  await retrySpecification('project-1')
  expect(apiRequest).toHaveBeenCalledWith('/api/projects/project-1/specification/retry', { method: 'POST', authenticated: true })
})

it('keeps server order or groups cards alphabetically by displayed kind', () => {
  const items = [{ id: '2', kind: 'role', sortOrder: 1 }, { id: '1', kind: 'functionalRequirement', sortOrder: 0 }]
  expect(sortItems(items, 'server').map((item) => item.id)).toEqual(['1', '2'])
  expect(sortItems(items, 'type').map((item) => item.id)).toEqual(['1', '2'])
})

it('treats only completed analyses as editable and reads the sole recording transcript', () => {
  expect(isSpecificationEditable({ status: 'completed' })).toBe(true)
  expect(isSpecificationEditable({ status: 'runningStage2' })).toBe(false)
  expect(getTranscriptSegments({ recordings: [{ segments: [{ startSeconds: 4, endSeconds: 6, text: 'Hello' }] }] })).toHaveLength(1)
})
```

- [ ] **Step 2: Run entity tests and verify they fail**

Run: `npm run test:unit -- src/entities/specification/api.test.js src/entities/specification/model.test.js`

Expected: FAIL because the entity slice does not exist.

- [ ] **Step 3: Implement the API functions and pure model**

```js
// src/entities/specification/api.js
import { apiRequest } from '@/shared/api/client'

const specificationPath = (projectId) => `/api/projects/${encodeURIComponent(projectId)}/specification`
const functionPath = (projectId, functionId) => `${specificationPath(projectId)}/functions/${encodeURIComponent(functionId)}`
const itemPath = (projectId, functionId, itemId) => `${functionPath(projectId, functionId)}/items/${encodeURIComponent(itemId)}`

export const getSpecification = (projectId) => apiRequest(specificationPath(projectId), { authenticated: true })
export const retrySpecification = (projectId) => apiRequest(`${specificationPath(projectId)}/retry`, { method: 'POST', authenticated: true })
export const createSpecificationFunction = (projectId, body) => apiRequest(`${specificationPath(projectId)}/functions`, { method: 'POST', body, authenticated: true })
export const updateSpecificationFunction = (projectId, functionId, body) => apiRequest(functionPath(projectId, functionId), { method: 'PATCH', body, authenticated: true })
export const deleteSpecificationFunction = (projectId, functionId) => apiRequest(functionPath(projectId, functionId), { method: 'DELETE', authenticated: true })
export const createSpecificationItem = (projectId, functionId, body) => apiRequest(`${functionPath(projectId, functionId)}/items`, { method: 'POST', body, authenticated: true })
export const updateSpecificationItem = (projectId, functionId, itemId, body) => apiRequest(itemPath(projectId, functionId, itemId), { method: 'PATCH', body, authenticated: true })
export const deleteSpecificationItem = (projectId, functionId, itemId) => apiRequest(itemPath(projectId, functionId, itemId), { method: 'DELETE', authenticated: true })
```

```js
// src/entities/specification/model.js
export const analysisStatusLabels = { queued: 'В очереди', runningStage0: 'Подготовка транскрипции', runningStage1: 'Выделение тем', runningStage2: 'Проверка фактов', runningStage3: 'Сборка ТЗ', completed: 'Готово', failed: 'Анализ не завершён' }
export const itemKindLabels = { businessContext: 'Контекст проекта', role: 'Роль', functionalRequirement: 'Требование', userScenario: 'Пользовательский сценарий', constraint: 'Ограничение', condition: 'Условие', agreement: 'Договорённость', keyQuestion: 'Открытый вопрос', projectContradiction: 'Противоречие' }
export const editableItemKinds = ['functionalRequirement', 'role', 'userScenario', 'constraint', 'condition', 'agreement', 'keyQuestion']
export const itemKindOrder = { functionalRequirement: 0, role: 1, userScenario: 2, constraint: 3, condition: 4, agreement: 5, keyQuestion: 6, businessContext: 7, projectContradiction: 8 }
export const isSpecificationEditable = (specification) => specification?.status === 'completed'
export const getTranscriptSegments = (project) => project?.recordings?.[0]?.segments || []
export const sortItems = (items, order) => [...items].sort((left, right) => order === 'type' ? itemKindOrder[left.kind] - itemKindOrder[right.kind] || left.sortOrder - right.sortOrder : left.sortOrder - right.sortOrder)
```

- [ ] **Step 4: Export the public API and rerun tests**

```js
// src/entities/specification/index.js
export * from './api'
export * from './model'
```

Run: `npm run test:unit -- src/entities/specification/api.test.js src/entities/specification/model.test.js`

Expected: PASS.

- [ ] **Step 5: Commit the entity slice**

```bash
git add src/entities/specification
git commit -m "feat: add specification entity contract"
```

### Task 2: Expose protected stream fetch options

**Files:**
- Modify: `src/shared/api/client.js`
- Modify: `src/shared/api/client.test.js`

**Interfaces:**
- Consumes: the existing callback configuration from `configureApi`.
- Produces: `getAuthenticatedFetchOptions()`, returning `{ credentials: 'include', headers: { Accept, Authorization? } }` for WaveSurfer's `fetchParams`.

- [ ] **Step 1: Write a failing authentication-options test**

```js
import { configureApi, getAuthenticatedFetchOptions } from './client'

it('returns refresh-cookie and bearer settings for protected media', () => {
  configureApi({ getAccessToken: () => 'wave-token' })
  expect(getAuthenticatedFetchOptions()).toEqual({
    credentials: 'include',
    headers: { Accept: 'application/json', Authorization: 'Bearer wave-token' },
  })
})
```

- [ ] **Step 2: Run the client test and verify it fails**

Run: `npm run test:unit -- src/shared/api/client.test.js`

Expected: FAIL because `getAuthenticatedFetchOptions` is not exported.

- [ ] **Step 3: Add the smallest shared helper**

```js
export function getAuthenticatedFetchOptions() {
  const { credentials, headers } = buildRequestOptions({ authenticated: true })
  return { credentials, headers }
}
```

Keep `buildRequestOptions` private; this prevents WaveSurfer from duplicating token lookup or refresh-cookie configuration.

- [ ] **Step 4: Run the client test and commit**

Run: `npm run test:unit -- src/shared/api/client.test.js`

Expected: PASS.

```bash
git add src/shared/api/client.js src/shared/api/client.test.js
git commit -m "feat: expose protected media fetch options"
```

### Task 3: Build the forms and local clarification control

**Files:**
- Create: `src/features/manage-specification/SpecificationCardForm.vue`
- Create: `src/features/manage-specification/SpecificationCardForm.test.js`
- Create: `src/features/manage-specification/SpecificationFunctionForm.vue`
- Create: `src/features/manage-specification/SpecificationFunctionForm.test.js`
- Create: `src/features/manage-specification/SpecificationConfirmDialog.vue`
- Create: `src/features/manage-specification/index.js`
- Create: `src/features/mark-requirement/ClarificationToggle.vue`
- Create: `src/features/mark-requirement/ClarificationToggle.test.js`
- Create: `src/features/mark-requirement/index.js`

**Interfaces:**
- Consumes: `BaseButton`, `BaseDialog`, `BaseInput`, and `editableItemKinds`.
- Produces: form components emitting `save` with contract-shaped body objects, confirmation emitting `confirm`, and `ClarificationToggle` emitting `update:modelValue` without API calls.

- [ ] **Step 1: Write failing feature tests**

```js
it('emits a create body for a required functional requirement', async () => {
  const wrapper = mount(SpecificationCardForm, { props: { open: true, mode: 'create' } })
  await wrapper.get('[name="kind"]').setValue('functionalRequirement')
  await wrapper.get('[name="title"]').setValue('SSO')
  await wrapper.get('textarea[name="description"]').setValue('Use corporate sign-in')
  await wrapper.get('[name="priority"]').setValue('required')
  await wrapper.get('form').trigger('submit')
  expect(wrapper.emitted('save')[0]).toEqual([{ kind: 'functionalRequirement', title: 'SSO', description: 'Use corporate sign-in', priority: 'required', sourceStatementIds: [] }])
})

it('does not emit any request-shaped event for the local clarification marker', async () => {
  const wrapper = mount(ClarificationToggle, { props: { modelValue: false } })
  await wrapper.get('input').setValue(true)
  expect(wrapper.emitted('update:modelValue')).toEqual([[true]])
  expect(wrapper.text()).toContain('Локально')
})
```

- [ ] **Step 2: Run feature tests and verify they fail**

Run: `npm run test:unit -- src/features/manage-specification/SpecificationCardForm.test.js src/features/manage-specification/SpecificationFunctionForm.test.js src/features/mark-requirement/ClarificationToggle.test.js`

Expected: FAIL because the feature components do not exist.

- [ ] **Step 3: Implement explicit, contract-aware forms**

`SpecificationCardForm` must use a `<select name="kind">` whose options are the seven `editableItemKinds`. It must conditionally render `title`, `priority` (`required`, `desirable`, `future`, `unknown`) for `functionalRequirement`, and reason values (`contradiction`, `unresolved`, `missingInformation`) for `keyQuestion`. It must preserve `sourceStatementIds` from an edited item and use `[]` for a new manual item. It emits `save` only after non-empty description validation and emits `cancel` without changing the current item.

`SpecificationFunctionForm` emits `{ title, description, sortOrder, sourceStatementIds }`; on a create it receives next `sortOrder` from its parent and uses an empty source list. On edit it preserves the function's source statement IDs. Both forms show a request error passed through their `error` prop and keep their local values after failure.

`SpecificationConfirmDialog` wraps `BaseDialog`, accepts `open`, `title`, `message`, and `confirmLabel`, and emits only `confirm` and `close`. It is reused for item delete, function delete, and retry confirmation.

`ClarificationToggle` is a labelled checkbox with copy `Требует уточнения` and `Локально: не сохраняется после обновления страницы.` It receives/updates `modelValue` only.

- [ ] **Step 4: Add feature public APIs and rerun tests**

```js
// src/features/manage-specification/index.js
export { default as SpecificationCardForm } from './SpecificationCardForm.vue'
export { default as SpecificationFunctionForm } from './SpecificationFunctionForm.vue'
export { default as SpecificationConfirmDialog } from './SpecificationConfirmDialog.vue'

// src/features/mark-requirement/index.js
export { default as ClarificationToggle } from './ClarificationToggle.vue'
```

Run: `npm run test:unit -- src/features/manage-specification/SpecificationCardForm.test.js src/features/manage-specification/SpecificationFunctionForm.test.js src/features/mark-requirement/ClarificationToggle.test.js`

Expected: PASS.

- [ ] **Step 5: Commit feature components**

```bash
git add src/features/manage-specification src/features/mark-requirement
git commit -m "feat: add specification editing controls"
```

### Task 4: Implement cards, transcript, player, and workspace

**Files:**
- Create: `src/widgets/specification-workspace/SpecificationWorkspace.vue`
- Create: `src/widgets/specification-workspace/SpecificationWorkspace.test.js`
- Create: `src/widgets/specification-workspace/SpecificationFunctionSection.vue`
- Create: `src/widgets/specification-workspace/SpecificationCard.vue`
- Create: `src/widgets/specification-workspace/SpecificationTranscript.vue`
- Create: `src/widgets/specification-workspace/SpecificationPlayer.vue`
- Create: `src/widgets/specification-workspace/SpecificationPlayer.test.js`
- Create: `src/widgets/specification-workspace/index.js`

**Interfaces:**
- Consumes: the `entities/specification` public API, management and clarification feature public APIs, `getAuthenticatedFetchOptions`, and `BaseButton`/`StatusMessage`.
- Produces: `SpecificationWorkspace` props `{ projectId, specification, project, mutationError }` and events `{ refresh, changed }`; `changed` is emitted only after successful backend mutation.

- [ ] **Step 1: Write failing workspace and player tests**

```js
vi.mock('@meersagor/wavesurfer-vue', () => ({
  useWaveSurfer: () => ({ waveSurfer: ref({ seekTo: vi.fn(), playPause: vi.fn(), setTime: vi.fn() }), isReady: ref(true), isPlaying: ref(false), currentTime: ref(0), totalDuration: ref(120) }),
}))

it('sorts displayed cards by type without changing the input items', async () => {
  const wrapper = mount(SpecificationWorkspace, { props: { projectId: 'project-1', specification: completedSpecification, project: projectWithTranscript } })
  await wrapper.get('[name="item-order"]').setValue('type')
  expect(wrapper.findAll('.specification-card').map((card) => card.attributes('data-kind'))).toEqual(['functionalRequirement', 'role'])
  expect(completedSpecification.functions[0].items.map((item) => item.id)).toEqual(['role-1', 'requirement-1'])
})

it('seeks the waveform and selects transcript text when a source is clicked', async () => {
  const wrapper = mount(SpecificationWorkspace, { props: { projectId: 'project-1', specification: completedSpecification, project: projectWithTranscript } })
  await wrapper.get('[data-source-start="14"]').trigger('click')
  expect(wrapper.get('.specification-transcript__segment--active').text()).toContain('corporate sign-in')
})
```

- [ ] **Step 2: Run workspace tests and verify they fail**

Run: `npm run test:unit -- src/widgets/specification-workspace/SpecificationWorkspace.test.js src/widgets/specification-workspace/SpecificationPlayer.test.js`

Expected: FAIL because the workspace components do not exist.

- [ ] **Step 3: Implement the private presentation components**

`SpecificationCard` displays label, optional title, description, priority/reason, manual marker, local clarification control, and one button for every `sourceSegments` entry. Its `source` event provides `{ startSeconds, endSeconds, text }`. Only actions supplied by `Completed` workspace state are enabled. Use a semantic left rail modifier such as `specification-card--functional-requirement`; type is also printed in text.

`SpecificationFunctionSection` is an accessible `<details>` section with function title/description, its card list, and buttons that emit `add-item`, `edit-function`, and `delete-function`.

`SpecificationTranscript` receives `segments`, `activeStartSeconds`, and `query`. It locally filters with `segment.text.toLocaleLowerCase('ru')`, marks the closest same-start segment active, and shows dedicated no-results copy. It never calls an API.

`SpecificationPlayer` uses `useWaveSurfer({ containerRef, options })` with this option shape:

```js
const options = computed(() => ({
  url: `/api/projects/${encodeURIComponent(props.projectId)}/recordings/${encodeURIComponent(props.recording.id)}/stream`,
  height: 72,
  waveColor: '#f7b1a7',
  progressColor: '#f1361d',
  cursorColor: '#111827',
  barWidth: 2,
  barGap: 2,
  barRadius: 2,
  fetchParams: getAuthenticatedFetchOptions(),
}))
```

It exposes a `seek(seconds)` method with `defineExpose`; it calls `waveSurfer.value?.setTime(seconds)` and never calls `play()`. Include labelled play/pause and time controls.

`SpecificationWorkspace` owns local `itemOrder`, `transcriptQuery`, `activeSource`, and a `Set` of clarification item IDs. It calls the entity CRUD methods from form events, awaits them, closes the form only on success, and emits `changed` so the page reloads the canonical server result. It computes next function order from the maximum current `sortOrder + 1` and preserves the server's original data while sorting copied arrays.

- [ ] **Step 4: Export the widget and rerun tests**

```js
// src/widgets/specification-workspace/index.js
export { default as SpecificationWorkspace } from './SpecificationWorkspace.vue'
```

Run: `npm run test:unit -- src/widgets/specification-workspace/SpecificationWorkspace.test.js src/widgets/specification-workspace/SpecificationPlayer.test.js`

Expected: PASS.

- [ ] **Step 5: Commit the workspace**

```bash
git add src/widgets/specification-workspace
git commit -m "feat: add specification evidence workspace"
```

### Task 5: Replace the route placeholder with lifecycle-aware loading

**Files:**
- Modify: `src/pages/specification/SpecificationPage.vue`
- Modify: `src/pages/specification/SpecificationPage.test.js`

**Interfaces:**
- Consumes: `getProject`, `getSpecification`, `retrySpecification`, `analysisStatusLabels`, `isSpecificationEditable`, `SpecificationWorkspace`, `SpecificationConfirmDialog`, `AppHeader`, and shared controls.
- Produces: the existing protected `specification` route with loading, no-analysis, processing, failure, and completed states.

- [ ] **Step 1: Replace the placeholder test with lifecycle tests**

```js
vi.mock('@/entities/project', () => ({ getProject: vi.fn() }))
vi.mock('@/entities/specification', () => ({ getSpecification: vi.fn(), retrySpecification: vi.fn(), analysisStatusLabels: { runningStage2: 'Проверка фактов' }, isSpecificationEditable: (value) => value?.status === 'completed' }))

it('renders the completed workspace after loading the project and specification', async () => {
  getProject.mockResolvedValue(projectWithTranscript)
  getSpecification.mockResolvedValue(completedSpecification)
  const wrapper = mountPage('project-1')
  await vi.waitFor(() => expect(wrapper.find('.specification-workspace').exists()).toBe(true))
})

it('shows a manual refresh while analysis is running and does not start an interval', async () => {
  getProject.mockResolvedValue(projectWithTranscript)
  getSpecification.mockResolvedValue({ id: 'analysis-1', status: 'runningStage2' })
  const wrapper = mountPage('project-1')
  await vi.waitFor(() => expect(wrapper.text()).toContain('Проверка фактов'))
  expect(window.setInterval).not.toHaveBeenCalled()
})

it('shows a no-analysis state for a 404 response', async () => {
  getProject.mockResolvedValue(projectWithTranscript)
  getSpecification.mockRejectedValue({ status: 404 })
  const wrapper = mountPage('project-1')
  await vi.waitFor(() => expect(wrapper.text()).toContain('Анализ ТЗ пока недоступен'))
})
```

- [ ] **Step 2: Run the page test and verify it fails**

Run: `npm run test:unit -- src/pages/specification/SpecificationPage.test.js`

Expected: FAIL because the page still renders the placeholder.

- [ ] **Step 3: Implement one explicit loader and state classifier**

```js
const project = ref(null)
const specification = ref(null)
const loading = ref(true)
const error = ref(null)
const retryOpen = ref(false)

async function loadWorkspace() {
  loading.value = true
  error.value = null
  const [projectResult, specificationResult] = await Promise.allSettled([getProject(route.params.id), getSpecification(route.params.id)])
  if (projectResult.status === 'rejected') error.value = projectResult.reason
  else project.value = projectResult.value
  if (specificationResult.status === 'fulfilled') specification.value = specificationResult.value
  else error.value = specificationResult.reason
  loading.value = false
}

const viewState = computed(() => {
  if (loading.value) return 'loading'
  if (error.value?.status === 404) return 'empty'
  if (error.value) return 'error'
  if (specification.value?.status === 'failed') return 'failed'
  if (!isSpecificationEditable(specification.value)) return 'processing'
  return 'completed'
})
```

Call `loadWorkspace()` on mount and after the workspace emits `changed` or `refresh`. Display the retry confirmation only for `failed`; after `retrySpecification` succeeds, call `loadWorkspace()` once. Keep the existing project-return link. Use `StatusMessage` for failure and network errors, and do not create any interval, timeout refresh, or client polling.

- [ ] **Step 4: Rerun page tests and the focused suite**

Run: `npm run test:unit -- src/pages/specification/SpecificationPage.test.js src/entities/specification src/widgets/specification-workspace`

Expected: PASS.

- [ ] **Step 5: Commit the route integration**

```bash
git add src/pages/specification/SpecificationPage.vue src/pages/specification/SpecificationPage.test.js
git commit -m "feat: replace specification placeholder"
```

### Task 6: Verify the completed frontend implementation

**Files:**
- Modify only if verification identifies a frontend defect in files from Tasks 1-5.

**Interfaces:**
- Consumes: all prior implementation tasks.
- Produces: a validated frontend-only implementation.

- [ ] **Step 1: Run all unit tests**

Run: `npm run test:unit`

Expected: PASS with all existing and new tests.

- [ ] **Step 2: Run lint**

Run: `npm run lint`

Expected: PASS. If the command makes automatic formatter changes, inspect and include only frontend files that belong to this feature.

- [ ] **Step 3: Run the production build**

Run: `npm run build`

Expected: PASS with a Vite production bundle.

- [ ] **Step 4: Perform a frontend-only manual smoke test**

Run: `npm run dev -- --host 127.0.0.1`

Check the protected route with a running backend only as an external API consumer: loading, processing, failed/retry, completed cards, edit/cancel, create/delete confirmations, local clarification reset after reload, type sorting, local transcript search, source seek/highlight, and narrow viewport layout. Do not modify, start, stop, seed, configure, or otherwise touch backend systems.

- [ ] **Step 5: Commit only frontend corrections, if any**

```bash
git add src
git commit -m "fix: polish specification workspace"
```

Skip this commit when no frontend correction was needed.
