// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Utilities;

using KtsuBuild.Tests.Helpers;
using KtsuBuild.Utilities;

// GITHUB_OUTPUT is process-wide state, so the tests that set it cannot run alongside anything
// else that reads it.
[DoNotParallelize]
[TestClass]
public class GitHubActionsOutputTests
{
	private string _tempDir = null!;
	private string _outputFile = null!;

	[TestInitialize]
	public void Setup()
	{
		_tempDir = TestHelpers.CreateTempDir("GitHubActionsOutput");
		_outputFile = Path.Combine(_tempDir, "output.txt");
	}

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	// WriteTo

	[TestMethod]
	public void WriteTo_SingleOutput_WritesKeyValueLine()
	{
		GitHubActionsOutput.WriteTo(_outputFile, [new("version", "1.7.27")]);

		Assert.AreEqual("version=1.7.27\n", File.ReadAllText(_outputFile));
	}

	[TestMethod]
	public void WriteTo_MultipleOutputs_WritesOneLineEachInOrder()
	{
		GitHubActionsOutput.WriteTo(_outputFile,
		[
			new("version", "1.7.27"),
			new("should_release", "false"),
			new("build_skipped", "true"),
		]);

		Assert.AreEqual(
			"version=1.7.27\nshould_release=false\nbuild_skipped=true\n",
			File.ReadAllText(_outputFile));
	}

	[TestMethod]
	public void WriteTo_FileAlreadyHasOutputs_AppendsRatherThanOverwrites()
	{
		File.WriteAllText(_outputFile, "existing=1\n");

		GitHubActionsOutput.WriteTo(_outputFile, [new("version", "1.7.27")]);

		Assert.AreEqual("existing=1\nversion=1.7.27\n", File.ReadAllText(_outputFile));
	}

	[TestMethod]
	public void WriteTo_MultiLineValue_Throws()
	{
		ArgumentException ex = Assert.ThrowsExactly<ArgumentException>(() =>
			GitHubActionsOutput.WriteTo(_outputFile, [new("changelog", "line one\nline two")]));

		StringAssert.Contains(ex.Message, "changelog");
	}

	[TestMethod]
	public void WriteTo_MultiLineValue_LeavesEarlierOutputsUnwritten()
	{
		try
		{
			GitHubActionsOutput.WriteTo(_outputFile,
			[
				new("version", "1.7.27"),
				new("changelog", "line one\nline two"),
			]);
		}
		catch (ArgumentException)
		{
			// The rejection itself is covered above; this test is about the file.
		}

		Assert.IsFalse(File.Exists(_outputFile), "A rejected value must not leave a partial write behind.");
	}

	// Write

	[TestMethod]
	public void Write_OutputVariableSet_WritesToThatFile()
	{
		string? original = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
		Environment.SetEnvironmentVariable("GITHUB_OUTPUT", _outputFile);
		try
		{
			GitHubActionsOutput.Write([new("build_skipped", "true")]);
		}
		finally
		{
			Environment.SetEnvironmentVariable("GITHUB_OUTPUT", original);
		}

		Assert.AreEqual("build_skipped=true\n", File.ReadAllText(_outputFile));
	}

	[TestMethod]
	public void Write_OutputVariableUnset_WritesNothing()
	{
		string? original = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
		Environment.SetEnvironmentVariable("GITHUB_OUTPUT", null);
		try
		{
			GitHubActionsOutput.Write([new("build_skipped", "true")]);
		}
		finally
		{
			Environment.SetEnvironmentVariable("GITHUB_OUTPUT", original);
		}

		Assert.IsFalse(File.Exists(_outputFile));
	}
}
