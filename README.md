<div align="center">

![TUFReplay](https://capsule-render.vercel.app/api?type=waving&height=220&color=0:050505,45:101827,100:5eead4&text=TUFReplay&fontColor=e6fffb&fontAlignY=38&desc=Clear,%20Record,%20Edit,%20Upload,%20Submit,%20Wait%20->%20Clear,%20Submit%20&descAlignY=58&animation=fadeIn)

![Tech stack](https://skillicons.dev/icons?i=cs,dotnet,unity,sqlite,bash)

**ADOFAI now records your clears automatically, ready to submit to TUF.**

[Website](https://tufreplay.impl1113.dev) · [Download](https://github.com/KGH1113/TUFReplay/releases/latest)

</div>

## What it does

- **Records every custom level run**: key presses, judgments and X-Accuracy, saved on your computer.
- **Web companion** at [tufreplay.impl1113.dev](https://tufreplay.impl1113.dev): browse runs by day and level, see where you missed, lock runs you want to keep, export recordings and delete the rest. In English and Korean.
- **API for other mods**: key viewers and overlays can save their state with each run and show the recorded keys in replays and videos.

### Replay a run inside ADOFAI

Pause and seek on the timeline. Any `.adofai` file with the same gameplay works, so you can replay with different effects.

https://github.com/user-attachments/assets/f9688270-67d9-4521-b60c-fed4f32c02b5

### Record your microphone and hand cam

Turn them on and they are recorded with each run, then play in sync in the replay.

https://github.com/user-attachments/assets/3f516781-16d8-4dbb-806e-d28e161f12f5

### Make a video

Turn a run into a video with [OrbitRender](https://github.com/imnyang/OrbitRender).

https://github.com/user-attachments/assets/4ff8e560-0aed-4fb5-8450-0f57c5941387

## Install

1. Install [UnityModManager](https://www.nexusmods.com/site/mods/21) for ADOFAI.
2. Download `TUFReplay.zip` from the [latest release](https://github.com/KGH1113/TUFReplay/releases/latest) and install it with UnityModManager, or extract it into ADOFAI's `Mods` folder.
3. Start ADOFAI and open [tufreplay.impl1113.dev](https://tufreplay.impl1113.dev), or press **Open TUFReplay website** in TUFReplay's UnityModManager settings.

TUFReplay updates itself. Turn on **Receive beta updates** in the same settings to get prereleases.

- **macOS**: allow A Dance of Fire and Ice in System Settings → Privacy & Security → **Input Monitoring**. For the microphone and camera, allow **TUFReplay Microphone Capture**.
- **Windows camera**: needs FFmpeg. The web companion asks before downloading it.
- **Linux**: camera recording is not supported.
- **Videos**: need OrbitRender 2.6.2 or later; the web companion's **Downloads** installs it. With 2.8.1 or later, key viewers and overlays that use the API also appear in the video.
- **TUF level IDs**: install TUFHelperLite or TUFHelper, and runs of levels downloaded with them are linked to their TUF level.

## For mod developers

Other mods use these by name, without referencing TUFReplay.dll:

- `TUFReplay.ReplayApi`: save your mod's state with each run, and get the replay's clock and recorded keys in game and in rendered videos.
- `TUFReplay.ReplayRuntime.IsPlaybackActive`: whether a TUFReplay replay is running.
- `TUFReplay.NativeInputPause`: pause TUFReplay's input recording while your UI takes the keyboard.

See [docs/replay-api](docs/replay-api/README.md). Renderers read the bundle described in [docs/replay-render-bundle.md](docs/replay-render-bundle.md).

## Development

You need the .NET SDK and [Bun](https://bun.sh). Packaging also needs zig (`brew install zig`) for the Windows native DLL, the macOS helper needs Xcode, and `unity-ui` needs Unity 6000.3.10f1.

```bash
cp .env.example .env    # adjust paths if ADOFAI is not in the macOS Steam default location
bun install
./scripts/run.sh build  # build, test and install into ADOFAI
```

| `./scripts/run.sh` | What it does |
| --- | --- |
| `build` | Build, test and install the mod into ADOFAI |
| `mod-check` | Build and test without installing |
| `package` | Make `build/TUFReplay.zip` and `build/TUFReplay.update.json` for a release |
| `web-dev` | Run the web companion |
| `web-check` | Web tests, type check, Biome and build |
| `ipc-generate` | Regenerate the C# and TS code after editing `ipc/main.tsp` |
| `mod-format`, `web-format` | Format C# (CSharpier) and web (Biome) |

`./scripts/run.sh help` lists every command.

Run the web companion against the game:

```bash
VITE_WEB_ADOFAI_EMBED_URL=http://127.0.0.1:5173/embed/chart ./scripts/run.sh web-dev
```

- The game accepts the web companion only from the deployed sites and from `localhost` or `127.0.0.1` on ports 5173, 5174 and 4173.
- Add `?mock=1` to the URL to use mock data without the game.

### Repository layout

- `TUFReplay/`: the mod, grouped by feature (`Activity`, `Replay`, `Recording`, `Microphone`, `Webcam`, `Calibration`, `ModApi`).
- `TUFReplay.Bootstrap/`, `TUFReplay.UpdateEngine/`: the launcher and the self-updater.
- `TUFReplay.Tests/`, `TUFReplay.UpdateTests/`: C# tests.
- `TUFReplay.Native.Windows/`: the Windows native DLL (keyboard hook, camera relay). See [its README](TUFReplay.Native.Windows/README.md).
- `TUFReplay.MicrophoneCapture.Mac/`: the macOS microphone and camera helper app.
- `TUFReplay.Unity/`: Unity project for the in-game UI bundles.
- `ipc/main.tsp`: the typed contract between the mod and the web companion.
- `web/`: the web companion (Bun, React, Vite).
- `scripts/`: `run.sh` and its tasks.
- `deploy/`: home server deployment.

### Release and deployment

- Run `package` and attach both files to a GitHub release. Mark betas as prereleases.
- Auto-submission builds (`TUFREPLAY_BUILD_FLAVOR=auto-submission` with `TUFREPLAY_BUILD_VERSION`) go to the home server feed instead of GitHub Releases.
- GitHub Actions deploys the web companion. See [deploy/README.md](deploy/README.md).

| Branch | Website |
| --- | --- |
| `main` | https://tufreplay.impl1113.dev |
| `dev` | https://tufreplay-dev.impl1113.dev |
| `feat/auto-submission` | https://tufreplay-auto.impl1113.dev |

Production hosting must rewrite `/api/tuf/*` to `https://api.tuforums.com/*`. Vite does this during development.

## Special Thanks

- **Teo** — Gave his idea to me and started this project.
- **Potato** - Developed CReplay, a mod that was heavily referenced in this project.
