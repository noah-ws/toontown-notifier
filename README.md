# Toontown invasion notifier

A .NET worker that watches live Cog invasions and pings a Discord webhook when one matches your ToonTasks.

ToonSync codes cannot be used by a custom app (they are limited to approved sites like ToonHQ, which also do not sync ToonTasks). This app reads tasks from TTR’s local Companion App API while you are logged in, caches them, and falls back to `tasks.yaml` when the game is closed.

Invasions come from the official [TTR invasions API](https://www.toontownrewritten.com/api/invasions) (the same feed ToonHQ displays). Discord messages link to [ToonHQ’s invasion tracker](https://toonhq.org/invasions/).

## Setup

1. Copy `.env.example` to `.env` and paste a Discord channel webhook URL.
2. Optional: edit `tasks.yaml` with cogs or departments you care about when TTR is not running.
3. In Toontown Rewritten, enable **Companion App Support** in Options.

### Docker Compose (recommended)

```bash
cp .env.example .env   # then edit DISCORD_WEBHOOK_URL
docker compose up --build -d
docker compose logs -f
```

On first login, approve the in-game Companion App prompt for **ToontownNotifier**. Leave the container running. Tasks refresh while you play; after you log out, the last snapshot is used until you play again.

The container reaches the game on your PC via `host.docker.internal`. If Companion App never connects from Docker, either run the worker on the host (`dotnet run`) or fill in `tasks.yaml`.

### Run on the host

```bash
dotnet run --project src/ToontownNotifier
```

Run this from the repo root so `tasks.yaml` and `.env` resolve. You can also set `DISCORD_WEBHOOK_URL` in the environment instead of `.env` (the worker reads environment variables; it does not load `.env` unless you use Compose).

## How matching works

Priority: live Companion App → last-known cache → `tasks.yaml`.

- Named cog (“Defeat 5 Pencil Pushers”) matches that invasion type, including Version 2.0 / Skelecog variants.
- Department (“Defeat 10 Sellbots”) matches any invading cog in that department.
- Generic (“Defeat 10 Cogs”) matches any invasion.
- Fishing, delivery, visits, and building/facility tasks are ignored.

Each invasion is notified once per district + cog type until it ends.

## Config

| Variable | Meaning |
| --- | --- |
| `DISCORD_WEBHOOK_URL` | Discord incoming webhook (required for pings) |
| `POLL_INTERVAL_SECONDS` | How often to check invasions (default 30) |
| `COMPANION_HOST` | Companion API host (`127.0.0.1` locally, `host.docker.internal` in Compose) |
| `STATE_PATH` | Cached tasks + already-notified invasions |
| `TASKS_YAML_PATH` | Manual fallback list |
