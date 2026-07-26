[**Русская версия →**](README.md)

# AntiAFK

Keeps your character active so the server doesn't kick you for being idle.

## Download

Grab the latest build from [Releases](https://github.com/BxdiS/antiafk/releases) — you only need `AntiAFK.exe`.

Single file, no installer, no admin rights. It doesn't write itself into the system anywhere: delete the exe and it's gone without a trace.

The binary isn't signed yet, so Windows SmartScreen may complain the first time you run it. A [SignPath Foundation](https://signpath.org/) application is in progress; once it goes through, releases will be signed.

## Getting started

Download it, run it, look for the tray icon. Hit **Start** and walk away.

The icon colour tells you what's going on:

| | |
|--|--|
| 🟢 | running |
| 🟡 | waiting for the game |
| 🔴 | stopped |
| 🔵 | update available |

The tray menu has settings, a live log, version info, an update button when there's something to update, and exit.

## Requirements

Windows 10 or 11, x64. The game needs to be in borderless windowed mode at a 16:9 resolution. Other aspect ratios aren't supported yet — that's [on the roadmap](ROADMAP.md).

## More detail

[docs/GUIDE.md](docs/GUIDE.md) covers building from source, configuration, how auto-updates work, and the code signing situation.

## License

[GPL v3](LICENSE), © 2026 [BxdiS](https://github.com/BxdiS).

Source is fully open. No telemetry, no system modifications.

## What's next

[ROADMAP.md](ROADMAP.md) — planned work, and why some of it counts as a major version bump.
