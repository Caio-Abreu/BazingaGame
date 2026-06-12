# Technical Decisions

This document explains *why* the code is built the way it is. If you're new to the
project (or to .NET / React), read this alongside the code — each section tells you
what I chose, what I rejected, and the reasoning, so you can follow the decisions
instead of just reading the result.

## Project Structure

I kept the backend as a **single project** instead of splitting it into the classic
"Clean Architecture" layers (separate Domain / Application / Infrastructure projects).

Why? Those layers exist to manage complexity in big systems. This game is small —
five choices, one set of rules, and a short per-player scoreboard. Adding three
projects and the wiring between them would be a lot of ceremony for very little gain;
you'd spend more time navigating folders than reading logic.

Instead, the one real "seam" I need — the ability to swap *where the scoreboard is
stored* (memory vs. Redis) — is handled by a single interface, `IGameService`. The
controller talks to that interface and has no idea which storage is behind it. That's
the main thing layered architecture would have given me, and I get it with one
interface. If the project grew, splitting into layers would be the natural next step.

## Per-Player Scoreboard

Each player gets **their own** scoreboard instead of one shared global one.

How does the server know who you are without a login? The frontend generates a random
UUID the first time you visit, saves it in the browser's `localStorage`, and sends it
on every request as an `X-Player-Id` header. The server uses that string as your key.
No accounts, no passwords — just a per-browser id.

The `IGameService` interface has three scoreboard methods: `AddToScoreboardAsync`,
`GetScoreboardAsync`, `ResetScoreboardAsync`. Notice they're all `async` (they return
`Task`). The in-memory version doesn't actually *need* to be async, but I made it async
anyway so that the interface looks identical whether the data lives in memory or in
Redis. That way, switching storage never forces a change to the interface or the
controller — a small upfront choice that avoids a painful refactor later.

**Which storage gets used is decided at startup, based on config.** If a Redis
connection string is set (`ConnectionStrings:Redis`), the app uses `RedisGameService`.
If it's empty — like when you're running locally without Docker — it falls back to
`GameService`, which keeps everything in memory. This switch happens in one place
(`Program.cs`); nothing else in the app knows or cares which one is active.

### In-memory option (`GameService`)

This uses `IMemoryCache`, which ships with .NET (no extra package). Each player's
scoreboard is stored under a key like `scoreboard:{playerId}`.

The cache entries have a **6-hour sliding expiration**: if a player doesn't play for
6 hours, their scoreboard is automatically dropped from memory. "Sliding" means the
clock resets every time they play, so active players never lose their history — only
abandoned ones get cleaned up. Without this, memory would slowly fill up with
scoreboards from people who left and never came back. (You can change the 6 hours via
`Game:ScoreboardExpirationHours` in `appsettings.json`.)

I first considered just using a plain `Dictionary<string, List<PlayResult>>`, but a
dictionary never forgets anything — it would grow forever. The cache's built-in
expiration is exactly what I wanted.

**One honest caveat:** reading the cache and writing it back are two separate steps,
not one atomic operation. If the same player somehow fired two plays at the exact same
instant, one could overwrite the other and you'd lose a result. I left this alone on
purpose — the in-memory path is only the local-dev fallback, and the production path
(Redis, below) doesn't have this problem at all.

### Redis option (`RedisGameService`)

For production I use Redis. There are two ways to talk to Redis from .NET, and I
deliberately picked the lower-level one (`IConnectionMultiplexer` from StackExchange.Redis)
instead of the convenient `IDistributedCache`.

Here's why. `IDistributedCache` only knows how to "get a value" and "set a value". To
add one result to a scoreboard, I'd have to: read the whole list, add the new item in
C#, then write the whole list back. If two requests do that at the same time, they can
clobber each other — the same race I mentioned above.

Redis has commands that do this safely in one shot, and `IConnectionMultiplexer` lets
me send them together:

```
LPUSH  scoreboard:{id}  <new result>   # add the result to the front of the list
LTRIM  scoreboard:{id}  0  9           # keep only the newest 10
EXPIRE scoreboard:{id}  <seconds>      # reset the 6-hour expiry
```

