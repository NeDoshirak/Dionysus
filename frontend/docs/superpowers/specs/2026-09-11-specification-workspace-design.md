# Specification workspace design

## Purpose

Replace the project-aware technical-specification placeholder with the authenticated workspace where a product team reviews the AI analysis of a meeting, checks every item against the recording and transcript, and maintains the structured specification.

The backend owns the completed analysis, functions, item order, source statements, source segments, and every persisted edit. The browser owns only ephemeral display state, including the selected item, transcript view, type-sort mode, unsaved form values, and the explicitly local `Requires clarification` marker.

## Scope

- Load a project's specification, recording and transcript using the existing authenticated HTTP client.
- Render read-only business-context cards followed by functions containing requirement cards.
- Play the project's single recording using the installed `@meersagor/wavesurfer-vue` module and the protected recording stream.
- Seek to and highlight the transcript segment cited by a card source.
- Search the transcript locally from the project's contract-provided transcript segments.
- Create, edit and delete functions and cards when the specification analysis is completed.
- Render the analysis lifecycle, absent-result, error, and successful states without polling.
- Provide a local, non-persistent `Requires clarification` marker for a card.

Out of scope: export, confidence scoring, manual statement-status changes, manual timeline editing, user-story generation beyond the `UserScenario` item kind, drag-and-drop ordering, multiple-recording source selection, and any invented persistence endpoint.

The backend is entirely out of scope: this work must not modify its source code, configuration, migrations, deployment, or data. It is treated exclusively as an immutable API contract consumed by the frontend.

## Backend contract

All specification and project requests are authenticated and use the application's Bearer access-token and refresh-cookie flow.

| Purpose | Request | Result |
| --- | --- | --- |
| Load specification | `GET /api/projects/{id}/specification` | `SpecificationDetailsDto`, or `404` when there is no analysis |
| Retry failed analysis | `POST /api/projects/{id}/specification/retry` | `202` only for `Failed`; otherwise `409` |
| Create function | `POST /api/projects/{id}/specification/functions` | `{ title, description, sortOrder, sourceStatementIds }` |
| Update / delete function | `PATCH` / `DELETE /.../functions/{functionId}` | Updated function / `204` |
| Create card | `POST /.../functions/{functionId}/items` | `{ kind, title, description, priority, sourceStatementIds }` |
| Update / delete card | `PATCH` / `DELETE /.../items/{itemId}` | Updated card / `204` |
| Recording stream | `GET /api/projects/{projectId}/recordings/{recordingId}/stream` | Range-enabled audio stream |

The backend rejects CRUD until the analysis is `Completed`. Supported states are `Queued`, `RunningStage0`, `RunningStage1`, `RunningStage2`, `RunningStage3`, `Completed`, and `Failed`; there is no polling contract. The page therefore makes one request on load and offers a manual refresh only for in-progress analyses.

The specification response provides `businessContext[]`, `functions[]`, `sourceStatements[]`, and `sourceSegments[]`. Project details provide the sole recording and full transcript. A source-segment click uses its time range to seek the player and locate the matching transcript segment; it does not autoplay. The transcript search filters those already loaded segments in the browser; it does not depend on a separate transcription-search request.

## Information architecture and visual language

The screen is an evidence-review workspace, not a uniform card dashboard. It uses the current Dionysus token system: `#F9FAFB` background, `#FFFFFF` surfaces, `#111827` primary text, `#6B7280` supporting text, `#F1361D` accent, and `#B42318` error/contradiction treatment. Inter remains the sole typeface.

On desktop, the main column holds project context, functions, and cards. A sticky right column contains player controls, waveform, transcript search, and the transcript. On mobile, cards precede the player; the player is fixed at the bottom and transcript opens in a dedicated panel.

Cards use a restrained semantic left rail and label for their type. Their source-time button is part of the working evidence trail, not decorative metadata. Business context is shown above functions as read-only cards. Cards are not given indiscriminate shadows or generic colourful decoration.

```
App header
Project actions
Business context cards
Functions and cards              Sticky player / transcript
sort: server | type              waveform
card source -> seek + highlight  search + active segment
```

## Components and FSD boundaries

`entities/specification` owns data mapping, API requests, status/type labels, and simple presentation helpers. Its public API is exposed from `index.js`.

`features/manage-specification` provides the explicit card/function forms and confirmations for save, delete, and retry. `features/mark-requirement` owns the local clarification switch. Both expose only their public APIs from `index.js`.

`widgets/specification-workspace` composes the workspace and keeps its private components together: function sections, cards, waveform player, transcript, and analysis-state presentation. It can depend only on entities, features, and shared code.

`pages/specification/SpecificationPage.vue` remains the route-level coordinator. It loads the project and specification, retains the currently loaded server result, performs an explicit refresh after every successful mutation, and passes scoped props/events to the workspace. It may depend on the workspace, entities, features, and shared code, but not on sibling pages.

## User operations

Functions are collapsible sections. The server's `sortOrder` is the default display order. A local `Server order / By type` control changes presentation only and never writes order back to the API.

Users can add a function and seven manual card types: functional requirement, role, user scenario, constraint, condition, agreement, and key question. Business-context and project-contradiction cards are analysis-owned, so the UI does not create them manually. Card kind is immutable after creation because the update contract does not contain `kind`.

Card and function forms have Save and Cancel. Card forms show only meaningful fields: requirements use the backend priorities `Required`, `Desirable`, `Future`, and `Unknown`; key questions use the backend reasons `Contradiction`, `Unresolved`, and `MissingInformation`; other kinds do not invent a priority field. Manual cards are valid without sources and are marked `Added manually`. Existing source statement IDs are preserved during edits. The UI does not claim it can attach arbitrary transcript text to a source statement because no supporting endpoint exists. The transcript panel filters the already loaded transcript locally, with no additional request.

Delete actions always open a confirmation. A function confirmation calls out that its nested cards will be deleted. A retry confirmation calls out that it begins a new analysis cycle.

`Requires clarification` is an explicitly local card switch: it affects only the current page session, does not send a request, and is visibly described as local.

## States and error handling

- Loading: show a workspace-shaped skeleton.
- `Queued` and `RunningStage*`: show the current lifecycle state with an explicit `Refresh` action and no automatic retry/polling.
- `Completed`: enable the workspace and every supported mutation.
- `Failed`: show the backend error when supplied and offer confirmed retry.
- `404`: explain that a specification analysis is not available for the project and offer navigation back to projects.
- Other load errors: show a specific error message and retry action.
- Mutation errors: retain the form values and loaded content, show the request failure near the operation, and do not apply a false optimistic result.
- Transcript search: filter the loaded transcript locally and show a distinct empty result when nothing matches.

## Accessibility and responsive behaviour

Every form input has a label, dialogs trap attention through the shared dialog primitive, actions remain keyboard reachable, and visible focus uses the global focus token. Type and status never rely on colour alone. Waveform controls have text labels and seek sources are buttons. Motion is limited to feedback for explicit user actions and honours `prefers-reduced-motion`.

The desktop workspace collapses safely at narrow widths. The player remains available without obscuring card controls; transcript becomes a separately opened mobile panel rather than a compressed permanent sidebar.

## Verification

Add focused tests for API URL, method, authenticated request body, specification mapping, the lifecycle states, empty/error/loading states, mutation events and failure retention, local clarification state, type sorting, local transcript filtering, and source seek/transcript highlighting. Stub WaveSurfer in component tests while testing the emitted seek behaviour.

Before handoff, run `npm run lint` and `npm run build`. If the backend is running, additionally check `GET /health` and validate the documented specification routes against Swagger.
