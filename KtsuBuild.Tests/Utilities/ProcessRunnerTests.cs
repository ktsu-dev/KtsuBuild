// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Utilities;

using KtsuBuild.Tests.Helpers;
using KtsuBuild.Utilities;

/// <summary>
/// Tests for <see cref="ProcessRunner"/>, which run real child processes.
/// </summary>
/// <remarks>
/// The scripts go through a shell so that one test body works on both CI legs. Everything here uses
/// the list overloads where the argument vector is the point, so the splitter is not silently under
/// test as well; <see cref="CommandLineArgumentsTests"/> covers that separately.
/// </remarks>
[TestClass]
public class ProcessRunnerTests
{
	private ProcessRunner _runner = null!;
	private string _tempDir = null!;

	[TestInitialize]
	public void Setup()
	{
		_runner = new ProcessRunner();
		_tempDir = TestHelpers.CreateTempDir("ProcessRunner");
	}

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	/// <summary>Gets the shell and the arguments that run <paramref name="script"/> through it.</summary>
	private static (string FileName, IReadOnlyList<string> Arguments) Shell(string script) =>
		OperatingSystem.IsWindows()
			? ("cmd.exe", ["/c", script])
			: ("/bin/sh", ["-c", script]);

	[TestMethod]
	public async Task RunAsync_CapturesOutputErrorAndExitCode()
	{
		string script = OperatingSystem.IsWindows()
			? "echo one& echo two& echo oops 1>&2& exit /b 3"
			: "echo one; echo two; echo oops 1>&2; exit 3";

		(string fileName, IReadOnlyList<string> arguments) = Shell(script);
		ProcessResult result = await _runner.RunAsync(fileName, arguments, _tempDir).ConfigureAwait(false);

		string[] expectedOutput = ["one", "two"];
		string[] expectedError = ["oops"];

		Assert.AreEqual(3, result.ExitCode);
		Assert.IsFalse(result.Success);
		CollectionAssert.AreEqual(expectedOutput, SplitLines(result.StandardOutput));
		CollectionAssert.AreEqual(expectedError, SplitLines(result.StandardError));
	}

	[TestMethod]
	public async Task RunWithCallbackAsync_DeliversOneWholeLinePerCall()
	{
		// LineOutputHandler rather than the raw OutputHandler: callers have always been handed whole
		// lines, and a chunked handler would split them wherever the pipe happened to be read.
		string script = OperatingSystem.IsWindows()
			? "echo alpha& echo beta& echo gamma 1>&2"
			: "echo alpha; echo beta; echo gamma 1>&2";

		List<string> output = [];
		List<string> error = [];
		(string fileName, IReadOnlyList<string> arguments) = Shell(script);

		int exitCode = await _runner.RunWithCallbackAsync(
			fileName, arguments, _tempDir, output.Add, error.Add).ConfigureAwait(false);

		string[] expectedOutput = ["alpha", "beta"];
		string[] expectedError = ["gamma"];

		Assert.AreEqual(0, exitCode);
		CollectionAssert.AreEqual(expectedOutput, Trimmed(output));
		CollectionAssert.AreEqual(expectedError, Trimmed(error));
	}

	/// <summary>
	/// Gets a command that writes <c>content</c> to stdout with no trailing newline.
	/// </summary>
	/// <remarks>
	/// <c>printf</c> on Unix. On Windows the <c>cmd</c> idiom for this is <c>&lt;nul set /p</c>, whose
	/// quoting is delicate enough to be its own source of failure, so PowerShell writes the bytes
	/// directly instead — both runners have it, and there is nothing to quote.
	/// </remarks>
	private static (string FileName, IReadOnlyList<string> Arguments) UnterminatedWrite() =>
		OperatingSystem.IsWindows()
			? ("powershell.exe", ["-NoProfile", "-Command", "[Console]::Out.Write('content')"])
			: ("/bin/sh", ["-c", "printf 'content'"]);

	[TestMethod]
	public async Task RunWithCallbackAsync_FinalLineWithoutANewline_IsStillDelivered()
	{
		// RunCommand's own LineOutputHandler drops this; the caller would see no output at all for a
		// process whose last line is unterminated. Assembling the lines here is what keeps it.
		List<string> output = [];
		(string fileName, IReadOnlyList<string> arguments) = UnterminatedWrite();

		int exitCode = await _runner.RunWithCallbackAsync(
			fileName, arguments, _tempDir, output.Add).ConfigureAwait(false);

		string[] expected = ["content"];
		Assert.AreEqual(0, exitCode);
		CollectionAssert.AreEqual(expected, Trimmed(output));
	}

	[TestMethod]
	public async Task RunAsync_FinalLineWithoutANewline_IsStillCaptured()
	{
		(string fileName, IReadOnlyList<string> arguments) = UnterminatedWrite();
		ProcessResult result = await _runner.RunAsync(fileName, arguments, _tempDir).ConfigureAwait(false);

		Assert.AreEqual(0, result.ExitCode);
		Assert.AreEqual("content", result.StandardOutput.Trim());
	}

