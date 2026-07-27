# Notes for Claude

## Branches and PRs

Don't commit to `main` directly. Every change goes through a branch and a PR, even small ones.

Branch naming: `feat/` for features, `fix/` for fixes, `docs/` for documentation. Keep it descriptive — `feat/auto-login`, `fix/log-console-crash`.

Open the PR right after pushing, don't leave the branch sitting there:

```bash
gh pr create --title "Your PR Title" --body "..." --head your-branch --base main
```

The description should say what changed and why. A list of touched files isn't a description.

**Never add tool attribution lines** like "🤖 Generated with Claude Code" or "Co-authored-by: Claude Fable 5" or similar metadata. Focus on the technical content — what the code does, why it matters, and any relevant context. No AI or tool branding in commits or PR descriptions.

Once it's merged, clean up:

```bash
git branch -D branch-name
git push origin --delete branch-name
```

Only `main` should survive long-term.

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
