# Dionysus Frontend Foundation Design

## Purpose

Build the SpecScribe frontend shown in Figma as two deliverable stages. The first stage provides polished, navigable interfaces using local contract-shaped data. The second connects those interfaces to the existing backend without changing page responsibilities or assuming undocumented data.

The product turns an audio or video recording of a customer–technical-specialist meeting into a project with transcript and a structured technical specification. The detailed requirements from `../Кейс 3. Томск.pdf` principally concern the future technical-specification editor and do not expand the first-stage scope.

## Scope and exclusions

### Included Figma screens

1. Landing page.
2. Registration.
3. Sign-in.
4. Password-reset request.
5. Password-reset code entry.
6. Email-confirmation code entry.
7. Successful email confirmation.
8. Project list.
9. Project search empty result.
10. Create-project dialog before file selection.
11. Create-project dialog after file selection.
12. Profile.
13. Technical-specification route as a deliberate placeholder.

The auth layouts use an image placeholder for their decorative illustration; no replacement SVG is created.

### Excluded from this initiative

- The full technical-specification editor: document sections, requirements table, inline editing, transcript synchronisation, timecodes, and audio player.
- Speech recognition, media conversion, transcription, or background-job orchestration in the browser.
- Export, requirement confidence, priority, contradiction detection, and other optional future capabilities from the case document.
- New backend endpoints or any modification to `../backend`.

## User journeys

```text
Landing → registration → email confirmation → confirmation success → projects
Landing → sign-in → projects
Sign-in → password reset request → reset code entry → sign-in
Projects → create project → project technical-specification placeholder
Projects → profile → projects
```

The project list exposes its status as returned by the server. The create-project dialog accepts the project name and one media file. In the first stage it presents realistic local success and error outcomes; it must not invent a progress protocol that the server does not offer.

## Two-stage architecture

### Stage 1: UI with contract-shaped local data

Create FSD slices only as each is needed. Pages compose widgets/features/entities through their public `index.js` APIs. Use Vue 3 Composition API, JavaScript, Vue Router, Pinia only where cross-page state genuinely needs it, and scoped indented Sass.

An API boundary isolates all data access. Each page consumes a small feature/entity interface rather than inline fixtures, so swapping in network calls is local to a service/adapter. Local values mirror known server DTO fields exactly. UI-only state—open dialog, selected file, client validation messages, current search text—remains local to its component or feature.

The first stage implements the following state matrix:

| Area | Required states |
| --- | --- |
| Auth forms | initial, client validation, submitting, server-error presentation, success/navigation |
| Projects | loading, populated, empty list, request error |
| Search | idle, query shorter than two characters, loading, matched, no results, error |
| Create project | empty form, invalid file/form, selected file, submitting, success, conversion/transcription error |
| Profile | current data, editing, validation, saving, success, error, logout transition |
| ТЗ placeholder | project-aware explanatory empty state and return-to-projects action |

### Stage 2: backend integration

Replace only the local adapters with an HTTP client and endpoint implementations. Preserve component inputs and outputs from Stage 1. Store the access token in client memory; send it in `Authorization: Bearer <token>` for protected requests. Send `credentials: 'include'` for refresh-cookie handling. On token expiry, attempt one refresh and repeat the protected request once; on failed refresh, clear auth state and navigate to sign-in.

No UI flow may require data absent from the backend contract. Before an endpoint is implemented, re-check its controller and DTO definitions; backend can change independently.

## Confirmed backend contract

| Area | Contract used by frontend |
| --- | --- |
| Register | `POST /api/auth/register` body `{ email, password }`, returns `202`; password is at least 8 characters with uppercase letter and digit |
| Confirm email | `POST /api/auth/verify-email` body `{ email, code }`, returns `{ accessToken, expiresAt }` and refresh cookie |
| Sign in | `POST /api/auth/login`; `401` invalid credentials, `403` unconfirmed email |
| Refresh and logout | `POST /api/auth/refresh` rotates the HttpOnly cookie; logout returns `204` |
| Projects | `GET /api/projects` returns `{ id, name, createdAt, status }[]` |
| Create project | `POST /api/projects`, multipart fields exactly `name`, `media`; audio/video only, maximum 100 MB; returns `201 ProjectDetailsDto`, `422` conversion failure, `502` transcription failure |
| Project details | `GET /api/projects/{id}` includes recordings and transcript segments `{ startSeconds, endSeconds, text }` |
| Search | `GET /api/projects/search?query=` and `GET /api/projects/{id}/transcription-search?query=` require query length ≥ 2 and return fuzzy-match score |

The backend currently handles upload/transcription synchronously. `status` from project summaries can be displayed but must not trigger client polling until an explicit async status endpoint/contract exists. An older backend test mentions `title`/`file`; the controller contract is authoritative: use `name`/`media`.

## Error behaviour

Show errors next to the action that caused them, retain safely entered form data, and provide retry for idempotent reads. Do not expose raw server payloads. For create-project failures, keep selected metadata in UI state so the user can replace the file or retry after correction. A 401 after the refresh attempt ends the session; a 403 from sign-in directs the user to confirmation rather than presenting it as a wrong password.

## Validation and verification

Stage 1 verifies route visibility, navigation, form validation, modal states, search-state transitions, and the ТЗ placeholder using local contract fixtures. Stage 2 adds unit tests for the HTTP/auth adapters and integration-oriented tests for each endpoint mapping and documented error branch. After code, style, or configuration changes run `npm run lint` and `npm run build`.

## Implementation order

1. App shell, routing, global tokens, API-boundary conventions, and local fixture adapters.
2. Shared controls and auth layout, then registration, sign-in, confirmation, and password-reset flow.
3. Projects list and search states.
4. Create-project dialog and file validation.
5. Profile and session exit.
6. Project-specific ТЗ placeholder.
7. Replace stage-1 adapters with backend integration in the same feature order.

This order delivers independently reviewable, navigable checkpoints and avoids blocking all screens on the later technical-specification editor.
