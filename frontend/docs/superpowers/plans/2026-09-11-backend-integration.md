# Dionysus Backend Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Connect the existing Vue screens to the confirmed Dionysus backend while preserving their UX and FSD boundaries.

**Architecture:** A shared API client normalizes HTTP behavior and receives auth callbacks from the app layer, so it never depends upward on session state. Session and project entities map HTTP contracts into the existing component interfaces; the router restores cookie-backed sessions before protected navigation.

**Tech Stack:** Vue 3 Composition API, Vue Router, JavaScript, Vite, Vitest, indented Sass.

**Spec:** `docs/superpowers/specs/2026-09-11-backend-integration-design.md`

## Global Constraints

- Use Vue 3 Composition API and JavaScript; do not introduce TypeScript, React, Tailwind, or unrelated refactors.
- Keep FSD imports directional and use slice public APIs for external consumption.
- Keep access tokens only in memory; send protected requests with Bearer tokens and refresh-cookie requests with credentials.
- Use `name` and `media` in multipart project creation; accept only audio/video up to 100 MB; do not add polling.
- Profile save controls remain local UI state because no server update contract exists.
- Preserve user-owned working-tree changes outside files listed in this plan.
- Run `npm run test:unit`, `npm run lint`, and `npm run build` after implementation; never push.

---

### Task 1: Shared HTTP client and Vite development proxy

**Files:**
- Create: `src/shared/api/client.js`
- Create: `src/shared/api/client.test.js`
- Modify: `src/shared/config/runtime.js`
- Modify: `vite.config.js`

**Interfaces:**
- Produces `configureApi({ getAccessToken, refreshAccessToken, onUnauthorized })` and `apiRequest(path, options)`.
- `apiRequest` accepts `{ method, body, authenticated, retryOnUnauthorized }`; resolves JSON, `undefined` for no content, or throws `{ status, code, detail }`.

- [ ] **Step 1: Write the failing client tests**

```js
it('adds a bearer token to protected JSON requests', async () => {
  configureApi({ getAccessToken: () => 'access-token' })
  globalThis.fetch = vi.fn().mockResolvedValue(jsonResponse({ ok: true }))
  await apiRequest('/api/projects', { authenticated: true })
  expect(fetch).toHaveBeenCalledWith('http://localhost:8000/api/projects', expect.objectContaining({
    credentials: 'include',
    headers: expect.objectContaining({ Authorization: 'Bearer access-token' }),
  }))
})

it('refreshes once and retries a protected request after 401', async () => {
  const refreshAccessToken = vi.fn().mockResolvedValue(true)
  globalThis.fetch = vi.fn().mockResolvedValueOnce(emptyResponse(401)).mockResolvedValueOnce(jsonResponse([]))
  configureApi({ getAccessToken: () => 'renewed-token', refreshAccessToken })
  await apiRequest('/api/projects', { authenticated: true })
  expect(refreshAccessToken).toHaveBeenCalledOnce()
  expect(fetch).toHaveBeenCalledTimes(2)
})
```

- [ ] **Step 2: Run the client tests to verify they fail**

Run: `npm run test:unit -- src/shared/api/client.test.js`

Expected: FAIL because `client.js` does not exist.

- [ ] **Step 3: Implement the minimal client and runtime configuration**

```js
export async function apiRequest(path, { authenticated = false, retryOnUnauthorized = true, ...options } = {}) {
  const response = await fetch(`${runtimeConfig.apiBaseUrl}${path}`, requestOptions)
  if (authenticated && response.status === 401 && retryOnUnauthorized && await callbacks.refreshAccessToken?.()) {
    return apiRequest(path, { authenticated, retryOnUnauthorized: false, ...options })
  }
  if (!response.ok) throw await toApiError(response)
  return response.status === 204 ? undefined : response.json()
}
```

Keep the empty default runtime base URL so development requests use same-origin `/api` paths; retain `VITE_API_BASE_URL` for deployed environments. Configure the Vite development server proxy for `/api` to the backend with `changeOrigin: true`.

- [ ] **Step 4: Run the client tests to verify they pass**

Run: `npm run test:unit -- src/shared/api/client.test.js`

Expected: PASS.

- [ ] **Step 5: Commit the focused change**

```bash
git add src/shared/api/client.js src/shared/api/client.test.js src/shared/config/runtime.js vite.config.js
git commit -m "feat: add authenticated API client"
```

### Task 2: Session entity, provider setup, and route protection

