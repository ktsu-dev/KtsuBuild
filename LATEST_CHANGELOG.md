## v2.19.0 (minor)

Changes since v2.18.0:

- Show the last completed ci.yml run in the profile Status column ([@Claude](https://github.com/Claude))
- Release a prerelease of X.Y.0 as X.Y.0 on a Minor or Major bump ([@Claude](https://github.com/Claude))
- Match template links to repositories ignoring case, as GitHub does ([@Claude](https://github.com/Claude))
- Keep TAGS.md topics within GitHub's rules so one bad tag cannot block the update ([@Claude](https://github.com/Claude))
- Read every page of releases so a run of prereleases cannot hide the stable one ([@Claude](https://github.com/Claude))
- Accept OperationCanceledException from a cancelled ProcessRunner run ([@Claude](https://github.com/Claude))
- Accept OperationCanceledException from a cancelled ProcessRunner run ([@Claude](https://github.com/Claude))
- Move CI onto the shared ci-shared.yml pipeline ([@Claude](https://github.com/Claude))
- Fall back to an empty commit list in the changelog test's range lookup ([@Claude](https://github.com/Claude))
- Diff a -pre.1 changelog entry against the previous tag, not the stable release after it [patch] ([@Claude](https://github.com/Claude))
- Match bot patterns against the author, so human commits mentioning GitHub still count [patch] ([@Claude](https://github.com/Claude))
- Strip only a trailing .git from the remote URL, so ktsu-dev/.github no longer parses as ktsu-dev/hub [patch] ([@matt-edmondson](https://github.com/matt-edmondson))

