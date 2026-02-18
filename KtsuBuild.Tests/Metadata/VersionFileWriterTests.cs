// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Metadata;

using KtsuBuild.Metadata;
using KtsuBuild.Tests.Helpers;

[TestClass]
public class VersionFileWriterTests
{
	private string _tempDir = null!;

	[TestInitialize]
	public void Setup() => _tempDir = TestHelpers.CreateTempDir("VerWriter");

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	[TestMethod]
	public async Task WriteAsync_CreatesVersionFile()
	{
		await VersionFileWriter.WriteAsync("1.2.3", _tempDir, "\n").ConfigureAwait(false);

		string filePath = Path.Combine(_tempDir, "VERSION.md");
		Assert.IsTrue(File.Exists(filePath), "VERSION.md should be created");
	}

	[TestMethod]
	public async Task WriteAsync_ContainsVersionString()
	{
		await VersionFileWriter.WriteAsync("1.2.3", _tempDir, "\n").ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "VERSION.md")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("1.2.3"), "Should contain version string");
	}

	[TestMethod]
	public async Task WriteAsync_AppendsLineEnding()
	{
		await VersionFileWriter.WriteAsync("1.2.3", _tempDir, "\n").ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "VERSION.md")).ConfigureAwait(false);
		Assert.AreEqual("1.2.3\n", content);
	}

	[TestMethod]
	public async Task WriteAsync_TrimsVersionString()
	{
		await VersionFileWriter.WriteAsync("  1.2.3  ", _tempDir, "\n").ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "VERSION.md")).ConfigureAwait(false);
		Assert.AreEqual("1.2.3\n", content);
	}

	[TestMethod]
	public async Task WriteAsync_UsesCorrectLineEnding_LF()
	{
		await VersionFileWriter.WriteAsync("1.0.0", _tempDir, "\n").ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "VERSION.md")).ConfigureAwait(false);
		Assert.AreEqual("1.0.0\n", content);
		Assert.IsFalse(content.Contains("\r\n"), "Should not contain CRLF");
	}

	[TestMethod]
	public async Task WriteAsync_UsesCorrectLineEnding_CRLF()
	{
		await VersionFileWriter.WriteAsync("1.0.0", _tempDir, "\r\n").ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "VERSION.md")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("\r\n"), "Should contain CRLF");
	}
}
