// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Winget;

using KtsuBuild.Tests.Helpers;
using KtsuBuild.Winget;

[TestClass]
public class ManifestGeneratorTests
{
	private string _tempDir = null!;
	private ManifestConfig _config = null!;
	private ProjectInfo _projectInfo = null!;
	private Dictionary<string, string> _hashes = null!;

	[TestInitialize]
	public void Setup()
	{
		_tempDir = TestHelpers.CreateTempDir("ManifestGen");
		_config = new ManifestConfig
		{
			PackageId = "ktsu-dev.MyApp",
			Version = "1.2.3",
			GitHubRepo = "ktsu-dev/MyApp",
			Owner = "ktsu-dev",
			RepoName = "MyApp",
			Publisher = "ktsu.dev",
			PackageName = "MyApp",
			ShortDescription = "A test application",
			ArtifactNamePattern = "{name}-{version}-{arch}.zip",
			ExecutableName = "MyApp.exe",
			CommandAlias = "myapp",
		};
		_projectInfo = new ProjectInfo
		{
			Name = "MyApp",
			Type = "csharp",
			ExecutableName = "MyApp.exe",
			CommandAlias = "myapp",
			Description = "A detailed description of the application",
			ShortDescription = "A test application",
			Publisher = "ktsu.dev",
		};
		_projectInfo.Tags.AddRange(["dotnet", "csharp", "tool"]);
		_hashes = new Dictionary<string, string>
		{
			["win-x64"] = "ABCD1234ABCD1234ABCD1234ABCD1234ABCD1234ABCD1234ABCD1234ABCD1234",
			["win-x86"] = "EFGH5678EFGH5678EFGH5678EFGH5678EFGH5678EFGH5678EFGH5678EFGH5678",
			["win-arm64"] = "IJKL9012IJKL9012IJKL9012IJKL9012IJKL9012IJKL9012IJKL9012IJKL9012",
		};
	}

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	[TestMethod]
	public async Task GenerateAsync_CreatesThreeManifestFiles()
	{
		IReadOnlyList<string> files = await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		Assert.AreEqual(3, files.Count);
		foreach (string file in files)
		{
			Assert.IsTrue(File.Exists(file), $"File should exist: {file}");
		}
	}

	// Version manifest tests