**Files:**
- Create: `src/entities/session/api.test.js`
- Modify: `src/entities/session/api.js`
- Modify: `src/entities/session/model.js`
- Modify: `src/entities/session/index.js`
- Modify: `src/main.js`
- Modify: `src/app/router/index.js`

**Interfaces:**
- Consumes `apiRequest` and `configureApi` from Task 1.
- Produces `signUp({ email, password })`, `verifyEmail({ email, code })`, `signIn({ email, password })`, `restoreSession()`, `signOut()`, and in-memory session `{ accessToken, expiresAt, email }`.

- [ ] **Step 1: Write failing endpoint-mapping tests**

```js
it('maps login 403 to an unconfirmed-email error', async () => {
  apiRequest.mockRejectedValue({ status: 403 })
  await expect(signIn({ email: 'person@example.com', password: 'Password1' }))
    .rejects.toEqual({ code: 'unconfirmed-email', status: 403 })
})

it('returns the submitted normalized email after body-less registration', async () => {
  apiRequest.mockResolvedValue(undefined)
  await expect(signUp({ email: ' Person@example.com ', password: 'Password1' }))
    .resolves.toEqual({ email: 'person@example.com' })
})
```

- [ ] **Step 2: Run the session tests to verify they fail**

Run: `npm run test:unit -- src/entities/session/api.test.js`

Expected: FAIL because `signIn`, `restoreSession`, and HTTP mappings are absent.

- [ ] **Step 3: Implement session operations and router guard**

```js
export async function restoreSession() {
  if (getSession()) return true
  try {
    const token = await refreshSession()
    const user = await getCurrentUser()
    setSession({ ...token, email: user.email })
    return true
  } catch {
    clearSession()
    return false
  }
}

router.beforeEach(async (to) => {
  if (!to.meta.requiresAuth || await restoreSession()) return true
  return { name: 'sign-in' }
})
```

Configure the shared client from `main.js`, before `app.use(router)`, with the session token getter, refresh function, and session clearer. Mark project, profile, and specification routes with `meta: { requiresAuth: true }`. Logout sends the backend request with credentials, clears session in a `finally` block, and the button retains landing navigation.

- [ ] **Step 4: Run the session tests to verify they pass**

Run: `npm run test:unit -- src/entities/session/api.test.js src/features/profile/SignOutButton.test.js`

Expected: PASS.

- [ ] **Step 5: Commit the focused change**

```bash
git add src/entities/session src/main.js src/app/router/index.js
git commit -m "feat: integrate authentication session"
```

### Task 3: Project entity HTTP mappings

**Files:**
- Create: `src/entities/project/api.test.js`
- Modify: `src/entities/project/api.js`
- Modify: `src/entities/project/index.js`

**Interfaces:**
- Consumes `apiRequest` from Task 1.
- Produces existing `getProjects`, `findProjects`, `createProject`, plus `getProject(id)` and `searchProjectTranscription(id, query)` for later specification work.

- [ ] **Step 1: Write failing project API tests**

```js
it('submits exactly name and media as multipart project data', async () => {
  const media = new File(['audio'], 'meeting.mp3', { type: 'audio/mpeg' })
  await createProject({ name: ' Discovery ', media })
  const body = apiRequest.mock.calls[0][1].body
  expect([...body.keys()]).toEqual(['name', 'media'])
  expect(body.get('name')).toBe('Discovery')
})

it('converts created project details into a project summary', async () => {
  apiRequest.mockResolvedValue({ id: '1', name: 'Discovery', createdAt: '2026-09-11T00:00:00Z', recordings: [{ status: 'completed' }] })
  await expect(createProject({ name: 'Discovery', media: validMedia() })).resolves.toMatchObject({ status: 'completed' })
})
```

- [ ] **Step 2: Run the project API tests to verify they fail**

Run: `npm run test:unit -- src/entities/project/api.test.js`

Expected: FAIL because calls still use local adapters.

- [ ] **Step 3: Implement API-backed project operations**

```js
export async function createProject({ name, media }) {
  validateProjectInput(name, media)
  const body = new FormData()
  body.append('name', name.trim())
  body.append('media', media)
  return toProjectSummary(await apiRequest('/api/projects', { method: 'POST', body, authenticated: true }))
}
```

Use URL encoding for searches, reject local queries shorter than two characters, and preserve `{ status, code, detail }` from the shared client so the dialog can render `422` and `502` correctly.

- [ ] **Step 4: Run the project API tests to verify they pass**

