// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Metadata;

using KtsuBuild.Metadata;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class LicenseGeneratorTests
{
	private string _tempDir = null!;

	[TestInitialize]
	public void Setup()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), $"LicenseTest_{Guid.NewGuid():N}");
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

	[TestMethod]
	public async Task GenerateAsync_CreatesLicenseFile()
	{
		// Act
		await LicenseGenerator.GenerateAsync(
			serverUrl: "https://github.com/testowner",
			owner: "testowner",
			repository: "testrepo",
			outputPath: _tempDir,
			lineEnding: "\n").ConfigureAwait(false);

		// Assert
		string licensePath = Path.Combine(_tempDir, "LICENSE.md");
		Assert.IsTrue(File.Exists(licensePath), "LICENSE.md should be created");

		string content = await File.ReadAllTextAsync(licensePath).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("MIT License"), "Should contain MIT License header");
		Assert.IsTrue(content.Contains("testowner"), "Should contain owner name");
	}

	[TestMethod]
	public async Task GenerateAsync_CreatesCopyrightFile()
	{
		// Act
		await LicenseGenerator.GenerateAsync(
			serverUrl: "https://github.com/testowner",
			owner: "testowner",
			repository: "testrepo",
			outputPath: _tempDir,
			lineEnding: "\n").ConfigureAwait(false);

		// Assert
		string copyrightPath = Path.Combine(_tempDir, "COPYRIGHT.md");
		Assert.IsTrue(File.Exists(copyrightPath), "COPYRIGHT.md should be created");

		string content = await File.ReadAllTextAsync(copyrightPath).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("Copyright (c)"), "Should contain copyright notice");
		Assert.IsTrue(content.Contains("testowner"), "Should contain owner name");
	}

	[TestMethod]
	public async Task GenerateAsync_IncludesYearRange()
	{
		// Act
		await LicenseGenerator.GenerateAsync(
			serverUrl: "https://github.com/testowner",
			owner: "testowner",
			repository: "testrepo",
			outputPath: _tempDir,
			lineEnding: "\n").ConfigureAwait(false);

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "LICENSE.md")).ConfigureAwait(false);
		int currentYear = DateTime.UtcNow.Year;
		Assert.IsTrue(content.Contains($"2023-{currentYear}"), "Should contain year range from 2023 to current year");
	}

	[TestMethod]
	public async Task GenerateAsync_IncludesProjectUrl()
	{
		// Act
		await LicenseGenerator.GenerateAsync(
			serverUrl: "https://github.com/testowner",
			owner: "testowner",
			repository: "testrepo",
			outputPath: _tempDir,
			lineEnding: "\n").ConfigureAwait(false);

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "LICENSE.md")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("https://github.com/testowner/testrepo"), "Should contain project URL");
	}

	[TestMethod]
	public async Task GenerateAsync_IncludesAuthorInfo()
	{
		// Act
		await LicenseGenerator.GenerateAsync(
			serverUrl: "https://github.com/acme",
			owner: "Acme Corporation",
			repository: "awesome-project",
			outputPath: _tempDir,
			lineEnding: "\n").ConfigureAwait(false);

		// Assert
		string licenseContent = await File.ReadAllTextAsync(Path.Combine(_tempDir, "LICENSE.md")).ConfigureAwait(false);
		string copyrightContent = await File.ReadAllTextAsync(Path.Combine(_tempDir, "COPYRIGHT.md")).ConfigureAwait(false);

		Assert.IsTrue(licenseContent.Contains("Acme Corporation"), "License should contain author");
		Assert.IsTrue(copyrightContent.Contains("Acme Corporation"), "Copyright should contain author");
	}

	[TestMethod]
	public async Task GenerateAsync_UsesCorrectLineEndings_LF()
	{
		// Act
		await LicenseGenerator.GenerateAsync(
			serverUrl: "https://github.com/testowner",
			owner: "testowner",
			repository: "testrepo",
			outputPath: _tempDir,
			lineEnding: "\n").ConfigureAwait(false);

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "LICENSE.md")).ConfigureAwait(false);
		Assert.IsFalse(content.Contains("\r\n"), "Should not contain CRLF when LF specified");
	}

	[TestMethod]
	public async Task GenerateAsync_UsesCorrectLineEndings_CRLF()
	{
		// Act
		await LicenseGenerator.GenerateAsync(
			serverUrl: "https://github.com/testowner",
			owner: "testowner",
			repository: "testrepo",
			outputPath: _tempDir,
			lineEnding: "\r\n").ConfigureAwait(false);

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "LICENSE.md")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("\r\n"), "Should contain CRLF when specified");
	}

	[TestMethod]
	public async Task GenerateAsync_ContainsMitLicenseTerms()
	{
		// Act
		await LicenseGenerator.GenerateAsync(
			serverUrl: "https://github.com/testowner",
			owner: "testowner",
			repository: "testrepo",
			outputPath: _tempDir,
			lineEnding: "\n").ConfigureAwait(false);

		// Assert
		string content = await File.ReadAllTextAsync(Path.Combine(_tempDir, "LICENSE.md")).ConfigureAwait(false);
		Assert.IsTrue(content.Contains("Permission is hereby granted"), "Should contain MIT permission grant");
		Assert.IsTrue(content.Contains("WITHOUT WARRANTY"), "Should contain warranty disclaimer");
		Assert.IsTrue(content.Contains("THE SOFTWARE IS PROVIDED"), "Should contain software provision clause");
	}
}
