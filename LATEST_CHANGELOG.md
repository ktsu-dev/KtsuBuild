## v2.22.0 (minor)

Changes since v2.21.0:

- Warn when a profile readme --only name produces no row, and say why [patch] (#291) (matt-edmondson)
- Recognise the main project from the GitHub repository name, ignoring case and trailing slashes [minor] (#290) (matt-edmondson)
- Leave benchmark, sample, example and demo libraries out of the pack [patch] (#284) (matt-edmondson)
- Use the project's WinGetCommandAlias in the winget manifests [patch] (#283) (matt-edmondson)
- Show a timed-out or startup-failed CI run as failing on the profile [patch] (#282) (matt-edmondson)
- Recognise MSTest.Sdk and Microsoft.NET.Test.Sdk test projects by content [patch] (Claude)
- Pass the test context's cancellation token in the per-feed publish tests (Claude)
- Pass the test context's cancellation token in the winget license tests (Claude)
- Pass the test context's cancellation token in the --only preview tests (Claude)
- Gate each package feed on its own credential and warn when one is skipped [patch] (Claude)
- Take the winget License and Copyright from LICENSE.md and COPYRIGHT.md [patch] (Claude)
- Print the rows instead of overwriting the README when --only narrows a run [patch] (Claude)
- Advance the glob index in a while loop rather than the for body (Claude)
- Let a **/ glob segment match zero directories [patch] (Claude)
- Take only v-prefixed semantic version tags as releases [patch] (matt-edmondson)
- Escape angle brackets in changelog subjects outside code spans [patch] (matt-edmondson)
- docs: document --version-bump on the release command (matt-edmondson)
- fix: honor --version-bump in the release command [patch] (matt-edmondson)

