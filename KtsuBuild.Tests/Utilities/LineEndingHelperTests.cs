// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Utilities;

using KtsuBuild.Abstractions;
using KtsuBuild.Tests.Helpers;
using KtsuBuild.Utilities;
using NSubstitute;

[TestClass]
public class LineEndingHelperTests
{
	private IProcessRunner _processRunner = null!;
	private LineEndingHelper _helper = null!;
	private string _tempDir = null!;

	[TestInitialize]
	public void Setup()
	{
		_processRunner = Substitute.For<IProcessRunner>();
		_helper = new LineEndingHelper(_processRunner);
		_tempDir = TestHelpers.CreateTempDir("LineEnding");
	}

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	// GetLineEndingAsync

	[TestMethod]
	public async Task GetLineEndingAsync_CoreEolLf_ReturnsLf()
	{
		_processRunner.RunAsync("git", "config --get core.eol", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("lf\n"));

		string result = await _helper.GetLineEndingAsync("/repo").ConfigureAwait(false);

		Assert.AreEqual("\n", result);
	}

	[TestMethod]
	public async Task GetLineEndingAsync_CoreEolCrlf_ReturnsCrlf()
	{
		_processRunner.RunAsync("git", "config --get core.eol", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("crlf\n"));

		string result = await _helper.GetLineEndingAsync("/repo").ConfigureAwait(false);

		Assert.AreEqual("\r\n", result);
	}

	[TestMethod]
	public async Task GetLineEndingAsync_CoreEolNative_ReturnsEnvironmentNewLine()
	{
		_processRunner.RunAsync("git", "config --get core.eol", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("native\n"));

		string result = await _helper.GetLineEndingAsync("/repo").ConfigureAwait(false);

		Assert.AreEqual(Environment.NewLine, result);
	}

	[TestMethod]
	public async Task GetLineEndingAsync_NoEol_AutocrlfTrue_ReturnsLf()
	{
		_processRunner.RunAsync("git", "config --get core.eol", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult());
		_processRunner.RunAsync("git", "config --get core.autocrlf", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("true\n"));

		string result = await _helper.GetLineEndingAsync("/repo").ConfigureAwait(false);

		Assert.AreEqual("\n", result);
	}

	[TestMethod]
	public async Task GetLineEndingAsync_NoEol_AutocrlfInput_ReturnsLf()
	{
		_processRunner.RunAsync("git", "config --get core.eol", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult());
		_processRunner.RunAsync("git", "config --get core.autocrlf", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("input\n"));

		string result = await _helper.GetLineEndingAsync("/repo").ConfigureAwait(false);

		Assert.AreEqual("\n", result);
	}

	[TestMethod]
	public async Task GetLineEndingAsync_NoEol_AutocrlfFalse_ReturnsEnvironmentNewLine()
	{
		_processRunner.RunAsync("git", "config --get core.eol", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult());
		_processRunner.RunAsync("git", "config --get core.autocrlf", Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.SuccessResult("false\n"));

		string result = await _helper.GetLineEndingAsync("/repo").ConfigureAwait(false);

		Assert.AreEqual(Environment.NewLine, result);
	}

	[TestMethod]
	public async Task GetLineEndingAsync_NeitherConfigured_ReturnsEnvironmentNewLine()
	{
		_processRunner.RunAsync("git", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(TestHelpers.FailureResult());

		string result = await _helper.GetLineEndingAsync("/repo").ConfigureAwait(false);

		Assert.AreEqual(Environment.NewLine, result);
	}

	// NormalizeLineEndings

	[TestMethod]
	public void NormalizeLineEndings_CrlfToLf_NormalizedCorrectly()
	{
		string input = "line1\r\nline2\r\nline3";
		string result = LineEndingHelper.NormalizeLineEndings(input, "\n");
		Assert.AreEqual("line1\nline2\nline3", result);
	}

	[TestMethod]
	public void NormalizeLineEndings_LfToCrlf_NormalizedCorrectly()
	{
		string input = "line1\nline2\nline3";
		string result = LineEndingHelper.NormalizeLineEndings(input, "\r\n");
		Assert.AreEqual("line1\r\nline2\r\nline3", result);
	}

	[TestMethod]
	public void NormalizeLineEndings_MixedToLf_NormalizedCorrectly()
	{
		string input = "line1\r\nline2\nline3\rline4";
		string result = LineEndingHelper.NormalizeLineEndings(input, "\n");
		Assert.AreEqual("line1\nline2\nline3\nline4", result);
	}

	[TestMethod]
	public void NormalizeLineEndings_AlreadyCorrect_Unchanged()
	{
		string input = "line1\nline2\nline3";
		string result = LineEndingHelper.NormalizeLineEndings(input, "\n");
		Assert.AreEqual("line1\nline2\nline3", result);
	}

	// WriteFileAsync

	[TestMethod]
	public async Task WriteFileAsync_CreatesFile()
	{
		string filePath = Path.Combine(_tempDir, "output.txt");

		await LineEndingHelper.WriteFileAsync(filePath, "hello\nworld", "\n").ConfigureAwait(false);

		Assert.IsTrue(File.Exists(filePath));
	}

	[TestMethod]
	public async Task WriteFileAsync_WritesUtf8WithoutBom()
	{
		string filePath = Path.Combine(_tempDir, "output.txt");

		await LineEndingHelper.WriteFileAsync(filePath, "hello", "\n").ConfigureAwait(false);

		byte[] bytes = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
		// UTF-8 BOM is 0xEF, 0xBB, 0xBF - verify it's NOT present
		Assert.IsFalse(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "File should not have BOM");
	}

	[TestMethod]
	public async Task WriteFileAsync_NormalizesLineEndings()
	{
		string filePath = Path.Combine(_tempDir, "output.txt");

		await LineEndingHelper.WriteFileAsync(filePath, "line1\r\nline2\rline3", "\n").ConfigureAwait(false);

		string content = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
		Assert.IsFalse(content.Contains("\r\n"), "Should not contain CRLF");
		Assert.IsFalse(content.Contains('\r'), "Should not contain standalone CR");
		Assert.IsTrue(content.Contains('\n'), "Should contain LF");
	}
}
