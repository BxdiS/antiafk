# Roadmap

Where AntiAFK is headed. Nothing here has a date attached — things get done when they get done, and the order shifts if something turns out to matter more.

## How versions work

| Bump | When |
|------|------|
| Patch (`1.1.x`) | Bug fixes, tweaks to timings and coordinates, small polish |
| Minor (`1.x.0`) | New features that fit the app as it is today — one server, one window layout |
| Major (`x.0.0`) | The app stops being the same app: a new game server, or letting outside code drive it |

The major bumps are the interesting ones. Adding a server isn't a feature you bolt on — coordinates, UI detection and the whole click cycle are written around Majestic RP specifically, so GTA5RP means splitting all of that apart into something pluggable. Same story with an API: once other programs can control the bot, its internals become a contract I have to keep. Both earn a `2.0` / `3.0`.

## v1.2.0 — Behaviour

Right now the bot clicks the same thing at the same pace forever. That's fine until someone looks at it.

- [ ] Watch how long AFK checks actually take and adapt the click rate instead of using fixed delays
- [ ] Pick categories at random rather than hammering one
- [ ] Uneven pauses, so the rhythm doesn't read as a script
- [ ] Notice when you've taken over manually and step aside until you're done
- [ ] Spot a frozen or crashed game and bring it back up

## v1.3.0 — Not falling over

- [ ] Trim memory use — it's heavier than it needs to be for what it does
- [ ] Come back on its own after a crash instead of silently dying in the tray
- [ ] Catch exceptions on the paths that currently take the whole app down
- [ ] Periodic self-check so a half-broken state gets noticed early

## v1.4.0 — Telling you things

The Telegram bot exists but barely does anything yet.

- [ ] Telegram: errors, status on request, start/stop from chat, a daily activity summary
- [ ] Discord webhook for people who live there instead
- [ ] Native Windows notifications
- [ ] Optional sounds, for when the window isn't visible
- [ ] A small "buy me a coffee" link somewhere unobtrusive

## v1.5.0 — Fitting more setups

- [ ] Resolutions beyond 16:9 — 4:3, 21:9, ultrawide
- [ ] Follow the Windows light/dark setting
- [ ] Global hotkeys for start/stop, rebindable
- [ ] More interface languages: DE, FR, ES, PT, PL alongside RU and EN

## v2.0.0 — GTA5RP

The big one. Everything server-specific — coordinates, window titles, the marketplace flow — gets pulled out of the core and into per-server profiles, so a third server later is a config file rather than a rewrite. Comes with a server picker in settings; Majestic RP stays the default.

## v3.0.0 — Opening it up

- [ ] Let users describe their own click patterns instead of shipping mine
- [ ] A local API so other tools can start, stop and read the bot's state

Ideas, not commitments. If nobody wants them, they won't happen.
