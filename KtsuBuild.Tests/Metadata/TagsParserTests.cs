// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace KtsuBuild.Tests.Metadata;

using KtsuBuild.Metadata;
using KtsuBuild.Tests.Helpers;

[TestClass]
public class TagsParserTests
{
	private static readonly string[] ThreeTopics = ["extensions", "collection", "utility"];
	private static readonly string[] HyphenatedTopics = ["my-library", "code-helper"];
	private static readonly string[] SanitizedTopics = ["c", "dotnet", "mytag", "valid-tag"];
	private static readonly string[] TwoTopics = ["dotnet", "csharp"];

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
}
