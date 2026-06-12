# Changelog

All notable changes to this project are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.1.2] — 2026-06-12

### Fixed
- **CORS blocked the challenge test UI** — the hosted test UI at `codechallenge.boohma.com` calls the API cross-origin, but CORS only permitted `http://localhost:5173`, so the browser blocked it. The config key changed from `Cors:AllowedOrigin` (single string) to `Cors:AllowedOrigins` (array), now whitelisting both our own UI and the test UI. Added an integration test asserting the test-UI origin receives the `Access-Control-Allow-Origin` header.

### Changed
- Rewrote `DECISIONS.md` in a clearer, junior-friendly voice and synced the README test commands

### Upgrade note
- **Config change:** if you override CORS via environment variable, `Cors__AllowedOrigin` is now `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, … (array form).

### Notes
- No breaking changes to the API contract; backward compatible with 1.1.x

---

## [1.1.1] — 2026-06-12

### Fixed
- **Favicon 404 in production** — `bazingaLogo.svg` lived in `src/assets/` but `index.html` referenced it at `/bazingaLogo.svg`, the path Vite serves from `public/`. Vite never copied it into the build output, so the favicon failed to load. Moved the file to `public/` so it ships in `dist/` and resolves correctly.

### Changed
- Added `.idea/` and `.vscode/` to `.gitignore` so IDE files aren't committed
- Removed the leftover default Vite template README from `frontend/` (the root README documents the frontend)
- Documented the release process and back-merge strategy in the README

### Notes
- No breaking changes; backward compatible with 1.1.0

---

## [1.1.0] — 2026-06-12

### Added
- **"How to Play" rules modal** (`HowToPlay` component) — displays the full RPSSL win-condition matrix so players can learn the rules in-app
- `HowToPlay.test.tsx` — test coverage for the new modal

### Changed
- Reorganized frontend components into per-component folders (`ChoiceGrid/`, `ResultCard/`, `Scoreboard/`, `ChoiceSkeleton/`, `ErrorBoundary/`, `HowToPlay/`) — each component now co-locates its `.tsx`, `.css`, and styles
- Minor styling refinements to `App`, `Scoreboard`, and `ResultCard`

### Notes
- Frontend test count: 50 tests passing
- No backend changes; backward compatible with 1.0.x

---

## [1.0.1] — 2026-06-12

### Added
- `SecurityHeadersMiddleware` — sets `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, and `Referrer-Policy: no-referrer` on every response
- Integration test for the `/health/ready` readiness probe (previously only liveness was tested)
- `SessionIdentityTests` — covers `X-Player-Id` scoreboard isolation, IP fallback when the header is missing, and the 128-character guard boundary (128 accepted, 129 falls back)
- `SecurityHeadersTests` — asserts the new security headers are present
- "Known Trade-offs & Future Work" section in DECISIONS.md documenting conscious scope boundaries

### Notes
- Test count: 116 → 124
- No breaking changes; backward compatible with 1.0.0

---

## [1.0.0] — 2026-06-12

First stable release of the Bazinga Game (RPSSL) full-stack application,
built as a Billups coding challenge.

### Added

**Backend**
- `GET /choices` — returns all 5 RPSSL choices
- `GET /choice` — returns a random computer choice via external API
- `POST /play` — plays a round; returns result with player and computer choices
- `GET /scoreboard` — returns the last 10 results for the current player
- `DELETE /scoreboard` — resets the current player's scoreboard
- `GET /health/live` — liveness probe (always 200 if process is up)
- `GET /health/ready` — readiness probe (checks random API + Redis)
- Per-player session identity via `X-Player-Id` header; falls back to IP
- Redis-backed scoreboard using atomic `LPUSH + LTRIM + EXPIRE` batch — no read-modify-write race
- In-memory cache fallback when no Redis connection string is configured
- External random API resilience: 2 retries, 3s per-attempt timeout, circuit breaker, local fallback
- Structured observability logging on random API fallback (`ExternalService`, `FallbackUsed`, `ExceptionType`)
- Per-IP rate limiting: 30 req/min on write endpoints, 60 req/min on read endpoints
- Global error handling middleware — 500 JSON response, client disconnects handled separately
- Split health checks (liveness vs readiness) for load balancer and orchestrator compatibility
- Swagger/OpenAPI in Development mode with accurate response schemas (400, 429, 500)
- Docker multi-stage build — runs `dotnet test` before publishing; failing test stops the build
- Redis sidecar in Docker Compose with healthcheck and `depends_on: service_healthy`
- `GameRules` static class — shared win condition logic between in-memory and Redis implementations
- 116 tests across unit, integration, middleware, Redis (Moq), rate limit, CORS, and health check suites

**Frontend**
- React 18 + TypeScript + Vite
- Play a round by selecting a choice; result displayed immediately
- Scoreboard showing last 10 results per player
- Reset scoreboard button
- Persistent player identity via `localStorage` UUID
- Error boundary — UI never shows a blank page on unexpected errors
- AbortController — in-flight requests cancelled on component unmount
- Responsive layout
- Runtime API URL config via `VITE_API_URL`
- Nginx reverse proxy in Docker — `/api/*` proxied to backend, SPA fallback for all other routes

**Infrastructure**
- Docker Compose: nginx (port 80) → backend (:8080) → Redis (:6379)
- Multi-environment compose: base `docker-compose.yml` (production) + `docker-compose.override.yml` (development)
- `.dockerignore` — excludes `bin/`, `obj/`, `node_modules/`, `.git/` from build context

### Technical Decisions

See [DECISIONS.md](DECISIONS.md) for the full rationale behind architectural choices.

---

## Versioning Policy

| Change type | Version bump |
|-------------|-------------|
| Breaking API change | MAJOR (2.0.0) |
| New endpoint or feature | MINOR (1.1.0) |
| Bug fix, refactor, docs | PATCH (1.0.1) |
