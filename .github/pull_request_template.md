<!-- Five bullets, maximum. What changed and why it matters.
     No preamble, no restating the title, no list of touched files. -->

-

## Detail

<!-- Delete this heading if the change doesn't have any. Otherwise: the root cause, an
     approach that was tried and rejected, an edge case that turned out to matter.
     Say what you did not verify, too. -->

## Testing

<!-- There is no test suite - this app is tested by running it against the real game.
     What did you run, and what did you see? "Should work" is not an answer. -->

## Checklist

- [ ] I have read [CLAUDE.md](../CLAUDE.md), including the **Danger zones** section
- [ ] This PR does not touch the input timing in `InputService`, the DPI or single-file publish settings in `AntiAfk.App.csproj`, or the coordinates in `GameConstants` — or it does, and the detail section says what was observed
- [ ] Docs updated if behaviour changed, both language versions of any bilingual pair
- [ ] Title follows Conventional Commits: `type: description`, imperative, lowercase, under 72 characters
