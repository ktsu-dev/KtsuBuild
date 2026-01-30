// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Winget;

/// <summary>
/// Result of Winget manifest generation.
/// </summary>
public class WingetManifestResult
{
	/// <summary>
	/// Gets or sets whether the generation was successful.
	/// </summary>
	public bool Success { get; set; }

	/// <summary>
	/// Gets or sets the error message if generation failed.
	/// </summary>
	public string? Error { get; set; }

	/// <summary>
	/// Gets or sets whether the project was detected as library-only (no manifests needed).
	/// </summary>
	public bool IsLibraryOnly { get; set; }

	/// <summary>
	/// Gets or sets the generated manifest file paths.
	/// </summary>
	public IReadOnlyList<string> ManifestFiles { get; set; } = [];

	/// <summary>
	/// Gets or sets the package identifier used.
	/// </summary>
	public string PackageId { get; set; } = string.Empty;
}
