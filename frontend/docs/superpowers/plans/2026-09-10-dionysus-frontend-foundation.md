# Dionysus Frontend Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver every approved Figma interface with local contract-shaped data first, then integrate the same flows with the existing Dionysus backend, while leaving the full technical-specification editor for a later project.

**Architecture:** Vue Router owns page navigation; FSD slices own page composition, reusable controls, user-facing features, entity contracts, and transport adapters. Stage 1 routes all data through local adapters shaped exactly like confirmed DTOs. Stage 2 replaces adapter internals with the HTTP client without changing components’ public inputs, outputs, or routes.

**Tech Stack:** Vue 3 Composition API and JavaScript, Vue Router, Pinia, Vite, Vitest, Vue Test Utils, indented Sass.

**Spec:** `docs/superpowers/specs/2026-09-10-frontend-foundation-design.md`

## Global Constraints

- Use Vue 3 Composition API and `<script setup>`; do not add TypeScript.
- Treat Figma React, Tailwind, and Code Connect output as visual reference only; do not add React, Tailwind, JSX, or Tailwind utility classes.
- Follow FSD dependencies and import only another slice’s public `index.js` API.
- Use local scoped `<style lang="sass">` with BEM class names; global styles belong in `src/app/styles`.
- Auth illustrations are image placeholders, not newly drawn SVG files.
- Stage 1 has no HTTP requests. It uses only fixtures and local adapters matching confirmed server data.
- Stage 2 must not expect fields or flows absent from the API controller/DTO contract.
- No profile read/update endpoint is confirmed: preserve profile editing as a clearly non-persistent local interface in Stage 2 instead of guessing a request.
- The technical-specification route is a project-aware explanatory stub until a separate implementation task.
- Always implement applicable loading, empty, error, and success states.
- Run `npm run lint` and `npm run build` after code, style, or configuration changes.
- Before beginning any Stage-2 batch, run `git pull --rebase --autostash` in this repository and inspect `../backend` read-only for controller/DTO changes.
- Stage-2 local backend base URL is `http://localhost:8000`; before changing an integration adapter, inspect `http://localhost:8000/swagger/index.html` and verify `GET http://localhost:8000/health`. Do not call Whisper at `http://localhost:9000` from the browser.

## Target File Structure

```text
src/
  app/
    providers/AppProviders.vue
    router/index.js
    styles/global.sass
    styles/tokens.sass
  pages/
    landing/LandingPage.vue
    auth/AuthPage.vue
    projects/ProjectsPage.vue
    specification/SpecificationPage.vue
    profile/ProfilePage.vue
  widgets/
    app-header/AppHeader.vue
    auth-layout/AuthLayout.vue
    project-list/ProjectList.vue
  features/
    sign-up/SignUpForm.vue
    sign-in/SignInForm.vue
    verify-email/VerifyEmailForm.vue
    reset-password/ResetPasswordForm.vue
    search-projects/ProjectSearch.vue
    create-project/CreateProjectDialog.vue
    edit-profile/ProfileForm.vue
    sign-out/SignOutButton.vue
  entities/
    session/{model,api,index}.js
    project/{model,api,index}.js
    recording/{model,index}.js
  shared/
    api/{client,local-adapters}.js
    config/runtime.js
    lib/validation.js
    ui/{BaseButton,BaseInput,BaseDialog,StatusMessage,FileDropzone}.vue
```

Only create a listed directory when the task that consumes it starts. Keep page-local small helpers in their `.vue` file rather than creating empty FSD layers.

## Stage 1 — Interface and Local Contract Data

### Task 1: Establish the application shell, routes, test runner, and local API boundary

**Files:**
- Modify: `package.json`, `vite.config.js`, `src/main.js`, `src/App.vue`, `src/router/index.js`
- Create: `src/app/providers/AppProviders.vue`, `src/app/router/index.js`, `src/app/styles/tokens.sass`, `src/app/styles/global.sass`, `src/shared/api/local-adapters.js`, `src/shared/config/runtime.js`, `src/shared/lib/validation.js`, `src/test/setup.js`, `src/shared/lib/validation.test.js`

**Interfaces:**
- Produces `router` with named routes `landing`, `sign-up`, `sign-in`, `verify-email`, `reset-password`, `projects`, `specification`, and `profile`.
- Produces `validatePassword(password)` returning `{ isValid, errors }`, where errors identify missing length, uppercase character, or digit.
- Produces `getLocalSession()`, `getLocalProjects()`, `searchLocalProjects(query)`, `createLocalProject({ name, media })`, and `getLocalProject(id)`.

