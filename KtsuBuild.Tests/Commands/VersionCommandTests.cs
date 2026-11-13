// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Commands;

using System.CommandLine;
using KtsuBuild.Tool;
using KtsuBuild.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Runs <c>version bump</c> and <c>version show</c> against a real repository and captures what
/// they write, because their standard output is what a script captures as the result.
/// </summary>
[TestClass]
[DoNotParallelize]
public class VersionCommandTests
{
	private static readonly string[] ShowResultLabels = ["Current Version:", "Last Tag:", "Last Version:", "Version Increment:", "Reason:", "Is Prerelease:"];

	private string _repo = null!;

	/// <summary>
	/// Gets or sets the context MSTest injects, whose cancellation token the commands run under.
	/// </summary>
	public TestContext TestContext { get; set; } = null!;

	[TestInitialize]
	public async Task Setup()
	{
		_repo = Path.Combine(Path.GetTempPath(), $"ktsubuild-version-{Guid.NewGuid():N}");
		Directory.CreateDirectory(_repo);
		ProcessRunner runner = new();
		await RunGitAsync(runner, "init", "-q").ConfigureAwait(false);
		await RunGitAsync(runner, "-c", "user.name=test", "-c", "user.email=test@example.com", "commit", "-q", "--allow-empty", "-m", "initial").ConfigureAwait(false);
		await RunGitAsync(runner, "tag", "v1.2.3").ConfigureAwait(false);
		await RunGitAsync(runner, "-c", "user.name=test", "-c", "user.email=test@example.com", "commit", "-q", "--allow-empty", "-m", "Fix a bug").ConfigureAwait(false);
	}

	[TestCleanup]
	public void Cleanup()
	{
		try
		{
			foreach (string file in Directory.EnumerateFiles(_repo, "*", SearchOption.AllDirectories))
			{
				File.SetAttributes(file, FileAttributes.Normal);
			}

			Directory.Delete(_repo, recursive: true);
		}
		catch (IOException ex)
		{
			// Leaving a temp repository behind must not fail the test; report it instead.
			TestContext.WriteLine($"Could not delete {_repo}: {ex.Message}");
		}
		catch (UnauthorizedAccessException ex)
		{
			TestContext.WriteLine($"Could not delete {_repo}: {ex.Message}");
		}
	}

	[TestMethod]
	public async Task Bump_WritesOnlyTheVersionToStandardOutput()
	{
		(int exitCode, string stdout, string stderr) = await InvokeAsync("version", "bump", "--workspace", _repo).ConfigureAwait(false);

		Assert.AreEqual(0, exitCode, stderr);
		string[] lines = [.. stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r'))];
		Assert.AreEqual(1, lines.Length, $"Expected the version alone on stdout, got:\n{stdout}");
		StringAssert.StartsWith(lines[0], "1.2.4", $"Unexpected version line: {lines[0]}");
		StringAssert.Contains(stderr, "Analyzing Version Information", "The diagnostics should still be written, to standard error.");
	}

	[TestMethod]
	public async Task Show_WritesOnlyTheDocumentedResultLinesToStandardOutput()
	{
		(int exitCode, string stdout, string stderr) = await InvokeAsync("version", "show", "--workspace", _repo).ConfigureAwait(false);

		Assert.AreEqual(0, exitCode, stderr);
		string[] lines = [.. stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r'))];
		CollectionAssert.AreEqual(
			ShowResultLabels,
			(string[])[.. lines.Select(l => l[..(l.IndexOf(':', StringComparison.Ordinal) + 1)])],
			$"Expected the six result lines alone on stdout, got:\n{stdout}");
	}

	private async Task<(int ExitCode, string Stdout, string Stderr)> InvokeAsync(params string[] args)
	{
		RootCommand root = [];
		Program.AddVersionCommand(root, new ProcessRunner(), new BuildLogger());

		TextWriter originalOut = Console.Out;
		TextWriter originalError = Console.Error;
		using StringWriter stdout = new();
		using StringWriter stderr = new();
		Console.SetOut(stdout);
		Console.SetError(stderr);
		try
		{
			int exitCode = await root.Parse(args).InvokeAsync(configuration: null, cancellationToken: TestContext.CancellationToken).ConfigureAwait(false);
			return (exitCode, stdout.ToString(), stderr.ToString());
		}
		finally
		{
			Console.SetOut(originalOut);
			Console.SetError(originalError);
		}
	}

	private async Task RunGitAsync(ProcessRunner runner, params string[] args)
	{
		ProcessResult result = await runner.RunAsync("git", args, _repo, TestContext.CancellationToken).ConfigureAwait(false);
		Assert.IsTrue(result.Success, $"git {string.Join(' ', args)} failed: {result.StandardError}");
	}
}
