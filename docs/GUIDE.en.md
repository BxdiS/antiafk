# Guide

## Installation

Download `AntiAFK.exe` from [releases](https://github.com/BxdiS/antiafk/releases) and run it. That's it — the app is portable, single file, no installer, no admin rights needed.

Important detail: logs and cycle progress live only in the process memory. The app writes to disk in two situations: on the first Save in the settings window, `AntiAFK.json` is created next to the exe with the current full configuration; and a couple of temporary files appear for a few seconds while an update is applied. It also *reads* one optional file — see [Configuration](#configuration).

## Tray

| Menu item | What it does |
|-----------|-------------|
| Start / Stop | Starts the bot. On the first start (or when no project is saved), a picker asks which project to use — Majestic RP or Russia Online. A balloon tip confirms the choice and suggests saving it in Settings. Cycle state persists until you restart the app |
| Update | Shows when an update has been downloaded. The app replaces itself with the new version and restarts |
| Settings | Default project (Majestic RP / Russia Online), language (RU/EN) and launcher path. Take effect immediately and trigger a save: on first Save, `AntiAFK.json` is created next to the exe with the current full configuration. A balloon tip on screen tells you if it was created |
| Open log | Current session log — a window in memory, not a file on disk |
| Exit | Close completely |

### Updates

Checks happen on startup and then every few hours (default 6, configurable in the config). Pulls the latest published release from GitHub — drafts and pre-releases are ignored.

Download happens in the background; the tray icon turns blue when there's something to update. Clicking "Update" makes the exe replace itself with the new version and restart; temp files are cleaned up immediately after.

Before any of that the downloaded file is checked against `AntiAFK.exe.sha256` from the same release. If the checksum doesn't match, or the release has no checksum file at all, the update is discarded — the app overwrites itself and runs the result, so "couldn't verify" is not treated as "verified" here.

## Picking a spawn point

Once a character is confirmed the game shows the map with a row of round icons along the bottom — the spawn points. Everyone has a different set, and the row is centred, so gaining one icon shifts every other one: "third from the left" means a different place for two different players.

So the bot doesn't click a position, it looks at what the icons are:

1. It captures the strip along the bottom and fits an icon count to it — the one where every slot is filled and the positions just past both ends are empty.
2. Each icon is compared against the reference glyphs built into the app.
3. It clicks the first entry of `spawn.priority` the player actually has.

What the app can recognise:

| id | What it is | Icon |
|----|------------|------|
| `exit_point` | Where you logged out | map with a pin |
| `starting_spawn` | The spawn a new character starts with | aeroplane |
| `personal_house` | Your own house | house with a roof |
| `personal_apartment` | Your own flat | tower block |
| `family_house` | Family house | two people |
| `family_mansion` | Family mansion | two people — the very same icon |
| `family_office` | Family office | office block |

Default order: `personal_house` → `personal_apartment` → `family_house` → `family_office` → `family_mansion` → `exit_point` → `starting_spawn`.

**About the house and the mansion.** The game draws them with the same icon — not a similar one, the same one: their pixel masks differ by 3 pixels out of 1936, which is the anti-aliasing of a half-pixel offset. No amount of image recognition will separate them.

They are separated by counting instead. A mansion cannot be owned without the house, and it sits before it on the bar, so a single two-person icon is always the house and two of them are the mansion followed by the house. That is why names sharing a glyph line up with the end of the list rather than the start.

If none of them are on the bar, the leftmost icon is used. If the bar couldn't be read at all, it falls back to the old fixed click at `(1053, 964)`.

The log shows everything it saw:

```
Auto-login: spawn bar has 5 icon(s) on row y=964 (fit 1.00): [1] exit_point (pin 0.20) @768  [2] starting_spawn (airplane 0.03) @864  [3] family_house (people 0.00) @960  [4] family_mansion (people 0.02) @1056  [5] family_office (office 0.03) @1152
Auto-login: selecting spawn point "exit_point" at (768, 964), icon 1 of 5.
```

An icon with no reference in this build is logged as `unknown`, together with its signature and a picture of the glyph — enough to add it to `SpawnIconCatalog` as one more line. The tool prints that line for you:

```bash
dotnet run --project tools/GenerateSpawnIcons -- screenshot.png
```

The match thresholds come from measurement rather than taste. The test: each of the fourteen icons was identified by a catalog with its own reference removed. All fourteen came out right — worst distance to their own glyph 0.22, closest different glyph 0.36, and the 0.30 threshold sits between the two. So an icon this build has no reference for comes back `unknown` rather than as the nearest building: failing to recognise one costs a spawn point, recognising it wrongly spawns the player somewhere they didn't ask for.

## Configuration

Most people never need this. Language and launcher path are in tray → **Settings**. They take effect immediately, and pressing Save triggers this flow: if `AntiAFK.json` does not exist yet, the app creates it next to the exe with the current full configuration — all fields, with current values. A balloon tip tells you. After that, the file stays as is; pressing Save again does nothing so your hand-edits are never overwritten.

Put `AntiAFK.json` next to `AntiAFK.exe` before the app starts, and it's read on startup. Anything in it that makes no sense is logged as a warning and falls back to a built-in value. The config is optional; leave it out and the app runs with defaults. Applying an update moves a new exe over the old one and leaves the rest of the folder alone, so `AntiAFK.json` survives auto-updates.

[config.example.json](config.example.json) lists every field with its default. Copy the whole thing or write only the part you care about — every section is optional and anything left out keeps its built-in value. This is a complete, valid config:

```json
{
  "spawn": { "priority": ["family_office", "personal_house"] }
}
```

Comments and trailing commas are accepted, even though strict JSON forbids both.

| Field | What it does |
|-------|--------------|
| `language` | `ru` or `en` |
| `project` | `majestic` or `russia_online`. Empty or missing means the picker asks on every start |
| `launcherPath` | Empty means auto-detect the launcher in the standard Windows paths |
| `spawn.priority` | Spawn points in the order you want them, best first, using the ids from the table above. An empty list always takes the leftmost icon on the bar |
| `timings.*` | How long the bot waits for the game between steps, in seconds. A `{min, max}` pair is drawn at random inside the range |
| `update.*` | Whether to check for updates and how often |

Screen coordinates aren't configurable, and neither are the delays inside a single click. Those live in `InputService` for a reason: they're what keeps a click where it was aimed, and shortening them is how clicks start landing on the neighbouring button.

The file is read once on startup. Any change you make by hand takes effect the next time you restart the app. Changes in the settings window take effect immediately for the running app and trigger a save of the full config if one does not exist yet.

Startup is where the log earns its keep: which file was read or that there wasn't one, then a line per problem — a key that doesn't exist, a spawn id this build can't recognise, a range with min above max. None of it stops the app starting. It falls back to the built-in value and says which and why.

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
  AntiAfk.Infrastructure/ — WinAPI, screen work, reading the config file, logs (in memory)
  AntiAfk.App/            — tray, settings window (WinForms), updates
```

## Releases

CI fires on push to a `v*` tag:

```bash
git push origin main
git tag v1.0.1
git push origin v1.0.1
```

Then GitHub Actions builds a self-contained single-file `.exe` for win-x64 (the .NET runtime is bundled, nothing needs to be installed), calculates SHA-256, and publishes the release immediately — not as a draft, with auto-generated release notes.

Automatic publishing is intentional. The old Velopack scheme left releases as draft, and it was easy to forget the manual "Publish" click — that's exactly why auto-updates "didn't work."

Two files go into each release:

| File | What it's for |
|------|---------------|
| `AntiAFK.exe` | What users download |
| `AntiAFK.exe.sha256` | Checksum, if someone wants to verify integrity |

Auto-generated release notes just list commits. If you want something more human-readable, edit the release description after publishing.

## Code signing

`AntiAFK.exe` isn't signed, so SmartScreen complains on the first run. That's normal for any unfamiliar binary and has nothing to do with how it's built.

The plan is to get a free signature from [SignPath Foundation](https://signpath.org/), and an application is in. Once approved, a step in CI will sign `publish/AntiAFK.exe` directly after `dotnet publish` — without `vpk --signTemplate`, which was leftover from the old Velopack scheme.

An OSI-compatible license is the requirement people usually hear about, and [GPL-3.0](../LICENSE) clears it, but it is nowhere near the whole list. The certificate is issued in SignPath Foundation's own name, so what they are really weighing is whether they want their name on this binary. Their [terms](https://signpath.org/terms) run to project reputation, verifiable builds, declared team roles, a published code signing policy, and a clause about malware and potentially unwanted programs that a tool automating game input has to be looked at under honestly.

The policy they require lives in [Code signing policy](../README.en.md#code-signing-policy) in the README — roles, and what the update check sends. When behaviour there changes, that section changes with it, in both languages.

## Troubleshooting

| Symptom | What to check |
|---------|---------------|
| Game not found | Need the `GTA5.exe` process or Majestic RP client window with version in the title |
| Clicks land in the wrong place | The log starts with `Display ...` lines showing each monitor's scale. Anything other than 100% shifts the coordinates — run the game on a 100% display |
| Spawned in the wrong place | The `spawn bar has N icon(s)` log line shows what the bot saw and what it took each icon to be. `unknown` means this build has no reference for it — the signature and glyph are logged next to it, which is what adding one takes |
| Engine stopped on its own | After five crashes in a row it stops restarting instead of looping the same failure. The cause is in the log console; Start tries again |
| Updates aren't coming | Latest release must be published (not draft, not pre-release) and contain `AntiAFK.exe`. Without an `AntiAFK.exe.sha256` next to it the update is discarded as unverifiable |
| Workflow didn't run | Tag must start with `v` |
| Settings from the window didn't stick | If you changed language or launcher path but they were back to defaults after restart, the save probably didn't happen. Check that you pressed Save in the settings window — a balloon tip should have appeared. If the save failed, the log console will say why. Put the values in `AntiAFK.json` by hand if you need them to stick without the window |
| Something in `AntiAFK.json` does nothing | The first lines of the log name every key that doesn't exist and every value that had to be corrected. A misspelled key reads as a key that isn't there, which is exactly what that warning is for |