- [ ] **Step 1: Add the test dependencies and Vitest configuration.**

```json
{
  "scripts": {
    "test:unit": "vitest run"
  }
}
```

- [ ] **Step 2: Write the failing password-policy test.**

```js
import { expect, it } from 'vitest'
import { validatePassword } from './validation'

it('requires eight characters, a digit, and an uppercase letter', () => {
  expect(validatePassword('password').isValid).toBe(false)
  expect(validatePassword('Password1').isValid).toBe(true)
})
```

- [ ] **Step 3: Implement the validation helper, route table, global styles, and fixture-backed adapter exports.**

```js
export function validatePassword(password) {
  const errors = []
  if (password.length < 8) errors.push('min-length')
  if (!/[A-Z]/.test(password)) errors.push('uppercase')
  if (!/\d/.test(password)) errors.push('digit')
  return { isValid: errors.length === 0, errors }
}
```

- [ ] **Step 4: Run `npm run test:unit`, then `npm run lint` and `npm run build`.**
- [ ] **Step 5: Commit.**

### Task 2: Build shared visual controls and the landing page

**Files:**
- Create: `src/shared/ui/BaseButton.vue`, `src/shared/ui/BaseInput.vue`, `src/shared/ui/StatusMessage.vue`, `src/shared/ui/BaseDialog.vue`, `src/shared/ui/FileDropzone.vue`, `src/pages/landing/LandingPage.vue`
- Test: `src/shared/ui/FileDropzone.test.js`, `src/pages/landing/LandingPage.test.js`

**Interfaces:**
- `BaseInput` accepts `modelValue`, `label`, `type`, `error` and emits `update:modelValue`.
- `FileDropzone` accepts `accept`, `maxBytes`, emits `select(File)` and `reject(message)`.
- Landing CTAs navigate to named `sign-up` and `sign-in` routes.

- [ ] **Step 1: Write failing tests for CTA navigation and media-file acceptance.**

```js
it('emits a selected audio file', async () => {
  const wrapper = mount(FileDropzone, { props: { accept: ['audio/*', 'video/*'], maxBytes: 100 * 1024 * 1024 } })
  await wrapper.get('input[type="file"]').trigger('change', { target: { files: [new File(['a'], 'meeting.mp3', { type: 'audio/mpeg' })] } })
  expect(wrapper.emitted('select')).toHaveLength(1)
})
```

- [ ] **Step 2: Implement controls with accessible labels, BEM classes, and the Figma landing sections.**
- [ ] **Step 3: Verify desktop and narrow viewport layout against the Figma nodes; run the new tests, lint, and build.**
- [ ] **Step 4: Commit.**

### Task 3: Implement the auth layout, registration, and email confirmation interfaces

**Files:**
- Create: `src/widgets/auth-layout/AuthLayout.vue`, `src/features/sign-up/SignUpForm.vue`, `src/features/verify-email/VerifyEmailForm.vue`, `src/pages/auth/AuthPage.vue`
- Modify: `src/shared/api/local-adapters.js`, `src/app/router/index.js`
- Test: `src/features/sign-up/SignUpForm.test.js`, `src/features/verify-email/VerifyEmailForm.test.js`

**Interfaces:**
- `signUpLocal({ email, password })` resolves to `{ email }`.
- `verifyEmailLocal({ email, code })` resolves to `{ accessToken, expiresAt }`.
- Auth page switches form by route and renders an image placeholder.

- [ ] **Step 1: Write failing tests that reject an invalid password and require six code digits before submission.**
- [ ] **Step 2: Implement local registration/verification adapters and forms with initial, validation, submitting, server-error, and success states.**
- [ ] **Step 3: Route successful verification to the Figma confirmation-success state, then projects.**
- [ ] **Step 4: Run feature tests, lint, and build.**
- [ ] **Step 5: Commit.**

### Task 4: Implement sign-in and password recovery interfaces

**Files:**
- Create: `src/features/sign-in/SignInForm.vue`, `src/features/reset-password/ResetPasswordForm.vue`
- Modify: `src/pages/auth/AuthPage.vue`, `src/shared/api/local-adapters.js`, `src/app/router/index.js`
- Test: `src/features/sign-in/SignInForm.test.js`, `src/features/reset-password/ResetPasswordForm.test.js`

**Interfaces:**
- `signInLocal({ email, password })` resolves to token data or rejects `{ code: 'invalid-credentials' | 'unconfirmed-email' }`.
- `requestPasswordResetLocal({ email })` resolves to `{ email }`; `confirmPasswordResetLocal({ email, code, password })` resolves successfully.

