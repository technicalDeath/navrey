---
name: navrey-shard-test
description: Run the Navrey agent client against the local Britannia Renaissance ModernUO shard and collect live state or command evidence; use for headless login, smoke tests, and feature rehearsals.
---

# Navrey shard testing

Navrey is the active agent-capable ClassicUO fork. Read `README.md` and relevant guides in `.claude/skills/` for gameplay workflows. `CLAUDE.md` explains command/state behavior, but its `/tmp` examples are Unix defaults; Windows defaults live under `%LOCALAPPDATA%\Temp\Navrey` and this workspace can use separate paths per session. Use the adjacent `../ShardContent/.agents/skills/britannia-renaissance/SKILL.md` for repository and world-state boundaries.

## Configuration and launch

`settings.json` is the shard/client source of truth: host, port, client version, UO data path, POI directory, server/character selection. `.env` holds `UO_USER` and `UO_PASS` and is ignored by Git. Do not print or commit its contents. Navrey reads both from its working directory; no CLI host or credential override exists. For this local shard, verify `127.0.0.1:2593`, a matching client version, and the adjacent `UOData` directory. Start ModernUO first.

On Windows, run the DLL through the workspace SDK from `Navrey/`; the apphost can fail when .NET 10 is not registered system-wide. Build client and CLI separately when code changed:

```powershell
$env:DOTNET_ROOT = (Resolve-Path ..\dotnet).Path
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
& "$env:DOTNET_ROOT\dotnet.exe" build .\src\ClassicUO.Client\ClassicUO.Client.csproj
& "$env:DOTNET_ROOT\dotnet.exe" build .\cli\Navrey.Cli.csproj
& "$env:DOTNET_ROOT\dotnet.exe" .\bin\Debug\net10.0\cuo.dll -agent -headless
```

`Start Navrey.cmd` starts the agent client with a window. For an isolated test while another client may be active, pass **all four** unique `-cmdfile`, `-logfile`, `-statefile`, and `-worldfile` paths to the DLL. Keep the process attached to a managed terminal session and stop that exact process when done; a second launcher can otherwise attach to stale state. The `cli/bin/Debug/net10.0/navrey.dll` CLI is available for interactive use but must match the client build.

## Evidence and commands

- Confirm the process is alive, then read the JSON state file. `inGame: true`, fresh `updatedAtMs`, character name/position, and world data prove login; `inGame` alone can be stale after a crash.
- Append one command per line to the configured command file. Read the matching `[CMD]` / `[CMD-END]` block and subsequent `[SYSTEM]` lines in the log. A successful send only proves delivery, so inspect server response and state. For local read-only checks, `status`, `world`, `say [IntentStatus`, and (with staff access) `say [ShardRulesStatus`, `say [MurderStatus`, `say [TheftStatus`, and `say [KnockedOutStatus` are useful.
- Poll state/world files for character and nearby-world observations. `goto` starts asynchronously; wait for `.nav.status` and `.nav.active`, not the command's immediate success. Use the relevant `.claude/skills/` guide before any multi-step gameplay test. Avoid combat, theft, movement, item transfer, account creation, or save mutation on the user's main world unless the task requires it; use disposable staging for destructive matrices.
- `[Rules` is absent from the checked server command registration; do not use it as a smoke-test expectation. Check commands against the current ShardContent source when the server changes.

Stop the client before asking the server to save and shut down. Do not include `.env`, account identifiers, or private log dumps in commits or skill files.
