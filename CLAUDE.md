# Notes for Claude

## Language

Everything written into the repository is English — commit messages, PR titles and bodies, code comments, log strings, docs. The two exceptions are `README.md` and `docs/GUIDE.md`, which are the Russian halves of bilingual pairs with `README.en.md` and `docs/GUIDE.en.md`.

Conversation can be in whatever language suits. That has no bearing on what lands in the repo.

## Branches and PRs

Don't commit to `main` directly. Every change goes through a branch and a PR, even small ones.

Branch naming: `feat/` for features, `fix/` for fixes, `docs/` for documentation. Keep it descriptive — `feat/auto-login`, `fix/log-console-crash`.

Open the PR right after pushing, don't leave the branch sitting there:

```bash
gh pr create --title "fix: raise the target window before every screen click" --body "..." --head your-branch --base main
```

**Never add tool attribution lines** like "🤖 Generated with Claude Code" or "Co-authored-by: Claude Fable 5" or similar metadata. Focus on the technical content — what the code does, why it matters, and any relevant context. No AI or tool branding in commits or PR descriptions.

Once it's merged, clean up:

```bash
git branch -D branch-name
git push origin --delete branch-name
```

Only `main` should survive long-term.

## Commit and PR style

Conventional Commits for both commit and PR titles: `type(scope): description`. Types are `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`. A breaking change takes a `!` before the colon and a `BREAKING CHANGE:` footer — that is what marks a major bump in ROADMAP.md.

The description is imperative mood, starts lowercase, and has no ending period. The whole title stays under 72 characters, prefix included. Identifiers keep their real casing: `fix: remove the Debug.WriteLine calls interleaved with the click`.

PRs are squash-merged, and `.github/workflows/release.yml` builds the release notes from PR titles. So the PR title is both the commit subject on `main` and the line users read on the release page — same rules as a commit title, and it has to make sense to someone who never saw the diff.

PR body: at most five bullets up top, covering what changed and why it matters. No preamble, no restating the title, no "here is your PR description". The first line is the change itself. A list of touched files isn't a description.

Detail goes below the bullets, under its own heading, when the change has any — a root cause, an approach that was tried and rejected, an edge case that turned out to matter. A one-line docs change gets a one-line body. Don't manufacture architecture decisions that aren't there.

## Commit and PR voice

Format rules alone produce correct, empty messages. Write the way the existing history writes. What it does consistently:

**Old behaviour in past tense, new behaviour in present.** "The launcher login click went out as a bare screen click… WindowService now locates the launcher window." The reader gets the delta without opening the diff.

**Say what was ruled out, not only what was done.** `b919982` eliminates DPI awareness, runtime version and a second running instance — each with the evidence that eliminated it — before naming the cause. That is usually the most useful part of the message, and it is the part that stops the next person re-checking the same three things.

**Numbers, not adjectives.** "Three full cycles in eight seconds." "Four GDI handles for the life of the process." "~247 loose files." Never "significantly improves reliability".

**Say what you did not verify.** If a change was reasoned about but never observed working, the message says so: "documented correctness rather than a demonstrated fix", "the centre of the screen is the usual place for a click-to-continue menu, but it is a guess". Six commits in a row once claimed to fix the same misclick, none of them confirmed, and the branch ended in a revert of all six. Asserting an unverified fix is precisely how that happened.

**List the consequences when a change has knock-on effects.** `3239d9d` spells them out — a method lost its branching, a call site disappeared, a constant was deleted rather than left unused. Those are the things a reviewer cannot see from the diff alone.

**Name the method or the field, not the abstraction.** `WaitForGameLoadAsync`, `_startupRecoveryPending`, `MOUSEEVENTF_MOVE_NOCOALESCE` — not "the retry logic".

**Say where nothing changed.** When a reported issue turned out not to need a fix, write that down with the reason. `c675d65` ends with a "No code change" block for three of eleven findings.

## Before opening a PR

Code compiles without warnings. No obvious crashes. Style matches whatever the surrounding file is already doing. Run the app and confirm the change does what the PR claims — there is nothing automated to lean on here, see below.

