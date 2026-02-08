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
	/// Gets all project files in a directory.
	/// </summary>
	/// <param name="workingDirectory">The working directory to search.</param>
	/// <returns>A list of project file paths.</returns>
	public IReadOnlyList<string> GetProjectFiles(string workingDirectory);

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
