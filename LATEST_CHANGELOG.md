## v2.20.0 (minor)

Changes since v2.19.0:

- [minor] Keep a repository's own license instead of overwriting it with MIT ([@Claude](https://github.com/Claude))
- Merge remote-tracking branch 'origin/main' into fix/215-absolute-workspace ([@Claude](https://github.com/Claude))
- Report the marketing version substitution from the library so it is covered by tests ([@Claude](https://github.com/Claude))
- Pass the test context's cancellation token in the test-argument capture helpers ([@Claude](https://github.com/Claude))
- Pass the test context's cancellation token in the iOS short version test ([@Claude](https://github.com/Claude))
- Pass the test context's cancellation token in the build retry test ([@Claude](https://github.com/Claude))
- Stamp the numeric core of the computed version as the iOS marketing version [patch] ([@Claude](https://github.com/Claude))
- Make the failed-build retry actually verbose instead of repeating the quiet rebuild [patch] ([@Claude](https://github.com/Claude))
- Fail the release when a project fails to pack instead of only warning [patch] ([@Claude](https://github.com/Claude))
- Compare relative-workspace test paths as the process directory reports them ([@Claude](https://github.com/Claude))
- Make a relative --workspace absolute so paths are not resolved twice [patch] ([@Claude](https://github.com/Claude))
- Reject an unknown --version-bump instead of publishing the detected version [patch] ([@Claude](https://github.com/Claude))
- Fail profile readme instead of writing it without a table when the org listing fails [patch] ([@Claude](https://github.com/Claude))
- Keep an existing COPYRIGHT.md instead of rewriting it from the clock [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- Pass the test's cancellation token when writing a project file in the release tests ([@Claude](https://github.com/Claude))
- Pass the test's cancellation token in the new release tests ([@Claude](https://github.com/Claude))
- Resolve release changelog paths against the workspace [patch] ([@Claude](https://github.com/Claude))
- Write an already-released version's changelog entry once ([@matt-edmondson](https://github.com/matt-edmondson))

