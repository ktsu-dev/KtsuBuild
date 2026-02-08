// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Winget;

using System.Text;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Generates Winget manifest files.
/// </summary>
public static class ManifestGenerator
{
	private static readonly string[] WindowsArchitectures = ["win-x64", "win-x86", "win-arm64"];

	/// <summary>
	/// Generates all manifest files for a release.
	/// </summary>
	/// <param name="config">The manifest configuration.</param>
	/// <param name="projectInfo">The detected project info.</param>
	/// <param name="sha256Hashes">SHA256 hashes for each architecture.</param>
	/// <param name="outputDirectory">The output directory for manifest files.</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns>The paths to generated manifest files.</returns>
	public static async Task<IReadOnlyList<string>> GenerateAsync(
		ManifestConfig config,
		ProjectInfo projectInfo,
		Dictionary<string, string> sha256Hashes,
		string outputDirectory,
		CancellationToken cancellationToken = default)
	{
		Ensure.NotNull(config);
		Ensure.NotNull(projectInfo);
		Ensure.NotNull(sha256Hashes);
		Ensure.NotNull(outputDirectory);

		Directory.CreateDirectory(outputDirectory);

		List<string> files = [];
		string releaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

		// Version manifest
		string versionPath = Path.Combine(outputDirectory, $"{config.PackageId}.yaml");
		await WriteVersionManifestAsync(versionPath, config, cancellationToken).ConfigureAwait(false);
		files.Add(versionPath);

		// Locale manifest
		string localePath = Path.Combine(outputDirectory, $"{config.PackageId}.locale.en-US.yaml");
		await WriteLocaleManifestAsync(localePath, config, projectInfo, cancellationToken).ConfigureAwait(false);
		files.Add(localePath);

		// Installer manifest
		string installerPath = Path.Combine(outputDirectory, $"{config.PackageId}.installer.yaml");
		await WriteInstallerManifestAsync(installerPath, config, projectInfo, sha256Hashes, releaseDate, cancellationToken).ConfigureAwait(false);
		files.Add(installerPath);

		return files;
	}

	private static async Task WriteVersionManifestAsync(string path, ManifestConfig config, CancellationToken cancellationToken)
	{
		string content = $"""
			# yaml-language-server: $schema=https://aka.ms/winget-manifest.version.1.10.0.schema.json
			PackageIdentifier: {config.PackageId}
			PackageVersion: {config.Version}
			DefaultLocale: en-US
			ManifestType: version
			ManifestVersion: 1.10.0
			""";

		await File.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
	}

	private static async Task WriteLocaleManifestAsync(string path, ManifestConfig config, ProjectInfo projectInfo, CancellationToken cancellationToken)
	{
		StringBuilder sb = new();
		sb.AppendLine("# yaml-language-server: $schema=https://aka.ms/winget-manifest.defaultLocale.1.10.0.schema.json");
		sb.AppendLine($"PackageIdentifier: {config.PackageId}");
		sb.AppendLine($"PackageVersion: {config.Version}");
		sb.AppendLine("PackageLocale: en-US");
		sb.AppendLine($"Publisher: {config.Publisher}");
		sb.AppendLine($"PublisherUrl: https://github.com/{config.Owner}");
		sb.AppendLine($"PublisherSupportUrl: https://github.com/{config.GitHubRepo}/issues");
		sb.AppendLine("# PrivacyUrl:");
		sb.AppendLine($"Author: {config.Publisher}");
		sb.AppendLine($"PackageName: {config.PackageName}");
		sb.AppendLine($"PackageUrl: https://github.com/{config.GitHubRepo}");
		sb.AppendLine("License: MIT");
		sb.AppendLine($"LicenseUrl: https://github.com/{config.GitHubRepo}/blob/main/LICENSE.md");
		sb.AppendLine($"Copyright: Copyright (c) {config.Publisher}");
		sb.AppendLine("# CopyrightUrl:");
		sb.AppendLine($"ShortDescription: {config.ShortDescription}");
		sb.AppendLine($"Description: {projectInfo.Description}");
		sb.AppendLine($"Moniker: {config.CommandAlias}");

		// Tags
		if (projectInfo.Tags.Count > 0)
		{
			sb.AppendLine("Tags:");
			foreach (string tag in projectInfo.Tags.Take(10))
			{
				sb.AppendLine($"- {tag}");
			}
		}

		sb.AppendLine("ReleaseNotes: |-");
		sb.AppendLine($"  See full changelog at: https://github.com/{config.GitHubRepo}/blob/main/CHANGELOG.md");
		sb.AppendLine($"ReleaseNotesUrl: https://github.com/{config.GitHubRepo}/releases/tag/v{config.Version}");
		sb.AppendLine("# PurchaseUrl:");
		sb.AppendLine("# InstallationNotes:");
		sb.AppendLine("Documentations:");
		sb.AppendLine("- DocumentLabel: README");
		sb.AppendLine($"  DocumentUrl: https://github.com/{config.GitHubRepo}/blob/main/README.md");
		sb.AppendLine("ManifestType: defaultLocale");
		sb.AppendLine("ManifestVersion: 1.10.0");

		await File.WriteAllTextAsync(path, sb.ToString(), cancellationToken).ConfigureAwait(false);
	}

