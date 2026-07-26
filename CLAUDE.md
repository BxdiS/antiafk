# Notes for Claude

## Branches and PRs

Don't commit to `main` directly. Every change goes through a branch and a PR, even small ones.

Branch naming: `feat/` for features, `fix/` for fixes, `docs/` for documentation. Keep it descriptive — `feat/auto-login`, `fix/log-console-crash`.

Open the PR right after pushing, don't leave the branch sitting there:

```bash
gh pr create --title "Your PR Title" --body "..." --head your-branch --base main
```

The description should say what changed and why. A list of touched files isn't a description.

Once it's merged, clean up:

```bash
git branch -D branch-name
git push origin --delete branch-name
```

Only `main` should survive long-term.

## Before opening a PR

Code compiles without warnings. Tests pass. No obvious crashes. Style matches whatever the surrounding file is already doing.

## Tooling

Needs `git` and `gh` on PATH, with `gh` authenticated (`gh auth login`, or a `GITHUB_TOKEN` in the environment). If something's missing, say so instead of working around it.
