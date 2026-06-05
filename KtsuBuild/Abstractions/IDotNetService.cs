// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Abstractions;

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
	/// <param name="workingDirectory">The working directory.</param>
	/// <param name="projectPath">Path to the project file.</param>
	/// <param name="outputPath">The output path.</param>
	/// <param name="runtime">The target runtime (e.g., win-x64).</param>
	/// <param name="configuration">The build configuration.</param>
	/// <param name="selfContained">Whether to create a self-contained deployment.</param>
	/// <param name="singleFile">Whether to create a single file executable.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	public Task PublishAsync(
		string workingDirectory,
		string projectPath,
		string outputPath,
		string runtime,
		string configuration = "Release",
		bool selfContained = true,
		bool singleFile = false,
		CancellationToken cancellationToken = default);

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