- [ ] **Step 1: Write failing tests for the invalid-credentials message, unconfirmed-email navigation, and resend-code timer state.**
- [ ] **Step 2: Implement the sign-in, reset-request, and reset-code components with preserved email state between routes.**
- [ ] **Step 3: Implement local adapter outcomes for success, validation, and server-error presentation.**
- [ ] **Step 4: Run feature tests, lint, and build.**
- [ ] **Step 5: Commit.**

### Task 5: Implement project listing and search

**Files:**
- Create: `src/entities/project/model.js`, `src/entities/project/api.js`, `src/entities/project/index.js`, `src/widgets/app-header/AppHeader.vue`, `src/widgets/project-list/ProjectList.vue`, `src/features/search-projects/ProjectSearch.vue`, `src/pages/projects/ProjectsPage.vue`
- Modify: `src/shared/api/local-adapters.js`, `src/app/router/index.js`
- Test: `src/features/search-projects/ProjectSearch.test.js`, `src/widgets/project-list/ProjectList.test.js`

**Interfaces:**
- Project summary fields are exactly `{ id, name, createdAt, status }`.
- `findProjects(query)` returns summaries with optional fuzzy `score`; queries shorter than two characters do not call the adapter.

- [ ] **Step 1: Write failing tests for the two-character threshold, empty search result, loading list, and project-row navigation.**
- [ ] **Step 2: Implement local project entity API and project list states: loading, populated, empty, and error.**
- [ ] **Step 3: Implement search idle, short-query, loading, matched, no-result, and error states matching Figma.**
- [ ] **Step 4: Run feature tests, lint, and build.**
- [ ] **Step 5: Commit.**

### Task 6: Implement the create-project dialog and client file validation

**Files:**
- Create: `src/features/create-project/CreateProjectDialog.vue`
- Modify: `src/shared/ui/FileDropzone.vue`, `src/entities/project/api.js`, `src/pages/projects/ProjectsPage.vue`
- Test: `src/features/create-project/CreateProjectDialog.test.js`

**Interfaces:**
- `createProject({ name, media })` accepts only a non-empty name and one `audio/*` or `video/*` File no larger than `104857600` bytes.
- Component emits `created(project)` and `close()`.

- [ ] **Step 1: Write failing tests for wrong media type, a file above 100 MB, missing project name, selected-file display, and a 422/502 error state.**
- [ ] **Step 2: Implement the two Figma dialog states and retain entered data when local create fails.**
- [ ] **Step 3: On success, insert the returned summary into the list and navigate to named `specification` using its `id`.**
- [ ] **Step 4: Run feature tests, lint, and build.**
- [ ] **Step 5: Commit.**

### Task 7: Implement profile and technical-specification stub

**Files:**
- Create: `src/features/edit-profile/ProfileForm.vue`, `src/features/sign-out/SignOutButton.vue`, `src/pages/profile/ProfilePage.vue`, `src/pages/specification/SpecificationPage.vue`
- Modify: `src/entities/session/model.js`, `src/entities/session/api.js`, `src/entities/session/index.js`, `src/app/router/index.js`
- Test: `src/features/edit-profile/ProfileForm.test.js`, `src/pages/specification/SpecificationPage.test.js`

**Interfaces:**
- Profile form receives and emits `{ email }` to local UI state only; sign-out clears local session and navigates to `landing`.
- Specification route parameter `id` is used only to display context and navigate back to projects; it does not request or edit specification content.

- [ ] **Step 1: Write failing tests for profile validation, sign-out navigation, and the specification stub’s return action.**
- [ ] **Step 2: Implement the profile edit/save states from Figma and the account exit flow.**
- [ ] **Step 3: Implement the explicitly deferred specification screen with no fake requirements table, transcript, player, or editor.**
- [ ] **Step 4: Run feature tests, lint, and build.**
- [ ] **Step 5: Commit Stage 1.**

## Stage 2 — Backend Integration

### Task 8: Replace local adapters with a resilient HTTP and session foundation

**Files:**
- Create: `src/shared/api/client.js`, `src/entities/session/api.test.js`
- Modify: `src/shared/config/runtime.js`, `src/entities/session/api.js`, `src/entities/session/model.js`, `src/entities/session/index.js`

**Interfaces:**
- `apiClient.request(path, options)` sends `Authorization: Bearer <accessToken>` when present.
- `refreshSession()` calls `POST /api/auth/refresh` with `credentials: 'include'` and returns `{ accessToken, expiresAt }`.
- One failed protected request may refresh then repeat exactly once; failed refresh clears the session.

