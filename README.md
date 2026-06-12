# Rock, Paper, Scissors, Lizard, Spock

Full-stack implementation of RPSSL for the Billups coding challenge.

## Stack

- **Backend:** .NET 8, ASP.NET Core Web API
- **Frontend:** React 18, TypeScript, Vite

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) — verify with `dotnet --version` (must be 8.x)
- [Node.js 20+](https://nodejs.org/) — verify with `node --version`

## Running Locally

You need **two terminals open at the same time** — one for the backend, one for the frontend.

### 1. Clone the repo

```bash
git clone git@github.com:Caio-Abreu/BazingaGame.git
cd BazingaGame
```

### 2. Start the backend (Terminal 1)

```bash
cd bazinga-game   # this is the API project folder
dotnet run
```

Wait until you see:
```
Now listening on: http://localhost:5000
```

- API: `http://localhost:5000`
- Swagger UI (interactive docs): `http://localhost:5000/swagger`
- Liveness: `http://localhost:5000/health/live`
- Readiness: `http://localhost:5000/health/ready`

### 3. Start the frontend (Terminal 2)

```bash
cd frontend
npm install      # only needed the first time
npm run dev
```

Wait until you see:
```
Local: http://localhost:5173/
```

Open `http://localhost:5173` in your browser — the game is ready.

### 4. Run the tests

From the repo root:

```bash
dotnet test
```

## Running with Docker (full stack)

Two compose files control the environment:

| File | Purpose |
|------|---------|
| `docker-compose.yml` | Base config — production defaults |
| `docker-compose.override.yml` | Dev overrides — auto-merged locally |

### Development (default)

`docker-compose.override.yml` is picked up automatically, setting the backend to `Development` mode (detailed errors, Swagger UI enabled):

```bash
docker compose up --build
```

Swagger UI is available at `http://localhost/api/swagger` in this mode.

### Production

Explicitly ignore the override file to use production settings only:

```bash
docker compose -f docker-compose.yml up --build
```

Open `http://localhost` in your browser — the game is ready.

- Frontend: `http://localhost` (nginx, port 80)
- Backend API: accessible internally; also reachable via `http://localhost/api/`

To stop:

```bash
docker compose down
```

### Backend only (no frontend)

```bash
docker build -t bazinga-game .
docker run -p 8080:8080 bazinga-game
```

API starts at `http://localhost:8080`.

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/choices` | All 5 choices |
| GET | `/choice` | Random computer choice (via external service) |
| POST | `/play` | Play a round — body: `{ "player": 1-5 }` |
| GET | `/scoreboard` | Last 10 results for the current player |
| DELETE | `/scoreboard` | Reset the current player's scoreboard |
| GET | `/health/live` | Liveness — is the process up? (load balancer) |
| GET | `/health/ready` | Readiness — is the external random API reachable? (orchestrator) |

### Player Identity

The scoreboard is **per-player**. The frontend automatically generates a UUID on first visit and stores it in `localStorage`. This UUID is sent as an `X-Player-Id` header on every request. Each player sees only their own history — resetting your scoreboard does not affect other players.

If you call the API directly (e.g. curl, Postman) without the header, the server falls back to your IP address as the player identity.

### Rate Limiting

The API enforces per-IP rate limits to prevent abuse:

| Endpoints | Limit |
|-----------|-------|
| `POST /play`, `DELETE /scoreboard` | 30 requests / minute |
| `GET /choices`, `GET /choice`, `GET /scoreboard` | 60 requests / minute |

Exceeding the limit returns `429 Too Many Requests`.

## Configuration

Key settings in `appsettings.json`:

| Key | Default | Description |
|-----|---------|-------------|
| `ConnectionStrings:Redis` | `""` | Redis connection string — when empty, falls back to in-memory cache |
| `Game:ScoreboardExpirationHours` | `6` | Hours of inactivity before a player's scoreboard is evicted |
| `Cors:AllowedOrigin` | `http://localhost:5173` | Frontend origin allowed by CORS |

Can be overridden with environment variables: `ConnectionStrings__Redis=localhost:6379`, `Game__ScoreboardExpirationHours=12`, `Cors__AllowedOrigin=https://mygame.com`

### Running with Redis locally (without Docker)

```bash
docker run -d -p 6379:6379 redis:7-alpine
```

Then set the connection string before starting the API:

```bash
export ConnectionStrings__Redis=localhost:6379
dotnet run
```

## Contributing

### Branch Strategy

| Branch | Purpose |
|--------|---------|
| `main` | Production-ready, released code — never commit directly. Every commit is a tagged release |
| `develop` | Integration branch — all feature work merges here first. Carries the next `-dev` version |
| `feature/*` | New features — branch off `develop` |
| `fix/*` | Bug fixes — branch off `develop` |
| `docs/*` | Documentation only — branch off `develop` |
| `release/*` | Release preparation — branch off `develop`, merge into `main` |
| `hotfix/*` | Urgent production fix — branch off `main`, merge into `main` |
| `backmerge/*` | Syncs `main` back into `develop` after a release or hotfix |

```bash
# Start a new piece of work
git checkout develop
git pull origin develop
git checkout -b feature/my-feature

# When done, push and open a PR → develop
git push origin feature/my-feature
```

### Commit Convention

Follows [Conventional Commits](https://www.conventionalcommits.org/):

| Prefix | When to use |
|--------|-------------|
| `feat:` | New feature |
| `fix:` | Bug fix |
| `test:` | Adding or updating tests |
| `refactor:` | Code change that isn't a fix or feature |
| `docs:` | README, DECISIONS.md, comments |
| `chore:` | Build config, dependencies, tooling |
| `obs:` | Observability — logging, metrics, tracing |

Examples:
```
feat: add per-player scoreboard with sliding expiration
fix: handle int.MinValue overflow in random number mapping
test: add rate limiting and CORS integration tests
docs: document structured logging decision for random fallback
```

### Versioning & Releasing

Versions follow [Semantic Versioning](https://semver.org/) — `MAJOR.MINOR.PATCH`:

| Bump | When |
|------|------|
| `MAJOR` | Breaking API change |
| `MINOR` | New backward-compatible feature |
| `PATCH` | Bug fix or internal change |

The version lives in two places, kept in sync: `bazinga-game/bazinga-game.csproj`
(`<Version>`) and `frontend/package.json` (`version`). `main` always carries the
released version (e.g. `1.1.0`); `develop` carries the next anticipated release with
a `-dev` suffix (e.g. `1.2.0-dev`) so development builds are never mistaken for a
release. The `AssemblyVersion`/`FileVersion` fields must stay numeric (no suffix).

#### Standard release (from `develop`)

```bash
# 1. Branch a release off develop and bump the version + CHANGELOG
git checkout develop && git pull origin develop
git checkout -b release/v1.2.0
#    edit bazinga-game.csproj, package.json, CHANGELOG.md → commit

# 2. Open a PR  release/v1.2.0 → main  and merge it

# 3. Tag the release commit on main and push the tag
git checkout main && git pull origin main
git tag -a v1.2.0 -m "Release v1.2.0"
git push origin v1.2.0

# 4. Create the GitHub Release from the tag (uses CHANGELOG as notes)
gh release create v1.2.0 --title "v1.2.0" --notes-file CHANGELOG.md

# 5. Back-merge main into develop and bump develop to the next -dev version
git checkout -b backmerge/main-to-develop origin/main
#    bump version to 1.3.0-dev → commit
git push origin backmerge/main-to-develop
#    open a PR  backmerge/main-to-develop → develop  (use a MERGE commit, not squash)
```

#### Hotfix (urgent fix to a deployed release)

```bash
# 1. Branch off main (NOT develop), make the fix, bump the PATCH version
git checkout main && git pull origin main
git checkout -b hotfix/v1.2.1
#    fix + bump csproj/package.json/CHANGELOG → commit

# 2. PR  hotfix/v1.2.1 → main, merge, then tag and release as above

# 3. Back-merge main → develop so the fix isn't lost on the next release
```

> **Why back-merge instead of merging the release branch into both `main` and
> `develop`?** Back-merging `main` gives `develop` the *exact* state of production —
> including the merge commit and any hotfixes that landed on `main` directly — so the
> two branches can never silently diverge.

## AI Usage

[Claude Code](https://claude.ai/code) (Anthropic's CLI) was used throughout this project as a pair programming tool — scaffolding boilerplate, explaining .NET patterns, suggesting refactors, and catching bugs. All architectural decisions, code review, and reasoning behind trade-offs were my own. The tool accelerated implementation; it did not replace understanding.

Specific areas where AI assistance was used:
- Initial project scaffold and folder structure
- Docker and nginx configuration
- Identifying the `random_number` snake_case deserialisation bug in `RandomService`
- Frontend component decomposition and `useGame` hook design
- Unit and integration test setup (Vitest + React Testing Library)

## Game Rules

Rock crushes Scissors and Lizard.  
Paper covers Rock and disproves Spock.  
Scissors cuts Paper and decapitates Lizard.  
Lizard poisons Spock and eats Paper.  
Spock smashes Scissors and vaporizes Rock.
