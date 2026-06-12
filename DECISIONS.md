# Technical Decisions

## Project Structure

I chose a single-project API rather than a layered solution (Clean Architecture, etc.). The domain here is simple — five choices, one rule set, no persistence — so adding Domain/Application/Infrastructure layers would be structural overhead without practical benefit. The separation of concerns is achieved with a `Services/` folder and interfaces, which is sufficient for this scale.

## Per-Player Scoreboard with Bounded Memory

The scoreboard is per-player rather than global. Each player is identified by a UUID generated in the browser's `localStorage` on first visit, sent as an `X-Player-Id` request header. This gives every user their own isolated history without requiring authentication.

Internally, scoreboards are stored in `IMemoryCache` (built into .NET, no extra package) keyed by `scoreboard:{playerSessionId}`. A **sliding expiration of 6 hours** means entries are automatically evicted after 6 hours of inactivity — a player who never returns doesn't occupy memory forever. This is configurable via `Game:ScoreboardExpirationHours` in `appsettings.json` without a code change.

A raw `Dictionary<string, List<PlayResult>>` was considered but rejected because it grows forever with no eviction policy.

**Storage strategy is environment-driven**: when `ConnectionStrings:Redis` is set, the app uses `RedisGameService` backed by `IDistributedCache`. When it is empty (local dev without Docker), it falls back to `GameService` backed by `IMemoryCache`. The switch happens in `Program.cs` — the controller and tests are unaware of which implementation is active.

`RedisGameService` stores each player's scoreboard as a JSON-serialised list. Reads and writes go through `IDistributedCache.GetString` / `SetString`, which map to Redis `GET` / `SET`. The list is kept at max 10 entries in application code before serialisation — atomicity at this granularity is sufficient because a single player's scoreboard is only ever written by their own requests.

The `IGameService` interface means the storage layer can be swapped to PostgreSQL or DynamoDB without touching the controller or any test.

## Random Number Service

The external random API (`codechallenge.boohma.com/random`) is isolated behind `IRandomService`, making the game logic fully testable without network calls — tests inject a deterministic `FakeRandomService`.

The mapping uses `(int)(Math.Abs((long)n) % 5) + 1`. The cast to `long` before `Math.Abs` is intentional: `Math.Abs(int.MinValue)` throws `OverflowException` because `2147483648` doesn't fit in an `int`. Casting first avoids this edge case.

`IRandomService` is registered with `AddHttpClient<T>` + `AddStandardResilienceHandler`:
- **3s per-attempt timeout** — individual calls don't hang indefinitely
- **10s total timeout** — ceiling across all retries
- **2 retries with exponential backoff** — recovers from transient failures
- **Circuit breaker** — if 50% of calls fail in a 30-second window, the circuit opens and the dependency is stopped from being hammered while it recovers

**Fallback to local random**: when the external service is unavailable (circuit open, timeout, any exception), `RandomService` catches the exception and returns `Random.Shared.Next(1, 6)`. The circuit breaker protects the dependency — it does not take the game down with it.

The fallback is logged at `Warning` (not `Error`) with three structured properties:
- `ExternalService` — identifies which dependency failed, useful when there are multiple external calls
- `FallbackUsed` — queryable boolean; an observability tool can alert on `FallbackUsed = true AND count > N in 5 minutes`
- `ExceptionType` — distinguishes `HttpRequestException` (network) from `TaskCanceledException` (timeout) from `BrokenCircuitException` (circuit open) without parsing free-text

`Warning` rather than `Error` because the system degraded gracefully — the user got a valid response. The signal is "monitor this", not "wake someone up".

## Game Logic

Win conditions are encoded as a `HashSet<(int, int)>` of `(winner, loser)` pairs. This is O(1) lookup and avoids a long `if/else` chain. The complete rule set for RPSSL is 10 pairs (each choice beats exactly 2 others). Both `DetermineResult` and `GetChoiceById` validate their inputs and throw `ArgumentOutOfRangeException` with descriptive messages — validation at the service boundary, not just the HTTP layer.

