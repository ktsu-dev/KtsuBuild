// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Profile;

/// <summary>
/// Settings that control how an organization profile is gathered and rendered.
/// </summary>
public sealed record ProfileOptions
{
	/// <summary>Gets the GitHub organization to profile.</summary>
	public required string Organization { get; init; }

	/// <summary>Gets the NuGet package prefix. A repository named <c>Extensions</c> in an organization
	/// with the prefix <c>ktsu</c> is looked up as <c>ktsu.Extensions</c>.</summary>
	public string PackagePrefix { get; init; } = "ktsu";

	/// <summary>Gets the MSBuild SDK whose pinned version is reported, and whose newest published
	/// version every repository is compared against.</summary>
	public string SdkPackageId { get; init; } = "ktsu.Sdk";

	/// <summary>Gets the repositories to leave out of the tables, by name. Use this for repositories
	/// covered elsewhere in the template rather than teaching the generator about them.</summary>
	public IReadOnlyList<string> ExcludedRepositories { get; init; } = [];

	/// <summary>Gets the only repositories to consider, by name. Empty means every repository. Useful
	/// for checking one repository's row without regenerating the whole profile.</summary>
	public IReadOnlyList<string> OnlyRepositories { get; init; } = [];

	/// <summary>Gets the build workflow file name every repository is expected to use.</summary>
	public string BuildWorkflowFileName { get; init; } = "dotnet.yml";

	/// <summary>Gets the workflow file names to try when a repository has no <see cref="BuildWorkflowFileName"/>.
	/// Leave empty to report no status for repositories that break the convention.</summary>
	public IReadOnlyList<string> FallbackWorkflowFileNames { get; init; } = [];

	/// <summary>Gets how many days of commit history the activity count covers.</summary>
	public int ActivityWindowDays { get; init; } = 30;
}