All three are sent in a single round-trip and Redis runs them atomically. No race, and
it works correctly even if you run several copies of the backend behind a load balancer
(they all share the one Redis).

**What if Redis is down?** Every Redis call is wrapped so that a `RedisException`
doesn't crash the game. Reads just return an empty scoreboard; writes and resets are
quietly skipped. We log a warning (with which service failed and which player) so an
operator can see it, but the player still gets a working game. Availability wins over a
perfect scoreboard.

**What if a stored entry is corrupt?** When reading the list back, each item is parsed
individually. If one item fails to parse (`JsonException`), we skip just that one and
log it — the rest of the scoreboard still loads fine.

## Game Logic

The win rules are stored as a `HashSet` of `(winner, loser)` number pairs. Checking
"does A beat B?" is then just "is `(A, B)` in the set?" — instant, and no giant
`if/else` ladder. RPSSL has exactly 10 such pairs (each of the 5 choices beats 2
others).

All of this lives in a `GameRules` static class. I made it `public` specifically so the
tests can call it directly without spinning up the whole app. Both `GameService` and
`RedisGameService` share this one class, so the rules aren't copy-pasted in two places.
The methods also validate their inputs and throw a clear `ArgumentOutOfRangeException`
if you pass an invalid choice id — I'd rather fail loudly at the service layer than
silently return a wrong answer.

## Random Number Service

The computer's choice comes from an **external random-number API**
(`codechallenge.boohma.com/random`). I hid it behind an interface, `IRandomService`,
for one big reason: tests. With the interface, tests can plug in a fake that returns a
fixed number, so the game logic can be tested without ever hitting the network (which
would be slow and flaky).

**A subtle bug I had to guard against:** the API returns a number that I map onto 1–5
using `(int)(Math.Abs((long)n) % 5) + 1`. The `(long)` cast looks odd but it's
important. If the number happens to be `int.MinValue`, calling `Math.Abs` on it *as an
int* throws an overflow error (the positive version is too big to fit in an int).
Casting to the bigger `long` type first sidesteps that entirely.

The HTTP client is set up with a **resilience policy** (`AddStandardResilienceHandler`)
so a flaky external service can't hang or break the game:

- **3s per-attempt timeout** — a single call can't hang forever.
- **10s total timeout** — a hard ceiling even across retries.
- **2 retries with backoff** — recovers from brief hiccups, waiting a bit longer each time.
- **Circuit breaker** — if half the calls fail within 30 seconds, it "trips" and stops
  calling the dead service for a while, instead of hammering something that's already
  struggling. (Think of it like a fuse box.)

**If the service is unreachable** (timed out, circuit tripped, whatever), the catch
block falls back to .NET's built-in `Random.Shared`. The player still gets a real
result and never sees an error.

I log that fallback at **Warning** level (not Error) with three named fields:

