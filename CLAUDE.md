# Claude Code Guidelines

## Pull Request Requirements

**IMPORTANT: Every feature branch must have a Pull Request created.**

When creating a feature branch and making changes:

1. **Create the branch** with a descriptive name:
   - `feat/` prefix for new features
   - `fix/` prefix for bug fixes
   - Example: `feat/auto-login`, `fix/log-console-crash`

2. **Create a Pull Request immediately** after pushing commits using GitHub CLI:
   ```bash
   gh pr create --title "Your PR Title" \
     --body "## Summary\n- Point 1\n- Point 2" \
     --head your-branch-name --base main
   ```

3. **PR Description should include:**
   - Clear summary of changes
   - List of key modifications

4. **After PR is merged:**
   - Delete the local branch: `git branch -D branch-name`
   - Delete the remote branch: `git push origin --delete branch-name`
   - Keep the repository clean with only main branch

## Setup for Automated PR Creation

To enable automatic PR creation via GitHub CLI (`gh`):

1. **Install GitHub CLI:**
   ```bash
   # Windows (using winget)
   winget install GitHub.cli
   
   # Or download from https://github.com/cli/cli/releases
   ```

2. **Authenticate with GitHub:**
   ```bash
   gh auth login
   # Follow prompts to authenticate
   ```

3. **Alternative: Use GitHub Token environment variable:**
   ```bash
   # Set GITHUB_TOKEN in your environment
   export GITHUB_TOKEN=your_github_token_here
   ```

## Code Quality Standards

- All code must compile without errors or warnings
- Tests should pass before creating PR
- No obvious bugs or crash issues
- Follow existing code style and patterns

## Required Tool Setup for Claude

Claude should have access to:
- `gh` command (GitHub CLI) for creating PRs
- `git` for branch management
- GitHub token (via `gh auth` or `GITHUB_TOKEN` env var)

If any tool is missing, the session instructions should note this.

---

**Last Updated:** 2026-07-25
