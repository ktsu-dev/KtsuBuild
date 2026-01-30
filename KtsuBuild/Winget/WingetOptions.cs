// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Winget;

/// <summary>
/// Options for Winget manifest generation.
/// </summary>
public class WingetOptions
{
	/// <summary>
	/// Gets or sets the version to generate manifests for.
	/// </summary>
	public required string Version { get; set; }

	/// <summary>
	/// Gets or sets the GitHub repository (owner/repo format).
	/// </summary>
	public string? GitHubRepo { get; set; }

	/// <summary>
	/// Gets or sets the package identifier (e.g., "company.Product").
	/// </summary>
	public string? PackageId { get; set; }

	/// <summary>
	/// Gets or sets the artifact name pattern with {version} and {arch} placeholders.
	/// </summary>
	public string? ArtifactNamePattern { get; set; }

	/// <summary>
	/// Gets or sets the executable name in the zip file.
	/// </summary>
	public string? ExecutableName { get; set; }

	/// <summary>
	/// Gets or sets the command alias for the executable.
	/// </summary>
	public string? CommandAlias { get; set; }

	/// <summary>
	/// Gets or sets the root directory of the project.
	/// </summary>
	public string RootDirectory { get; set; } = ".";

	/// <summary>
	/// Gets or sets the output directory for manifest files.
	/// </summary>
	public string OutputDirectory { get; set; } = "winget";

	/// <summary>
	/// Gets or sets the staging directory where hashes.txt might be found.
	/// </summary>
	public string? StagingDirectory { get; set; }
}
