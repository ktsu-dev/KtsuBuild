// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Abstractions;

using KtsuBuild.DotNet;

/// <summary>
/// Interface for .NET SDK operations.
/// </summary>
public interface IDotNetService
{
	/// <summary>
	/// Restores NuGet packages.
	/// </summary>
	/// <param name="workingDirectory">The working directory.</param>
	/// <param name="lockedMode">Whether to use locked mode.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task RestoreAsync(string workingDirectory, bool lockedMode = true, CancellationToken cancellationToken = default);

	/// <summary>
	/// Builds the solution or project.
	/// </summary>
	/// <param name="workingDirectory">The working directory.</param>
	/// <param name="configuration">The build configuration (Debug/Release).</param>
	/// <param name="additionalArgs">Additional build arguments.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task BuildAsync(string workingDirectory, string configuration = "Release", string? additionalArgs = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs tests with optional code coverage.
	/// </summary>
	/// <param name="workingDirectory">The working directory.</param>
	/// <param name="configuration">The build configuration.</param>
	/// <param name="coverageOutputPath">Path for coverage output.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task TestAsync(string workingDirectory, string configuration = "Release", string? coverageOutputPath = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Runs a single test project with coverage.
	/// </summary>
	/// <param name="projectPath">The project file to test.</param>
	/// <param name="workingDirectory">The directory to run from.</param>
	/// <param name="configuration">The build configuration.</param>
	/// <param name="coverageOutputPath">Where coverage output is written. Defaults to <c>coverage</c> when null.</param>
	/// <param name="noBuild">Whether to skip building before running the tests.</param>
	/// <param name="hostRuntimeOnly">Whether to pin the build to the host runtime instead of every runtime identifier the project's packages ship.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>A task that completes when the run succeeds.</returns>
	/// <remarks>
	/// <para>
	/// Scoping a run to one project also removes the condition behind the coverage collector's
	/// exit-code-7 flake, which only appears when several test assemblies run in one invocation.
	/// The retry is kept anyway, since the caller decides how many projects an invocation covers.
	/// </para>
	/// <para>
	/// Setting <paramref name="noBuild"/> passes <c>--no-build</c> to <c>dotnet test</c>, which reads
	/// <c>obj/project.assets.json</c> for absolute paths into the build output. The caller must have
	/// already built the project for <paramref name="configuration"/>, and the outputs must still be
	/// present in the same <paramref name="workingDirectory"/> the build used. This option exists so a
	/// caller that builds once and tests several projects from that build can skip the redundant
	/// per-project rebuild.
	/// </para>
	/// <para>
	/// Setting <paramref name="hostRuntimeOnly"/> passes the current process's runtime identifier as
	/// <c>-p:RuntimeIdentifier</c> together with <c>-p:SelfContained=false</c>, always as a pair.
	/// A runtime identifier alone would make the build self-contained, which copies the whole
	/// framework into the output and makes the size problem worse rather than better. With both
	/// properties set, the build's output moves to a runtime-specific directory and copies only the
	/// host's native assets instead of the natives for every runtime identifier the project's
	/// packages ship. This defaults to <see langword="false"/> so callers stay runtime-agnostic
	/// unless they opt in.
	/// </para>
	/// </remarks>
	public Task TestProjectAsync(string projectPath, string workingDirectory, string configuration = "Release", string? coverageOutputPath = null, bool noBuild = false, bool hostRuntimeOnly = false, CancellationToken cancellationToken = default);

	/// <summary>
	/// Creates NuGet packages.
	/// </summary>
	/// <param name="workingDirectory">The working directory.</param>
	/// <param name="outputPath">The output path for packages.</param>
	/// <param name="configuration">The build configuration.</param>
	/// <param name="releaseNotesFile">Optional path to release notes file.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task PackAsync(string workingDirectory, string outputPath, string configuration = "Release", string? releaseNotesFile = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Publishes an application for a specific runtime.
	/// </summary>
	/// <param name="options">The publish options.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task PublishAsync(PublishOptions options, CancellationToken cancellationToken = default);

