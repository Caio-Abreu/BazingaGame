# Changelog

All notable changes to this project are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
