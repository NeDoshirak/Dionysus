# Dionysus Frontend Backend Integration Design

## Goal

Replace the stage-one local adapters with the currently implemented Dionysus API while keeping the Vue interface and FSD slice boundaries intact. Profile editing remains local because the backend has no update-profile contract.

## Confirmed API surface

The backend service was unavailable during this design pass, so its live Swagger document and `/health` endpoint could not be read. The controller and DTO source in `../backend/Dionysus.Api` confirms the following API surface:

| Flow | Endpoint | Request | Successful response |
| --- | --- | --- | --- |
| Registration | `POST /api/auth/register` | JSON `{ email, password }` | `202 Accepted`, empty body |
| Email confirmation | `POST /api/auth/verify-email` | JSON `{ email, code }` | token `{ accessToken, expiresAt }` plus refresh cookie |
| Sign-in | `POST /api/auth/login` | JSON `{ email, password }` | token plus refresh cookie; `401` invalid credentials; `403` unconfirmed email |
| Session restore | `POST /api/auth/refresh` | empty body with cookie credentials | rotated refresh cookie and token |
| Sign-out | `POST /api/auth/logout` | empty body with cookie credentials | `204 No Content` |
| Current user | `GET /api/auth/me` | Bearer token | `{ id, email, emailConfirmed }` |
| Projects | `GET /api/projects` | Bearer token | project summaries |
| Project search | `GET /api/projects/search?query=` | Bearer token, query length >= 2 | fuzzy project results |
| Create project | `POST /api/projects` | multipart fields `name`, `media` | `201 ProjectDetailsDto`; `422` conversion error; `502` transcription error |
| Project data | `GET /api/projects/{id}` and `GET /api/projects/{id}/transcription-search?query=` | Bearer token | details and transcription matches |

The browser must never call Whisper. It remains an internal backend dependency.

## Architecture

Add one shared HTTP client that resolves `VITE_API_BASE_URL` when supplied and otherwise uses same-origin `/api` paths, serializes JSON or preserves `FormData`, parses successful JSON where present, and normalizes non-success responses to `{ status, code, detail }`. It always includes credentials so the HttpOnly refresh cookie participates in the auth lifecycle.

The application entry point configures the client with session callbacks before the router is installed. A protected request sends the in-memory access token and, after one `401`, refreshes once and retries once. A failed refresh clears the session. This callback configuration keeps `shared` independent from `entities`, avoiding a same-layer entity import.

`entities/session` owns registration, verification, login, refresh, logout, and current-user mapping. The session model stores the token and email in memory. Route metadata marks projects, profile, and specification protected; a router guard restores the session via refresh before admitting those routes and otherwise redirects to sign-in.

`entities/project` maps list, fuzzy-search, create, project-details, and transcription-search responses. Creation builds `FormData` with exactly `name` and `media`. It derives the summary status from the newest recording because `ProjectDetailsDto` does not contain top-level `status`.

## Component behaviour

- Registration uses the submitted, normalized email after the body-less `202` to open confirmation.
- Sign-in calls the session entity, stores `{ accessToken, expiresAt, email }`, and sends `403` users to confirmation; `401` displays invalid credentials.
- Email confirmation stores the issued token and then opens the existing success screen.
- Password-reset request preserves the entered email after `202`; confirmation sends the backend field `newPassword` from the existing password UI state.
- Project list, search, and create dialog retain their existing loading, empty, error, and success presentation. Create maps `422` and `502` to the existing specific messages and does not poll.
- Profile obtains the authenticated email from `GET /api/auth/me` during session restoration when available. The existing email/password save controls stay explicitly local. Sign-out calls the API and clears local state even if the network request fails.
- The technical-specification route stays a placeholder; its backend detail/search endpoints are available at the entity boundary but do not imply an editor, player, or transcript UI.

## Development-server integration

The backend presently has no CORS middleware. Configure Vite's development server to proxy `/api` to `http://localhost:8000`; the default API URL is therefore same-origin during development. `VITE_API_BASE_URL` remains available for deployed environments where CORS or reverse-proxy policy is configured externally.

## Error and security policy

- Do not persist access tokens to localStorage, sessionStorage, or cookies.
- Do not expose raw API error bodies.
- Do not retry mutable requests other than the single authorization retry caused by a rejected token; the request is not duplicated after any received non-401 result.
- A missing/expired refresh cookie leaves the user signed out and protected navigation goes to sign-in.
- Network, parse, and unexpected server failures use the components' existing generic messages.

## Tests and verification

Write tests first for the shared client response/refresh behavior, session endpoint mappings and status errors, project multipart mapping and result transformation, and logout behavior. Preserve the existing component tests, updating mocks only where the public entity interface changes. Run `npm run test:unit`, `npm run lint`, and `npm run build` after implementation. Re-check live `/health` and Swagger before browser-level integration verification once the service is running.