	/// <summary>
	/// Builds an iOS application head for a single runtime identifier. This is a
	/// distinct build shape from the desktop <see cref="PublishAsync"/>: it drives
	/// the iOS toolchain via MSBuild properties rather than the runtime-loop
	/// publish, and produces a <c>.app</c> bundle rather than a runtime folder.
	/// The build restores the project graph implicitly (no <c>--no-restore</c>),
	/// so iOS heads can be built on a macOS host without a solution-wide restore
	/// that would drag in Windows-only heads.
	/// </summary>
	/// <param name="workingDirectory">The working directory.</param>
	/// <param name="projectPath">Path to the iOS head project file.</param>
	/// <param name="runtimeIdentifier">The iOS runtime identifier (for example <c>iossimulator-arm64</c> or <c>ios-arm64</c>).</param>
	/// <param name="configuration">The build configuration.</param>
	/// <param name="codeSigning">Whether to leave code signing enabled. When false (the default) signing is disabled and the signing properties are emptied, producing an unsigned build suitable for pull-request validation without secrets.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task BuildIosAsync(
		string workingDirectory,
		string projectPath,
		string runtimeIdentifier,
		string configuration = "Release",
		bool codeSigning = false,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Gets the iOS application heads in a directory: executable projects whose
	/// target framework ties them to iOS. Unlike <see cref="GetBuildableProjects"/>
	/// this is not filtered by the current host, so the heads can be reported even
	/// when the host cannot build them.
	/// </summary>
	/// <param name="workingDirectory">The working directory to search.</param>
	/// <returns>A list of iOS head project file paths.</returns>
	public IReadOnlyList<string> GetIosHeads(string workingDirectory);

	/// <summary>
	/// Gets all project files in a directory.
	/// </summary>
	/// <param name="workingDirectory">The working directory to search.</param>
	/// <returns>A list of project file paths.</returns>
	public IReadOnlyList<string> GetProjectFiles(string workingDirectory);

	/// <summary>
	/// Gets the project files in a directory that can be restored and built on
	/// the current host, excluding projects whose target framework ties them to
	/// a different platform (for example iOS projects on a non-macOS host).
	/// </summary>
	/// <param name="workingDirectory">The working directory to search.</param>
	/// <returns>A list of buildable project file paths.</returns>
	public IReadOnlyList<string> GetBuildableProjects(string workingDirectory);

	/// <summary>
	/// Gets the test projects in a directory, each with the platform it is tied to.
	/// </summary>
	/// <param name="workingDirectory">The directory to search.</param>
	/// <returns>Every test project found, whatever host it needs.</returns>
	/// <remarks>
	/// Deliberately not filtered by the current host, unlike <see cref="GetBuildableProjects"/>.
	/// The caller builds a matrix that spans hosts other than the one this runs on, so it needs the
	/// full list with each project's platform and decides which pairs are valid itself.
	/// </remarks>
	public IReadOnlyList<TestProjectInfo> GetTestProjects(string workingDirectory);

	/// <summary>
	/// Classifies a project by the platform its target framework(s) tie it to.
	/// </summary>
	/// <param name="projectPath">Path to the project file.</param>
	/// <returns>The platform classification for the project.</returns>
	public ProjectPlatform GetProjectPlatform(string projectPath);

	/// <summary>
	/// Checks whether a project can be restored and built on the current host.
	/// </summary>
	/// <param name="projectPath">Path to the project file.</param>
	/// <returns>True if the current host can build the project.</returns>
	public bool CanBuildOnCurrentHost(string projectPath);

	/// <summary>
	/// Checks if a project is an executable.
	/// </summary>
	/// <param name="projectPath">Path to the project file.</param>
	/// <returns>True if the project outputs an executable.</returns>
	public bool IsExecutableProject(string projectPath);

	/// <summary>
	/// Checks if a project is a test project.
	/// </summary>
	/// <param name="projectPath">Path to the project file.</param>
	/// <returns>True if the project is a test project.</returns>
	public bool IsTestProject(string projectPath);
}
