<div align="center">

# AntiAFK

**Anti-AFK bot for Majestic RP (GTA 5 RP).** Keeps your character active so the server doesn't kick you for being idle.
One `.exe`, lives in the tray, fully open source.

[![Release](https://img.shields.io/github/v/release/BxdiS/antiafk?style=flat-square&color=2ea44f)](https://github.com/BxdiS/antiafk/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/BxdiS/antiafk/total?style=flat-square)](https://github.com/BxdiS/antiafk/releases)
[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011%20x64-0078d4?style=flat-square&logo=windows&logoColor=white)](https://github.com/BxdiS/antiafk/releases/latest)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512bd4?style=flat-square&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License GPL v3](https://img.shields.io/badge/license-GPL--3.0-blue?style=flat-square)](LICENSE)
[![Stars](https://img.shields.io/github/stars/BxdiS/antiafk?style=flat-square&color=f5c518)](https://github.com/BxdiS/antiafk/stargazers)

[**Download**](https://github.com/BxdiS/antiafk/releases/latest) ·
[Guide](docs/GUIDE.en.md) ·
[Roadmap](ROADMAP.md) ·
[Bug or idea](https://github.com/BxdiS/antiafk/issues) ·
[**Русская версия →**](README.md)

</div>

---

## Why it exists

Majestic RP throws an AFK check at you, and failing it means getting dropped from the game. AntiAFK sits in the tray, spots the game window on its own and clears the check for you — farming, an overnight session, or just stepping away no longer costs you your session.

Built for **Majestic RP**, which it supports today. **GTA5RP** is coming in 2.0.0 — see the [roadmap](ROADMAP.md).

## What you get

- **A single file.** No installer, no admin rights, nothing written to the registry or startup. Delete the `.exe` and it's gone without a trace.
- **Starts itself.** Game not running? It waits. Game shows up? It goes to work.
- **Auto-updates.** New versions install from the tray in one click.
- **Live log.** You can see exactly what it's doing right now.
- **Two languages.** English and Russian UI.
- **No telemetry.** Nothing is sent anywhere, and the source is fully open.

## Download

Grab the latest build from [Releases](https://github.com/BxdiS/antiafk/releases/latest) — you only need `AntiAFK.exe`.

The binary isn't signed yet, so Windows SmartScreen may complain the first time you run it: **More info → Run anyway**. A [SignPath Foundation](https://signpath.org/) application is in progress; once it goes through, releases will be signed.

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

Display scaling needs to be 100%. Click coordinates are real pixels, so anything else shifts them.

## How it works

C# on .NET 8, WinForms for the UI, and every line of it is in this repository. It doesn't read or inject anything into the game's memory: it finds the window, brings it to the front, and sends ordinary clicks and key presses through the Windows API — the same events your mouse and keyboard produce. Actions are spaced out with delays so the input looks human rather than like a burst of instant events.

You can build it yourself — [docs/GUIDE.en.md](docs/GUIDE.en.md) covers that, plus configuration, auto-updates and the code signing situation.

## FAQ

**Is this a cheat?** No. It doesn't touch game files or memory and gives you no abilities you didn't have — it just presses the buttons for you.

**Will I get banned?** Nobody can promise otherwise: server rules are the server's call, and using this is your decision. There's no detection evasion here and none is planned.

**My antivirus / SmartScreen complains.** The binary isn't signed yet — see [Download](#download). The source is open, so you can always build your own.

**Does it work on other servers?** Not yet. The coordinates and logic are written around Majestic RP; GTA5RP and per-server profiles are [on the roadmap](ROADMAP.md).

## What's next

[ROADMAP.md](ROADMAP.md) — planned work, and why some of it counts as a major version bump.

## License

[GPL v3](LICENSE), © 2026 [BxdiS](https://github.com/BxdiS).

---

<div align="center">

### If it saved you a session, leave a star ⭐

Stars are what push the project up in GitHub search, so the people who need it can actually find it.
It costs you a second, and it's the only signal I get that this is useful to anyone.

<a href="https://github.com/BxdiS/antiafk/stargazers">
  <img src="https://img.shields.io/github/stars/BxdiS/antiafk?style=social" alt="GitHub Stars">
</a>

</div>
