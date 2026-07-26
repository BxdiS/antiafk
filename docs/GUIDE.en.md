# Guide

## Installation

Download `AntiAFK.exe` from [releases](https://github.com/BxdiS/antiafk/releases) and run it. That's it — the app is portable, single file, no installer, no admin rights needed.

Important detail: settings, logs, and cycle progress live only in the process memory. Every startup is a clean slate, nothing gets written to disk. The only exception is a couple of temporary files for a few seconds when applying an update.

## Tray

| Menu item | What it does |
|-----------|-------------|
| Start / Stop | Starts and pauses the bot. Cycle state persists until you restart the app |
| Update | Shows when an update has been downloaded. The app replaces itself with the new version and restarts |
| Settings | Language (RU/EN) and launcher path. Take effect until the app closes |
| Open log | Current session log — a window in memory, not a file on disk |
| Exit | Close completely |

### Updates

Checks happen on startup and then every few hours (default 6, configurable in the config). Pulls the latest published release from GitHub — drafts and pre-releases are ignored.

Download happens in the background; the tray icon turns blue when there's something to update. Clicking "Update" makes the exe replace itself with the new version and restart; temp files are cleaned up immediately after.

## Configuration

Settings are set via tray → **Settings** and don't persist to disk. The structure of config fields is documented in [config.example.json](config.example.json) — it's a reference, not a file the app reads.

`launcherPath` empty means auto-detect the launcher in standard Windows paths. UI coordinates and timings are hardcoded; you can't change them through config.

## Building from source

```bash
git clone https://github.com/BxdiS/antiafk.git
cd antiafk
dotnet build src/AntiAfk.App/AntiAfk.App.csproj
dotnet run --project src/AntiAfk.App/AntiAfk.App.csproj
```

How the code is organized:

```
src/
  AntiAfk.Core/           — engine, coordinates, state
  AntiAfk.Infrastructure/ — WinAPI, screen work, config and logs (both in memory)
  AntiAfk.App/            — tray, settings window (WinForms), updates
```

## Releases

CI fires on push to a `v*` tag:

```bash
git push origin main
git tag v1.0.1
git push origin v1.0.1
```

Then GitHub Actions builds a Native AOT `.exe` for win-x64 (self-contained binary, no .NET runtime needed), calculates SHA-256, and publishes the release immediately — not as a draft, with auto-generated release notes.

Automatic publishing is intentional. The old Velopack scheme left releases as draft, and it was easy to forget the manual "Publish" click — that's exactly why auto-updates "didn't work."

Two files go into each release:

| File | What it's for |
|------|---------------|
| `AntiAFK.exe` | What users download |
| `AntiAFK.exe.sha256` | Checksum, if someone wants to verify integrity |

Auto-generated release notes just list commits. If you want something more human-readable, edit the release description after publishing.

## Code signing

`AntiAFK.exe` isn't signed, so SmartScreen complains on the first run. That's normal for any unfamiliar binary and has nothing to do with portability or AOT builds.

The plan is to get a free signature from [SignPath](https://signpath.org/). The project is under [GPL-3.0](../LICENSE), and an OSI-compatible license is their main requirement (they don't sign proprietary or non-OSI ones). Once approved, a step in CI will sign `publish/AntiAFK.exe` directly after `dotnet publish` — without `vpk --signTemplate`, which was leftover from the old Velopack scheme.

## Troubleshooting

| Symptom | What to check |
|---------|---------------|
| Game not found | Need the `GTA5.exe` process or Majestic RP client window with version in the title |
| Updates aren't coming | Latest release must be published (not draft, not pre-release) and contain `AntiAFK.exe` |
| Workflow didn't run | Tag must start with `v` |
| Settings disappeared after restart | That's by design — portable version keeps nothing between runs |