	private static async Task WriteInstallerManifestAsync(
		string path,
		ManifestConfig config,
		ProjectInfo projectInfo,
		Dictionary<string, string> sha256Hashes,
		string releaseDate,
		CancellationToken cancellationToken)
	{
		StringBuilder sb = new();
		sb.AppendLine("# yaml-language-server: $schema=https://aka.ms/winget-manifest.installer.1.10.0.schema.json");
		sb.AppendLine($"PackageIdentifier: {config.PackageId}");
		sb.AppendLine($"PackageVersion: {config.Version}");
		sb.AppendLine("Platform:");
		sb.AppendLine("- Windows.Desktop");
		sb.AppendLine("MinimumOSVersion: 10.0.17763.0");
		sb.AppendLine("InstallerType: zip");
		sb.AppendLine("InstallModes:");
		sb.AppendLine("- interactive");
		sb.AppendLine("- silent");
		sb.AppendLine("UpgradeBehavior: install");

		// Commands
		sb.AppendLine("Commands:");
		sb.AppendLine($"- {config.CommandAlias}");
		string exeNameWithoutExt = Path.GetFileNameWithoutExtension(config.ExecutableName);
		if (exeNameWithoutExt != config.CommandAlias)
		{
			sb.AppendLine($"- {exeNameWithoutExt}");
		}

		// File extensions
		if (projectInfo.FileExtensions.Count > 0)
		{
			sb.AppendLine("FileExtensions:");
			foreach (string ext in projectInfo.FileExtensions)
			{
				sb.AppendLine($"- {ext}");
			}
		}

		sb.AppendLine($"ReleaseDate: {releaseDate}");

		// Dependencies
		sb.AppendLine("Dependencies:");
		sb.AppendLine("  PackageDependencies:");
		if (projectInfo.Type == "csharp")
		{
			sb.AppendLine("    - PackageIdentifier: Microsoft.DotNet.DesktopRuntime.9");
		}

		// Installers
		sb.AppendLine("Installers:");
		foreach (string arch in WindowsArchitectures)
		{
			if (!sha256Hashes.TryGetValue(arch, out string? hash))
			{
				continue;
			}

			string fileName = config.ArtifactNamePattern
				.Replace("{name}", config.RepoName)
				.Replace("{version}", config.Version)
				.Replace("{arch}", arch);

			string wingetArch = arch.Replace("win-", "");
			sb.AppendLine($"- Architecture: {wingetArch}");
			sb.AppendLine($"  InstallerUrl: https://github.com/{config.GitHubRepo}/releases/download/v{config.Version}/{fileName}");
			sb.AppendLine($"  InstallerSha256: {hash.ToUpperInvariant()}");
			sb.AppendLine("  NestedInstallerType: portable");
			sb.AppendLine("  NestedInstallerFiles:");
			sb.AppendLine($"  - RelativeFilePath: {config.ExecutableName}");
			sb.AppendLine($"    PortableCommandAlias: {config.CommandAlias}");
		}

		sb.AppendLine("ManifestType: installer");
		sb.AppendLine("ManifestVersion: 1.10.0");

		await File.WriteAllTextAsync(path, sb.ToString(), cancellationToken).ConfigureAwait(false);
	}
}

/// <summary>
/// Configuration for generating Winget manifests.
/// </summary>
public class ManifestConfig
{
	/// <summary>
	/// Gets or sets the package identifier.
	/// </summary>
	public required string PackageId { get; set; }

	/// <summary>
	/// Gets or sets the version.
	/// </summary>
	public required string Version { get; set; }

	/// <summary>
	/// Gets or sets the GitHub repository (owner/repo).
	/// </summary>
	public required string GitHubRepo { get; set; }

	/// <summary>
	/// Gets or sets the repository owner.
	/// </summary>
	public required string Owner { get; set; }

	/// <summary>
	/// Gets or sets the repository name.
	/// </summary>
	public required string RepoName { get; set; }

	/// <summary>
	/// Gets or sets the publisher name.
	/// </summary>
	public required string Publisher { get; set; }

	/// <summary>
	/// Gets or sets the package name.
	/// </summary>
	public required string PackageName { get; set; }

	/// <summary>
	/// Gets or sets the short description.
	/// </summary>
	public required string ShortDescription { get; set; }

	/// <summary>
	/// Gets or sets the artifact name pattern.
	/// </summary>
	public required string ArtifactNamePattern { get; set; }

	/// <summary>
	/// Gets or sets the executable name.
	/// </summary>
	public required string ExecutableName { get; set; }

	/// <summary>
	/// Gets or sets the command alias.
	/// </summary>
	public required string CommandAlias { get; set; }
}
