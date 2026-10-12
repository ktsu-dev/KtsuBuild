## v2.19.3 (patch)

Changes since v2.19.2:

- Report the marketing version substitution from the library so it is covered by tests ([@Claude](https://github.com/Claude))
- Pass the test context's cancellation token in the test-argument capture helpers ([@Claude](https://github.com/Claude))
- Pass the test context's cancellation token in the iOS short version test ([@Claude](https://github.com/Claude))
- Pass the test context's cancellation token in the build retry test ([@Claude](https://github.com/Claude))
- Stamp the numeric core of the computed version as the iOS marketing version [patch] ([@Claude](https://github.com/Claude))
- Make the failed-build retry actually verbose instead of repeating the quiet rebuild [patch] ([@Claude](https://github.com/Claude))
- Fail the release when a project fails to pack instead of only warning [patch] ([@Claude](https://github.com/Claude))
- Reject an unknown --version-bump instead of publishing the detected version [patch] ([@Claude](https://github.com/Claude))

