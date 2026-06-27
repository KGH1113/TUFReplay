<div align="center">

![TUFReplay](https://capsule-render.vercel.app/api?type=waving&height=220&color=0:050505,45:101827,100:5eead4&text=TUFReplay&fontColor=e6fffb&fontAlignY=38&desc=Clear,%20Record,%20Edit,%20Upload,%20Submit,%20Wait%20->%20Clear,%20Submit%20&descAlignY=58&animation=fadeIn)

[![Runtime](https://img.shields.io/badge/runtime-ADOFAI%20%2F%20Unity-111827?style=for-the-badge&logo=unity&logoColor=white)](https://store.steampowered.com/app/977950/A_Dance_of_Fire_and_Ice/)
[![Mod Loader](https://img.shields.io/badge/mod%20loader-UnityModManager-7c3aed?style=for-the-badge)](https://www.nexusmods.com/site/mods/21)
[![Framework](https://img.shields.io/badge/framework-JALib-f43f5e?style=for-the-badge)](https://github.com/Jongye0l/JALib)
[![Build](https://img.shields.io/badge/build-.NET%20SDK-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Target](https://img.shields.io/badge/target-netstandard2.1-2563eb?style=for-the-badge)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![Database](https://img.shields.io/badge/database-SQLite-003B57?style=for-the-badge&logo=sqlite&logoColor=white)](https://sqlite.org/)
[![API](https://img.shields.io/badge/api-local%20HTTP-f97316?style=for-the-badge)](#local-http-api)

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

TUFReplay is a UnityModManager/JALib mod for **A Dance of Fire and Ice**. It records OS-native keyboard state changes for replay keyviewer/display output, records CReplay-style hit contexts for game playback, stores play records in a local SQLite database, serves those records through a localhost HTTP API, and is designed to support replay playback and TUF submission tooling.

The project is built around preserving low-level play data instead of trusting final judgment labels. That makes the recorded output more useful for server-side validation, exports, dashboards, and future replay workflows.

## Features

- Records OS-native keyboard state changes during eligible TUFHelper-opened clears.
- Stores records, metadata, input payloads, and future microphone recordings in SQLite.
- Serves local HTTP APIs for record browsing, deletion, health checks, and opening recorded levels.
- Opens recorded TUF levels through TUFHelper.
- Provides the project foundation for replay playback and TUF clear submission.

## Runtime

TUFReplay runs inside ADOFAI through UnityModManager and JALib.

Required at runtime:

- A Dance of Fire and Ice
- UnityModManager
- JALib
- TUFHelper
- TUFReplay installed under the ADOFAI `Mods/TUFReplay` directory

The mod starts a localhost server at:

```text
http://127.0.0.1:32145/
```

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
- `TUFHELPER_DLL`: TUFHelper assembly path.
- `TUFREPLAY_INSTALL_DIR`: install output override.

Create a clean shareable package:

```bash
./package.sh
```

The package script builds the mod into `build/TUFReplay.zip` without copying data from an installed `Mods/TUFReplay` directory. It includes the Windows x64 SQLite native library from the `SourceGear.sqlite3` NuGet package and excludes local database/log data from the package.

## Local HTTP API

The local API is intended for the companion web UI and development tools.

Available endpoints:

- `GET /api/health`
- `GET /api/records`
- `GET /api/records/:id`
- `DELETE /api/records/:id`
- `POST /api/records/:id/open`

## Tech Stack

- **C# / .NET SDK**: mod implementation and build tooling.
- **netstandard2.1**: target framework for Unity compatibility.
- **UnityModManager**: ADOFAI mod loading.
- **JALib**: JAMod lifecycle, settings, and mod structure.
- **Harmony**: game method patching for recording hooks.
- **TUFHelper**: TUF level metadata and level-opening integration.
- **SQLite / Microsoft.Data.Sqlite**: local play record storage.
- **Newtonsoft.Json**: metadata and API JSON serialization.
- **HttpListener**: localhost API server.
- **Bash / .env**: local build and install configuration.

## Special Thanks

- **Teo** — Gave his idea to me and started this project.
