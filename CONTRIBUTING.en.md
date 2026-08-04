[**Русская версия →**](CONTRIBUTING.md)

# Contributing

AntiAFK is a Windows tray app that keeps a Majestic RP character active. Nearly everything it does is timing against a real game window — real pixels, a real cursor — so the sections here about testing and danger zones carry more weight than they would in an ordinary project. A change that looks obviously correct in the diff has broken this app before.

## Getting it building

Windows, .NET 8 SDK. WinForms, `net8.0-windows`, so it does not build anywhere else.

```bash
dotnet build AntiAfk.slnx
```

There is no test suite, and there is not going to be one. Every path worth testing needs a real game window, real screen pixels and real cursor timing: mock that away and the test proves nothing, keep it real and the suite only runs with GTA5 open. So the app is tested by running it — which makes your PR description the only evidence anyone has that it was. Say what you ran and what you saw.

## Danger zones

Read this section even if you skip everything else.

Each of these has already broken the app at least once, through a change that read as reasonable at review time. Leave them alone in a PR that is about something else. If your change genuinely needs one of them, say so and describe what you observed — not what should happen in theory.

**The input path.** `InputService.ClickScreen` is: set `Cursor.Position`, sleep, button down, sleep, button up, sleep. Those delays are the mechanism, not padding. The game acts on the release, and a click sent before the cursor has arrived — or before the UI has registered the hover — lands on a neighbouring button. Don't shorten them, don't remove them, and don't put anything between the steps. A `Debug.WriteLine` there is what made Debug builds misclick while Release builds worked: `OutputDebugString` raises an exception that an attached debugger services by suspending the process, so the gap between "cursor is on target" and "button goes down" became unbounded. Trace before or after the click, through `IAppLogger`, never between.

**DPI awareness.** Stays at the WinForms default, SystemAware. PerMonitorV2 was set once on a hypothesis, changes what every screen coordinate in the app means, and was reverted. The reasoning is in a comment in `AntiAfk.App.csproj`. Don't set it in `app.manifest` either — WinForms manages it, and declaring it in both places trips WFAC010.

**Native AOT.** Not usable here. `RichTextBox` registers an `IRichEditOleCallback` over COM on handle creation, and the source-generated interop path Native AOT requires throws there.

**The single-file publish settings.** `GitHubUpdateService` applies an update by moving the downloaded `AntiAFK.exe` over the running one, so the release asset has to stay exactly one file. A plain self-contained publish emits about 247. The release workflow fails the build if anything lands beside the exe — that is the alarm, not the fix.

**`<Version>` in `AntiAfk.App.csproj`.** A placeholder. The release workflow overwrites it from the tag. Editing it by hand changes nothing and misleads whoever reads it next.

**Coordinates in `GameConstants`.** Every value is 1920×1080 and passes through `CoordinateScaler`. Don't add raw literals for other resolutions. The character-select pixel shares its pink with the in-game HUD pixel, so the order those two checks run in is load-bearing.

## Branches and pull requests

No direct commits to `main`. Everything goes through a branch and a PR, including one-line changes.

Branch names are `feat/`, `fix/` or `docs/` plus something descriptive — `feat/auto-login`, `fix/log-console-crash`. Open the PR as soon as you push; don't leave the branch sitting there.

### Titles

Conventional Commits, for commit subjects and PR titles alike:

```
type(scope): description
```

Types are `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`. A breaking change takes a `!` before the colon and a `BREAKING CHANGE:` footer — that is what marks a major version bump in ROADMAP.md.

The description is imperative mood, starts lowercase, has no trailing period, and the whole title stays under 72 characters with the prefix counted. Identifiers keep their real casing:

```
fix: remove the Debug.WriteLine calls interleaved with the click
```

The title carries further than it looks. PRs are squash-merged, so it becomes the commit subject on `main`, and the release workflow builds release notes from PR titles — it is the line users read on the release page. It has to make sense to someone who never saw the diff.

### Description

At most five bullets at the top: what changed and why it matters. No preamble, no restating the title, no list of touched files.

Underneath, if the change has any: the root cause, an approach you tried and rejected, an edge case that turned out to matter. A one-line docs change gets a one-line description. Don't manufacture architecture decisions that aren't there.

### How to write it

Rules about types and character counts can all be satisfied by a description that says nothing. What actually makes these useful:

**Old behaviour in past tense, new behaviour in present.** "The launcher login click went out as a bare screen click… WindowService now locates the launcher window." The reader gets the delta without opening the diff.

**Say what you ruled out, not only what you did.** One commit here eliminates DPI awareness, runtime version and a second running instance — each with the evidence that eliminated it — before naming the cause. That is often the most useful part, and it is what stops the next person re-checking the same three things.

**Numbers, not adjectives.** "Three full cycles in eight seconds." "Four GDI handles for the life of the process." "About 247 loose files." Never "significantly improves reliability".

**Say what you did not verify.** If you reasoned a fix through but never watched it work, write that down. Six commits in a row once claimed to fix the same misclick — each written against the previous one's symptom, none confirmed — and the branch ended in a revert of all six. Asserting an unverified fix is exactly how that happened.

**List the consequences.** A method that lost its branching, a call site that disappeared, a constant deleted rather than left unused. Reviewers cannot see those from the diff.

**Name the method or the field, not the abstraction.** `WaitForGameLoadAsync`, `_startupRecoveryPending`, `MOUSEEVENTF_MOVE_NOCOALESCE` — not "the retry logic".

**Say where nothing changed.** If a reported issue turned out not to need a fix, record that with the reason.

### Before you open it

The code compiles without warnings. Nothing obviously crashes. Style matches whatever the surrounding file already does. You ran the app and confirmed the change does what the PR says — there is nothing automated to lean on.

## Documentation

When behaviour changes, the docs change in the same PR:

- **docs/GUIDE.md** and **docs/GUIDE.en.md** — setup, tray menu, troubleshooting
- **ROADMAP.md** — feature additions or a change in direction
- **README.md** and **README.en.md** — what the app does or how to get it

Bilingual pairs move together, in the same commit. Updating one half and catching the other later is the most frequent miss in this repo.

Markdown here is written in a human voice: no mechanical "Term — description" bullet lists, no absolute promises like "never crash", bold and emoji sparingly if at all.

`docs/config.example.json` documents what configuration fields exist and what they do. Add or remove a field in the code and it changes too.

## Language

Everything written into the repository is English — commit messages, PR titles and descriptions, code comments, log strings. The exceptions are the Russian halves of the three bilingual pairs: `README.md`, `docs/GUIDE.md` and `CONTRIBUTING.md`.

Issues and PR conversation can be in Russian or English, whichever you are comfortable with. That has no bearing on what gets committed.

## Releases

Maintainers only. Releases are cut by tag, and only by tag:

```bash
git tag v1.2.3
git push origin v1.2.3
```

`.github/workflows/release.yml` does the rest — version from the tag name, self-contained single-file publish, SLSA build provenance, SHA256 checksum, and release notes assembled from the PR titles in the range. Nothing about a release is put together by hand. What earns a patch, minor or major bump is in ROADMAP.md.
