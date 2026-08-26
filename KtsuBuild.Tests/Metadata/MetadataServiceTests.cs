// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Metadata;

using KtsuBuild.Abstractions;
using KtsuBuild.Configuration;
using KtsuBuild.Git;
using KtsuBuild.Metadata;
using KtsuBuild.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class MetadataServiceTests
{
	private string _tempDir = null!;
	private MetadataService _service = null!;

	[TestInitialize]
	public void Setup()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), $"MetadataTest_{Guid.NewGuid():N}");
		Directory.CreateDirectory(_tempDir);

		MockBuildLogger logger = new();
		MockGitService gitService = new();
		_service = new MetadataService(gitService, logger);
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
	public async Task WriteAuthorsFileAsync_CreatesAuthorsFile()
	{
		// Arrange
		List<string> authors = ["Alice", "Bob"];

		// Act
		await _service.WriteAuthorsFileAsync(authors, _tempDir, "\n").ConfigureAwait(false);

		// Assert
		string filePath = Path.Combine(_tempDir, "AUTHORS.md");
		Assert.IsTrue(File.Exists(filePath), "AUTHORS.md should be created");
	}

	[TestMethod]
	public async Task WriteAuthorsFileAsync_ContainsHeader()
	{
		// Arrange
		List<string> authors = ["Alice"];

		// Act
		await _service.WriteAuthorsFileAsync(authors, _tempDir, "\n").ConfigureAwait(false);

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "AUTHORS.md")).ConfigureAwait(false);
		Assert.IsTrue(content.StartsWith("# Project Authors", StringComparison.Ordinal), "Should start with header");
	}

	[TestMethod]
	public async Task WriteAuthorsFileAsync_ContainsAllAuthors()
	{
		// Arrange
		List<string> authors = ["Alice", "Bob", "Charlie"];

		// Act
		await _service.WriteAuthorsFileAsync(authors, _tempDir, "\n").ConfigureAwait(false);

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "AUTHORS.md")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("* Alice"), "Should contain first author");
		Assert.IsTrue(content.Contains("* Bob"), "Should contain second author");
		Assert.IsTrue(content.Contains("* Charlie"), "Should contain third author");
	}

	[TestMethod]
	public async Task WriteAuthorsFileAsync_MatchesPSBuildFormat()
	{
		// Arrange - PSBuild format: "# Project Authors\n\n* Author1\n* Author2\n"
		List<string> authors = ["Alice", "Bob"];

		// Act
		await _service.WriteAuthorsFileAsync(authors, _tempDir, "\n").ConfigureAwait(false);

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "AUTHORS.md")).ConfigureAwait(false);
		string expected = "# Project Authors\n\n* Alice\n* Bob\n";
		Assert.AreEqual(expected, content, "Should match PSBuild.psm1 format exactly");
	}

	[TestMethod]
	public async Task WriteAuthorsFileAsync_UsesCorrectLineEndings_CRLF()
	{
		// Arrange
		List<string> authors = ["Alice", "Bob"];

		// Act
		await _service.WriteAuthorsFileAsync(authors, _tempDir, "\r\n").ConfigureAwait(false);

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "AUTHORS.md")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("\r\n"), "Should contain CRLF line endings");
		Assert.IsTrue(content.Contains("* Alice\r\n"), "Author lines should use CRLF");
	}

	// WriteVersionFileAsync

	[TestMethod]
	public async Task WriteVersionFileAsync_CreatesVersionMd()
	{
		await _service.WriteVersionFileAsync("2.0.0", _tempDir, "\n").ConfigureAwait(false);

		string filePath = Path.Combine(_tempDir, "VERSION.md");
		Assert.IsTrue(File.Exists(filePath), "VERSION.md should be created");
	}

	[TestMethod]
	public async Task WriteVersionFileAsync_ContainsVersionString()
	{
		await _service.WriteVersionFileAsync("2.0.0", _tempDir, "\n").ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "VERSION.md")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("2.0.0"), "Should contain version string");
	}

	// WriteLicenseFilesAsync

	[TestMethod]
	public async Task WriteLicenseFilesAsync_CreatesLicenseAndCopyright()
	{
		await _service.WriteLicenseFilesAsync("https://github.com", "testowner", "testowner/testrepo", _tempDir, "\n").ConfigureAwait(false);

		Assert.IsTrue(File.Exists(Path.Combine(_tempDir, "LICENSE.md")), "LICENSE.md should be created");
		Assert.IsTrue(File.Exists(Path.Combine(_tempDir, "COPYRIGHT.md")), "COPYRIGHT.md should be created");
	}

	// WriteUrlFilesAsync

	[TestMethod]
	public async Task WriteUrlFilesAsync_CreatesAuthorsUrl()
	{
		await _service.WriteUrlFilesAsync("https://github.com", "testowner", "testowner/testrepo", _tempDir, "\n").ConfigureAwait(false);

		string filePath = Path.Combine(_tempDir, "AUTHORS.url");
		Assert.IsTrue(File.Exists(filePath), "AUTHORS.url should be created");
	}

	[TestMethod]
	public async Task WriteUrlFilesAsync_CreatesProjectUrl()
	{
		await _service.WriteUrlFilesAsync("https://github.com", "testowner", "testowner/testrepo", _tempDir, "\n").ConfigureAwait(false);

		string filePath = Path.Combine(_tempDir, "PROJECT_URL.url");
		Assert.IsTrue(File.Exists(filePath), "PROJECT_URL.url should be created");
	}

	[TestMethod]
	public async Task WriteUrlFilesAsync_AuthorsUrlContainsOwnerUrl()
	{
		await _service.WriteUrlFilesAsync("https://github.com", "testowner", "testowner/testrepo", _tempDir, "\n").ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "AUTHORS.url")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("https://github.com/testowner"), "AUTHORS.url should contain owner URL");
	}

	[TestMethod]
	public async Task WriteUrlFilesAsync_ProjectUrlContainsRepoUrl()
	{
		await _service.WriteUrlFilesAsync("https://github.com", "testowner", "testowner/testrepo", _tempDir, "\n").ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "PROJECT_URL.url")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("https://github.com/testowner/testrepo"), "PROJECT_URL.url should contain repo URL");
	}

	// UpdateAllAsync license regeneration

	private static MetadataUpdateOptions ForkOptions(string workspacePath, bool isOfficial) => new()
	{
		BuildConfiguration = new BuildConfiguration
		{
			WorkspacePath = workspacePath,
			ServerUrl = "https://github.com",
			GitHubOwner = isOfficial ? "ktsu-dev" : "forkowner",
			GitHubRepo = isOfficial ? "ktsu-dev/testrepo" : "forkowner/testrepo",
			ExpectedOwner = "ktsu-dev",
			IsOfficial = isOfficial,
		},
		CommitChanges = false,
	};

	/// <summary>
	/// A fork is already forbidden from committing metadata, but it was still rewriting COPYRIGHT.md
	/// in the working tree, renaming the copyright holder to the fork owner. The SDK derives the
	/// required file header from that file, so every source file in the repository then failed
	/// IDE0073 against a header naming an owner the fork had no business asserting.
	/// </summary>
	[TestMethod]
	public async Task UpdateAllAsync_LeavesLicenseFilesAloneWhenNotOfficial()
	{
		string copyrightPath = Path.Combine(_tempDir, "COPYRIGHT.md");
		await File.WriteAllTextAsync(copyrightPath, "Copyright (c) 2023-2026 ktsu-dev contributors").ConfigureAwait(false);

		MetadataUpdateResult result = await _service.UpdateAllAsync(ForkOptions(_tempDir, isOfficial: false)).ConfigureAwait(false);

		Assert.IsTrue(result.Success, result.Error);
		string copyright = await File.ReadAllTextAsync(copyrightPath).ConfigureAwait(false);
		StringAssert.Contains(copyright, "ktsu-dev contributors", "A fork must not rewrite the copyright holder.");
	}

	[TestMethod]
	public async Task UpdateAllAsync_WritesLicenseFilesWhenOfficial()
	{
		MetadataUpdateResult result = await _service.UpdateAllAsync(ForkOptions(_tempDir, isOfficial: true)).ConfigureAwait(false);

		Assert.IsTrue(result.Success, result.Error);
		string copyright = await File.ReadAllTextAsync(Path.Combine(_tempDir, "COPYRIGHT.md")).ConfigureAwait(false);
		StringAssert.Contains(copyright, "ktsu-dev contributors", "An official build still regenerates the copyright.");
	}

	/// <summary>
	/// Minimal mock of IGitService for MetadataService construction.
	/// </summary>
	private sealed class MockGitService : IGitService
	{
		public Task<IReadOnlyList<string>> GetTagsAsync(string workingDirectory, CancellationToken cancellationToken = default)
			=> Task.FromResult<IReadOnlyList<string>>([]);

		public Task<string> GetCurrentCommitHashAsync(string workingDirectory, CancellationToken cancellationToken = default)
			=> Task.FromResult("abc123");

		public Task<string?> GetTagCommitHashAsync(string workingDirectory, string tag, CancellationToken cancellationToken = default)
			=> Task.FromResult<string?>("abc123");

		public Task<string?> GetRemoteUrlAsync(string workingDirectory, string remoteName = "origin", CancellationToken cancellationToken = default)
			=> Task.FromResult<string?>("https://github.com/test/test");

		public Task<string> GetLineEndingAsync(string workingDirectory, CancellationToken cancellationToken = default)
			=> Task.FromResult("\n");

		public Task<IReadOnlyList<string>> GetCommitMessagesAsync(string workingDirectory, string range, CancellationToken cancellationToken = default)
			=> Task.FromResult<IReadOnlyList<string>>([]);

		public Task<IReadOnlyList<CommitInfo>> GetCommitsAsync(string workingDirectory, string range, CancellationToken cancellationToken = default)
			=> Task.FromResult<IReadOnlyList<CommitInfo>>([]);

		public Task<string> GetDiffAsync(string workingDirectory, string range, string? pathSpec = null, CancellationToken cancellationToken = default)
			=> Task.FromResult(string.Empty);

		public Task<bool> IsCommitTaggedAsync(string workingDirectory, string commitHash, CancellationToken cancellationToken = default)
			=> Task.FromResult(false);

		public Task<string> GetFirstCommitAsync(string workingDirectory, CancellationToken cancellationToken = default)
			=> Task.FromResult("abc123");

		public Task CreateAndPushTagAsync(string workingDirectory, string tagName, string commitHash, string message, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;

		public Task StageFilesAsync(string workingDirectory, IEnumerable<string> files, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;

		public Task<string> CommitAsync(string workingDirectory, string message, CancellationToken cancellationToken = default)
			=> Task.FromResult("abc123");

		public Task PushAsync(string workingDirectory, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;

		public Task<bool> HasUncommittedChangesAsync(string workingDirectory, CancellationToken cancellationToken = default)
			=> Task.FromResult(false);

		public Task<bool> HasStagedChangesAsync(string workingDirectory, CancellationToken cancellationToken = default)
			=> Task.FromResult(false);

		public Task SetIdentityAsync(string workingDirectory, string name, string email, CancellationToken cancellationToken = default)
			=> Task.CompletedTask;

		public Task<bool> EnsureIdentityAsync(string workingDirectory, string fallbackName, string fallbackEmail, CancellationToken cancellationToken = default)
			=> Task.FromResult(false);
	}
}
