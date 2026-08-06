// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.DotNet;

/// <summary>
/// Options for publishing an application for a single runtime identifier.
/// </summary>
public class PublishOptions
{
	/// <summary>
	/// Gets or sets the working directory the publish runs from.
	/// </summary>
	public required string WorkingDirectory { get; set; }

	/// <summary>
	/// Gets or sets the path to the project file to publish.
	/// </summary>
	public required string ProjectPath { get; set; }

	/// <summary>
	/// Gets or sets the output path the published runtime folder is written to.
	/// </summary>
	public required string OutputPath { get; set; }

	/// <summary>
	/// Gets or sets the target runtime identifier (for example <c>win-x64</c>).
	/// </summary>
	public required string Runtime { get; set; }

	/// <summary>
	/// Gets or sets the build configuration (Debug/Release).
	/// </summary>
	public string Configuration { get; set; } = "Release";

	/// <summary>
	/// Gets or sets whether to create a self-contained deployment.
	/// </summary>
	public bool SelfContained { get; set; } = true;

	/// <summary>
	/// Gets or sets whether to create a single file executable.
	/// </summary>
	public bool SingleFile { get; set; }
}