Run: `npm run test:unit -- src/entities/project/api.test.js src/features/create-project/CreateProjectDialog.test.js src/features/search-projects/ProjectSearch.test.js`

Expected: PASS.

- [ ] **Step 5: Commit the focused change**

```bash
git add src/entities/project
git commit -m "feat: integrate project API"
```

### Task 4: Wire auth and project screens to the entity boundary

**Files:**
- Modify: `src/features/sign-in/SignInForm.vue`
- Modify: `src/features/sign-in/SignInForm.test.js`
- Modify: `src/features/reset-password/ResetPasswordForm.vue`
- Modify: `src/features/reset-password/ResetPasswordForm.test.js`
- Modify: `src/pages/profile/ProfilePage.vue`
- Modify: `src/pages/projects/ProjectsPage.vue` only if result handling requires a contract correction

**Interfaces:**
- Consumes Task 2 session APIs and the unchanged Task 3 project APIs.
- Produces the same component events, routes, and Russian UI messages already exercised by the component test suite.

- [ ] **Step 1: Write failing form-binding tests**

```js
it('calls the session sign-in boundary rather than a local adapter', async () => {
  signIn.mockResolvedValue({ accessToken: 'token', expiresAt: '2026-09-11T12:00:00Z' })
  await submitValidCredentials(wrapper)
  expect(signIn).toHaveBeenCalledWith({ email: 'person@example.com', password: 'Password1' })
})

it('sends newPassword when confirming a reset', async () => {
  await submitResetCodeAndPassword(wrapper)
  expect(confirmPasswordReset).toHaveBeenCalledWith(expect.objectContaining({ newPassword: 'Password1' }))
})
```

- [ ] **Step 2: Run the focused component tests to verify they fail**

Run: `npm run test:unit -- src/features/sign-in/SignInForm.test.js src/features/reset-password/ResetPasswordForm.test.js`

Expected: FAIL because the components import stage-one local adapter functions.

- [ ] **Step 3: Replace local-adapter imports without altering visual behavior**

```js
import { signIn } from '@/entities/session'

const session = await signIn({ email: email.value.trim(), password: password.value })
setSession({ ...session, email: email.value.trim() })
```

Replace password-reset local calls with exported session operations. Map only the documented `401` and `403` cases; all other errors retain existing generic copy. Profile labels stop calling the active session a local demo and continue to state that save changes are local.

- [ ] **Step 4: Run the focused component tests to verify they pass**

Run: `npm run test:unit -- src/features/sign-in/SignInForm.test.js src/features/reset-password/ResetPasswordForm.test.js src/features/sign-up/SignUpForm.test.js src/features/verify-email/VerifyEmailForm.test.js src/pages/projects/ProjectsPage.test.js`

Expected: PASS.

- [ ] **Step 5: Commit the focused change**

```bash
git add src/features/sign-in src/features/reset-password src/pages/profile/ProfilePage.vue src/pages/projects/ProjectsPage.vue
git commit -m "feat: connect auth forms to session API"
```

### Task 5: Full verification and live contract check

**Files:**
- Modify: none unless a verification failure identifies a minimal required fix.

**Interfaces:**
- Consumes all prior public interfaces.
- Produces evidence that the integration builds, passes tests, and targets a live backend contract.

- [ ] **Step 1: Run all frontend tests**

Run: `npm run test:unit`

Expected: all Vitest tests pass.

- [ ] **Step 2: Run lint and build**

Run: `npm run lint && npm run build`

Expected: both commands exit 0.

- [ ] **Step 3: Recheck the live backend contract**

Run: `curl -fsS http://localhost:8000/health && curl -fsS http://localhost:8000/swagger/v1/swagger.json -o /tmp/dionysus-swagger-live.json`

Expected: health payload contains `status: ok` and Swagger includes `/api/auth/*` plus `/api/projects*` paths.

- [ ] **Step 4: Inspect the scoped diff before handoff**

Run: `git diff --check && git status --short`

Expected: no whitespace errors; unrelated user changes remain unmodified.

- [ ] **Step 5: Commit only integration-owned files after fresh verification**

```bash
git add src/shared/api/client.js src/shared/api/client.test.js src/shared/config/runtime.js vite.config.js src/entities/session src/entities/project src/app/providers/AppProviders.vue src/app/router/index.js src/features/sign-in src/features/reset-password src/pages/profile/ProfilePage.vue docs/superpowers/plans/2026-09-11-backend-integration.md
git commit -m "feat: connect frontend to backend API"
```
