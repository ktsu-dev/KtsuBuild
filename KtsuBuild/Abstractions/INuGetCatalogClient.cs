// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Abstractions;

/// <summary>
/// Read-only access to the public nuget.org catalog.
/// </summary>
public interface INuGetCatalogClient
{
	/// <summary>
	/// Looks up the published versions and download total for a package.
	/// </summary>
	/// <param name="packageId">The package identifier, such as <c>ktsu.Extensions</c>.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The package information, or <see langword="null"/> when no such package is published.</returns>
	public Task<NuGetPackageInfo?> GetPackageAsync(string packageId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The published state of a NuGet package.
/// </summary>
/// <param name="StableVersion">The newest version without a prerelease label, or <see langword="null"/> when every version is prerelease.</param>
/// <param name="PrereleaseVersion">The newest version with a prerelease label, or <see langword="null"/> when none exists.</param>
/// <param name="TotalDownloads">The download count across every version.</param>
/// <param name="PackageTypes">The declared package types, such as <c>DotnetTool</c>. Empty means the default library type.</param>
public record NuGetPackageInfo(
	string? StableVersion,
	string? PrereleaseVersion,
	long TotalDownloads,
	IReadOnlyList<string> PackageTypes);