## Resilience and Observability

- **Rate limiting** (built-in .NET 8, per-IP): `POST /play` is capped at 30 req/min; read endpoints at 60 req/min. Uses `RateLimitPartition` so each IP has an independent counter — one client cannot exhaust the limit for others.
- **Global error handling middleware**: All unhandled exceptions are caught before reaching the client. `OperationCanceledException` (client disconnects) is handled separately and logged at `Debug` — not as an error — to avoid false-alarm noise. `Response.HasStarted` is checked before writing the error body to avoid a second exception on already-flushed responses.
- **Serilog**: Structured logging with named properties (e.g., `{Player}`, `{Computer}`, `{Result}`) rather than string interpolation. In production these become queryable fields in a log aggregator (Datadog, ELK, etc.).
- **Split health checks** — `GET /health/live` (liveness) and `GET /health/ready` (readiness) serve different consumers. A load balancer only needs to know if the process is alive — it uses `/live`, which always returns 200 if Kestrel is responding. A container orchestrator (Kubernetes, ECS) uses `/ready` before routing traffic — it probes the external random API and reports `Unhealthy` if that dependency is down. Keeping them separate prevents a dependency outage from triggering a pod restart loop.
- **CORS restricted to `GET`, `POST`, `DELETE`** — `AllowAnyMethod()` would allow `PUT`, `PATCH`, `OPTIONS` etc. from the frontend origin. Explicit methods are the minimum needed and reduce the browser's pre-flight attack surface.

## Patterns and Libraries

- **Primary constructors (C# 12)** for constructor injection — cleaner than field declarations.
- **`record` types** for DTOs — immutable, structural equality, and concise syntax match the read-only nature of API request/response shapes.
- **Swagger/OpenAPI** included in Development mode only — not exposed in production.
- **`IMemoryCache`** over a raw `Dictionary` — eviction, memory pressure handling, and expiration built in.
- **`[Range]` on `PlayRequest`** — validation is declared on the model, not duplicated in the controller. ASP.NET Core model binding returns 400 automatically; the service-layer guard is a defensive last line, never reachable from normal HTTP traffic.
- **`ActionResult<T>` on action methods** — lets Swagger infer response schemas automatically; `IActionResult` loses the type information.
- **CORS origin in config** (`Cors:AllowedOrigin`) — the hardcoded `localhost:5173` would need a code change for every environment. Config can be overridden with `Cors__AllowedOrigin` env var in CI/staging/production without touching source.

## Testing

**Unit tests** (`GameServiceTests`) cover all 10 win conditions, all 5 tie conditions, all 10 lose conditions, invalid input validation, scoreboard isolation between players, cap-at-10 behavior, newest-first ordering, snapshot immutability, and reset. Tests are written against the `IGameService` interface, not the concrete class.

**Middleware tests** (`ErrorHandlingMiddlewareTests`) cover the happy path, unhandled exceptions (500 + JSON body), client disconnects (`OperationCanceledException` → no 500), and response-already-started handling — using `DefaultHttpContext` with a `MemoryStream` body, no real HTTP server needed.

**Integration tests** (`GameControllerTests`) use `WebApplicationFactory<Program>` to spin up the real ASP.NET pipeline in memory. `IRandomService` is replaced with a deterministic `FakeRandomService`. Each test class creates its own factory instance (not `IClassFixture`) so the singleton `GameService` scoreboard is isolated between tests.

## Docker

Multi-stage Dockerfile: the SDK image (~750MB) compiles and publishes; the runtime image (~220MB) runs the output. The build layer is never shipped. TLS termination is expected at the load balancer/ingress layer, so the container listens on HTTP port 8080.

## AI Usage

Claude (claude-code CLI) was used throughout this project to scaffold the initial structure, explain .NET patterns, and generate boilerplate. All code was reviewed and the reasoning behind each decision was my own. The tool was used as a pair programmer, not a replacement for understanding.
