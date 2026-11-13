// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Git;

using KtsuBuild.Abstractions;
using KtsuBuild.Git;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

[TestClass]
public class CommitAnalyzerTests
{
	private IGitService _gitService = null!;
	private CommitAnalyzer _analyzer = null!;

	[TestInitialize]
	public void Setup()
	{
		_gitService = Substitute.For<IGitService>();
		_analyzer = new CommitAnalyzer(_gitService);
	}

	private void SetupSubjects(IEnumerable<string> subjects) =>
		SetupCommits([.. subjects.Select(static (s, i) => new CommitInfo { Hash = $"c{i}", Subject = s, Author = "developer" })]);

	private void SetupCommits(IReadOnlyList<CommitInfo> commits) =>
		_gitService.GetCommitsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(commits));

	[TestMethod]
	public async Task AnalyzeAsync_NoCommitsInRange_ReturnsSkip()
	{
		// Arrange
		SetupSubjects([]);

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Skip, type);
		Assert.IsTrue(reason.Contains("No commits found"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_AllCommitsHaveSkipCi_ReturnsSkip()
	{
		// Arrange
		List<string> messages =
		[
			"Fix typo [skip ci]",
			"Update docs [ci skip]",
		];
		SetupSubjects(messages);

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Skip, type);
		Assert.IsTrue(reason.Contains("[skip ci]"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_CommitWithMajorTag_ReturnsMajor()
	{
		// Arrange
		List<string> messages =
		[
			"Breaking change [major]",
			"Fix bug",
		];
		SetupSubjects(messages);

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Major, type);
		Assert.IsTrue(reason.Contains("[major]"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_CommitWithMinorTag_ReturnsMinor()
	{
		// Arrange
		List<string> messages =
		[
			"Add new feature [minor]",
			"Fix bug",
		];
		SetupSubjects(messages);

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Minor, type);
		Assert.IsTrue(reason.Contains("[minor]"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_CommitWithPatchTag_ReturnsPatch()
	{
		// Arrange
		List<string> messages =
		[
			"Fix critical bug [patch]",
		];
		SetupSubjects(messages);

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Patch, type);
		Assert.IsTrue(reason.Contains("[patch]"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_CommitWithPreTag_ReturnsPrerelease()
	{
		// Arrange
		List<string> messages =
		[
			"Experimental feature [pre]",
		];
		SetupSubjects(messages);

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Prerelease, type);
		Assert.IsTrue(reason.Contains("[pre]"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_MajorTakesPrecedenceOverMinor()
	{
		// Arrange
		List<string> messages =
		[
			"New feature [minor]",
			"Breaking change [major]",
		];
		SetupSubjects(messages);

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Major, type);
		Assert.IsTrue(reason.Contains("[major]"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_BotCommitsAreFiltered_ReturnsPrerelease()
	{
		// Arrange
		SetupCommits([
			new CommitInfo { Hash = "a", Subject = "[bot][skip ci] Update Metadata", Author = "github-actions[bot]" },
			new CommitInfo { Hash = "b", Subject = "Bump Polyfill from 11.4.0 to 11.4.1", Author = "dependabot[bot]" },
			new CommitInfo { Hash = "c", Subject = "ProjectDirector update", Author = "developer" },
		]);
		_gitService.GetDiffAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(string.Empty));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Prerelease, type);
		Assert.IsTrue(reason.Contains("No significant changes"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_HumanCommitMentioningGitHub_ReturnsPatch()
	{
		// Arrange
		SetupCommits([
			new CommitInfo { Hash = "a", Subject = "Fix GitHubApiClient retry", Author = "developer" },
		]);
		_gitService.GetDiffAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(string.Empty));

		// Act
		(VersionType type, _) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Patch, type);
	}

	[TestMethod]
	public async Task AnalyzeAsync_PrMergeCommitsFiltered_ReturnsPrerelease()
	{
		// Arrange
		List<string> messages =
		[
			"Merge pull request #123 from feature",
			"Merge branch 'main' into release",
		];
		SetupSubjects(messages);
		_gitService.GetDiffAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(string.Empty));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Prerelease, type);
	}

	[TestMethod]
	public async Task AnalyzeAsync_DiffAddsPublicClass_ReturnsMinor()
	{
		// Arrange
		List<string> messages =
		[
			"Add new service class",
		];
		string diff = @"
+public class NewService
+{
+    public void DoWork() { }
+}
";
		SetupSubjects(messages);
		_gitService.GetDiffAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(diff));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Minor, type);
		Assert.IsTrue(reason.Contains("API changes"));
	}

	private async Task<VersionType> AnalyzeDiffAsync(string diff)
	{
		SetupSubjects(["Change code"]);
		_gitService.GetDiffAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(diff));
		(VersionType type, string _) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);
		return type;
	}

	[TestMethod]
	public async Task AnalyzeAsync_TestOnlyDiff_IsNotAnApiChange()
	{
		// c6a95ec: a test-only commit whose public test class forced a minor bump (#173).
		string diff = """
			diff --git a/KtsuBuild.Tests/Commands/CiCommandTests.cs b/KtsuBuild.Tests/Commands/CiCommandTests.cs
			new file mode 100644
			--- /dev/null
			+++ b/KtsuBuild.Tests/Commands/CiCommandTests.cs
			@@ -0,0 +1,9 @@
			+namespace KtsuBuild.Tests.Commands;
			+
			+[TestClass]
			+public class CiCommandTests
			+{
			+	[TestMethod]
			+	public async Task Ci_RunsThePipeline() { }
			+	public CiCommandTests() { }
			+}
			""";

		Assert.AreEqual(VersionType.Patch, await AnalyzeDiffAsync(diff).ConfigureAwait(false));
	}

	[TestMethod]
	public async Task AnalyzeAsync_NullablePropertyAndAsyncGenericMethod_AreApiChanges()
	{
		// 52e94e6: real public API that matched none of the old patterns (#173).
		string diff = """
			diff --git a/KtsuBuild/Metadata/MetadataUpdateOptions.cs b/KtsuBuild/Metadata/MetadataUpdateOptions.cs
			--- a/KtsuBuild/Metadata/MetadataUpdateOptions.cs
			+++ b/KtsuBuild/Metadata/MetadataUpdateOptions.cs
			@@ -20,3 +20,5 @@
			+	public VersionType? ForcedVersionType { get; set; }
			diff --git a/KtsuBuild/Metadata/MetadataService.cs b/KtsuBuild/Metadata/MetadataService.cs
			--- a/KtsuBuild/Metadata/MetadataService.cs
			+++ b/KtsuBuild/Metadata/MetadataService.cs
			@@ -40,1 +40,1 @@
			-	public async Task<MetadataUpdateResult> UpdateMetadataAsync(MetadataUpdateOptions options)
			+	public async Task<MetadataUpdateResult> UpdateMetadataAsync(MetadataUpdateOptions options, CancellationToken cancellationToken = default)
			""";

		Assert.AreEqual(VersionType.Minor, await AnalyzeDiffAsync(diff).ConfigureAwait(false));
	}

	[TestMethod]
	[DataRow("+\tpublic VersionType? ForcedVersionType { get; set; }")]
	[DataRow("+\tpublic async Task<MetadataUpdateResult> UpdateMetadataAsync(MetadataUpdateOptions options)")]
	[DataRow("+\tpublic static string Describe(int value) => value.ToString();")]
	[DataRow("+\tpublic IReadOnlyList<string> Names { get; } = [];")]
	[DataRow("+\tpublic Dictionary<string, List<int>> Lookup() => [];")]
	[DataRow("+\tpublic string[] Arguments { get; init; } = [];")]
	[DataRow("+\tpublic (int Major, int Minor) Parts() => (1, 2);")]
	[DataRow("+\tpublic T? Find<T>(string key) where T : class => null;")]
	[DataRow("+\tpublic static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);")]
	[DataRow("+\tprotected virtual void OnChanged() { }")]
	[DataRow("+\tprotected internal override string ToString() => string.Empty;")]
	[DataRow("+\tpublic required string Name { get; init; }")]
	[DataRow("+\tpublic event EventHandler? Changed;")]
	[DataRow("+\tpublic const int Limit = 3;")]
	[DataRow("+public static class Extensions")]
	[DataRow("+public sealed record Point(int X, int Y);")]
	[DataRow("+public delegate void Handler(object sender);")]
	[DataRow("+\tpublic Widget(string name) { }")]
	[DataRow("-\tpublic static string Describe(int value) => value.ToString();")]
	public async Task AnalyzeAsync_PublicMemberShapes_AreApiChanges(string line)
	{
		string diff = $"""
			diff --git a/Lib/Widget.cs b/Lib/Widget.cs
			--- a/Lib/Widget.cs
			+++ b/Lib/Widget.cs
			@@ -1,1 +1,1 @@
			{line}
			""";

		Assert.AreEqual(VersionType.Minor, await AnalyzeDiffAsync(diff).ConfigureAwait(false), line);
	}

	[TestMethod]
	[DataRow("+\tinternal static string Describe(int value) => value.ToString();")]
	[DataRow("+\tprivate protected void Reset() { }")]
	[DataRow("+\t// public void Commented() { }")]
	[DataRow("+\treturn Describe(publicValue);")]
	public async Task AnalyzeAsync_NonPublicLines_AreNotApiChanges(string line)
	{
		string diff = $"""
			diff --git a/Lib/Widget.cs b/Lib/Widget.cs
			--- a/Lib/Widget.cs
			+++ b/Lib/Widget.cs
			@@ -1,1 +1,1 @@
			{line}
			""";

		Assert.AreEqual(VersionType.Patch, await AnalyzeDiffAsync(diff).ConfigureAwait(false), line);
	}

	[TestMethod]
	[DataRow("Foo.Test/FooTests.cs", true)]
	[DataRow("Foo.Tests/Sub/FooTests.cs", true)]
	[DataRow("Foo.UnitTests/FooTests.cs", true)]
	[DataRow("tests/Foo.Integration/FooTests.cs", true)]
	[DataRow("Foo/FooTests.cs", false)]
	[DataRow("Foo.Testing/Helpers.cs", false)]
	[DataRow("Tests.cs", false)]
	public void IsTestPath_RecognisesTestProjectsAndFolders(string path, bool expected) =>
		Assert.AreEqual(expected, CommitAnalyzer.IsTestPath(path), path);

	[TestMethod]
	public async Task AnalyzeAsync_DiffModifiesInternalCodeOnly_ReturnsPatch()
	{
		// Arrange
		List<string> messages =
		[
			"Optimize internal algorithm",
		];
		string diff = @"
-internal int Calculate() => x + y;
+internal int Calculate() => x * 2 + y;
";
		SetupSubjects(messages);
		_gitService.GetDiffAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromResult(diff));

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Patch, type);
		Assert.IsTrue(reason.Contains("patch"));
	}

	[TestMethod]
	public async Task AnalyzeAsync_MinorTakesPrecedenceOverPatch()
	{
		// Arrange
		List<string> messages =
		[
			"Fix bug [patch]",
			"Add feature [minor]",
		];
		SetupSubjects(messages);

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Minor, type);
	}

	[TestMethod]
	public async Task AnalyzeAsync_PatchTakesPrecedenceOverPre()
	{
		// Arrange
		List<string> messages =
		[
			"Experimental [pre]",
			"Important fix [patch]",
		];
		SetupSubjects(messages);

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Patch, type);
	}

	[TestMethod]
	public async Task AnalyzeAsync_CaseInsensitiveTags()
	{
		// Arrange
		List<string> messages =
		[
			"Breaking change [MAJOR]",
		];
		SetupSubjects(messages);

		// Act
		(VersionType type, string reason) = await _analyzer.AnalyzeAsync("/repo", "abc..def").ConfigureAwait(false);

		// Assert
		Assert.AreEqual(VersionType.Major, type);
	}
}
