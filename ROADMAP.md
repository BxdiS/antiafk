# Roadmap — AntiAFK Development

Development priorities and planned features for upcoming releases.

---

## v1.2.0 — Smart Behavior

**Focus:** Anti-detection and adaptive gameplay

- [ ] **Adaptive clicks** — Detect AFK check timeouts and adjust click frequency
- [ ] **Smart category selection** — Randomize category choices instead of always clicking the same one
- [ ] **Random pauses** — Human-like behavior with unpredictable delays between actions
- [ ] **Play detection** — Disable bot when player takes manual control for extended period
- [ ] **Crash recovery** — Automatically restart game if it freezes/crashes
- [ ] **Healthcheck** — Periodic verification that all systems are functioning

**Release target:** 2-3 months

---

## v1.3.0 — Multi-Platform Support

**Focus:** Broader game compatibility and resolution handling

- [ ] **Multi-resolution support** — Support resolutions beyond 16:9 (4:3, 21:9, ultrawide)
- [ ] **Multi-server support** — Detect and support other GTA RP servers with marketplace
- [ ] **Dark mode** — System theme integration (auto-sync with Windows settings)
- [ ] **Keyboard shortcuts** — Quick enable/disable (Win+A or customizable)
- [ ] **Mute audio** — Option to disable sound notifications

**Release target:** 1-2 months

---

## v1.4.0 — Notifications & Control

**Focus:** Better monitoring and remote management

- [ ] **Telegram bot expansion:**
  - Error notifications
  - Periodic status reports (hourly/daily)
  - Remote commands (start/stop/status via Telegram)
  - Daily activity graph
- [ ] **Discord webhook** — Log events to personal Discord server
- [ ] **Windows notifications** — Native taskbar alerts
- [ ] **Sound alerts** — Audio cues for critical events
- [ ] **Donation reminder** — Coffee ☕ support button in app

**Release target:** 1-2 months

---

## v1.5.0 — Optimization & Stability

**Focus:** Performance and reliability

- [ ] **Memory optimization** — Reduce RAM footprint
- [ ] **Anti-crash system** — Comprehensive exception handling
- [ ] **Auto-recovery** — Restart app if it crashes
- [ ] **Multi-language support** — RU, EN, DE, FR, ES, PT, PL
- [ ] **Configuration backup** — Cloud sync for settings (future-ready)

**Release target:** 2 months

---

## v2.0.0 — Advanced Features

**Focus:** Customization and extensibility

- [ ] **Custom scripts** — Users write their own behavior patterns
- [ ] **REST API** — External apps can control/monitor the bot
- [ ] **Plugin system** — Third-party extensions
- [ ] **Behavior profiles** — Casual / Aggressive / Stealth modes
- [ ] **Advanced analytics** — Statistics dashboard, export to CSV/JSON
- [ ] **Marketplace trends** — Track what sells, category popularity analysis

**Release target:** 4-6 months

---

## v3.0.0 — Ecosystem

**Focus:** Community and ecosystem expansion

- [ ] **Web dashboard** — Manage bot from browser on any device
- [ ] **Mobile app** — Android/iOS companion for notifications
- [ ] **Leaderboard** — Anonymous earnings tracking (opt-in)
- [ ] **Community config sharing** — Share/download behavior profiles
- [ ] **OBS integration** — Auto-pause if streaming detected
- [ ] **Scheduled tasks** — Time-based enable/disable, account rotation

**Release target:** 6-12 months

---

## v4.0.0 — Future Exploration

**Focus:** Long-term, experimental features

- [ ] **Multi-account automation** — Rotate between characters automatically
- [ ] **AI-driven behavior** — Machine learning for adaptive gameplay
- [ ] **Premium tier** — Advanced features subscription (optional)
- [ ] **Marketplace bot** — Auto-list items, price optimization
- [ ] **Cross-platform** — Wine support for Linux (if viable)
- [ ] **VPN/Proxy integration** — Route through VPN for added privacy

**Release target:** 12+ months (highly speculative)

---

## Current Status (v1.1.8)

✅ **Implemented:**
- Core AFK prevention (marketplace clicks, walking, turning)
- Auto-login system (character selection, spawn point)
- Portable single-file executable
- Tray icon with status indicators
- Auto-updates via GitHub releases
- SLSA build provenance (signed binaries)
- System theme support (basic)
- Log console with real-time events
- Settings panel

---

## Legend

- **[ ]** — Not started
- **[/]** — In progress
- **[x]** — Complete

---

## Contributing

Want to help? Check out [GUIDE.md](docs/GUIDE.md) for building from source and contribution guidelines.

---

**Last updated:** 2026-07-26