- [ ] **Step 1: Run `git pull --rebase --autostash`; inspect backend auth controller and contracts read-only.**
- [ ] **Step 2: Write failing mocked-fetch tests for bearer header, credentialed refresh, one retry, and failed-refresh session clearing.**
- [ ] **Step 3: Implement runtime API base URL configuration and the HTTP/session boundary.**
- [ ] **Step 4: Run unit tests, lint, and build.**
- [ ] **Step 5: Commit.**

### Task 9: Integrate registration, confirmation, sign-in, reset, and logout

**Files:**
- Modify: `src/entities/session/api.js`, `src/features/sign-up/SignUpForm.vue`, `src/features/verify-email/VerifyEmailForm.vue`, `src/features/sign-in/SignInForm.vue`, `src/features/reset-password/ResetPasswordForm.vue`, `src/features/sign-out/SignOutButton.vue`
- Test: `src/entities/session/api.test.js`, `src/features/sign-in/SignInForm.test.js`

**Interfaces:**
- Map confirmed endpoints and bodies exactly: register, verify email, login, refresh, logout, password-reset request, password-reset confirmation.
- Sign-in maps `401` to invalid credentials and `403` to confirmation guidance.

- [ ] **Step 1: Re-inspect backend controller routes and request bodies before coding endpoint mappings.**
- [ ] **Step 2: Add failing tests for 202 registration, token response storage, 401, 403, and 204 logout.**
- [ ] **Step 3: Replace auth local-adapter calls with entity API calls while keeping the Stage-1 component states unchanged.**
- [ ] **Step 4: Run tests, lint, and build.**
- [ ] **Step 5: Commit.**

### Task 10: Integrate projects, search, and media upload

**Files:**
- Modify: `src/entities/project/api.js`, `src/features/search-projects/ProjectSearch.vue`, `src/features/create-project/CreateProjectDialog.vue`, `src/pages/projects/ProjectsPage.vue`
- Test: `src/entities/project/api.test.js`, `src/features/create-project/CreateProjectDialog.test.js`

**Interfaces:**
- `listProjects()` maps `GET /api/projects` to `{ id, name, createdAt, status }[]`.
- `searchProjects(query)` only requests for `query.trim().length >= 2`.
- `uploadProject({ name, media })` builds `FormData` with keys `name` and `media`, then maps 201, 422, and 502.

- [ ] **Step 1: Run `git pull --rebase --autostash`; inspect the backend project controller and DTOs read-only.**
- [ ] **Step 2: Write failing mocked-fetch tests that assert the exact multipart keys `name` and `media`, no request below two search characters, and 422/502 mappings.**
- [ ] **Step 3: Implement endpoint adapters and replace only their Stage-1 counterparts.**
- [ ] **Step 4: Preserve the synchronous create-project UX; do not add polling or rely on missing progress fields.**
- [ ] **Step 5: Run tests, lint, and build, then commit.**

### Task 11: Integrate project-detail context for the specification stub and perform release verification

**Files:**
- Modify: `src/entities/project/api.js`, `src/pages/specification/SpecificationPage.vue`
- Test: `src/entities/project/api.test.js`, `src/pages/specification/SpecificationPage.test.js`

**Interfaces:**
- `getProject(id)` maps only documented project detail, recording, and transcript-segment fields.
- Stub consumes project name/status when present; it does not render/edit transcript or requirements.

- [ ] **Step 1: Write failing tests for detail response mapping and safe missing/unavailable project handling.**
- [ ] **Step 2: Fetch project context for the stub and retain its deliberately limited UI.**
- [ ] **Step 3: Exercise each documented error state with mocked responses and each Stage-2 journey manually against the configured backend.**
- [ ] **Step 4: Run the complete unit suite, `npm run lint`, and `npm run build`.**
- [ ] **Step 5: Commit Stage 2 and record the backend commit/contract revision checked.**

## Plan Self-Review

- Spec coverage: tasks 1–7 cover every approved Figma screen and all Stage-1 state categories; tasks 8–11 cover the confirmed authentication, projects, search, upload, and project-details contracts.
- Deferred scope: Tasks 7 and 11 intentionally retain a narrow specification route and do not introduce requirements editing, transcript playback, or a requirements table.
- API assumptions: every named request, multipart key, response field, query minimum, and error code originates in the confirmed backend contract. No progress endpoint, profile-update endpoint, or specification endpoint is assumed.
- Verification: every implementation task includes focused tests and lint/build; all integration batches refresh and inspect backend contract state first.
