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

## Game Rules

Rock crushes Scissors and Lizard.  
Paper covers Rock and disproves Spock.  
Scissors cuts Paper and decapitates Lizard.  
Lizard poisons Spock and eats Paper.  
Spock smashes Scissors and vaporizes Rock.