- `ExternalService` — which dependency failed (handy if there are several later).
- `FallbackUsed` — a true/false flag you can build an alert on ("warn me if this is
  true more than N times in 5 minutes").
- `ExceptionType` — was it a network error, a timeout, or a tripped circuit? Knowing
  this without grepping raw text is gold when debugging.

Why Warning and not Error? Because nothing actually broke for the user — the system
degraded gracefully. The message is "keep an eye on this," not "wake someone up at 3am."

## Resilience and Observability

- **Rate limiting** (built into .NET 8): `POST /play` is capped at 30 requests/minute
  and the read endpoints at 60/minute, **per IP address**. Each IP has its own counter,
  so one abusive client can't use up everyone else's allowance.
- **Global error-handling middleware**: a catch-all that sits in front of everything and
  turns any unhandled exception into a clean 500 response instead of leaking a stack
  trace. It treats client disconnects (`OperationCanceledException` — the user closed
  the tab mid-request) specially, logging them quietly at Debug instead of screaming
  about an error that isn't really one. It also checks whether the response already
  started sending before trying to write an error body (you can't change a response
  that's already on its way out).
- **Serilog** for logging: instead of mashing values into a string, we log named fields
  like `{Player}`, `{Computer}`, `{Result}`. In a real log system (Datadog, ELK, etc.)
  those become searchable columns — you can filter "show me every game where Result =
  win" without text parsing.
- **Two separate health checks**, because two different systems ask "are you okay?" for
  different reasons:
  - `GET /health/live` — "is the process even running?" A load balancer asks this. It
    returns 200 as long as the app is responding at all.
  - `GET /health/ready` — "are you ready to handle traffic?" An orchestrator (Kubernetes,
    ECS) asks this before sending users your way. It actually checks the external random
    API and Redis, and says "not ready" if they're down.

  Keeping them separate matters: if `/live` also checked Redis, a Redis blip would make
  the orchestrator think the app crashed and restart it in a loop — making things worse.
- **CORS limited to `GET`, `POST`, `DELETE`** — only the methods the frontend actually
  uses. The lazy option (`AllowAnyMethod()`) would also permit PUT, PATCH, etc., which we
  never need. Allowing only what's necessary is just good security hygiene.

## Patterns and Libraries

A quick tour of the smaller choices and why each one is there:

- **Primary constructors (C# 12)** — the `class Foo(IBar bar)` syntax. It's a shorter way
  to do dependency injection without writing out a constructor and a backing field.
- **`record` types for DTOs** (`Choice`, `PlayResult`, `PlayRequest`, `ErrorResponse`) —
  records are immutable and compare by value. API request/response shapes are read-only
  data, so records fit perfectly and are less code than classes.
- **`GameRules` static class** — the shared rules, pulled out so neither service
  duplicates them and tests can hit them directly.
- **`ErrorResponse` record** — a consistent `{ "error": "..." }` shape for error
  responses (429, 500), so the frontend always gets errors in the same format.
- **Swagger / OpenAPI in Development only** — interactive API docs while developing, but
  not exposed in production. The endpoints are annotated so Swagger shows the *real*
  response shapes, not generic ones.
- **`IMemoryCache` over a plain `Dictionary`** — for the expiration/eviction behavior
  explained earlier.
- **`[Range]` attribute on `PlayRequest`** — input validation declared right on the
  model. ASP.NET checks it automatically and returns a 400 before my code even runs, so I
  don't repeat validation in the controller.
- **`ActionResult<T>` return types** — lets Swagger figure out the response type
  automatically. The alternative (`IActionResult`) hides that information.
- **CORS origin in config** — the allowed frontend URL lives in `appsettings.json` and
  can be overridden with an environment variable per environment, so I never hard-code a
  production URL into the source.

## Frontend Architecture

The frontend is a **React 18 + TypeScript + Vite** single-page app. The guiding idea is
lots of small, focused files rather than a few giant ones.

**One folder per component.** Each component keeps its code and its styles together, e.g.
`components/ChoiceGrid/ChoiceGrid.tsx` next to `ChoiceGrid.css`. A single "barrel" file
(`components/index.ts`) re-exports everything, so other files import from `"../components"`
and don't need to know the internal folder layout.

I *considered* giving every component its own tiny `index.ts` too, but that's just a
one-line file that re-exports the file sitting right next to it — pure noise at this
size, so I skipped it. One exception to the barrel rule: when a component uses a sibling
component (e.g. `Scoreboard` shows the `HowToPlay` modal), it imports that sibling
*directly* rather than through the barrel. Going through the barrel there would make the
file import a file that imports it back — a circular import — which tools dislike.

**All the game state lives in one hook: `useGame()`.** Choices, the current result, the
scoreboard, the loading status, and any error message are all owned here. The components
themselves are "dumb" — they just receive data and callbacks and render. Keeping state
in one place makes it far easier to reason about and to test.

**`choiceMap` is shared via Context, not passed down as props.** `choiceMap` is just a
lookup from id to name (`{ "1": "rock", ... }`). It's created at the top of the app but
needed deep down in `ResultCard` and `Scoreboard`. I could pass it as a prop through
every component in between, but that "prop drilling" is tedious and clutters components
that don't even use it. Instead I provide it once with `ChoiceProvider` and read it with
a `useChoiceMap()` hook wherever it's needed. React Context is built for exactly this:
a value that lots of components read but rarely changes.

**Canceling in-flight requests with `AbortController`.** Imagine you click a choice, then
immediately click another before the first response comes back. Without care, the slow
first response could land *after* the second and overwrite it with stale data. To prevent
that, `useGame` keeps an `AbortController` and cancels the previous request before
starting a new one (and cancels everything if the component unmounts). A canceled request
throws a special `AbortError`, which I deliberately ignore — it's expected, not a real
failure to show the user.

**Error Boundary.** `ErrorBoundary` wraps the whole app so that if a component throws
while rendering, the user sees a friendly "something went wrong, refresh" message instead
of a blank white screen. This is the one spot where I had to use an old-style class
component — React still has no hook equivalent for catching render errors.

**Configuration at runtime, not build time.** The app needs to know the API's URL. The
naive approach bakes that URL into the JavaScript when you build it — but then the built
files only work for *one* environment, and you'd have to rebuild for staging vs.
production. Instead, the app reads the URL from `window.__ENV__.API_URL`, which is set by
a tiny `public/env.js` file. In Docker, the startup script (`docker-entrypoint.sh`)
writes that file from an environment variable when the container boots. So the *exact
same* built image runs anywhere — you just change one env var. (For plain local `npm run
dev` there's a `VITE_API_URL` fallback.)

**Theming with CSS variables.** Colors, fonts, and shadows are defined once as CSS
variables in `index.css`, with a `prefers-color-scheme: dark` block that overrides them
for dark mode. Components use `var(--bg)`, `var(--text-h)`, etc., never raw hex codes.
Result: dark mode works automatically and changing the palette means editing one file.

**Accessibility.** Buttons have `aria-label`s, the result card is an `aria-live` region
(so screen readers announce who won), decorative emoji are marked `aria-hidden` (so they
aren't read aloud), and the How-to-play popup uses the native `<dialog>` element, which
gives keyboard focus trapping and Escape-to-close for free.

## Security Headers

`SecurityHeadersMiddleware` adds a few protective HTTP headers to every response:

- `X-Content-Type-Options: nosniff` — stops the browser from guessing (and mis-guessing)
  a file's type.
- `X-Frame-Options: DENY` — stops other sites from embedding ours in an iframe
  (anti-clickjacking).
- `Referrer-Policy: no-referrer` — don't leak our URLs to other sites.

I left out HSTS (which forces HTTPS) on purpose: HTTPS is terminated at the load
balancer/ingress in front of the app, and that's the correct place to set HSTS — not in
the app itself.

## Docker

**Backend Dockerfile** is multi-stage: the big .NET SDK image compiles the code, **runs
the tests** (if a test fails, the whole build fails — you can't ship broken code), then
publishes. The final image is a small runtime image with just the published output — the
SDK and source never ship. It listens on plain HTTP port 8080 because, again, HTTPS is
handled by the load balancer in front.

**Redis** runs as a sidecar container (`redis:7-alpine`) with a `redis-cli ping`
healthcheck. The backend `depends_on` it with `condition: service_healthy`, so it waits
for Redis to actually be ready before starting.

**Frontend Dockerfile** is also multi-stage: a Node stage builds the static files, then
an `nginx:alpine` image serves them. nginx also forwards any `/api/*` request to the
backend (stripping the `/api` prefix). Because the browser then talks to a single origin,
there's no CORS issue at all in the Docker setup. And thanks to the runtime-config trick
above, the one image works in any environment.

**Switching environments** uses Docker Compose's override feature. `docker-compose.yml`
holds the production-shaped defaults; `docker-compose.override.yml` is picked up
automatically when you run locally and layers on dev settings (like
`ASPNETCORE_ENVIRONMENT=Development`). One base file, small overrides — instead of a
whole separate compose file per environment.

## Known Trade-offs & Future Work

These are places where I *consciously* stopped short of a bigger solution because the
scope didn't justify it. I'm writing them down so the limits are intentional and visible,
not surprises someone trips over later.

- **The in-memory scoreboard has a small write race.** As covered above, two simultaneous
  writes for the same player could lose one result. I didn't add locking because this path
  only runs in local dev — production uses Redis, which doesn't have the problem. Adding
  locks to code that never runs in production would be complexity for nothing.

- **`CancellationToken` isn't threaded through the backend.** Controller and service
  methods don't take a cancellation token. For operations this fast it barely matters, and
  the error middleware already handles client disconnects cleanly. If a slow operation
  appeared later (a database query, a long external call), I'd pass
  `HttpContext.RequestAborted` down so it could be canceled.

- **Redis is tested with a mock, not a real server.** The Redis tests fake the connection,
  which proves the *command logic* is right (correct keys, batched calls, error handling)
  but not a real save-and-load round-trip. For production I'd add a real throwaway Redis in
  CI using [Testcontainers](https://dotnet.testcontainers.org/).

- **The external random API URL is hard-coded.** It sits in `RandomService` rather than
  config. With multiple environments I'd move it to `appsettings.json`. It's hard-coded
  here because there's exactly one upstream and it never changes.

- **Random-API failures are invisible to the player on purpose.** When the upstream is
  down we silently fall back to local randomness and the player gets a normal result. This
  is an availability-first choice — the game should keep working. It's fully visible in the
  logs (`FallbackUsed=true`), so operators can still see and alert on it; the player just
  doesn't need to.

- **There's no true end-to-end test across the whole stack.** The backend has solid
  in-memory integration tests and the frontend has component/hook tests, but nothing boots
  the real API and drives the real UI against it together. Because the frontend tests fake
  the network, a mismatch between front and back (a renamed field, different casing)
  wouldn't be caught by either suite. For production I'd add a Playwright test that runs
  `docker compose up` and plays a full round over real HTTP — that's the layer that catches
  cross-boundary bugs.

- **`ErrorBoundary` has no test.** It works, but no test deliberately makes a child throw
  and checks the fallback appears. Low risk (the component is tiny and has no logic), but
  it's a genuine gap.

## Testing

The tests are layered so each layer is checked at the cheapest level that's still
meaningful — fast unit tests for logic, heavier integration tests only where they earn it.

**Backend unit tests:**

- `GameServiceTests` — all 10 win pairs, all 5 ties, all 10 lose pairs, invalid input,
  scoreboard isolation between players, the cap-at-10 rule, newest-first ordering, and that
  reading the scoreboard returns a copy (so callers can't mutate the stored list). Written
  against the `IGameService` interface, not the concrete class.
- `GameRulesTests` — the shared rule logic directly: every outcome, every choice name,
  invalid id throws.
- `RedisGameServiceTests` — use a **mocked** Redis connection. They verify the right keys
  are used, the LPUSH/LTRIM/EXPIRE batch is sent, corrupt entries are skipped, an empty
  list reads back as empty, and a `RedisException` never crashes anything.
- `ErrorHandlingMiddlewareTests` — the happy path, an unhandled exception becoming a 500
  with a JSON body, a client disconnect *not* becoming a 500, and the
  response-already-started case. These use a fake `HttpContext`, no real server needed.

**Backend integration tests** (`GameControllerTests` and friends) use
`WebApplicationFactory<Program>`, which boots the *real* ASP.NET pipeline in memory and
sends real HTTP requests to it. The external random service is swapped for a deterministic
fake. Each test class gets its own factory so scoreboards don't leak between tests. This
group also covers rate limiting, CORS, both health checks, the `X-Player-Id` logic
(isolation, IP fallback, and the 128-character guard), the security headers, and the
random-service fallback.

**Frontend tests** run on Vitest + React Testing Library + happy-dom. (I chose happy-dom
over the more common jsdom to dodge a dependency conflict.) They're layered the same way:
the API layer test checks headers, request shapes, 204 handling, and error parsing; the
`useGame` hook test covers init/play/reset/the cap-at-10 rule/error states; each component
has a focused test; and an integration test drives the whole app through loading → ready →
play → reset against a faked API. (As noted in the trade-offs, this faking is exactly why
there's no true cross-stack E2E test yet.)

## AI Usage

I used Claude (the claude-code CLI) throughout this project as a pair programmer — to
scaffold the initial structure, explain .NET patterns, generate boilerplate, and catch
bugs. I reviewed all of it, and the reasoning behind each decision here is my own. The
tool sped up the typing; it didn't replace the understanding.
