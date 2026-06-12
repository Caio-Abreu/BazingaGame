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
- Health check: `http://localhost:5000/health`

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
| `Game:ScoreboardExpirationHours` | `6` | Hours of inactivity before a player's scoreboard is evicted from cache |
| `Cors:AllowedOrigin` | `http://localhost:5173` | Frontend origin allowed by CORS |

Can be overridden with environment variables: `Game__ScoreboardExpirationHours=12`, `Cors__AllowedOrigin=https://mygame.com`

## Contributing

### Branch Strategy

| Branch | Purpose |
|--------|---------|
| `main` | Production-ready code — never commit directly |
| `develop` | Integration branch — all feature work merges here first |
| `feature/*` | New features — branch off `develop` |
| `fix/*` | Bug fixes — branch off `develop` |
| `docs/*` | Documentation only — branch off `develop` |

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