## Building and releasing

Windows only, .NET 8 (`net8.0-windows`, WinForms).

```bash
dotnet build AntiAfk.slnx
```

There is no test project, and there should not be one. Everything this app does depends on a real game window, real screen pixels and real cursor timing; a test suite would either mock all of that away and prove nothing, or need the game running. It is tested by running it. That makes the testing note in the PR body the only evidence anyone has — say what you ran and what you saw, not what should happen.

Releases are cut by tag, and only by tag:

```bash
git tag v1.2.3
git push origin v1.2.3
```

`.github/workflows/release.yml` does the rest: version from the tag name, self-contained single-file publish, SLSA build provenance, SHA256 checksum, and release notes assembled from the PR titles in the range. Nothing about a release is assembled by hand. What earns a patch, minor or major bump is in ROADMAP.md.

## Danger zones

Each of these has already broken the app once, in a way that looked reasonable in the diff. Leave them alone in an unrelated PR. If a change genuinely needs to touch one, say so in the body and describe what you observed — not what should happen in theory.

**The input path.** `InputService.ClickScreen` is: set `Cursor.Position`, sleep, button down, sleep, button up, sleep. The delays are the mechanism, not padding. The game acts on the release, and a click sent before the cursor has arrived, or before the UI has registered the hover, lands on a neighbouring button. Don't shorten them, don't remove them, and don't put anything between the steps. A `Debug.WriteLine` there is what made Debug builds misclick while Release builds worked — `OutputDebugString` raises an exception the debugger services by suspending the process, so the gap between "cursor is on target" and "button goes down" became unbounded. Trace before or after the click, through `IAppLogger`, never between.

**DPI awareness.** Stays at the WinForms default, SystemAware. PerMonitorV2 was set once on a hypothesis, changes what every screen coordinate in the app means, and was reverted. The reasoning sits in a comment in `AntiAfk.App.csproj`. Don't set it in `app.manifest` either — WinForms manages it, and declaring it twice trips WFAC010.

**Native AOT.** Not usable. `RichTextBox` registers an `IRichEditOleCallback` over COM on handle creation, and the source-generated interop path Native AOT requires throws there.

**The single-file publish settings.** `GitHubUpdateService` applies an update by moving the downloaded `AntiAFK.exe` over the running one, so the release asset has to stay exactly one file. A plain self-contained publish emits ~247. `release.yml` fails the build if anything lands beside the exe, which is the check, not the fix.

**`<Version>` in `AntiAfk.App.csproj`.** A placeholder. The release workflow overwrites it from the tag. Editing it by hand changes nothing and misleads the next reader.

**Coordinates in `GameConstants`.** Every value is 1920×1080 and passes through `CoordinateScaler`. Don't add raw literals for other resolutions. The character-select pixel shares its pink with the in-game HUD pixel, so the order those two checks run in is load-bearing.

## Documentation

When behavior changes, update the relevant docs:
- **docs/GUIDE.md** and **docs/GUIDE.en.md** — user-facing setup, tray menu, troubleshooting
- **ROADMAP.md** — for feature additions or major changes in direction
- **README.md** and **README.en.md** — if something about what the app does or how to get it changes

Bilingual pairs move together, in the same commit. Updating one half and remembering the other later is the most frequent miss in this repo.

Markdown here is written in a human voice: no mechanical "Term — description" bullet lists, no absolute promises like "never crash", and bold and emoji used sparingly if at all.

This file (CLAUDE.md) itself should stay current if the workflow changes.

## Configuration

**docs/config.example.json** is a reference for what config fields exist and what they do. If you add or remove fields, update this file so it stays in sync with the actual code.

## Tooling

Needs `git` and `gh` on PATH, with `gh` authenticated (`gh auth login`, or a `GITHUB_TOKEN` in the environment). If something's missing, say so instead of working around it.

GitHub CLI is installed at: `C:\Program Files\GitHub CLI\gh.exe`
