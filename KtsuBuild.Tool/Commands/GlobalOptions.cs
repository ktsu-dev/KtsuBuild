// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tool.Commands;

using System.CommandLine;

/// <summary>
/// Global options shared across all commands.
/// </summary>
public static class GlobalOptions
{
	/// <summary>
	/// Gets the workspace option.
	/// </summary>
	/// <remarks>
	/// A relative value is made absolute against the current directory here, once. Paths built from
	/// a relative workspace stayed relative, and the child processes that start inside the workspace
	/// resolved them a second time, so <c>--workspace repo</c> packed <c>repo/repo/...</c>.
	/// </remarks>
	public static Option<string> Workspace { get; } = new("--workspace", "-w")
	{
		Description = "The workspace/repository path",
		DefaultValueFactory = _ => Directory.GetCurrentDirectory(),
		CustomParser = result => Path.GetFullPath(result.Tokens.Single().Value),
	};

	/// <summary>
	/// Gets the configuration option.
	/// </summary>
	public static Option<string> Configuration { get; } = new("--configuration", "-c")
	{
		Description = "The build configuration (Debug/Release)",
		DefaultValueFactory = _ => "Release",
	};

	/// <summary>
	/// Gets the verbose option.
	/// </summary>
	public static Option<bool> Verbose { get; } = new("--verbose", "-v")
	{
		Description = "Enable verbose output",
		DefaultValueFactory = _ => false,
	};

	/// <summary>
	/// Gets the dry-run option.
	/// </summary>
	public static Option<bool> DryRun { get; } = new("--dry-run")
	{
		Description = "Preview actions without executing them",
		DefaultValueFactory = _ => false,
	};

	/// <summary>
	/// Gets the values <see cref="VersionBump"/> accepts, compared ignoring case. Declared before
	/// it because static initializers run in declaration order.
	/// </summary>
	internal static IReadOnlyList<string> VersionBumpValues { get; } = ["auto", "patch", "minor", "major"];

	/// <summary>
	/// Gets the version-bump option.
	/// </summary>
	public static Option<string> VersionBump { get; } = CreateVersionBumpOption();

	/// <summary>
	/// Gets the no-test option.
	/// </summary>
	public static Option<bool> NoTest { get; } = new("--no-test")
	{
		Description = "Skip the test step",
		DefaultValueFactory = _ => false,
	};

	/// <summary>
	/// Gets the no-release option.
	/// </summary>
	public static Option<bool> NoRelease { get; } = new("--no-release")
	{
		Description = "Skip the release step, leaving the release to a later step or job",
		DefaultValueFactory = _ => false,
	};

	// A value outside the list used to fall through to automatic detection, so a typo such as
	// "majr" published the detected version instead of the intended one, and a published NuGet
	// version cannot be taken back. Rejecting it at parse time fails the run before any stage.
	private static Option<string> CreateVersionBumpOption()
	{
		Option<string> option = new("--version-bump")
		{
			Description = "Force a specific version bump type (auto, patch, minor, major)",
			DefaultValueFactory = _ => "auto",
		};

		option.Validators.Add(result =>
		{
			string? value = result.GetValueOrDefault<string>();
			if (!VersionBumpValues.Contains(value, StringComparer.OrdinalIgnoreCase))
			{
				result.AddError($"'{value}' is not a valid --version-bump value. Use one of: {string.Join(", ", VersionBumpValues)}.");
			}
		});
		option.CompletionSources.Add([.. VersionBumpValues]);

		return option;
	}
}
