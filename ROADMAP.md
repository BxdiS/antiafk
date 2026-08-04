# Roadmap

Where AntiAFK is headed. Nothing here has a date attached — things get done when they get done, and the order shifts if something turns out to matter more.

## How versions work

| Bump | When |
|------|------|
| Patch (`1.1.x`) | Bug fixes, tweaks to timings and coordinates, small polish |
| Minor (`1.x.0`) | New features that fit the app as it is today — one server, one window layout |
| Major (`x.0.0`) | The app stops being the same app: a new game server, or letting outside code drive it |

The major bumps are the interesting ones. Adding a server isn't a feature you bolt on — coordinates, UI detection and the whole click cycle are written around Majestic RP specifically, so GTA5RP means splitting all of that apart into something pluggable. Same story with an API: once other programs can control the bot, its internals become a contract I have to keep. Both earn a `2.0` / `3.0`.

## v1.1.x — Where the work actually is

Almost everything since v1.1.0 has gone here rather than into any of the versions below, and the roadmap should say so instead of implying feature work is underway.

Clicks landing on the wrong button is the recurring one. The fixed delays in `InputService.ClickScreen` are what keeps a click where it was aimed, and they are fragile enough that a single `Debug.WriteLine` between the steps broke Debug builds while Release builds worked. One branch spent six commits fixing the same misclick, each written against the previous one's symptom, and ended in a revert of all six. Anything touching click timing has to prove itself against a running game before it goes in.

The 100% display scaling and 16:9 borderless requirements in the README come from the same place. Coordinates are physical pixels, and the app is honest about what it can't handle yet.

## v1.2.0 — Foundations

Nothing further down works properly until these do.

- [ ] Persist settings. `ConfigService` holds everything in memory, so language, launcher path and every timing reset on restart — and because an update replaces the running exe, each auto-update wipes them too. An `AntiAFK.json` next to the executable, in the shape `docs/config.example.json` already documents.
- [ ] Make a click verifiable: tell from the log whether it landed where it was aimed, instead of inferring it from what the game did next
- [ ] Spot a frozen or crashed game and bring it back up
- [ ] Notice when you've taken over manually and step aside until you're done
- [ ] Periodic self-check so a half-broken state gets noticed early
- [ ] Catch exceptions on the paths that currently take the whole app down
- [ ] Trim memory use — it's heavier than it needs to be for what it does
- [ ] Harden the update path. Moving a downloaded exe over the running one is the only thing here that can break every install at once, and nothing checks it beyond the release workflow refusing to publish more than one file.

Restarting after a crash rather than dying quietly in the tray was on this list and shipped in v1.1.x: five attempts with exponential backoff, then it stops and says why.

## v1.3.0 — Behaviour

Right now the bot clicks the same thing at the same pace forever. That's fine until someone looks at it.

This waits for Foundations on purpose. The delays in the click path are load-bearing, not padding, so randomising timings on top of them makes every later misclick ambiguous — was that the game, or the randomiser? Untangling exactly that ambiguity is what cost six commits and a revert.

- [ ] Watch how long AFK checks actually take and adapt the click rate instead of using fixed delays
- [ ] Pick categories at random rather than hammering one
- [ ] Uneven pauses, so the rhythm doesn't read as a script

## v1.4.0 — Telling you things

There is no Telegram bot today. Nothing in the app touches the network except the update check, and the only notification is a balloon tip from the tray icon. So this is a build rather than an extension: an outbound channel, a bot token that needs somewhere to live (which is why it waits on the config file in v1.2.0), and start/stop from chat means accepting remote control of a process on someone's machine.

- [ ] Telegram: errors, status on request, start/stop from chat, a daily activity summary
- [ ] Discord webhook for people who live there instead
- [ ] Native Windows notifications
- [ ] Optional sounds, for when the window isn't visible

The "buy me a coffee" link that used to sit in this section depends on none of it and goes out with whatever patch comes next.

## v1.5.0 — Fitting more setups

- [ ] Resolutions beyond 16:9 — 4:3, 21:9, ultrawide
- [ ] Follow the Windows light/dark setting
- [ ] Global hotkeys for start/stop, rebindable
- [ ] More interface languages: DE, FR, ES, PT, PL alongside RU and EN

Resolutions is the large one, and it is really the first half of v2.0.0. `CoordinateScaler` already scales each axis independently; what stands in the way is the twenty-odd hardcoded pixel positions and probe colours in `GameConstants`, which at 21:9 don't stretch so much as move. Getting those out of code and into data is the same job as pulling per-server layout into profiles, so it gets done once and both versions use it.

## v2.0.0 — GTA5RP

The big one. Everything server-specific — coordinates, window titles, the marketplace flow — gets pulled out of the core and into per-server profiles, so a third server later is a config file rather than a rewrite. Comes with a server picker in settings; Majestic RP stays the default.

Builds straight on the coordinates-as-data work from v1.5.0 and the settings file from v1.2.0. A profile has to be something you can ship, edit and select, which means both of those have to exist first.

## v3.0.0 — Opening it up

- [ ] Let users describe their own click patterns instead of shipping mine
- [ ] A local API so other tools can start, stop and read the bot's state

Ideas, not commitments. If nobody wants them, they won't happen.
