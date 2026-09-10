# Stack Migration and Deploy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the temporary Python/static scaffold with ASP.NET Core, Vite, Docker Compose, and GitHub Actions CI/CD.

**Architecture:** The ASP.NET Core API exposes health and transcription endpoints, forwarding audio to the Whisper container. Vite builds static assets served by nginx; Compose runs frontend, backend, PostgreSQL, and Whisper together.

**Tech Stack:** .NET 8, ASP.NET Core Minimal API, xUnit, Vite, TypeScript, nginx, Docker Compose, GitHub Actions.

**Spec:** Approved in chat on 2026-09-10.

## Global Constraints

- Backend must compile and run on .NET 8.
- Frontend must build with Vite.
- Runtime dependencies must be represented in Docker Compose.
- CI must run backend tests, backend build, frontend build, and Compose validation.
- Deploy must build and start the stack on a remote Docker host over SSH.

---

### Task 1: Backend

Create the ASP.NET Core API, transcription client, and xUnit health test.

### Task 2: Frontend

Create the Vite TypeScript application and nginx reverse proxy configuration.

### Task 3: Containers and pipelines

Update Compose, add Dockerfiles, CI workflow, deploy workflow, and deployment configuration documentation.

### Task 4: Verification

Run .NET tests/build, frontend build, Compose config, and full Compose smoke tests.
