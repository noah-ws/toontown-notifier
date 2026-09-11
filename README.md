# Toontown invasion notifier

A .NET worker that watches live Cog invasions and pings a Discord webhook when one matches your ToonTasks.

ToonSync codes cannot be used by a custom app (they are limited to approved sites like ToonHQ, which also do not sync ToonTasks). This app reads tasks from TTR’s local Companion App API while you are logged in, caches them, and falls back to `tasks.yaml` when the game is closed.

Invasions come from the official [TTR invasions API](https://www.toontownrewritten.com/api/invasions) (the same feed ToonHQ displays). Discord messages link to [ToonHQ’s invasion tracker](https://toonhq.org/invasions/).

## Setup

1. Copy `.env.example` to `.env` and paste a Discord channel webhook URL.
2. Optional: edit `tasks.yaml` with cogs or departments you care about when TTR is not running.
3. In Toontown Rewritten, enable **Companion App Support** in Options, then log into a Toon.

### Docker Compose (Windows)

TTR’s Companion API listens only on `127.0.0.1`, which Docker cannot reach. `start.ps1` runs a tiny host proxy on port 11547 and then starts the container.

```powershell
.\start.ps1
docker compose logs -f
```

On first connect, approve the in-game Companion App prompt. Leave it running. Tasks refresh while you play; after you log out, the last snapshot is used until you play again.

```powershell
.\stop.ps1
```

If you see `TTR Companion App is not reachable`, the game is usually open without Companion App enabled, or you are still on the login/toon-select screen. Invasions still poll; matching waits for live tasks, cache, or `tasks.yaml`.

### Run on the host

This talks to TTR on localhost directly (no proxy):

```bash
dotnet run --project src/ToontownNotifier
```

Run this from the repo root so `tasks.yaml` and `.env` resolve.

### Test Discord ping

Sends one webhook immediately (uses a live invasion if any exist, otherwise a fake Pencil Pusher) and exits. Does not mark that invasion as already notified.

```powershell
dotnet run --project src/ToontownNotifier -- --test-discord
```

Or from Docker:

```powershell
docker compose run --rm --no-deps notifier --test-discord
```

## How matching works

Priority: live Companion App cog tasks → `MOCK_TASK` → `tasks.yaml` → last-known cache.

If you have no cog ToonTasks, set `MOCK_TASK=any` in `.env` (matches every invasion) or fill in `tasks.yaml`. `tasks.yaml` currently has `any_cog: true` for testing; set it back to `false` when you have real tasks.

- Named cog (“Defeat 5 Pencil Pushers”) matches that invasion type, including Version 2.0 / Skelecog variants.
- Department (“Defeat 10 Sellbots”) matches any invading cog in that department.
- Generic (“Defeat 10 Cogs”) is ignored — it would match every invasion.
- Fishing, delivery, visits, and building/facility tasks are ignored.

Each invasion is notified once per district + cog type until it ends.

## Config

| Variable | Meaning |
| --- | --- |
| `DISCORD_WEBHOOK_URL` | Discord incoming webhook (required for pings) |
| `POLL_INTERVAL_SECONDS` | How often to check invasions (default 30) |
| `COMPANION_HOST` | Companion API host (`127.0.0.1` locally, `host.docker.internal` in Compose) |
| `COMPANION_PROXY_PORT` | Host proxy port used from Docker (default 11547) |
| `MOCK_TASK` | Fake task for testing: `any`, a cog name, or a department |
| `STATE_PATH` | Cached tasks + already-notified invasions |
| `TASKS_YAML_PATH` | Manual fallback list |
