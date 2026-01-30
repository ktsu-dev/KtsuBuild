// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Winget;

/// <summary>
/// Represents detected project information for Winget manifest generation.
/// </summary>
public class ProjectInfo
{
	/// <summary>
	/// Gets or sets the project name.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the project type (csharp, node, rust, unknown).
	/// </summary>
	public string Type { get; set; } = "unknown";

	/// <summary>
	/// Gets or sets the executable name.
	/// </summary>
	public string ExecutableName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the command alias.
	/// </summary>
	public string? CommandAlias { get; set; }

	/// <summary>
	/// Gets or sets the supported file extensions.
	/// </summary>
	public IList<string> FileExtensions { get; set; } = [];

	/// <summary>
	/// Gets or sets the tags for the package.
	/// </summary>
	public IList<string> Tags { get; set; } = [];

	/// <summary>
	/// Gets or sets the version.
	/// </summary>
	public string Version { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the short description.
	/// </summary>
	public string ShortDescription { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the full description.
	/// </summary>
	public string Description { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the publisher name.
	/// </summary>
	public string Publisher { get; set; } = string.Empty;
}
