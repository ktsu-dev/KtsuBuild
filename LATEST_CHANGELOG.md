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