	/// <summary>
	/// Asserts the process reported a working directory that is the same directory as
	/// <paramref name="expected"/>, whatever route either path took to get there.
	/// </summary>
	/// <remarks>
	/// Comparing the strings does not work: on macOS the temp directory is reached through
	/// <c>/var</c>, a symlink to <c>/private/var</c>, and the child reports the resolved form while
	/// the test holds the unresolved one. A sentinel file settles which directory it actually is
	/// without either side having to resolve anything.
	/// </remarks>
	private static void AssertSameDirectory(string expected, string reported)
	{
		string sentinel = $"sentinel-{Guid.NewGuid():N}";
		string path = Path.Combine(expected, sentinel);
		File.WriteAllText(path, "present");

		try
		{
			Assert.IsTrue(
				File.Exists(Path.Combine(reported.Trim(), sentinel)),
				$"Expected the process to run in '{expected}', but it reported '{reported.Trim()}'.");
		}
		finally
		{
			File.Delete(path);
		}
	}

	[TestMethod]
	public async Task RunAsync_RunsInTheGivenWorkingDirectory()
	{
		(string fileName, IReadOnlyList<string> arguments) = Shell(OperatingSystem.IsWindows() ? "cd" : "pwd");
		ProcessResult result = await _runner.RunAsync(fileName, arguments, _tempDir).ConfigureAwait(false);

		Assert.AreEqual(0, result.ExitCode);
		AssertSameDirectory(_tempDir, result.StandardOutput);
	}

	[TestMethod]
	public async Task RunAsync_RelativeWorkingDirectory_IsResolvedAgainstTheCurrentDirectory()
	{
		string leaf = Path.GetFileName(_tempDir);
		string parent = Path.GetDirectoryName(_tempDir)!;
		string original = Directory.GetCurrentDirectory();

		try
		{
			Directory.SetCurrentDirectory(parent);
			(string fileName, IReadOnlyList<string> arguments) = Shell(OperatingSystem.IsWindows() ? "cd" : "pwd");
			ProcessResult result = await _runner.RunAsync(fileName, arguments, leaf).ConfigureAwait(false);

			Assert.AreEqual(0, result.ExitCode);
			AssertSameDirectory(_tempDir, result.StandardOutput);
		}
		finally
		{
			Directory.SetCurrentDirectory(original);
		}
	}

	[TestMethod]
	public async Task RunAsync_StringOverload_SplitsArgumentsTheWayTheCallerJoinedThem()
	{
		// The string overloads are what every existing call site uses, so the join has to survive
		// the trip back apart.
		string script = OperatingSystem.IsWindows() ? "echo hello there" : "echo hello there";
		string arguments = OperatingSystem.IsWindows() ? $"/c \"{script}\"" : $"-c \"{script}\"";
		string fileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";

		ProcessResult result = await _runner.RunAsync(fileName, arguments, _tempDir).ConfigureAwait(false);

		Assert.AreEqual(0, result.ExitCode);
		Assert.AreEqual("hello there", result.StandardOutput.Trim());
	}

	[TestMethod]
	public async Task RunAsync_ListOverload_PassesAnArgumentContainingSpacesAsOneValue()
	{
		// Nothing is quoted by the caller, and nothing needs to be: the value arrives whole.
		string marker = Path.Combine(_tempDir, "a file with spaces.txt");
		await File.WriteAllTextAsync(marker, "content").ConfigureAwait(false);

		// The path is its own element in both cases, with no quoting written by hand anywhere.
		(string fileName, IReadOnlyList<string> arguments) = OperatingSystem.IsWindows()
			? ("cmd.exe", (IReadOnlyList<string>)["/c", "type", marker])
			: ("/bin/cat", [marker]);

		ProcessResult result = await _runner.RunAsync(fileName, arguments, _tempDir).ConfigureAwait(false);

		Assert.AreEqual(0, result.ExitCode, result.CombinedOutput);
		Assert.AreEqual("content", result.StandardOutput.Trim());
	}

	[TestMethod]
	public async Task RunWithCallbackAsync_WhenCancelled_KillsTheChildInsteadOfAbandoningIt()
	{
		// The regression this whole adapter exists for. The previous implementation only stopped
		// awaiting WaitForExitAsync, so a cancelled run left its child running: for a build tool
		// that means an orphaned compiler or test host still holding file locks. The marker file is
		// written only if the child survives past the cancellation, so its absence is the assertion.
		// The child writes its marker shortly after cancellation and only then settles into a long
		// sleep. The short delay is what makes this a real test: a child that is merely abandoned
		// gets to the marker well before the assertion, while a killed one never does.
		string marker = Path.Combine(_tempDir, "child-survived.txt");
		string script = OperatingSystem.IsWindows()
			? $"ping -n 4 127.0.0.1 >nul & type nul >\"{marker}\" & ping -n 21 127.0.0.1 >nul"
			: $"sleep 3; : > '{marker}'; sleep 20";

		(string fileName, IReadOnlyList<string> arguments) = Shell(script);
		using CancellationTokenSource cts = new();

		Task<int> running = _runner.RunWithCallbackAsync(fileName, arguments, _tempDir, null, null, cts.Token);
		await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
		await cts.CancelAsync().ConfigureAwait(false);

		await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => running).ConfigureAwait(false);

		// Well past the point the child would have written the marker had it been left alone.
		await Task.Delay(TimeSpan.FromSeconds(6)).ConfigureAwait(false);
		Assert.IsFalse(
			File.Exists(marker),
			"The child process outlived cancellation and wrote its marker, so it was abandoned rather than killed.");
	}

	private static string[] SplitLines(string value) =>
		[.. value.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim())];

	private static string[] Trimmed(IEnumerable<string> lines) => [.. lines.Select(l => l.Trim())];
}
