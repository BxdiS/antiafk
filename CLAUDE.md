# Notes for Claude

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

## Before opening a PR

Code compiles without warnings. Tests pass. No obvious crashes. Style matches whatever the surrounding file is already doing.

## Documentation

When behavior changes, update the relevant docs:
- **GUIDE.md** and **GUIDE.en.md** — user-facing setup, tray menu, troubleshooting
- **ROADMAP.md** — for feature additions or major changes in direction
- **README.md** and **README.en.md** — if something about what the app does or how to get it changes

This file (CLAUDE.md) itself should stay current if the workflow changes.

## Configuration

**config.example.json** is a reference for what config fields exist and what they do. If you add or remove fields, update this file so it stays in sync with the actual code.

## Tooling

Needs `git` and `gh` on PATH, with `gh` authenticated (`gh auth login`, or a `GITHUB_TOKEN` in the environment). If something's missing, say so instead of working around it.

GitHub CLI is installed at: `C:\Program Files\GitHub CLI\gh.exe`
