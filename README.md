<div align="center">

![TUFReplay](https://capsule-render.vercel.app/api?type=waving&height=220&color=0:050505,45:101827,100:5eead4&text=TUFReplay&fontColor=e6fffb&fontAlignY=38&desc=Clear,%20Record,%20Edit,%20Upload,%20Submit,%20Wait%20->%20Clear,%20Submit%20&descAlignY=58&animation=fadeIn)

[![Runtime](https://img.shields.io/badge/runtime-ADOFAI%20%2F%20Unity-111827?style=for-the-badge&logo=unity&logoColor=white)](https://store.steampowered.com/app/977950/A_Dance_of_Fire_and_Ice/)
[![Mod Loader](https://img.shields.io/badge/mod%20loader-UnityModManager-7c3aed?style=for-the-badge)](https://www.nexusmods.com/site/mods/21)
[![Framework](https://img.shields.io/badge/framework-JALib-f43f5e?style=for-the-badge)](https://github.com/Jongye0l/JALib)
[![Build](https://img.shields.io/badge/build-.NET%20SDK-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Target](https://img.shields.io/badge/target-netstandard2.1-2563eb?style=for-the-badge)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![Database](https://img.shields.io/badge/database-SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)](https://sqlite.org/)
[![API](https://img.shields.io/badge/api-AdofaiIpc-f97316?style=for-the-badge)](#adofaiipc-api)

<br />

![Tech stack](https://skillicons.dev/icons?i=cs,dotnet,unity,sqlite,bash)

**ADOFAI will now automatically records your clear, and you can use it for submitting to TUF**

</div>

<div align="center">
  <img
    src="docs/media/showcase.gif"
    alt="showcase-gif"
    width="900"
  />
  <br />
</div>

## Overview

TUFReplay is a UnityModManager/JALib mod for **A Dance of Fire and Ice**. It records OS-native keyboard state changes for replay keyviewer/display output, records CReplay-style hit contexts for game playback, stores play records in a local SQLite database, exposes those records through AdofaiIpc, and plays saved runs directly from the companion web UI.

The project is built around preserving low-level play data instead of trusting final judgment labels. That makes the recorded output more useful for server-side validation, exports, dashboards, and future replay workflows.

## Features

- Records OS-native keyboard state changes and hit contexts for every custom `.adofai` run.
- Stores lean activity records, replay payloads, level paths, and recorder timezone context in SQLite.
- Exposes local IPC methods for activity browsing and health checks through AdofaiIpc.
- Serves the current chart text to the companion web UI without exposing local file paths.
- Opens a saved run's recorded level, reuses an already-open matching editor level, and replays it from its recorded start tile.
- Keeps recording input after a clear until the editor returns so post-clear keyviewer input is preserved.
- Optionally identifies TUFHelperLite-downloaded levels for future TUF submission workflows.
- Provides the project foundation for replay playback and TUF clear submission.

## Runtime

TUFReplay runs inside ADOFAI through UnityModManager and JALib.

Required at runtime:

- A Dance of Fire and Ice
- UnityModManager
- JALib
- AdofaiIpc
- TUFReplay installed under the ADOFAI `Mods/TUFReplay` directory

TUFHelperLite is optional. When installed, TUFReplay resolves its downloaded level paths to public TUF forum IDs; recording itself does not depend on it.

## Repository Layout

- `TUFReplay/`: UnityModManager/JALib mod source.
- `web/`: Bun/Vite companion web UI, managed as a workspace package.

## Build

Copy the environment template and adjust paths if your setup differs from the macOS Steam default:

```bash
cp .env.example .env
```

Build and install the mod:

```bash
./build.sh
```

The build script:

- Builds `TUFReplay/TUFReplay.csproj`.
- Copies `Info.json`, `JAModInfo.json`, `JAMod.Bootstrap.dll`, and `TUFReplay.dll`.
- Copies managed dependencies into `Mods/TUFReplay/dependency`.
- Installs the mod into `Mods/TUFReplay` by default.

Important environment variables:

- `ADOFAI_DIR`: ADOFAI install directory.
- `ADOFAI_MODS_DIR`: ADOFAI Mods directory.
- `ADOFAI_MANAGED`: Unity managed assembly directory.
- `DOTNET_EXE`: .NET SDK executable.
- `JALIB_DLL`: JALib assembly path.
- `ADOFAI_IPC_DLL`: AdofaiIpc assembly path.
- `TUFREPLAY_INSTALL_DIR`: install output override.

Create a clean shareable package:

```bash
./package.sh
```

The package script builds the mod into `build/TUFReplay.zip` without copying data from an installed `Mods/TUFReplay` directory. It includes the Windows x64 SQLite native library from the `SourceGear.sqlite3` NuGet package and excludes local database/log data from the package.

## Web Development

Install the Bun workspace dependencies from the repository root:

```bash
bun install
```

Run the companion web UI:

```bash
bun run web:dev
```

To run the UI against the bundled activity, chart, run, and replay mock data instead of AdofaiIpc, open the development URL with `?mock=1` (for example, `http://localhost:5174/?mock=1`) or start Vite with `VITE_USE_MOCK_ACTIVITY=true`.

Test, type-check, or build the web workspace:

```bash
bun run web:test
bun run web:typecheck
bun run web:build
```

The web UI is built and deployed independently. `build.sh` and `package.sh` continue to build and package only the ADOFAI mod.

The browser reads TUF metadata through the same-origin `/api/tuf/*` path to avoid CORS failures. The Vite development and preview servers proxy that path to `https://api.tuforums.com`; production hosting must configure the equivalent rewrite while preserving the remaining path (for example, `/api/tuf/v2/database/levels/byId/871` → `https://api.tuforums.com/v2/database/levels/byId/871`).

## AdofaiIpc API

The local API is intended for the companion web UI and development tools. Clients should find AdofaiIpc by probing `/ipc/health`, then call TUFReplay through:

```http
POST /ipc
Content-Type: application/json
```

```json
{
  "namespace": "tuf-replay",
  "method": "health.get",
  "params": {},
  "id": "optional-client-id"
}
```

Registered methods:

- `health.get`
- `activity.app-sessions.list`
- `activity.level-session.get`
- `activity.level-session.runs.list`
- `activity.level-session.chart.get`
- `replay.play`
- `replay.status.get`

## Tech Stack

- **C# / .NET SDK**: mod implementation and build tooling.
- **netstandard2.1**: target framework for Unity compatibility.
- **UnityModManager**: ADOFAI mod loading.
- **JALib**: JAMod lifecycle, settings, and mod structure.
- **AdofaiIpc**: shared localhost IPC listener and namespace routing.
- **Harmony**: game method patching for recording hooks.
- **TUFHelperLite**: optional public TUF level ID resolution for downloaded charts.
- **SQLite / Microsoft.Data.Sqlite**: local play record storage.
- **Newtonsoft.Json**: metadata and API JSON serialization.
- **Bun / React / Vite**: companion activity explorer and embedded chart bridge.
- **Bash / .env**: local build and install configuration.

## Special Thanks

- **Teo** — Gave his idea to me and started this project.
