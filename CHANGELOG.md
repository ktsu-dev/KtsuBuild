## v2.12.1

No significant changes detected since v2.12.1.

## v2.12.1 (patch)

Changes since v2.12.0:

- Bump MSTest.Sdk from 4.3.3 to 4.4.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v2.12.0 (minor)

Changes since v2.11.0:

- feat: fail when the profile template promotes a retired repository [minor] ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.11.0 (minor)

Changes since v2.10.0:

- feat: trade the prerelease, winget, and README columns for stars [minor] ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.10.0 (minor)

Changes since v2.9.0:

- fix: clear the remaining analyzer findings on the profile code [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: address the review findings on the profile generator [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: report what each repository ships and which SDK it pins [minor] ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: generate the organization profile README [minor] ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.9.0 (minor)

Changes since v2.8.0:

- fix: merge every test project's coverage instead of picking one [minor] ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: write the git identity locally instead of machine-wide [minor] ([@matt-edmondson](https://github.com/matt-edmondson))
- ci: make the SonarQube quality gate opt in [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- ci: adopt the unified dotnet workflow [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: report an --exclude that matched nothing at information level [patch] ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.8.2 (patch)

Changes since v2.8.1:

- ci: make the SonarQube quality gate opt in [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- ci: adopt the unified dotnet workflow [patch] ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.8.1 (patch)

Changes since v2.8.0:

- fix: report an --exclude that matched nothing at information level [patch] ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.8.0 (minor)

Changes since v2.7.0:

- feat: add --exclude to test all [minor] ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.7.0 (minor)

Changes since v2.6.0:

- fix: run test all in one invocation and ask the Sdk for the host runtime [minor] ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.6.0 (minor)

Changes since v2.5.0:

- docs: document test all, and name platforms as readers expect [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add test all, which tests against the host runtime only [minor] ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: allow a test run to pin the host runtime [minor] ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: plan the host runtime test command ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: select the test project with --project instead of a positional path [patch] ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.5.1 (patch)

Changes since v2.5.0:

- fix: select the test project with --project instead of a positional path [patch] ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.5.0 (minor)

Changes since v2.4.0:

- docs: document --no-build and the release target [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: release the commit that carries the version [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add --no-build to test run [minor] ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: plan the no-build option and the release target fix ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.4.0 (minor)

Changes since v2.3.0:

- refactor: split preparation from version resolution to preserve ci ordering [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- refactor: extract the CI pipeline stages into a service [minor] ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: plan the pipeline extraction ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.3.0 (minor)

Changes since v2.2.0:

- docs: document the ci skip flags [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add --no-test and --no-release to the ci command [minor] ([@matt-edmondson](https://github.com/matt-edmondson))
- refactor: extract the CI release decision behind a tested seam [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: plan the ci skip flags ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.2.0 (minor)

Changes since v2.1.0:

- docs: document the test commands and --no-test [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add --no-test to the build command [minor] ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: add the test list and test run commands [minor] ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: reject empty project path and strengthen TestProjectAsync test assertions [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: run a single test project with coverage [minor] ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: strengthen host-filtering regression test and use StringAssert [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: list test projects with their platform [minor] ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: plan the scoped test commands ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: stop forks rewriting the copyright holder [patch] ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: scope build badge to the default branch ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: correct README, DESCRIPTION and TAGS metadata ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Run build and test when the version increment is Skip ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.1.6 (patch)

Changes since v2.1.5:

- fix: stop forks rewriting the copyright holder [patch] ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.1.5 (patch)

Changes since v2.1.4:

- Bump the ktsu group with 9 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v2.1.4 (patch)

Changes since v2.1.3:

- docs: scope build badge to the default branch ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: correct README, DESCRIPTION and TAGS metadata ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.1.3 (patch)

Changes since v2.1.2:

- [patch] Run build and test when the version increment is Skip ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.1.2 (patch)

Changes since v2.1.1:

- Bump the ktsu group with 9 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v2.1.1 (patch)

Changes since v2.1.0:

- Bump Polyfill from 11.0.2 to 11.2.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))
- Bump the ktsu group with 9 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))
- Fix nullability handling in GitHubActionsOutput.Write method ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.1.1-pre.1 (prerelease)

No significant changes detected since v2.1.1.

## v2.1.0 (minor)

Changes since v2.0.0:

- Sync .runsettings ([@KtsuTools](https://github.com/KtsuTools))
- Sync .editorconfig ([@KtsuTools](https://github.com/KtsuTools))
- Sync .gitattributes ([@KtsuTools](https://github.com/KtsuTools))
- Sync global.json ([@KtsuTools](https://github.com/KtsuTools))
- Sync global.json ([@KtsuTools](https://github.com/KtsuTools))
- [patch] Run CI from the installed KtsuBuild tool instead of a source clone ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.0.2 (patch)

Changes since v2.0.1:

- Bump Polyfill from 11.0.1 to 11.0.2 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v2.0.1 (patch)

Changes since v2.0.0:

- [patch] Run CI from the installed KtsuBuild tool instead of a source clone ([@matt-edmondson](https://github.com/matt-edmondson))

## v2.0.0 (major)

Changes since v1.9.0:

- [major] Distribute the CLI as a dotnet tool ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.9.0 (minor)

Changes since v1.8.0:

- [minor] Fix all CI build and SonarCloud warnings ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Stop the changelog emitting an empty section for a release with no changes ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] fix(pack): pass solution context (SolutionDir/SolutionName) to per-project pack ([@matt-edmondson](https://github.com/matt-edmondson))
- chore: remove unused SourceLink package versions ([@matt-edmondson](https://github.com/matt-edmondson))
- chore: remove redundant package references ([@matt-edmondson](https://github.com/matt-edmondson))
- Remove stale files ([@matt-edmondson](https://github.com/matt-edmondson))
- Retry code-coverage collector pipe flake in test runner [patch] ([@Claude](https://github.com/Claude))

## v1.8.17 (patch)

Changes since v1.8.16:

- Merge remote-tracking branch 'refs/remotes/origin/main' ([@ktsu[bot]](https://github.com/ktsu[bot]))
- Sync .github\workflows\dotnet.yml ([@ktsu[bot]](https://github.com/ktsu[bot]))
- Sync .github\workflows\dependabot-merge.yml ([@ktsu[bot]](https://github.com/ktsu[bot]))
- Sync .gitattributes ([@ktsu[bot]](https://github.com/ktsu[bot]))

## v1.8.16 (patch)

Changes since v1.8.15:

- [patch] Stop the changelog emitting an empty section for a release with no changes ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.8.15 (patch)

Changes since v1.8.14:

- Bump MSTest.Sdk from 4.3.2 to 4.3.3 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.8.14 (patch)

Changes since v1.8.13:

- Bump the ktsu group with 8 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.8.13 (patch)

Changes since v1.8.12:

- Bump System.CommandLine from 2.0.9 to 2.0.10 ([@dependabot[bot]](https://github.com/dependabot[bot]))
- Bump Polyfill from 10.11.2 to 11.0.1 ([@dependabot[bot]](https://github.com/dependabot[bot]))
- Bump the microsoft group with 6 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.8.12 (patch)

Changes since v1.8.11:

- Bump MSTest.Sdk from 4.3.0 to 4.3.2 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.8.11 (patch)

Changes since v1.8.10:

- Bump NSubstitute from 5.3.0 to 6.0.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.8.10 (patch)

Changes since v1.8.9:

- Bump MSTest.Sdk from 4.2.3 to 4.3.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.8.9 (patch)

Changes since v1.8.8:

- Bump the ktsu group with 8 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.8.8 (patch)

Changes since v1.8.7:

- Bump Polyfill from 10.11.0 to 10.11.2 ([@dependabot[bot]](https://github.com/dependabot[bot]))
- Bump the ktsu group with 8 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.8.7 (patch)

Changes since v1.8.6:

- [patch] fix(pack): pass solution context (SolutionDir/SolutionName) to per-project pack ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.8.6 (patch)

Changes since v1.8.5:

- Merge remote-tracking branch 'refs/remotes/origin/main' ([@ktsu[bot]](https://github.com/ktsu[bot]))
- Sync .github\workflows\dotnet.yml ([@ktsu[bot]](https://github.com/ktsu[bot]))
- Sync global.json ([@ktsu[bot]](https://github.com/ktsu[bot]))

## v1.8.5 (patch)

Changes since v1.8.4:

- chore: remove unused SourceLink package versions ([@matt-edmondson](https://github.com/matt-edmondson))
- chore: remove redundant package references ([@matt-edmondson](https://github.com/matt-edmondson))
- Remove stale files ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.8.4 (patch)

Changes since v1.8.3:

- Sync .github\workflows\dotnet.yml ([@ktsu[bot]](https://github.com/ktsu[bot]))
- Sync .github\dependabot.yml ([@ktsu[bot]](https://github.com/ktsu[bot]))
- Sync .serena\.gitignore ([@ktsu[bot]](https://github.com/ktsu[bot]))
- Sync .gitignore ([@ktsu[bot]](https://github.com/ktsu[bot]))
- Sync global.json ([@ktsu[bot]](https://github.com/ktsu[bot]))

## v1.8.3 (patch)

Changes since v1.8.2:

- Bump System.CommandLine from 2.0.8 to 2.0.9 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.8.2 (patch)

Changes since v1.8.1:

- Bump the microsoft group with 5 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.8.1 (patch)

Changes since v1.8.0:

- Retry code-coverage collector pipe flake in test runner [patch] ([@Claude](https://github.com/Claude))

## v1.8.0 (minor)

Changes since v1.7.0:

- Fix CA1506 coupling and resolve cross-platform IDE0055 line-ending errors ([@Claude](https://github.com/Claude))
- Auto-detect and validate iOS heads in the ci pipeline ([@Claude](https://github.com/Claude))
- Add TestFlight upload command (iOS support phase 4) ([@Claude](https://github.com/Claude))

## v1.7.0 (minor)

Changes since v1.6.0:

- Fix iOS packaging code-style errors flagged by CI ([@Claude](https://github.com/Claude))
- Add signed iOS packaging command (iOS support phase 3) ([@Claude](https://github.com/Claude))

## v1.6.0 (minor)

Changes since v1.5.0:

- Extract iOS build orchestration into testable IosBuildService ([@Claude](https://github.com/Claude))
- Remove unnecessary null-forgiving operator in FindAppBundles ([@Claude](https://github.com/Claude))
- Add unsigned iOS build command (iOS support phase 2) ([@Claude](https://github.com/Claude))

## v1.5.0 (minor)

Changes since v1.4.0:

- Add iOS-aware project classification and host build gating ([@Claude](https://github.com/Claude))
- Add plan for iOS build and publish support ([@Claude](https://github.com/Claude))

## v1.4.2 (patch)

Changes since v1.4.1:

- Bump Polyfill from 10.7.1 to 10.8.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.4.1 (patch)

Changes since v1.4.0:

- Bump Polyfill from 10.7.0 to 10.7.1 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.4.0 (minor)

Changes since v1.3.0:

- Fix metadata commit failing when files are identical ([@Claude](https://github.com/Claude))
- Add TAGS.md with NuGet package tags ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Fix CI bootstrap deadlock: use current checkout for self-builds ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Remove unnecessary null-forgiving operators after IsNullOrEmpty checks (IDE0370) ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.3.24 (patch)

Changes since v1.3.23:

- Bump YamlDotNet from 17.1.0 to 18.0.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))
- Bump Polyfill from 10.6.0 to 10.7.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.23 (patch)

Changes since v1.3.22:

- Bump Polyfill from 10.5.1 to 10.6.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.22 (patch)

Changes since v1.3.21:

- Bump MSTest.Sdk from 4.2.2 to 4.2.3 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.21 (patch)

Changes since v1.3.20:

- Bump System.CommandLine from 2.0.7 to 2.0.8 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.20 (patch)

Changes since v1.3.19:

- Bump the microsoft group with 7 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.19 (patch)

Changes since v1.3.18:

- Bump Polyfill from 10.5.0 to 10.5.1 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.18 (patch)

Changes since v1.3.17:

- Add TAGS.md with NuGet package tags ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.3.17 (patch)

Changes since v1.3.16:

- Bump Polyfill from 10.4.0 to 10.5.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.16 (patch)

Changes since v1.3.15:

- Bump Polyfill from 10.3.0 to 10.4.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.15 (patch)

Changes since v1.3.14:

- Bump MSTest.Sdk from 4.2.1 to 4.2.2 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.14 (patch)

Changes since v1.3.13:

- Bump YamlDotNet from 17.0.1 to 17.1.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.13 (patch)

Changes since v1.3.12:

- Bump the system group with 1 update ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.12 (patch)

Changes since v1.3.11:

- Bump the microsoft group with 7 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.11 (patch)

Changes since v1.3.10:

- Bump the system group with 1 update ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.10 (patch)

Changes since v1.3.9:

- Bump the microsoft group with 7 updates ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.9 (patch)

Changes since v1.3.8:

- Bump YamlDotNet from 17.0.0 to 17.0.1 ([@dependabot[bot]](https://github.com/dependabot[bot]))
- Bump Polyfill from 10.1.1 to 10.3.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.8 (patch)

Changes since v1.3.7:

- Bump YamlDotNet from 16.3.0 to 17.0.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))
- Bump Polyfill from 10.0.0 to 10.1.1 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.7 (patch)

Changes since v1.3.6:

- Bump MSTest.Sdk from 4.1.0 to 4.2.1 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.6 (patch)

Changes since v1.3.5:

- Bump Polyfill from 9.24.0 to 10.0.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.5 (patch)

Changes since v1.3.4:

- Bump Polyfill from 9.23.0 to 9.24.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.4 (patch)

Changes since v1.3.3:

- [patch] Fix CI bootstrap deadlock: use current checkout for self-builds ([@matt-edmondson](https://github.com/matt-edmondson))
- [patch] Remove unnecessary null-forgiving operators after IsNullOrEmpty checks (IDE0370) ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.3.3 (patch)

Changes since v1.3.2:

- Bump Polyfill from 9.11.0 to 9.12.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.2 (patch)

Changes since v1.3.1:

- Bump Polyfill from 9.10.0 to 9.11.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.1 (patch)

Changes since v1.3.0:

- Bump Polyfill from 9.9.0 to 9.10.0 ([@dependabot[bot]](https://github.com/dependabot[bot]))

## v1.3.0 (minor)

Changes since v1.2.0:

- Implement repository topics management and add related tests ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.2.0 (minor)

Changes since v1.1.0:

- Add unit tests for ReleaseService, LineEndingHelper, ManifestGenerator, and WingetService ([@matt-edmondson](https://github.com/matt-edmondson))
- Add conditional metadata commit logic in CiCommand for official builds ([@matt-edmondson](https://github.com/matt-edmondson))
- Refactor CiCommand to extract pipeline execution logic into a separate method for improved readability and maintainability ([@matt-edmondson](https://github.com/matt-edmondson))
- Refactor WingetService to streamline library project handling and improve logging ([@matt-edmondson](https://github.com/matt-edmondson))
- Refactor WriteAuthorsFileAsync to use StringBuilder for improved performance ([@matt-edmondson](https://github.com/matt-edmondson))
- Refactor CI permissions for least-privilege access, add SonarLint configuration, and streamline version bump parsing ([@matt-edmondson](https://github.com/matt-edmondson))
- Refactor CI command to use arguments array for backward compatibility ([@matt-edmondson](https://github.com/matt-edmondson))
- Refactor CI command to improve backward compatibility for version bump handling ([@matt-edmondson](https://github.com/matt-edmondson))
- Add version bump control to CI command and workflows ([@matt-edmondson](https://github.com/matt-edmondson))
- Dont fail when theres no executables to put in a winget manifest ([@matt-edmondson](https://github.com/matt-edmondson))
- refactor: remove skipped_release logic from workflow conditions ([@matt-edmondson](https://github.com/matt-edmondson))
- Remove update-winget-manifests.ps1 script as it is no longer needed for managing winget manifests. ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.8 (patch)

Changes since v1.1.7:

- Add conditional metadata commit logic in CiCommand for official builds ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.7 (patch)

Changes since v1.1.6:

- Refactor CiCommand to extract pipeline execution logic into a separate method for improved readability and maintainability ([@matt-edmondson](https://github.com/matt-edmondson))
- Refactor WingetService to streamline library project handling and improve logging ([@matt-edmondson](https://github.com/matt-edmondson))
- Refactor WriteAuthorsFileAsync to use StringBuilder for improved performance ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.7-pre.1 (prerelease)

No significant changes detected since v1.1.7.

## v1.1.6 (patch)

Changes since v1.1.5:

- Merge remote-tracking branch 'refs/remotes/origin/main' ([@ktsu[bot]](https://github.com/ktsu[bot]))
- Sync .github\workflows\dotnet.yml ([@ktsu[bot]](https://github.com/ktsu[bot]))

## v1.1.5 (patch)

Changes since v1.1.4:

- Refactor CI permissions for least-privilege access, add SonarLint configuration, and streamline version bump parsing ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.4 (patch)

Changes since v1.1.3:

- Refactor CI command to use arguments array for backward compatibility ([@matt-edmondson](https://github.com/matt-edmondson))
- Refactor CI command to improve backward compatibility for version bump handling ([@matt-edmondson](https://github.com/matt-edmondson))
- Add version bump control to CI command and workflows ([@matt-edmondson](https://github.com/matt-edmondson))
- Dont fail when theres no executables to put in a winget manifest ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.3 (patch)

Changes since v1.1.2:

- Merge remote-tracking branch 'refs/remotes/origin/main' ([@ktsu[bot]](https://github.com/ktsu[bot]))
- Sync global.json ([@ktsu[bot]](https://github.com/ktsu[bot]))

## v1.1.2 (patch)

Changes since v1.1.1:

- refactor: remove skipped_release logic from workflow conditions ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.1 (patch)

Changes since v1.1.0:

- Remove update-winget-manifests.ps1 script as it is no longer needed for managing winget manifests. ([@matt-edmondson](https://github.com/matt-edmondson))

## v1.1.0 (major)

- feat: add AUTHORS file handling to MetadataService for conditional inclusion ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: add suppression for conditional log level evaluation performance warning in BuildLogger ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: ensure latest tag retrieval does not fail by adding fallback to true ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: update tag cloning logic to ensure correct latest version retrieval ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: enhance CI pipeline by cloning latest KtsuBuild tag and simplifying build steps ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: update project files to target .NET 10.0 and improve test visibility - Updated KtsuBuild.CLI.csproj and KtsuBuild.csproj to target net10.0 - Added InternalsVisibleTo attribute for KtsuBuild.Tests - Modified KtsuBuild.Tests.csproj to target net10.0 and include additional package references - Refactored test files to use ConfigureAwait(false) for async calls ([@matt-edmondson](https://github.com/matt-edmondson))
- refactor: remove embedded resource template and use constant for license template ([@matt-edmondson](https://github.com/matt-edmondson))
- docs: add CLAUDE.md for project guidance and update README.md for clarity and usage instructions ([@matt-edmondson](https://github.com/matt-edmondson))
- fix: update no-commit option description for clarity ([@matt-edmondson](https://github.com/matt-edmondson))
- refactor: remove unnecessary test package references and improve project detection logic ([@matt-edmondson](https://github.com/matt-edmondson))
- Additional initial work ([@matt-edmondson](https://github.com/matt-edmondson))
- refactor: simplify variable declarations and enhance code readability across command files ([@matt-edmondson](https://github.com/matt-edmondson))
- feat: update default single file option and enhance build logging ([@matt-edmondson](https://github.com/matt-edmondson))
- Refactor and enhance KtsuBuild utilities and services ([@matt-edmondson](https://github.com/matt-edmondson))
- Add unit tests for Changelog and License generation ([@matt-edmondson](https://github.com/matt-edmondson))
- Add Winget manifest generation functionality ([@matt-edmondson](https://github.com/matt-edmondson))
- Initial files ([@matt-edmondson](https://github.com/matt-edmondson))

