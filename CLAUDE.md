# Notes for Claude

The contribution rules live in CONTRIBUTING.en.md and they apply to you exactly as they apply to a person: branch and PR workflow, Conventional Commits titles, how to write a description, danger zones, documentation, language, releases. Read it before making any change.

@CONTRIBUTING.en.md

`CONTRIBUTING.md` is the Russian half of that pair. Change one and you change both, in the same commit.

## If you read nothing else

The six danger zones, by name. Full reasoning for each is in CONTRIBUTING.en.md; don't act on this list alone.

- The sleeps in `InputService.ClickScreen`, and nothing logged between the steps
- DPI awareness stays at the WinForms default, SystemAware
- Native AOT is unusable — `RichTextBox` COM interop
- The single-file publish settings in `AntiAfk.App.csproj`
- `<Version>` in that csproj is a placeholder the release workflow overwrites
- Coordinates in `GameConstants` are 1920×1080 and go through `CoordinateScaler`

## Specific to you

**Never add tool attribution lines** — no "🤖 Generated with Claude Code", no "Co-authored-by: Claude Fable 5", no similar metadata, in commits or PR descriptions. Focus on the technical content: what the code does, why it matters, and any relevant context. No AI or tool branding anywhere in the repository.

CONTRIBUTING.en.md tells a contributor to run the app and report what they saw. You usually cannot — this needs a real game window and a real cursor. When that is the case, say so plainly in the PR: what you verified by reading, what remains unverified, and what a human would have to run to confirm it. Don't describe a change as fixed when nobody has watched it work. That failure has a history here, and it is written up in the danger zones section.

Clean up after a merge:

```bash
git branch -D branch-name
git push origin --delete branch-name
```

Only `main` survives long-term.

## Tooling on this machine

Needs `git` and `gh` on PATH, with `gh` authenticated (`gh auth login`, or a `GITHUB_TOKEN` in the environment). If something's missing, say so instead of working around it.

GitHub CLI is at `C:\Program Files\GitHub CLI\gh.exe`.

`gh pr create --body "..."` and `git commit -m "..."` break in PowerShell when the text contains double quotes — it re-parses them and splits the argument. Write the text to a file and use `--body-file` or `git commit -F`.

This file and CONTRIBUTING should stay current if the workflow changes.
