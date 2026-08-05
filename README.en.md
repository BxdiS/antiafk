[**Русская версия →**](README.md)
> [!TIP]
> ⭐ **If the program useful — star the project!**
> Stars help the project get into the search. Thank you!

# AntiAFK

Keeps your character active so the server doesn't kick you for being idle.

Built for **Majestic RP**, which it supports today. **GTA5RP** is coming in 2.0.0 — see the [roadmap](ROADMAP.md).

## Download

Grab the latest build from [Releases](https://github.com/BxdiS/antiafk/releases) — you only need `AntiAFK.exe`.

Single file, no installer, no admin rights. It doesn't write itself into the system anywhere: delete the exe and it's gone without a trace.

The binary isn't signed yet, so Windows SmartScreen may complain the first time you run it. A [SignPath Foundation](https://signpath.org/) application is in progress; once it goes through, releases will be signed. See [Code signing policy](#code-signing-policy) below.

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

## More detail

[docs/GUIDE.en.md](docs/GUIDE.en.md) covers building from source, configuration, how auto-updates work, and the code signing situation.

[CONTRIBUTING.en.md](CONTRIBUTING.en.md) is for sending a change: how branches and PRs work here, and which parts of the code to leave alone.

## Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

Releases are not signed yet. The application is pending, and until it goes through, SmartScreen will keep warning on first run.

### Roles

One maintainer, holding all three roles: authors, reviewers and approvers — [BxdiS](https://github.com/BxdiS).

Everything lands through a pull request rather than a push to `main`, including changes the maintainer wrote. [CONTRIBUTING.en.md](CONTRIBUTING.en.md) covers what that involves.

### Privacy policy

AntiAFK has no analytics, no crash reporting and no account of any kind. It collects nothing about you and sends nothing on your behalf.

It makes one outbound request, to see whether a newer release exists:

- `https://api.github.com/repos/BxdiS/antiafk/releases/latest`, once at startup and every 6 hours after that.
- A plain HTTPS GET with the user agent `antiafk-updater` and nothing else. No identifier, no machine details, nothing about the game or your account.
- GitHub sees the IP address it came from, the same as for any download.
- If there is a newer release, `AntiAFK.exe` and its `.sha256` are downloaded to a temporary folder and the checksum is verified. A file that fails verification is deleted rather than kept.
- Installing it — swapping the executable and restarting — happens only when you click Update in the tray menu.

The tray menu has no switch for it. Blocking `api.github.com` in a firewall stops it: the failure goes to the log and the app carries on.

## License

[GPL v3](LICENSE), © 2026 [BxdiS](https://github.com/BxdiS).

AntiAFK comes with absolutely no warranty. It is free software, and you are welcome to redistribute it under the terms of the GNU General Public License, version 3 or later.

Source is fully open. No telemetry, no system modifications.

## What's next

[ROADMAP.md](ROADMAP.md) — planned work, and why some of it counts as a major version bump.
