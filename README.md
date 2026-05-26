# CrewJet

Field service management platform for trades businesses (starting with electrical contractors).

## Local Development (Windows + Rider + Docker Desktop)

### One-time prerequisites
- Docker Desktop running
- .NET 10 SDK
- (Optional) JetBrains Rider

### Start the databases (required)
```pwsh
docker compose -f docker-compose.yaml up -d
```

This automatically:
- Creates **both** required PostgreSQL databases (`crewjet` + `openiddict`) on first run.
- Persists data in a named Docker volume.

### Run the applications
From Rider (recommended) or command line:

1. **CrewJet.Auth** (OpenIddict authorization server + identity UI) — default port 5001 (or see launchSettings)
2. **CrewJet.Server** (main Blazor Server + WASM host) — default port 5000

Both projects read their connection strings from `appsettings.Development.json` and point at `localhost:5432`.

### What happens automatically
- **Databases**: created by the compose init script (first run only).
- **Auth migrations**: `OpenIddictDbContext` EF Core migrations are applied automatically on startup when `ASPNETCORE_ENVIRONMENT=Development`.
- **Marten schemas** (both Auth and Server): auto-created via `AutoCreate.All` in Development. No manual migrations needed.

You should never need to run `dotnet ef database update` or manually create databases for local work.

### Common commands
```pwsh
# Start / restart just the DBs
docker compose -f docker-compose.yaml up -d

# View logs
docker compose -f docker-compose.yaml logs -f postgres

# Stop and remove containers (data stays in the volume)
docker compose -f docker-compose.yaml down

# Nuclear option — delete the DB volume (next up will re-run init scripts)
docker compose -f docker-compose.yaml down -v
```

### Two compose files exist — use the right one
| File                    | Purpose                                      | When to use                              |
|-------------------------|----------------------------------------------|------------------------------------------|
| `docker-compose.yaml`   | Postgres for local dev (this file)           | Daily local development (you are here)   |
| `compose.yaml`          | Builds CrewJet.Client + CrewJet.Server images| Full containerized runs (future)         |

### Connection strings (for reference)
See `CrewJet.Auth/appsettings.Development.json` and `CrewJet.Server/appsettings.Development.json`.

### Next steps once running
- Auth will be available for OpenIddict flows.
- Server hosts the main Blazor app (with WASM client).

See `Docs/ROADMAP.md` for the larger technical direction.
