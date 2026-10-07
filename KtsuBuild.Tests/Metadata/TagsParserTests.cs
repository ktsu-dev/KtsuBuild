// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Metadata;

using System.Globalization;
using System.Text.RegularExpressions;
using KtsuBuild.Metadata;
using KtsuBuild.Tests.Helpers;
using KtsuBuild.Tests.Mocks;

[TestClass]
public partial class TagsParserTests
{
	private static readonly string[] ThreeTopics = ["extensions", "collection", "utility"];
	private static readonly string[] HyphenatedTopics = ["my-library", "code-helper"];
	private static readonly string[] SanitizedTopics = ["c", "dotnet", "mytag", "valid-tag"];
	private static readonly string[] TwoTopics = ["dotnet", "csharp"];
	private static readonly string[] HyphenTrimmedTopics = ["foo", "bar"];
	private static readonly string[] OneTopic = ["dotnet"];

	private string _tempDir = null!;

	[TestInitialize]
	public void Setup() => _tempDir = TestHelpers.CreateTempDir("TagsParser");

	[TestCleanup]
	public void Cleanup()
	{
		if (Directory.Exists(_tempDir))
		{
			Directory.Delete(_tempDir, recursive: true);
		}
	}

	[TestMethod]
	public async Task ParseAsync_SemicolonSeparated_ReturnsParsedTopics()
	{
		string tagsFile = Path.Combine(_tempDir, "TAGS.md");
		await File.WriteAllTextAsync(tagsFile, "extensions;collection;utility").ConfigureAwait(false);

		IReadOnlyList<string> topics = await TagsParser.ParseAsync(tagsFile).ConfigureAwait(false);

		CollectionAssert.AreEqual(ThreeTopics, topics.ToList());
	}

	[TestMethod]
	public async Task ParseAsync_TrimsWhitespace()
	{
		string tagsFile = Path.Combine(_tempDir, "TAGS.md");
		await File.WriteAllTextAsync(tagsFile, "  extensions ; collection ; utility  ").ConfigureAwait(false);

		IReadOnlyList<string> topics = await TagsParser.ParseAsync(tagsFile).ConfigureAwait(false);

		CollectionAssert.AreEqual(ThreeTopics, topics.ToList());
	}

	[TestMethod]
	public async Task ParseAsync_ConvertsToLowercase()
	{
		string tagsFile = Path.Combine(_tempDir, "TAGS.md");
		await File.WriteAllTextAsync(tagsFile, "Extensions;COLLECTION;Utility").ConfigureAwait(false);

		IReadOnlyList<string> topics = await TagsParser.ParseAsync(tagsFile).ConfigureAwait(false);

		CollectionAssert.AreEqual(ThreeTopics, topics.ToList());
	}

	[TestMethod]
	public async Task ParseAsync_ReplacesSpacesWithHyphens()
	{
		string tagsFile = Path.Combine(_tempDir, "TAGS.md");
		await File.WriteAllTextAsync(tagsFile, "my library;code helper").ConfigureAwait(false);

		IReadOnlyList<string> topics = await TagsParser.ParseAsync(tagsFile).ConfigureAwait(false);

		CollectionAssert.AreEqual(HyphenatedTopics, topics.ToList());
	}

	[TestMethod]
	public async Task ParseAsync_RemovesInvalidCharacters()
	{
		string tagsFile = Path.Combine(_tempDir, "TAGS.md");
		await File.WriteAllTextAsync(tagsFile, "c#;dotnet!;my_tag;valid-tag").ConfigureAwait(false);

		IReadOnlyList<string> topics = await TagsParser.ParseAsync(tagsFile).ConfigureAwait(false);

		CollectionAssert.AreEqual(SanitizedTopics, topics.ToList());
	}

	[TestMethod]
	public async Task ParseAsync_FileNotFound_ReturnsEmptyList()
	{
		IReadOnlyList<string> topics = await TagsParser.ParseAsync(Path.Combine(_tempDir, "nonexistent.md")).ConfigureAwait(false);

		Assert.AreEqual(0, topics.Count);
	}

