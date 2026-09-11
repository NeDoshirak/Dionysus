# Asynchronous Transcription Design

## Goal

Make project creation complete after the upload is safely stored, rather than
waiting for media conversion or Whisper. This removes long-lived request paths
that can exhaust proxy timeouts while retaining the existing 100 MB media
validation and project/recording API shape.

## API contract

`POST /api/projects` continues to accept multipart fields `name` and `media`.
After validating and persisting the incoming recording, it returns
`202 Accepted`, with a `Location` header for `GET /api/projects/{id}` and the
normal `ProjectDetailsDto`. The initial recording status is `processing`.

The existing `GET /api/projects/{id}` response is the status endpoint. Its
recording status transitions are:

- `processing`: accepted, queued, converting, or transcribing;
- `completed`: transcript and segments are saved and specification analysis is
  queued;
- `failed`: conversion or transcription failed; `error` contains a safe,
  user-facing message.

Malformed uploads and unsupported media remain immediate `400` responses. No
new migration is required because the persisted recording already has status,
error, source type, content type, and audio-data fields.

## Background processing

Add a dedicated in-memory `ITranscriptionQueue` and hosted
`TranscriptionWorker`, separate from specification-analysis processing. The
queue contains recording IDs. On application startup the worker scans durable
records with `processing` status and re-enqueues them, so a restart after the
request returns does not strand a recording.

The worker loads a recording in its own DI scope. For video it converts the
stored source bytes to WAV and updates file metadata; for audio it sends the
stored bytes directly to Whisper. It saves transcript, language, and segments;
then marks the recording `completed`, creates the project’s queued
`SpecificationAnalysis`, saves the transaction, and enqueues that analysis.
Conversion and transcription exceptions mark the recording `failed` and retain
only `Conversion failed.` or `Transcription failed.` for the client. Queue
worker exceptions are logged and do not terminate processing of later jobs.

## Frontend flow

The create-project client accepts the successful `202` response through the
existing generic HTTP client. The existing navigation to the project’s
specification route is retained. While the current recording has
`processing`, that page polls its existing project-detail endpoint every three
seconds. It stops polling on `completed`, `failed`, unmount, or route change.
Once transcription completes, it reloads the specification endpoint so the
existing analysis-progress UI takes over. A failed recording is shown as a
project-processing failure rather than an endlessly loading analysis.

## Verification

Backend tests prove that creation returns `202` without invoking conversion or
Whisper synchronously, persists a `processing` recording, enqueues it, and
that the worker completes or fails it safely. Frontend tests prove polling
continues only while the recording is processing and stops at terminal status.
Run backend tests plus the frontend unit, lint, and build checks.
