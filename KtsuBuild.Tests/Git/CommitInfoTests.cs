// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Tests.Git;

using KtsuBuild.Git;

[TestClass]
public class CommitInfoTests
{
	private static CommitInfo Commit(string author, string email) =>
		new() { Hash = "abc123", Subject = "Fix bug", Author = author, AuthorEmail = email };

	[TestMethod]
	[DataRow("12345+jdoe@users.noreply.github.com", "jdoe")]
	[DataRow("jdoe@users.noreply.github.com", "jdoe")]
	[DataRow("12345+Jane-Doe@Users.NoReply.GitHub.com", "Jane-Doe")]
	public void GitHubLogin_NoreplyEmail_ReturnsLogin(string email, string login) =>
		Assert.AreEqual(login, Commit("Jane Doe", email).GitHubLogin);

	[TestMethod]
	[DataRow("")]
	[DataRow("noreply@anthropic.com")]
	[DataRow("jane@example.com")]
	[DataRow("49699333+dependabot[bot]@users.noreply.github.com")]
	[DataRow("41898282+github-actions[bot]@users.noreply.github.com")]
	[DataRow("-jdoe@users.noreply.github.com")]
	[DataRow("jdoe@users.noreply.github.com.example.com")]
	public void GitHubLogin_OtherEmail_ReturnsNull(string email) =>
		Assert.IsNull(Commit("Jane Doe", email).GitHubLogin);

	[TestMethod]
	public void FormattedEntry_NoreplyEmail_LinksTheLoginNotTheDisplayName() =>
		Assert.AreEqual(
			"Fix bug ([@jdoe](https://github.com/jdoe))",
			Commit("Jane Doe", "12345+jdoe@users.noreply.github.com").FormattedEntry);

	[TestMethod]
	public void FormattedEntry_NoLogin_WritesThePlainName() =>
		Assert.AreEqual("Fix bug (Claude)", Commit("Claude", "noreply@anthropic.com").FormattedEntry);

	[TestMethod]
	[DataRow("Jane Doe", "jane@example.com")]
	[DataRow("Github Actions", "")]
	[DataRow("dependabot[bot]", "49699333+dependabot[bot]@users.noreply.github.com")]
	public void FormattedEntry_NeverWritesALinkWhoseDestinationHasASpaceOrBracket(string author, string email)
	{
		string entry = Commit(author, email).FormattedEntry;
		Assert.IsFalse(entry.Contains("](", StringComparison.Ordinal), entry);
		Assert.AreEqual($"Fix bug ({author})", entry);
	}
}
