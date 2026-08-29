// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Profile;

using KtsuBuild.Abstractions;
using KtsuBuild.Profile;
using KtsuBuild.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

[TestClass]
public class ProfileGeneratorTests
{
	private IGitHubApiClient _gitHub = null!;
	private INuGetCatalogClient _nuGet = null!;
	private ProfileGenerator _generator = null!;
	private string _tempDir = null!;

	[TestInitialize]
	public void Setup()
	{
		_gitHub = Substitute.For<IGitHubApiClient>();
		_nuGet = Substitute.For<INuGetCatalogClient>();

		_gitHub.ListOrganizationRepositoriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<GitHubRepository>>([new GitHubRepository("Extensions", "main", false)]));
		_gitHub.ListReleasesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<GitHubRelease>>([new GitHubRelease("v1.6.8")]));
		_gitHub.ListTreePathsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<string>>([]));
		_gitHub.GetFileTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<string?>(new string('x', 512)));
		_nuGet.GetPackageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<NuGetPackageInfo?>(null));

		MockBuildLogger logger = new();
		_generator = new ProfileGenerator(new OrgProfileService(_gitHub, _nuGet, logger), _gitHub, logger);

		_tempDir = Path.Combine(Path.GetTempPath(), $"ProfileGeneratorTest_{Guid.NewGuid():N}");
		Directory.CreateDirectory(_tempDir);
	}

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	private static ProfileOptions Options => new() { Organization = "ktsu-dev" };

	private string WriteTemplate(string content)
	{
		string path = Path.Combine(_tempDir, "README.template");
		File.WriteAllText(path, content);
		return path;
	}

	[TestMethod]
	public async Task GenerateAsync_AppendsTheTableToTheTemplate()
	{
		string template = WriteTemplate("# ktsu.dev\n\n## Project Status\n");
		string output = Path.Combine(_tempDir, "README.md");

		await _generator.GenerateAsync(Options, template, output).ConfigureAwait(false);

		string rendered = await File.ReadAllTextAsync(output).ConfigureAwait(false);
		Assert.StartsWith("# ktsu.dev", rendered);
		Assert.Contains("| Repo | Ships | Stable |", rendered);
		Assert.Contains("[Extensions](https://github.com/ktsu-dev/Extensions)", rendered);
	}

	[TestMethod]
	public async Task GenerateAsync_WritesLineFeedsRegardlessOfPlatform()
	{
		// Git normalizes the committed blob to LF, so writing CRLF on Windows would make the daily
		// commit look like a change every time.
		string template = WriteTemplate("## Project Status\n");
		string output = Path.Combine(_tempDir, "README.md");

		await _generator.GenerateAsync(Options, template, output).ConfigureAwait(false);

		string rendered = await File.ReadAllTextAsync(output).ConfigureAwait(false);
		Assert.IsFalse(rendered.Contains('\r'), "The output should contain no carriage returns");
	}

	[TestMethod]
	public async Task GenerateAsync_NormalizesATemplateThatUsesCarriageReturns()
	{
		string template = WriteTemplate("## Project Status\r\n");
		string output = Path.Combine(_tempDir, "README.md");

		await _generator.GenerateAsync(Options, template, output).ConfigureAwait(false);

		Assert.IsFalse((await File.ReadAllTextAsync(output).ConfigureAwait(false)).Contains('\r'));
	}

	[TestMethod]
	public async Task GenerateAsync_WritesWithoutAByteOrderMark()
	{
		string template = WriteTemplate("## Project Status\n");
		string output = Path.Combine(_tempDir, "README.md");

		await _generator.GenerateAsync(Options, template, output).ConfigureAwait(false);

		byte[] bytes = await File.ReadAllBytesAsync(output).ConfigureAwait(false);
		Assert.IsFalse(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
	}

	[TestMethod]
	public async Task GenerateAsync_CreatesTheOutputDirectory()
	{
		string template = WriteTemplate("## Project Status\n");
		string output = Path.Combine(_tempDir, "nested", "deeper", "README.md");

		await _generator.GenerateAsync(Options, template, output).ConfigureAwait(false);

		Assert.IsTrue(File.Exists(output));
	}

	[TestMethod]
	public async Task GenerateAsync_ReturnsTheListedRepositories()
	{
		string template = WriteTemplate("## Project Status\n");

		IReadOnlyList<RepoFacts> facts = await _generator
			.GenerateAsync(Options, template, Path.Combine(_tempDir, "README.md"))
			.ConfigureAwait(false);

		Assert.HasCount(1, facts);
		Assert.AreEqual("Extensions", facts[0].Name);
	}

	[TestMethod]
	public async Task GenerateAsync_WithAMissingTemplate_ThrowsWithoutWritingAnything()
	{
		// Writing a README without the template would silently discard everything the profile page
		// says about the organization.
		string output = Path.Combine(_tempDir, "README.md");

		await Assert.ThrowsExactlyAsync<FileNotFoundException>(
			() => _generator.GenerateAsync(Options, Path.Combine(_tempDir, "absent.template"), output))
			.ConfigureAwait(false);

		Assert.IsFalse(File.Exists(output));
	}

	[TestMethod]
	public async Task GenerateAsync_OverwritesAnExistingReadme()
	{
		string template = WriteTemplate("## Project Status\n");
		string output = Path.Combine(_tempDir, "README.md");
		await File.WriteAllTextAsync(output, "stale content that should not survive").ConfigureAwait(false);

		await _generator.GenerateAsync(Options, template, output).ConfigureAwait(false);

		Assert.IsFalse((await File.ReadAllTextAsync(output).ConfigureAwait(false)).Contains("stale", StringComparison.Ordinal));
	}

	[TestMethod]
	public async Task GenerateAsync_WithNoQualifyingRepositories_StillWritesTheTemplate()
	{
		_gitHub.ListReleasesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<GitHubRelease>>([]));
		string template = WriteTemplate("## Project Status\n");
		string output = Path.Combine(_tempDir, "README.md");

		await _generator.GenerateAsync(Options, template, output).ConfigureAwait(false);

		Assert.AreEqual("## Project Status\n\n", await File.ReadAllTextAsync(output).ConfigureAwait(false));
	}

	[TestMethod]
	public async Task GenerateAsync_WithAnArchivedLinkInTheTemplate_ThrowsWithoutWritingAnything()
	{
		// Publishing a page that promotes retired work is worse than failing loudly.
		_gitHub.ListOrganizationRepositoriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<GitHubRepository>>([
				new GitHubRepository("Extensions", "main", false),
				new GitHubRepository("PersistenceProvider", "main", true),
			]));
		string template = WriteTemplate("- [PersistenceProvider](https://github.com/ktsu-dev/PersistenceProvider)\n## Project Status\n");
		string output = Path.Combine(_tempDir, "README.md");

		InvalidOperationException error = await Assert
			.ThrowsExactlyAsync<InvalidOperationException>(() => _generator.GenerateAsync(Options, template, output))
			.ConfigureAwait(false);

		Assert.Contains("PersistenceProvider", error.Message);
		Assert.IsFalse(File.Exists(output), "Nothing should be written when the template is unhealthy");
	}

	[TestMethod]
	public async Task GenerateAsync_WithHealthyLinks_Writes()
	{
		string template = WriteTemplate("- [Extensions](https://github.com/ktsu-dev/Extensions)\n## Project Status\n");
		string output = Path.Combine(_tempDir, "README.md");

		await _generator.GenerateAsync(Options, template, output).ConfigureAwait(false);

		Assert.IsTrue(File.Exists(output));
	}

	[TestMethod]
	public async Task GenerateAsync_WithAnEmptyListing_WritesRatherThanBlockingOnAFailedLookup()
	{
		// A listing that could not be read is a transient failure, not evidence of a bad template.
		_gitHub.ListOrganizationRepositoriesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult<IReadOnlyList<GitHubRepository>>([]));
		string template = WriteTemplate("- [Whatever](https://github.com/ktsu-dev/Whatever)\n## Project Status\n");
		string output = Path.Combine(_tempDir, "README.md");

		await _generator.GenerateAsync(Options, template, output).ConfigureAwait(false);

		Assert.IsTrue(File.Exists(output));
	}
}
