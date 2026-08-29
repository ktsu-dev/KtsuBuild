// Copyright (c) 2023-2026 ktsu-dev contributors

namespace KtsuBuild.Profile;

using System.Text.RegularExpressions;
using KtsuBuild.Abstractions;
#if !NET10_0_OR_GREATER
using static Polyfill;
#endif

/// <summary>
/// Checks the repositories a profile template links to.
/// </summary>
/// <remarks>
/// The curated lists at the top of the template are written by hand, so nothing stops them pointing
/// at a repository that has since been archived. That is exactly what happened: the page promoted
/// four archived repositories, three of them in the same section, and nobody noticed because the
/// generated table below is a separate list.
/// </remarks>
public static partial class TemplateLinks
{
	private const int MatchTimeoutMilliseconds = 2000;

	/// <summary>
	/// Finds the repositories a template links to within an organization.
	/// </summary>
	/// <param name="template">The template content.</param>
	/// <param name="organization">The organization whose links are of interest.</param>
	/// <returns>The distinct repository names, in the order they first appear.</returns>
	/// <remarks>
	/// Links to other owners are ignored, as is a bare link to the organization itself.
	/// </remarks>
	public static IReadOnlyList<string> FindLinkedRepositories(string template, string organization)
	{
		Ensure.NotNull(template);
		Ensure.NotNull(organization);

		List<string> names = [];
		foreach (Match match in RepositoryLinkRegex().Matches(template))
		{
			if (!match.Groups["owner"].Value.Equals(organization, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			string name = match.Groups["repo"].Value;
			if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
			{
				names.Add(name);
			}
		}

		return names;
	}

	/// <summary>
	/// Finds the linked repositories that should no longer be promoted.
	/// </summary>
	/// <param name="template">The template content.</param>
	/// <param name="organization">The organization whose links are checked.</param>
	/// <param name="repositories">Every repository in the organization, archived ones included.</param>
	/// <returns>The names of linked repositories that are archived or no longer public, in the order
	/// they appear in the template. Empty when every link is healthy.</returns>
	public static IReadOnlyList<string> FindRetired(
		string template,
		string organization,
		IEnumerable<GitHubRepository> repositories)
	{
		Ensure.NotNull(repositories);

		Dictionary<string, GitHubRepository> known = [];
		foreach (GitHubRepository repository in repositories)
		{
			known[repository.Name] = repository;
		}

		List<string> retired = [];
		foreach (string name in FindLinkedRepositories(template, organization))
		{
			// A name the listing does not know is either archived out of the public listing, renamed,
			// or deleted. Any of those makes the link worth a second look.
			if (!known.TryGetValue(name, out GitHubRepository? repository) || repository.IsArchived)
			{
				retired.Add(name);
			}
		}

		return retired;
	}

	/// <summary>
	/// Matches a GitHub repository link, capturing its owner and name.
	/// </summary>
	/// <returns>The compiled regex.</returns>
	[GeneratedRegex(
		@"github\.com/(?<owner>[A-Za-z0-9._-]+)/(?<repo>[A-Za-z0-9._-]+)",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
		MatchTimeoutMilliseconds)]
	private static partial Regex RepositoryLinkRegex();
}