	[TestMethod]
	public async Task GenerateAsync_VersionManifest_ContainsPackageIdentifier()
	{
		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("PackageIdentifier: ktsu-dev.MyApp"));
	}

	[TestMethod]
	public async Task GenerateAsync_VersionManifest_ContainsVersion()
	{
		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("PackageVersion: 1.2.3"));
	}

	[TestMethod]
	public async Task GenerateAsync_VersionManifest_ContainsManifestType()
	{
		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("ManifestType: version"));
	}

	// Locale manifest tests

	[TestMethod]
	public async Task GenerateAsync_LocaleManifest_ContainsPublisher()
	{
		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.locale.en-US.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("Publisher: ktsu.dev"));
	}

	[TestMethod]
	public async Task GenerateAsync_LocaleManifest_ContainsPackageName()
	{
		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.locale.en-US.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("PackageName: MyApp"));
	}

	[TestMethod]
	public async Task GenerateAsync_LocaleManifest_ContainsShortDescription()
	{
		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.locale.en-US.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("ShortDescription: A test application"));
	}

	[TestMethod]
	public async Task GenerateAsync_LocaleManifest_ContainsTags()
	{
		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.locale.en-US.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("Tags:"));
		Assert.IsTrue(content.Contains("- dotnet"));
		Assert.IsTrue(content.Contains("- csharp"));
		Assert.IsTrue(content.Contains("- tool"));
	}

	[TestMethod]
	public async Task GenerateAsync_LocaleManifest_LimitsTenTags()
	{
		_projectInfo.Tags.Clear();
		for (int i = 0; i < 15; i++)
		{
			_projectInfo.Tags.Add($"tag{i}");
		}

		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.locale.en-US.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("- tag9"), "Should include tag9 (10th tag)");
		Assert.IsFalse(content.Contains("- tag10"), "Should not include tag10 (11th tag)");
	}

	[TestMethod]
	public async Task GenerateAsync_LocaleManifest_ContainsLicenseUrl()
	{
		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.locale.en-US.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("LicenseUrl: https://github.com/ktsu-dev/MyApp/blob/main/LICENSE.md"));
	}

	[TestMethod]
	public async Task GenerateAsync_LocaleManifest_ContainsDocumentationUrl()
	{
		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.locale.en-US.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("DocumentUrl: https://github.com/ktsu-dev/MyApp/blob/main/README.md"));
	}

	// Installer manifest tests

	[TestMethod]
	public async Task GenerateAsync_InstallerManifest_ContainsInstallers()
	{
		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.installer.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("Installers:"));
	}

	[TestMethod]
	public async Task GenerateAsync_InstallerManifest_IncludesAllArchitectures()
	{
		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.installer.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("Architecture: x64"));
		Assert.IsTrue(content.Contains("Architecture: x86"));
		Assert.IsTrue(content.Contains("Architecture: arm64"));
	}

	[TestMethod]
	public async Task GenerateAsync_InstallerManifest_ContainsSha256Hashes()
	{
		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.installer.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("InstallerSha256: ABCD1234ABCD1234ABCD1234ABCD1234ABCD1234ABCD1234ABCD1234ABCD1234"));
	}

	[TestMethod]
	public async Task GenerateAsync_InstallerManifest_ContainsInstallerUrl()
	{
		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.installer.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("InstallerUrl: https://github.com/ktsu-dev/MyApp/releases/download/v1.2.3/MyApp-1.2.3-win-x64.zip"));
	}

	[TestMethod]
	public async Task GenerateAsync_InstallerManifest_ContainsCommandAlias()
	{
		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.installer.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("PortableCommandAlias: myapp"));
	}

	[TestMethod]
	public async Task GenerateAsync_InstallerManifest_CSharpProject_IncludesDotNetDependency()
	{
		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.installer.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("Microsoft.DotNet.DesktopRuntime"));
	}

	[TestMethod]
	public async Task GenerateAsync_InstallerManifest_MissingArchitecture_SkipsEntry()
	{
		// Only provide hashes for x64
		Dictionary<string, string> singleHash = new()
		{
			["win-x64"] = "ABCD1234ABCD1234ABCD1234ABCD1234ABCD1234ABCD1234ABCD1234ABCD1234",
		};

		await ManifestGenerator.GenerateAsync(_config, _projectInfo, singleHash, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.installer.yaml")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("Architecture: x64"));
		Assert.IsFalse(content.Contains("Architecture: x86"), "Should not include x86 when no hash provided");
		Assert.IsFalse(content.Contains("Architecture: arm64"), "Should not include arm64 when no hash provided");
	}

	[TestMethod]
	public async Task GenerateAsync_FileNaming_UsesPackageIdPrefix()
	{
		IReadOnlyList<string> files = await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		Assert.IsTrue(files.Any(f => f.EndsWith("ktsu-dev.MyApp.yaml", StringComparison.Ordinal)));
		Assert.IsTrue(files.Any(f => f.EndsWith("ktsu-dev.MyApp.locale.en-US.yaml", StringComparison.Ordinal)));
		Assert.IsTrue(files.Any(f => f.EndsWith("ktsu-dev.MyApp.installer.yaml", StringComparison.Ordinal)));
	}

	[TestMethod]
	public async Task GenerateAsync_InstallerManifest_ContainsReleaseDate()
	{
		await ManifestGenerator.GenerateAsync(_config, _projectInfo, _hashes, _tempDir).ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "ktsu-dev.MyApp.installer.yaml")).ConfigureAwait(false);
		// ReleaseDate should be today's UTC date
		string expectedDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
		Assert.IsTrue(content.Contains($"ReleaseDate: {expectedDate}"));
	}
}
