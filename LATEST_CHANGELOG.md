## v2.24.0 (minor)

Changes since v2.23.0:

- Read a template link followed by a period or .git as the repository it names [patch] (#313) (matt-edmondson)
- Count a project as supporting only when the keyword starts a word of its name [patch] (#314) (matt-edmondson)
- Skip pull request runs when reading a repository's profile Status [patch] (#315) (matt-edmondson)
- Count KtsuBuild's own metadata commit as automation [patch] (#316) (matt-edmondson)
- Detect public API changes by shape, and leave test code out [patch] (#317) (matt-edmondson)
- Resolve a relative iOS --project against the workspace [patch] (#318) (matt-edmondson)
- Classify a head whose TFM comes from ktsu.Sdk.iOS as iOS [patch] (#319) (matt-edmondson)
- Keep earlier prerelease series' notes when a new series starts [patch] (#324) (matt-edmondson)
- Quote free text in the winget locale manifest [patch] (#325) (matt-edmondson)
- Pass iOS signing passwords as separate arguments [patch] (#326) (matt-edmondson)
- Delete the test filter when every test project is excluded [patch] (#328) (matt-edmondson)
- Escape commas and semicolons in the iOS signing identity and profile name [patch] (#329) (matt-edmondson)
- Restore a developer's own App Store Connect key after ios upload [patch] (#330) (matt-edmondson)
- Write version bump and version show diagnostics to standard error [patch] (#331) (matt-edmondson)
- Empty staging and each publish folder before a release [patch] (#332) (matt-edmondson)
- Pack only the projects in the solution [patch] (#333) (matt-edmondson)
- Apply the pinned Xcode and iOS workload before ios build and the ci iOS build [minor] (#335) (matt-edmondson)
- Skip benchmarks, examples and SDK-only test projects in winget library detection [patch] (#301) (matt-edmondson)

