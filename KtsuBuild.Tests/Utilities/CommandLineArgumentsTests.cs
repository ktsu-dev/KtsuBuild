// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Utilities;

using KtsuBuild.Utilities;

/// <summary>
/// Tests for the argument splitter that <c>ProcessRunner</c>'s string overloads use.
/// </summary>
/// <remarks>
/// These matter more than their size suggests. Every existing call site passes one pre-joined
/// argument string, and the splitter is what turns it back into the values handed to the child
/// process. Getting a rule wrong here changes what a shipped command runs, quietly.
/// </remarks>
[TestClass]
public class CommandLineArgumentsTests
{
	/// <summary>Asserts that <paramref name="arguments"/> splits into exactly <paramref name="expected"/>.</summary>
	private static void AssertSplit(string arguments, params string[] expected) =>
		CollectionAssert.AreEqual(expected, CommandLineArguments.Split(arguments));

	[TestMethod]
	public void Split_Empty_ReturnsNoArguments()
	{
		AssertSplit("");
		AssertSplit("   \t  ");
	}

	[TestMethod]
	public void Split_PlainWords_SplitsOnWhitespace() =>
		AssertSplit("rev-parse HEAD", "rev-parse", "HEAD");

	[TestMethod]
	public void Split_RunsOfWhitespace_ProduceNoEmptyArguments() =>
		AssertSplit("  a \t\t b   c  ", "a", "b", "c");

	[TestMethod]
	public void Split_QuotedValue_KeepsSpacesAndDropsQuotes() =>
		AssertSplit("commit -m \"a message with spaces\"", "commit", "-m", "a message with spaces");

	[TestMethod]
	public void Split_QuoteInsideToken_GroupsWithoutSplittingTheToken() =>
		// GitService builds exactly this shape: --pretty=format:"..." as one token.
		AssertSplit(
			"log --pretty=format:\"%h %s\" \"v1.0.0..HEAD\"",
			"log", "--pretty=format:%h %s", "v1.0.0..HEAD");

	[TestMethod]
	public void Split_EscapedQuote_IsContentNotADelimiter() =>
		// 2n+1 backslashes before a quote: n backslashes and a literal quote.
		AssertSplit("say \"he said \\\"hi\\\"\"", "say", "he said \"hi\"");

	[TestMethod]
	public void Split_EvenBackslashesBeforeQuote_HalveAndTheQuoteDelimits() =>
		// 2n backslashes before a quote: n backslashes, and the quote still groups.
		AssertSplit(@"""C:\path\\"" next", @"C:\path\", "next");

	[TestMethod]
	public void Split_BackslashesNotBeforeQuote_StayLiteral() =>
		AssertSplit(@"""C:\Program Files\tool"" a\\b", @"C:\Program Files\tool", @"a\\b");

	[TestMethod]
	public void Split_DoubledQuoteInsideQuotes_IsALiteralQuote() =>
		AssertSplit("\"a\"\"b\"", "a\"b");

	[TestMethod]
	public void Split_UnterminatedQuote_TakesTheRestOfTheString() =>
		AssertSplit("tag \"unfinished value", "tag", "unfinished value");

	[TestMethod]
	public void Split_EmptyQuotedArgument_IsPreserved() =>
		AssertSplit("config \"\" value", "config", "", "value");

	[TestMethod]
	public void Split_CallSiteShapes_RoundTripToTheValuesTheCallerMeant()
	{
		// The patterns the repository actually builds today, each asserted as the argument vector
		// the caller intends.
		AssertSplit("config versionsort.suffix \"-pre\"", "config", "versionsort.suffix", "-pre");

		AssertSplit(
			"tag -a \"v1.0.0\" \"abc123\" -m \"Release v1.0.0\"",
			"tag", "-a", "v1.0.0", "abc123", "-m", "Release v1.0.0");

		AssertSplit(
			"api \"repos/ktsu-dev/Sdk/actions/workflows/ci.yml/runs?per_page=1\"",
			"api", "repos/ktsu-dev/Sdk/actions/workflows/ci.yml/runs?per_page=1");

		AssertSplit("-f \"/tmp/some path/file.txt\"", "-f", "/tmp/some path/file.txt");
	}
}
