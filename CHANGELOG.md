## v1.1.4 (patch)

Changes since v1.1.3:

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