	[TestMethod]
	public async Task ParseAsync_EmptyFile_ReturnsEmptyList()
	{
		string tagsFile = Path.Combine(_tempDir, "TAGS.md");
		await File.WriteAllTextAsync(tagsFile, "").ConfigureAwait(false);

		IReadOnlyList<string> topics = await TagsParser.ParseAsync(tagsFile).ConfigureAwait(false);

		Assert.AreEqual(0, topics.Count);
	}

	[TestMethod]
	public async Task ParseAsync_DeduplicatesTopics()
	{
		string tagsFile = Path.Combine(_tempDir, "TAGS.md");
		await File.WriteAllTextAsync(tagsFile, "dotnet;csharp;dotnet;csharp").ConfigureAwait(false);

		IReadOnlyList<string> topics = await TagsParser.ParseAsync(tagsFile).ConfigureAwait(false);

		CollectionAssert.AreEqual(TwoTopics, topics.ToList());
	}

	[TestMethod]
	public void Parse_WhitespaceOnly_ReturnsEmptyList()
	{
		IReadOnlyList<string> topics = TagsParser.Parse("   ");

		Assert.AreEqual(0, topics.Count);
	}

	[TestMethod]
	public void Parse_EmptySemicolonSegments_FilteredOut()
	{
		IReadOnlyList<string> topics = TagsParser.Parse(";;dotnet;;;csharp;;");

		CollectionAssert.AreEqual(TwoTopics, topics.ToList());
	}

	private static string ManyTags(int count) =>
		string.Join(";", Enumerable.Range(1, count).Select(static n => $"tag{n.ToString(CultureInfo.InvariantCulture)}"));

	[TestMethod]
	public void Parse_MoreThanTwentyTopics_KeepsTheFirstTwenty()
	{
		// GitHub rejects a topics update carrying more than 20 entries, and rejecting it leaves every
		// topic stale rather than just the extras.
		IReadOnlyList<string> topics = TagsParser.Parse(ManyTags(25));

		Assert.HasCount(20, topics);
		Assert.AreEqual("tag1", topics[0]);
		Assert.AreEqual("tag20", topics[^1]);
	}

	[TestMethod]
	public void Parse_MoreThanTwentyTopics_WarnsNamingTheDroppedOnes()
	{
		RecordingBuildLogger log = new();

		TagsParser.Parse(ManyTags(22), log);

		Assert.HasCount(1, log.Warnings);
		Assert.IsTrue(RecordingBuildLogger.Any(log.Warnings, "tag21, tag22"));
	}

	[TestMethod]
	public void Parse_TwentyTopics_DoesNotWarn()
	{
		RecordingBuildLogger log = new();

		Assert.HasCount(20, TagsParser.Parse(ManyTags(20), log));
		Assert.IsEmpty(log.Warnings);
	}

	[TestMethod]
	public void Parse_DuplicatesDoNotCountTowardTheLimit()
	{
		IReadOnlyList<string> topics = TagsParser.Parse($"{ManyTags(20)};{ManyTags(20)}");

		Assert.HasCount(20, topics);
	}

	[TestMethod]
	public void Parse_TrimsLeadingAndTrailingHyphens()
	{
		CollectionAssert.AreEqual(HyphenTrimmedTopics, TagsParser.Parse("-foo;bar-").ToList());
	}

	[TestMethod]
	public void Parse_DropsTopicsThatAreOnlyHyphens()
	{
		CollectionAssert.AreEqual(OneTopic, TagsParser.Parse("---;dotnet; - ").ToList());
	}

	[TestMethod]
	public void Parse_EveryTopicMatchesGitHubsRules()
	{
		IReadOnlyList<string> topics = TagsParser.Parse("-foo;.net;c#;_bar;--x--;Hello World;a_b-c;-;ok-");

		Assert.IsNotEmpty(topics);
		foreach (string topic in topics)
		{
			Assert.IsTrue(GitHubTopic().IsMatch(topic), $"'{topic}' is not a valid GitHub topic");
		}
	}

	[GeneratedRegex("^[a-z0-9][a-z0-9-]{0,49}$")]
	private static partial Regex GitHubTopic();
}
